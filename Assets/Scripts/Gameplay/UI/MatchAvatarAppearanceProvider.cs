using System;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class MatchAvatarAppearanceProvider : IAvatarAppearanceProvider, IDisposable
    {
        private const int MaxSlots = 4;

        private readonly CharacterAppearanceProfile[] cachedProfiles = new CharacterAppearanceProfile[MaxSlots];
        private readonly Color[] cachedColors = new Color[MaxSlots];
        private readonly AvatarAppearanceData[] cachedAppearances = new AvatarAppearanceData[MaxSlots];
        private readonly bool[] cacheValid = new bool[MaxSlots];

        private MatchCoordinator coordinator;
        private readonly Sprite defaultPortrait;
        private bool disposed;

        public event Action<int> AppearanceChanged;

        public MatchAvatarAppearanceProvider(MatchCoordinator coordinator, Sprite defaultPortrait = null)
        {
            this.coordinator = coordinator;
            this.defaultPortrait = defaultPortrait;

            if (this.coordinator != null)
            {
                this.coordinator.ParticipantAppearanceChanged += HandleParticipantAppearanceChanged;
            }
        }

        public AvatarAppearanceData GetAppearance(int slotId, Color fallbackColor)
        {
            CharacterAppearanceProfile profile = ResolveProfile(slotId);

            if (slotId >= 0 && slotId < MaxSlots)
            {
                if (cacheValid[slotId]
                    && ReferenceEquals(cachedProfiles[slotId], profile)
                    && ColorsApproximatelyEqual(cachedColors[slotId], fallbackColor))
                {
                    return cachedAppearances[slotId];
                }

                AvatarAppearanceData built = AvatarAppearanceData.FromProfile(profile, fallbackColor, defaultPortrait);
                cachedProfiles[slotId] = profile;
                cachedColors[slotId] = fallbackColor;
                cachedAppearances[slotId] = built;
                cacheValid[slotId] = true;
                return built;
            }

            return AvatarAppearanceData.FromProfile(profile, fallbackColor, defaultPortrait);
        }

        public void InvalidateSlot(int slotId)
        {
            if (slotId >= 0 && slotId < MaxSlots)
            {
                cacheValid[slotId] = false;
            }
            else
            {
                Array.Clear(cacheValid, 0, cacheValid.Length);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (coordinator != null)
            {
                coordinator.ParticipantAppearanceChanged -= HandleParticipantAppearanceChanged;
                coordinator = null;
            }

            AppearanceChanged = null;
            Array.Clear(cacheValid, 0, cacheValid.Length);
            Array.Clear(cachedProfiles, 0, cachedProfiles.Length);
        }

        private CharacterAppearanceProfile ResolveProfile(int slotId)
        {
            if (disposed || coordinator == null || slotId < 0 || slotId >= MaxSlots)
            {
                return null;
            }

            CharacterAppearanceProfile assigned = coordinator.GetParticipantAppearance(slotId);
            if (assigned != null)
            {
                return assigned;
            }

            ArenaCharacter[] characters = coordinator.Characters;
            if (characters != null && slotId < characters.Length && characters[slotId] != null)
            {
                ArenaCharacter character = characters[slotId];
                if (character.CurrentAppearanceProfile != null)
                {
                    return character.CurrentAppearanceProfile;
                }

                return character.GetFallbackProfile(slotId);
            }

            return null;
        }

        private void HandleParticipantAppearanceChanged(int slotId)
        {
            if (disposed)
            {
                return;
            }

            InvalidateSlot(slotId);
            AppearanceChanged?.Invoke(slotId);
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                && Mathf.Abs(a.g - b.g) < 0.002f
                && Mathf.Abs(a.b - b.b) < 0.002f
                && Mathf.Abs(a.a - b.a) < 0.002f;
        }
    }
}
