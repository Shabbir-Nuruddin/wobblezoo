using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Owns score, the merge action, the danger line, and game-over / restart.
    /// Draws a lightweight on-screen HUD so the prototype is playable with zero UI setup.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public float DangerY = 2.55f;
        public int Score { get; private set; }
        public int Best { get; private set; }
        public bool IsGameOver { get; private set; }

        private CritterSpawner _spawner;
        private TiltGravity _tilt;
        private GUIStyle _big, _mid, _small;

        private void Awake()
        {
            Instance = this;
            _spawner = GetComponent<CritterSpawner>();
            _tilt = GetComponent<TiltGravity>();
            Best = PlayerPrefs.GetInt("chonky_best", 0);
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

            if (nextTier < GameConfig.TierCount)
            {
                var merged = _spawner.Create(nextTier, mid);
                merged.Drop(); // already in play, so it falls immediately
                PopFx(mid, GameConfig.Tint[nextTier]);
            }
            else
            {
                // Reached the top of the chain — bonus and clear it.
                Score += 200;
                PopFx(mid, Color.white);
            }
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

        public void Restart()
        {
            foreach (var c in FindObjectsByType<Critter>(FindObjectsSortMode.None))
                Destroy(c.gameObject);
            Score = 0;
            IsGameOver = false;
            Physics2D.gravity = new Vector2(0, -26f);
            _spawner.SendMessage("SpawnHeld", SendMessageOptions.DontRequireReceiver);
        }

        private static void PopFx(Vector2 pos, Color color)
        {
            // Cheap juice: a quick expanding ring. Replaced by real art/particles later.
            var go = new GameObject("Pop");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle();
            sr.color = new Color(color.r, color.g, color.b, 0.6f);
            go.AddComponent<PopFx>();
        }

        // ---- Minimal on-screen HUD (no Canvas needed) ----
        private void OnGUI()
        {
            EnsureStyles();
            GUI.Label(new Rect(20, 14, 400, 40), $"Score  {Score}", _big);
            GUI.Label(new Rect(20, 54, 400, 30), $"Best  {Best}", _small);

            string hint = Application.isEditor
                ? "Arrow keys / A-D = tilt      Click or Space = drop"
                : "Tilt your phone to steer      Tap to drop";
            GUI.Label(new Rect(0, Screen.height - 44, Screen.width, 30), hint, _mid);

            if (IsGameOver)
            {
                var box = new Rect(Screen.width / 2f - 150, Screen.height / 2f - 90, 300, 180);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x, box.y + 18, box.width, 40), "Jar Full!", _big);
                GUI.Label(new Rect(box.x, box.y + 66, box.width, 30), $"Score  {Score}", _mid);
                if (GUI.Button(new Rect(box.x + 70, box.y + 110, 160, 46), "Play again"))
                    Restart();
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

    /// <summary>Tiny self-destructing pop animation for merge feedback.</summary>
    public class PopFx : MonoBehaviour
    {
        private float _t;
        private SpriteRenderer _sr;
        private void Awake() => _sr = GetComponent<SpriteRenderer>();
        private void Update()
        {
            _t += Time.deltaTime * 3f;
            float s = Mathf.Lerp(0.4f, 1.6f, _t);
            transform.localScale = new Vector3(s, s, 1f);
            var c = _sr.color; c.a = Mathf.Lerp(0.6f, 0f, _t); _sr.color = c;
            if (_t >= 1f) Destroy(gameObject);
        }
    }
}
