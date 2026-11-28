using System;

namespace EachOneCoin.Gameplay
{
    public static class CosmeticAssignmentPolicy
    {
        public const byte UnspecifiedIndex = byte.MaxValue;

        public static byte SanitizeIndex(byte requested, int catalogCount, int fallbackSlot)
        {
            if (catalogCount <= 0) return 0;
            int safeCount = catalogCount < byte.MaxValue ? catalogCount : byte.MaxValue;
            if (requested < safeCount) return requested;
            int safeFallback = fallbackSlot >= 0 ? fallbackSlot % safeCount : 0;
            return (byte)safeFallback;
        }

        public static void ResolveMatchAppearances(
            int catalogCount,
            byte humanMask,
            byte visibleMask,
            byte[] humanRequested,
            byte[] output,
            bool allowDuplicates = true)
        {
            if (output == null || output.Length == 0) return;

            int safeCount = catalogCount <= 0 ? 0 : (catalogCount < byte.MaxValue ? catalogCount : byte.MaxValue);
            int slotCount = output.Length < 8 ? output.Length : 8;

            if (safeCount <= 0)
            {
                for (int i = 0; i < output.Length; i++) output[i] = 0;
                return;
            }

            ulong used0 = 0UL;
            ulong used1 = 0UL;
            ulong used2 = 0UL;
            ulong used3 = 0UL;

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = SanitizeIndex(UnspecifiedIndex, safeCount, i);
            }

            for (int i = 0; i < slotCount; i++)
            {
                int bit = 1 << i;
                if ((humanMask & bit) == 0) continue;

                byte requested = humanRequested != null && i < humanRequested.Length
                    ? humanRequested[i]
                    : UnspecifiedIndex;
                byte chosen = SanitizeIndex(requested, safeCount, i);

                if (!allowDuplicates && IsUsed(chosen, used0, used1, used2, used3))
                {
                    int unused = FindFirstUnused(safeCount, used0, used1, used2, used3);
                    if (unused >= 0) chosen = (byte)unused;
                }

                output[i] = chosen;
                MarkUsed(chosen, ref used0, ref used1, ref used2, ref used3);
            }

            for (int i = 0; i < slotCount; i++)
            {
                int bit = 1 << i;
                if ((visibleMask & bit) == 0 || (humanMask & bit) != 0) continue;

                int unused = FindFirstUnused(safeCount, used0, used1, used2, used3);
                byte chosen = unused >= 0 ? (byte)unused : SanitizeIndex(UnspecifiedIndex, safeCount, i);
                output[i] = chosen;
                MarkUsed(chosen, ref used0, ref used1, ref used2, ref used3);
            }
        }

        private static bool IsUsed(int index, ulong u0, ulong u1, ulong u2, ulong u3)
        {
            int shift = index & 63;
            switch (index >> 6)
            {
                case 0: return (u0 & (1UL << shift)) != 0UL;
                case 1: return (u1 & (1UL << shift)) != 0UL;
                case 2: return (u2 & (1UL << shift)) != 0UL;
                case 3: return (u3 & (1UL << shift)) != 0UL;
                default: return false;
            }
        }

        private static void MarkUsed(int index, ref ulong u0, ref ulong u1, ref ulong u2, ref ulong u3)
        {
            int shift = index & 63;
            switch (index >> 6)
            {
                case 0: u0 |= 1UL << shift; break;
                case 1: u1 |= 1UL << shift; break;
                case 2: u2 |= 1UL << shift; break;
                case 3: u3 |= 1UL << shift; break;
            }
        }

        private static int FindFirstUnused(int safeCount, ulong u0, ulong u1, ulong u2, ulong u3)
        {
            for (int i = 0; i < safeCount; i++)
            {
                if (!IsUsed(i, u0, u1, u2, u3)) return i;
            }
            return -1;
        }
    }
}
