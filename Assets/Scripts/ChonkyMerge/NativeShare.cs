using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Opens the phone's native "Share" sheet with a text message. On Android this
    /// uses an ACTION_SEND intent; in the editor it just logs so we can test flow.
    /// </summary>
    public static class NativeShare
    {
        public static void ShareText(string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), message);

                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share Wobble Zoo");
                activity.Call("startActivity", chooser);
            }
            catch (System.Exception e) { Debug.LogWarning("Share failed: " + e.Message); }
#else
            Debug.Log("[Share] " + message);
#endif
        }
    }
}
