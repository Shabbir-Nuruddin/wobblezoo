using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Loads and caches the real animal sprites per tier. Returns null when a tier's
    /// art hasn't been generated yet, so the game falls back to the placeholder circle.
    /// </summary>
    public static class AnimalSprites
    {
        private static readonly Sprite[] _cache = new Sprite[GameConfig.TierCount];
        private static readonly bool[] _tried = new bool[GameConfig.TierCount];

        public static Sprite Get(int tier)
        {
            if (tier < 0 || tier >= GameConfig.TierCount) return null;
            if (!_tried[tier])
            {
                _tried[tier] = true;
                _cache[tier] = Resources.Load<Sprite>(GameConfig.AnimalRes[tier]);
            }
            return _cache[tier];
        }
    }
}
