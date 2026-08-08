using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// Things you can actually put in the room.
    ///
    /// "Decorate" used to shift a background gradient by a few percent, which is not
    /// decorating — the room stayed exactly the same room. This is the real thing: a
    /// short catalogue of objects, bought with snacks, each of which is DRAWN in the
    /// dorm at its own spot. Buy the rug and there is a rug on the floor.
    ///
    /// Rules that keep it from turning into a shop:
    ///
    ///   * ONE CURRENCY, EARNED BY PLAYING. Everything costs snacks, and snacks come
    ///     from clearing levels. There is nothing to buy with real money, no second
    ///     premium currency, and no item that is ever unavailable.
    ///
    ///   * BUYING IS PERMANENT AND ORDER-FREE. Items are never consumed, never
    ///     break, and can be bought in any order. The prices rise gently so the room
    ///     fills up over weeks rather than in one sitting.
    ///
    ///   * NOTHING IS BEHIND A FRIEND. A locked animal never blocks an item; the two
    ///     progressions run alongside each other so neither becomes a wall.
    /// </summary>
    public static class Decor
    {
        public class Item
        {
            public readonly string key, name, blurb;
            public readonly int price;
            public Item(string key, string name, int price, string blurb)
            { this.key = key; this.name = name; this.price = price; this.blurb = blurb; }
        }

        /// The catalogue, in the order it is shown and roughly the order most people
        /// will buy it. Prices are in snacks (2 per level cleared).
        public static readonly Item[] Items =
        {
            new Item("rug",     "Round rug",      10, "Something soft underfoot."),
            new Item("lamp",    "Corner lamp",    16, "Warm light after dark."),
            new Item("plant",   "Big leafy plant",24, "It is doing its best."),
            new Item("cushion", "Cushion pile",   30, "Nobody has ever tidied these."),
            new Item("picture", "Framed picture", 38, "A landscape nobody recognises."),
            new Item("toybox",  "Toy box",        46, "Somebody will empty this."),
            new Item("bunting", "Paper bunting",  56, "For no particular occasion."),
            new Item("clock",   "Sleepy clock",   66, "It is always about bedtime."),
            new Item("shelf",   "Little bookshelf",76,"Mostly picture books."),
            new Item("window",  "Window box",     88, "Flowers that never need watering."),
            new Item("mobile",  "Moon mobile",    102,"It turns very slowly."),
            new Item("stars",   "Star projector", 120,"Turns the ceiling into a sky."),
        };

        public static int Count => Items.Length;

        private static string Key(string k) => "decor_" + k;

        public static bool Owned(string key) => PlayerPrefs.GetInt(Key(key), 0) == 1;
        public static bool Owned(int i) => Owned(Items[i].key);

        public static int OwnedCount()
        {
            int n = 0;
            for (int i = 0; i < Items.Length; i++) if (Owned(i)) n++;
            return n;
        }

        /// Buys the item if there are enough snacks. Returns false and changes
        /// nothing otherwise, so the button can simply grey itself out.
        public static bool Buy(int i)
        {
            if (Owned(i)) return false;
            var it = Items[i];
            if (Dorm.Snacks < it.price) return false;
            Dorm.Snacks = Dorm.Snacks - it.price;
            PlayerPrefs.SetInt(Key(it.key), 1);
            PlayerPrefs.Save();
            return true;
        }

        /// The star projector is the one item that changes the whole room rather
        /// than sitting in a corner, so the dorm asks about it directly.
        public static bool Stars => Owned("stars");
        public static bool Lamp => Owned("lamp");
    }
}
