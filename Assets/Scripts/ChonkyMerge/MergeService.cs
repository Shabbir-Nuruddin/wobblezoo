using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Decouples a Critter's "we touched a matching critter" event from whichever
    /// game mode is active (jar or tower). The active mode registers a handler.
    /// </summary>
    public static class MergeService
    {
        public static System.Action<Critter, Critter> Handler;

        public static void Merge(Critter a, Critter b)
        {
            Handler?.Invoke(a, b);
        }
    }
}
