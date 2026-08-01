using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// A second copy of the player's progress, kept in a plain file next to the game.
    ///
    /// Why this exists: everything is stored in PlayerPrefs, which on Android is a
    /// single XML file the OS can lose. A kill during a write, a bad restore from a
    /// device transfer, some cleaner apps — any of those and a player who spent weeks
    /// filling a zoo opens the game to level one. There is no way to apologise for
    /// that, so the game keeps a mirror and puts it back automatically.
    ///
    /// Deliberately conservative: it only restores when PlayerPrefs has NO progress at
    /// all. A partial mirror is a guess, and a game that guesses about your save can
    /// resurrect progress you meant to erase. All-or-nothing is the honest rule.
    ///
    /// No account, no server, nothing to sign into. This survives the save file being
    /// lost; it does not survive the app being uninstalled.
    /// </summary>
    public static class SaveGuard
    {
        private const string FileName = "zoo_progress.json";
        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        // Every key the game actually stores. PlayerPrefs can't be enumerated, so this
        // list IS the save format — anything added to the game must be added here or it
        // silently won't be protected.
        private static IEnumerable<string> Keys()
        {
            yield return "zoo_level"; yield return "zoo_furthest"; yield return "zoo_seen";
            yield return "zoo_tutorial_done"; yield return "chonky_best";
            yield return "sound_on"; yield return "haptics_on";
            yield return "night_last"; yield return "night_streak";
            yield return "night_best"; yield return "night_total";
            for (int i = 0; i < SleepyZoo.PuzzleGame.LevelCount; i++) yield return "zoo_stars_" + i;
            for (int c = 1; c < SleepyZoo.PuzzleGame.ChapterCount; c++) yield return "zoo_taught_ch" + c;
        }

        [System.Serializable]
        private class Blob
        {
            public List<string> keys = new();
            public List<int> vals = new();
        }

        /// Write the mirror. Called after progress changes; cheap enough to not batch.
        public static void Mirror()
        {
            try
            {
                var b = new Blob();
                foreach (var k in Keys())
                {
                    if (!PlayerPrefs.HasKey(k)) continue;
                    b.keys.Add(k); b.vals.Add(PlayerPrefs.GetInt(k, 0));
                }
                // Write beside the real file and swap, so a kill mid-write can't leave a
                // half-written mirror where a good one used to be.
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(b));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
            }
            catch (System.Exception e) { Debug.LogWarning("SaveGuard could not write: " + e.Message); }
        }

        /// Put the mirror back if — and only if — the game has come up with nothing.
        /// Runs before the first scene loads, so nothing can read progress ahead of it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RestoreIfLost()
        {
            try
            {
                if (PlayerPrefs.HasKey("zoo_furthest") || PlayerPrefs.HasKey("zoo_stars_0")) return;
                if (!File.Exists(Path)) return;
                var b = JsonUtility.FromJson<Blob>(File.ReadAllText(Path));
                if (b == null || b.keys == null || b.keys.Count == 0) return;
                for (int i = 0; i < b.keys.Count && i < b.vals.Count; i++)
                    PlayerPrefs.SetInt(b.keys[i], b.vals[i]);
                PlayerPrefs.Save();
                Debug.Log($"SaveGuard restored {b.keys.Count} saved values.");
            }
            catch (System.Exception e) { Debug.LogWarning("SaveGuard could not restore: " + e.Message); }
        }
    }
}
