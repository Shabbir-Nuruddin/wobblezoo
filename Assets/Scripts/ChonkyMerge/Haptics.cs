using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Very short vibrations for the two moments the game is built around: an
    /// animal skidding to a stop, and an animal falling asleep.
    ///
    /// Unity's own Handheld.Vibrate() is a blunt ~500ms buzz — far too much for a
    /// bedtime game — so this talks to Android's Vibrator directly and asks for
    /// 8-30ms taps instead. On API 26+ it uses VibrationEffect (which respects
    /// amplitude); below that it falls back to a plain short pulse. Anywhere that
    /// isn't an Android device (the editor, a desktop build) it does nothing.
    ///
    /// Vibration is its own setting, separate from sound: plenty of people play
    /// muted but still want to feel the animals land.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("haptics_on", 1) == 1;
            set { PlayerPrefs.SetInt("haptics_on", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // the three strengths the game uses
        public static void Light()  => Buzz(8,  60);    // an animal stops
        public static void Soft()   => Buzz(14, 90);    // an animal falls asleep
        public static void Medium() => Buzz(26, 140);   // the level is solved

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _sdk = -1;
        private static bool _unavailable;

        private static void Buzz(long ms, int amplitude)
        {
            if (!Enabled || _unavailable) return;
            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _sdk = version.GetStatic<int>("SDK_INT");
                    if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator")) { _unavailable = true; return; }
                }
                if (_sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", ms, Mathf.Clamp(amplitude, 1, 255)))
                        _vibrator.Call("vibrate", effect);
                }
                else _vibrator.Call("vibrate", ms);
            }
            catch (System.Exception)
            {
                _unavailable = true;   // some devices/ROMs refuse: stop asking, stay quiet
            }
        }
#else
        private static void Buzz(long ms, int amplitude) { }
#endif
    }
}
