using System;
using System.Collections.Generic;

namespace EachOneCoin.Networking
{
    // The server is the only writer; empty slots remain available to humans even when NPC fill is enabled.
    public sealed class RoomState
    {
        public const int Capacity = 4;
        private readonly int?[] slots = new int?[Capacity];
        public bool FillEmptyWithNpcs { get; private set; }

        public int Add(int connectionId)
        {
            for (int i = 0; i < Capacity; i++) if (slots[i] == connectionId) return i;
            for (int i = 0; i < Capacity; i++)
                if (!slots[i].HasValue) { slots[i] = connectionId; return i; }
            return -1;
        }

        public void Remove(int connectionId)
        {
            for (int i = 0; i < Capacity; i++) if (slots[i] == connectionId) slots[i] = null;
        }

        public bool IsOccupied(int index) { return slots[index].HasValue; }
        public int SlotOf(int connectionId)
        {
            for (int i = 0; i < Capacity; i++) if (slots[i] == connectionId) return i;
            return -1;
        }
        public int? ConnectionAt(int index) { return slots[index]; }
        public void SetNpcFill(bool enabled) { FillEmptyWithNpcs = enabled; }
    }
}
