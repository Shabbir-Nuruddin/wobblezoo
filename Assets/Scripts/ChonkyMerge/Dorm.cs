using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// The dorm's interactive layer: snacks, moods, and who is asleep.
    ///
    /// The zoo used to be a list of portraits you could look at but not touch. The
    /// redesign turns it into a room you visit — tap a friend and you can Feed, Pet
    /// or Tuck them in. This class owns the small amount of state that needs, and
    /// nothing more.
    ///
    /// Three rules, inherited from the rest of the game and deliberately kept:
    ///
    ///   1. NOTHING DECAYS. A fed animal never gets hungry again on a timer, and a
    ///      tucked-in animal never wakes up because you were away. This is a bedtime
    ///      game; a room that gets worse while you sleep is an obligation, not a
    ///      comfort. Moods only ever change because the player touched something.
    ///
    ///   2. SNACKS ARE NOT PROGRESS. Stars gate chapters; snacks buy affection and
    ///      nothing else. They can never unlock a level, so no amount of dorm play
    ///      moves anybody past the levels that teach the rules — and skipping the
    ///      dorm entirely costs the player nothing.
    ///
    ///   3. NO CAP AND NO SINK ANXIETY. Snacks accumulate from levels you were
    ///      going to play anyway. Petting is free precisely so an empty snack jar
    ///      never means "there is nothing you can do here".
    /// </summary>
    public static class Dorm
    {
        private const string SnackKey = "dorm_snacks";
        private const string MoodKey = "dorm_mood_";     // + index
        private const string SleepKey = "dorm_sleep_";    // + index
        private const string ThemeKey = "dorm_theme";

        /// Snacks earned per level cleared. Matches the win screen's "+2 snacks".
        public const int SnacksPerLevel = 2;

        // The moods from the design, in the order the enum is stored.
        public static readonly string[] Moods =
            { "Content", "Playful", "Sleepy", "Hungry", "Curious" };

        // Each friend's starting mood, straight out of the mockup.
        private static readonly int[] StartMood = { 1, 2, 3, 0, 4, 0, 1, 3, 0, 2 };

        public static int Snacks
        {
            get => PlayerPrefs.GetInt(SnackKey, 3);
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

        /// Mood colours from the design: hungry reads warm-coral, sleepy lavender,
        /// playful green, everything else a quiet cream.
        public static Color MoodColor(int i)
        {
            switch (MoodOf(i))
            {
                case 1: return Ui2.Hex(0x9ed666);   // Playful
                case 2: return Ui2.Hex(0xbda8ff);   // Sleepy
                case 3: return Ui2.Hex(0xffb0a0);   // Hungry
                case 4: return Ui2.Hex(0xffd8a0);   // Curious
                default: return new Color(1f, 0.925f, 0.816f, 0.62f);
            }
        }

        public static bool Asleep(int i) => PlayerPrefs.GetInt(SleepKey + i, 0) == 1;

        private static void SetMood(int i, int mood)
        {
            PlayerPrefs.SetInt(MoodKey + i, mood);
        }
        private static void SetAsleep(int i, bool v)
        {
            PlayerPrefs.SetInt(SleepKey + i, v ? 1 : 0);
        }

        // ---- the three things you can do ----
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

        /// The one-line prompt under the dorm, from the design.
        public static string Hint()
        {
            int home = Zoo.UnlockedCount();
            if (home <= 1) return "Tap a friend. They remember.";
            if (AsleepCount() >= home) return "Everyone's asleep. Goodnight.";
            if (Snacks <= 0) return "Out of snacks — petting is always free.";
            return "Tap a friend. They remember.";
        }

        // ---- decorate ----
        // The one screen the design names but never draws. Kept deliberately small:
        // it recolours the dorm's lamplight, using colours the game already owns
        // (each friend's signature colour), so it can never introduce art that
        // clashes with the rest of the room.
        public static readonly string[] ThemeNames =
            { "Lamplight", "Moonlight", "Embers", "Meadow", "Blossom" };
        private static readonly Color[] ThemeCols =
        {
            Ui2.Hex(0xffc478), Ui2.Hex(0x9fb8ff), Ui2.Hex(0xff9a5c),
            Ui2.Hex(0x9ed666), Ui2.Hex(0xee7c9b)
        };

        public static int Theme
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ThemeKey, 0), 0, ThemeNames.Length - 1);
            set { PlayerPrefs.SetInt(ThemeKey, value); PlayerPrefs.Save(); }
        }
        public static Color ThemeColor => ThemeCols[Theme];
    }

    /// A tiny colour helper so this file does not have to reach across into the UI
    /// assembly for one function.
    internal static class Ui2
    {
        public static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
