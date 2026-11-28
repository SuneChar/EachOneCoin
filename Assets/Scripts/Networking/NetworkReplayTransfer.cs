using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EachOneCoin.Gameplay.Replay;
using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    // Lives on the persistent network manager, not on the arena scene.
    public sealed class NetworkReplayTransfer : MonoBehaviour
    {
        private const int ChunkSize = 4096;
        private LanNetworkManager manager;
        private byte[] receiving;
        private int received;
        private string expectedHash;

        public void Initialize(LanNetworkManager owner) { manager = owner; }

        public void RegisterClientHandlers()
        {
            NetworkClient.RegisterHandler<ReplayTransferBegin>(BeginReceive);
            NetworkClient.RegisterHandler<ReplayTransferChunk>(ReceiveChunk);
            NetworkClient.RegisterHandler<ReplayTransferEnd>(EndReceive);
        }

        public void ResetReceive() { receiving = null; received = 0; expectedHash = null; }

        public void SendCompleted(string path)
        {
            if (!NetworkServer.active || manager == null || !manager.MatchFinished) return;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0 || info.Length > ReplayStorage.MaximumBytes) return;
                byte[] bytes = File.ReadAllBytes(path);
                var recipients = new List<NetworkConnectionToClient>();
                foreach (var conn in NetworkServer.connections.Values)
                    if (conn != null && conn.connectionId != 0 && conn.isAuthenticated && manager.ServerSlot(conn.connectionId) >= 0)
                        recipients.Add(conn);
                if (recipients.Count > 0) StartCoroutine(Send(bytes, recipients));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Debug.LogWarning("录像分发失败：" + e.Message); }
        }

        private IEnumerator Send(byte[] bytes, List<NetworkConnectionToClient> recipients)
        {
            var begin = new ReplayTransferBegin { length = bytes.Length, sha256 = Hash(bytes) };
            foreach (var conn in recipients) if (Connected(conn)) conn.Send(begin);
            for (int offset = 0; offset < bytes.Length && NetworkServer.active; offset += ChunkSize)
            {
                int length = Math.Min(ChunkSize, bytes.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(bytes, offset, chunk, 0, length);
                var message = new ReplayTransferChunk { offset = offset, bytes = chunk };
                foreach (var conn in recipients) if (Connected(conn)) conn.Send(message);
                yield return null;
            }
            if (NetworkServer.active) foreach (var conn in recipients) if (Connected(conn)) conn.Send(new ReplayTransferEnd());
        }

        private static bool Connected(NetworkConnectionToClient conn)
            => conn != null && NetworkServer.connections.TryGetValue(conn.connectionId, out var current) && ReferenceEquals(conn, current) && conn.isAuthenticated;

        private void BeginReceive(ReplayTransferBegin message)
        {
            ResetReceive();
            if (NetworkServer.active || message.length <= 0 || message.length > ReplayStorage.MaximumBytes || message.sha256 == null || message.sha256.Length != 64) return;
            receiving = new byte[message.length]; expectedHash = message.sha256;
        }

        private void ReceiveChunk(ReplayTransferChunk message)
        {
            if (receiving == null) return;
            if (message.bytes == null || message.bytes.Length == 0 || message.bytes.Length > ChunkSize || message.offset != received || message.bytes.Length > receiving.Length - received)
            { ResetReceive(); return; }
            Buffer.BlockCopy(message.bytes, 0, receiving, received, message.bytes.Length);
            received += message.bytes.Length;
        }

        private void EndReceive(ReplayTransferEnd message)
        {
            if (receiving == null) return;
            try
            {
                if (received != receiving.Length || !string.Equals(Hash(receiving), expectedHash, StringComparison.Ordinal)) return;
                string json = new UTF8Encoding(false, true).GetString(receiving);
                var data = JsonUtility.FromJson<ReplayData>(json);
                if (!ReplayStorage.TrySave(data, out _, out var error)) Debug.LogWarning("接收录像失败：" + error);
            }
            catch (Exception e) when (e is ArgumentException || e is DecoderFallbackException)
            { Debug.LogWarning("接收录像无效：" + e.Message); }
            finally { ResetReceive(); }
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
