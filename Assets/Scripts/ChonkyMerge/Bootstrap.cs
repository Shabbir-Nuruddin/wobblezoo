using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Builds the whole playable scene from code at startup: camera and the open-top
    /// "jar" the critters fall into. Put this (plus GameManager, CritterSpawner,
    /// TiltGravity) on a single GameObject and press Play — no manual wiring needed.
    /// </summary>
    [RequireComponent(typeof(GameManager), typeof(CritterSpawner), typeof(TiltGravity))]
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private float jarHalfWidth = 2.3f;
        [SerializeField] private float jarFloorY = -3.6f;
        [SerializeField] private float jarTopY = 3.2f;
        [SerializeField] private float wallThickness = 0.4f;

        private void Awake()
        {
            SetupCamera();
            BuildJar();
            Physics2D.gravity = new Vector2(0f, -26f);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 4.7f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.16f, 0.14f, 0.22f); // cozy dark
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void BuildJar()
        {
            float height = jarTopY - jarFloorY;
            float midY = (jarTopY + jarFloorY) * 0.5f;

            Wall("Floor", new Vector2(0f, jarFloorY - wallThickness * 0.5f),
                 new Vector2(jarHalfWidth * 2f + wallThickness * 2f, wallThickness));
            Wall("WallL", new Vector2(-jarHalfWidth - wallThickness * 0.5f, midY),
                 new Vector2(wallThickness, height + wallThickness));
            Wall("WallR", new Vector2(jarHalfWidth + wallThickness * 0.5f, midY),
                 new Vector2(wallThickness, height + wallThickness));
        }

        private void Wall(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.sharedMaterial = Physics.BouncyMaterial();

            // Visible slab so the jar reads on screen.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.30f, 0.27f, 0.38f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static Sprite _solid;
        private static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(4, 4);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels32(px); tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return _solid;
        }
    }
}
