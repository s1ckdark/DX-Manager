using System;

namespace DexManager.Models
{
    public static class FrameRateRules
    {
        public const int ExperimentalMaxFps = 120;
        public static bool IsSupported(int fps) { return fps == 30 || fps == 60 || fps == ExperimentalMaxFps; }
        public static bool RequiresScreenOn(int fps) { return fps >= ExperimentalMaxFps; }

        public static void Normalize(int fps, ref bool screenOff, ref bool? previous)
        {
            if (RequiresScreenOn(fps))
            {
                // Compatibility with older experimental settings lacking a backup.
                previous = previous ?? true;
                screenOff = false;
            }
            else
            {
                screenOff = previous ?? screenOff;
                previous = null;
            }
        }

        public static void RequireConsent(int fps, bool confirmed)
        {
            if (RequiresScreenOn(fps) && !confirmed)
                throw new InvalidOperationException("120 FPS requires confirmation before every start.");
        }

        public static void Normalize(ScrcpySettings settings)
        {
            if (!IsSupported(settings.MaxFps)) settings.MaxFps = 60;
            var off = settings.TurnScreenOff;
            var previous = settings.TurnScreenOffBeforeHighFps;
            Normalize(settings.MaxFps, ref off, ref previous);
            settings.TurnScreenOff = off;
            settings.TurnScreenOffBeforeHighFps = previous;
        }

        public static void Normalize(SingleWindowSlotSettings settings)
        {
            if (!IsSupported(settings.MaxFps)) settings.MaxFps = 60;
            var off = settings.TurnScreenOff;
            var previous = settings.TurnScreenOffBeforeHighFps;
            Normalize(settings.MaxFps, ref off, ref previous);
            settings.TurnScreenOff = off;
            settings.TurnScreenOffBeforeHighFps = previous;
        }

        public static void Normalize(SingleWindowAppProfile settings)
        {
            if (!IsSupported(settings.MaxFps)) settings.MaxFps = 60;
            var off = settings.TurnScreenOff;
            var previous = settings.TurnScreenOffBeforeHighFps;
            Normalize(settings.MaxFps, ref off, ref previous);
            settings.TurnScreenOff = off;
            settings.TurnScreenOffBeforeHighFps = previous;
        }
    }
}
