using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChonkyMerge; // AnimalSprites, Sfx, SpriteFactory

namespace SleepyZoo
{
    /// <summary>
    /// Animals-as-mechanic puzzle. Swipe an animal; it moves by ITS rule —
    /// cat/fox/deer step, hamster/hedgehog/duck roll, bunny/owl hop, corgi/puppy
    /// push, capybara/panda are pushable sleepy blocks. Get every animal onto its
    /// bed. Turn-based grid, deterministic, full undo, move-par + stars.
    ///
    /// Every level here is BFS-verified solvable and its par = the true optimal
    /// move count, so 3 stars is always achievable.
    /// </summary>
    public class PuzzleGame : MonoBehaviour
    {
        private enum Move { Step, Roll, Hop, Push, Block }
        private struct EntDef { public int tier; public Move move; public int x, y, bx, by;
            public EntDef(int t, Move m, int x, int y, int bx, int by){tier=t;move=m;this.x=x;this.y=y;this.bx=bx;this.by=by;} }
        private class Lv { public int w, h, par; public string hint; public Vector2Int[] walls; public EntDef[] ents;
            public Lv(int w,int h,int par,string hint,Vector2Int[] walls,EntDef[] e){this.w=w;this.h=h;this.par=par;this.hint=hint;this.walls=walls;this.ents=e;} }

        private static Vector2Int W2(int x,int y)=>new Vector2Int(x,y);
        // tiers: 0 hamster,1 bunny,2 cat,3 puppy,4 persian,5 corgi,6 samoyed,7 capybara,
        //        8 fox,9 panda,10 hedgehog,11 owl,12 deer,13 duck
        private static readonly Lv[] Levels =
        {
            new Lv(4,4,6,"Swipe the kitten — it steps one tile per swipe.",
                new[]{ W2(1,0),W2(1,1),W2(1,2) },
                new[]{ new EntDef(2,Move.Step, 0,0, 3,3) }),
            new Lv(5,4,7,"Fox steps one tile at a time. Weave around the wall.",
                new[]{ W2(2,1),W2(2,2),W2(2,3) },
                new[]{ new EntDef(8,Move.Step, 0,0, 4,3) }),
            new Lv(5,5,2,"Hamster rolls until it bumps a wall or edge. Line it up with the bed.",
                new[]{ W2(3,0),W2(2,3) },
                new[]{ new EntDef(0,Move.Roll, 0,0, 2,2) }),
            new Lv(5,5,2,"Roll into a wall to stop exactly where you want.",
                new[]{ W2(4,2) },
                new[]{ new EntDef(10,Move.Roll, 0,0, 4,4) }),
            new Lv(5,5,2,"Bunny hops OVER the next tile and lands two away.",
                new[]{ W2(1,0),W2(2,1) },
                new[]{ new EntDef(1,Move.Hop, 0,0, 2,2) }),
            new Lv(5,5,4,"Owl hops two tiles per swipe — mind the corners.",
                new Vector2Int[0],
                new[]{ new EntDef(11,Move.Hop, 0,0, 4,4) }),
            new Lv(5,5,2,"Corgi pushes! Swipe it into the capybara to nudge it along.",
                new Vector2Int[0],
                new[]{ new EntDef(5,Move.Push, 0,0, 2,0), new EntDef(7,Move.Block, 1,0, 3,0) }),
            new Lv(5,5,5,"Two sleepyheads. Each moves its own way — tuck in both.",
                new Vector2Int[0],
                new[]{ new EntDef(0,Move.Roll, 0,0, 2,0), new EntDef(2,Move.Step, 3,4, 3,0) }),
            new Lv(5,5,6,"Fox steps, owl hops. Give each a clear path to bed.",
                new[]{ W2(2,2) },
                new[]{ new EntDef(8,Move.Step, 0,0, 0,4), new EntDef(11,Move.Hop, 4,0, 4,4) }),
            new Lv(6,5,2,"Roll around the block — end against the far wall.",
                new[]{ W2(5,2) },
                new[]{ new EntDef(13,Move.Roll, 0,0, 5,4) }),
            new Lv(5,5,10,"Steady deer steps; the duck rolls far. Don't block each other.",
                new Vector2Int[0],
                new[]{ new EntDef(12,Move.Step, 0,0, 4,4), new EntDef(13,Move.Roll, 4,0, 0,4) }),
            new Lv(5,5,2,"Push the panda gently into its spot.",
                new Vector2Int[0],
                new[]{ new EntDef(3,Move.Push, 0,2, 2,2), new EntDef(9,Move.Block, 1,2, 3,2) }),
            new Lv(5,5,9,"Three friends, three rules. Solve them one at a time.",
                new[]{ W2(2,2) },
                new[]{ new EntDef(0,Move.Roll, 0,0, 0,4), new EntDef(2,Move.Step, 2,0, 2,4), new EntDef(1,Move.Hop, 4,0, 4,4) }),
            new Lv(6,6,6,"Rollers need a wall to stop. Steppers go anywhere.",
                new[]{ W2(2,2),W2(3,3) },
                new[]{ new EntDef(10,Move.Roll, 0,0, 5,0), new EntDef(8,Move.Step, 0,5, 5,5) }),
            new Lv(5,5,6,"Push the capybara, then send the owl hopping home.",
                new Vector2Int[0],
                new[]{ new EntDef(5,Move.Push, 0,0, 2,0), new EntDef(7,Move.Block, 1,0, 4,0), new EntDef(11,Move.Hop, 0,4, 4,4) }),
            new Lv(6,6,12,"A cozy maze. Patience — one tile at a time.",
                new[]{ W2(1,1),W2(4,1),W2(1,4),W2(4,4) },
                new[]{ new EntDef(2,Move.Step, 0,0, 5,5), new EntDef(0,Move.Roll, 5,0, 0,5) }),
        };

        // Cozy backgrounds, rotated per 3-level pack so it never feels repetitive.
        private static readonly string[] BgPacks =
            { "Art/bg_meadow","Art/bg_treehouse","Art/bg_clouds","Art/bg_library","Art/bg_snowcabin" };

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
        private Sprite _sCell,_sBed,_sBg,_sWall; private Transform _bgTf;

        private int _selected=-1, _swipeEnt=-1, _moves, _stars;
        private float _levelTime;
        private bool _showHint;
        private Vector2 _swipeStart;
        private bool _solved;

        // UI
        private GUIStyle _title, _hud, _sub, _btn, _btnMenu, _win, _hintText, _panelBody, _panelSub;
        private Texture2D _btnTex, _btnTexDown, _panelTex, _starTex, _dimTex;
        private int _btnBorder;   // 9-slice inset; 0 = stretch the real pill art

        private void Start(){ SetupCamera(); LoadLevel(PlayerPrefs.GetInt("zoo_level",0)); }

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
            _selected=-1; _swipeEnt=-1; _moves=0; _solved=false; _stars=0; _levelTime=0f; _showHint=false;

            _levelIndex=Mathf.Clamp(index,0,Levels.Length-1);
            PlayerPrefs.SetInt("zoo_level",_levelIndex); PlayerPrefs.Save();
            _lv=Levels[_levelIndex];
            foreach (var w in _lv.walls) _walls.Add(w);

            _sCell=Resources.Load<Sprite>("Art/tile");
            _sBed=Resources.Load<Sprite>("Art/tile_bed");
            _sWall=Resources.Load<Sprite>("Art/tile_wall");
            _sBg=Resources.Load<Sprite>(BgPacks[(_levelIndex/3)%BgPacks.Length]);

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
            var go=new GameObject("BG"); go.transform.SetParent(transform);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=_sBg!=null?_sBg:BgSprite(); sr.sortingOrder=-20;
            _bgTf=go.transform;
        }

        private void BuildBoard()
        {
            Sprite cellSprite=_sCell!=null?_sCell:RoundedTile();
            for (int y=0;y<_lv.h;y++)
            for (int x=0;x<_lv.w;x++)
            {
                var cell=new Vector2Int(x,y);
                bool wall=_walls.Contains(cell);
                if (wall && _sWall!=null)
                {
                    // cozy cushion under, cute wooden block on top
                    Tile(CellToWorld(cell),0,new Color(0.30f,0.26f,0.40f),cellSprite,0.98f);
                    Tile(CellToWorld(cell)+new Vector3(0,0.04f,-0.1f),4,Color.white,_sWall,0.94f);
                }
                else
                {
                    Color col=_sCell!=null
                        ? (wall?new Color(0.42f,0.38f,0.50f):Color.white)
                        : (wall?new Color(0.14f,0.12f,0.22f):new Color(0.34f,0.31f,0.48f));
                    Tile(CellToWorld(cell),0,col,cellSprite,0.98f);
                }
            }
        }

        private Transform Tile(Vector3 pos,int order,Color col,Sprite sprite,float worldSize)
        {
            var go=new GameObject("Tile"); go.transform.SetParent(transform); go.transform.position=pos;
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.color=col; sr.sortingOrder=order;
            float s=worldSize/sprite.bounds.size.x; go.transform.localScale=new Vector3(s,s,1f);
            return go.transform;
        }

        private void SpawnBed(int i)
        {
            Sprite bedSprite=_sBed!=null?_sBed:RoundedTile();
            Tile(CellToWorld(_bed[i]),1,_sBed!=null?Color.white:new Color(0.46f,0.43f,0.62f),bedSprite,0.92f);
            var s=AnimalSprites.Get(_tier[i]);
            var go=new GameObject("Bed"); go.transform.SetParent(transform); go.transform.position=CellToWorld(_bed[i])+new Vector3(0,0,-0.05f);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=3;
            if(s!=null){ sr.sprite=s; sr.color=new Color(1,1,1,0.34f); float sc=0.52f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);}
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
            float aspect=Mathf.Max(0.3f,(float)Screen.width/Screen.height), margin=1.6f;
            _cam.orthographicSize=Mathf.Max(_lv.h*0.5f+margin,(_lv.w*0.5f+margin)/aspect);

            if(_bgTf!=null)
            {
                var sp=_bgTf.GetComponent<SpriteRenderer>().sprite;
                float camH=_cam.orthographicSize*2f, camW=camH*aspect;
                float cover=Mathf.Max(camW/sp.bounds.size.x, camH/sp.bounds.size.y)*1.02f;
                var cp=_cam.transform.position;
                _bgTf.position=new Vector3(cp.x,cp.y,2f);
                _bgTf.localScale=new Vector3(cover,cover,1f);
            }
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
            _levelTime+=Time.deltaTime;

            if (Input.GetMouseButtonDown(0))
            {
                if (PointerOverUI(Input.mousePosition)) return;
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

        // Ignore board taps that land on the on-screen buttons.
        private readonly List<Rect> _uiRects = new();
        private bool PointerOverUI(Vector2 mouse)
        {
            Vector2 g=new Vector2(mouse.x, Screen.height-mouse.y); // GUI space
            foreach (var r in _uiRects) if (r.Contains(g)) return true;
            return false;
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
            int key=PlayerPrefs.GetInt("zoo_stars_"+_levelIndex,0);
            if(_stars>key) PlayerPrefs.SetInt("zoo_stars_"+_levelIndex,_stars);
            PlayerPrefs.Save();
            Sfx.Pop();
        }

        private bool HintReady => !_solved && (_moves >= Mathf.Max(_lv.par+3,5) || _levelTime >= 30f);

        // ---- UI ----
        private void OnGUI()
        {
            EnsureStyles();
            _uiRects.Clear();
            var sa=Screen.safeArea;
            float top=Screen.height-(sa.y+sa.height)+18f;     // top inset
            float bot=sa.y+18f;                               // bottom inset
            float cx=Screen.width/2f;
            float u=Mathf.Min(Screen.width,Screen.height);    // scale reference

            // ---- top HUD ----
            GUI.Label(new Rect(0,top+2,Screen.width,48), $"Level {_levelIndex+1}", _title);
            GUI.Label(new Rect(0,top+56,Screen.width,30), $"3★ in {_lv.par} moves   ·   {_moves} so far", _sub);

            if(!_solved)
            {
                // Menu (top-left)
                var rMenu=new Rect(sa.x+14, top, 132, 64); _uiRects.Add(rMenu);
                if(CozyButton(rMenu,"Menu",_btnMenu)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }

                // bottom row: Undo | Reset (big, lively)
                float bw=Mathf.Min(210, (Screen.width-60)/2f), bh=86, gap=20;
                float by=Screen.height-bot-bh;
                var rUndo=new Rect(cx-bw-gap/2, by, bw, bh); _uiRects.Add(rUndo);
                var rReset=new Rect(cx+gap/2, by, bw, bh); _uiRects.Add(rReset);
                if(CozyButton(rUndo,"Undo",_btn)) Undo();
                if(CozyButton(rReset,"Reset",_btn)){ Sfx.Click(); LoadLevel(_levelIndex); }

                // Hint button — only after the player has clearly been trying a while.
                if(HintReady && !_showHint)
                {
                    float hw=Mathf.Min(260,Screen.width*0.6f), hh=64;
                    var rHint=new Rect(cx-hw/2, by-hh-18, hw, hh); _uiRects.Add(rHint);
                    float pulse=0.82f+0.18f*Mathf.Sin(Time.unscaledTime*4f);
                    var prev=GUI.color; GUI.color=new Color(1f,1f,1f,pulse);
                    if(CozyButton(rHint,"Need a hint?",_btn)){ Sfx.Click(); _showHint=true; }
                    GUI.color=prev;
                }

                // onboarding, level 1 only, before the first move (not a spoiler — L1 is trivial)
                if(_levelIndex==0 && _moves==0)
                    GUI.Label(new Rect(0,Screen.height-bot-210,Screen.width,28),"Swipe an animal to move it",_sub);

                if(_showHint) DrawHintCard(cx, u);
            }
            else DrawWinPanel(cx, u);
        }

        private void DrawHintCard(float cx, float u)
        {
            // dim, then a cozy panel with the level's nudge; tap anywhere to close.
            GUI.color=new Color(0,0,0,0.5f); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),_dimTex); GUI.color=Color.white;
            float w=Mathf.Min(Screen.width*0.84f,560), h=w*0.5f;
            var box=new Rect(cx-w/2,(Screen.height-h)/2,w,h);
            if(_panelTex!=null) GUI.DrawTexture(box,_panelTex);
            GUI.Label(new Rect(box.x+30,box.y+h*0.16f,w-60,40),"Hint",_win);
            GUI.Label(new Rect(box.x+34,box.y+h*0.34f,w-68,h*0.4f),_lv.hint,_hintText);
            var rOk=new Rect(cx-90,box.yMax-96,180,72); _uiRects.Add(rOk);
            if(CozyButton(rOk,"Got it",_btn)){ Sfx.Click(); _showHint=false; }
        }

        private void DrawWinPanel(float cx, float u)
        {
            GUI.color=new Color(0,0,0,0.62f); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),_dimTex); GUI.color=Color.white;
            float w=Mathf.Min(Screen.width*0.9f,660), h=w*(512f/768f);
            var box=new Rect(cx-w/2,(Screen.height-h)/2,w,h);
            if(_panelTex!=null) GUI.DrawTexture(box,_panelTex);

            GUI.Label(new Rect(box.x,box.y+h*0.11f,w,52),"All tucked in!",_win);
            DrawStars(cx, box.y+h*0.30f, _stars, u*0.15f);
            GUI.Label(new Rect(box.x,box.y+h*0.52f,w,30), $"{_moves} moves", _panelBody);
            GUI.Label(new Rect(box.x,box.y+h*0.60f,w,26), $"3★ ≤ {_lv.par}      2★ ≤ {_lv.par+2}", _panelSub);

            bool last=_levelIndex>=Levels.Length-1;
            float bw=Mathf.Min(300,w*0.62f), bh=84;
            var rNext=new Rect(cx-bw/2, box.yMax-bh-24, bw, bh); _uiRects.Add(rNext);
            if(CozyButton(rNext, last?"Play again":"Next level",_btn))
            { Sfx.Click(); LoadLevel(last?0:_levelIndex+1); }
            var rMenu=new Rect(box.x+18, box.y+14, 116, 56); _uiRects.Add(rMenu);
            if(CozyButton(rMenu,"Menu",_btnMenu)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
        }

        private void DrawStars(float cx,float y,int count,float s)
        {
            float gap=s*0.22f, total=3*s+2*gap;
            for(int i=0;i<3;i++)
            {
                bool on=i<count;
                GUI.color = on ? Color.white : new Color(0.30f,0.26f,0.34f,0.55f);
                float lift = on ? -s*0.10f : 0f;                 // earned stars sit a touch higher
                float sz = on ? s*1.06f : s*0.9f;
                var r=new Rect(cx-total/2+i*(s+gap)+(s-sz)/2, y+lift+(s-sz)/2, sz, sz);
                if(_starTex!=null) GUI.DrawTexture(r,_starTex);
            }
            GUI.color=Color.white;
        }

        // ---- lively cozy button (matches the warm, chunky landing-page style) ----
        private bool CozyButton(Rect r, string label, GUIStyle style)
        {
            bool down = r.Contains(Event.current.mousePosition) &&
                        (Event.current.type==EventType.MouseDown || Input.GetMouseButton(0)) &&
                        Event.current.type!=EventType.MouseUp;
            style.normal.background = style.hover.background = down?_btnTexDown:_btnTex;
            style.active.background = _btnTexDown;
            return GUI.Button(r, label, style);
        }

        // ---- generated sprites / textures ----
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

        // Warm rounded button texture (9-sliced via GUIStyle.border) with a soft bevel.
        private static Texture2D MakeButtonTex(bool pressed)
        {
            int W=120,H=88; float r=28f, bt=4f;
            var top   = pressed?new Color(0.94f,0.70f,0.44f):new Color(1.00f,0.87f,0.63f);
            var bottom= pressed?new Color(0.90f,0.60f,0.36f):new Color(0.98f,0.73f,0.46f);
            var edge  = new Color(0.78f,0.48f,0.30f);
            var tex=new Texture2D(W,H,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[W*H];
            float hx=W*0.5f, hy=H*0.5f;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float qx=Mathf.Abs(x+0.5f-hx)-(hx-r), qy=Mathf.Abs(y+0.5f-hy)-(hy-r);
                float ax=Mathf.Max(qx,0f), ay=Mathf.Max(qy,0f);
                float sdf=Mathf.Sqrt(ax*ax+ay*ay)+Mathf.Min(Mathf.Max(qx,qy),0f)-r; // <0 inside
                float alpha=Mathf.Clamp01(-sdf/1.2f+0.5f);
                float t=(float)y/(H-1);
                Color fill=Color.Lerp(bottom,top,t);
                if(t>0.72f) fill=Color.Lerp(fill,Color.white,(t-0.72f)*0.5f);   // top sheen
                if(t<0.14f) fill=Color.Lerp(fill,edge,0.35f);                    // bottom bevel
                float be=Mathf.Clamp01((-sdf)/bt);                               // border blend
                Color c=Color.Lerp(edge,fill,be);
                px[y*W+x]=new Color(c.r,c.g,c.b,alpha);
            }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        private Font _font;
        private void EnsureStyles()
        {
            if(_title!=null) return;
            _font=Resources.Load<Font>("Fonts/Fredoka");   // cozy rounded font (falls back to default if missing)
            var brown=new Color(0.38f,0.23f,0.12f);
            _title=new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _hud=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            _sub=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleCenter};
            _win=new GUIStyle(GUI.skin.label){fontSize=40,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _hintText=new GUIStyle(GUI.skin.label){fontSize=24,alignment=TextAnchor.UpperCenter,wordWrap=true};
            _panelBody=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            _panelSub=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter};
            _title.normal.textColor=_hud.normal.textColor=Color.white;
            _sub.normal.textColor=new Color(1f,0.95f,0.86f,0.92f);
            _win.normal.textColor=_hintText.normal.textColor=brown;
            _panelBody.normal.textColor=brown;
            _panelSub.normal.textColor=new Color(0.45f,0.30f,0.18f);
            if(_font!=null) foreach(var st in new[]{_title,_hud,_sub,_win,_hintText,_panelBody,_panelSub}) st.font=_font;

            // Prefer the real cozy pill art; fall back to the procedural button.
            var pill=Resources.Load<Texture2D>("Art/ui_button");
            var pillDown=Resources.Load<Texture2D>("Art/ui_button_down");
            if(pill!=null){ _btnTex=pill; _btnTexDown=pillDown!=null?pillDown:pill; _btnBorder=0; }
            else { _btnTex=MakeButtonTex(false); _btnTexDown=MakeButtonTex(true); _btnBorder=28; }
            _dimTex=Texture2D.whiteTexture;
            _panelTex=Resources.Load<Texture2D>("Art/ui_panel");
            _starTex=Resources.Load<Texture2D>("Art/star_full");

            _btn=CozyStyle(30);
            _btnMenu=CozyStyle(23);
        }

        private GUIStyle CozyStyle(int fontSize)
        {
            var s=new GUIStyle(GUI.skin.button){
                fontSize=fontSize, fontStyle=FontStyle.Bold, alignment=TextAnchor.MiddleCenter,
                border=new RectOffset(_btnBorder,_btnBorder,_btnBorder,_btnBorder), padding=new RectOffset(12,12,6,10)
            };
            var brown=new Color(0.36f,0.21f,0.10f);
            s.normal.textColor=s.hover.textColor=s.active.textColor=brown;
            s.normal.background=_btnTex; s.hover.background=_btnTex; s.active.background=_btnTexDown;
            if(_font!=null) s.font=_font;
            return s;
        }
    }
}
