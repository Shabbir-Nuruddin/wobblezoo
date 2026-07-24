using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Tiny procedural sound engine — generates a soft click and a merge "pop" in
    /// code so we get satisfying button feedback with no audio files. Respects the
    /// player's sound on/off setting (stored in PlayerPrefs).
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        private static Sfx _i;
        private AudioSource _src;
        private AudioClip _click, _pop;

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt("sound_on", 1) == 1;
            set { PlayerPrefs.SetInt("sound_on", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private static Sfx I
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    _i = go.AddComponent<Sfx>();
                    _i._src = go.AddComponent<AudioSource>();
                    _i._click = Blip(0.07f, 660f, 0.35f);
                    _i._pop = Blip(0.12f, 420f, 0.5f, true);
                }
                return _i;
            }
        }

        public static void Click() => Play(I._click);
        public static void Pop() => Play(I._pop);

        private static void Play(AudioClip c)
        {
            if (!SoundOn || c == null) return;
            I._src.PlayOneShot(c);
        }

        private static AudioClip Blip(float dur, float freq, float vol, bool rising = false)
        {
            int rate = 44100;
            int n = Mathf.CeilToInt(rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 18f);                 // quick decay
                float f = rising ? freq * (1f + t * 4f) : freq;  // pop slides up
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * vol;
            }
            var clip = AudioClip.Create("blip", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
