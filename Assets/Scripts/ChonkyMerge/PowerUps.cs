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
    ///     play          ->  power-ups
    ///     power-ups     ->  levels get easier
    ///
    /// which is what finally gives unlocking an animal a reason beyond a nicer
    /// picture. Ten friends home is ten power-ups a day; one friend is one.
    ///
    /// Design rules:
    ///
    ///   * NOTHING IS BOUGHT WITH MONEY, and there is no second currency. Power-ups
    ///     are earned by playing with animals you already own.
    ///
    ///   * THEY NEVER EXPIRE and there is no cap. A stock of thirty Pillows is a
    ///     perfectly good way to play; it is not an exploit to be closed.
    ///
    ///   * USING ONE COSTS A STAR, NOT NOTHING. A level finished with help caps at
    ///     two stars, so the star total still means "I solved these" — otherwise the
    ///     chapter gates stop being a measure of anything. It is announced up front
    ///     on the button, never sprung on the player afterwards.
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

        /// Everybody starts with two of each, so the first one is discovered by
        /// using it rather than by reading about it somewhere.
        private const int StartingStock = 2;

        public static int Total()
        {
            int n = 0;
            for (int i = 0; i < Count; i++) n += Have((Kind)i);
            return n;
        }

        // ---- earning them: one free play per friend per day ----
        // "Today" is the device's own date, so this works with no network at all.
        // Missing a day costs nothing: there is no streak here and nothing is lost,
        // you simply did not collect. That is the difference between a reward and a
        // chore, and this game does not do chores.
        private static string Today => DateTime.Now.ToString("yyyyMMdd");

        public static bool CanPlayWith(int pal) =>
            Zoo.Unlocked(pal) && PlayerPrefs.GetString("play_" + pal, "") != Today;

        /// Plays with a friend and hands back whatever they found. The kind is
        /// derived from the day and the animal rather than from Random, so it is the
        /// same answer if the screen is redrawn — and nobody can reroll it by
        /// backing out of the dorm.
        public static Kind PlayWith(int pal)
        {
            var k = (Kind)(Mathf.Abs((Today + "#" + pal).GetHashCode()) % Count);
            PlayerPrefs.SetString("play_" + pal, Today);
            Grant(k);
            return k;
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
