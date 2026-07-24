using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// A single droppable / merge-able animal. Knows its tier, shows the right art,
    /// and reports a matching-tier contact to whichever game mode is active.
    /// Supports a circle collider (jar mode, things roll) or a box collider
    /// (tower mode, things stack).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class Critter : MonoBehaviour
    {
        // Set by the active game mode before spawning critters.
        public static bool TowerMode = false;

        public int Tier { get; private set; }
        public bool Consumed { get; set; }        // true once merged away
        public bool Dropped { get; private set; }  // false while held, waiting to drop
        public bool HasLanded { get; private set; }

        private Rigidbody2D _rb;
        private Collider2D _col;
        private SpriteRenderer _sr;

        public Rigidbody2D Body => _rb;

        public void Init(int tier)
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _sr = GetComponent<SpriteRenderer>();

            Tier = Mathf.Clamp(tier, 0, GameConfig.TierCount - 1);
            float d = GameConfig.Radius[Tier] * 2f;

            float scale;
            var animal = AnimalSprites.Get(Tier);
            if (animal != null)
            {
                _sr.sprite = animal;
                _sr.color = Color.white;
                scale = (d / animal.bounds.size.x) * 1.12f;
            }
            else
            {
                _sr.sprite = SpriteFactory.Circle();
                _sr.color = GameConfig.Tint[Tier];
                scale = d; // circle sprite is 1 world unit
            }
            transform.localScale = new Vector3(scale, scale, 1f);

            // Size whichever collider this critter was created with.
            if (_col is CircleCollider2D cc)
                cc.radius = (d * 0.5f) / scale;
            else if (_col is BoxCollider2D bc)
                bc.size = new Vector2((d * 0.86f) / scale, (d * 0.80f) / scale);

            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.sharedMaterial = Physics.CritterMaterial();
            _rb.simulated = true;

            if (TowerMode)
            {
                // Heavier as tiers grow, so big merges shift the tower's balance.
                _rb.mass = Mathf.Max(0.6f, d * d * 2f);
                _rb.angularDamping = 0.6f;
                _rb.linearDamping = 0.05f;
            }

            SetHeld();
        }

        /// <summary>Hold in place until the player taps to drop.</summary>
        public void SetHeld()
        {
            Dropped = false;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        public void Drop()
        {
            Dropped = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            HasLanded = true;

            if (Consumed) return;
            var otherCritter = other.collider.GetComponent<Critter>();
            if (otherCritter == null || otherCritter.Consumed) return;
            if (otherCritter.Tier != Tier) return;

            // Only one of the pair runs the merge (the one with the lower id).
            if (GetInstanceID() < otherCritter.GetInstanceID())
                MergeService.Merge(this, otherCritter);
        }
    }

    /// <summary>Shared physics material — grippy enough to stack, lively enough to roll.</summary>
    public static class Physics
    {
        private static PhysicsMaterial2D _mat;
        public static PhysicsMaterial2D CritterMaterial()
        {
            if (_mat != null) return _mat;
            _mat = new PhysicsMaterial2D("CritterMat") { friction = 0.6f, bounciness = 0.05f };
            return _mat;
        }
        // Back-compat alias.
        public static PhysicsMaterial2D BouncyMaterial() => CritterMaterial();
    }
}
