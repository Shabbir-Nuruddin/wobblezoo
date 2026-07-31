using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// The game's whole sound world. One event = one sound, never more — a cozy
    /// game gets noisy fast, so every call here is deliberate.
    ///
    ///   Tap    - a UI button
    ///   Swipe  - the whole room slides (a soft cloth whoosh, generated in code
    ///            because no sample sounds like a room of animals sliding)
    ///   Land   - an animal skids to a stop. Pitched by how far it travelled, so a
    ///            long slide lands lower and heavier than a nudge.
    ///   Sleep  - an animal is caught by its own bed (chapter 2's whole payoff)
    ///   Star   - one star popping onto the win panel; pitch rises per star
    ///   Win    - the level is finished
    ///   Undo / Locked - the two "no, go back" moments
    ///
    /// Samples are Kenney's CC0 interface + jingle packs, loaded from
    /// Resources/Audio. If a clip is ever missing the procedural blip stands in,
    /// so sound never silently disappears.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        private static Sfx _i;
        private AudioSource _src;
        private AudioClip _tap, _land, _sleep, _star, _win, _undo, _locked, _swipe, _blip;

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
                    _i._src.playOnAwake = false;
                    _i._blip   = Blip(0.07f, 660f, 0.35f);
                    _i._tap    = Load("tap");
                    _i._land   = Load("land");
                    _i._sleep  = Load("sleep");
                    _i._star   = Load("star");
                    _i._win    = Load("win");
                    _i._undo   = Load("undo");
                    _i._locked = Load("locked");
                    _i._swipe  = Whoosh();
                }
                return _i;
            }
        }

        private static AudioClip Load(string name) => Resources.Load<AudioClip>("Audio/" + name);

        // ---- the events the game actually fires ----
        public static void Tap()    => Play(I._tap,   0.55f);
        public static void Click()  => Tap();                       // legacy name, same sound
        public static void Swipe()  => Play(I._swipe, 0.34f, Random.Range(0.94f, 1.06f));
        public static void Undo()   => Play(I._undo,  0.45f);
        public static void Locked() => Play(I._locked,0.40f);
        public static void Sleep()  => Play(I._sleep, 0.60f, Random.Range(0.97f, 1.05f));
        public static void Win()    => Play(I._win,   0.55f);
        public static void Pop()    => Win();                       // legacy name

        /// <param name="distance">cells travelled — a longer skid lands lower and louder</param>
        public static void Land(int distance)
        {
            float t = Mathf.Clamp01((distance - 1) / 4f);
            Play(I._land, Mathf.Lerp(0.30f, 0.52f, t), Mathf.Lerp(1.16f, 0.88f, t));
        }

        /// <param name="index">0,1,2 — each star in the win panel rings a step higher</param>
        public static void Star(int index) => Play(I._star, 0.55f, 1f + index * 0.16f);

        private static void Play(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (!SoundOn) return;
            var s = I._src;
            if (c == null) c = I._blip;                 // never go silent on a missing file
            s.pitch = pitch;
            s.PlayOneShot(c, vol);
        }

        // ---- generated sounds (no sample fits these) ----

        /// A short breathy sweep: filtered noise under a quick swell, which reads as
        /// "everything in the room slid at once" far better than any click.
        private static AudioClip Whoosh()
        {
            int rate = 44100, n = Mathf.CeilToInt(rate * 0.20f);
            var data = new float[n];
            float lp = 0f;
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, u = (float)i / n;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.06f + u * 0.10f);      // opens up as it moves
                float env = Mathf.Sin(u * Mathf.PI);                 // swell in and out
                env *= env;
                data[i] = lp * env * 0.9f;
            }
            var clip = AudioClip.Create("whoosh", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
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
