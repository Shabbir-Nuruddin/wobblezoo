using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChonkyMerge
{
    /// <summary>
    /// Wobble Tower mode: stack cute animals on a narrow base. Matching animals that
    /// touch merge into a bigger, heavier one — which shifts the balance. Gravity
    /// follows the phone's tilt, so the whole tower sways; tilt to keep it from
    /// toppling and to aim where the next animal drops. If an animal falls off, the
    /// tower has collapsed. Height + merges = score.
    /// </summary>
    [RequireComponent(typeof(TiltGravity))]
    public class TowerGame : MonoBehaviour
    {
        public static TowerGame Instance { get; private set; }

        [SerializeField] private float baseY = -3.5f;
        [SerializeField] private float baseWidth = 2.6f;
        [SerializeField] private float baseThickness = 0.6f;

        private TiltGravity _tilt;
        private Camera _cam;
        private Critter _held;
        private float _cooldown;
        private float _baseTopY, _killY, _aimRange, _fallX = 7f;

        public int Score { get; private set; }
        public int Best { get; private set; }
        public float MaxHeight { get; private set; }
        public bool IsGameOver { get; private set; }

        private GUIStyle _big, _mid, _small;

        private void Awake()
        {
            Instance = this;
            _tilt = GetComponent<TiltGravity>();
            Critter.TowerMode = true;
            MergeService.Handler = Merge;
            Best = PlayerPrefs.GetInt("chonky_best", 0);
            _baseTopY = baseY + baseThickness * 0.5f;
            _killY = baseY - 1.4f;
            _aimRange = baseWidth * 0.6f;
        }

        private void Start()
        {
            SetupCamera();
            BuildBase();
            Physics2D.gravity = new Vector2(0, -24f);
            SpawnHeld();
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _cam = go.AddComponent<Camera>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = 5.2f;
            _cam.transform.position = new Vector3(0, 0, -10);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.62f, 0.78f, 0.88f); // soft sky
        }

        private void BuildBase()
        {
            var go = new GameObject("Base");
            go.transform.position = new Vector2(0, baseY);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(baseWidth, baseThickness);
            col.sharedMaterial = Physics.CritterMaterial();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.55f, 0.40f, 0.28f);
            sr.sortingOrder = 1;
            go.transform.localScale = new Vector3(baseWidth, baseThickness, 1f);

            // A little grassy mound under the base for cozy framing.
            var mound = new GameObject("Mound");
            mound.transform.position = new Vector2(0, baseY - 2.4f);
            var msr = mound.AddComponent<SpriteRenderer>();
            msr.sprite = SpriteFactory.Circle();
            msr.color = new Color(0.55f, 0.78f, 0.50f);
            msr.sortingOrder = 0;
            mound.transform.localScale = new Vector3(baseWidth * 3.2f, 5f, 1f);
        }

        private void Update()
        {
            if (IsGameOver) return;

            // Aim the held animal with tilt (the same tilt leans the tower).
            if (_held != null)
            {
                float topY = TowerTopY();
                float x = Mathf.Clamp(_tilt.Tilt * _aimRange, -_aimRange, _aimRange);
                _held.transform.position = new Vector3(x, topY + 2.4f, 0f);

                if (DropPressed())
                {
                    _held.Drop();
                    _held = null;
                    _cooldown = 0.55f;
                }
            }
            else if (_cooldown > 0f)
            {
                _cooldown -= Time.deltaTime;
                if (_cooldown <= 0f) SpawnHeld();
            }

            UpdateScoreAndLoss();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            // Frame the whole tower from base to top so you can watch it sway.
            float top = Mathf.Max(TowerTopY(), _held != null ? _held.transform.position.y : _baseTopY) + 1.3f;
            float bottom = baseY - 1.0f;
            float targetSize = Mathf.Clamp((top - bottom) * 0.5f + 0.4f, 5.2f, 14f);
            float targetY = (top + bottom) * 0.5f;
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, Time.deltaTime * 4f);
            var p = _cam.transform.position;
            _cam.transform.position = new Vector3(0, Mathf.Lerp(p.y, targetY, Time.deltaTime * 4f), -10f);
        }

        private void UpdateScoreAndLoss()
        {
            float top = _baseTopY;
            foreach (var c in FindObjectsByType<Critter>(FindObjectsSortMode.None))
            {
                if (c.Consumed || !c.Dropped) continue;
                Vector3 pos = c.transform.position;

                // Fell off the tower / base — collapse.
                if (pos.y < _killY || Mathf.Abs(pos.x) > _fallX)
                {
                    GameOver();
                    return;
                }
                float t = pos.y + GameConfig.Radius[c.Tier];
                if (t > top) top = t;
            }
            float h = Mathf.Max(0f, top - _baseTopY);
            if (h > MaxHeight) MaxHeight = h;
        }

        private float TowerTopY()
        {
            float top = _baseTopY;
            foreach (var c in FindObjectsByType<Critter>(FindObjectsSortMode.None))
            {
                if (c.Consumed || !c.Dropped || !c.HasLanded) continue;
                float t = c.transform.position.y + GameConfig.Radius[c.Tier];
                if (t > top) top = t;
            }
            return top;
        }

        private void SpawnHeld()
        {
            int tier = Random.Range(0, GameConfig.MaxSpawnTier + 1);
            _held = Create(tier, new Vector2(0, TowerTopY() + 2.4f));
        }

        public Critter Create(int tier, Vector2 pos)
        {
            var go = new GameObject($"Critter_T{tier}");
            go.transform.position = pos;
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<SpriteRenderer>().sortingOrder = 2;
            var c = go.AddComponent<Critter>();
            c.Init(tier);
            return c;
        }

        public void Merge(Critter a, Critter b)
        {
            if (a.Consumed || b.Consumed) return;
            a.Consumed = true;
            b.Consumed = true;

            Vector2 mid = (a.transform.position + b.transform.position) * 0.5f;
            int nextTier = a.Tier + 1;

            Destroy(a.gameObject);
            Destroy(b.gameObject);

            Score += GameConfig.ScoreForTier(a.Tier);
            Sfx.Pop();

            if (nextTier < GameConfig.TierCount)
            {
                var merged = Create(nextTier, mid);
                merged.Drop();
            }
            else
            {
                Score += 300;
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

        public void GameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            if (Score > Best)
            {
                Best = Score;
                PlayerPrefs.SetInt("chonky_best", Best);
                PlayerPrefs.Save();
            }
        }

        public void Restart() => SceneManager.LoadScene("Tower");

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

        private void OnGUI()
        {
            EnsureStyles();
            GUI.Label(new Rect(20, 12, 400, 40), $"Score  {Score}", _big);
            GUI.Label(new Rect(20, 52, 400, 30), $"Height  {MaxHeight:0.0}   Best  {Best}", _small);

            string hint = Application.isEditor
                ? "Arrow keys / A-D = tilt & balance      Click / Space = drop"
                : "Tilt to balance the tower      Tap to drop";
            GUI.Label(new Rect(0, Screen.height - 44, Screen.width, 30), hint, _mid);

            if (!IsGameOver && GUI.Button(new Rect(Screen.width - 92, 16, 76, 44), "Menu"))
            { Sfx.Click(); SceneManager.LoadScene("MainMenu"); }

            if (IsGameOver)
            {
                var box = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 120, 320, 250);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x, box.y + 18, box.width, 40), "Tower Toppled!", _big);
                GUI.Label(new Rect(box.x, box.y + 62, box.width, 30), $"Score  {Score}", _mid);
                GUI.Label(new Rect(box.x, box.y + 92, box.width, 30), $"Height  {MaxHeight:0.0}", _small);
                if (GUI.Button(new Rect(box.x + 80, box.y + 128, 160, 48), "Play again"))
                { Sfx.Click(); Restart(); }
                if (GUI.Button(new Rect(box.x + 80, box.y + 186, 160, 44), "Menu"))
                { Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
            }
        }

        private void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperCenter };
            _big.normal.textColor = _mid.normal.textColor = _small.normal.textColor = Color.white;
        }
    }
}
