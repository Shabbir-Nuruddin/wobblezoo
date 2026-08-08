using System;
using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Three things you can spend to make a board easier.
    ///
    /// The levels are deliberately gentle now, so power-ups are not a difficulty
    /// valve — they are the payoff for the dorm. That is the whole loop:
    ///
    ///     clear levels  ->  snacks + stars
    ///     stars         ->  new friends move in
    ///     friends       ->  one free play each per day
    ///     play          ->  MAYBE a power-up, usually a snack
    ///     snacks        ->  the shop, which also sells power-ups
    ///     power-ups     ->  levels get easier
    ///
    /// which is what finally gives unlocking an animal a reason beyond a nicer
    /// picture. Ten friends home is ten rolls a day; one friend is one.
    ///
    /// Design rules:
    ///
    ///   * NOTHING IS BOUGHT WITH MONEY, and there is no second currency. Power-ups
    ///     are earned by playing with animals you already own, or bought with the
    ///     same snacks that buy furniture.
    ///
    ///   * THEY NEVER EXPIRE and there is no cap. A stock of thirty Pillows is a
    ///     perfectly good way to play; it is not an exploit to be closed.
    ///
    ///   * USING ONE COSTS NOTHING. No star penalty, no timer, no apology. An
    ///     earlier version capped a helped level at two stars; it reads as fair and
    ///     plays badly, because it turns every power-up into a "should I?" and
    ///     hesitancy is the exact feeling this game exists to remove. The balance is
    ///     SUPPLY, handled below — never a penalty at the point of use.
    ///
    /// ---------------------------------------------------------------------------
    /// THE SUPPLY MATHS (this is the balance, so it is written down)
    ///
    /// Playing with a friend used to hand over a power-up every single time. One
    /// friend was one guaranteed power-up a day and ten friends were ten, so by the
    /// middle of the game the shelf was permanently full and nothing on it meant
    /// anything. Now a play is a ROLL, and the odds fall as the dorm fills up:
    ///
    ///     friends home   1     2     3     4     5    ...   10
    ///     chance         30%   15%   10%   10%   10%        10%
    ///     rolls/day      1     2     3     4     5          10
    ///     expected/day   0.30  0.30  0.30  0.40  0.50       1.00
    ///
    /// which is `max(10, 30 / friends)` — 30% at one friend, halving to 15% at two,
    /// then settling on a 10% floor. The floor is what lets the expected haul grow
    /// gently with the roster (so unlocking a friend still means something) while
    /// the per-play chance never climbs back up.
    ///
    /// Read across the whole game that averages out to about 12.5% a play, and a
    /// player with the full dorm finds ONE on a typical day, two on a good one
    /// (26% of days), three occasionally (7%). Which is the point: a power-up is
    /// something you save up for and choose a moment for, not something you have
    /// six of and forget about.
    ///
    /// A miss is never nothing — it pays a snack (see PlayWith), so opening the dorm
    /// is always worth it and the shop always creeps closer.
    /// ---------------------------------------------------------------------------
    /// </summary>
    public static class PowerUps
    {
        public enum Kind { Pillow, Lullaby, Tidy }
        public const int Count = 3;

        public static string Name(Kind k) =>
            k == Kind.Pillow ? "Pillow" : k == Kind.Lullaby ? "Lullaby" : "Tidy up";

        /// One line, written as what it DOES to the board rather than as a stat.
        public static string Blurb(Kind k) =>
            k == Kind.Pillow ? "Pick a friend. They stay put for one swipe."
          : k == Kind.Lullaby ? "Pick a friend. They go straight to bed."
          : "Pick a block. It tidies itself away.";

        /// The instruction shown while the power-up is armed and waiting for a tap.
        public static string Prompt(Kind k) =>
            k == Kind.Pillow ? "Tap a friend to hold them still"
          : k == Kind.Lullaby ? "Tap a friend to send them to bed"
          : "Tap a block to tidy it away";

        private static string Key(Kind k) => "pu_" + (int)k;

        public static int Have(Kind k) => Mathf.Max(0, PlayerPrefs.GetInt(Key(k), StartingStock));
        public static void Grant(Kind k, int n = 1)
        {
            PlayerPrefs.SetInt(Key(k), Have(k) + Mathf.Max(0, n));
            PlayerPrefs.Save();
        }

        /// Spends one. Returns false (and changes nothing) when the shelf is empty.
        public static bool Spend(Kind k)
        {
            int n = Have(k);
            if (n <= 0) return false;
            PlayerPrefs.SetInt(Key(k), n - 1);
            PlayerPrefs.Save();
            return true;
        }

        /// One of each to start with, so the first one is discovered by using it
        /// rather than by reading about it somewhere — and the tutorial has something
        /// real to point at. One, not two: the whole point of the odds above is that
        /// the shelf should look worth protecting from the very first level.
        private const int StartingStock = 1;

        public static int Total()
        {
            int n = 0;
            for (int i = 0; i < Count; i++) n += Have((Kind)i);
            return n;
        }

        // ---- earning them: one roll per friend per day ----
        // "Today" is the device's own date, so this works with no network at all.
        // Missing a day costs nothing: there is no streak here and nothing is lost,
        // you simply did not collect. That is the difference between a reward and a
        // chore, and this game does not do chores.
        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        public static bool CanPlayWith(int pal) =>
            Zoo.Unlocked(pal) && PlayerPrefs.GetString("play_" + pal, "") != Today;

        /// The odds of a play turning up a power-up, given how many friends live
        /// here. See the table in the class comment — this one line IS the balance.
        public static int ChancePercent(int friends)
        {
            if (friends <= 1) return 30;
            return Mathf.Max(10, 30 / friends);   // 2 -> 15%, 3+ -> 10%
        }

        /// Today's odds, for the dorm to show honestly rather than making the player
        /// guess why some plays pay out and some don't.
        public static int ChanceToday() => ChancePercent(Zoo.UnlockedCount());

        /// The snacks a play pays when it doesn't turn up a power-up. Small, but it
        /// means opening the dorm is never a wasted trip.
        public const int ConsolationSnacks = 2;

        /// What a play turned up. `kind` is only meaningful when `found` is true.
        public struct Prize { public bool found; public Kind kind; public int snacks; }

        /// Plays with a friend and hands back whatever they found.
        ///
        /// BOTH the roll and the kind come from a hash of (today, this friend), not
        /// from Random — so the answer is the same if the screen redraws, and backing
        /// out of the dorm and coming back cannot reroll a miss into a hit.
        public static Prize PlayWith(int pal)
        {
            uint h = Hash(Today + "#" + pal);
            bool found = (int)(h % 100u) < ChancePercent(Zoo.UnlockedCount());
            var k = (Kind)(int)((h / 100u) % (uint)Count);

            PlayerPrefs.SetString("play_" + pal, Today);
            if (found) Grant(k);
            else Dorm.EarnSnacks(ConsolationSnacks);
            PlayerPrefs.Save();

            return new Prize { found = found, kind = k,
                               snacks = found ? 0 : ConsolationSnacks };
        }

        /// FNV-1a. `string.GetHashCode()` is not guaranteed stable across runtimes,
        /// and this hash now decides whether a player gets a power-up — so it is
        /// written out rather than borrowed.
        private static uint Hash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
            return h;
        }

        // ---- buying them: the shop's power-up counter ----
        // The other half of the supply. Snacks come from levels (2 each) and from
        // dorm plays that miss, so a power-up is always reachable by simply playing —
        // it just takes a while, which is exactly the feeling wanted.
        private const string BoughtKey = "pu_bought";

        /// How many have been bought over the life of the save. Drives the price.
        public static int Bought => Mathf.Max(0, PlayerPrefs.GetInt(BoughtKey, 0));

        /// The price rises with each purchase and then settles, so the counter can
        /// never become a tap-tap-tap vending machine — but it also never prices
        /// itself out of reach of somebody who keeps playing.
        public static int Price => Mathf.Min(60, 24 + Bought * 8);

        /// Buys one of a chosen kind with snacks. Returns false and changes nothing
        /// when the jar is short, so the button can simply grey itself out.
        public static bool Buy(Kind k)
        {
            int p = Price;
            if (Dorm.Snacks < p) return false;
            Dorm.Snacks = Dorm.Snacks - p;
            PlayerPrefs.SetInt(BoughtKey, Bought + 1);
            Grant(k);
            return true;
        }

        /// How many friends still have a play in them today. Drives the badge on
        /// the dorm, which is the only nudge the game gives about any of this.
        public static int PlaysAvailable()
        {
            int n = 0;
            for (int i = 0; i < Zoo.Count; i++) if (CanPlayWith(i)) n++;
            return n;
        }
    }
}
