using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Central tuning for the prototype. Everything the game needs to know about
    /// the critter tiers lives here so it is easy to tweak in one place.
    /// Later, each tier index will map to a cute AI-made animal sprite.
    /// </summary>
    public static class GameConfig
    {
        // How many critters exist (tier 0 = smallest). 8 originals + 6 new friends.
        public const int TierCount = 14;

        // Radius (world units) of each tier. They grow gently so a merge feels rewarding.
        public static readonly float[] Radius =
            { 0.34f, 0.44f, 0.57f, 0.72f, 0.90f, 1.12f, 1.38f, 1.66f,
              0.60f, 0.70f, 0.55f, 0.60f, 0.72f, 0.55f };

        // Placeholder colours per tier. These stand in for the real animal art.
        public static readonly Color[] Tint =
        {
            new Color(0.98f, 0.80f, 0.42f), // warm yellow
            new Color(0.97f, 0.62f, 0.55f), // coral
            new Color(0.86f, 0.55f, 0.80f), // pink-purple
            new Color(0.58f, 0.63f, 0.92f), // periwinkle
            new Color(0.50f, 0.82f, 0.86f), // teal
            new Color(0.55f, 0.85f, 0.60f), // mint
            new Color(0.95f, 0.72f, 0.40f), // shiba orange
            new Color(0.98f, 0.55f, 0.62f), // legendary rose
            new Color(0.98f, 0.55f, 0.30f), // fox orange
            new Color(0.90f, 0.90f, 0.92f), // panda
            new Color(0.80f, 0.68f, 0.52f), // hedgehog
            new Color(0.85f, 0.72f, 0.50f), // owl
            new Color(0.85f, 0.60f, 0.40f), // deer
            new Color(0.98f, 0.83f, 0.35f), // duckling
        };

        // Resource paths (under a Resources folder) for the real animal art, per tier.
        // Missing files fall back to the placeholder circle automatically.
        public static readonly string[] AnimalRes =
        {
            "Art/animals/tier1_hamster",
            "Art/animals/tier2_bunny",
            "Art/animals/tier3_kitten",
            "Art/animals/tier4_puppy",
            "Art/animals/tier5_persian",
            "Art/animals/tier6_corgi",
            "Art/animals/tier7_samoyed",
            "Art/animals/tier8_capybara",
            "Art/animals/tier9_fox",
            "Art/animals/tier10_panda",
            "Art/animals/tier11_hedgehog",
            "Art/animals/tier12_owl",
            "Art/animals/tier13_deer",
            "Art/animals/tier14_duck",
        };

        // Only spawn from the smaller tiers as "droppable" pieces.
        public const int MaxSpawnTier = 3;

        // Score awarded when you create a tier. Bigger merges = more points.
        public static int ScoreForTier(int tier) => (tier + 1) * 10;
    }
}
