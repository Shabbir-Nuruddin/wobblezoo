using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChonkyMerge; // Sfx

namespace SleepyZoo
{
    /// <summary>
    /// Bedtime Shuffle — one elegant rule that makes every level a real puzzle.
    ///
    ///   Swipe any direction and EVERY sleepy animal slides together, each one
    ///   skidding until it hits the board edge, a toy block (wall), or another
    ///   animal. Get every animal onto its own bed to win.
    ///
    /// Because one swipe moves the whole room at once, the player can't just walk
    /// each animal home — placing one shoves the others, so real planning is
    /// required. Every level below is BFS-verified: its par is the true optimal
    /// number of moves, so 3 stars is always achievable and never trivial.
    /// </summary>
    public class PuzzleGame : MonoBehaviour
    {
        // A level entity: where an animal starts (x,y) and which bed it belongs to (bx,by).
        private struct EntDef { public int x, y, bx, by;
            public EntDef(int x, int y, int bx, int by){ this.x=x; this.y=y; this.bx=bx; this.by=by; } }
        private class Lv { public int w, h, par; public string hint; public Vector2Int[] walls; public EntDef[] ents;
            public Lv(int w,int h,int par,string hint,Vector2Int[] walls,EntDef[] e){this.w=w;this.h=h;this.par=par;this.hint=hint;this.walls=walls;this.ents=e;} }

        private static Vector2Int W2(int x,int y)=>new Vector2Int(x,y);

        // Every animal moves by the SAME rule (slide to a stop), so the roster is
        // purely cosmetic variety. Distinct species make each animal easy to track.
        private static readonly string[] Pets =
            { "dog","rabbit","panda","owl","pig","frog","penguin","bear","duck","cow" };

        // ---- BFS-verified level ramp (par = true optimal move count) ----
        private static readonly Lv[] Levels =
        {
            new Lv(4,4,2,"Swipe any direction - every animal slides until it hits something.",
                new[]{ W2(0,3) },
                new[]{ new EntDef(0,2, 3,0) }),
            new Lv(4,4,6,"Walls stop a slide. Use them to park exactly where you need.",
                new[]{ W2(1,2),W2(1,3),W2(2,1) },
                new[]{ new EntDef(0,1, 3,2) }),
            new Lv(4,4,5,"Both animals move on every swipe. Line them up together.",
                new[]{ W2(1,0),W2(3,2) },
                new[]{ new EntDef(3,1, 1,1), new EntDef(0,3, 0,0) }),
            new Lv(5,5,9,"Sometimes you must send one the wrong way to free the other.",
                new[]{ W2(0,1),W2(0,4),W2(1,3) },
                new[]{ new EntDef(1,1, 4,2), new EntDef(0,2, 4,0) }),
            new Lv(5,5,10,"Bump an animal into a wall to hold it while you place the next.",
                new[]{ W2(0,2),W2(0,3),W2(3,1),W2(4,4) },
                new[]{ new EntDef(1,3, 0,0), new EntDef(1,2, 0,1) }),
            new Lv(5,5,9,"Three sleepy friends. Think about the order before you swipe.",
                new[]{ W2(1,0),W2(2,0),W2(2,3) },
                new[]{ new EntDef(3,0, 4,3), new EntDef(0,3, 0,4), new EntDef(0,0, 4,4) }),
            new Lv(5,5,11,"One animal can act as a wall for another. Use each other.",
                new[]{ W2(0,3),W2(2,1),W2(3,0),W2(4,1) },
                new[]{ new EntDef(1,2, 1,3), new EntDef(4,2, 0,0), new EntDef(3,2, 0,4) }),
            new Lv(6,6,12,"More room, more skidding. Plan two moves ahead.",
                new[]{ W2(0,1),W2(3,0),W2(4,0),W2(5,2) },
                new[]{ new EntDef(1,1, 5,0), new EntDef(1,4, 0,0), new EntDef(2,3, 1,0) }),
            new Lv(6,6,15,"Tight corners. The first swipe usually sets up the last.",
                new[]{ W2(0,1),W2(2,0),W2(2,3),W2(3,4),W2(4,1) },
                new[]{ new EntDef(4,4, 2,4), new EntDef(3,2, 5,0), new EntDef(2,2, 5,2) }),
            new Lv(6,6,17,"A full den. Group them, then peel them off one by one.",
                new[]{ W2(2,1),W2(2,2),W2(3,3),W2(4,5) },
                new[]{ new EntDef(5,5, 3,1), new EntDef(1,3, 1,0), new EntDef(3,0, 0,0), new EntDef(3,2, 0,4) }),
            new Lv(6,6,17,"Almost bedtime. Every swipe matters now.",
                new[]{ W2(0,1),W2(2,1),W2(3,0),W2(3,3),W2(5,3) },
                new[]{ new EntDef(3,2, 5,1), new EntDef(3,1, 5,4), new EntDef(4,5, 5,2), new EntDef(3,5, 5,0) }),
            new Lv(6,6,14,"Last one. Tuck the whole zoo in - good night.",
                new[]{ W2(0,2),W2(2,0),W2(3,4),W2(4,1),W2(5,3),W2(5,4) },
                new[]{ new EntDef(2,2, 1,0), new EntDef(0,4, 4,3), new EntDef(4,4, 3,1), new EntDef(1,1, 2,1) }),
        };

        // ---- warm, flat cozy palette (everything sits in the same family) ----
        private static readonly Color NightTop   = new Color(0.15f,0.12f,0.23f);
        private static readonly Color NightBottom = new Color(0.34f,0.24f,0.31f);
        private static readonly Color BoardCream  = new Color(0.99f,0.93f,0.82f);
        private static readonly Color TileCream   = new Color(1.00f,0.965f,0.89f);
        private static readonly Color TileShadow  = new Color(0.90f,0.82f,0.70f);
        private static readonly Color WallWood    = new Color(0.80f,0.62f,0.44f);
        private static readonly Color BedNest     = new Color(0.95f,0.87f,0.73f);
        private static readonly Color BedRing     = new Color(0.86f,0.74f,0.56f);
        private static readonly Color Brown       = new Color(0.36f,0.22f,0.12f);

        // ---- runtime ----
        private int _levelIndex;
        private Lv _lv;
        private Camera _cam;
        private readonly HashSet<Vector2Int> _walls = new();
        private Vector2Int[] _pos, _bed;
        private string[] _pet;
        private Transform[] _view;
        private Vector3[] _target;
        private readonly Stack<Vector2Int[]> _undo = new();
        private Transform _bgTf;

        private int _moves, _stars;
        private float _levelTime;
        private bool _showHint;
        private Vector2 _swipeStart;
        private bool _swiping;
        private bool _solved;

        // UI
        private GUIStyle _title, _sub, _btn, _btnMenu, _win, _hintText, _panelBody, _panelSub;
        private Texture2D _btnTex, _btnTexDown, _panelTex, _starTex, _dimTex;
        private int _btnBorder;
        private Font _font;
        private readonly List<Rect> _uiRects = new();

        public static int LevelCount => Levels.Length;

        private void Start(){ SetupCamera(); LoadLevel(PlayerPrefs.GetInt("zoo_level",0)); }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam==null){ var go=new GameObject("Main Camera"); go.tag="MainCamera"; _cam=go.AddComponent<Camera>(); }
            _cam.orthographic=true; _cam.transform.position=new Vector3(0,0,-10);
            _cam.clearFlags=CameraClearFlags.SolidColor; _cam.backgroundColor=NightBottom;
        }

        private void LoadLevel(int index)
        {
            foreach (Transform c in transform) Destroy(c.gameObject);
            _walls.Clear(); _undo.Clear();
            _moves=0; _solved=false; _stars=0; _levelTime=0f; _showHint=false; _swiping=false;

            _levelIndex=Mathf.Clamp(index,0,Levels.Length-1);
            PlayerPrefs.SetInt("zoo_level",_levelIndex); PlayerPrefs.Save();
            _lv=Levels[_levelIndex];
            foreach (var w in _lv.walls) _walls.Add(w);

            int n=_lv.ents.Length;
            _pos=new Vector2Int[n]; _bed=new Vector2Int[n]; _pet=new string[n];
            _view=new Transform[n]; _target=new Vector3[n];

            SpawnBackground();
            BuildBoard();
            for (int i=0;i<n;i++)
            {
                var e=_lv.ents[i];
                _pos[i]=new Vector2Int(e.x,e.y); _bed[i]=new Vector2Int(e.bx,e.by);
                _pet[i]=Pets[i % Pets.Length];
                SpawnBed(i); SpawnAnimal(i);
            }
            FrameCamera();
        }

        // ---- visuals ----
        private void SpawnBackground()
        {
            var go=new GameObject("BG"); go.transform.SetParent(transform);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=BgGradient(); sr.sortingOrder=-20;
            _bgTf=go.transform;
        }

        private void BuildBoard()
        {
            // soft raised cream board panel behind the grid
            var back=new GameObject("BoardBack"); back.transform.SetParent(transform);
            back.transform.position=new Vector3(0,-0.04f,0.06f);
            var shadow=Tile(new Vector3(0,-0.12f,0.08f),-8,new Color(0,0,0,0.22f),RoundedTile(),1f);
            float pad=0.62f, bw=RoundedTile().bounds.size.x;
            shadow.localScale=new Vector3((_lv.w+pad)/bw,(_lv.h+pad)/bw,1f);
            var bsr=back.AddComponent<SpriteRenderer>(); bsr.sprite=RoundedTile();
            bsr.color=BoardCream; bsr.sortingOrder=-6;
            back.transform.localScale=new Vector3((_lv.w+pad)/bw,(_lv.h+pad)/bw,1f);

            for (int y=0;y<_lv.h;y++)
            for (int x=0;x<_lv.w;x++)
            {
                var cell=new Vector2Int(x,y);
                if (_walls.Contains(cell))
                {
                    Tile(CellToWorld(cell),0,TileShadow,RoundedTile(),0.92f);
                    Tile(CellToWorld(cell)+new Vector3(0,0.04f,-0.1f),1,WallWood,RoundedTile(),0.80f);
                }
                else
                {
                    Tile(CellToWorld(cell),0,TileCream,RoundedTile(),0.92f);
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
            // a cream nest with a soft ring, then a faded "ghost" of the exact animal
            // that belongs here — so the player always knows whose bed is whose.
            Tile(CellToWorld(_bed[i]),1,BedRing,RoundedTile(),0.80f);
            Tile(CellToWorld(_bed[i])+new Vector3(0,0,-0.02f),2,BedNest,RoundedTile(),0.68f);
            var s=Pet(_pet[i]);
            if(s!=null)
            {
                var go=new GameObject("BedGhost"); go.transform.SetParent(transform);
                go.transform.position=CellToWorld(_bed[i])+new Vector3(0,0,-0.05f);
                var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=s; sr.sortingOrder=3;
                sr.color=new Color(0.4f,0.35f,0.3f,0.32f);
                float sc=0.52f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);
            }
        }

        private void SpawnAnimal(int i)
        {
            var s=Pet(_pet[i]);
            var go=new GameObject("Animal"); go.transform.SetParent(transform); go.transform.position=CellToWorld(_pos[i]);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=6;
            if(s!=null){ sr.sprite=s; float sc=0.86f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);}
            else { sr.sprite=RoundedTile(); sr.color=new Color(0.9f,0.7f,0.55f); go.transform.localScale=new Vector3(0.7f,0.7f,1f);}
            _view[i]=go.transform; _target[i]=CellToWorld(_pos[i]);
        }

        private static readonly Dictionary<string,Sprite> _petCache = new();
        private static Sprite Pet(string name)
        {
            if(_petCache.TryGetValue(name,out var s)) return s;
            s=Resources.Load<Sprite>("Art/pets/"+name); _petCache[name]=s; return s;
        }

        private Vector3 CellToWorld(Vector2Int c)=>new Vector3(c.x-(_lv.w-1)*0.5f, c.y-(_lv.h-1)*0.5f, 0);
        private bool WorldToCell(Vector3 w, out Vector2Int cell)
        {
            int x=Mathf.RoundToInt(w.x+(_lv.w-1)*0.5f), y=Mathf.RoundToInt(w.y+(_lv.h-1)*0.5f);
            cell=new Vector2Int(x,y); return x>=0&&x<_lv.w&&y>=0&&y<_lv.h;
        }

        private void FrameCamera()
        {
            float aspect=Mathf.Max(0.3f,(float)Screen.width/Screen.height), margin=1.7f;
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

        // ---- input: swipe ANYWHERE tips the whole room ----
        private void Update()
        {
            FrameCamera();
            for (int i=0;i<_view.Length;i++)
            {
                var s=Pet(_pet[i]); float b=s!=null?0.86f/s.bounds.size.x:0.7f;
                _view[i].position=Vector3.Lerp(_view[i].position,_target[i],Time.deltaTime*16f);
                _view[i].localScale=Vector3.Lerp(_view[i].localScale,new Vector3(b,b,1f),Time.deltaTime*12f);
            }
            if (_solved) return;
            _levelTime+=Time.deltaTime;

            if (Input.GetMouseButtonDown(0))
            {
                _swiping = !PointerOverUI(Input.mousePosition);
                _swipeStart=Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0) && _swiping)
            {
                _swiping=false;
                Vector2 d=(Vector2)Input.mousePosition-_swipeStart;
                if (d.magnitude>28f) DoMove(Dir(d));
            }

            if (Input.GetKeyDown(KeyCode.RightArrow)) DoMove(Vector2Int.right);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) DoMove(Vector2Int.left);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) DoMove(Vector2Int.up);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) DoMove(Vector2Int.down);
        }

        private bool PointerOverUI(Vector2 mouse)
        {
            Vector2 g=new Vector2(mouse.x, Screen.height-mouse.y); // GUI space
            foreach (var r in _uiRects) if (r.Contains(g)) return true;
            return false;
        }

        private static Vector2Int Dir(Vector2 d)=>Mathf.Abs(d.x)>Mathf.Abs(d.y)?(d.x>0?Vector2Int.right:Vector2Int.left):(d.y>0?Vector2Int.up:Vector2Int.down);

        // Slide EVERY animal at once. Process leading-edge-first so trains settle
        // deterministically (exactly like the BFS solver that verified each par).
        private void DoMove(Vector2Int dir)
        {
            if (_solved) return;
            int n=_pos.Length;
            var np=(Vector2Int[])_pos.Clone();
            var order=new int[n]; for(int i=0;i<n;i++) order[i]=i;
            System.Array.Sort(order,(a,b)=>(np[b].x*dir.x+np[b].y*dir.y).CompareTo(np[a].x*dir.x+np[a].y*dir.y));

            var occ=new HashSet<Vector2Int>(np);
            foreach(int i in order)
            {
                occ.Remove(np[i]);
                var p=np[i];
                while(true)
                {
                    var q=p+dir;
                    if(q.x<0||q.x>=_lv.w||q.y<0||q.y>=_lv.h||_walls.Contains(q)||occ.Contains(q)) break;
                    p=q;
                }
                np[i]=p; occ.Add(p);
            }

            bool changed=false; for(int i=0;i<n;i++) if(np[i]!=_pos[i]) changed=true;
            if(!changed) return;

            PushUndo();
            for(int i=0;i<n;i++){ _pos[i]=np[i]; _target[i]=CellToWorld(np[i]); }
            _moves++; Sfx.Click(); CheckWin();
        }

        private void PushUndo(){ var s=new Vector2Int[_pos.Length]; System.Array.Copy(_pos,s,_pos.Length); _undo.Push(s); }
        private void Undo()
        {
            if(_undo.Count==0) return;
            var s=_undo.Pop();
            for(int i=0;i<_pos.Length;i++){ _pos[i]=s[i]; _target[i]=CellToWorld(s[i]); }
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

        private bool HintReady => !_solved && (_moves >= Mathf.Max(_lv.par+3,6) || _levelTime >= 30f);

        // ---- UI ----
        private void OnGUI()
        {
            EnsureStyles();
            _uiRects.Clear();
            var sa=Screen.safeArea;
            float top=Screen.height-(sa.y+sa.height)+18f;
            float bot=sa.y+18f;
            float cx=Screen.width/2f;
            float u=Mathf.Min(Screen.width,Screen.height);

            GUI.Label(new Rect(0,top+2,Screen.width,48), $"Level {_levelIndex+1}", _title);
            GUI.Label(new Rect(0,top+56,Screen.width,30), $"3 stars in {_lv.par} moves   -   {_moves} so far", _sub);

            if(!_solved)
            {
                // Menu — clearly-sized, tappable pill (top-left, out of the board)
                var rMenu=new Rect(sa.x+16, top, 148, 74); _uiRects.Add(rMenu);
                if(CozyButton(rMenu,"Menu",_btnMenu)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }

                // big, obvious Undo | Reset
                float bw=Mathf.Min(300, (Screen.width-64)/2f), bh=120, gap=24;
                float by=Screen.height-bot-bh;
                var rUndo=new Rect(cx-bw-gap/2, by, bw, bh); _uiRects.Add(rUndo);
                var rReset=new Rect(cx+gap/2, by, bw, bh); _uiRects.Add(rReset);
                if(CozyButton(rUndo,"Undo",_btn)) Undo();
                if(CozyButton(rReset,"Reset",_btn)){ Sfx.Click(); LoadLevel(_levelIndex); }

                if(HintReady && !_showHint)
                {
                    float hw=Mathf.Min(280,Screen.width*0.62f), hh=68;
                    var rHint=new Rect(cx-hw/2, by-hh-18, hw, hh); _uiRects.Add(rHint);
                    float pulse=0.82f+0.18f*Mathf.Sin(Time.unscaledTime*4f);
                    var prev=GUI.color; GUI.color=new Color(1f,1f,1f,pulse);
                    if(CozyButton(rHint,"Need a hint?",_btn)){ Sfx.Click(); _showHint=true; }
                    GUI.color=prev;
                }

                if(_levelIndex==0 && _moves==0)
                    GUI.Label(new Rect(0,Screen.height-bot-210,Screen.width,28),"Swipe anywhere to tip the room",_sub);

                if(_showHint) DrawHintCard(cx);
            }
            else DrawWinPanel(cx, u);
        }

        private void DrawHintCard(float cx)
        {
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

            GUI.Label(new Rect(box.x,box.y+h*0.12f,w,52),"All tucked in!",_win);
            // stars sized off the BOX so they always stay inside the cream panel
            DrawStars(cx, box.y+h*0.34f, _stars, Mathf.Min(h*0.20f, w*0.16f));
            GUI.Label(new Rect(box.x,box.y+h*0.56f,w,30), $"{_moves} moves", _panelBody);
            GUI.Label(new Rect(box.x,box.y+h*0.64f,w,26), $"3 stars <= {_lv.par}     2 stars <= {_lv.par+2}", _panelSub);

            bool last=_levelIndex>=Levels.Length-1;
            float bw=Mathf.Min(360,w*0.74f), bh=108;
            var rNext=new Rect(cx-bw/2, box.yMax-bh-22, bw, bh); _uiRects.Add(rNext);
            if(CozyButton(rNext, last?"Play again":"Next level",_btn))
            { Sfx.Click(); LoadLevel(last?0:_levelIndex+1); }
            var rMenu=new Rect(box.x+18, box.y+14, 120, 58); _uiRects.Add(rMenu);
            if(CozyButton(rMenu,"Menu",_btnMenu)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
        }

        private void DrawStars(float cx,float y,int count,float s)
        {
            float gap=s*0.22f, total=3*s+2*gap;
            for(int i=0;i<3;i++)
            {
                bool on=i<count;
                GUI.color = on ? Color.white : new Color(0.30f,0.26f,0.34f,0.55f);
                float lift = on ? -s*0.10f : 0f;
                float sz = on ? s*1.06f : s*0.9f;
                var r=new Rect(cx-total/2+i*(s+gap)+(s-sz)/2, y+lift+(s-sz)/2, sz, sz);
                if(_starTex!=null) GUI.DrawTexture(r,_starTex);
            }
            GUI.color=Color.white;
        }

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

        // Flat warm night gradient with a single soft moon glow — cozy, not busy.
        private static Sprite BgGradient()
        {
            if(_bg!=null) return _bg;
            int w=128,h=256;
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[w*h];
            Vector2 moon=new Vector2(0.74f,0.82f); float mr=0.10f;
            var moonCol=new Color(1f,0.96f,0.84f);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float t=(float)y/(h-1);
                Color c=Color.Lerp(NightBottom,NightTop,t);
                float nx=(float)x/(w-1), ny=(float)y/(h-1);
                float d=Mathf.Sqrt((nx-moon.x)*(nx-moon.x)+(ny-moon.y)*(ny-moon.y));
                float glow=Mathf.Clamp01(1f-d/(mr*3.2f)); glow*=glow;
                float core=d<mr?Mathf.Clamp01(1f-d/mr):0f;
                c=Color.Lerp(c,moonCol,Mathf.Clamp01(glow*0.5f+core*0.85f));
                px[y*w+x]=c;
            }
            tex.SetPixels32(px); tex.Apply();
            _bg=Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(0.5f,0.5f),1);
            return _bg;
        }

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
                float sdf=Mathf.Sqrt(ax*ax+ay*ay)+Mathf.Min(Mathf.Max(qx,qy),0f)-r;
                float alpha=Mathf.Clamp01(-sdf/1.2f+0.5f);
                float t=(float)y/(H-1);
                Color fill=Color.Lerp(bottom,top,t);
                if(t>0.72f) fill=Color.Lerp(fill,Color.white,(t-0.72f)*0.5f);
                if(t<0.14f) fill=Color.Lerp(fill,edge,0.35f);
                float be=Mathf.Clamp01((-sdf)/bt);
                Color c=Color.Lerp(edge,fill,be);
                px[y*W+x]=new Color(c.r,c.g,c.b,alpha);
            }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        private void EnsureStyles()
        {
            if(_title!=null) return;
            _font=Resources.Load<Font>("Fonts/Fredoka");
            _title=new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _sub=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleCenter};
            _win=new GUIStyle(GUI.skin.label){fontSize=40,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            _hintText=new GUIStyle(GUI.skin.label){fontSize=24,alignment=TextAnchor.UpperCenter,wordWrap=true};
            _panelBody=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            _panelSub=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter};
            _title.normal.textColor=Color.white;
            _sub.normal.textColor=new Color(1f,0.95f,0.86f,0.92f);
            _win.normal.textColor=_hintText.normal.textColor=Brown;
            _panelBody.normal.textColor=Brown;
            _panelSub.normal.textColor=new Color(0.45f,0.30f,0.18f);
            if(_font!=null) foreach(var st in new[]{_title,_sub,_win,_hintText,_panelBody,_panelSub}) st.font=_font;

            var pill=Resources.Load<Texture2D>("Art/ui_button");
            var pillDown=Resources.Load<Texture2D>("Art/ui_button_down");
            if(pill!=null){ _btnTex=pill; _btnTexDown=pillDown!=null?pillDown:pill; _btnBorder=0; }
            else { _btnTex=MakeButtonTex(false); _btnTexDown=MakeButtonTex(true); _btnBorder=28; }
            _dimTex=Texture2D.whiteTexture;
            _panelTex=Resources.Load<Texture2D>("Art/ui_panel");
            _starTex=Resources.Load<Texture2D>("Art/star_full");

            _btn=CozyStyle(36);
            _btnMenu=CozyStyle(26);
        }

        private GUIStyle CozyStyle(int fontSize)
        {
            var s=new GUIStyle(GUI.skin.button){
                fontSize=fontSize, fontStyle=FontStyle.Bold, alignment=TextAnchor.MiddleCenter,
                border=new RectOffset(_btnBorder,_btnBorder,_btnBorder,_btnBorder), padding=new RectOffset(12,12,6,10)
            };
            s.normal.textColor=s.hover.textColor=s.active.textColor=Brown;
            s.normal.background=_btnTex; s.hover.background=_btnTex; s.active.background=_btnTexDown;
            if(_font!=null) s.font=_font;
            return s;
        }
    }
}
