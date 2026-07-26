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

        // Each species gets its OWN signature colour. The animal wears a soft glow of
        // it and its bed's blanket is the same colour, so "whose bed is whose" reads at
        // a glance — even when two faces are close (the grey dog vs the grey rabbit).
        private static readonly Color[] PetColors =
        {
            new Color(0.42f,0.62f,0.96f), // dog     - blue
            new Color(0.98f,0.55f,0.68f), // rabbit  - rose
            new Color(0.36f,0.80f,0.68f), // panda   - teal
            new Color(0.99f,0.72f,0.36f), // owl     - amber
            new Color(0.86f,0.52f,0.93f), // pig     - orchid
            new Color(0.62f,0.84f,0.40f), // frog    - lime
            new Color(0.42f,0.80f,0.94f), // penguin - sky
            new Color(0.93f,0.60f,0.42f), // bear    - coral
            new Color(0.98f,0.84f,0.36f), // duck    - gold
            new Color(0.70f,0.62f,0.95f), // cow     - lavender
        };
        private Color PetCol(int i)=>PetColors[i % PetColors.Length];

        // ---- BFS-verified level ramp (par = true optimal move count) ----
        private static readonly Lv[] Levels =
        {
            new Lv(4,4,2,"Swipe and your animal slides all the way to the wall.",
                new Vector2Int[0],
                new[]{ new EntDef(2,1, 3,3) }),
            new Lv(4,4,3,"A toy block stops the slide. Use it to park where you want.",
                new[]{ W2(0,3) },
                new[]{ new EntDef(0,1, 1,3) }),
            new Lv(4,4,4,"Two blocks make a pocket. Slide in from the right side.",
                new[]{ W2(1,0),W2(2,2) },
                new[]{ new EntDef(3,3, 3,1) }),
            new Lv(4,4,5,"Two friends move on every swipe. Solve them together.",
                new[]{ W2(2,1) },
                new[]{ new EntDef(3,2, 0,2), new EntDef(0,0, 0,3) }),
            new Lv(4,4,6,"Line both up, then send them home in one direction.",
                new[]{ W2(0,3),W2(1,3) },
                new[]{ new EntDef(2,0, 3,2), new EntDef(0,0, 3,1) }),
            new Lv(5,5,5,"More room now. A wrong-way swipe often sets up the right one.",
                new[]{ W2(1,1),W2(1,4) },
                new[]{ new EntDef(4,1, 3,0), new EntDef(0,3, 2,0) }),
            new Lv(5,5,7,"Bump one into a block to hold it while you place the other.",
                new[]{ W2(2,3),W2(3,0),W2(4,1) },
                new[]{ new EntDef(2,4, 4,3), new EntDef(0,2, 4,4) }),
            new Lv(5,5,9,"Think one move ahead before you swipe.",
                new[]{ W2(1,1),W2(3,3),W2(4,3) },
                new[]{ new EntDef(2,4, 2,0), new EntDef(2,3, 4,4) }),
            new Lv(5,5,8,"Three friends. Handle the trickiest one first.",
                new[]{ W2(2,0),W2(4,1) },
                new[]{ new EntDef(3,1, 3,4), new EntDef(2,1, 1,4), new EntDef(3,3, 4,0) }),
            new Lv(5,5,10,"Use one animal as a wall for another.",
                new[]{ W2(1,2),W2(2,2),W2(4,3) },
                new[]{ new EntDef(0,2, 4,4), new EntDef(2,0, 3,3), new EntDef(3,4, 4,2) }),
            new Lv(6,6,10,"Big board, long slides. Group them, then split them off.",
                new[]{ W2(3,0),W2(3,2),W2(5,3) },
                new[]{ new EntDef(0,4, 4,5), new EntDef(0,3, 5,5), new EntDef(5,0, 5,4) }),
            new Lv(6,6,11,"Corners are your friends. Trap an animal in one.",
                new[]{ W2(1,3),W2(2,0),W2(3,2),W2(4,4) },
                new[]{ new EntDef(3,1, 5,0), new EntDef(0,2, 5,1), new EntDef(3,0, 4,1) }),
            new Lv(6,6,14,"Plan the last move first, then work backwards.",
                new[]{ W2(1,4),W2(3,2),W2(4,1),W2(4,3) },
                new[]{ new EntDef(5,5, 0,0), new EntDef(3,0, 1,5), new EntDef(2,5, 0,5) }),
            new Lv(6,6,14,"A full den. Peel them off one at a time.",
                new[]{ W2(1,4),W2(2,0),W2(4,5) },
                new[]{ new EntDef(1,3, 4,0), new EntDef(2,2, 3,0), new EntDef(0,0, 0,1), new EntDef(0,4, 1,1) }),
            new Lv(6,6,14,"Almost there. Every swipe counts now.",
                new[]{ W2(1,0),W2(2,3),W2(4,0),W2(4,3) },
                new[]{ new EntDef(4,4, 5,4), new EntDef(1,4, 5,2), new EntDef(3,4, 5,0), new EntDef(4,1, 5,1) }),
            new Lv(6,6,17,"Last one - tuck the whole zoo in. Good night.",
                new[]{ W2(1,4),W2(2,1),W2(3,1),W2(4,4),W2(5,3) },
                new[]{ new EntDef(4,2, 5,5), new EntDef(3,2, 4,0), new EntDef(2,2, 5,0), new EntDef(0,2, 5,4) }),
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
        private List<Vector2Int> _hintPath;   // optimal remaining swipes, computed when a hint is opened
        private Vector2 _swipeStart;
        private bool _swiping;
        private bool _solved;

        // on-board guidance arrow (used for both the tutorial nudge and hints)
        private Transform _arrowTf;
        private SpriteRenderer _arrowSr;
        private bool _arrowOn;
        private Vector2Int _arrowDir;
        private Color _arrowTint;
        private bool _isTutorial;             // level 0 shows a guided first-swipe demo
        private Vector2Int _tutorialDir;      // the helpful first swipe to demonstrate
        private float _tipTime;               // brief per-level teaching tip fades out

        // UI
        private GUIStyle _title, _sub, _btn, _btnMenu, _win, _hintText, _panelBody, _panelSub;
        private Texture2D _btnTex, _btnTexDown, _panelTex, _starTex, _dimTex;
        private int _btnBorder;
        private Font _font;
        private readonly List<Rect> _uiRects = new();

        public static int LevelCount => Levels.Length;
        public static int MaxStars => Levels.Length * 3;

        // Where "Play" should drop the player: the furthest level they've reached that
        // is actually unlocked, so they continue instead of replaying the tutorial.
        public static int ResumeLevel()
        {
            int want=PlayerPrefs.GetInt("zoo_furthest",0);
            want=Mathf.Clamp(want,0,Levels.Length-1);
            while(want>0 && !IsUnlocked(want)) want--;   // gated? fall back to the last open one
            return want;
        }

        // ---- star economy (Where's-My-Water style gentle checkpoints) ----
        // Levels open in order once you've cleared the one before, but a few
        // checkpoints also need a running star total — so a lazy 1-star run hits a
        // wall and has to replay a couple of levels for more stars.
        public static int StarsFor(int i) => PlayerPrefs.GetInt("zoo_stars_" + i, 0);
        public static int TotalStars()
        {
            int t = 0; for (int i = 0; i < Levels.Length; i++) t += StarsFor(i); return t;
        }
        // Total stars required to step past each checkpoint.
        public static int RequiredStars(int i)
        {
            if (i >= 12) return 24;   // gate before level 13
            if (i >= 8)  return 14;   // gate before level 9
            if (i >= 4)  return 6;    // gate before level 5
            return 0;
        }
        public static bool IsUnlocked(int i)
        {
            if (i <= 0) return true;                       // tutorial always open
            if (StarsFor(i - 1) <= 0) return false;        // must clear the previous level
            return TotalStars() >= RequiredStars(i);       // …and clear the checkpoint
        }

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
            _arrowTf=null; _arrowSr=null; _arrowOn=false; _hintPath=null;

            _levelIndex=Mathf.Clamp(index,0,Levels.Length-1);
            // The full "how to play" walkthrough shows on level 1 only until it's been
            // completed once; after that level 1 plays like any other level.
            _isTutorial=(_levelIndex==0 && PlayerPrefs.GetInt("zoo_tutorial_done",0)==0);
            _tipTime=0f;
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
            SpawnArrow();
            if(_isTutorial)
            {
                var demo=SolveFrom(_pos);
                _tutorialDir=(demo!=null&&demo.Count>0)?demo[0]:Vector2Int.right;
            }
            FrameCamera();
        }

        // A single big translucent arrow, reused for the tutorial demo and hints.
        private void SpawnArrow()
        {
            var go=new GameObject("GuideArrow"); go.transform.SetParent(transform);
            go.transform.position=new Vector3(0,0,-0.4f);
            _arrowSr=go.AddComponent<SpriteRenderer>();
            _arrowSr.sprite=ArrowSprite(); _arrowSr.sortingOrder=30;
            _arrowSr.enabled=false;
            _arrowTf=go.transform;
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
            // Each bed is a soft colour-matched blanket (same signature colour the
            // animal wears), a cream pillow, then a faded "ghost" of the exact animal
            // that sleeps here — so whose bed is whose reads instantly, by colour.
            Color col=PetCol(i);
            var pos=CellToWorld(_bed[i]);
            // A cozy nook: a soft round colour-glow (no hard edges), a cream pillow on
            // top, then a faded ghost of the animal that sleeps here. The soft disc reads
            // as a warm glow rather than a UI box, while still coding the bed by colour.
            var halo=Tile(pos+new Vector3(0,0,0.03f),1,col,SoftDisc(),1.06f);
            halo.GetComponent<SpriteRenderer>().color=new Color(col.r,col.g,col.b,0.9f);
            var inner=Tile(pos+new Vector3(0,0,0.02f),2,col,SoftDisc(),0.72f);
            inner.GetComponent<SpriteRenderer>().color=new Color(col.r,col.g,col.b,0.7f);
            // soft cream pillow in the middle (small, so a clear colour rim shows around it)
            Tile(pos+new Vector3(0,0.01f,-0.02f),3,BedNest,RoundedTile(),0.44f);
            var s=Pet(_pet[i]);
            if(s!=null)
            {
                var go=new GameObject("BedGhost"); go.transform.SetParent(transform);
                go.transform.position=pos+new Vector3(0,0,-0.05f);
                var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=s; sr.sortingOrder=4;
                // tint the sleeping ghost toward the bed colour so the match is obvious
                sr.color=new Color(0.5f+col.r*0.18f,0.47f+col.g*0.18f,0.45f+col.b*0.18f,0.5f);
                float sc=0.44f/s.bounds.size.x; go.transform.localScale=new Vector3(sc,sc,1f);
            }
        }

        private void SpawnAnimal(int i)
        {
            var s=Pet(_pet[i]);
            var go=new GameObject("Animal"); go.transform.SetParent(transform); go.transform.position=CellToWorld(_pos[i]);

            var sr=go.AddComponent<SpriteRenderer>(); sr.sortingOrder=6;
            float parentScale;
            if(s!=null){ sr.sprite=s; parentScale=0.86f/s.bounds.size.x; }
            else { sr.sprite=RoundedTile(); sr.color=new Color(0.9f,0.7f,0.55f); parentScale=0.7f; }
            go.transform.localScale=new Vector3(parentScale,parentScale,1f);

            // soft signature-colour glow that rides along under the animal. It's a child,
            // so its local scale is divided back out of the parent's scale to land at ~1 cell.
            Color col=PetCol(i);
            var glow=new GameObject("Glow"); glow.transform.SetParent(go.transform);
            glow.transform.localPosition=new Vector3(0,0,0.2f);
            var gsr=glow.AddComponent<SpriteRenderer>(); gsr.sprite=SoftDisc(); gsr.sortingOrder=5;
            gsr.color=new Color(col.r,col.g,col.b,0.75f);
            float gs=1.34f/(parentScale*SoftDisc().bounds.size.x);   // bigger than the animal so a coloured aura shows
            glow.transform.localScale=new Vector3(gs,gs,1f);

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
            if (_solved){ DriveArrow(); return; }
            _levelTime+=Time.deltaTime;
            _tipTime+=Time.deltaTime;
            DriveArrow();

            // Only read input while the app is actually in front — stops stray taps/swipes
            // from registering when the window is in the background.
            if (!Application.isFocused){ _swiping=false; return; }

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

        // Shows/animates the single on-board guide arrow: a warm gold demo on the
        // tutorial's first swipe, a minty "next move" arrow when a hint is open.
        private void DriveArrow()
        {
            if(_arrowTf==null||_arrowSr==null) return;
            bool show=false; Vector2Int dir=Vector2Int.right; Color tint=Color.white;
            if(!_solved && _isTutorial && _moves==0)
            { show=true; dir=_tutorialDir; tint=new Color(1f,0.85f,0.42f); }
            else if(!_solved && _showHint && _hintPath!=null && _hintPath.Count>0)
            { show=true; dir=_hintPath[0]; tint=new Color(0.52f,0.90f,0.70f); }

            _arrowSr.enabled=show; _arrowOn=show;
            if(!show) return;

            _arrowTf.rotation=Quaternion.Euler(0,0,DirAngle(dir));
            float pulse=0.5f+0.5f*Mathf.Sin(Time.unscaledTime*3.2f);
            Vector3 off=new Vector3(dir.x,dir.y,0f)*(0.14f+0.14f*pulse);
            _arrowTf.position=new Vector3(off.x,off.y,-0.4f);
            float sc=(1.7f+0.18f*pulse)/ArrowSprite().bounds.size.x;
            _arrowTf.localScale=new Vector3(sc,sc,1f);
            _arrowSr.color=new Color(tint.r,tint.g,tint.b,0.5f+0.4f*pulse);
        }

        // Slide EVERY animal at once. Process leading-edge-first so trains settle
        // deterministically. The hint solver reuses the exact same function, so the
        // moves it suggests are guaranteed to behave identically in play.
        private Vector2Int[] SlideSim(Vector2Int[] pos, Vector2Int dir)
        {
            int n=pos.Length;
            var np=(Vector2Int[])pos.Clone();
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
            return np;
        }

        private void DoMove(Vector2Int dir)
        {
            if (_solved) return;
            var np=SlideSim(_pos,dir);
            bool changed=false; for(int i=0;i<np.Length;i++) if(np[i]!=_pos[i]) changed=true;
            if(!changed) return;

            PushUndo();
            for(int i=0;i<np.Length;i++){ _pos[i]=np[i]; _target[i]=CellToWorld(np[i]); }
            _hintPath=null; _showHint=false; _arrowOn=false;  // any move clears shown guidance
            _tipTime=999f;             // hide the teaching tip once they act
            _moves++; Sfx.Click(); CheckWin();
        }

        // ---- runtime hint solver: optimal remaining swipes from any position ----
        private static readonly Vector2Int[] AllDirs =
            { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        private long StateKey(Vector2Int[] s)
        {
            long k=0; for(int i=0;i<s.Length;i++) k=k*64L+(s[i].x*_lv.h+s[i].y); return k;
        }
        private bool IsGoal(Vector2Int[] s)
        {
            for(int i=0;i<s.Length;i++) if(s[i]!=_bed[i]) return false; return true;
        }

        // Breadth-first search back to the beds; returns the shortest swipe sequence.
        private List<Vector2Int> SolveFrom(Vector2Int[] start)
        {
            if(IsGoal(start)) return new List<Vector2Int>();
            var came=new Dictionary<long,(long prev,int dir)>();
            var q=new Queue<Vector2Int[]>();
            long sk=StateKey(start); came[sk]=(-1,-1); q.Enqueue(start);
            long goal=-1;
            while(q.Count>0)
            {
                var cur=q.Dequeue(); long ck=StateKey(cur);
                for(int di=0; di<4; di++)
                {
                    var ns=SlideSim(cur,AllDirs[di]); long nk=StateKey(ns);
                    if(came.ContainsKey(nk)) continue;
                    came[nk]=(ck,di);
                    if(IsGoal(ns)){ goal=nk; q.Clear(); break; }
                    q.Enqueue(ns);
                }
                if(goal>=0) break;
                if(came.Count>300000) return null;
            }
            if(goal<0) return null;
            var path=new List<Vector2Int>();
            long k=goal;
            while(came[k].prev!=-1){ path.Add(AllDirs[came[k].dir]); k=came[k].prev; }
            path.Reverse(); return path;
        }

        private static string DirWord(Vector2Int d)=>
            d==Vector2Int.right?"Right":d==Vector2Int.left?"Left":d==Vector2Int.up?"Up":"Down";

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
            if(_levelIndex==0) PlayerPrefs.SetInt("zoo_tutorial_done",1);  // never re-teach
            // remember the furthest level reached so Play resumes there
            int furthest=PlayerPrefs.GetInt("zoo_furthest",0);
            int nextLv=Mathf.Min(_levelIndex+1,Levels.Length-1);
            if(nextLv>furthest) PlayerPrefs.SetInt("zoo_furthest",nextLv);
            PlayerPrefs.Save();
            Sfx.Pop();
        }

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

            // Header text sits BELOW the Menu button's row, centred full-width, so the
            // Menu pill can never overlap the title (even the long "Welcome!").
            float hy = top + 64f;
            GUI.Label(new Rect(0,hy,Screen.width,46), _isTutorial?"Welcome!":$"Level {_levelIndex+1}", _title);

            if(!_solved)
            {
                // Menu — clearly-sized, tappable pill (top-left corner, above the title)
                var rMenu=new Rect(sa.x+16, top, 132, 60); _uiRects.Add(rMenu);
                if(CozyButton(rMenu,"Menu",_btnMenu)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }

                // teaching text: a guided tutorial on level 1, a gentle one-line tip
                // on the next few levels — both fade the moment the player acts.
                if(_isTutorial && _moves==0)
                {
                    GUI.Label(new Rect(16,hy+48,Screen.width-32,28),"Swipe any way — everyone slides at once.",_sub);
                    GUI.Label(new Rect(16,hy+78,Screen.width-32,28),"Follow the arrow to the glowing bed.",_sub);
                }
                else if(!_isTutorial)
                {
                    GUI.Label(new Rect(0,hy+48,Screen.width,28), $"3 stars in {_lv.par} moves   -   {_moves} so far", _sub);
                    // early-level teaching tip, on its own soft strip so it stays legible
                    if(_levelIndex>=1 && _levelIndex<=4 && _moves==0 && _tipTime<6f)
                    {
                        float tw=Screen.width-56f;
                        float th=_hintText.CalcHeight(new GUIContent(_lv.hint),tw)+16f;
                        var tip=new Rect(28f,hy+80,tw,th);
                        var pc=GUI.color;
                        GUI.color=new Color(0.10f,0.07f,0.16f,0.55f);
                        GUI.DrawTexture(tip,_dimTex);
                        GUI.color=pc;
                        GUI.Label(new Rect(tip.x+8,tip.y+8,tip.width-16,tip.height-12),_lv.hint,_hintText);
                    }
                }

                // Undo | Reset — comfortably tappable but no longer dominating the screen
                float bw=Mathf.Min(210, (Screen.width-80)/2f), bh=84, gap=20;
                float by=Screen.height-bot-bh;
                var rUndo=new Rect(cx-bw-gap/2, by, bw, bh); _uiRects.Add(rUndo);
                var rReset=new Rect(cx+gap/2, by, bw, bh); _uiRects.Add(rReset);
                if(CozyButton(rUndo,"Undo",_btn)) Undo();
                if(CozyButton(rReset,"Reset",_btn)){ Sfx.Click(); LoadLevel(_levelIndex); }

                // Hint is always here to help; it pulses and speaks up after a struggle.
                // Tapping it drops a glowing arrow on the board showing the very next
                // swipe — real help, one step at a time.
                bool struggling = _moves>=Mathf.Max(_lv.par+2,5) || _levelTime>=25f;
                float hw=Mathf.Min(260,Screen.width*0.60f), hh=70;
                var rHint=new Rect(cx-hw/2, by-hh-16, hw, hh); _uiRects.Add(rHint);
                string hlabel=_showHint?"Hide hint":(struggling?"Need a hint?":"Hint");
                var prev=GUI.color;
                if(struggling && !_showHint) GUI.color=new Color(1f,1f,1f,0.82f+0.18f*Mathf.Sin(Time.unscaledTime*4f));
                if(CozyButton(rHint,hlabel,_btn))
                {
                    Sfx.Click();
                    if(_showHint) _showHint=false;
                    else { _hintPath=SolveFrom(_pos); _showHint=true; }
                }
                GUI.color=prev;

                // Caption for the on-board arrow. It sits on its own dark rounded strip so
                // it always reads clearly instead of disappearing into the board behind it.
                if(_showHint)
                {
                    string cap;
                    if(_hintPath==null) cap="This one's tangled - tap Reset to start fresh.";
                    else if(_hintPath.Count==0) cap="You're there - one more nudge!";
                    else cap=$"Swipe {DirWord(_hintPath[0])}   -   {_hintPath.Count} move"+(_hintPath.Count==1?"":"s")+" to go";

                    var size=_hintText.CalcSize(new GUIContent(cap));
                    float pw=Mathf.Min(Screen.width-40f, size.x+44f), ph=44f;
                    var strip=new Rect(cx-pw/2f, by-hh-16-ph-12f, pw, ph);
                    var prevC=GUI.color;
                    GUI.color=new Color(0.10f,0.07f,0.16f,0.80f);
                    GUI.DrawTexture(strip,_dimTex);
                    GUI.color=prevC;
                    var lab=_hintText.alignment; _hintText.alignment=TextAnchor.MiddleCenter;
                    GUI.Label(strip,cap,_hintText);
                    _hintText.alignment=lab;
                }
            }
            else DrawWinPanel(cx, u);
        }

        private void DrawWinPanel(float cx, float u)
        {
            GUI.color=new Color(0,0,0,0.62f); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),_dimTex); GUI.color=Color.white;
            float w=Mathf.Min(Screen.width*0.9f,660), h=w*(512f/768f);
            var box=new Rect(cx-w/2,(Screen.height-h)/2,w,h);
            if(_panelTex!=null) GUI.DrawTexture(box,_panelTex);

            // Title owns the full top of the panel now — nothing overlaps it.
            GUI.Label(new Rect(box.x,box.y+h*0.11f,w,52),"All tucked in!",_win);
            // stars sized off the BOX so they always stay inside the cream panel
            DrawStars(cx, box.y+h*0.31f, _stars, Mathf.Min(h*0.16f, w*0.13f));
            GUI.Label(new Rect(box.x,box.y+h*0.49f,w,30), $"{_moves} moves   -   {TotalStars()} / {MaxStars} stars", _panelBody);

            bool last=_levelIndex>=Levels.Length-1;
            int next=_levelIndex+1;
            bool nextOpen = !last && IsUnlocked(next);
            bool nextGated = !last && !nextOpen;   // blocked by a star checkpoint

            // Buttons live in one bottom row (Menu + action) so neither can collide with
            // the title or the info line. Heights scale with the panel.
            float bh=Mathf.Min(92f, h*0.20f);
            float by=box.yMax - bh - h*0.07f;
            float infoY=box.y+h*0.585f, infoH=by-infoY-6f;
            if(nextGated)
            {
                int need=RequiredStars(next)-TotalStars();
                GUI.Label(new Rect(box.x+22,infoY,w-44,infoH),
                    $"Level {next+1} opens at {RequiredStars(next)} stars.\n{need} more to go — replay for stars!",_panelSub);
            }
            else
            {
                GUI.Label(new Rect(box.x,infoY,w,infoH), $"3 stars: {_lv.par} moves    2 stars: {_lv.par+2} moves", _panelSub);
            }

            float gap=16f;
            float menuW=Mathf.Min(150f, w*0.32f);
            float actW=Mathf.Min(330f, w*0.52f);
            float rowW=menuW+gap+actW, sx=cx-rowW/2f;
            var rMenu=new Rect(sx, by, menuW, bh); _uiRects.Add(rMenu);
            if(CozyButton(rMenu,"Menu",_btnMenu)){ Sfx.Click(); SceneManager.LoadScene("MainMenu"); }
            var rAct=new Rect(sx+menuW+gap, by, actW, bh); _uiRects.Add(rAct);
            if(nextGated)
            {
                if(CozyButton(rAct,"More stars",_btn)){ Sfx.Click(); LoadLevel(BestReplayLevel()); }
            }
            else if(CozyButton(rAct, last?"Play again":"Next level",_btn))
            { Sfx.Click(); LoadLevel(last?0:next); }
        }

        // When a checkpoint blocks the next level, send the player to the earliest
        // level where they haven't earned 3 stars yet — the easiest place to top up.
        private int BestReplayLevel()
        {
            for(int i=0;i<=_levelIndex;i++) if(StarsFor(i)<3) return i;
            return _levelIndex;
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

        // Soft radial disc used for animal glows and coloured bed blankets.
        private static Sprite _disc;
        private static Sprite SoftDisc()
        {
            if(_disc!=null) return _disc;
            int s=128; float half=s*0.5f;
            var tex=new Texture2D(s,s,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[s*s];
            for(int y=0;y<s;y++)for(int x=0;x<s;x++){
                float d=Mathf.Sqrt((x+0.5f-half)*(x+0.5f-half)+(y+0.5f-half)*(y+0.5f-half))/half;
                float a=Mathf.Clamp01(1f-d); a=a*a*(3f-2f*a);   // smooth falloff
                px[y*s+x]=new Color32(255,255,255,(byte)(a*255));
            }
            tex.SetPixels32(px); tex.Apply();
            _disc=Sprite.Create(tex,new Rect(0,0,s,s),new Vector2(0.5f,0.5f),s);
            return _disc;
        }

        // A chunky rounded arrow pointing +x (right). Rotate the transform for other dirs.
        private static Sprite _arrowSprite;
        private static Sprite ArrowSprite()
        {
            if(_arrowSprite!=null) return _arrowSprite;
            int s=128; var tex=new Texture2D(s,s,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color32[s*s];
            for(int y=0;y<s;y++)for(int x=0;x<s;x++){
                float cx=x+0.5f-64f, cy=y+0.5f-64f;
                // shaft: rounded bar; head: triangle narrowing to the tip at cx=48
                bool shaft = cx>=-44f && cx<=16f && Mathf.Abs(cy)<=15f;
                bool head  = cx>=8f && cx<=48f && Mathf.Abs(cy) <= (48f-cx)*0.95f;
                float inside = (shaft||head)?1f:0f;
                // cheap 1px anti-alias by sampling the boundary softly
                float aa=Mathf.Clamp01(inside);
                px[y*s+x]=new Color32(255,255,255,(byte)(aa*255));
            }
            tex.SetPixels32(px); tex.Apply();
            _arrowSprite=Sprite.Create(tex,new Rect(0,0,s,s),new Vector2(0.5f,0.5f),s);
            return _arrowSprite;
        }

        private static float DirAngle(Vector2Int d)=>
            d==Vector2Int.right?0f:d==Vector2Int.up?90f:d==Vector2Int.left?180f:270f;

        // A painted-feeling bedtime sky: deep indigo up top warming to dusky plum at the
        // horizon, a scatter of twinkle-sized stars, a haloed moon, and three layers of
        // rolling hills that get lighter with distance. All generated, so it costs no art.
        private static Sprite BgGradient()
        {
            if(_bg!=null) return _bg;
            int w=360,h=720;
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color[w*h];

            // sky palette (y=0 is the BOTTOM of the texture in Unity)
            var skyTop    = new Color(0.13f,0.11f,0.24f);
            var skyMid    = new Color(0.26f,0.18f,0.33f);
            var skyHorizon= new Color(0.47f,0.29f,0.38f);
            Vector2 moon=new Vector2(0.76f,0.84f); float mr=0.052f;
            var moonCol=new Color(1f,0.97f,0.88f);

            for(int y=0;y<h;y++)
            {
                float ny=(float)y/(h-1);
                for(int x=0;x<w;x++)
                {
                    float nx=(float)x/(w-1);
                    // vertical gradient: horizon warmth low, deep indigo high
                    Color c = ny<0.45f
                        ? Color.Lerp(skyHorizon,skyMid, ny/0.45f)
                        : Color.Lerp(skyMid,skyTop,(ny-0.45f)/0.55f);
                    // moon halo + core
                    float aspect=(float)w/h;
                    float dx=(nx-moon.x)*aspect, dy=ny-moon.y;
                    float d=Mathf.Sqrt(dx*dx+dy*dy);
                    float halo=Mathf.Clamp01(1f-d/(mr*5.5f)); halo*=halo*halo;
                    float core=Mathf.Clamp01(1f-d/mr);
                    core=core*core*(3f-2f*core);
                    c=Color.Lerp(c,moonCol,Mathf.Clamp01(halo*0.30f+core*0.95f));
                    px[y*w+x]=c;
                }
            }

            // stars — deterministic scatter, denser and brighter high in the sky
            var rng=new System.Random(20260726);
            for(int i=0;i<170;i++)
            {
                float sx=(float)rng.NextDouble(), sy=0.38f+(float)rng.NextDouble()*0.62f;
                float bright=0.35f+(float)rng.NextDouble()*0.65f;
                // fade stars near the moon so its glow stays clean
                float aspect=(float)w/h;
                float ddx=(sx-moon.x)*aspect, ddy=sy-moon.y;
                if(Mathf.Sqrt(ddx*ddx+ddy*ddy) < mr*4f) continue;
                int cx=Mathf.RoundToInt(sx*(w-1)), cy=Mathf.RoundToInt(sy*(h-1));
                float rad=(float)rng.NextDouble()<0.18f?1.7f:0.95f;   // a few bigger ones
                int ri=Mathf.CeilToInt(rad);
                for(int oy=-ri;oy<=ri;oy++)for(int ox=-ri;ox<=ri;ox++)
                {
                    int tx=cx+ox, ty=cy+oy;
                    if(tx<0||tx>=w||ty<0||ty>=h) continue;
                    float dd=Mathf.Sqrt(ox*ox+oy*oy);
                    float a=Mathf.Clamp01(1f-dd/rad)*bright;
                    if(a<=0f) continue;
                    px[ty*w+tx]=Color.Lerp(px[ty*w+tx],new Color(1f,0.98f,0.92f),a);
                }
            }

            // three hill layers, far (lightest) to near (darkest silhouette)
            DrawHills(px,w,h, 0.30f, 0.055f, 1.7f, 0.6f,  new Color(0.32f,0.22f,0.35f));
            DrawHills(px,w,h, 0.21f, 0.05f,  2.6f, 2.1f,  new Color(0.24f,0.16f,0.29f));
            DrawHills(px,w,h, 0.12f, 0.045f, 3.7f, 4.3f,  new Color(0.17f,0.11f,0.22f));

            var out32=new Color32[w*h];
            for(int i=0;i<px.Length;i++) out32[i]=px[i];
            tex.SetPixels32(out32); tex.Apply();
            _bg=Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(0.5f,0.5f),1);
            return _bg;
        }

        // Fills everything below a soft sine-blended ridge line with `col`.
        private static void DrawHills(Color[] px,int w,int h,float baseY,float amp,float freq,float phase,Color col)
        {
            for(int x=0;x<w;x++)
            {
                float nx=(float)x/(w-1);
                float ridge=baseY
                    + Mathf.Sin(nx*freq*Mathf.PI*2f+phase)*amp
                    + Mathf.Sin(nx*freq*1.9f*Mathf.PI*2f+phase*1.7f)*amp*0.35f;
                int ry=Mathf.RoundToInt(ridge*(h-1));
                for(int y=0;y<=ry && y<h;y++)
                {
                    // soften the top 2px of the ridge so it doesn't alias
                    float a=(ry-y)<2 ? 0.55f : 1f;
                    px[y*w+x]=Color.Lerp(px[y*w+x],col,a);
                }
            }
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
            _hintText=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter,wordWrap=true};
            _panelBody=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            _panelSub=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter};
            _title.normal.textColor=Color.white;
            _sub.normal.textColor=new Color(1f,0.95f,0.86f,0.95f);
            // Text ON THE DARK SKY is warm cream (brown vanished against the night);
            // brown is reserved for text sitting inside the cream panels.
            _hintText.normal.textColor=new Color(1f,0.93f,0.80f,0.98f);
            _win.normal.textColor=Brown;
            _panelBody.normal.textColor=Brown;
            _panelSub.normal.textColor=new Color(0.42f,0.27f,0.16f);
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
