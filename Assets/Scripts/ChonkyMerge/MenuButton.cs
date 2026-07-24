using UnityEngine;

namespace ChonkyMerge
{
    public enum ButtonId { Play, HighScore, Settings, Share, SoundToggle }

    /// <summary>
    /// A tappable sprite button. Stores which action it triggers and plays a little
    /// squash-and-bounce when pressed for that satisfying casual-game feedback.
    /// </summary>
    public class MenuButton : MonoBehaviour
    {
        public ButtonId Id;
        private Vector3 _baseScale;
        private float _anim = 1f;

        public void Setup(ButtonId id, Vector3 baseScale)
        {
            Id = id;
            _baseScale = baseScale;
            transform.localScale = baseScale;
        }

        public void Press()
        {
            _anim = 0f; // triggers the bounce in Update
        }

        private void Update()
        {
            if (_anim < 1f)
            {
                _anim = Mathf.Min(1f, _anim + Time.deltaTime * 6f);
                // dip to 0.9 then overshoot back to 1.0
                float s = 0.9f + 0.1f * _anim + Mathf.Sin(_anim * Mathf.PI) * 0.06f;
                transform.localScale = _baseScale * s;
            }
        }
    }
}
