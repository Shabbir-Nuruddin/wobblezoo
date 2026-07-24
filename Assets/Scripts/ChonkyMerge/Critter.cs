using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// A single droppable/merge-able critter. Knows its tier, merges with a matching
    /// tier on contact, and reports to the GameManager if it settles above the danger line.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public class Critter : MonoBehaviour
    {
        public int Tier { get; private set; }
        public bool Consumed { get; set; }   // set true once it has been merged away
        public bool Dropped { get; private set; }  // false while held at the top, waiting to fall

        private Rigidbody2D _rb;
        private CircleCollider2D _col;
        private SpriteRenderer _sr;
        private float _overflowTimer;
        private bool _hasLanded;

        public Rigidbody2D Body => _rb;

        public void Init(int tier)
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CircleCollider2D>();
            _sr = GetComponent<SpriteRenderer>();

            Tier = Mathf.Clamp(tier, 0, GameConfig.TierCount - 1);
            float d = GameConfig.Radius[Tier] * 2f;

            var animal = AnimalSprites.Get(Tier);
            if (animal != null)
            {
                // Real animal art: scale so the body roughly fills the tier diameter,
                // and keep the physics circle at the true tier radius.
                _sr.sprite = animal;
                _sr.color = Color.white;
                float sw = animal.bounds.size.x;
                float sc = (d / sw) * 1.12f;
                transform.localScale = new Vector3(sc, sc, 1f);
                _col.radius = (d * 0.5f) / sc;
            }
            else
            {
                // Placeholder circle, tinted per tier.
                _sr.sprite = SpriteFactory.Circle();
                _sr.color = GameConfig.Tint[Tier];
                transform.localScale = new Vector3(d, d, 1f);
                _col.radius = 0.5f;
            }

            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.sharedMaterial = Physics.BouncyMaterial();
            _rb.simulated = true;

            SetHeld();
        }

        /// <summary>Float in place at the top of the jar until the player taps to drop.</summary>
        public void SetHeld()
        {
            Dropped = false;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
        }

        public void Drop()
        {
            Dropped = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            _hasLanded = true;

            if (Consumed) return;
            var otherCritter = other.collider.GetComponent<Critter>();
            if (otherCritter == null || otherCritter.Consumed) return;
            if (otherCritter.Tier != Tier) return;

            // Only one of the pair runs the merge (the one with the lower id).
            if (GetInstanceID() < otherCritter.GetInstanceID())
                GameManager.Instance.Merge(this, otherCritter);
        }

        private void Update()
        {
            if (Consumed || !Dropped) return;

            // Overflow check: if we come to rest above the danger line, the jar is full.
            bool high = transform.position.y + GameConfig.Radius[Tier] > GameManager.Instance.DangerY;
            bool slow = _rb.linearVelocity.sqrMagnitude < 0.35f;
            if (_hasLanded && high && slow)
            {
                _overflowTimer += Time.deltaTime;
                if (_overflowTimer > 2.0f)
                    GameManager.Instance.GameOver();
            }
            else
            {
                _overflowTimer = 0f;
            }
        }
    }

    /// <summary>Shared physics material so critters roll and squish nicely.</summary>
    public static class Physics
    {
        private static PhysicsMaterial2D _mat;
        public static PhysicsMaterial2D BouncyMaterial()
        {
            if (_mat != null) return _mat;
            _mat = new PhysicsMaterial2D("CritterMat") { friction = 0.35f, bounciness = 0.12f };
            return _mat;
        }
    }
}
