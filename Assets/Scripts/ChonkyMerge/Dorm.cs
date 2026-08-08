using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// The dorm's state: snacks, moods, and who is asleep.
    ///
    /// The first pass of this screen let you tuck an animal in and nothing else, so
    /// there was no reason to open it twice. It now carries four verbs — Feed, Pet,
    /// Play, Tuck in — and Play is the one that matters, because it is where the
    /// power-ups that make the levels easier actually come from (see PowerUps).
    ///
    /// Three rules, inherited from the rest of the game and deliberately kept:
    ///
    ///   1. NOTHING DECAYS. A fed animal never gets hungry again on a timer, and a
    ///      tucked-in animal never wakes up because you were away. This is a bedtime
    ///      game; a room that gets worse while you sleep is an obligation, not a
    ///      comfort. Moods only ever change because the player touched something.
    ///
    ///   2. SNACKS ARE NOT PROGRESS. Stars gate chapters; snacks buy affection and
    ///      furniture. They can never unlock a level, so no amount of dorm play moves
    ///      anybody past the levels that teach the rules — and skipping the dorm
    ///      entirely costs the player nothing they cannot get by playing on.
    ///
    ///   3. PETTING IS ALWAYS FREE. An empty snack jar must never mean "there is
    ///      nothing you can do in here".
    /// </summary>
    public static class Dorm
    {
        private const string SnackKey = "dorm_snacks";
        private const string MoodKey = "dorm_mood_";     // + index
        private const string SleepKey = "dorm_sleep_";    // + index

        /// Snacks earned per level cleared. Matches the win screen's "+2 snacks".
        public const int SnacksPerLevel = 2;

        // The moods, in the order the enum is stored.
        public static readonly string[] Moods =
            { "Content", "Playful", "Sleepy", "Hungry", "Curious" };

        // Each friend's starting mood, straight out of the mockup.
        private static readonly int[] StartMood = { 1, 2, 3, 0, 4, 0, 1, 3, 0, 2 };

        public static int Snacks
        {
            get => PlayerPrefs.GetInt(SnackKey, 6);
            set { PlayerPrefs.SetInt(SnackKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static void EarnSnacks(int n)
        {
            if (n <= 0) return;
            Snacks = Snacks + n;
        }

        public static int MoodOf(int i) =>
            Mathf.Clamp(PlayerPrefs.GetInt(MoodKey + i, StartMood[i % StartMood.Length]), 0, Moods.Length - 1);

        public static string MoodWord(int i) => Moods[MoodOf(i)];

        /// Mood colours: hungry reads warm-coral, sleepy lavender, playful green,
        /// everything else a quiet cream.
        public static Color MoodColor(int i)
        {
            switch (MoodOf(i))
            {
                case 1: return Rgb(0x9ed666);   // Playful
                case 2: return Rgb(0xbda8ff);   // Sleepy
                case 3: return Rgb(0xffb0a0);   // Hungry
                case 4: return Rgb(0xffd8a0);   // Curious
                default: return new Color(1f, 0.925f, 0.816f, 0.62f);
            }
        }

        public static bool Asleep(int i) => PlayerPrefs.GetInt(SleepKey + i, 0) == 1;

        private static void SetMood(int i, int mood) => PlayerPrefs.SetInt(MoodKey + i, mood);
        private static void SetAsleep(int i, bool v) => PlayerPrefs.SetInt(SleepKey + i, v ? 1 : 0);

        // ---- the four things you can do ----
        /// Costs a snack, and they are Content afterwards. Returns false (and does
        /// nothing) when the jar is empty, so the button can grey itself out.
        public static bool Feed(int i)
        {
            if (Snacks <= 0) return false;
            Snacks = Snacks - 1;
            SetMood(i, 0);
            SetAsleep(i, false);
            PlayerPrefs.Save();
            return true;
        }

        /// Always free. Wakes them up and makes them playful.
        public static void Pet(int i)
        {
            SetMood(i, 1);
            SetAsleep(i, false);
            PlayerPrefs.Save();
        }

        /// Goodnight.
        public static void Tuck(int i)
        {
            SetMood(i, 2);
            SetAsleep(i, true);
            PlayerPrefs.Save();
        }

        /// A game together. Wakes them up, leaves them Playful, and hands back what
        /// they turned up — usually a couple of snacks, sometimes a power-up. See
        /// PowerUps.PlayWith for the odds, why it is once a day, and why missing a
        /// day costs nothing.
        public static PowerUps.Prize Play(int i)
        {
            SetMood(i, 1);
            SetAsleep(i, false);
            var p = PowerUps.PlayWith(i);
            PlayerPrefs.Save();
            return p;
        }

        public static void TuckEveryone()
        {
            for (int i = 0; i < Zoo.Count; i++)
                if (Zoo.Unlocked(i)) { SetMood(i, 2); SetAsleep(i, true); }
            PlayerPrefs.Save();
        }

        public static int AsleepCount()
        {
            int n = 0;
            for (int i = 0; i < Zoo.Count; i++) if (Zoo.Unlocked(i) && Asleep(i)) n++;
            return n;
        }

        /// The one-line prompt under the room. It answers "what is there to do here?"
        /// and it always has an answer.
        public static string Hint()
        {
            // The odds are said out loud. A power-up is roughly a one-in-ten find
            // now, and a player who isn't told that will read nine ordinary plays as
            // the game being broken rather than as the game being generous later.
            int plays = PowerUps.PlaysAvailable();
            if (plays > 0)
                return plays == 1
                    ? $"One friend still wants to play — {PowerUps.ChanceToday()}% chance of a treat."
                    : $"{plays} friends still want to play — {PowerUps.ChanceToday()}% chance each.";
            int home = Zoo.UnlockedCount();
            if (AsleepCount() >= home && home > 0) return "Everyone's asleep. Goodnight.";
            if (Snacks <= 0) return "Out of snacks — petting is always free.";
            return "Tap a friend. They remember.";
        }

        /// The warm light in the room. Comes from the lamp if it has been bought,
        /// and is stronger once it's actually dark outside.
        public static Color LampColor => Decor.Lamp ? Rgb(0xffc478) : Rgb(0xffdcae);

        private static Color Rgb(uint c) => new Color(
            ((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f, 1f);
    }
}
