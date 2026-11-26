using System;
using System.Security.Cryptography;

namespace EachOneCoin.Networking
{
    // The server owns seat assignments. Reserved seats remain occupied until reclaimed or expired.
    public sealed class RoomState
    {
        public const int Capacity = 4;
        public const double ReconnectSeconds = 15d;
        private sealed class Seat { public int? Connection; public string Token; public bool Reserved; public double Deadline; }
        private readonly Seat[] seats = { new Seat(), new Seat(), new Seat(), new Seat() };
        public bool FillEmptyWithNpcs { get; private set; }

        public int Add(int connectionId)
        {
            for (int i = 0; i < Capacity; i++) if (seats[i].Connection == connectionId) return i;
            for (int i = 0; i < Capacity; i++)
                if (!IsOccupied(i)) { seats[i].Connection = connectionId; seats[i].Token = GenerateToken(); return i; }
            return -1;
        }

        public bool Reserve(int connectionId, double now)
        {
            int slot = SlotOf(connectionId);
            if (slot < 0 || slot == 0 || double.IsNaN(now) || double.IsInfinity(now)) return false;
            seats[slot].Connection = null;
            seats[slot].Reserved = true;
            if (seats[slot].Deadline <= 0d) seats[slot].Deadline = now + ReconnectSeconds;
            return true;
        }

        public int Reclaim(int connectionId, string token, double now)
        {
            if (string.IsNullOrEmpty(token) || double.IsNaN(now) || double.IsInfinity(now) || SlotOf(connectionId) >= 0) return -1;
            for (int i = 1; i < Capacity; i++)
            {
                var seat = seats[i];
                if (!seat.Reserved || now >= seat.Deadline || !TokenEquals(seat.Token, token)) continue;
                seat.Connection = connectionId; seat.Reserved = false;
                return i;
            }
            return -1;
        }

        public bool IsTokenConnected(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            for (int i = 1; i < Capacity; i++) if (seats[i].Connection.HasValue && TokenEquals(seats[i].Token, token)) return true;
            return false;
        }

        public bool IsExpired(int slot, double now) => slot > 0 && slot < Capacity && seats[slot].Reserved && now >= seats[slot].Deadline;
        public bool IsReclaimExpired(int slot, double now) => slot > 0 && slot < Capacity && seats[slot].Deadline > 0d && now >= seats[slot].Deadline;
        public double RemainingSeconds(int slot, double now) => slot > 0 && slot < Capacity && seats[slot].Deadline > 0d ? Math.Max(0d, seats[slot].Deadline - now) : 0d;
        public void CommitReclaim(int slot) { if (slot > 0 && slot < Capacity && seats[slot].Connection.HasValue) seats[slot].Deadline = 0d; }
        public bool IsReserved(int slot) => slot >= 0 && slot < Capacity && seats[slot].Reserved;
        public bool IsConnected(int slot) => slot >= 0 && slot < Capacity && seats[slot].Connection.HasValue;
        public string TokenAt(int slot) => slot >= 0 && slot < Capacity ? seats[slot].Token : null;

        public void Remove(int connectionId) { int slot = SlotOf(connectionId); if (slot >= 0) Release(slot); }
        public void Release(int slot)
        {
            if (slot < 0 || slot >= Capacity) return;
            seats[slot].Connection = null; seats[slot].Reserved = false; seats[slot].Deadline = 0; seats[slot].Token = null;
        }
        public bool IsOccupied(int index) => seats[index].Connection.HasValue || seats[index].Reserved;
        public int SlotOf(int connectionId)
        {
            for (int i = 0; i < Capacity; i++) if (seats[i].Connection == connectionId) return i;
            return -1;
        }
        public int? ConnectionAt(int index) => seats[index].Connection;
        public void SetNpcFill(bool enabled) { FillEmptyWithNpcs = enabled; }

        private static string GenerateToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        private static bool TokenEquals(string expected, string supplied)
        {
            if (expected == null || supplied == null || expected.Length != supplied.Length) return false;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ supplied[i];
            return difference == 0;
        }
    }
}
