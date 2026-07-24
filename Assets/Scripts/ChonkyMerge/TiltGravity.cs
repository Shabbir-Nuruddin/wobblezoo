using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// The heart of the game's twist: the direction of gravity follows how you tilt
    /// the phone. Tilt right and everything rolls right; tilt left, it rolls left.
    /// On a real phone this reads the motion sensor. In the editor (no tilting a laptop)
    /// it falls back to the LEFT/RIGHT arrow keys or A/D so you can feel it while testing.
    /// </summary>
    public class TiltGravity : MonoBehaviour
    {
        [SerializeField] private float maxTiltDegrees = 38f;
        [SerializeField] private float gravityStrength = 24f;
        [SerializeField] private float smoothing = 10f;

        private float _tilt; // smoothed, -1 (left) .. +1 (right)

        public float CurrentTiltDegrees => _tilt * maxTiltDegrees;
        public float Tilt => _tilt; // normalized -1..1, used to aim the tower drop

        private void FixedUpdate()
        {
            float target = ReadTiltInput();
            _tilt = Mathf.Lerp(_tilt, target, Time.fixedDeltaTime * smoothing);

            float angle = _tilt * maxTiltDegrees * Mathf.Deg2Rad;
            // 0 degrees = straight down. Positive tilt pushes gravity toward the right.
            Vector2 dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
            Physics2D.gravity = dir * gravityStrength;
        }

        private float ReadTiltInput()
        {
#if UNITY_EDITOR
            // Laptops can't tilt: simulate with arrow keys / A-D.
            return Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
#else
            if (SystemInfo.supportsAccelerometer)
            {
                // acceleration.x is roughly -1..1 as you roll the phone left/right in portrait.
                return Mathf.Clamp(Input.acceleration.x * 1.6f, -1f, 1f);
            }
            return Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
#endif
        }
    }
}
