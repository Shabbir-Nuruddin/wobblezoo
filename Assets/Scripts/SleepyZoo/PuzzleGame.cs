using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChonkyMerge; // AnimalSprites, Sfx, SpriteFactory

namespace SleepyZoo
{
    /// <summary>
    /// Animals-as-mechanic puzzle. Swipe an animal; it moves by ITS rule —
    /// cat steps, hamster rolls, bunny hops, corgi pushes, capybara is a pushable
    /// sleepy block. Get every animal onto its bed. Turn-based grid, deterministic,
    /// full undo, move-par + stars.
    /// </summary>
    public class PuzzleGame : MonoBehaviour
    {
        private enum Move { Step, Roll, Hop, Push, Block }
        private struct EntDef { public int tier; public Move move; public int x, y, bx, by;
            public EntDef(int t, Move m, int x, int y, int bx, int by){tier=t;move=m;this.x=x;this.y=y;this.bx=bx;this.by=by;} }
        private class Lv { public int w, h, par; public Vector2Int[] walls; public EntDef[] ents;
            public Lv(int w,int h,int par,Vector2Int[] walls,EntDef[] e){this.w=w;this.h=h;this.par=par;this.walls=walls;this.ents=e;} }

        // tiers: 0 hamster,1 bunny,2 cat,3 puppy,4 persian,5 corgi,6 samoyed,7 capybara
        private static readonly Lv[] Levels =
        {
            new Lv(4,4,6, new[]{ new Vector2Int(1,0),new Vector2Int(1,1),new Vector2Int(1,2) },
                new[]{ new EntDef(2,Move.Step, 0,0, 3,3) }),
            new Lv(5,5,2, new[]{ new Vector2Int(3,0),new Vector2Int(2,3) },
                new[]{ new EntDef(0,Move.Roll, 0,0, 2,2) }),
            new Lv(5,5,2, new[]{ new Vector2Int(1,0),new Vector2Int(2,1) },
                new[]{ new EntDef(1,Move.Hop, 0,0, 2,2) }),
            new Lv(5,5,2, new Vector2Int[0],
                new[]{ new EntDef(5,Move.Push, 0,0, 2,0), new EntDef(7,Move.Block, 1,0, 3,0) }),
            new Lv(5,5,5, new Vector2Int[0],
                new[]{ new EntDef(0,Move.Roll, 0,0, 2,0), new EntDef(2,Move.Step, 3,4, 3,0) }),
            new Lv(5,5,8, new[]{ new Vector2Int(1,1),new Vector2Int(3,1),new Vector2Int(1,3),new Vector2Int(3,3) },
                new[]{ new EntDef(2,Move.Step, 0,0, 4,4) }),
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

        private int _selected=-1, _swipeEnt=-1, _moves, _stars;
        private Vector2 _swipeStart;
        private bool _solved;
        private GUIStyle _title, _hud, _btn, _win;

        private void Start(){ SetupCamera(); LoadLevel(0); }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam==null){ var go=new GameObject("Main Camera"); go.tag="MainCamera"; _cam=go.AddComponent<Camera>(); }
            _cam.orthographic=true; _cam.transform.position=new Vector3(0,0,-10);
            _cam.clearFlags=CameraClearFlags.SolidColor; _cam.backgroundColor=new Color(0.12f,0.10f,0.20f);
        }

        private void LoadLevel(int index)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            _walls.Clear(); _occ.Clear(); _undo.Clear();
            _selected=-1; _swipeEnt=-1; _moves=0; _solved=false; _stars=0;

            _levelIndex=Mathf.Clamp(index,0,Levels.Length-1);
            _lv=Levels[_levelIndex];
            foreach (var w in _lv.walls) _walls.Add(w);

            int n=_lv.ents.Length;
            _pos=new Vector2Int[n]; _bed=new Vector2Int[n]; _move=new Move[n]; _tier=new int[n];
            _view=new Transform[n]; _target=new Vector3[n];

            SpawnBackground();
            BuildBoard();
            for (int i=0;i<n;i++)
            {
                var e=_lv.ents[i];
                _pos[i]=new Vector2Int(e.x,e.y); _bed[i]=new Vector2Int(e.bx,e.by);
                _move[i]=e.move; _tier[i]=e.tier; _occ[_pos[i]]=i;
                SpawnBed(i); SpawnAnimal(i);
            }
            FrameCamera();
        }

        // ---- visuals ----
        private void SpawnBackground()
        {
            var go=new GameObject("BG"); go.transform.SetParent(transform); go.transform.position=new Vector3(0,0,1);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=BgSprite(); sr.sortingOrder=-20;
            go.transform.localScale=new Vector3(60,60,1);
        }

        private void BuildBoard()
        {
            for (int y=0;y<_lv.h;y++)
            for (int x=0;x<_lv.w;x++)
            {
                var cell=new Vector2Int(x,y);
                bool wall=_walls.Contains(cell);
                var t=Tile(CellToWorld(cell),0, wall?new Color(0.14f,0.12f,0.22f):new Color(0.34f,0.31f,0.48f));
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
            var pad=Tile(CellToWorld(_bed[i]),1,new Color(0.46f,0.43f,0.62f)); pad.localScale=new Vector3(0.80f,0.80f,1f);
            var s=AnimalSprites.Get(_tier[i]);
            var go=new GameObject("Bed"); go.transform.SetParent(transform); go.transform.position=CellToWorld(_bed[i]);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=2;
            if(s!=null){ sr.sprite=s; sr.color=new Color(1,1,1,0.30f); float sc=0.64f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);}
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
            int x=Mathf.RoundToInt(w.x+(_lv.w-1)*0.5f), y=Mathf.RoundToInt(w.y+(_lv.h-1)*0.5f);
            cell=new Vector2Int(x,y); return x>=0&&x<_lv.w&&y>=0&&y<_lv.h;
        }
        private void FrameCamera()
        {
            float aspect=Mathf.Max(0.3f,(float)Screen.width/Screen.height), margin=1.2f;
            _cam.orthographicSize=Mathf.Max(_lv.h*0.5f+margin,(_lv.w*0.5f+margin)/aspect);
        }

        // ---- input ----
        private void Update()
        {
            FrameCamera();
            for (int i=0;i<_view.Length;i++)
            {
                float sc=(i==_selected?1.1f:1f);
                var s=AnimalSprites.Get(_tier[i]); float b=s!=null?0.82f/s.bounds.size.x:0.7f;
                _view[i].position=Vector3.Lerp(_view[i].position,_target[i],Time.deltaTime*16f);
                _view[i].localScale=Vector3.Lerp(_view[i].localScale,new Vector3(b*sc,b*sc,1f),Time.deltaTime*12f);
            }
            if (_solved) return;

            if (Input.GetMouseButtonDown(0))
            {
                _swipeStart=Input.mousePosition;
                _swipeEnt=-1;
                if (WorldToCell(_cam.ScreenToWorldPoint(Input.mousePosition),out var c) && _occ.TryGetValue(c,out int e) && _move[e]!=Move.Block)
                    _swipeEnt=e;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                Vector2 d=(Vector2)Input.mousePosition-_swipeStart;
                if (d.magnitude>30f && _swipeEnt>=0) DoMove(_swipeEnt,Dir(d));
                else if (_swipeEnt>=0) _selected=_swipeEnt;
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

        private static Vector2Int Dir(Vector2 d)=>Mathf.Abs(d.x)>Mathf.Abs(d.y)?(d.x>0?Vector2Int.right:Vector2Int.left):(d.y>0?Vector2Int.up:Vector2Int.down);
        private bool Free(Vector2Int c)=>c.x>=0&&c.x<_lv.w&&c.y>=0&&c.y<_lv.h&&!_walls.Contains(c)&&!_occ.ContainsKey(c);

        private void DoMove(int i, Vector2Int dir)
        {
            Vector2Int from=_pos[i], to=from; int pushed=-1; Vector2Int pushTo=default;
            switch(_move[i])
            {
                case Move.Step: { var n=from+dir; if(Free(n)) to=n; break; }
                case Move.Roll: { var cur=from; while(Free(cur+dir)) cur+=dir; to=cur; break; }
                case Move.Hop:  { var land=from+dir*2; if(Free(land)) to=land; break; }
                case Move.Push:
                {
                    var n=from+dir;
                    if(Free(n)){ to=n; }                       // nothing ahead — just step
                    else if(_occ.TryGetValue(n,out int j))     // animal ahead — push if space beyond
                    {
                        var beyond=n+dir;
                        if(Free(beyond)){ pushed=j; pushTo=beyond; to=n; }
                    }
                    break;
                }
            }
            if (to==from && pushed<0) return;

            PushUndo();
            if (pushed>=0){ _occ.Remove(_pos[pushed]); _pos[pushed]=pushTo; _occ[pushTo]=pushed; _target[pushed]=CellToWorld(pushTo); }
            _occ.Remove(from); _pos[i]=to; _occ[to]=i; _target[i]=CellToWorld(to);
            _moves++; Sfx.Click(); CheckWin();
        }

        private void PushUndo(){ var s=new Vector2Int[_pos.Length]; System.Array.Copy(_pos,s,_pos.Length); _undo.Push(s); }
        private void Undo()
        {
            if(_undo.Count==0) return;
            var s=_undo.Pop(); _occ.Clear();
            for(int i=0;i<_pos.Length;i++){ _pos[i]=s[i]; _occ[s[i]]=i; _target[i]=CellToWorld(s[i]); }
            if(_moves>0) _moves--; Sfx.Click();
        }

        private void CheckWin()
        {
            for(int i=0;i<_pos.Length;i++) if(_pos[i]!=_bed[i]) return;
            _solved=true;
            _stars = _moves<=_lv.par ? 3 : (_moves<=_lv.par+2 ? 2 : 1);
            Sfx.Pop();
        }

        // ---- UI ----
        private void OnGUI()
        {
            EnsureStyles();
            var sa=Screen.safeArea;
            float top=Screen.height-(sa.y+sa.height)+16f;     // top inset
            float bot=sa.y+16f;                               // bottom inset

            GUI.Label(new Rect(0,top,Screen.width,44), $"Level {_levelIndex+1}", _title);
            GUI.Label(new Rect(0,top+52,Screen.width,32), $"Moves {_moves}   ·   Par {_lv.par}", _hud);

            if(!_solved)
            {
                float by=Screen.height-bot-84f;
                if(GUI.Button(new Rect(Screen.width/2f-170,by,150,72),"Undo",_btn)) Undo();
                if(GUI.Button(new Rect(Screen.width/2f+20,by,150,72),"Reset",_btn)){ Sfx.Click(); LoadLevel(_levelIndex); }
                if(GUI.Button(new Rect(20,top,132,64),"Menu",_btn)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
                GUI.Label(new Rect(0,Screen.height-bot-150,Screen.width,28), Hint(), _hud);
            }
            else
            {
                float w=Mathf.Min(Screen.width*0.86f,620), h=380;
                var box=new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h);
                GUI.Box(box,GUIContent.none);
                GUI.Label(new Rect(box.x,box.y+26,box.width,50),"All tucked in!",_win);
                DrawStars(box.center.x, box.y+110, _stars);
                GUI.Label(new Rect(box.x,box.y+170,box.width,32), $"{_moves} moves   ·   Par {_lv.par}", _hud);
                bool last=_levelIndex>=Levels.Length-1;
                if(GUI.Button(new Rect(box.x+box.width/2-150,box.y+230,300,74), last?"Play again":"Next level",_btn))
                { Sfx.Click(); LoadLevel(last?0:_levelIndex+1); }
            }
        }

        private void DrawStars(float cx,float y,int count)
        {
            var tex=SpriteFactory.Circle().texture; float s=54, gap=14; float total=3*s+2*gap;
            for(int i=0;i<3;i++)
            {
                GUI.color = i<count ? new Color(1f,0.82f,0.25f) : new Color(1,1,1,0.18f);
                GUI.DrawTexture(new Rect(cx-total/2+i*(s+gap),y,s,s),tex);
            }
            GUI.color=Color.white;
        }

        private string Hint()
        {
            if(_selected<0) return "Swipe an animal — each moves differently. Get everyone to bed.";
            return _move[_selected] switch {
                Move.Step=>"Cat: steps one cell",
                Move.Roll=>"Hamster: rolls until it hits something",
                Move.Hop=>"Bunny: hops over the next cell",
                Move.Push=>"Corgi: pushes the animal in front",
                _=>"" };
        }

        // ---- generated sprites ----
        private static Sprite _round, _bg;
        private static Sprite RoundedTile()
        {
            if(_round!=null) return _round;
            int s=128; float r=26f, half=s*0.5f;
            var tex=new Texture2D(s,s,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[s*s];
            for(int y=0;y<s;y++)for(int x=0;x<s;x++){
                float dx=Mathf.Max(Mathf.Abs(x+0.5f-half)-(half-r),0f), dy=Mathf.Max(Mathf.Abs(y+0.5f-half)-(half-r),0f);
                float a=Mathf.Clamp01((r-Mathf.Sqrt(dx*dx+dy*dy))/1.5f);
                px[y*s+x]=new Color32(255,255,255,(byte)(a*255));
            }
            tex.SetPixels32(px); tex.Apply();
            _round=Sprite.Create(tex,new Rect(0,0,s,s),new Vector2(0.5f,0.5f),s);
            return _round;
        }
        private static Sprite BgSprite()
        {
            if(_bg!=null) return _bg;
            int w=64,h=128; var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var top=new Color(0.10f,0.09f,0.20f); var bottom=new Color(0.24f,0.18f,0.30f);
            var rnd=new System.Random(7); var px=new Color32[w*h];
            for(int y=0;y<h;y++){ float t=(float)y/(h-1); var c=Color.Lerp(bottom,top,t); for(int x=0;x<w;x++) px[y*w+x]=c; }
            tex.SetPixels32(px);
            for(int i=0;i<70;i++){ int x=rnd.Next(w),y=rnd.Next(h/3,h); float b=0.5f+(float)rnd.NextDouble()*0.5f; tex.SetPixel(x,y,new Color(b,b,b*0.9f,1)); }
            tex.Apply();
            _bg=Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(0.5f,0.5f),1);
            return _bg;
        }

        private void EnsureStyles()
        {
            if(_title!=null) return;
            _title=new GUIStyle(GUI.skin.label){fontSize=32,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _hud=new GUIStyle(GUI.skin.label){fontSize=22,alignment=TextAnchor.MiddleCenter};
            _btn=new GUIStyle(GUI.skin.button){fontSize=26,fontStyle=FontStyle.Bold};
            _win=new GUIStyle(GUI.skin.label){fontSize=38,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _title.normal.textColor=_hud.normal.textColor=_win.normal.textColor=Color.white;
        }
    }
}
