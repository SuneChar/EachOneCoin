using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // Stable across processes: never hash Unity instance IDs or object memory addresses.
    public static class ArenaMapFingerprint
    {
        public static bool IsFixedProtocolCompatible(ArenaMapDefinition map)
        {
            if (!ArenaMapValidator.TryValidate(map, out _) || map.Width != ArenaGridView.Size || map.Height != ArenaGridView.Size) return false;
            foreach (var cell in map.Cells) if (cell.terrain != ArenaTerrain.Ground || cell.height != 0) return false;
            int patrols = 0, spikes = 0;
            foreach (var item in map.Objects)
            {
                if (item.group == ArenaMapObjectGroup.Moving) patrols++;
                else if (item.group == ArenaMapObjectGroup.Spike) spikes++;
            }
            return patrols <= 256 && spikes <= 256;
        }

        public static string Compute(ArenaMapDefinition map)
        {
            if (!ArenaMapValidator.TryValidate(map, out var error)) throw new ArgumentException(error, nameof(map));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(2);
                writer.Write(map.MapId ?? "");
                writer.Write(map.Width); writer.Write(map.Height); writer.Write(map.HeightUnit); writer.Write(map.SlowMultiplier);
                WriteObjectName(writer, map.DefaultTilePrefab); WriteObjectName(writer, map.WallPrefab);
                foreach (var cell in map.Cells)
                {
                    writer.Write((int)cell.terrain); writer.Write(cell.height); writer.Write(cell.direction);
                    WriteObjectName(writer, cell.tilePrefabOverride); WriteObjectName(writer, cell.meshOverride);
                }
                foreach (var point in map.Spawns) { writer.Write(point.x); writer.Write(point.y); }
                foreach (var point in map.Mines) { writer.Write(point.x); writer.Write(point.y); }
                writer.Write(map.Objects.Length);
                foreach (var item in map.Objects)
                {
                    writer.Write(item.id ?? ""); writer.Write((int)item.group); WriteObjectName(writer, item.prefab);
                    WriteVector(writer, item.position); WriteVector(writer, item.rotation);
                    writer.Write(item.overridePatrolSpeed); writer.Write(item.patrolSpeed);
                    writer.Write(item.overridePatrolEndOffset); WriteVector(writer, item.patrolEndOffset);
                    writer.Write(item.overrideInitiallyVisible); writer.Write(item.initiallyVisible);
                }
                writer.Flush();
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void WriteObjectName(BinaryWriter writer, UnityEngine.Object value)
        {
            writer.Write(value != null ? value.name : "");
            if (value == null) return;
            if (value is Mesh mesh) { WriteMesh(writer, mesh); return; }
            if (!(value is GameObject prefab)) return;
            var meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
            writer.Write(meshes.Length);
            foreach (var filter in meshes)
            {
                writer.Write(filter.gameObject.name);
                WriteVector(writer, filter.transform.localPosition);
                WriteVector(writer, filter.transform.localEulerAngles);
                WriteVector(writer, filter.transform.localScale);
                WriteMesh(writer, filter.sharedMesh);
                var renderer = filter.GetComponent<Renderer>();
                var materials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>();
                writer.Write(materials.Length);
                foreach (var material in materials) { writer.Write(material != null ? material.name : ""); writer.Write(material != null && material.shader != null ? material.shader.name : ""); }
            }
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            writer.Write(colliders.Length);
            foreach (var collider in colliders)
            {
                writer.Write(collider.gameObject.name); writer.Write(collider.GetType().Name);
                writer.Write(collider.enabled); writer.Write(collider.isTrigger);
                WriteVector(writer, collider.transform.localPosition);
                WriteVector(writer, collider.transform.localEulerAngles);
                WriteVector(writer, collider.transform.localScale);
                if (collider is BoxCollider box) { WriteVector(writer, box.center); WriteVector(writer, box.size); }
                else if (collider is SphereCollider sphere) { WriteVector(writer, sphere.center); writer.Write(sphere.radius); }
                else if (collider is CapsuleCollider capsule) { WriteVector(writer, capsule.center); writer.Write(capsule.radius); writer.Write(capsule.height); writer.Write(capsule.direction); }
            }
            var patrol = prefab.GetComponent<LinearPatrolObstacle>();
            writer.Write(patrol != null);
            if (patrol != null) { WriteVector(writer, patrol.ConfiguredEndOffset); writer.Write(patrol.ConfiguredSpeed); }
            var spike = prefab.GetComponent<TimedSpikeTrap>();
            writer.Write(spike != null);
            if (spike != null)
            {
                writer.Write(spike.ConfiguredVisibleDuration); writer.Write(spike.ConfiguredHiddenDuration);
                writer.Write(spike.ConfiguredInitiallyVisible); writer.Write(spike.ConfiguredTransitionDuration);
                writer.Write(spike.ConfiguredRaisedOffset); writer.Write(spike.ConfiguredRetractedOffset);
            }
        }

        private static void WriteMesh(BinaryWriter writer, Mesh mesh)
        {
            writer.Write(mesh != null ? mesh.name : "");
            if (mesh == null) return;
            writer.Write(mesh.vertexCount);
            WriteVector(writer, mesh.bounds.center); WriteVector(writer, mesh.bounds.size);
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
    }
}
