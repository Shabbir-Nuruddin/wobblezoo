using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChonkyMerge; // AnimalSprites, Sfx

namespace SleepyZoo
{
    /// <summary>
    /// Animals-as-mechanic puzzle. Swipe an animal and it moves by ITS OWN rule —
    /// cat steps one cell, hamster rolls until it hits something, bunny hops over the
    /// next cell. Get every animal onto its matching bed. Pure turn-based grid logic:
    /// deterministic, no physics, fully undo-able.
    /// </summary>
    public class PuzzleGame : MonoBehaviour
    {
        private enum Move { Step, Roll, Hop }
        private struct EntDef { public int tier; public Move move; public int x, y, bx, by;
            public EntDef(int t, Move m, int x, int y, int bx, int by){tier=t;move=m;this.x=x;this.y=y;this.bx=bx;this.by=by;} }
        private class Lv { public int w, h; public Vector2Int[] walls; public EntDef[] ents;
            public Lv(int w,int h,Vector2Int[] walls,EntDef[] e){this.w=w;this.h=h;this.walls=walls;this.ents=e;} }

        // tiers: 0 hamster,1 bunny,2 kitten(cat),3 puppy,4 persian,5 corgi,6 samoyed,7 capybara
        private static readonly Lv[] Levels =
        {
            // L1 — meet the CAT: steps one cell. Navigate around a wall.
            new Lv(3,3, new[]{ new Vector2Int(1,1) },
                new[]{ new EntDef(2, Move.Step, 0,0, 2,2) }),
            // L2 — meet the HAMSTER: rolls till it hits a wall. Position matters.
            new Lv(5,5, new[]{ new Vector2Int(3,2) },
                new[]{ new EntDef(0, Move.Roll, 0,2, 2,2) }),
            // L3 — meet the BUNNY: hops over the next cell (a wall here).
            new Lv(5,5, new[]{ new Vector2Int(1,0) },
                new[]{ new EntDef(1, Move.Hop, 0,0, 2,0) }),
            // L4 — combine: park the cat as a wall so the hamster's roll stops on its bed.
            new Lv(5,5, new Vector2Int[0],
                new[]{ new EntDef(0, Move.Roll, 0,0, 2,0),
                       new EntDef(2, Move.Step, 3,4, 3,0) }),
        };

        // ---- runtime ----
        private int _levelIndex;
        private Lv _lv;
        private Camera _cam;
        private readonly HashSet<Vector2Int> _walls = new();
        private Vector2Int[] _pos, _bed;
        private Move[] _move;
        private int[] _tier;
        private Transform[] _view;
        private Vector3[] _target;
        private readonly Dictionary<Vector2Int,int> _occ = new();
        private readonly Stack<Vector2Int[]> _undo = new();

        private int _selected = -1, _swipeEnt = -1, _moves;
        private Vector2 _swipeStart;
        private bool _solved;
        private GUIStyle _big, _mid;

        private void Start(){ SetupCamera(); LoadLevel(0); }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null){ var go=new GameObject("Main Camera"); go.tag="MainCamera"; _cam=go.AddComponent<Camera>(); }
            _cam.orthographic = true;
            _cam.transform.position = new Vector3(0,0,-10);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.20f,0.18f,0.30f);
        }

        private void LoadLevel(int index)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            _walls.Clear(); _occ.Clear(); _undo.Clear();
            _selected=-1; _swipeEnt=-1; _moves=0; _solved=false;

            _levelIndex = Mathf.Clamp(index,0,Levels.Length-1);
            _lv = Levels[_levelIndex];
            foreach (var w in _lv.walls) _walls.Add(w);

            int n=_lv.ents.Length;
            _pos=new Vector2Int[n]; _bed=new Vector2Int[n]; _move=new Move[n]; _tier=new int[n];
            _view=new Transform[n]; _target=new Vector3[n];

            BuildBoard();
            for (int i=0;i<n;i++)
            {
                var e=_lv.ents[i];
                _pos[i]=new Vector2Int(e.x,e.y); _bed[i]=new Vector2Int(e.bx,e.by);
                _move[i]=e.move; _tier[i]=e.tier;
                _occ[_pos[i]]=i;
                SpawnBed(i); SpawnAnimal(i);
            }
            FrameCamera();
        }

        // ---- board / sprites ----
        private void BuildBoard()
        {
            for (int y=0;y<_lv.h;y++)
            for (int x=0;x<_lv.w;x++)
            {
                var cell=new Vector2Int(x,y);
                bool wall=_walls.Contains(cell);
                var t=Tile(CellToWorld(cell),0, wall? new Color(0.13f,0.12f,0.20f):new Color(0.30f,0.28f,0.42f));
                t.localScale=new Vector3(0.94f,0.94f,1f);
            }
        }

        private Transform Tile(Vector3 pos,int order,Color col)
        {
            var go=new GameObject("Tile"); go.transform.SetParent(transform); go.transform.position=pos;
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=RoundedTile(); sr.color=col; sr.sortingOrder=order;
            return go.transform;
        }

        private void SpawnBed(int i)
        {
            var pad=Tile(CellToWorld(_bed[i]),1,new Color(0.40f,0.38f,0.55f)); pad.localScale=new Vector3(0.80f,0.80f,1f);
            var s=AnimalSprites.Get(_tier[i]);
            var go=new GameObject("Bed"); go.transform.SetParent(transform); go.transform.position=CellToWorld(_bed[i]);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=2;
            if(s!=null){ sr.sprite=s; sr.color=new Color(1,1,1,0.28f); float sc=0.66f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);}
        }

        private void SpawnAnimal(int i)
        {
            var s=AnimalSprites.Get(_tier[i]);
            var go=new GameObject("Animal"); go.transform.SetParent(transform); go.transform.position=CellToWorld(_pos[i]);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=5;
            if(s!=null){ sr.sprite=s; float sc=0.82f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);}
            else { sr.sprite=RoundedTile(); go.transform.localScale=new Vector3(0.7f,0.7f,1f);}
            _view[i]=go.transform; _target[i]=CellToWorld(_pos[i]);
        }

        private Vector3 CellToWorld(Vector2Int c)=>new Vector3(c.x-(_lv.w-1)*0.5f, c.y-(_lv.h-1)*0.5f, 0);
        private bool WorldToCell(Vector3 w, out Vector2Int cell)
        {
            int x=Mathf.RoundToInt(w.x+(_lv.w-1)*0.5f);
            int y=Mathf.RoundToInt(w.y+(_lv.h-1)*0.5f);
            cell=new Vector2Int(x,y);
            return x>=0&&x<_lv.w&&y>=0&&y<_lv.h;
        }

        private void FrameCamera()
        {
            float aspect=Mathf.Max(0.3f,(float)Screen.width/Screen.height);
            float margin=1.1f;
            float sizeH=_lv.h*0.5f+margin;
            float sizeW=(_lv.w*0.5f+margin)/aspect;
            _cam.orthographicSize=Mathf.Max(sizeH,sizeW);
        }

        // ---- input ----
        private void Update()
        {
            FrameCamera();
            for (int i=0;i<_view.Length;i++)
            {
                float sc = (i==_selected?1.1f:1f);
                var s=AnimalSprites.Get(_tier[i]); float baseSc = s!=null? 0.82f/s.bounds.size.x : 0.7f;
                _view[i].position=Vector3.Lerp(_view[i].position,_target[i],Time.deltaTime*16f);
                _view[i].localScale=Vector3.Lerp(_view[i].localScale,new Vector3(baseSc*sc,baseSc*sc,1f),Time.deltaTime*12f);
            }
            if (_solved) return;

            if (Input.GetMouseButtonDown(0))
            {
                _swipeStart=Input.mousePosition;
                _swipeEnt = WorldToCell(_cam.ScreenToWorldPoint(Input.mousePosition),out var c) && _occ.TryGetValue(c,out int e)? e : -1;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                Vector2 d=(Vector2)Input.mousePosition-_swipeStart;
                if (d.magnitude>30f && _swipeEnt>=0) DoMove(_swipeEnt, Dir(d));
                else if (_swipeEnt>=0) _selected=_swipeEnt; // tap = select (for arrow keys)
                _swipeEnt=-1;
            }

            if (_selected>=0)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow)) DoMove(_selected,Vector2Int.right);
                else if (Input.GetKeyDown(KeyCode.LeftArrow)) DoMove(_selected,Vector2Int.left);
                else if (Input.GetKeyDown(KeyCode.UpArrow)) DoMove(_selected,Vector2Int.up);
                else if (Input.GetKeyDown(KeyCode.DownArrow)) DoMove(_selected,Vector2Int.down);
            }
        }

        private static Vector2Int Dir(Vector2 d)=> Mathf.Abs(d.x)>Mathf.Abs(d.y)
            ? (d.x>0?Vector2Int.right:Vector2Int.left)
            : (d.y>0?Vector2Int.up:Vector2Int.down);

        private bool Free(Vector2Int c)=> c.x>=0&&c.x<_lv.w&&c.y>=0&&c.y<_lv.h && !_walls.Contains(c) && !_occ.ContainsKey(c);

        private void DoMove(int i, Vector2Int dir)
        {
            Vector2Int from=_pos[i], to=from;
            switch(_move[i])
            {
                case Move.Step: { var n=from+dir; if(Free(n)) to=n; break; }
                case Move.Roll: { var cur=from; while(Free(cur+dir)) cur+=dir; to=cur; break; }
                case Move.Hop:  { var land=from+dir*2; if(Free(land)) to=land; break; }
            }
            if (to==from) return; // nothing moved — no wasted "turn"

            PushUndo();
            _occ.Remove(from); _pos[i]=to; _occ[to]=i; _target[i]=CellToWorld(to);
            _moves++; Sfx.Click();
            CheckWin();
        }

        private void PushUndo(){ var snap=new Vector2Int[_pos.Length]; System.Array.Copy(_pos,snap,_pos.Length); _undo.Push(snap); }
        private void Undo()
        {
            if(_undo.Count==0) return;
            var snap=_undo.Pop(); _occ.Clear();
            for(int i=0;i<_pos.Length;i++){ _pos[i]=snap[i]; _occ[snap[i]]=i; _target[i]=CellToWorld(snap[i]); }
            Sfx.Click();
        }

        private void CheckWin()
        {
            for(int i=0;i<_pos.Length;i++) if(_pos[i]!=_bed[i]) return;
            _solved=true; Sfx.Pop();
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.Label(new Rect(0,12,Screen.width,34), $"Level {_levelIndex+1}", _big);
            GUI.Label(new Rect(0,Screen.height-40,Screen.width,26), MoveHint(), _mid);

            if(!_solved)
            {
                if(GUI.Button(new Rect(16,14,84,40),"Menu")){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
                if(GUI.Button(new Rect(16,Screen.height-100,120,44),"Undo")) Undo();
                if(GUI.Button(new Rect(Screen.width-136,Screen.height-100,120,44),"Reset")){ Sfx.Click(); LoadLevel(_levelIndex); }
            }
            else
            {
                var box=new Rect(Screen.width/2f-160,Screen.height/2f-90,320,180);
                GUI.Box(box,GUIContent.none);
                GUI.Label(new Rect(box.x,box.y+18,box.width,40),"All tucked in!",_big);
                GUI.Label(new Rect(box.x,box.y+60,box.width,26),$"{_moves} moves",_mid);
                bool last=_levelIndex>=Levels.Length-1;
                if(GUI.Button(new Rect(box.x+80,box.y+104,160,48), last?"Play again":"Next level"))
                { Sfx.Click(); LoadLevel(last?0:_levelIndex+1); }
            }
        }

        private string MoveHint()
        {
            if (_selected<0) return "Swipe an animal to move it — get everyone to their bed";
            return _move[_selected] switch {
                Move.Step => "Cat: steps one cell",
                Move.Roll => "Hamster: rolls until it hits something",
                Move.Hop  => "Bunny: hops over the next cell",
                _ => "" };
        }

        // rounded-square tile sprite
        private static Sprite _round;
        private static Sprite RoundedTile()
        {
            if(_round!=null) return _round;
            int s=128; float r=26f, half=s*0.5f;
            var tex=new Texture2D(s,s,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[s*s];
            for(int y=0;y<s;y++)for(int x=0;x<s;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x+0.5f-half)-(half-r),0f);
                float dy=Mathf.Max(Mathf.Abs(y+0.5f-half)-(half-r),0f);
                float dist=Mathf.Sqrt(dx*dx+dy*dy);
                float a=Mathf.Clamp01((r-dist)/1.5f);
                px[y*s+x]=new Color32(255,255,255,(byte)(a*255));
            }
            tex.SetPixels32(px); tex.Apply();
            _round=Sprite.Create(tex,new Rect(0,0,s,s),new Vector2(0.5f,0.5f),s);
            return _round;
        }

        private void EnsureStyles()
        {
            if(_big!=null) return;
            _big=new GUIStyle(GUI.skin.label){fontSize=28,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _mid=new GUIStyle(GUI.skin.label){fontSize=18,alignment=TextAnchor.MiddleCenter};
            _big.normal.textColor=_mid.normal.textColor=Color.white;
        }
    }
}
