using System;
using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Tonight's Puzzle: one extra level a night, the same one for everybody.
    ///
    /// Four decisions hold this together, and they're all about NOT punishing people:
    ///
    ///   1. IT PAYS NO STARS. Stars open chapters. If the nightly puzzle paid them,
    ///      a player who skips it would fall behind the gates through no fault of
    ///      their own, and a player who plays it would blow past the levels that
    ///      teach the rules. It pays nights, and nights buy lanterns. Nothing else.
    ///
    ///   2. IT ONLY EVER USES CHAPTER ONE'S RULES. No sticky beds, no honey, no
    ///      burrows. A daily puzzle is the one thing a brand-new player might tap
    ///      first, so it can't lean on a rule they haven't met — and it must never
    ///      spoil a twist they haven't paid for yet.
    ///
    ///   3. THE NIGHT TURNS OVER AT 4AM, not midnight. It's a bedtime game. Somebody
    ///      playing at 1am is having tonight, not tomorrow, and losing a streak on a
    ///      technicality is exactly the kind of small betrayal that gets an app deleted.
    ///
    ///   4. A MISSED NIGHT COSTS ONE NIGHT, not the whole streak. Streaks that zero
    ///      out turn a cozy habit into an obligation — miss once and the honest move
    ///      is to quit, because the thing you were building is gone. Here a 12-night
    ///      streak comes back as an 11. You can always be a night away from your best.
    /// </summary>
    public static class Nightly
    {
        // Nights are counted from a fixed date so the number means the same thing on
        // every device; the clock is local, because "tonight" is local.
        private static readonly DateTime Epoch = new DateTime(2026, 1, 1);
        private const int RollOverHour = 4;

        /// Which night it is right now.
        public static int Tonight =>
            (int)(DateTime.Now.AddHours(-RollOverHour).Date - Epoch).TotalDays;

        private const string KLast = "night_last", KStreak = "night_streak",
                             KBest = "night_best", KTotal = "night_total";

        private static int LastPlayed => PlayerPrefs.GetInt(KLast, int.MinValue / 2);
        private static int RawStreak => PlayerPrefs.GetInt(KStreak, 0);

        /// True once tonight's puzzle has been solved.
        public static bool DoneTonight => LastPlayed == Tonight;

        /// The streak as it stands right now, already cooled for any nights missed.
        /// Deliberately a live calculation rather than a stored number: nothing has to
        /// run at midnight for the count to be honest, and there's no "streak reaper"
        /// that can fire twice or not at all.
        public static int Streak
        {
            get
            {
                int gap = Tonight - LastPlayed;
                if (gap <= 1) return RawStreak;             // tonight, or last night — intact
                return Mathf.Max(0, RawStreak - (gap - 1)); // one night lost per night missed
            }
        }

        public static int BestStreak => PlayerPrefs.GetInt(KBest, 0);
        public static int TotalNights => PlayerPrefs.GetInt(KTotal, 0);

        /// Record tonight's win. Safe to call twice — the second call is a no-op.
        public static void MarkDone()
        {
            if (DoneTonight) return;
            int s = Streak + 1;
            PlayerPrefs.SetInt(KStreak, s);
            PlayerPrefs.SetInt(KLast, Tonight);
            PlayerPrefs.SetInt(KTotal, TotalNights + 1);
            if (s > BestStreak) PlayerPrefs.SetInt(KBest, s);
            PlayerPrefs.Save();
            SaveGuard.Mirror();
        }

        // ---- lanterns: what a streak actually buys ----
        // Nights light lanterns in the zoo. It's the one reward that can't unbalance
        // anything, because it isn't a currency — it's just the room getting cosier.
        public static readonly int[] LanternNights = { 2, 3, 5, 7, 10, 14, 21, 30 };
        public static int Lanterns
        {
            get
            {
                int best = Mathf.Max(BestStreak, Streak), n = 0;
                foreach (var t in LanternNights) if (best >= t) n++;
                return n;
            }
        }
        public static int MaxLanterns => LanternNights.Length;

        /// One SHORT line for the menu — it sits under a corner pill, so anything
        /// longer than about four words gets clipped rather than read.
        public static string Line()
        {
            if (!Available) return "";
            if (!DoneTonight)
                return Streak > 0 ? $"{Streak} nights so far" : "A fresh puzzle";
            int lit = Lanterns;
            if (lit >= MaxLanterns) return "All lanterns lit";
            int need = LanternNights[lit] - Streak;
            return need <= 1 ? "1 night to a lantern" : $"{need} nights to a lantern";
        }

        /// False when no pool has been generated yet — the menu hides the whole
        /// feature rather than offering a button that can't do anything.
        public static bool Available => SleepyZoo.PuzzleGame.DailyCount > 0;
    }
}
