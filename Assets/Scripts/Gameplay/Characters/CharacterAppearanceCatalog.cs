using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [CreateAssetMenu(menuName = "EachOneCoin/Characters/Appearance Catalog", fileName = "AppearanceCatalog")]
    public sealed class CharacterAppearanceCatalog : ScriptableObject
    {
        [SerializeField] private CharacterAppearanceProfile[] profiles = Array.Empty<CharacterAppearanceProfile>();

        public int Count => profiles != null ? profiles.Length : 0;
        public IReadOnlyList<CharacterAppearanceProfile> Profiles => profiles ?? Array.Empty<CharacterAppearanceProfile>();

        public CharacterAppearanceProfile GetProfile(int index)
        {
            if (profiles == null || index < 0 || index >= profiles.Length) return null;
            return profiles[index];
        }

        public int IndexOf(string appearanceId)
        {
            if (profiles == null || string.IsNullOrWhiteSpace(appearanceId)) return -1;
            string trimmed = appearanceId.Trim();
            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i];
                if (profile != null && string.Equals(profile.AppearanceId, trimmed, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        public int ResolveIndexOrDefault(string appearanceId, int fallbackIndex = 0)
        {
            int found = IndexOf(appearanceId);
            if (found >= 0) return found;
            int count = Count;
            if (count <= 0) return 0;
            if (fallbackIndex < 0 || fallbackIndex >= count) return 0;
            return fallbackIndex;
        }

        public string GetId(int index)
        {
            var profile = GetProfile(index);
            return profile != null && !string.IsNullOrEmpty(profile.AppearanceId) ? profile.AppearanceId : string.Empty;
        }

        public string GetDisplayName(int index)
        {
            var profile = GetProfile(index);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.DisplayName)) return profile.DisplayName;
            return index >= 0 ? "装扮 " + (index + 1) : string.Empty;
        }
    }
}
