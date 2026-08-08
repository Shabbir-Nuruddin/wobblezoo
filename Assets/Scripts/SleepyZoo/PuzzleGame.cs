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
    /// required.
    ///
    /// CHAPTERS. The game is two 20-level chapters, and the second one breaks the
    /// rule the first one spent 20 levels teaching:
    ///
    ///   Chapter 1 "Bedtime Shuffle"  — beds are just destinations. Land on one
    ///                                  and the next swipe drags you straight
    ///                                  back off it. That near-miss frustration
    ///                                  is the whole point: it's the setup.
    ///   Chapter 2 "Sleepyheads"      — the beds turn sticky. The instant an
    ///                                  animal *touches* its own bed, even
    ///                                  mid-slide, it snuggles in, stops dead,
    ///                                  and never moves again — becoming a soft
    ///                                  wall for everyone still awake.
    ///
    /// Chapter 2 is hidden behind a star total, and the menu deliberately refuses
    /// to say what changes, so finishing chapter 1 is the reward.
    ///
    /// Every level below is BFS-verified by tools/gen_levels.py, which mirrors
    /// SlideSim exactly: par is the true optimal move count, so 3 stars is always
    /// achievable and never a designer's guess.
    /// </summary>
    // `partial` so the nightly puzzle pool can live in its own generated file
    // (DailyLevels.cs) without a tool ever having to splice two arrays in here.
    public partial class PuzzleGame : MonoBehaviour
    {
        // A level entity: where an animal starts (x,y) and which bed it belongs to (bx,by).
        private struct EntDef { public int x, y, bx, by;
            public EntDef(int x, int y, int bx, int by){ this.x=x; this.y=y; this.bx=bx; this.by=by; } }

        /// One level. The last four fields are the chapter "toys" — a level only ever
        /// carries the toy belonging to its own chapter (see Rules), so a board never
        /// asks the player to hold more than one new idea at a time.
        ///   rugs   - silk you can cross but never stop on
        ///   honey  - touch it and you stop dead, right there
        ///   holes  - burrows in pairs: slide into one, come out of the other
        ///   heavy  - index of the one animal too heavy to slide on its own
        private class Lv
        {
            public int w, h, par; public string hint;
            public Vector2Int[] walls; public EntDef[] ents;
            public Vector2Int[] rugs, honey, holes; public int heavy;
            public Lv(int w,int h,int par,string hint,Vector2Int[] walls,EntDef[] e,
                      Vector2Int[] rugs=null, Vector2Int[] honey=null, Vector2Int[] holes=null, int heavy=-1)
            { this.w=w; this.h=h; this.par=par; this.hint=hint; this.walls=walls; this.ents=e;
              this.rugs=rugs??System.Array.Empty<Vector2Int>();
              this.honey=honey??System.Array.Empty<Vector2Int>();
              this.holes=holes??System.Array.Empty<Vector2Int>();
              this.heavy=heavy; }
        }

        /// What's switched on in each chapter. Sticky beds arrive in chapter 2 and
        /// never leave (they fixed chapter 1's one real frustration — taking that back
        /// would be a punishment). Everything after that is a visible object on the
        /// board, so "what's new" is something you can point at rather than a rule you
        /// have to be told. Chapter 8 adds nothing new: it's the exam.
        private class Rule
        {
            public string name, blurb, tease, taught;
            public bool sticky, anyBed;
            public Rule(string name,string blurb,string tease,string taught,bool sticky,bool anyBed)
            { this.name=name; this.blurb=blurb; this.tease=tease; this.taught=taught;
              this.sticky=sticky; this.anyBed=anyBed; }
        }
        private static readonly Rule[] Rules =
        {
            new Rule("Bedtime Shuffle","Swipe - everyone slides at once.",
                     "Where it all begins.","Swipe any way you like. Everybody moves.",false,false),
            new Rule("Sleepyheads","Touch your own bed and you're asleep for good.",
                     "One bedtime rule you know by heart is about to change.",
                     "Touch your own bed - even in passing - and you're in for the night.",true,false),
            new Rule("Musical Beds","Tonight nobody minds whose bed is whose.",
                     "Something about these beds is different tonight.",
                     "Any animal, any bed. Just fill them all.",true,true),
            new Rule("Slippery Rugs","You can cross a silk rug, but you can't stop on one.",
                     "The floor is not going to help you.",
                     "Silk is too slippery to sleep on - you'll always slide back off.",true,false),
            new Rule("Honey Puddles","Touch the honey and you stop dead.",
                     "Someone has been careless in the pantry.",
                     "Honey is sticky. Touch it and that's where you stay.",true,false),
            new Rule("Rabbit Holes","Slide into one burrow, pop out of the other.",
                     "There are new ways through this room.",
                     "Burrows come in pairs. Go in one, come out the other, keep sliding.",true,false),
            new Rule("Heavy Sleepers","The big one won't slide on its own - it has to be pushed.",
                     "Not everyone here is a light sleeper.",
                     "The big one is fast asleep. It only moves when somebody bumps it.",true,false),
            new Rule("The Long Night","Everything you've learned, in one room.",
                     "One last night. Everything at once.",
                     "No new rules tonight. Just everything you already know.",true,false),
        };

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
        // Levels 1-20  = chapter 1, normal beds.
        // Levels 21-40 = chapter 2, sticky beds (see StickyBeds / SlideSim).
        private static readonly Lv[] Levels =
        {


            // ===================== CHAPTER 1 =====================

            // ===================== CHAPTER 1 =====================
            new Lv(4,4,2,"Swipe any direction. Everyone slides until something stops them.",
                new Vector2Int[0],
                new[]{ new EntDef(2,2, 3,3) }),
            new Lv(4,4,2,"Walls stop you. So does the edge of the room.",
                new[]{ W2(3,3) },
                new[]{ new EntDef(3,1, 0,3) }),
            new Lv(4,4,3,"Two friends now. One swipe moves them both.",
                new[]{ W2(0,3) },
                new[]{ new EntDef(0,1, 1,3) }),
            new Lv(4,4,3,"Line them up, then send them home.",
                new[]{ W2(1,3) },
                new[]{ new EntDef(3,1, 0,2), new EntDef(1,1, 0,3) }),
            new Lv(4,4,3,"Animals stop each other too - use that.",
                new[]{ W2(2,3) },
                new[]{ new EntDef(2,0, 3,3), new EntDef(1,1, 2,2) }),
            new Lv(4,4,3,"A bigger room. Same one rule.",
                new[]{ W2(1,2),W2(2,3) },
                new[]{ new EntDef(1,0, 0,3), new EntDef(2,0, 0,1) }),
            new Lv(5,5,4,"Sometimes the long way round is the short way.",
                new[]{ W2(3,0) },
                new[]{ new EntDef(0,1, 0,4), new EntDef(1,1, 4,4) }),
            new Lv(5,5,4,"Blocks are just walls you can plan around.",
                new[]{ W2(1,2),W2(3,1) },
                new[]{ new EntDef(0,1, 0,0), new EntDef(2,1, 0,4) }),
            new Lv(5,5,4,"Send the far one first.",
                new[]{ W2(1,0),W2(3,3) },
                new[]{ new EntDef(1,4, 0,0), new EntDef(4,4, 4,0) }),
            new Lv(5,5,4,"Corners are good places to park somebody.",
                new[]{ W2(3,3),W2(4,1) },
                new[]{ new EntDef(2,0, 4,2), new EntDef(0,4, 4,4) }),
            new Lv(5,5,4,"Three friends. Nobody gets left out.",
                new[]{ W2(0,3),W2(3,1) },
                new[]{ new EntDef(4,4, 1,3), new EntDef(3,2, 0,4) }),
            new Lv(5,5,4,"One swipe, three animals. Watch where they all end up.",
                new[]{ W2(1,2),W2(4,0) },
                new[]{ new EntDef(2,3, 4,4), new EntDef(3,3, 4,1) }),
            new Lv(5,5,4,"Use a friend as a wall for another friend.",
                new[]{ W2(3,4) },
                new[]{ new EntDef(3,1, 0,4), new EntDef(3,0, 0,3), new EntDef(2,3, 4,4) }),
            new Lv(5,5,4,"The order they stop in is the whole puzzle.",
                new[]{ W2(2,0),W2(4,2) },
                new[]{ new EntDef(0,1, 0,4), new EntDef(3,0, 3,4), new EntDef(4,1, 0,3) }),
            new Lv(5,5,5,"Take your time. Nothing here is in a hurry.",
                new[]{ W2(3,4),W2(4,4) },
                new[]{ new EntDef(0,3, 0,1), new EntDef(3,1, 0,0), new EntDef(0,4, 1,0) }),
            new Lv(5,5,4,"If it looks stuck, undo and try the other way.",
                new[]{ W2(0,0),W2(2,2) },
                new[]{ new EntDef(1,3, 4,4), new EntDef(1,4, 3,4), new EntDef(1,0, 4,3) }),
            new Lv(5,5,5,"Get one home, then work on the rest.",
                new[]{ W2(0,1),W2(1,4),W2(4,3) },
                new[]{ new EntDef(0,3, 0,0), new EntDef(2,4, 1,0), new EntDef(3,1, 0,2) }),
            new Lv(5,5,4,"Every bed wants its own animal.",
                new[]{ W2(0,1),W2(1,2),W2(3,0) },
                new[]{ new EntDef(3,4, 4,0), new EntDef(2,4, 4,1), new EntDef(0,3, 2,0) }),
            new Lv(5,5,5,"Almost the end of the first room.",
                new[]{ W2(0,0),W2(2,3),W2(4,2) },
                new[]{ new EntDef(2,1, 2,0), new EntDef(2,2, 3,3), new EntDef(1,2, 1,0) }),
            new Lv(5,5,5,"Last one here. Then something changes.",
                new[]{ W2(0,2),W2(1,0),W2(2,4) },
                new[]{ new EntDef(1,2, 4,0), new EntDef(2,2, 3,0), new EntDef(3,2, 0,3) }),

            // ===================== CHAPTER 2 =====================
            new Lv(4,4,2,"Beds are sticky now. Touch yours and you're asleep for good.",
                new Vector2Int[0],
                new[]{ new EntDef(0,0, 1,0), new EntDef(0,2, 2,1) }),
            new Lv(5,5,3,"An animal that's asleep never moves again.",
                new[]{ W2(4,2) },
                new[]{ new EntDef(4,0, 0,3), new EntDef(3,3, 2,4) }),
            new Lv(5,5,3,"A sleeping friend is a wall. That's useful.",
                new[]{ W2(2,3) },
                new[]{ new EntDef(0,3, 2,4), new EntDef(4,4, 4,1) }),
            new Lv(5,5,3,"Park somebody in their bed, then use them.",
                new[]{ W2(3,3) },
                new[]{ new EntDef(3,1, 0,2), new EntDef(0,4, 2,4) }),
            new Lv(5,5,4,"Who should fall asleep first?",
                new[]{ W2(0,4),W2(2,4) },
                new[]{ new EntDef(0,3, 2,3), new EntDef(4,0, 1,1) }),
            new Lv(5,5,3,"Sometimes you want to NOT land on your bed yet.",
                new[]{ W2(2,4) },
                new[]{ new EntDef(1,0, 1,1), new EntDef(0,2, 4,0), new EntDef(1,3, 1,4) }),
            new Lv(5,5,4,"Three friends and sticky beds.",
                new[]{ W2(2,3),W2(4,4) },
                new[]{ new EntDef(1,3, 1,2), new EntDef(0,2, 3,3), new EntDef(0,1, 3,0) }),
            new Lv(5,5,4,"Wake nobody. Once they're in, they're in.",
                new[]{ W2(3,0),W2(4,2) },
                new[]{ new EntDef(1,3, 4,4), new EntDef(3,1, 1,2), new EntDef(1,0, 0,1) }),
            new Lv(5,5,4,"Build a wall out of sleepers.",
                new[]{ W2(2,0),W2(2,1) },
                new[]{ new EntDef(1,4, 3,0), new EntDef(3,1, 4,3), new EntDef(3,2, 1,3) }),
            new Lv(5,5,6,"The first one to bed changes everything after.",
                new[]{ W2(2,3),W2(3,2),W2(4,4) },
                new[]{ new EntDef(3,1, 3,4), new EntDef(1,0, 2,0), new EntDef(1,3, 4,2) }),
            new Lv(6,6,4,"Try it the other way round.",
                new[]{ W2(5,3),W2(5,4) },
                new[]{ new EntDef(3,1, 5,0), new EntDef(3,2, 1,5), new EntDef(0,3, 0,2) }),
            new Lv(6,6,5,"Slow is fine. Undo is free.",
                new[]{ W2(1,3),W2(2,1),W2(2,4) },
                new[]{ new EntDef(3,4, 3,5), new EntDef(4,3, 3,2), new EntDef(2,5, 5,2) }),
            new Lv(6,6,4,"Nearly there.",
                new[]{ W2(1,2),W2(2,2) },
                new[]{ new EntDef(0,2, 0,0), new EntDef(0,4, 4,5), new EntDef(3,1, 0,5), new EntDef(5,0, 1,4) }),
            new Lv(6,6,4,"Four friends now. Same idea.",
                new[]{ W2(1,2),W2(3,0),W2(5,2) },
                new[]{ new EntDef(4,4, 2,4), new EntDef(3,5, 5,5), new EntDef(2,3, 0,0), new EntDef(5,1, 4,0) }),
            new Lv(6,6,5,"One at a time, in the right order.",
                new[]{ W2(0,1),W2(2,2),W2(2,5) },
                new[]{ new EntDef(3,5, 4,0), new EntDef(5,4, 0,4), new EntDef(2,0, 5,3), new EntDef(1,5, 1,1) }),
            new Lv(6,6,5,"The awkward one usually goes first.",
                new[]{ W2(0,3),W2(1,5),W2(3,4) },
                new[]{ new EntDef(4,0, 5,5), new EntDef(1,2, 1,0), new EntDef(4,2, 5,2), new EntDef(5,4, 2,3) }),
            new Lv(6,6,5,"A sleeper in the right spot solves the rest.",
                new[]{ W2(1,1),W2(3,0),W2(5,2) },
                new[]{ new EntDef(2,0, 5,1), new EntDef(3,3, 1,4), new EntDef(1,3, 3,5), new EntDef(3,2, 2,2) }),
            new Lv(6,6,5,"Think about who blocks who.",
                new[]{ W2(0,5),W2(3,1),W2(3,4),W2(5,5) },
                new[]{ new EntDef(4,4, 4,2), new EntDef(3,0, 1,4), new EntDef(0,4, 0,1), new EntDef(2,0, 4,3) }),
            new Lv(6,6,5,"Second-to-last in this room.",
                new[]{ W2(0,0),W2(4,4),W2(4,5),W2(5,4) },
                new[]{ new EntDef(1,1, 3,5), new EntDef(4,1, 1,3), new EntDef(2,1, 2,4), new EntDef(2,3, 3,1) }),
            new Lv(6,6,5,"Last one. Then the rules move again.",
                new[]{ W2(1,5),W2(4,2),W2(4,4),W2(5,5) },
                new[]{ new EntDef(2,5, 5,0), new EntDef(0,4, 2,1), new EntDef(3,5, 3,3), new EntDef(1,3, 1,4) }),

            // ===================== CHAPTER 3 =====================
            new Lv(5,5,3,"Sticky beds - but tonight nobody minds whose bed is whose.",
                new Vector2Int[0],
                new[]{ new EntDef(4,0, 0,1), new EntDef(0,4, 1,2) }),
            new Lv(5,5,3,"Any animal, any bed. Just fill them all.",
                new[]{ W2(1,4) },
                new[]{ new EntDef(3,3, 0,0), new EntDef(2,2, 4,3) }),
            new Lv(5,5,3,"Two friends, two beds, either way round.",
                new[]{ W2(0,4) },
                new[]{ new EntDef(0,0, 4,3), new EntDef(2,4, 3,1) }),
            new Lv(5,5,3,"Sometimes the far bed is the easy one.",
                new[]{ W2(3,1) },
                new[]{ new EntDef(4,4, 1,2), new EntDef(1,0, 3,0), new EntDef(1,4, 2,4) }),
            new Lv(5,5,7,"Fill the awkward bed first.",
                new[]{ W2(0,3),W2(1,2),W2(2,3) },
                new[]{ new EntDef(2,1, 0,2), new EntDef(2,2, 0,4), new EntDef(3,0, 1,3) }),
            new Lv(5,5,4,"Swapping who goes where can save you three swipes.",
                new[]{ W2(1,0),W2(2,3) },
                new[]{ new EntDef(1,4, 1,2), new EntDef(2,1, 0,1), new EntDef(4,1, 2,0) }),
            new Lv(6,6,4,"Count the beds, not the animals.",
                new[]{ W2(1,4),W2(3,2) },
                new[]{ new EntDef(0,5, 1,3), new EntDef(0,0, 5,0), new EntDef(0,4, 4,5) }),
            new Lv(6,6,4,"One bed is harder to reach than the rest. Start there.",
                new[]{ W2(1,3),W2(3,2) },
                new[]{ new EntDef(0,4, 3,5), new EntDef(0,2, 3,1), new EntDef(2,1, 0,0) }),
            new Lv(6,6,4,"Whoever goes first decides the rest.",
                new[]{ W2(0,2),W2(2,4),W2(4,1) },
                new[]{ new EntDef(0,5, 5,1), new EntDef(3,2, 2,0), new EntDef(4,4, 5,4) }),
            new Lv(6,6,4,"Any order you like - but only one order is short.",
                new[]{ W2(4,3),W2(5,0) },
                new[]{ new EntDef(3,4, 1,4), new EntDef(3,5, 1,2), new EntDef(0,2, 2,5), new EntDef(2,0, 5,2) }),
            new Lv(6,6,5,"Leave the open bed for last.",
                new[]{ W2(0,5),W2(1,5),W2(3,1) },
                new[]{ new EntDef(5,1, 4,1), new EntDef(5,0, 3,5), new EntDef(5,2, 4,5), new EntDef(4,2, 4,4) }),
            new Lv(6,6,5,"Five beds, five friends, no name tags.",
                new[]{ W2(0,5),W2(5,3),W2(5,4) },
                new[]{ new EntDef(2,3, 2,4), new EntDef(1,1, 2,0), new EntDef(4,2, 1,2), new EntDef(3,1, 0,4) }),
            new Lv(6,6,5,"Look for the bed only one animal can reach.",
                new[]{ W2(0,4),W2(2,5),W2(3,3) },
                new[]{ new EntDef(5,3, 2,0), new EntDef(2,2, 1,5), new EntDef(3,0, 5,1), new EntDef(0,3, 4,4) }),
            new Lv(6,6,5,"Nearly there. Fill the corner first.",
                new[]{ W2(1,3),W2(1,4),W2(2,0),W2(2,5) },
                new[]{ new EntDef(5,5, 5,0), new EntDef(5,3, 1,2), new EntDef(2,1, 4,3), new EntDef(5,2, 2,4) }),
            new Lv(6,6,5,"Every bed full, everybody asleep. That's the whole job.",
                new[]{ W2(0,0),W2(1,2),W2(3,4),W2(5,3) },
                new[]{ new EntDef(0,5, 1,0), new EntDef(1,1, 4,1), new EntDef(0,4, 3,0), new EntDef(1,3, 3,5) }),

            // ===================== CHAPTER 4 =====================
            new Lv(5,5,3,"Silk is too slippery to sleep on - you always slide back off.",
                new Vector2Int[0],
                new[]{ new EntDef(4,2, 0,2), new EntDef(4,1, 3,2) },
                rugs: new[]{ W2(4,4) }),
            new Lv(5,5,3,"Cross the rug. Don't try to stop on it.",
                new[]{ W2(4,1) },
                new[]{ new EntDef(4,4, 3,3), new EntDef(0,4, 2,0) },
                rugs: new[]{ W2(4,2) }),
            new Lv(5,5,3,"A rug can carry you straight past your own bed. Careful.",
                new[]{ W2(0,2) },
                new[]{ new EntDef(2,2, 3,4), new EntDef(1,0, 3,2) },
                rugs: new[]{ W2(1,4),W2(4,0) }),
            new Lv(5,5,3,"Use the rug to reach somewhere you couldn't stop before.",
                new[]{ W2(4,0) },
                new[]{ new EntDef(2,2, 0,1), new EntDef(2,0, 1,2), new EntDef(1,0, 1,1) },
                rugs: new[]{ W2(2,3),W2(3,2) }),
            new Lv(5,5,6,"Rugs turn short slides into long ones.",
                new[]{ W2(2,1),W2(3,0) },
                new[]{ new EntDef(2,2, 4,3), new EntDef(3,1, 1,3), new EntDef(0,3, 4,2) },
                rugs: new[]{ W2(0,0),W2(1,2) }),
            new Lv(5,5,4,"Come at the bed from the other side.",
                new[]{ W2(1,4),W2(4,0) },
                new[]{ new EntDef(3,4, 0,1), new EntDef(4,2, 3,0), new EntDef(4,3, 1,3) },
                rugs: new[]{ W2(1,2),W2(2,0) }),
            new Lv(6,6,4,"The rug is a corridor, not a room.",
                new[]{ W2(4,5),W2(5,0) },
                new[]{ new EntDef(0,1, 0,4), new EntDef(1,3, 2,3), new EntDef(2,1, 4,4) },
                rugs: new[]{ W2(0,2),W2(1,5) }),
            new Lv(6,6,4,"Two rugs in a row is just a longer corridor.",
                new[]{ W2(1,2),W2(3,0) },
                new[]{ new EntDef(0,1, 0,2), new EntDef(5,4, 2,0), new EntDef(2,2, 3,4) },
                rugs: new[]{ W2(0,5),W2(1,3) }),
            new Lv(6,6,4,"Stop before the silk, not on it.",
                new[]{ W2(4,1),W2(4,2),W2(4,5) },
                new[]{ new EntDef(2,3, 1,3), new EntDef(5,4, 2,2), new EntDef(2,1, 0,3) },
                rugs: new[]{ W2(3,2),W2(3,5),W2(4,0) }),
            new Lv(6,6,4,"Sometimes the rug is the only way across.",
                new[]{ W2(2,3),W2(5,3) },
                new[]{ new EntDef(3,0, 1,4), new EntDef(4,5, 4,0), new EntDef(0,3, 0,1), new EntDef(2,1, 3,2) },
                rugs: new[]{ W2(3,5),W2(4,3),W2(5,0) }),
            new Lv(6,6,5,"A friend parked on the far side gives you something to stop against.",
                new[]{ W2(0,1),W2(1,3),W2(2,1) },
                new[]{ new EntDef(4,0, 2,3), new EntDef(3,0, 4,4), new EntDef(3,1, 4,5), new EntDef(2,4, 5,0) },
                rugs: new[]{ W2(3,2),W2(5,1),W2(5,4) }),
            new Lv(6,6,5,"Plan where you'll land, not where you'll pass.",
                new[]{ W2(1,5),W2(2,5),W2(5,0) },
                new[]{ new EntDef(3,0, 3,2), new EntDef(3,5, 4,4), new EntDef(1,3, 4,0), new EntDef(4,1, 1,1) },
                rugs: new[]{ W2(0,3),W2(1,0),W2(4,5) }),
            new Lv(6,6,5,"Silk never lets go until something solid does.",
                new[]{ W2(1,0),W2(4,3),W2(4,4) },
                new[]{ new EntDef(0,2, 2,1), new EntDef(3,1, 3,4), new EntDef(5,3, 2,2), new EntDef(1,1, 1,3) },
                rugs: new[]{ W2(0,0),W2(0,5),W2(5,1) }),
            new Lv(6,6,5,"Almost the last of the rugs. Take it slowly.",
                new[]{ W2(1,3),W2(4,0),W2(4,3),W2(5,3) },
                new[]{ new EntDef(3,3, 0,1), new EntDef(0,5, 4,5), new EntDef(2,5, 2,1), new EntDef(0,3, 3,4) },
                rugs: new[]{ W2(1,2),W2(2,2),W2(5,1) }),
            new Lv(6,6,5,"One room, four rugs, five sleepy animals.",
                new[]{ W2(2,2),W2(2,5),W2(4,3),W2(5,5) },
                new[]{ new EntDef(2,1, 1,0), new EntDef(5,4, 4,2), new EntDef(4,5, 4,0), new EntDef(0,4, 0,2) },
                rugs: new[]{ W2(3,2),W2(5,0),W2(5,1),W2(5,3) }),

            // ===================== CHAPTER 5 =====================
            new Lv(5,5,3,"Honey is sticky. Touch it and that's where you stay.",
                new Vector2Int[0],
                new[]{ new EntDef(1,2, 2,3), new EntDef(4,1, 1,1) },
                honey: new[]{ W2(0,3) }),
            new Lv(5,5,3,"Honey stops you dead - useful, if you aim it.",
                new[]{ W2(4,4) },
                new[]{ new EntDef(3,4, 2,2), new EntDef(2,0, 2,3) },
                honey: new[]{ W2(3,2) }),
            new Lv(5,5,3,"Park someone in the honey on purpose.",
                new[]{ W2(2,3) },
                new[]{ new EntDef(1,3, 0,2), new EntDef(2,4, 2,0) },
                honey: new[]{ W2(1,4),W2(4,0) }),
            new Lv(5,5,3,"Honey beats a long slide every time.",
                new[]{ W2(0,2) },
                new[]{ new EntDef(4,1, 4,0), new EntDef(2,1, 2,0), new EntDef(3,1, 1,3) },
                honey: new[]{ W2(2,2),W2(3,3) }),
            new Lv(5,5,4,"Use the honey to stop short of a bed.",
                new[]{ W2(1,1),W2(1,2) },
                new[]{ new EntDef(4,3, 4,1), new EntDef(0,2, 2,1), new EntDef(0,1, 0,3) },
                honey: new[]{ W2(2,3),W2(2,4) }),
            new Lv(5,5,4,"The honey is a brake, not a wall.",
                new[]{ W2(0,4),W2(3,1) },
                new[]{ new EntDef(2,2, 3,2), new EntDef(3,3, 0,2), new EntDef(2,0, 1,3) },
                honey: new[]{ W2(1,0),W2(2,1) }),
            new Lv(6,6,4,"Two puddles make a very short corridor.",
                new[]{ W2(4,1),W2(4,2) },
                new[]{ new EntDef(3,2, 2,1), new EntDef(5,1, 0,2), new EntDef(4,3, 3,3) },
                honey: new[]{ W2(0,5),W2(2,0) }),
            new Lv(6,6,4,"Whoever reaches the honey first blocks everyone behind.",
                new[]{ W2(2,3),W2(2,4) },
                new[]{ new EntDef(2,0, 0,3), new EntDef(3,1, 2,1), new EntDef(5,2, 0,1) },
                honey: new[]{ W2(2,2),W2(4,1) }),
            new Lv(6,6,4,"Send the wrong one into the honey and you're stuck.",
                new[]{ W2(2,3),W2(3,0),W2(3,3) },
                new[]{ new EntDef(1,0, 3,4), new EntDef(5,5, 1,5), new EntDef(3,2, 3,1) },
                honey: new[]{ W2(0,3),W2(1,4),W2(5,2) }),
            new Lv(6,6,4,"Honey first, beds after.",
                new[]{ W2(2,3),W2(3,3) },
                new[]{ new EntDef(2,5, 3,4), new EntDef(1,1, 0,2), new EntDef(2,0, 4,4), new EntDef(5,5, 5,4) },
                honey: new[]{ W2(0,5),W2(4,0),W2(4,5) }),
            new Lv(6,6,5,"A sleeper and a puddle make a pocket.",
                new[]{ W2(0,0),W2(3,5),W2(5,2) },
                new[]{ new EntDef(0,5, 1,1), new EntDef(1,2, 4,0), new EntDef(5,1, 2,3), new EntDef(4,3, 0,2) },
                honey: new[]{ W2(4,1),W2(4,2),W2(4,5) }),
            new Lv(6,6,5,"Think about who must NOT touch the honey.",
                new[]{ W2(2,3),W2(4,0),W2(4,1) },
                new[]{ new EntDef(5,0, 3,1), new EntDef(1,2, 5,4), new EntDef(3,5, 3,0), new EntDef(1,4, 0,3) },
                honey: new[]{ W2(2,1),W2(3,4),W2(5,5) }),
            new Lv(6,6,5,"The honey is doing half the work. Let it.",
                new[]{ W2(0,1),W2(5,2),W2(5,5) },
                new[]{ new EntDef(2,2, 3,3), new EntDef(4,0, 1,3), new EntDef(5,0, 1,1), new EntDef(2,5, 2,4) },
                honey: new[]{ W2(1,5),W2(2,3),W2(3,0) }),
            new Lv(6,6,5,"Nearly the last of the mess. Mind your step.",
                new[]{ W2(0,4),W2(1,5),W2(3,0),W2(5,1) },
                new[]{ new EntDef(3,2, 4,1), new EntDef(0,3, 3,5), new EntDef(1,3, 4,4), new EntDef(5,0, 4,0) },
                honey: new[]{ W2(2,5),W2(4,2),W2(4,3) }),
            new Lv(6,6,7,"Five friends, and honey everywhere.",
                new[]{ W2(3,5),W2(4,0),W2(4,4),W2(5,4),W2(5,5) },
                new[]{ new EntDef(3,0, 2,4), new EntDef(5,1, 4,3), new EntDef(2,1, 0,0), new EntDef(1,3, 3,2) },
                honey: new[]{ W2(1,0),W2(1,5),W2(3,3),W2(4,2) }),

            // ===================== CHAPTER 6 =====================
            new Lv(5,5,3,"Burrows come in pairs. In one, out the other, still sliding.",
                new Vector2Int[0],
                new[]{ new EntDef(0,2, 4,4), new EntDef(2,2, 0,1) },
                holes: new[]{ W2(3,4),W2(0,4) }),
            new Lv(5,5,3,"You keep your speed all the way through a burrow.",
                new[]{ W2(1,4) },
                new[]{ new EntDef(1,2, 4,4), new EntDef(4,2, 0,0) },
                holes: new[]{ W2(1,0),W2(2,4) }),
            new Lv(5,5,3,"A burrow can put you where no swipe could reach.",
                new[]{ W2(1,2) },
                new[]{ new EntDef(3,1, 1,0), new EntDef(2,0, 4,3) },
                holes: new[]{ W2(4,2),W2(2,1) }),
            new Lv(5,5,3,"Follow the colours - a pair shares one colour.",
                new[]{ W2(0,2) },
                new[]{ new EntDef(4,2, 0,3), new EntDef(1,0, 1,1), new EntDef(2,1, 2,4) },
                holes: new[]{ W2(3,4),W2(4,3) }),
            new Lv(5,5,6,"Sometimes the long way round is underground.",
                new[]{ W2(2,4),W2(3,2) },
                new[]{ new EntDef(0,3, 1,1), new EntDef(0,2, 0,0), new EntDef(1,2, 3,1) },
                holes: new[]{ W2(1,4),W2(4,0) }),
            new Lv(5,5,4,"A friend standing on the far end blocks the burrow.",
                new[]{ W2(1,4),W2(2,0) },
                new[]{ new EntDef(4,3, 3,1), new EntDef(0,3, 1,2), new EntDef(2,2, 3,4) },
                holes: new[]{ W2(4,2),W2(3,3) }),
            new Lv(6,6,4,"Go in the near one to come out of the far one.",
                new[]{ W2(1,0),W2(3,0) },
                new[]{ new EntDef(5,2, 4,5), new EntDef(0,3, 5,1), new EntDef(0,2, 2,4) },
                holes: new[]{ W2(4,2),W2(2,0) }),
            new Lv(6,6,4,"Two pairs means two ways across.",
                new[]{ W2(2,5),W2(5,5) },
                new[]{ new EntDef(4,3, 0,3), new EntDef(2,4, 0,2), new EntDef(0,4, 1,0) },
                holes: new[]{ W2(4,4),W2(2,2) }),
            new Lv(6,6,4,"Sometimes you want to miss the burrow.",
                new[]{ W2(2,3),W2(3,4),W2(3,5) },
                new[]{ new EntDef(4,1, 5,5), new EntDef(5,4, 1,0), new EntDef(5,0, 5,2) },
                holes: new[]{ W2(5,3),W2(0,4) }),
            new Lv(6,6,4,"The exit decides where you stop, not the entrance.",
                new[]{ W2(1,0),W2(3,3) },
                new[]{ new EntDef(1,4, 0,5), new EntDef(0,2, 1,3), new EntDef(4,1, 0,3), new EntDef(5,0, 2,1) },
                holes: new[]{ W2(1,5),W2(5,1) }),
            new Lv(6,6,5,"Line them up before you dive.",
                new[]{ W2(3,3),W2(4,2),W2(4,3) },
                new[]{ new EntDef(5,3, 1,1), new EntDef(3,5, 5,2), new EntDef(0,3, 0,4), new EntDef(3,2, 1,3) },
                holes: new[]{ W2(2,0),W2(3,1) }),
            new Lv(6,6,5,"One burrow, one bed, one swipe - if you set it up right.",
                new[]{ W2(0,0),W2(2,0),W2(5,2) },
                new[]{ new EntDef(1,1, 4,5), new EntDef(4,3, 1,4), new EntDef(3,5, 5,1), new EntDef(2,1, 4,1) },
                holes: new[]{ W2(2,3),W2(1,0) }),
            new Lv(6,6,5,"Watch what the burrow does to the animal behind you.",
                new[]{ W2(3,1),W2(3,5),W2(4,5) },
                new[]{ new EntDef(1,2, 2,5), new EntDef(2,1, 1,3), new EntDef(4,2, 5,2), new EntDef(4,3, 0,0) },
                holes: new[]{ W2(5,0),W2(0,2) }),
            new Lv(6,6,5,"Nearly through. Where does that exit put you?",
                new[]{ W2(0,3),W2(2,3),W2(4,4),W2(5,3) },
                new[]{ new EntDef(5,1, 3,1), new EntDef(0,4, 0,0), new EntDef(5,0, 0,5), new EntDef(4,3, 2,2) },
                holes: new[]{ W2(4,0),W2(2,5) }),
            new Lv(6,6,5,"The whole warren, all at once.",
                new[]{ W2(1,2),W2(3,5),W2(4,0),W2(5,5) },
                new[]{ new EntDef(4,1, 4,2), new EntDef(3,3, 5,3), new EntDef(3,0, 4,3), new EntDef(5,1, 0,2) },
                holes: new[]{ W2(1,0),W2(4,4),W2(1,4),W2(5,2) }),

            // ===================== CHAPTER 7 =====================
            new Lv(5,5,4,"The big one is fast asleep. It only moves if somebody bumps it.",
                new Vector2Int[0],
                new[]{ new EntDef(4,0, 3,2), new EntDef(4,1, 4,4), new EntDef(3,0, 2,0) },
                heavy: 1),
            new Lv(5,5,4,"Push the big one - it slides until something stops it.",
                new[]{ W2(0,1) },
                new[]{ new EntDef(0,3, 3,4), new EntDef(3,0, 2,0), new EntDef(0,4, 4,1) },
                heavy: 1),
            new Lv(5,5,4,"The big one makes an excellent wall.",
                new[]{ W2(0,3) },
                new[]{ new EntDef(0,4, 0,2), new EntDef(3,0, 0,0), new EntDef(1,4, 1,3) },
                heavy: 1),
            new Lv(5,5,4,"Bump it once and it's somewhere new for good.",
                new[]{ W2(1,4) },
                new[]{ new EntDef(0,4, 3,2), new EntDef(4,2, 4,4), new EntDef(1,1, 2,3) },
                heavy: 1),
            new Lv(5,5,4,"Push it out of the way before you need the space.",
                new[]{ W2(1,0),W2(1,1) },
                new[]{ new EntDef(4,0, 0,1), new EntDef(4,1, 3,3), new EntDef(2,1, 2,4) },
                heavy: 2),
            new Lv(5,5,4,"You always stop right behind whatever you push.",
                new[]{ W2(1,1),W2(3,4) },
                new[]{ new EntDef(3,1, 1,3), new EntDef(0,3, 0,1), new EntDef(4,3, 4,4) },
                heavy: 2),
            new Lv(6,6,5,"Line up behind the big one to move it a long way.",
                new[]{ W2(0,4),W2(3,2) },
                new[]{ new EntDef(4,4, 4,0), new EntDef(1,5, 0,5), new EntDef(2,2, 0,0) },
                heavy: 1),
            new Lv(6,6,5,"It can be pushed into its own bed, too.",
                new[]{ W2(1,2),W2(1,4) },
                new[]{ new EntDef(2,4, 0,5), new EntDef(2,5, 0,1), new EntDef(1,0, 0,0) },
                heavy: 2),
            new Lv(6,6,5,"Push it once too often and it's in the way.",
                new[]{ W2(2,4),W2(3,4),W2(5,3) },
                new[]{ new EntDef(2,2, 0,2), new EntDef(0,1, 3,5), new EntDef(5,0, 2,5) },
                heavy: 0),
            new Lv(6,6,5,"The big one is blocking the beds behind it.",
                new[]{ W2(4,0),W2(4,1) },
                new[]{ new EntDef(3,1, 3,4), new EntDef(0,2, 0,5), new EntDef(0,0, 5,5) },
                heavy: 0),
            new Lv(6,6,5,"Decide where it has to end up first.",
                new[]{ W2(0,1),W2(1,4),W2(2,3) },
                new[]{ new EntDef(2,2, 3,2), new EntDef(2,1, 5,0), new EntDef(0,2, 5,5) },
                heavy: 0),
            new Lv(6,6,5,"Two pushes, if you have room for two.",
                new[]{ W2(0,0),W2(2,4),W2(5,5) },
                new[]{ new EntDef(2,0, 2,2), new EntDef(5,2, 5,3), new EntDef(1,1, 0,1), new EntDef(3,3, 3,1) },
                heavy: 1),
            new Lv(6,6,5,"The big one never moves on its own. Ever.",
                new[]{ W2(0,3),W2(0,4),W2(4,2) },
                new[]{ new EntDef(5,4, 0,2), new EntDef(5,3, 0,0), new EntDef(1,5, 0,5), new EntDef(2,4, 1,1) },
                heavy: 2),
            new Lv(6,6,5,"One push, then everybody home.",
                new[]{ W2(2,5),W2(3,3),W2(3,4),W2(5,5) },
                new[]{ new EntDef(4,1, 2,0), new EntDef(5,3, 5,4), new EntDef(1,4, 2,1), new EntDef(1,5, 0,4) },
                heavy: 1),
            new Lv(7,7,5,"The heaviest sleeper in the zoo, and four friends around it.",
                new[]{ W2(2,5),W2(3,5),W2(4,0),W2(5,2),W2(6,3) },
                new[]{ new EntDef(5,5, 4,5), new EntDef(2,2, 0,4), new EntDef(4,1, 5,1), new EntDef(5,6, 3,1) },
                heavy: 2),

            // ===================== CHAPTER 8 =====================
            new Lv(5,5,3,"No new rules tonight. Everything you already know.",
                new Vector2Int[0],
                new[]{ new EntDef(3,3, 1,0), new EntDef(4,0, 0,1) },
                rugs: new[]{ W2(3,0) },
                honey: new[]{ W2(1,3) }),
            new Lv(5,5,3,"Rug and honey in one room. Read the floor.",
                new[]{ W2(3,4) },
                new[]{ new EntDef(4,3, 2,2), new EntDef(1,2, 4,2) },
                rugs: new[]{ W2(3,0) },
                honey: new[]{ W2(2,3) }),
            new Lv(5,5,3,"The silk carries, the honey stops.",
                new[]{ W2(4,3) },
                new[]{ new EntDef(1,2, 0,1), new EntDef(0,4, 3,0) },
                rugs: new[]{ W2(1,0) },
                honey: new[]{ W2(1,3) }),
            new Lv(5,5,3,"Same rules, less room.",
                new[]{ W2(0,3) },
                new[]{ new EntDef(1,2, 4,2), new EntDef(1,4, 3,2), new EntDef(1,0, 2,2) },
                rugs: new[]{ W2(2,0) },
                honey: new[]{ W2(3,4) }),
            new Lv(5,5,6,"Take one animal at a time in your head.",
                new[]{ W2(2,4),W2(4,2) },
                new[]{ new EntDef(4,4, 0,1), new EntDef(1,4, 2,2), new EntDef(3,4, 1,0) },
                rugs: new[]{ W2(0,4) },
                honey: new[]{ W2(2,1) }),
            new Lv(5,5,4,"The floor is telling you the answer.",
                new[]{ W2(0,2),W2(1,0) },
                new[]{ new EntDef(2,4, 1,3), new EntDef(1,4, 4,4), new EntDef(3,2, 4,0) },
                rugs: new[]{ W2(2,1) },
                honey: new[]{ W2(3,3) }),
            new Lv(6,6,4,"You've solved harder than this - twice.",
                new[]{ W2(0,3),W2(2,3) },
                new[]{ new EntDef(1,0, 5,2), new EntDef(1,2, 0,4), new EntDef(4,3, 5,4) },
                rugs: new[]{ W2(4,0) },
                honey: new[]{ W2(4,4) }),
            new Lv(6,6,4,"Slow down. Everything here is familiar.",
                new[]{ W2(1,5),W2(3,0) },
                new[]{ new EntDef(4,5, 0,3), new EntDef(1,4, 0,2), new EntDef(5,4, 4,4) },
                rugs: new[]{ W2(3,5) },
                honey: new[]{ W2(4,3) }),
            new Lv(6,6,4,"One awkward friend, as always.",
                new[]{ W2(0,1),W2(3,0),W2(3,5) },
                new[]{ new EntDef(5,1, 4,2), new EntDef(4,0, 2,3), new EntDef(2,4, 4,4) },
                rugs: new[]{ W2(0,4),W2(1,1) },
                honey: new[]{ W2(5,2) }),
            new Lv(6,6,7,"Set the room up, then send everyone home.",
                new[]{ W2(3,2),W2(4,3),W2(5,3) },
                new[]{ new EntDef(5,4, 4,2), new EntDef(0,4, 1,3), new EntDef(3,3, 1,4), new EntDef(2,2, 2,1) },
                rugs: new[]{ W2(0,0),W2(4,4) },
                honey: new[]{ W2(2,3) }),
            new Lv(6,6,5,"The last few nights are the quiet ones.",
                new[]{ W2(0,1),W2(0,3),W2(5,4) },
                new[]{ new EntDef(3,0, 0,0), new EntDef(2,1, 4,3), new EntDef(3,3, 5,3), new EntDef(0,5, 3,2) },
                rugs: new[]{ W2(2,3),W2(4,4) },
                honey: new[]{ W2(1,2) }),
            new Lv(6,6,5,"Nearly the end of the zoo.",
                new[]{ W2(1,2),W2(1,4),W2(4,2) },
                new[]{ new EntDef(2,2, 2,0), new EntDef(0,0, 1,5), new EntDef(3,5, 5,1), new EntDef(4,4, 4,0) },
                rugs: new[]{ W2(2,1),W2(4,5) },
                honey: new[]{ W2(3,3) }),
            new Lv(6,6,5,"Second to last. Enjoy it.",
                new[]{ W2(0,2),W2(0,3),W2(5,3) },
                new[]{ new EntDef(1,4, 4,4), new EntDef(1,0, 5,5), new EntDef(4,0, 1,2), new EntDef(5,1, 1,3) },
                rugs: new[]{ W2(0,1),W2(5,4) },
                honey: new[]{ W2(5,2) }),
            new Lv(6,6,5,"One more after this one.",
                new[]{ W2(0,5),W2(2,1),W2(4,4),W2(5,0) },
                new[]{ new EntDef(3,3, 5,4), new EntDef(2,0, 5,1), new EntDef(2,4, 3,5), new EntDef(2,3, 2,5) },
                rugs: new[]{ W2(3,4),W2(4,2) },
                honey: new[]{ W2(3,1) }),
            new Lv(6,6,5,"Goodnight, everybody. Sleep well.",
                new[]{ W2(0,0),W2(3,0),W2(3,1),W2(4,0) },
                new[]{ new EntDef(5,4, 4,2), new EntDef(0,2, 0,3), new EntDef(1,0, 1,4), new EntDef(1,1, 3,4) },
                rugs: new[]{ W2(4,1),W2(5,3) },
                honey: new[]{ W2(2,0),W2(4,4) }),
        };

        // ---- warm, flat cozy palette (everything sits in the same family) ----
        private static readonly Color NightTop   = new Color(0.15f,0.12f,0.23f);
        private static readonly Color NightBottom = new Color(0.34f,0.24f,0.31f);

        // ---- one room per chapter -------------------------------------------------
        // Every chapter is somewhere else in the house, so progress is something you
        // SEE rather than a number you read. The board furniture never changes; only
        // the sky, the moon and the hills behind it do.
        //
        // These are painted in code in the same flat style as everything else (see
        // BgGradient). The photo-real room photographs in Resources/Art are from an
        // older art direction and would fight this one, so they're deliberately unused.
        private class Room
        {
            public string name;
            public Color skyTop, skyMid, skyHorizon;   // vertical gradient, bottom-up
            public Color hillFar, hillMid, hillNear;   // three silhouette layers
            public Vector2 moon; public float moonSize;
            public Color glow;                         // twinkle colour: stars, fireflies, snow
            public int glowCount;                      // how busy the sky is
            public Room(string name, Color t, Color m, Color hz, Color hf, Color hm, Color hn,
                        Vector2 moon, float moonSize, Color glow, int glowCount)
            { this.name=name; skyTop=t; skyMid=m; skyHorizon=hz; hillFar=hf; hillMid=hm;
              hillNear=hn; this.moon=moon; this.moonSize=moonSize; this.glow=glow; this.glowCount=glowCount; }
        }
        private static Color C(float r,float g,float b)=>new Color(r,g,b);
        private static readonly Room[] Rooms =
        {
            // 1 - the nursery: the warmest, softest sky in the game. Home.
            new Room("The nursery",   C(0.13f,0.11f,0.24f), C(0.26f,0.18f,0.33f), C(0.47f,0.29f,0.38f),
                     C(0.32f,0.22f,0.35f), C(0.24f,0.16f,0.29f), C(0.17f,0.11f,0.22f),
                     new Vector2(0.76f,0.84f), 0.052f, C(1f,0.98f,0.92f), 170),
            // 2 - the treehouse: leaves and moss creep into the night
            new Room("The treehouse", C(0.09f,0.14f,0.20f), C(0.15f,0.24f,0.28f), C(0.31f,0.36f,0.30f),
                     C(0.20f,0.28f,0.26f), C(0.14f,0.21f,0.20f), C(0.09f,0.15f,0.15f),
                     new Vector2(0.24f,0.86f), 0.046f, C(0.86f,1f,0.82f), 150),
            // 3 - the meadow: a wide open summer night, thick with fireflies
            new Room("The meadow",    C(0.08f,0.12f,0.26f), C(0.16f,0.21f,0.38f), C(0.42f,0.32f,0.44f),
                     C(0.24f,0.27f,0.40f), C(0.16f,0.20f,0.31f), C(0.10f,0.14f,0.22f),
                     new Vector2(0.70f,0.88f), 0.060f, C(1f,0.95f,0.70f), 200),
            // 4 - the snow cabin: the coldest, quietest room. Fewer, bigger flakes.
            new Room("The snow cabin",C(0.11f,0.15f,0.28f), C(0.19f,0.26f,0.40f), C(0.44f,0.49f,0.61f),
                     C(0.30f,0.36f,0.48f), C(0.21f,0.26f,0.37f), C(0.14f,0.18f,0.27f),
                     new Vector2(0.20f,0.82f), 0.058f, C(1f,1f,1f), 130),
            // 5 - the pantry: lamplight, honey and warm wood
            new Room("The pantry",    C(0.18f,0.11f,0.16f), C(0.32f,0.19f,0.20f), C(0.55f,0.36f,0.26f),
                     C(0.38f,0.24f,0.22f), C(0.28f,0.17f,0.17f), C(0.19f,0.11f,0.12f),
                     new Vector2(0.78f,0.80f), 0.050f, C(1f,0.88f,0.66f), 120),
            // 6 - the garden: deep green, dew, things moving in the dark
            new Room("The garden",    C(0.07f,0.13f,0.16f), C(0.12f,0.22f,0.24f), C(0.28f,0.36f,0.32f),
                     C(0.18f,0.28f,0.26f), C(0.12f,0.20f,0.19f), C(0.07f,0.13f,0.13f),
                     new Vector2(0.30f,0.88f), 0.044f, C(0.80f,1f,0.90f), 175),
            // 7 - the library: dusty violet, hushed, the lamp turned low
            new Room("The library",   C(0.14f,0.10f,0.20f), C(0.24f,0.17f,0.30f), C(0.40f,0.28f,0.36f),
                     C(0.29f,0.21f,0.33f), C(0.21f,0.15f,0.25f), C(0.14f,0.10f,0.18f),
                     new Vector2(0.72f,0.86f), 0.040f, C(0.96f,0.90f,1f), 140),
            // 8 - under the stars: the darkest sky, the most stars, the last night
            new Room("Under the stars",C(0.05f,0.06f,0.16f), C(0.10f,0.11f,0.26f), C(0.26f,0.20f,0.38f),
                     C(0.18f,0.16f,0.32f), C(0.12f,0.10f,0.23f), C(0.07f,0.06f,0.15f),
                     new Vector2(0.50f,0.90f), 0.066f, C(1f,1f,1f), 260),
        };
        private static Room RoomFor(int chapter)=>Rooms[Mathf.Clamp(chapter,0,Rooms.Length-1)];
        private static readonly Color BoardCream  = new Color(0.99f,0.93f,0.82f);
        private static readonly Color TileCream   = new Color(1.00f,0.965f,0.89f);
        private static readonly Color TileShadow  = new Color(0.90f,0.82f,0.70f);
        private static readonly Color WallWood    = new Color(0.80f,0.62f,0.44f);
        private static readonly Color BedNest     = new Color(0.95f,0.87f,0.73f);
        private static readonly Color BedRing     = new Color(0.86f,0.74f,0.56f);
        private static readonly Color Brown       = new Color(0.36f,0.22f,0.12f);
        // the chapter toys, each a different material so they never read as the same thing
        private static readonly Color RugSilk     = new Color(0.62f,0.70f,0.92f);
        private static readonly Color HoneyDark   = new Color(0.78f,0.52f,0.16f);
        private static readonly Color HoneyGold   = new Color(0.99f,0.78f,0.30f);
        private static readonly Color HoleDark    = new Color(0.16f,0.12f,0.18f);
        private static readonly Color[] HolePair  =
        { new Color(0.60f,0.86f,0.72f), new Color(0.86f,0.70f,0.95f), new Color(0.95f,0.78f,0.55f) };

        // ---- runtime ----
        private int _levelIndex;
        // Tonight's Puzzle runs on the same board, the same solver and the same feel —
        // it just isn't part of the map. While this is true the level index means
        // nothing, so every line that saves progress has to check it first.
        private bool _daily;
        private Lv _lv;
        private Camera _cam;
        private readonly HashSet<Vector2Int> _walls = new();
        // every wall's sprites, by cell, so one can be tidied away at runtime
        private readonly Dictionary<Vector2Int,List<Transform>> _wallTiles = new();
        private Vector2Int[] _pos, _bed;
        private string[] _pet;
        private Transform[] _view;
        private Vector3[] _target;
        // per-animal feel: is it still sliding, how far it's going, how squashed it is
        // right now, whether it's already tucked in, and a random breathing offset so
        // the room doesn't pulse in unison.
        private bool[] _moving, _wasAsleep;
        private int[] _travel;
        private float[] _squash, _phase;
        private int _landsThisMove;          // cap the thumps so a 5-animal swipe isn't a drum roll
        private bool _hapticThisMove;
        private readonly Stack<Vector2Int[]> _undo = new();
        private Transform _bgTf;
        private bool _sticky;                       // chapter 2 on: beds catch and hold
        private bool _anyBed;                       // chapter 3: any animal, any bed
        private readonly List<SpriteRenderer> _bedGlow = new();  // pulsed while sticky

        // ---- power-ups ----
        // Armed by the rail at the bottom, then spent on the next thing the player
        // taps. `_anchored` lives for exactly one swipe.
        //
        // Using one costs NO stars, deliberately. An earlier version capped a helped
        // level at two, which sounds fair and plays badly: it turns every power-up
        // into a "should I?" decision, and hesitancy is exactly the feeling this game
        // is supposed to remove. Supply is the balance instead — you get one play per
        // friend per day and that is it, so nothing here can be spammed.
        private int _armed = -1;        // ChonkyMerge.PowerUps.Kind, or -1 for none
        private int _anchored = -1;     // entity held still for the next swipe only

        private int _moves, _stars;
        private float _levelTime;
        private bool _showHint;
        private List<Vector2Int> _hintPath;   // optimal remaining swipes, computed when a hint is opened
        private Vector2 _swipeStart;
        private bool _swiping;
        private bool _solved;
        // The win panel used to appear the instant the last animal was logically home —
        // covering the board before it had finished sliding there. The whole payoff of
        // the game is watching the room go quiet, so the panel now waits for everyone
        // to land, holds a beat, and fades in over the top.
        private float _winAt, _winFade;

        // on-board guidance arrow (used for both the tutorial nudge and hints)
        private Transform _arrowTf;
        private SpriteRenderer _arrowSr;
        private bool _arrowOn;
        private Vector2Int _arrowDir;
        private Color _arrowTint;
        private bool _isTutorial;             // first level of a chapter: guided first-swipe demo
        private Vector2Int _tutorialDir;      // the helpful first swipe to demonstrate
        private float _tipTime;               // brief per-level teaching tip fades out
        private int _chapter;                 // which chapter this level belongs to
        private static string TaughtKey(int chapter) =>
            chapter == 0 ? "zoo_tutorial_done" : "zoo_taught_ch" + chapter;

        // UI
        private Texture2D _dimTex;
        private readonly List<Rect> _uiRects = new();

        public static int LevelCount => Levels.Length;
        public static int MaxStars => Levels.Length * 3;

        // ---- chapters ----
        // Chapter 2 rewrites the sliding rule. Everything the menu shows about it
        // stays vague until it's unlocked — the surprise IS the reward.
        // Chapters aren't all the same length. The first two run 20 levels because
        // they were built that way and they earn it; every chapter after them is 15,
        // which is about as long as one new idea stays interesting.
        private static readonly int[] ChapterStart = { 0, 20, 40, 55, 70, 85, 100, 115 };
        public static int ChapterCount => ChapterStart.Length;
        public static int ChapterOf(int level)
        {
            for (int c = ChapterStart.Length - 1; c >= 0; c--) if (level >= ChapterStart[c]) return c;
            return 0;
        }
        public static int ChapterFirstLevel(int chapter) =>
            ChapterStart[Mathf.Clamp(chapter, 0, ChapterStart.Length - 1)];
        public static int ChapterLastLevel(int chapter)
        {
            chapter = Mathf.Clamp(chapter, 0, ChapterStart.Length - 1);
            int end = chapter + 1 < ChapterStart.Length ? ChapterStart[chapter + 1] - 1 : Levels.Length - 1;
            return Mathf.Min(end, Levels.Length - 1);
        }
        private static Rule RuleFor(int chapter) => Rules[Mathf.Clamp(chapter, 0, Rules.Length - 1)];
        public static string ChapterName(int chapter) => RuleFor(chapter).name;
        // Shown on the locked chapter card. Teases the change without spoiling it.
        public static string ChapterTease(int chapter) => RuleFor(chapter).tease;
        public static string ChapterBlurb(int chapter) => RuleFor(chapter).blurb;
        // Every rule question routes through the table, so the game, the solver, the
        // hints and the UI can never drift apart about what tonight's rules are.
        public static bool StickyBeds(int level) => RuleFor(ChapterOf(level)).sticky;
        public static bool AnyBed(int level) => RuleFor(ChapterOf(level)).anyBed;

        // The redesigned map and home screen name the room you're heading into and
        // preview the shape of the level before you tap it. Both read from the same
        // tables the game itself uses, so the map can never promise a board the
        // puzzle doesn't deliver.
        public static string RoomName(int chapter) => RoomFor(chapter).name;
        public static int ParOf(int level) =>
            Levels[Mathf.Clamp(level, 0, Levels.Length - 1)].par;
        public static int AnimalCount(int level) =>
            Levels[Mathf.Clamp(level, 0, Levels.Length - 1)].ents.Length;

        // 3 stars is always the BFS-optimal par. The 2-star window is deliberately
        // generous - half the par again, minimum 3 spare swipes - because pars are
        // short now (2-12) and real play always costs a few exploratory swipes.
        // A par-10 level still pays 2 stars at 15 moves, so nobody gets gate-locked
        // for thinking out loud on the board.
        public static int TwoStarMoves(int par) => par + Mathf.Max(3, Mathf.RoundToInt(par / 2f));

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
        // Stars are read constantly — the level picker asks about every level, and the
        // zoo asks for the running total once per animal, every frame. Going to
        // PlayerPrefs for each of those meant ~34,000 lookups AND ~34,000 string
        // allocations per frame on the picker, which is a stutter you can feel and a
        // phone you can warm your hands on. It also got three times worse the moment
        // the game went from 40 levels to 130.
        //
        // So the whole star table lives in memory, and PlayerPrefs is only touched when
        // it actually changes. Anything that writes stars behind this cache's back MUST
        // call ReloadProgress() — scene loads do it automatically.
        private static int[] _starCache;
        private static int _starTotal;

        public static void ReloadProgress()
        {
            _starCache = new int[Levels.Length];
            _starTotal = 0;
            for (int i = 0; i < Levels.Length; i++)
            {
                _starCache[i] = PlayerPrefs.GetInt("zoo_stars_" + i, 0);
                _starTotal += _starCache[i];
            }
        }

        public static int StarsFor(int i)
        {
            if (_starCache == null) ReloadProgress();
            return (uint)i < (uint)_starCache.Length ? _starCache[i] : 0;
        }

        public static int TotalStars()
        {
            if (_starCache == null) ReloadProgress();
            return _starTotal;
        }

        /// The one place stars are written. Keeps the cache and PlayerPrefs in step.
        private static void SetStars(int i, int stars)
        {
            if (_starCache == null) ReloadProgress();
            if ((uint)i >= (uint)_starCache.Length || stars <= _starCache[i]) return;
            _starTotal += stars - _starCache[i];
            _starCache[i] = stars;
            PlayerPrefs.SetInt("zoo_stars_" + i, stars);
        }
        // Total stars required to step past each checkpoint. Every 4 levels there's
        // a small one; the big one is the chapter door at level 21. The curve sits
        // just under 2 stars per cleared level, so a player averaging 2 stars walks
        // straight through and a player scraping 1s replays a couple of favourites.
        // Total stars needed to step past each checkpoint, built rather than typed:
        // a small gate every 4 levels, and a bigger one on each chapter door. The
        // curve sits at ~1.6 stars per level already cleared, so a player averaging
        // two walks straight through and a player scraping ones replays a couple.
        private static readonly int[] Gates = BuildGates();
        private static int[] BuildGates()
        {
            // one gate every 4 levels, plus a heavier one on every chapter door
            var need = new SortedDictionary<int, int>();
            for (int i = 4; i < 200; i += 4) need[i] = Mathf.RoundToInt(i * 1.55f);
            for (int c = 1; c < ChapterStart.Length; c++)
            {
                int i = ChapterStart[c];
                need[i] = Mathf.Max(need.TryGetValue(i, out var v) ? v : 0,
                                    Mathf.RoundToInt(i * 1.8f));
            }
            // never let a later gate ask for less than an earlier one — a checkpoint
            // that goes backwards reads as a bug even when it's harmless
            var g = new List<int>();
            int running = 0;
            foreach (var kv in need)
            {
                running = Mathf.Max(running, kv.Value);
                g.Add(kv.Key); g.Add(running);
            }
            return g.ToArray();
        }

        public static int RequiredStars(int i)
        {
            int need = 0;
            for (int g = 0; g < Gates.Length; g += 2) if (i >= Gates[g]) need = Gates[g + 1];
            return need;
        }
        // The star total that opens a whole chapter (0 for chapter 1).
        public static int ChapterRequiredStars(int chapter) => RequiredStars(ChapterFirstLevel(chapter));
        public static bool ChapterUnlocked(int chapter) =>
            chapter <= 0 || (TotalStars() >= ChapterRequiredStars(chapter)
                             && StarsFor(ChapterFirstLevel(chapter) - 1) > 0);
        public static bool IsUnlocked(int i)
        {
            if (i <= 0) return true;                       // tutorial always open
            if (StarsFor(i - 1) <= 0) return false;        // must clear the previous level
            return TotalStars() >= RequiredStars(i);       // …and clear the checkpoint
        }

        /// The menu sets this to ask for Tonight's Puzzle instead of the map. It's
        /// cleared the moment it's read, so backing out and hitting Play lands on the
        /// campaign, not last night's puzzle.
        public const string DailyRequestKey = "zoo_want_daily";

        private void Start()
        {
            ReloadProgress();          // a scene load is the one place prefs may have moved under us
            SetupCamera();
            // The menu's screenshot tour hands over to this scene so the store gets
            // pictures of the actual game, not just its menus.
            if(ShotArg("-shots")!=null){ StartCoroutine(ShotTour()); return; }
            bool wantDaily = PlayerPrefs.GetInt(DailyRequestKey,0)==1;
            if(wantDaily){ PlayerPrefs.SetInt(DailyRequestKey,0); PlayerPrefs.Save(); }
            if(wantDaily) LoadDaily();
            else LoadLevel(PlayerPrefs.GetInt("zoo_level",0));
        }

        // ---- screenshot tour (development only) ----
        private static string ShotArg(string flag)
        {
            var a=System.Environment.GetCommandLineArgs();
            for(int i=0;i<a.Length-1;i++) if(a[i]==flag) return a[i+1];
            for(int i=0;i<a.Length;i++) if(a[i]==flag) return "";
            return null;
        }

        /// One board per chapter, so the listing shows what each chapter's toy actually
        /// looks like — a screenshot of seven plain levels sells a game with one idea.
        private System.Collections.IEnumerator ShotTour()
        {
            string dir=ShotArg("-shots");
            if(string.IsNullOrEmpty(dir)) dir=".";
            // deliberately not the first level of any chapter: those run the guided
            // tutorial, which covers the board with arrows and instructions
            int[] picks   = { 2, 32, 47, 62, 78, 92, 108, 122 };
            string[] names= { "08_basics","09_sticky_beds","10_musical_beds","11_rugs",
                              "12_honey","13_burrows","14_heavy","15_long_night" };
            for(int i=0;i<picks.Length && i<names.Length;i++)
            {
                LoadLevel(picks[i]);
                yield return Shot(dir,names[i]);
            }
            LoadDaily();
            yield return Shot(dir,"16_tonight");

            // The win panel is the game's biggest moment and the tour never showed
            // it, so it was the one screen nobody could check. Force it: load a
            // level, declare it solved, and skip the fade.
            LoadLevel(62);
            _solved=true; _stars=2; _moves=_lv.par+1; _winFade=1f;
            yield return Shot(dir,"17_win");

            // and the version of it that carries a new arrival
            PlayerPrefs.SetInt("zoo_seen",0);
            yield return Shot(dir,"18_win_arrival");

            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        private System.Collections.IEnumerator Shot(string dir,string name)
        {
            for(int f=0;f<10;f++) yield return null;      // let the board settle and animate in
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,name+".png"),2);
            for(int f=0;f<14;f++) yield return null;      // and let the file land
        }

        /// Re-solves every shipped level with the GAME'S OWN simulation and solver,
        /// and reports any level whose stored par doesn't match.
        ///
        /// This matters because levels are generated by tools/gen_levels.py, which is
        /// a second, independent implementation of these rules. Checking the file
        /// against the Python sim only proves Python agrees with itself. This is the
        /// check that actually catches the two implementations drifting apart — run it
        /// from the editor (see Assets/Editor/LevelAudit.cs) after ANY rule change.
        public static string AuditAllLevels()
        {
            var probe=new GameObject("LevelAudit").AddComponent<PuzzleGame>();
            var report=new System.Text.StringBuilder();
            int bad=0;
            for(int i=0;i<Levels.Length;i++)
            {
                var lv=Levels[i];
                probe._lv=lv;
                probe._levelIndex=i;
                probe._sticky=StickyBeds(i);
                probe._anyBed=AnyBed(i);
                probe._walls.Clear(); foreach(var w in lv.walls) probe._walls.Add(w);
                int n=lv.ents.Length;
                probe._pos=new Vector2Int[n]; probe._bed=new Vector2Int[n];
                for(int e=0;e<n;e++)
                {
                    probe._pos[e]=new Vector2Int(lv.ents[e].x,lv.ents[e].y);
                    probe._bed[e]=new Vector2Int(lv.ents[e].bx,lv.ents[e].by);
                }
                var path=probe.SolveFrom(probe._pos);
                int got=path?.Count ?? -1;
                if(got!=lv.par)
                {
                    bad++;
                    report.AppendLine($"BAD level {i+1}: file says par {lv.par}, the game's own solver says {(got<0?"unsolvable":got.ToString())}");
                }
            }
            Destroy(probe.gameObject);
            report.AppendLine(bad==0
                ? $"OK - all {Levels.Length} levels re-solved by the game's own solver, every par matches."
                : $"{bad} of {Levels.Length} levels DISAGREE - the C# and Python rules have drifted apart.");
            return report.ToString();
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam==null){ var go=new GameObject("Main Camera"); go.tag="MainCamera"; _cam=go.AddComponent<Camera>(); }
            _cam.orthographic=true; _cam.transform.position=new Vector3(0,0,-10);
            _cam.clearFlags=CameraClearFlags.SolidColor; _cam.backgroundColor=NightBottom;
        }

        /// Which nightly puzzle tonight is. The pool is walked in order so consecutive
        /// nights never repeat, and it's driven purely by the date — so every player
        /// gets the same board without anything having to be fetched or agreed on.
        private static int TonightsIndex =>
            DailyCount==0 ? -1 : ((Nightly.Tonight % DailyCount) + DailyCount) % DailyCount;

        private void LoadDaily()
        {
            if(DailyCount==0){ LoadLevel(ResumeLevel()); return; }
            LoadBoard(Dailies[TonightsIndex], -1, true);
        }

        private void LoadLevel(int index) =>
            LoadBoard(Levels[Mathf.Clamp(index,0,Levels.Length-1)],
                      Mathf.Clamp(index,0,Levels.Length-1), false);

        private void LoadBoard(Lv lv, int index, bool daily)
        {
            CancelHint();                       // never let a previous level's hint land here
            foreach (Transform c in transform) Destroy(c.gameObject);
            _walls.Clear(); _undo.Clear();
            _moves=0; _solved=false; _stars=0; _levelTime=0f; _showHint=false; _swiping=false;
            _armed=-1; _anchored=-1;
            _arrowTf=null; _arrowSr=null; _arrowOn=false; _hintPath=null;

            _daily=daily;
            _levelIndex=index;
            _bedGlow.Clear();
            if(daily)
            {
                // Chapter one's rules, always. See Nightly for why that isn't negotiable.
                _sticky=false; _anyBed=false; _chapter=0; _isTutorial=false;
            }
            else
            {
                _sticky=StickyBeds(_levelIndex);
                _anyBed=AnyBed(_levelIndex);
                // The full "how to play" walkthrough shows on the FIRST level of each
                // chapter until it's been cleared once — chapter 2 changes the rule, so
                // it earns the same guided first swipe that level 1 gets.
                _chapter=ChapterOf(_levelIndex);
                _isTutorial=(_levelIndex==ChapterFirstLevel(_chapter)
                             && PlayerPrefs.GetInt(TaughtKey(_chapter),0)==0);
                PlayerPrefs.SetInt("zoo_level",_levelIndex); PlayerPrefs.Save();
            }
            _tipTime=0f;
            _lv=lv;
            _wallTiles.Clear();
            foreach (var w in _lv.walls) _walls.Add(w);

            int n=_lv.ents.Length;
            _pos=new Vector2Int[n]; _bed=new Vector2Int[n]; _pet=new string[n];
            _view=new Transform[n]; _target=new Vector3[n];
            _moving=new bool[n]; _wasAsleep=new bool[n]; _travel=new int[n];
            _squash=new float[n]; _phase=new float[n];
            for(int i=0;i<n;i++) _phase[i]=i*1.7f;      // stagger the breathing
            _landsThisMove=0; _hapticThisMove=false;

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
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=BgGradient(_chapter); sr.sortingOrder=-20;
            _bgTf=go.transform;
            // the camera clears to the room's own horizon colour, so any sliver the
            // background sprite doesn't cover still belongs to this room
            if(_cam!=null) _cam.backgroundColor=RoomFor(_chapter).skyHorizon;
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
                    // Registered by cell, because the Tidy up power-up has to be able
                    // to delete exactly one block's sprites later. Hunting for them by
                    // world position afterwards is guesswork.
                    var shadowT=Tile(CellToWorld(cell),0,TileShadow,RoundedTile(),0.92f);
                    var woodT=Tile(CellToWorld(cell)+new Vector3(0,0.04f,-0.1f),1,WallWood,RoundedTile(),0.80f);
                    _wallTiles[cell]=new List<Transform>{shadowT,woodT};
                }
                else
                {
                    Tile(CellToWorld(cell),0,TileCream,RoundedTile(),0.92f);
                }
            }

            // ---- this chapter's toy, drawn on top of the plain floor ----
            // Each reads as a different material so you can tell them apart at a
            // glance without a legend: silk shines, honey is thick and round, a
            // burrow is a hole with a dark middle.
            foreach(var c in _lv.rugs)
            {
                var t=Tile(CellToWorld(c)+new Vector3(0,0,-0.01f),1,RugSilk,RoundedTile(),0.86f);
                var sheen=Tile(CellToWorld(c)+new Vector3(0,0.06f,-0.02f),2,Color.white,SoftDisc(),0.52f);
                sheen.GetComponent<SpriteRenderer>().color=new Color(1f,1f,1f,0.34f);
            }
            foreach(var c in _lv.honey)
            {
                Tile(CellToWorld(c)+new Vector3(0,0,-0.01f),1,HoneyDark,SoftDisc(),0.94f);
                var top=Tile(CellToWorld(c)+new Vector3(0,0.03f,-0.02f),2,HoneyGold,SoftDisc(),0.74f);
                top.GetComponent<SpriteRenderer>().color=HoneyGold;
            }
            for(int k=0;k<_lv.holes.Length;k++)
            {
                var c=_lv.holes[k];
                // The two ends of a pair share a colour AND a number of pebbles on the
                // rim: one pebble, two, three. Colour alone excluded roughly one man in
                // twelve from reasoning about where a burrow goes — which turns a puzzle
                // into a guess for them. The pebbles carry the same information without
                // needing colour vision at all.
                int pair=(k/2)%HolePair.Length;
                Color rim=HolePair[pair];
                Tile(CellToWorld(c)+new Vector3(0,0,-0.01f),1,rim,SoftDisc(),0.90f);
                var mouth=Tile(CellToWorld(c)+new Vector3(0,-0.02f,-0.02f),2,HoleDark,SoftDisc(),0.60f);
                mouth.GetComponent<SpriteRenderer>().color=HoleDark;

                int marks=pair+1;
                for(int m=0;m<marks;m++)
                {
                    float t = marks==1 ? 0f : (m/(float)(marks-1))*2f-1f;   // -1..1 across the rim
                    var at=CellToWorld(c)+new Vector3(t*0.22f, 0.30f-Mathf.Abs(t)*0.05f, -0.03f);
                    // A pale pebble on a pale rim was invisible at phone size. Dark core,
                    // light halo — it has to read at a glance or it isn't doing its job.
                    var halo=Tile(at+new Vector3(0,0,0.01f),3,Color.white,SoftDisc(),0.30f);
                    halo.GetComponent<SpriteRenderer>().color=new Color(1f,0.99f,0.94f,0.85f);
                    var dot=Tile(at,4,Color.white,SoftDisc(),0.19f);
                    dot.GetComponent<SpriteRenderer>().color=new Color(0.20f,0.15f,0.22f,0.95f);
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
            // In chapter 2 the beds are sticky, so they breathe — a standing visual
            // promise that this bed will grab its animal the moment it's touched.
            if(_sticky) _bedGlow.Add(halo.GetComponent<SpriteRenderer>());
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
            if(i==_lv.heavy) parentScale*=1.18f;
            go.transform.localScale=new Vector3(parentScale,parentScale,1f);

            // The heavy sleeper is drawn bigger and sits in a dark shadow, so "this one
            // isn't going anywhere on its own" is legible before you try to move it.
            //
            // The shadow MUST be wider than the signature glow below. The glow is drawn
            // on top of it, so a shadow the same size or smaller is completely invisible
            // — which is exactly what happened, and made the heavy animal look like
            // every other animal on the board.
            if(i==_lv.heavy)
            {
                var ring=new GameObject("HeavyRing"); ring.transform.SetParent(go.transform);
                // Sat behind the animal it just blends into a grey animal's silhouette,
                // so it's pushed down to the floor of the cell and squashed flat — a
                // shadow something is standing on, not a halo it's wearing.
                ring.transform.localPosition=new Vector3(0,-0.38f/parentScale,0.25f);
                var rsr=ring.AddComponent<SpriteRenderer>(); rsr.sprite=SoftDisc(); rsr.sortingOrder=4;
                rsr.color=new Color(0.24f,0.17f,0.24f,0.58f);
                float rs=1.34f/(parentScale*SoftDisc().bounds.size.x);
                ring.transform.localScale=new Vector3(rs,rs*0.30f,1f);   // flat: it's weight, not a halo
            }

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
            // The board is framed onto ONE rect, defined in design units by
            // BoardRect() — the same rect the chrome above and below is laid out
            // around. Previously the camera worked in margins and the UI worked in
            // screen fractions, so the two drifted apart on every new aspect ratio
            // and the board could end up under the buttons.
            //
            // Solve it once, exactly: pick the world-units-per-pixel that makes the
            // board panel fill the rect, then move the camera so the board's centre
            // lands on the rect's centre.
            TuckIn.Ui.Frame();
            var want = BoardRect();
            var px = TuckIn.Ui.R(want.x, want.y, want.width, want.height);

            const float pad = 0.62f;                 // the cream panel's overhang
            float boardUnits = Mathf.Max(_lv.w, _lv.h) + pad;
            float unitsPerPixel = boardUnits / Mathf.Max(1f, px.width);

            _cam.orthographicSize = Screen.height * 0.5f * unitsPerPixel;

            // GUI rects are top-down, Unity's screen space is bottom-up
            float cxPix = px.center.x, cyPix = Screen.height - px.center.y;
            _cam.transform.position = new Vector3(
                -(cxPix - Screen.width * 0.5f) * unitsPerPixel,
                -(cyPix - Screen.height * 0.5f) * unitsPerPixel,
                -10f);

            float aspect = Mathf.Max(0.3f, (float)Screen.width / Screen.height);

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
                if(i==_lv.heavy) b*=1.18f;               // the big one really is bigger
                // a tucked-in sleeper settles a little smaller, so "this one is done"
                // is readable without reading the board
                bool asleep=Asleep(_pos,i);
                if(asleep) b*=0.86f;
                _view[i].position=Vector3.Lerp(_view[i].position,_target[i],Time.deltaTime*16f);

                // Landing: the moment an animal actually arrives it squashes, thumps and
                // (in chapter 2) falls asleep. Doing it here rather than on the swipe means
                // the sound lands with the animal, not before it.
                if(_moving[i] && Vector3.Distance(_view[i].position,_target[i])<0.07f)
                {
                    _moving[i]=false;
                    _squash[i]=1f;
                    if(_landsThisMove<2){ Sfx.Land(_travel[i]); _landsThisMove++; }
                    if(!_hapticThisMove){ Haptics.Light(); _hapticThisMove=true; }
                    if(asleep && !_wasAsleep[i])
                    {
                        _wasAsleep[i]=true;
                        Sfx.Sleep(); Haptics.Soft();
                        StartCoroutine(SleepPuff(_target[i],PetCol(i)));
                    }
                }
                if(!asleep) _wasAsleep[i]=false;              // undo can wake them again

                // squash on impact, then a slow breath while they wait
                _squash[i]=Mathf.Max(0f,_squash[i]-Time.deltaTime*3.4f);
                float sq=_squash[i]*_squash[i];
                float breathe=asleep ? 0.020f : 0.012f;      // sleepers breathe deeper
                float pulse=1f+Mathf.Sin(Time.time*(asleep?1.1f:1.7f)+_phase[i])*breathe;
                var want=new Vector3(b*(1f+sq*0.20f)*pulse, b*(1f-sq*0.24f)*pulse, 1f);
                _view[i].localScale=Vector3.Lerp(_view[i].localScale,want,Time.deltaTime*18f);
            }
            PulseBeds();
            if (_solved)
            {
                // hold until nobody is still sliding, then a short beat, then fade up
                bool settled=true;
                for(int i=0;i<_moving.Length;i++) if(_moving[i]) settled=false;
                bool ready = settled && Time.time-_winAt>0.55f;
                _winFade=Mathf.MoveTowards(_winFade, ready?1f:0f, Time.deltaTime*3.2f);
                DriveArrow(); return;
            }
            _winFade=0f;
            _levelTime+=Time.deltaTime;
            _tipTime+=Time.deltaTime;
            DriveArrow();

            // Only read input while the app is actually in front — stops stray taps/swipes
            // from registering when the window is in the background.
            if (!Application.isFocused){ _swiping=false; return; }

            if (Input.GetMouseButtonDown(0))
            {
                // While a power-up is armed the board is a target picker, not a
                // swipe surface — otherwise choosing who to tuck in would also
                // count as a swipe in whatever direction the thumb drifted.
                if (_armed >= 0 && !PointerOverUI(Input.mousePosition))
                { TryUsePowerUp(Input.mousePosition); _swiping=false; return; }

                _swiping = !PointerOverUI(Input.mousePosition);
                _swipeStart=Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0) && _swiping)
            {
                _swiping=false;
                Vector2 d=(Vector2)Input.mousePosition-_swipeStart;
                if (d.magnitude>SwipeThreshold) DoMove(Dir(d));
            }

            if (Input.GetKeyDown(KeyCode.RightArrow)) DoMove(Vector2Int.right);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) DoMove(Vector2Int.left);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) DoMove(Vector2Int.up);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) DoMove(Vector2Int.down);
        }

        // Sticky beds glow in and out, and an animal that's already tucked in gets a
        // steady bright bed — so "asleep, and never moving again" reads at a glance.
        private void PulseBeds()
        {
            if(!_sticky) return;
            float pulse=0.5f+0.5f*Mathf.Sin(Time.unscaledTime*2.2f);
            for(int i=0;i<_bedGlow.Count && i<_pos.Length;i++)
            {
                var sr=_bedGlow[i]; if(sr==null) continue;
                var c=sr.color;
                float a = Asleep(_pos,i) ? 1f : 0.62f+0.30f*pulse;
                sr.color=new Color(c.r,c.g,c.b,a);
            }
        }

        // ---- spending a power-up ----
        /// Resolve a tap on the board into the thing the armed power-up acts on.
        /// A tap that lands on nothing valid simply disarms, which is the least
        /// annoying possible outcome: nothing is spent and nothing is lost.
        private void TryUsePowerUp(Vector2 screenPos)
        {
            var kind = (ChonkyMerge.PowerUps.Kind)_armed;
            var world = _cam.ScreenToWorldPoint(screenPos);
            if (!WorldToCell(world, out var cell)) { Disarm(); return; }

            switch (kind)
            {
                case ChonkyMerge.PowerUps.Kind.Pillow:
                {
                    int who = EntityAt(cell);
                    // an animal already asleep cannot move anyway, so holding it
                    // still would be spending a power-up on nothing
                    if (who < 0 || Asleep(_pos, who)) { Sfx.Locked(); return; }
                    if (!ChonkyMerge.PowerUps.Spend(kind)) { Disarm(); return; }
                    PushUndo();
                    _anchored = who;
                    Spent();
                    break;
                }
                case ChonkyMerge.PowerUps.Kind.Lullaby:
                {
                    int who = EntityAt(cell);
                    if (who < 0 || Asleep(_pos, who)) { Sfx.Locked(); return; }
                    // Their own bed has to be free — under Musical Beds any empty
                    // bed will do, which is exactly how that chapter already works.
                    var bed = FreeBedFor(who);
                    if (bed.x < 0) { Sfx.Locked(); return; }
                    if (!ChonkyMerge.PowerUps.Spend(kind)) { Disarm(); return; }
                    PushUndo();
                    _pos[who] = bed;
                    _target[who] = CellToWorld(bed);
                    _moving[who] = true; _travel[who] = 2;
                    Spent();
                    Sfx.Sleep();
                    CheckWin();
                    break;
                }
                default:
                {
                    if (!_walls.Contains(cell)) { Sfx.Locked(); return; }
                    if (!ChonkyMerge.PowerUps.Spend(kind)) { Disarm(); return; }
                    PushUndo();
                    _walls.Remove(cell);
                    RemoveWallSprite(cell);
                    Spent();
                    break;
                }
            }
        }

        private void Spent()
        {
            _armed = -1;
            CancelHint();
            _hintPath = null; _showHint = false; _arrowOn = false;
            _tipTime = 999f;
            ChonkyMerge.Haptics.Soft();
        }

        private void Disarm() { _armed = -1; Sfx.Click(); }

        private int EntityAt(Vector2Int cell)
        {
            for (int i = 0; i < _pos.Length; i++) if (_pos[i] == cell) return i;
            return -1;
        }

        /// Where a Lullaby can put this animal: its own bed normally, or any empty
        /// bed under Musical Beds. Returns (-1,-1) when there is nowhere to go.
        private Vector2Int FreeBedFor(int who)
        {
            if (!_anyBed)
                return EntityAt(_bed[who]) < 0 ? _bed[who] : new Vector2Int(-1, -1);
            for (int b = 0; b < _bed.Length; b++)
                if (EntityAt(_bed[b]) < 0) return _bed[b];
            return new Vector2Int(-1, -1);
        }

        /// The board is built from GameObjects, so tidying a block away has to take
        /// its sprite with it or the wall stays on screen after it stops existing.
        private void RemoveWallSprite(Vector2Int cell)
        {
            if (_wallTiles.TryGetValue(cell, out var tiles))
            {
                foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
                _wallTiles.Remove(cell);
            }
            // the cell is a plain floor tile now, not a hole in the board
            Tile(CellToWorld(cell), 0, TileCream, RoundedTile(), 0.92f);
            StartCoroutine(SleepPuff(CellToWorld(cell), new Color(0.95f, 0.85f, 0.70f)));
        }

        private bool PointerOverUI(Vector2 mouse)
        {
            Vector2 g=new Vector2(mouse.x, Screen.height-mouse.y); // GUI space
            foreach (var r in _uiRects) if (r.Contains(g)) return true;
            return false;
        }

        /// How far a finger has to travel before it counts as a swipe.
        ///
        /// This was a flat 28 pixels, which is fine on a laptop and far too twitchy on a
        /// phone: on a 1080-wide screen it's 2.6% of the width, so the small drift in an
        /// ordinary tap fires a move the player never asked for. Scaling it to the screen
        /// keeps it about a finger's width everywhere, and the floor keeps it sane in the
        /// tiny windows the screenshot tour uses.
        private static float SwipeThreshold => Mathf.Max(28f, Mathf.Min(Screen.width,Screen.height)*0.055f);

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
        //
        // Chapter 2 adds exactly two lines to this loop (both guarded by _sticky):
        // an animal already tucked in never moves again, and any animal that
        // touches its own bed mid-slide stops right there. That's the whole twist.
        private Vector2Int[] SlideSim(Vector2Int[] pos, Vector2Int dir)
        {
            int n=pos.Length;
            var np=(Vector2Int[])pos.Clone();
            var order=new int[n]; for(int i=0;i<n;i++) order[i]=i;
            System.Array.Sort(order,(a,b)=>(np[b].x*dir.x+np[b].y*dir.y).CompareTo(np[a].x*dir.x+np[a].y*dir.y));
            var occ=new HashSet<Vector2Int>(np);
            foreach(int i in order)
            {
                if(Asleep(np,i)) continue;                // tucked in — a soft wall now
                if(i==_anchored) continue;                // a Pillow is holding this one
                if(i==_lv.heavy) continue;                // too heavy to move on its own
                occ.Remove(np[i]);
                var p=Walk(np, i, np[i], dir, occ);
                // Heavy sleeper: if we stopped because the big one is in the way, shove
                // it along first and then keep going into the space it left.
                int guard=0;
                while(_lv.heavy>=0 && guard++<8)
                {
                    var ahead=p+dir;
                    if(!InBounds(ahead) || np[_lv.heavy]!=ahead || Asleep(np,_lv.heavy)) break;
                    occ.Remove(ahead);
                    var hp=Walk(np, _lv.heavy, ahead, dir, occ);
                    occ.Add(hp);
                    np[_lv.heavy]=hp;
                    if(hp==ahead) break;                  // it couldn't budge, so neither can we
                    p=Walk(np, i, p, dir, occ);
                }
                np[i]=p; occ.Add(p);
            }
            return np;
        }

        // Is entity i already tucked in? Under Musical Beds any bed will do, so an
        // animal is asleep if it's sitting on ANY bed; otherwise only its own counts.
        private bool Asleep(Vector2Int[] p, int i)
        {
            if(!_sticky) return false;
            if(!_anyBed) return p[i]==_bed[i];
            for(int b=0;b<_bed.Length;b++) if(p[i]==_bed[b]) return true;
            return false;
        }
        private bool IsBedFor(int i, Vector2Int c)
        {
            if(!_anyBed) return c==_bed[i];
            for(int b=0;b<_bed.Length;b++) if(c==_bed[b]) return true;
            return false;
        }
        private bool InBounds(Vector2Int c)=>c.x>=0&&c.x<_lv.w&&c.y>=0&&c.y<_lv.h;

        /// One animal's skid, from `from` until something stops it. This is the only
        /// place the board's toys are interpreted, so the game, the hint solver and
        /// the level generator can't disagree about what a swipe does.
        private Vector2Int Walk(Vector2Int[] np, int i, Vector2Int from, Vector2Int dir, HashSet<Vector2Int> occ)
        {
            var p=from;
            _trail.Clear(); _trail.Add(p);
            int guard=_lv.w*_lv.h*2+8;
            while(guard-->0)
            {
                var q=p+dir;
                if(!InBounds(q)||_walls.Contains(q)||occ.Contains(q)) break;
                p=q;
                // burrow: drop in one end, pop out of the other and keep going
                var exit=HoleExit(p);
                if(exit.HasValue && !occ.Contains(exit.Value) && !_walls.Contains(exit.Value))
                {
                    p=exit.Value;
                    _trail.Add(p);
                    if(_sticky && IsBedFor(i,p)) break;
                    if(IsHoney(p)) break;
                    continue;
                }
                _trail.Add(p);
                if(_sticky && IsBedFor(i,p)) break;        // caught by a bed
                if(IsHoney(p)) break;                      // stuck in the honey
            }
            // silk: you can cross a rug but never come to rest on one, so back up
            // along the way you came until the floor is solid again
            while(_trail.Count>1 && IsRug(p)) { _trail.RemoveAt(_trail.Count-1); p=_trail[_trail.Count-1]; }
            return p;
        }

        private readonly List<Vector2Int> _trail = new();
        private bool IsRug(Vector2Int c){ foreach(var r in _lv.rugs) if(r==c) return true; return false; }
        private bool IsHoney(Vector2Int c){ foreach(var r in _lv.honey) if(r==c) return true; return false; }
        // Burrows are stored in pairs: 0<->1, 2<->3, ...
        private Vector2Int? HoleExit(Vector2Int c)
        {
            var hs=_lv.holes;
            for(int k=0;k+1<hs.Length;k+=2)
            {
                if(hs[k]==c) return hs[k+1];
                if(hs[k+1]==c) return hs[k];
            }
            return null;
        }

        private void DoMove(Vector2Int dir)
        {
            if (_solved) return;
            var np=SlideSim(_pos,dir);
            bool changed=false; for(int i=0;i<np.Length;i++) if(np[i]!=_pos[i]) changed=true;
            // A Pillow is spent on the swipe it was placed for, even if that swipe
            // turns out to change nothing - otherwise it silently carries over into a
            // move the player never bought it for.
            _anchored=-1;
            if(!changed) return;

            PushUndo();
            _landsThisMove=0; _hapticThisMove=false;
            for(int i=0;i<np.Length;i++)
            {
                // remember how far this one is skidding: Update uses it to pitch the thump
                _travel[i]=Mathf.Abs(np[i].x-_pos[i].x)+Mathf.Abs(np[i].y-_pos[i].y);
                if(_travel[i]>0) _moving[i]=true;
                _pos[i]=np[i]; _target[i]=CellToWorld(np[i]);
            }
            CancelHint();                                     // a hint still thinking is now stale
            _hintPath=null; _showHint=false; _arrowOn=false;  // any move clears shown guidance
            _tipTime=999f;             // hide the teaching tip once they act
            _moves++; Sfx.Swipe(); CheckWin();
        }

        /// Three soft motes drifting up off an animal that's just fallen asleep — the
        /// visual half of chapter 2's payoff, so the moment reads even with sound off.
        private System.Collections.IEnumerator SleepPuff(Vector3 at, Color col)
        {
            var motes=new Transform[3];
            for(int i=0;i<motes.Length;i++)
            {
                var go=new GameObject("SleepPuff"); go.transform.SetParent(transform);
                go.transform.position=at+new Vector3(0.06f+i*0.10f,0.16f+i*0.05f,-0.6f);
                var sr=go.AddComponent<SpriteRenderer>();
                sr.sprite=SoftDisc(); sr.sortingOrder=20;
                sr.color=new Color(1f,1f,1f,0f);
                float s=(0.16f-i*0.03f)/SoftDisc().bounds.size.x;
                go.transform.localScale=new Vector3(s,s,1f);
                motes[i]=go.transform;
            }
            for(float t=0;t<1f;t+=Time.deltaTime*0.85f)
            {
                for(int i=0;i<motes.Length;i++)
                {
                    if(motes[i]==null) continue;
                    float lt=Mathf.Clamp01(t-i*0.18f);
                    var sr=motes[i].GetComponent<SpriteRenderer>();
                    sr.color=new Color(col.r*0.4f+0.6f,col.g*0.4f+0.6f,col.b*0.4f+0.6f,
                                       Mathf.Sin(lt*Mathf.PI)*0.75f);
                    motes[i].position+=new Vector3(0.06f,0.42f,0)*Time.deltaTime;
                }
                yield return null;
            }
            foreach(var m in motes) if(m!=null) Destroy(m.gameObject);
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
            if(!_anyBed)
            {
                for(int i=0;i<s.Length;i++) if(s[i]!=_bed[i]) return false;
                return true;
            }
            // Musical Beds: nobody minds whose bed is whose, so the level is done
            // when every bed has somebody in it.
            for(int b=0;b<_bed.Length;b++)
            {
                bool filled=false;
                for(int i=0;i<s.Length;i++) if(s[i]==_bed[b]){ filled=true; break; }
                if(!filled) return false;
            }
            return true;
        }

        // ---- the hint, solved across frames ----
        // The same search as SolveFrom, but it hands the frame back every so often.
        // The biggest boards reach ~88,000 states, and doing that in one go locks the
        // phone up for a second or two the moment somebody taps Hint — on the exact
        // levels where they're most likely to need it.
        private bool _hintSolving, _hintCancel;
        private const int HintStatesPerFrame = 2500;

        private System.Collections.IEnumerator SolveHint()
        {
            _hintSolving=true; _hintCancel=false; _hintPath=null;
            var start=_pos;
            List<Vector2Int> result=null;

            if(IsGoal(start)) result=new List<Vector2Int>();
            else
            {
                var came=new Dictionary<long,(long prev,int dir)>();
                var q=new Queue<Vector2Int[]>();
                came[StateKey(start)]=(-1,-1); q.Enqueue(start);
                long goal=-1; int budget=HintStatesPerFrame;
                while(q.Count>0 && !_hintCancel)
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
                    if(came.Count>300000) break;
                    if(--budget<=0){ budget=HintStatesPerFrame; yield return null; }
                }
                if(goal>=0 && !_hintCancel)
                {
                    result=new List<Vector2Int>();
                    long k=goal;
                    while(came[k].prev!=-1){ result.Add(AllDirs[came[k].dir]); k=came[k].prev; }
                    result.Reverse();
                }
            }

            _hintSolving=false;
            if(_hintCancel || _solved) yield break;
            _hintPath=result; _showHint=true;
        }

        /// Stop a hint that's still thinking — any move makes its answer stale.
        private void CancelHint(){ _hintCancel=true; _hintSolving=false; }

        // Breadth-first search back to the beds; returns the shortest swipe sequence.
        // Still used for the tutorial's demo swipe, where the board is tiny.
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
            CancelHint();                             // the board is about to change under it
            _hintPath=null; _showHint=false;
            var s=_undo.Pop();
            _landsThisMove=2; _hapticThisMove=true;    // undo is a quiet rewind, not a landing
            for(int i=0;i<_pos.Length;i++)
            {
                _travel[i]=Mathf.Abs(s[i].x-_pos[i].x)+Mathf.Abs(s[i].y-_pos[i].y);
                if(_travel[i]>0) _moving[i]=true;
                _pos[i]=s[i]; _target[i]=CellToWorld(s[i]);
            }
            if(_moves>0) _moves--; Sfx.Undo();
        }

        private void CheckWin()
        {
            if(!IsGoal(_pos)) return;
            _solved=true; _winAt=Time.time; _winFade=0f;
            _stars = _moves<=_lv.par ? 3 : (_moves<=TwoStarMoves(_lv.par) ? 2 : 1);
            if(_daily)
            {
                // Tonight's Puzzle pays nights, never stars. Stars open chapters, and a
                // daily puzzle that opened chapters would drag anyone who plays it past
                // the levels that teach the rules — and punish anyone who doesn't.
                Nightly.MarkDone();
                Dorm.EarnSnacks(Dorm.SnacksPerLevel);
                Haptics.Medium();
                StartCoroutine(WinFanfare(_stars));
                return;
            }
            SetStars(_levelIndex,_stars);          // keeps the in-memory star table in step
            // Snacks are affection, not progress: they buy nothing but a fed animal,
            // so paying them on every clear (including replays) can never unbalance
            // the star gates that actually pace the game.
            Dorm.EarnSnacks(Dorm.SnacksPerLevel);
            if(_isTutorial) PlayerPrefs.SetInt(TaughtKey(_chapter),1);     // never re-teach
            // remember the furthest level reached so Play resumes there
            int furthest=PlayerPrefs.GetInt("zoo_furthest",0);
            int nextLv=Mathf.Min(_levelIndex+1,Levels.Length-1);
            if(nextLv>furthest) PlayerPrefs.SetInt("zoo_furthest",nextLv);
            PlayerPrefs.Save();
            SaveGuard.Mirror();          // keep the backup copy level with the real save
            Haptics.Medium();
            StartCoroutine(WinFanfare(_stars));
        }

        /// The win sound, told as a little sequence: the animals settle, then each
        /// earned star rings a step higher, then the jingle. Rushing all of it into
        /// one frame is what makes puzzle games feel like slot machines.
        private System.Collections.IEnumerator WinFanfare(int stars)
        {
            yield return new WaitForSeconds(0.28f);
            for(int i=0;i<stars;i++){ Sfx.Star(i); yield return new WaitForSeconds(0.16f); }
            yield return new WaitForSeconds(0.10f);
            Sfx.Win();
        }

        // ---- UI ----
        // =====================================================================
        // 1e — THE BOARD
        // Thumb rail at the bottom, board in the sweet spot.
        // =====================================================================
        // Where the board sits, in design units. The camera is framed onto exactly
        // this rect (see FrameCamera), so the cream panel the player sees and the
        // world-space grid the game simulates are the same object — there is no
        // second layout to keep in sync.
        public static Rect BoardRect()
        {
            // The bottom of the play area is now the POWER-UP rail, not the thumb
            // rail — it sits 74 units above it, and the board must not slide under it.
            float railTop = TuckIn.Ui.H - 34f - 62f - 74f;
            float bandTop = 236f;
            float avail = railTop - 16f - bandTop;
            float size = Mathf.Min(322f, TuckIn.Ui.W - 68f, avail);
            // sits slightly high in the band, which is where a thumb wants it
            float top = bandTop + Mathf.Max(0f, avail - size) * 0.18f;
            return new Rect((TuckIn.Ui.W - size) * 0.5f, top, size, size);
        }

        private void OnGUI()
        {
            EnsureStyles();
            TuckIn.Ui.Frame();
            _uiRects.Clear();
            float H = TuckIn.Ui.H, W = TuckIn.Ui.W;

            // NOTE: nothing full-screen is drawn here on purpose. The night sky, the
            // moon and the hills are the room's own painted background, and they live
            // in WORLD space behind the board (see SpawnBackground/BgGradient). IMGUI
            // always draws on top of the camera, so painting the chrome's gradient
            // here hid the board completely — the one thing the screen is for.
            if (_solved) { DrawWinPanel(); return; }

            // ---- top row ----
            var backR = TuckIn.Ui.R(18, 58, 40, 40);
            _uiRects.Add(backR);
            if (TuckIn.Ui.GhostDisc(18, 58, 40, TuckIn.Icons.Chevron, 0.45f))
            { Sfx.Click(); SceneManager.LoadScene("MainMenu"); }

            string heading = _daily ? "Tonight" : $"Level {_levelIndex + 1}";
            GUI.Label(TuckIn.Ui.R(70, 58, W - 140, 22), heading,
                      TuckIn.Ui.Head(21, TuckIn.Ui.Hex(0xfff4e4)));
            string sub = _daily
                ? "Tonight's puzzle"
                : $"{ChapterName(_chapter)} · {RoomName(_chapter).ToLowerInvariant()}";
            GUI.Label(TuckIn.Ui.R(70, 78, W - 140, 16), sub,
                      TuckIn.Ui.Bold(11, new Color(1f, 0.925f, 0.816f, 0.55f)));

            var chip = TuckIn.Ui.Chip(W - 74, 58, 30, _lv.par.ToString(), TuckIn.Ui.StarTex,
                                      TuckIn.Ui.Snack, TuckIn.Ui.Hex(0xffe9bd),
                                      TuckIn.Ui.Ghost(0.13f), 13f);

            // ---- the move counter, as pips rather than a number to decode ----
            DrawMovePips(118f);

            // ---- the one teaching line, on its own soft strip ----
            string teach = null;
            if (_isTutorial && _moves == 0)
                teach = RuleFor(_chapter).taught;
            else
            {
                int intoChapter = _daily ? 1 : _levelIndex - ChapterFirstLevel(_chapter);
                if (intoChapter >= 1 && intoChapter <= 4 && _moves == 0 && _tipTime < 6f)
                    teach = _lv.hint;
            }
            if (!string.IsNullOrEmpty(teach))
            {
                var st = TuckIn.Ui.Bold(13, new Color(1f, 0.957f, 0.894f, 0.86f),
                                        TextAnchor.MiddleCenter, true);
                float th = Mathf.Max(46f, TuckIn.Ui.TextHeight(teach, st, W - 84) + 26f);
                TuckIn.Ui.RoundOutline(TuckIn.Ui.R(26, 170, W - 52, th), 28, 1,
                                       TuckIn.Ui.Hex(0xffe1be, 0.18f),
                                       new Color(1f, 0.957f, 0.894f, 0.09f));
                GUI.Label(TuckIn.Ui.R(42, 170, W - 84, th), teach, st);
            }

            // ---- the power-up rail ----
            float ry = H - 34f - 62f;
            DrawPowerUpRail(ry - 74f);

            // ---- the thumb rail: undo, the one primary, reset ----
            var rUndo = TuckIn.Ui.R(22, ry, 62, 62); _uiRects.Add(rUndo);
            if (TuckIn.Ui.GhostDisc(22, ry, 62, TuckIn.Icons.Undo, 0.36f)) Undo();

            var rReset = TuckIn.Ui.R(W - 84, ry, 62, 62); _uiRects.Add(rReset);
            if (TuckIn.Ui.GhostDisc(W - 84, ry, 62, TuckIn.Icons.Reset, 0.36f))
            { Sfx.Click(); if (_daily) LoadDaily(); else LoadLevel(_levelIndex); }

            // Hint pulses and speaks up after a struggle, but never nags before then.
            bool struggling = _moves >= Mathf.Max(TwoStarMoves(_lv.par), 5) || _levelTime >= 25f;
            string hlabel = _hintSolving ? "Thinking..." : _showHint ? "Hide hint"
                          : struggling ? "Need a hint?" : "Hint";
            var rHint = TuckIn.Ui.R(96, ry, W - 192, 62); _uiRects.Add(rHint);
            var prev = GUI.color;
            if (struggling && !_showHint && !_hintSolving)
                GUI.color = new Color(1f, 1f, 1f, 0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 4f));
            bool hit = TuckIn.Ui.Primary(96, ry, W - 192, 62, hlabel, 19f);
            GUI.color = prev;
            // the bulb rides inside the primary, left of its label
            var pc2 = GUI.color; GUI.color = TuckIn.Ui.PrimaryInk;
            float lw = TuckIn.Ui.Head(19, Color.white).CalcSize(new GUIContent(hlabel)).x / TuckIn.Ui.S;
            GUI.DrawTexture(TuckIn.Ui.R(96 + (W - 192) * 0.5f - lw * 0.5f - 26, ry + 18, 19, 19),
                            TuckIn.Icons.Bulb);
            GUI.color = pc2;
            if (hit)
            {
                Sfx.Click();
                if (_hintSolving) CancelHint();
                else if (_showHint) _showHint = false;
                else StartCoroutine(SolveHint());
            }

            // ---- the hint's caption, above the rail ----
            if (_showHint)
            {
                string cap;
                // With sticky beds a tucked-in animal can block the last friend, so a
                // dead end is possible — point at Undo, not a full restart.
                if (_hintPath == null) cap = _sticky ? "That corner's blocked now - tap Undo."
                                                     : "This one's tangled - tap Reset to start fresh.";
                else if (_hintPath.Count == 0) cap = "You're there - one more nudge!";
                else cap = $"Swipe {DirWord(_hintPath[0])}  ·  {_hintPath.Count} move"
                           + (_hintPath.Count == 1 ? "" : "s") + " to go";

                var st = TuckIn.Ui.Bold(13, TuckIn.Ui.Hex(0xfff4e4));
                float cw = Mathf.Min(W - 40f, st.CalcSize(new GUIContent(cap)).x / TuckIn.Ui.S + 36f);
                TuckIn.Ui.Round(TuckIn.Ui.R((W - cw) * 0.5f, ry - 126, cw, 40), 20,
                                TuckIn.Ui.Hex(0x1a1226, 0.86f));
                GUI.Label(TuckIn.Ui.R((W - cw) * 0.5f, ry - 126, cw, 40), cap, st);
            }
        }


        /// The three power-ups, always visible, always honest about their cost.
        ///
        /// They sit ABOVE the thumb rail rather than inside it, because Undo, Hint
        /// and Reset are free and reversible and these are neither — mixing them
        /// into the same row would make spending one feel like an accident.
        private void DrawPowerUpRail(float y)
        {
            float W = TuckIn.Ui.W;

            // While one is armed the rail becomes a single instruction: what to tap,
            // and how to back out. Nothing has been spent yet at this point.
            if (_armed >= 0)
            {
                var kind = (ChonkyMerge.PowerUps.Kind)_armed;
                var r = TuckIn.Ui.R(22, y, W - 44, 62);
                TuckIn.Ui.RoundOutline(r, 22, 2, TuckIn.Ui.Hex(0xffd166, 0.7f),
                                       TuckIn.Ui.Hex(0xffd166, 0.14f));
                GUI.Label(TuckIn.Ui.R(34, y + 10, W - 68 - 60, 20),
                          ChonkyMerge.PowerUps.Prompt(kind),
                          TuckIn.Ui.Bold(13, TuckIn.Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));
                GUI.Label(TuckIn.Ui.R(34, y + 32, W - 68 - 60, 18),
                          "Your stars are safe — this costs nothing",
                          TuckIn.Ui.Bold(10.5f, new Color(1f, 0.925f, 0.816f, 0.55f),
                                         TextAnchor.MiddleLeft));
                var cancel = TuckIn.Ui.R(W - 84, y + 11, 40, 40);
                _uiRects.Add(cancel);
                if (TuckIn.Ui.GhostDisc(W - 84, y + 11, 40, TuckIn.Icons.Close, 0.42f)) Disarm();
                _uiRects.Add(r);
                return;
            }

            float bw = (W - 44 - 20) / 3f;
            for (int i = 0; i < ChonkyMerge.PowerUps.Count; i++)
            {
                var kind = (ChonkyMerge.PowerUps.Kind)i;
                int have = ChonkyMerge.PowerUps.Have(kind);
                float x = 22 + i * (bw + 10);
                var r = TuckIn.Ui.R(x, y, bw, 62);
                _uiRects.Add(r);

                float a = have > 0 ? 1f : 0.4f;
                TuckIn.Ui.RoundOutline(r, 20, 1.5f,
                                       TuckIn.Ui.Hex(0xffe1be, 0.26f * a),
                                       TuckIn.Ui.Ghost(0.08f * a));

                var icon = ChonkyMerge.MainMenu.PowerUpIcon(kind);
                var pc = GUI.color;
                GUI.color = new Color(1f, 0.933f, 0.839f, a);
                GUI.DrawTexture(TuckIn.Ui.R(x + bw * 0.5f - 11, y + 9, 22, 22), icon);
                GUI.color = pc;
                GUI.Label(TuckIn.Ui.R(x, y + 34, bw, 16), ChonkyMerge.PowerUps.Name(kind),
                          TuckIn.Ui.Bold(11, new Color(1f, 0.933f, 0.839f, a)));

                // the count rides on the corner, gold when there is something to spend
                var badge = TuckIn.Ui.R(x + bw - 26, y - 8, 24, 20);
                TuckIn.Ui.Round(badge, 999, have > 0 ? TuckIn.Ui.StarLit
                                                     : new Color(1f, 0.93f, 0.84f, 0.16f));
                GUI.Label(badge, have.ToString(),
                          TuckIn.Ui.Bold(11, have > 0 ? TuckIn.Ui.Umber
                                                      : new Color(1f, 0.93f, 0.84f, 0.5f)));

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (have <= 0)
                    {
                        Sfx.Locked();
                        _noStockAt = Time.unscaledTime;
                    }
                    else { Sfx.Tap(); _armed = i; CancelHint(); _showHint = false; _arrowOn = false; }
                }
            }

            // one quiet line when they tap an empty shelf, pointing at where more
            // come from rather than at a shop
            if (Time.unscaledTime - _noStockAt < 2.6f)
                GUI.Label(TuckIn.Ui.R(22, y - 24, W - 44, 18),
                          "Play with your friends in the dorm to find more",
                          TuckIn.Ui.Bold(11.5f, TuckIn.Ui.Hex(0xffd166)));
        }

        private float _noStockAt = -99f;

        /// Moves as pips: gold for the ones you have spent, hollow for the ones that
        /// still buy a third star. A number tells you how many; this tells you how
        /// close you are without doing arithmetic in bed.
        private void DrawMovePips(float y)
        {
            int par = _lv.par;
            int shown = Mathf.Min(par, 12);
            float d = 11f, gap = 5f;
            var st = TuckIn.Ui.Bold(11, new Color(1f, 0.925f, 0.816f, 0.5f));
            float labelW = st.CalcSize(new GUIContent(TuckIn.Ui.Track("moves"))).x / TuckIn.Ui.S;
            string count = $"{_moves} / {par}";
            float countW = st.CalcSize(new GUIContent(count)).x / TuckIn.Ui.S;
            float pipsW = shown * d + (shown - 1) * gap;
            float total = labelW + 7 + pipsW + 7 + countW;
            float x = (TuckIn.Ui.W - total) * 0.5f;

            GUI.Label(TuckIn.Ui.R(x, y, labelW, 14), TuckIn.Ui.Track("moves"), st);
            x += labelW + 7;
            for (int i = 0; i < shown; i++)
                TuckIn.Ui.Circle(TuckIn.Ui.R(x + i * (d + gap), y + 1.5f, d, d),
                                 i < _moves ? TuckIn.Ui.StarLit
                                            : new Color(1f, 0.925f, 0.816f, 0.2f));
            x += pipsW + 7;
            GUI.Label(TuckIn.Ui.R(x, y, countW, 14), count,
                      TuckIn.Ui.Bold(11, _moves > par ? new Color(1f, 0.69f, 0.63f, 0.9f)
                                                      : new Color(1f, 0.925f, 0.816f, 0.5f)));
        }

        // =====================================================================
        // 1d — WIN + ARRIVAL
        // The payoff, one beat at a time.
        // =====================================================================
        private void DrawWinPanel()
        {
            // Nothing at all until the animals have settled — see _winFade. The whole
            // payoff of the game is watching the room go quiet.
            if (_winFade <= 0.01f) return;
            // Buttons stay untouchable until the panel is essentially solid, so a tap
            // meant for the board can't accidentally hit "Next level" as it fades in.
            bool live = _winFade > 0.85f;
            float H = TuckIn.Ui.H, W = TuckIn.Ui.W;
            float rise = (1f - _winFade) * 18f;

            var pc = GUI.color;
            GUI.color = new Color(1, 1, 1, _winFade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                TuckIn.Ui.VGrad(50, (0f, TuckIn.Ui.WinTop), (0.62f, TuckIn.Ui.WinMid),
                                    (1f, TuckIn.Ui.DormBot)), ScaleMode.StretchToFill);
            GUI.color = new Color(1, 1, 1, _winFade * 0.85f);
            TuckIn.Ui.Glow(TuckIn.Ui.R(W * 0.25f, H * 0.10f, W * 0.5f, H * 0.30f), 90f,
                           new Color(1f, 0.82f, 0.4f, 0.22f));
            GUI.color = new Color(1, 1, 1, _winFade);

            GUI.enabled = live;
            try
            {
                if (_daily) { DrawDailyWin(rise); return; }

                bool last = _levelIndex >= Levels.Length - 1;
                int next = _levelIndex + 1;
                bool nextOpen = !last && IsUnlocked(next);
                bool nextGated = !last && !nextOpen;
                bool chapterDone = !last && ChapterOf(next) > _chapter;

                // ---- the headline ----
                GUI.Label(TuckIn.Ui.R(0, 98 + rise, W, 18),
                          TuckIn.Ui.Track($"level {_levelIndex + 1} · {ChapterName(_chapter)}"),
                          TuckIn.Ui.Bold(11, new Color(1f, 0.925f, 0.816f, 0.55f)));
                GUI.Label(TuckIn.Ui.R(0, 122 + rise, W, 50),
                          chapterDone ? "Chapter" : "Everyone's",
                          TuckIn.Ui.Head(44, TuckIn.Ui.Hex(0xfff4e4)));
                GUI.Label(TuckIn.Ui.R(0, 168 + rise, W, 50),
                          chapterDone ? "complete" : "asleep",
                          TuckIn.Ui.Head(44, TuckIn.Ui.Hex(0xfff4e4)));

                // ---- the stars, the middle one raised and lit ----
                DrawWinStars(232 + rise);

                GUI.Label(TuckIn.Ui.R(0, 310 + rise, W, 20),
                          $"{_moves} move" + (_moves == 1 ? "" : "s") + $" · {_lv.par} for the third star",
                          TuckIn.Ui.Bold(13.5f, new Color(1f, 0.925f, 0.816f, 0.75f)));

                // ---- who moved in, or what changes next ----
                float cardY = 360 + rise;
                int arrived = Zoo.PendingArrival();
                float cardH;
                if (arrived >= 0) cardH = DrawArrivalStrip(cardY, arrived);
                else if (chapterDone) cardH = DrawNoteStrip(cardY, "next chapter",
                    $"Chapter {ChapterOf(next) + 1}", ChapterTease(ChapterOf(next)));
                else cardH = DrawNoteStrip(cardY, "the dorm", Zoo.NextLine(),
                    $"+{Dorm.SnacksPerLevel} snacks earned. Everyone's waiting up.");

                // ---- the progress bar toward the next friend ----
                DrawNextFriendBar(cardY + cardH + 18f);

                // ---- the one primary, and two quiet ways out ----
                float py = H - 100f - 58f;
                if (nextGated)
                {
                    // The next level can be shut by EITHER a star checkpoint or by
                    // simply not having been reached yet. Subtracting blindly printed
                    // "-46 to go" when the stars were already paid.
                    int need = RequiredStars(next) - TotalStars();
                    GUI.Label(TuckIn.Ui.R(22, py - 26, W - 44, 18),
                              need > 0
                                ? $"Level {next + 1} opens at {RequiredStars(next)} stars — {need} to go."
                                : $"Level {next + 1} is still ahead of you.",
                              TuckIn.Ui.Bold(12, new Color(1f, 0.925f, 0.816f, 0.6f)));
                    if (TuckIn.Ui.Primary(22, py, W - 44, 58, "Earn more stars", 24f))
                    { Sfx.Click(); LoadLevel(BestReplayLevel()); }
                }
                else if (TuckIn.Ui.Primary(22, py, W - 44, 58,
                         last ? "Play again" : chapterDone ? "See what changed" : "Next level", 24f))
                { Sfx.Click(); LoadLevel(last ? 0 : next); }

                float bw = (W - 44 - 11) / 2f;
                if (TuckIn.Ui.Outline(22, H - 32 - 46, bw, 46, "Visit the dorm", 14f))
                {
                    Sfx.Click();
                    PlayerPrefs.SetInt(MenuScreenKey, 1);   // open straight into the dorm
                    PlayerPrefs.Save();
                    SceneManager.LoadScene("MainMenu");
                }
                if (TuckIn.Ui.Outline(22 + bw + 11, H - 32 - 46, bw, 46,
                                      _stars >= 3 ? "Back to the map" : "Replay for ★★★", 14f))
                {
                    Sfx.Click();
                    if (_stars >= 3) SceneManager.LoadScene("MainMenu");
                    else LoadLevel(_levelIndex);
                }
            }
            finally { GUI.enabled = true; GUI.color = pc; }
        }

        /// Three stars, the middle one bigger, raised and glowing — the design's
        /// "one beat at a time" payoff. They ring in one by one (see _winFade).
        private void DrawWinStars(float y)
        {
            float W = TuckIn.Ui.W;
            float[] size = { 52, 64, 52 };
            float[] lift = { 0, -8, 0 };
            float total = 52 + 64 + 52 + 28;
            float x = (W - total) * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                bool on = i < _stars;
                var r = TuckIn.Ui.R(x, y + lift[i], size[i], size[i]);
                if (on && i == 1)
                    TuckIn.Ui.Glow(r, 16f, new Color(1f, 0.82f, 0.4f, 0.7f));
                TuckIn.Ui.StarShape(r, on ? (i == 1 ? TuckIn.Ui.StarLit : TuckIn.Ui.Star)
                                          : new Color(1f, 0.965f, 0.918f, 0.18f));
                x += size[i] + 14;
            }
        }

        /// "Someone moved in" — the reason stars are worth chasing, shown the moment
        /// they buy something rather than waiting for the player to go and look.
        private float DrawArrivalStrip(float y, int i)
        {
            float W = TuckIn.Ui.W;
            var pal = Zoo.Pals[i];
            TuckIn.Ui.RoundOutline(TuckIn.Ui.R(22, y, W - 44, 112), 28, 1.5f,
                                   TuckIn.Ui.Hex(0xffe1be, 0.24f),
                                   new Color(1f, 0.957f, 0.894f, 0.10f));

            float pulse = 6f + 3f * Mathf.Sin(Time.time * 2.4f);
            var tile = TuckIn.Ui.R(40, y + 18, 76, 76);
            TuckIn.Ui.RoundOutline(new Rect(tile.x - TuckIn.Ui.P(pulse), tile.y - TuckIn.Ui.P(pulse),
                                            tile.width + TuckIn.Ui.P(pulse * 2),
                                            tile.height + TuckIn.Ui.P(pulse * 2)),
                                   32, 2, TuckIn.Ui.Hex(0xffd166, 0.5f), new Color(0, 0, 0, 0f));
            TuckIn.Ui.RoundGrad(tile, 24, TuckIn.Ui.VGrad(21, (0f, TuckIn.Ui.Hex(0xffdca6)),
                                                              (1f, TuckIn.Ui.Hex(0xf2a95c))));
            var art = Zoo.Art(i);
            if (art != null)
            {
                float bob = Mathf.Sin(Time.time * 2.2f) * 3f;
                float aw = 58f, ah = aw * art.height / (float)art.width;
                GUI.DrawTexture(TuckIn.Ui.R(40 + (76 - aw) * 0.5f, y + 18 + (76 - ah) * 0.5f + bob, aw, ah), art);
            }

            GUI.Label(TuckIn.Ui.R(132, y + 22, W - 156, 16), TuckIn.Ui.Track("someone moved in"),
                      TuckIn.Ui.Bold(11, TuckIn.Ui.Hex(0xffd166), TextAnchor.MiddleLeft));
            GUI.Label(TuckIn.Ui.R(132, y + 42, W - 156, 26), $"{pal.name} is here",
                      TuckIn.Ui.Head(24, TuckIn.Ui.Hex(0xfff4e4), TextAnchor.MiddleLeft));
            GUI.Label(TuckIn.Ui.R(132, y + 66, W - 160, 32),
                      char.ToUpper(pal.dream[0]) + pal.dream.Substring(1) + ".",
                      TuckIn.Ui.Bold(12.5f, new Color(1f, 0.925f, 0.816f, 0.68f),
                                     TextAnchor.UpperLeft, true));
            return 112f;
        }

        /// The same strip, used when nobody arrived: an eyebrow, a headline and a
        /// line of detail. Keeps the win screen's rhythm identical every time.
        ///
        /// The headline is a whole sentence from Zoo.NextLine(), so it can be one
        /// line or three depending on whose turn it is to move in. It is MEASURED
        /// rather than assumed — a fixed 28-unit slot had "Marzipan moves in when
        /// you finish chapter 4." printing straight through the line underneath it.
        private float DrawNoteStrip(float y, string eyebrow, string headline, string detail)
        {
            float W = TuckIn.Ui.W;
            float textW = W - 84;

            var headSt = TuckIn.Ui.Head(21, TuckIn.Ui.Hex(0xfff4e4), TextAnchor.UpperLeft, true);
            var detSt = TuckIn.Ui.Bold(12.5f, new Color(1f, 0.925f, 0.816f, 0.68f),
                                        TextAnchor.UpperLeft, true);
            float headH = TuckIn.Ui.TextHeight(headline, headSt, textW);
            float detH = TuckIn.Ui.TextHeight(detail, detSt, textW);
            float h = Mathf.Max(112f, 20f + 16f + 6f + headH + 6f + detH + 20f);

            TuckIn.Ui.RoundOutline(TuckIn.Ui.R(22, y, W - 44, h), 28, 1.5f,
                                   TuckIn.Ui.Hex(0xffe1be, 0.24f),
                                   new Color(1f, 0.957f, 0.894f, 0.10f));
            GUI.Label(TuckIn.Ui.R(42, y + 20, textW, 16), TuckIn.Ui.Track(eyebrow),
                      TuckIn.Ui.Bold(11, TuckIn.Ui.Hex(0xffd166), TextAnchor.MiddleLeft));
            GUI.Label(TuckIn.Ui.R(42, y + 42, textW, headH), headline, headSt);
            GUI.Label(TuckIn.Ui.R(42, y + 42 + headH + 6, textW, detH), detail, detSt);
            return h;
        }

        private void DrawNextFriendBar(float y)
        {
            float W = TuckIn.Ui.W;
            int nextPal = Zoo.NextLocked();
            string who = nextPal < 0 ? "Everyone's home" : $"Next friend · {Zoo.Pals[nextPal].name}";
            int have = TotalStars();
            int need = MaxStars;
            if (nextPal >= 0 && Zoo.Pals[nextPal].how == Zoo.How.Stars) need = Zoo.Pals[nextPal].arg;
            else
            {
                // a chapter friend: measure against that chapter's own door instead,
                // so the bar always has a real, reachable end
                int ch = nextPal >= 0 ? Mathf.Min(Zoo.Pals[nextPal].arg + 1, ChapterCount - 1) : 0;
                need = Mathf.Max(have + 1, ChapterRequiredStars(ch));
            }

            GUI.Label(TuckIn.Ui.R(22, y, W - 130, 16), who,
                      TuckIn.Ui.Bold(11.5f, new Color(1f, 0.925f, 0.816f, 0.6f), TextAnchor.MiddleLeft));
            GUI.Label(TuckIn.Ui.R(W - 130, y, 108, 16), $"{have} / {need} ★",
                      TuckIn.Ui.Bold(11.5f, new Color(1f, 0.925f, 0.816f, 0.6f), TextAnchor.MiddleRight));
            TuckIn.Ui.Bar(22, y + 21, W - 44, 12, need <= 0 ? 1f : have / (float)need,
                          new Color(1f, 0.925f, 0.816f, 0.14f),
                          TuckIn.Ui.Hex(0xe08b46), TuckIn.Ui.Hex(0xffd166));
        }

        /// The end of Tonight's Puzzle. Deliberately a dead end: there is no "next",
        /// because the whole point is that tonight is finished and tomorrow is a
        /// separate, small pleasure. A daily puzzle with a "play another" button is
        /// just a level pack that resets your streak for fun.
        private void DrawDailyWin(float rise)
        {
            float H = TuckIn.Ui.H, W = TuckIn.Ui.W;
            int streak = Nightly.Streak;

            GUI.Label(TuckIn.Ui.R(0, 98 + rise, W, 18), TuckIn.Ui.Track("tonight's puzzle"),
                      TuckIn.Ui.Bold(11, new Color(1f, 0.925f, 0.816f, 0.55f)));
            GUI.Label(TuckIn.Ui.R(0, 128 + rise, W, 56), "Goodnight.",
                      TuckIn.Ui.Head(44, TuckIn.Ui.Hex(0xfff4e4)));

            DrawWinStars(232 + rise);

            GUI.Label(TuckIn.Ui.R(0, 310 + rise, W, 20),
                      $"{_moves} move" + (_moves == 1 ? "" : "s") + " · "
                      + (streak == 1 ? "1 night in a row" : $"{streak} nights in a row"),
                      TuckIn.Ui.Bold(13.5f, new Color(1f, 0.925f, 0.816f, 0.75f)));

            int lit = Nightly.Lanterns;
            DrawNoteStrip(360 + rise, "the dorm",
                lit >= Nightly.MaxLanterns ? "Every lantern is lit"
                                           : $"{lit} of {Nightly.MaxLanterns} lanterns lit",
                streak >= Nightly.BestStreak && streak > 1
                    ? "That's your longest run yet."
                    : "A new puzzle arrives tomorrow night.");

            // the lanterns themselves, so the reward is a thing you can point at
            DrawLanternRow(500 + rise);

            float py = H - 100f - 58f;
            if (TuckIn.Ui.Primary(22, py, W - 44, 58, "Back to the map", 24f))
            { Sfx.Click(); SceneManager.LoadScene("MainMenu"); }

            float bw = (W - 44 - 11) / 2f;
            if (TuckIn.Ui.Outline(22, H - 32 - 46, bw, 46, "Visit the dorm", 14f))
            {
                Sfx.Click();
                PlayerPrefs.SetInt(MenuScreenKey, 1);
                PlayerPrefs.Save();
                SceneManager.LoadScene("MainMenu");
            }
            if (TuckIn.Ui.Outline(22 + bw + 11, H - 32 - 46, bw, 46, "Keep playing", 14f))
            { Sfx.Click(); LoadLevel(ResumeLevel()); }
        }

        private void DrawLanternRow(float y)
        {
            int lit = Nightly.Lanterns, n = Nightly.MaxLanterns;
            float d = 12f, gap = 16f;
            float total = n * d + (n - 1) * gap;
            float x = (TuckIn.Ui.W - total) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var r = TuckIn.Ui.R(x + i * (d + gap), y, d, d);
                if (i < lit)
                {
                    TuckIn.Ui.Glow(r, 8f, new Color(1f, 0.78f, 0.36f, 0.45f));
                    TuckIn.Ui.Circle(r, TuckIn.Ui.StarLit);
                }
                else TuckIn.Ui.Circle(r, new Color(1f, 0.925f, 0.816f, 0.18f));
            }
        }

        /// Which screen the menu should open on. The win panel can send the player
        /// straight to the dorm, which is the only place the "+2 snacks" they just
        /// earned means anything.
        public const string MenuScreenKey = "menu_screen";

        // When a checkpoint blocks the next level, send the player to the earliest
        // level where they haven't earned 3 stars yet — the easiest place to top up.
        private int BestReplayLevel()
        {
            for(int i=0;i<=_levelIndex;i++) if(StarsFor(i)<3) return i;
            return _levelIndex;
        }

        /// The same idea, for the level picker: the earliest unlocked level that still
        /// has a star going spare. A locked gate should always be able to point at
        /// somewhere to go, rather than just saying no.
        public static int EasiestTopUpLevel()
        {
            for(int i=0;i<Levels.Length;i++)
            {
                if(!IsUnlocked(i)) break;
                if(StarsFor(i)<3) return i;
            }
            return 0;
        }

        // ---- generated sprites / textures ----
        private static Sprite _round;
        // one painted sky per chapter, built once and kept (they're 360x720 each)
        private static readonly Dictionary<int,Sprite> _bgRooms = new();
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
        private static Sprite BgGradient(int chapter)
        {
            if(_bgRooms.TryGetValue(chapter,out var cached) && cached!=null) return cached;
            var room=RoomFor(chapter);
            int w=360,h=720;
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var px=new Color[w*h];

            // sky palette (y=0 is the BOTTOM of the texture in Unity)
            var skyTop    = room.skyTop;
            var skyMid    = room.skyMid;
            var skyHorizon= room.skyHorizon;
            Vector2 moon=room.moon; float mr=room.moonSize;
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

            // The sky's twinkle: stars in the nursery, fireflies in the meadow, snow in
            // the cabin. Same loop, different colour and density per room — deterministic
            // scatter, denser and brighter high in the sky.
            var rng=new System.Random(20260726+chapter*977);
            for(int i=0;i<room.glowCount;i++)
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
                    px[ty*w+tx]=Color.Lerp(px[ty*w+tx],room.glow,a);
                }
            }

            // three hill layers, far (lightest) to near (darkest silhouette)
            DrawHills(px,w,h, 0.30f, 0.055f, 1.7f, 0.6f,  room.hillFar);
            DrawHills(px,w,h, 0.21f, 0.05f,  2.6f, 2.1f,  room.hillMid);
            DrawHills(px,w,h, 0.12f, 0.045f, 3.7f, 4.3f,  room.hillNear);

            var out32=new Color32[w*h];
            for(int i=0;i<px.Length;i++) out32[i]=px[i];
            tex.SetPixels32(out32); tex.Apply();
            var sprite=Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(0.5f,0.5f),1);
            _bgRooms[chapter]=sprite;
            return sprite;
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
        private void EnsureStyles()
        {
            if(_dimTex!=null) return;
            _dimTex=Texture2D.whiteTexture;
        }
    }
}
