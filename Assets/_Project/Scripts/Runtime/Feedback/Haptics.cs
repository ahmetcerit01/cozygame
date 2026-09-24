#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CozyLab.Puzzle.Feedback
{
    /// <summary>
    /// Light-weight haptics. On iOS devices this calls UIFeedbackGenerator through the tiny native
    /// bridge in Plugins/iOS/CozyHaptics.mm. Everywhere else (Editor, other platforms) it is a no-op.
    /// </summary>
    public static class Haptics
    {
        public enum Impact
        {
            Light = 0,
            Medium = 1,
            Heavy = 2,
            Soft = 3,
            Rigid = 4,
        }

        public enum Notification
        {
            Success = 0,
            Warning = 1,
            Error = 2,
        }

        public static bool Enabled { get; set; } = true;

        public static void Selection()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) _CozyHapticsSelection();
#endif
        }

        public static void Play(Impact style)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) _CozyHapticsImpact((int)style);
#endif
        }

        public static void Play(Notification type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) _CozyHapticsNotify((int)type);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _CozyHapticsSelection();
        [DllImport("__Internal")] private static extern void _CozyHapticsImpact(int style);
        [DllImport("__Internal")] private static extern void _CozyHapticsNotify(int type);
#endif
    }
}
