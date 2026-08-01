using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// The zoo: which animals have moved in, and how settled they are.
    ///
    /// Two rules hold this together:
    ///
    ///   1. NOTHING IS RANDOM. Every friend arrives on a schedule the player can
    ///      see coming — a star total, or finishing a chapter. No luck, no boxes,
    ///      nothing to buy. The zoo screen always answers "who's next, and how far?"
    ///
    ///   2. THE ZOO OWNS NO SAVE DATA. Every answer here is derived from the stars
    ///      the player already earned, so the zoo can never disagree with the level
    ///      picker, and there is nothing extra to migrate or corrupt. The single
    ///      exception is `zoo_seen`, which only remembers which arrivals have been
    ///      announced, so the "someone moved in!" card shows exactly once.
    ///
    /// Animals settle in as you keep playing: Visiting -> Snuggled -> Dreaming.
    /// That's a picture of your progress, not a second currency.
    ///
    /// The schedule is one friend per chapter, plus two early ones so a new player
    /// meets somebody in the first hour. Ten friends across eight chapters means the
    /// zoo is never finished and never empty.
    /// </summary>
    public static class Zoo
    {
        public enum How { AtHome, Stars, ChapterCleared }
        public enum Tier { Friend, Special, Guest }

        public class Pal
        {
            public string sprite, name, dream;
            public How how; public int arg;
            public int settleBase;       // star total they arrive around; settling counts up from here
            public Tier tier;
            public Color col;
            public Pal(string sprite, string name, How how, int arg, int settleBase, Tier tier, Color col, string dream)
            { this.sprite=sprite; this.name=name; this.how=how; this.arg=arg;
              this.settleBase=settleBase; this.tier=tier; this.col=col; this.dream=dream; }
        }

        private static Color C(float r,float g,float b)=>new Color(r,g,b);

        // The roster, in arrival order. Colours match each animal's signature colour
        // on the board (PuzzleGame.PetColors), so a friend looks the same everywhere.
        //
        // Only the flat `Art/pets` sprites are used: the fluffy 512px `Art/animals`
        // renders are from an older art direction and would look like a different
        // game. New friends = drop a flat sprite in Art/pets and add a line here.
        public static readonly Pal[] Pals =
        {
            new Pal("dog","Pip",       How.AtHome,        0,   0, Tier.Friend,  C(0.42f,0.62f,0.96f),
                    "dreams of a garden with no fences"),
            new Pal("rabbit","Clover", How.Stars,        10,  10, Tier.Friend,  C(0.98f,0.55f,0.68f),
                    "dreams of a very long carrot"),
            new Pal("frog","Puddle",   How.ChapterCleared,0,  36, Tier.Friend,  C(0.62f,0.84f,0.40f),
                    "dreams of warm rain on a lilypad"),
            new Pal("duck","Biscuit",  How.ChapterCleared,1,  72, Tier.Friend,  C(0.98f,0.84f,0.36f),
                    "dreams of a pond that's all his"),
            new Pal("owl","Professor", How.ChapterCleared,2, 100, Tier.Special, C(0.99f,0.72f,0.36f),
                    "dreams in questions, and answers them too"),
            new Pal("pig","Marzipan",  How.ChapterCleared,3, 128, Tier.Friend,  C(0.86f,0.52f,0.93f),
                    "dreams of a blanket fresh from the dryer"),
            new Pal("penguin","Sprout",How.ChapterCleared,4, 156, Tier.Special, C(0.42f,0.80f,0.94f),
                    "dreams of sliding downhill forever"),
            new Pal("bear","Marlow",   How.ChapterCleared,5, 184, Tier.Special, C(0.93f,0.60f,0.42f),
                    "dreams of honey, obviously"),
            new Pal("cow","Nutmeg",    How.ChapterCleared,6, 212, Tier.Friend,  C(0.70f,0.62f,0.95f),
                    "dreams of a field at four in the afternoon"),
            new Pal("panda","Momo",    How.ChapterCleared,7, 240, Tier.Guest,   C(0.36f,0.80f,0.68f),
                    "dreams of the quietest bamboo grove there is"),
        };

        public static int Count => Pals.Length;

        // ---- has this friend arrived? ----
        public static bool Unlocked(int i)
        {
            var p = Pals[i];
            switch (p.how)
            {
                case How.AtHome:         return true;
                case How.Stars:          return SleepyZoo.PuzzleGame.TotalStars() >= p.arg;
                case How.ChapterCleared: return ChapterCleared(p.arg);
            }
            return false;
        }

        /// A chapter counts as cleared when its last level has at least one star.
        /// One friend per chapter is the backbone of the whole schedule: finish a
        /// chapter, somebody new is waiting at home.
        private static bool ChapterCleared(int chapter)
        {
            int last = SleepyZoo.PuzzleGame.ChapterLastLevel(chapter);
            return SleepyZoo.PuzzleGame.StarsFor(last) > 0;
        }

        public static int UnlockedCount()
        {
            int n = 0;
            for (int i = 0; i < Pals.Length; i++) if (Unlocked(i)) n++;
            return n;
        }

        // ---- how settled are they? 0 visiting, 1 snuggled, 2 dreaming ----
        public const int SnuggleStars = 12, DreamStars = 30;
        public static int Stage(int i)
        {
            if (!Unlocked(i)) return -1;
            int since = SleepyZoo.PuzzleGame.TotalStars() - Pals[i].settleBase;
            if (since >= DreamStars) return 2;
            if (since >= SnuggleStars) return 1;
            return 0;
        }
        public static string StageWord(int stage) =>
            stage >= 2 ? "Dreaming" : stage == 1 ? "Snuggled in" : "Just visiting";

        // ---- what does it take to get the next one? ----
        public static int NextLocked()
        {
            for (int i = 0; i < Pals.Length; i++) if (!Unlocked(i)) return i;
            return -1;
        }

        /// One plain line telling the player exactly what's between them and the
        /// next friend. This is the zoo's whole job as a motivator.
        public static string NextLine()
        {
            int i = NextLocked();
            if (i < 0) return "Everyone's home. The whole zoo is asleep.";
            var p = Pals[i];
            if (p.how == How.Stars)
            {
                int need = p.arg - SleepyZoo.PuzzleGame.TotalStars();
                return need <= 1 ? $"{p.name} arrives with your next star."
                                 : $"{p.name} is {need} stars away.";
            }
            return $"{p.name} moves in when you finish chapter {p.arg + 1}.";
        }

        public static string Requirement(int i)
        {
            var p = Pals[i];
            if (p.how == How.Stars) return $"Arrives at {p.arg} stars";
            if (p.how == How.ChapterCleared) return $"Arrives after chapter {p.arg + 1}";
            return "";
        }

        // ---- the "someone moved in!" moment ----
        // `zoo_seen` is the number of arrivals already announced. If more animals are
        // home than we've announced, the next unannounced one gets a card.
        public static int PendingArrival()
        {
            int seen = PlayerPrefs.GetInt("zoo_seen", 0);
            if (UnlockedCount() <= seen) return -1;
            int n = 0;
            for (int i = 0; i < Pals.Length; i++)
            {
                if (!Unlocked(i)) continue;
                if (n == seen) return i;
                n++;
            }
            return -1;
        }
        public static void MarkArrivalSeen()
        {
            PlayerPrefs.SetInt("zoo_seen", PlayerPrefs.GetInt("zoo_seen", 0) + 1);
            PlayerPrefs.Save();
        }

        private static readonly System.Collections.Generic.Dictionary<string,Texture2D> _tex = new();
        public static Texture2D Art(int i)
        {
            var key = Pals[i].sprite;
            if (_tex.TryGetValue(key, out var t)) return t;
            t = Resources.Load<Texture2D>("Art/pets/" + key);
            _tex[key] = t; return t;
        }
    }
}
