using System;

namespace Muhanok.Bootstrap
{
    // Command-line switches for tests, captures and recordings. They exist only in the Editor and development builds;
    // a release build ignores them, so a player cannot change how a run is recorded or which styles are allowed.
    public static class ToolingFlags
    {
        public const string Smoke = "--muhanok-smoke", UiCapture = "--muhanok-uicapture", Capture = "--muhanok-capture",
            Review = "--mongscape-review", Tests = "--mongscape-tests", Recording = "--mongscape-recording";

        // Developer (exhibition) mode and its operator keys: Editor and development builds only.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DeveloperModeAvailable => true;
#else
        public static bool DeveloperModeAvailable => false;
#endif

        public static bool Has(string flag)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
#else
            return false;
#endif
        }

        // Automated runs: keyboard style, no camera scan, no performance auto-tuning.
        public static bool Automated => Has(Smoke) || Has(UiCapture) || Has(Review) || Has(Tests) || Has(Capture) || Has(Recording);

        // Tool runs whose finished runs never reach the rankings.
        public static bool SkipsRecords => Has(Capture) || Has(Smoke) || Has(Review) || Has(Tests) || Has(UiCapture);
    }
}
