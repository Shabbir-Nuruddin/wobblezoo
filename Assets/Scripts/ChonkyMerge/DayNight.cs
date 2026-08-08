using System;
using UnityEngine;

namespace ChonkyMerge
{
    /// <summary>
    /// What time it is in the dorm.
    ///
    /// The dorm used to look identical whether you opened it at breakfast or at
    /// midnight, which made it feel like a menu rather than a place. It now runs on
    /// the device clock: the window behind the beds shows the real sky, the lamps
    /// only matter after dark, and the animals get sleepy at bedtime.
    ///
    /// Two deliberate constraints:
    ///
    ///   * IT READS THE CLOCK, IT NEVER SETS A TIMER. Nothing in here counts down,
    ///     expires, or is missed. Open the game at 3am and you get a dark room and a
    ///     quiet greeting — not a penalty for having been away.
    ///
    ///   * IT NEEDS NO NETWORK. `DateTime.Now` is the device's own clock, so the
    ///     whole feature works on a plane, in a tunnel, or in a car with no signal.
    ///     A player who shifts their clock gets a different sky; that is their
    ///     business, because nothing here is a reward worth cheating for.
    /// </summary>
    public static class DayNight
    {
        public enum Phase { Dawn, Day, Dusk, Night }

        /// Hour of the day as a float, e.g. 13.5 for half past one.
        public static float Hour
        {
            get { var n = DateTime.Now; return n.Hour + n.Minute / 60f; }
        }

        public static Phase Now
        {
            get
            {
                float h = Hour;
                if (h >= 5f && h < 8f) return Phase.Dawn;
                if (h >= 8f && h < 17f) return Phase.Day;
                if (h >= 17f && h < 20f) return Phase.Dusk;
                return Phase.Night;
            }
        }

        public static string Greeting
        {
            get
            {
                switch (Now)
                {
                    case Phase.Dawn: return "Good morning";
                    case Phase.Day: return "Good afternoon";
                    case Phase.Dusk: return "Good evening";
                    default: return "Goodnight";
                }
            }
        }

        /// A short line about what the room is doing, shown under the title.
        public static string Mood
        {
            get
            {
                switch (Now)
                {
                    case Phase.Dawn: return "the sun is coming up";
                    case Phase.Day: return "everyone's awake";
                    case Phase.Dusk: return "the lamps are going on";
                    default: return "lights out";
                }
            }
        }

        /// 24-hour clock, e.g. "21:04". Deliberately not localised to 12-hour: this
        /// is decoration, not a widget, and one format keeps the layout stable.
        public static string Clock => DateTime.Now.ToString("HH:mm");

        /// True once it's properly dark. Lamps light up, animals yawn, and the room
        /// stops pretending it's the afternoon.
        public static bool IsDark => Now == Phase.Night;

        /// How lit the room is, 0 (pitch dark) to 1 (full daylight). Drives the
        /// ambient wash over the whole dorm.
        public static float Light
        {
            get
            {
                float h = Hour;
                if (h >= 8f && h < 17f) return 1f;
                if (h >= 5f && h < 8f) return Mathf.InverseLerp(5f, 8f, h);          // dawn
                if (h >= 17f && h < 20f) return 1f - Mathf.InverseLerp(17f, 20f, h); // dusk
                return 0f;
            }
        }

        // ---- the sky in the window ----
        // Two stops per phase, blended by Light, so the window is never a flat
        // rectangle of colour and dawn genuinely fades up into day.
        public static Color SkyTop
        {
            get
            {
                switch (Now)
                {
                    case Phase.Dawn: return Hex(0xf6b98a);
                    case Phase.Day: return Hex(0x7fc4e8);
                    case Phase.Dusk: return Hex(0x8b6aa8);
                    default: return Hex(0x16183a);
                }
            }
        }

        public static Color SkyBottom
        {
            get
            {
                switch (Now)
                {
                    case Phase.Dawn: return Hex(0xffe0b8);
                    case Phase.Day: return Hex(0xcfeaf7);
                    case Phase.Dusk: return Hex(0xe89b6a);
                    default: return Hex(0x2b2660);
                }
            }
        }

        /// The sun or the moon, and where it sits in the window (0..1 across, 0..1 down).
        public static bool ShowMoon => Now == Phase.Night;
        public static Vector2 OrbPos
        {
            get
            {
                float h = Hour;
                // sun tracks left-to-right across the day, moon across the night
                float t = ShowMoon
                    ? Mathf.Repeat((h - 20f) / 9f, 1f)
                    : Mathf.Clamp01((h - 5f) / 15f);
                return new Vector2(0.15f + t * 0.7f, 0.68f - Mathf.Sin(t * Mathf.PI) * 0.42f);
            }
        }

        private static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
