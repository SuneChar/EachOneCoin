using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace EachOneCoin.Gameplay.Replay
{
    public static class ReplayStorage
    {
        public const int MaximumBytes = 16 * 1024 * 1024;
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "Replays");

        public static bool Validate(ReplayData data, string gameVersion, out string error)
        {
            error = null;
            if (data == null || !data.complete) error = "录像不完整";
            else if (data.formatVersion != ReplayData.CurrentFormat || data.gameVersion != gameVersion) error = "录像版本不兼容";
            else if (!Guid.TryParseExact(data.matchId, "N", out _) || !DateTime.TryParse(data.recordedUtc, out _) ||
                data.coinsToWin <= 0 || !Finite(data.duration) || data.duration <= 0f || data.frames == null ||
                data.frames.Length == 0 || data.frames.Length > 10000 || data.events == null || data.events.Length > 30000) error = "录像数据无效";
            else
            {
                float previous = -1f;
                int patrolCount = -1, spikeCount = -1;
                foreach (var frame in data.frames)
                {
                    if (frame == null || !Finite(frame.time) || frame.time < 0f || frame.time < previous ||
                        !Finite(frame.remaining) || frame.remaining < 0f || frame.remaining > data.duration + 0.1f ||
                        frame.matchStatus < 0 || frame.matchStatus > (int)MatchStatus.Finished ||
                        frame.participants == null || frame.participants.Length != 4 || frame.gridOwners == null || frame.gridOwners.Length != 441 ||
                        frame.coins == null || frame.coins.Length > 441 || frame.patrolPositions == null || frame.spikes == null ||
                        frame.patrolPositions.Length > 256 || frame.spikes.Length > 256 ||
                        (patrolCount >= 0 && patrolCount != frame.patrolPositions.Length) || (spikeCount >= 0 && spikeCount != frame.spikes.Length) ||
                        double.IsNaN(frame.mineElapsed) || double.IsInfinity(frame.mineElapsed) || frame.mineElapsed < 0 || frame.mineElapsed > data.duration + 0.1 ||
                        frame.nextMineEvent < 0 || frame.nextMineEvent > CoinSpawnSchedule.EventCount || frame.warningMine < -1 || frame.warningMine > 3 ||
                        frame.warningX == null || frame.warningY == null || frame.warningX.Length != frame.warningY.Length || frame.warningX.Length > CoinSpawnSchedule.CoinsPerEvent ||
                        (frame.warningMine < 0 && frame.warningX.Length != 0) || (frame.warningMine >= 0 && frame.warningX.Length == 0))
                    { error = "录像帧无效"; break; }
                    patrolCount = frame.patrolPositions.Length; spikeCount = frame.spikes.Length;
                    foreach (var p in frame.participants)
                        if (!Finite(p.position) || !Finite(p.facing) || !Finite(p.aimDirection) || !Finite(p.stamina) || p.stamina < 0 || p.status < 0 || p.status > (int)ParticipantStatus.Lost ||
                            p.health < 0 || p.maxHealth < 0 || p.health > p.maxHealth || p.coins < 0 || p.finishOrder < 0 || p.finishOrder > 4)
                            error = "录像参与者无效";
                    foreach (byte owner in frame.gridOwners) if (owner > 4) error = "录像格子无效";
                    var ids = new System.Collections.Generic.HashSet<uint>();
                    foreach (var coin in frame.coins)
                        if (coin.id == 0 || coin.kind > 1 || coin.x < 0 || coin.x >= 21 || coin.y < 0 || coin.y >= 21 || !ids.Add(coin.id)) error = "录像金币无效";
                    for (int i = 0; i < frame.warningX.Length; i++) if (frame.warningX[i] >= 21 || frame.warningY[i] >= 21) error = "录像警告无效";
                    foreach (var p in frame.patrolPositions) if (!Finite(p)) error = "录像障碍无效";
                    foreach (var spike in frame.spikes) if (!Finite(spike.height) || !Finite(spike.remainingFraction) || spike.remainingFraction < 0 || spike.remainingFraction > 1) error = "录像障碍无效";
                    if (error != null) break;
                    previous = frame.time;
                }
                if (error == null && (data.frames[0].time != 0f || data.frames[data.frames.Length - 1].matchStatus != (int)MatchStatus.Finished)) error = "录像缺少完整起止状态";
                float eventTime = -1f;
                int sequence = -1;
                foreach (var item in data.events)
                {
                    if (!Finite(item.time) || item.time < 0f || item.time < eventTime || item.time > previous || item.sequence <= sequence || !Finite(item.direction) || (item.type != "attack" && item.type != "feed"))
                    { error = "录像事件顺序无效"; break; }
                    eventTime = item.time;
                    sequence = item.sequence;
                }
            }
            return error == null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        public static bool TrySave(ReplayData data, out string path, out string error)
        {
            path = null;
            if (!Validate(data, Application.version, out error)) return false;
            string temp = null;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
                if (bytes.Length > MaximumBytes) { error = "录像文件过大"; return false; }
                Directory.CreateDirectory(DirectoryPath);
                string destination = Path.Combine(DirectoryPath, data.matchId + ".replay.json");
                temp = Path.Combine(DirectoryPath, data.matchId + "." + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllBytes(temp, bytes);
                // Never replace an existing recording; a duplicate ID is an error.
                File.Move(temp, destination);
                path = destination;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { error = "无法保存录像：" + e.Message; return false; }
            finally
            {
                if (temp != null) try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        public static bool TryLoad(string path, out ReplayData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 || file.Length > MaximumBytes) { error = "录像文件不存在或大小无效"; return false; }
                data = JsonUtility.FromJson<ReplayData>(File.ReadAllText(path, Encoding.UTF8));
                if (Validate(data, Application.version, out error)) return true;
                data = null;
                return false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { data = null; error = "无法读取录像：" + e.Message; return false; }
        }
    }
}
