using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // Enabled explicitly with --movement-telemetry; never changes gameplay decisions.
    [DefaultExecutionOrder(10000)]
    public sealed class MovementTelemetry : MonoBehaviour
    {
        [Serializable]
        private sealed class Record
        {
            public int schema = 1;
            public string type, match_id, reason, scene, version, platform, status;
            public int actor, width, height;
            public float game_time, real_time, dt, move_speed, fov, camera_x, camera_y, camera_z;
            public float x, z, speed, input_x, input_z, distance, relative_speed, viewport_x, viewport_y, depth;
            public bool visible, valid_speed, valid_relative_speed, knocked_back, has_waypoint;
        }

        private const float SampleInterval = 0.1f;
        private const int BufferLimit = 512;
        private readonly List<string> buffer = new List<string>(BufferLimit);
        private MatchCoordinator match;
        private LocalPlayerInput input;
        private Camera view;
        private StreamWriter writer;
        private string matchId;
        private float gameTime, lastSample, lastFlush, startReal;
        private Vector3[] previous;
        private ParticipantStatus[] previousStatus;
        private bool[] knockbackSinceSample, invalidSinceSample;
        private bool paused, closed, dropped;
        private Vector3 lastInput;

        public void Initialize(MatchCoordinator coordinator, LocalPlayerInput playerInput)
        {
            match = coordinator;
            input = playerInput;
            matchId = Guid.NewGuid().ToString("N");
            startReal = Time.realtimeSinceStartup;
            previous = new Vector3[match.Characters.Length];
            previousStatus = new ParticipantStatus[previous.Length];
            knockbackSinceSample = new bool[previous.Length];
            invalidSinceSample = new bool[previous.Length];
            view = match.Characters[0].GetComponentInChildren<Camera>();
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "MovementTelemetry");
                Directory.CreateDirectory(directory);
                writer = new StreamWriter(Path.Combine(directory, matchId + ".jsonl"), false, new System.Text.UTF8Encoding(false));
                var record = NewRecord("match_start");
                record.scene = gameObject.scene.path; record.version = Application.version;
                record.platform = Application.platform.ToString(); record.width = Screen.width; record.height = Screen.height;
                record.move_speed = match.MoveSpeed;
                if (view != null) { record.fov = view.fieldOfView; var p = view.transform.localPosition; record.camera_x = p.x; record.camera_y = p.y; record.camera_z = p.z; }
                Add(record);
                ResetBaseline();
            }
            catch (Exception e) { Fail(e); }
        }

        private Record NewRecord(string type) => new Record { type = type, match_id = matchId, game_time = gameTime, real_time = Time.realtimeSinceStartup - startReal, actor = -1 };

        internal void NpcEvent(int id, string reason)
        {
            if (closed || writer == null || match.IsPaused) return;
            var record = NewRecord("npc_event"); record.actor = id; record.reason = reason;
            record.knocked_back = match.Characters[id].IsKnockedBack;
            Add(record);
        }

        private void LateUpdate()
        {
            if (closed || writer == null || match == null || match.Model == null) return;
            if (match.Model.Status != MatchStatus.Playing) { Close(match.Model.RemainingTime <= 0f ? "timeout" : "finished_unknown"); return; }
            if (match.IsPaused)
            {
                if (!paused) { paused = true; var r = NewRecord("pause"); Add(r); Flush(); }
                return;
            }
            if (paused) { paused = false; Add(NewRecord("resume")); ResetBaseline(); }
            gameTime += Time.deltaTime;
            for (int i = 0; i < previous.Length; i++)
            {
                if (match.Characters[i].IsKnockedBack) knockbackSinceSample[i] = true;
                if (match.Characters[i].LastTeleportFrame == Time.frameCount || match.Model.Participants[i].Status != previousStatus[i]) invalidSinceSample[i] = true;
            }
            Vector3 movement = PlanarVelocity.Cardinal(input.Movement, match.Characters[0].MovementVelocity).normalized;
            if (movement != lastInput)
            {
                var r = NewRecord("input"); r.input_x = movement.x; r.input_z = movement.z; Add(r); lastInput = movement;
            }
            if (gameTime - lastSample >= SampleInterval) Sample(movement);
            if (Time.realtimeSinceStartup - lastFlush >= 1f) Flush();
        }

        private void ResetBaseline()
        {
            lastSample = gameTime;
            for (int i = 0; i < previous.Length; i++)
            {
                previous[i] = match.Characters[i].transform.position;
                previousStatus[i] = match.Model.Participants[i].Status;
                knockbackSinceSample[i] = match.Characters[i].IsKnockedBack;
                invalidSinceSample[i] = false;
            }
        }

        private void Sample(Vector3 movement)
        {
            float dt = gameTime - lastSample;
            Vector3 playerPosition = match.Characters[0].transform.position;
            Vector3 previousPlayer = previous[0];
            bool playerValid = !invalidSinceSample[0] && previousStatus[0] == ParticipantStatus.Active && match.Model.Participants[0].Status == ParticipantStatus.Active;
            for (int i = 0; i < previous.Length; i++)
            {
                var character = match.Characters[i];
                Vector3 position = character.transform.position;
                var status = match.Model.Participants[i].Status;
                bool valid = dt > 0f && !invalidSinceSample[i] && status == ParticipantStatus.Active && previousStatus[i] == status;
                var r = NewRecord("movement"); r.actor = i; r.status = status.ToString(); r.dt = dt;
                r.x = position.x; r.z = position.z; r.valid_speed = valid;
                r.knocked_back = knockbackSinceSample[i] || character.IsKnockedBack;
                if (valid) r.speed = PlanarDistance(position, previous[i]) / dt;
                if (i == 0) { r.input_x = movement.x; r.input_z = movement.z; }
                else
                {
                    r.has_waypoint = match.Npcs != null && match.Npcs.HasWaypoint(i);
                    r.distance = PlanarDistance(position, playerPosition);
                    r.valid_relative_speed = valid && playerValid;
                    if (valid && playerValid) r.relative_speed = (r.distance - PlanarDistance(previous[i], previousPlayer)) / dt;
                    if (view != null)
                    {
                        Vector3 projected = view.WorldToViewportPoint(position);
                        r.viewport_x = projected.x; r.viewport_y = projected.y; r.depth = projected.z;
                        r.visible = projected.z > 0f && projected.x >= 0f && projected.x <= 1f && projected.y >= 0f && projected.y <= 1f;
                    }
                }
                Add(r);
                previous[i] = position; previousStatus[i] = status; knockbackSinceSample[i] = character.IsKnockedBack; invalidSinceSample[i] = false;
            }
            lastSample = gameTime;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return Mathf.Sqrt(x * x + z * z); }

        private void Add(Record record)
        {
            if (closed || writer == null) return;
            if (buffer.Count >= BufferLimit) { dropped = true; return; }
            buffer.Add(JsonUtility.ToJson(record));
        }

        private void Flush()
        {
            if (closed || writer == null) return;
            try
            {
                foreach (string line in buffer) writer.WriteLine(line);
                buffer.Clear();
                if (dropped) { writer.WriteLine(JsonUtility.ToJson(NewRecord("buffer_overflow"))); dropped = false; }
                writer.Flush(); lastFlush = Time.realtimeSinceStartup;
            }
            catch (Exception e) { Fail(e); }
        }

        private void Close(string reason)
        {
            if (closed) return;
            if (writer != null) { var r = NewRecord("match_end"); r.reason = reason; Add(r); Flush(); }
            closed = true;
            try { writer?.Dispose(); } catch (Exception e) { Debug.LogWarning("Movement telemetry close failed: " + e.Message); }
            writer = null;
        }

        private void Fail(Exception e)
        {
            Debug.LogWarning("Movement telemetry disabled: " + e.Message);
            closed = true;
            try { writer?.Dispose(); } catch (Exception) { }
            writer = null; buffer.Clear();
        }

        private void OnDestroy() { Close("interrupted"); }
        private void OnApplicationQuit() { Close("interrupted"); }
    }
}
