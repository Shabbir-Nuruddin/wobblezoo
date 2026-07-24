using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Holds the "next" critter at the top of the jar and drops it when the player taps.
    /// Also creates merged critters on demand for the GameManager.
    /// </summary>
    public class CritterSpawner : MonoBehaviour
    {
        [SerializeField] private float spawnY = 3.0f;
        [SerializeField] private float dropCooldown = 0.45f;

        private Critter _held;
        private float _cooldown;

        private void Start() => SpawnHeld();

        private void Update()
        {
            if (GameManager.Instance.IsGameOver) return;

            if (_cooldown > 0f)
            {
                _cooldown -= Time.deltaTime;
                if (_cooldown <= 0f && _held == null) SpawnHeld();
            }

            if (_held != null && DropPressed())
            {
                _held.Drop();
                _held = null;
                _cooldown = dropCooldown;
            }
        }

        private static bool DropPressed()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            if (Input.GetKeyDown(KeyCode.Space)) return true;
            for (int i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            return false;
        }

        private void SpawnHeld()
        {
            int tier = Random.Range(0, GameConfig.MaxSpawnTier + 1);
            _held = Create(tier, new Vector2(0f, spawnY));
        }

        /// <summary>Create a critter of a given tier at a world position.</summary>
        public Critter Create(int tier, Vector2 pos)
        {
            var go = new GameObject($"Critter_T{tier}");
            go.transform.position = pos;
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<CircleCollider2D>();
            go.AddComponent<SpriteRenderer>();
            var c = go.AddComponent<Critter>();
            c.Init(tier);
            return c;
        }
    }
}
