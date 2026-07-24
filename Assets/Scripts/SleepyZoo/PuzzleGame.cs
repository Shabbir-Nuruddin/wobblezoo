using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChonkyMerge; // reuse AnimalSprites + SpriteFactory

namespace SleepyZoo
{
    /// <summary>
    /// Cozy connect-the-animals puzzle. Drag a trail from each cute animal to its
    /// matching twin; trails can't cross; connect every pair to solve the level.
    /// Pure grid logic — deterministic and physics-free, so it stays robust.
    /// </summary>
    public class PuzzleGame : MonoBehaviour
    {
        // ---- level data ----
        private struct Pair { public int tier, ax, ay, bx, by; public Pair(int t,int ax,int ay,int bx,int by){tier=t;this.ax=ax;this.ay=ay;this.bx=bx;this.by=by;} }
        private class Level { public int w, h; public Pair[] pairs; public Level(int w,int h,Pair[] p){this.w=w;this.h=h;pairs=p;} }

        private static readonly Level[] Levels =
        {
            // L1 – gentle intro: three straight rows.
            new Level(5,5, new[]{ new Pair(0,0,0,4,0), new Pair(2,0,2,4,2), new Pair(1,0,4,4,4) }),
            // L2 – columns, different animals.
            new Level(5,5, new[]{ new Pair(3,0,0,0,4), new Pair(5,2,0,2,4), new Pair(7,4,0,4,4) }),
            // L3 – needs a bend.
            new Level(5,5, new[]{ new Pair(4,0,0,4,0), new Pair(6,0,4,4,4), new Pair(2,2,1,2,3) }),
        };

        private static readonly Color[] Palette =
        {
            new Color(0.95f,0.55f,0.45f), new Color(0.55f,0.70f,0.95f),
            new Color(0.60f,0.82f,0.55f), new Color(0.90f,0.70f,0.40f),
            new Color(0.80f,0.55f,0.85f), new Color(0.45f,0.80f,0.82f),
            new Color(0.95f,0.72f,0.80f), new Color(0.75f,0.75f,0.80f),
        };

        // ---- runtime ----
        private int _levelIndex;
        private Level _lv;
        private Camera _cam;
        private float _cell = 1f, _ox, _oy;

        private readonly Dictionary<int, List<Vector2Int>> _paths = new();
        private readonly Dictionary<Vector2Int, int> _owner = new();   // path-cell owner
        private readonly Dictionary<Vector2Int, int> _endpoint = new(); // endpoint-cell -> colorId
        private readonly Dictionary<int, (Vector2Int a, Vector2Int b)> _ends = new();

        private int _drawing = -1;
        private readonly List<GameObject> _pathVisuals = new();
        private bool _solved;
        private GUIStyle _big, _mid, _small;

        private void Start()
        {
            SetupCamera();
            LoadLevel(0);
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) { var go=new GameObject("Main Camera"); go.tag="MainCamera"; _cam=go.AddComponent<Camera>(); }
            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0,0,-10);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.20f,0.18f,0.30f); // cozy night
        }

        private void LoadLevel(int index)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            _pathVisuals.Clear(); _paths.Clear(); _owner.Clear(); _endpoint.Clear(); _ends.Clear();
            _drawing = -1; _solved = false;

            _levelIndex = Mathf.Clamp(index, 0, Levels.Length - 1);
            _lv = Levels[_levelIndex];

            _ox = -(_lv.w - 1) * _cell * 0.5f;
            _oy = -(_lv.h - 1) * _cell * 0.5f;
            _cam.orthographicSize = Mathf.Max(_lv.w * 0.62f, _lv.h * 0.5f + 1.6f);

            BuildBoard();

            for (int i = 0; i < _lv.pairs.Length; i++)
            {
                var p = _lv.pairs[i];
                var a = new Vector2Int(p.ax, p.ay);
                var b = new Vector2Int(p.bx, p.by);
                _ends[i] = (a, b);
                _endpoint[a] = i; _endpoint[b] = i;
                _paths[i] = new List<Vector2Int>();
                SpawnEndpoint(a, i, p.tier);
                SpawnEndpoint(b, i, p.tier);
            }
        }

        private void BuildBoard()
        {
            for (int y = 0; y < _lv.h; y++)
            for (int x = 0; x < _lv.w; x++)
            {
                var go = Tile($"Cell_{x}_{y}", new Vector2Int(x,y), 0, new Color(0.28f,0.26f,0.40f));
                go.transform.localScale = new Vector3(_cell*0.94f, _cell*0.94f, 1f);
            }
        }

        private GameObject Tile(string name, Vector2Int cell, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = CellToWorld(cell);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle();  // soft rounded blob = cozy rounded cell
            sr.color = color; sr.sortingOrder = order;
            go.transform.localScale = new Vector3(_cell, _cell, 1f);
            return go;
        }

        private void SpawnEndpoint(Vector2Int cell, int colorId, int tier)
        {
            // colored pad
            var pad = Tile($"Pad_{colorId}", cell, 2, Palette[colorId % Palette.Length]);
            pad.transform.localScale = new Vector3(_cell*0.86f, _cell*0.86f, 1f);
            // animal on top
            var animal = AnimalSprites.Get(tier);
            var go = new GameObject($"Animal_{colorId}");
            go.transform.SetParent(transform);
            go.transform.position = CellToWorld(cell) + new Vector3(0,0,-0.1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;
            if (animal != null) { sr.sprite = animal; float s=(_cell*0.78f)/animal.bounds.size.x; go.transform.localScale=new Vector3(s,s,1f); }
            else { sr.sprite = SpriteFactory.Circle(); sr.color = Palette[colorId%Palette.Length]; go.transform.localScale=new Vector3(_cell*0.5f,_cell*0.5f,1f); }
        }

        private Vector3 CellToWorld(Vector2Int c) => new Vector3(_ox + c.x*_cell, _oy + c.y*_cell, 0);

        private bool WorldToCell(Vector3 w, out Vector2Int cell)
        {
            int x = Mathf.RoundToInt((w.x - _ox)/_cell);
            int y = Mathf.RoundToInt((w.y - _oy)/_cell);
            cell = new Vector2Int(x,y);
            return x>=0 && x<_lv.w && y>=0 && y<_lv.h;
        }

        private void Update()
        {
            if (_solved) return;

            if (Input.GetMouseButtonDown(0)) BeginDraw();
            else if (Input.GetMouseButton(0) && _drawing >= 0) ContinueDraw();
            else if (Input.GetMouseButtonUp(0) && _drawing >= 0) { _drawing = -1; CheckWin(); }
        }

        private Vector3 PointerWorld() => _cam.ScreenToWorldPoint(Input.mousePosition);

        private void BeginDraw()
        {
            if (!WorldToCell(PointerWorld(), out var cell)) return;
            if (!_endpoint.TryGetValue(cell, out int c)) return; // must start on an animal
            _drawing = c;
            ClearPath(c);
            _paths[c].Add(cell);
            RedrawPaths();
        }

        private void ContinueDraw()
        {
            if (!WorldToCell(PointerWorld(), out var cell)) return;
            int c = _drawing;
            var path = _paths[c];
            if (path.Count == 0) return;
            var tip = path[path.Count - 1];
            if (cell == tip) return;
            if ((cell - tip).sqrMagnitude != 1) return; // must be 4-adjacent

            // backtrack
            if (path.Count >= 2 && cell == path[path.Count - 2]) { RemoveOwner(tip); path.RemoveAt(path.Count-1); RedrawPaths(); return; }
            if (path.Count == 1 && cell == _ends[c].a) return;

            // reached matching endpoint?
            bool isOwnFarEnd = cell == OtherEnd(c, path[0]);
            if (!isOwnFarEnd)
            {
                if (_endpoint.ContainsKey(cell)) return;        // another color's endpoint (or own start) — block
                if (_owner.TryGetValue(cell, out int o)) return; // occupied
                if (path.Contains(cell)) return;                 // no self loop
            }
            else if (path.Contains(cell)) return;

            path.Add(cell);
            if (!_endpoint.ContainsKey(cell)) _owner[cell] = c;
            RedrawPaths();
        }

        private Vector2Int OtherEnd(int c, Vector2Int start) => start == _ends[c].a ? _ends[c].b : _ends[c].a;

        private void ClearPath(int c)
        {
            foreach (var cell in _paths[c]) if (!_endpoint.ContainsKey(cell)) _owner.Remove(cell);
            _paths[c].Clear();
        }
        private void RemoveOwner(Vector2Int cell){ if(!_endpoint.ContainsKey(cell)) _owner.Remove(cell); }

        private void RedrawPaths()
        {
            foreach (var v in _pathVisuals) Destroy(v);
            _pathVisuals.Clear();
            foreach (var kv in _paths)
            {
                int c = kv.Key; var col = Palette[c % Palette.Length];
                foreach (var cell in kv.Value)
                {
                    var go = new GameObject($"Path_{c}");
                    go.transform.SetParent(transform);
                    go.transform.position = CellToWorld(cell) + new Vector3(0,0,0.05f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteFactory.Circle(); sr.color = col; sr.sortingOrder = 1;
                    go.transform.localScale = new Vector3(_cell*0.55f, _cell*0.55f, 1f);
                    _pathVisuals.Add(go);
                }
            }
        }

        private void CheckWin()
        {
            foreach (var kv in _paths)
            {
                var path = kv.Value;
                if (path.Count < 2) return;
                var ends = _ends[kv.Key];
                var lo = path[0]; var hi = path[path.Count-1];
                bool connected = (lo==ends.a && hi==ends.b) || (lo==ends.b && hi==ends.a);
                if (!connected) return;
            }
            _solved = true;
            Sfx.Pop();
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.Label(new Rect(0, 14, Screen.width, 36), $"Level {_levelIndex + 1}", _big);
            GUI.Label(new Rect(0, Screen.height-42, Screen.width, 28),
                "Drag from an animal to its twin — don't cross the trails", _mid);

            if (!_solved && GUI.Button(new Rect(20, 16, 88, 40), "Menu"))
            { Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
            if (!_solved && GUI.Button(new Rect(Screen.width-108, 16, 88, 40), "Reset"))
            { Sfx.Click(); LoadLevel(_levelIndex); }

            if (_solved)
            {
                var box = new Rect(Screen.width/2f-160, Screen.height/2f-90, 320, 180);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x, box.y+18, box.width, 40), "All tucked in! 💤", _big);
                bool last = _levelIndex >= Levels.Length - 1;
                if (GUI.Button(new Rect(box.x+80, box.y+80, 160, 48), last ? "Play again" : "Next level"))
                { Sfx.Click(); LoadLevel(last ? 0 : _levelIndex + 1); }
            }
        }

        private void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label){ fontSize=30, fontStyle=FontStyle.Bold, alignment=TextAnchor.UpperCenter };
            _mid = new GUIStyle(GUI.skin.label){ fontSize=19, alignment=TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.label){ fontSize=16, alignment=TextAnchor.UpperCenter };
            _big.normal.textColor = _mid.normal.textColor = _small.normal.textColor = Color.white;
        }
    }
}
