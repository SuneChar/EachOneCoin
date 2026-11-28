using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public static class CosmeticPreferenceStore
    {
        public const string DefaultAppearanceId = "slot_0";
        private const string KeyPrefix = "EachOneCoin.AppearanceId";

        private static string CurrentKey
        {
            get
            {
#if UNITY_EDITOR
                return BuildStorageKey(Application.dataPath, true);
#else
                return KeyPrefix;
#endif
            }
        }

        public static string LoadSelectedAppearanceId(string fallback = DefaultAppearanceId)
        {
            string resolvedFallback = string.IsNullOrWhiteSpace(fallback) ? DefaultAppearanceId : fallback.Trim();
            string stored = PlayerPrefs.GetString(CurrentKey, string.Empty);
            return string.IsNullOrWhiteSpace(stored) ? resolvedFallback : stored.Trim();
        }

        public static void SaveSelectedAppearanceId(string appearanceId)
        {
            if (string.IsNullOrWhiteSpace(appearanceId)) return;
            PlayerPrefs.SetString(CurrentKey, appearanceId.Trim());
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.Save();
        }

        internal static string BuildStorageKey(string dataPath, bool isolateByPath)
        {
            if (!isolateByPath || string.IsNullOrWhiteSpace(dataPath)) return KeyPrefix;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(dataPath.Trim().ToLowerInvariant()));
                var sb = new StringBuilder(KeyPrefix.Length + 1 + 16);
                sb.Append(KeyPrefix).Append('.');
                for (int i = 0; i < 8 && i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
