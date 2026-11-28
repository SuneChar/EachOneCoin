using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // Shared access to the touch stick preference so the gameplay overlay and both settings
    // pages (main menu and in-game) read and write the same value.
    public static class TouchSettings
    {
        private const string JoystickModeKey = "Touch.JoystickMode";

        // The device reports Android; Play Mode in the Editor reports the host platform, so the
        // Android build target is used as the preview switch there.
        public static bool TouchControlsAvailable
        {
            get
            {
                if (Application.platform == RuntimePlatform.Android) return true;
#if UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android;
#else
                return false;
#endif
            }
        }

        public static TouchJoystickMode JoystickMode
        {
            get
            {
                return PlayerPrefs.GetInt(JoystickModeKey, (int)TouchJoystickMode.Fixed) == (int)TouchJoystickMode.Floating
                    ? TouchJoystickMode.Floating
                    : TouchJoystickMode.Fixed;
            }
            set
            {
                PlayerPrefs.SetInt(JoystickModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string JoystickModeLabel(TouchJoystickMode mode)
        {
            return mode == TouchJoystickMode.Floating ? "摇杆：浮动" : "摇杆：固定";
        }
    }
}
