using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class CoinSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject coinModel;
        [SerializeField] private Transform coinRoot;
        [SerializeField] private Transform warningRoot;
        [SerializeField, Min(0.1f)] private float respawnSeconds = 2f;
        [SerializeField, Min(0.1f)] private float warningSeconds = 1f;
        private readonly List<Slot> slots = new List<Slot>();
        private MatchCoordinator match;
        private Material warningMaterial;
        private sealed class Slot { public GridPosition Position; public bool Random; public bool Dropped; public GameObject Coin; public GameObject Warning; public float Timer; }

        public void Initialize(MatchCoordinator coordinator)
        {
            match = coordinator;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            warningMaterial = new Material(shader);
            warningMaterial.color = new Color(1f, 0.8f, 0.1f);
            int center = ArenaGridView.Size / 2;
            int offset = (ArenaGridView.Size + 3) / 4;
            int near = center - offset;
            int far = center + offset;
            foreach (var position in new[] { new GridPosition(near, near), new GridPosition(far, near), new GridPosition(near, far), new GridPosition(far, far) }) slots.Add(new Slot { Position = position });
            for (int i = 0; i < 3; i++) slots.Add(new Slot { Random = true });
            foreach (var slot in slots) BeginWarning(slot);
        }

        public void Tick(float deltaTime)
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot.Coin != null) { slot.Coin.transform.Rotate(Vector3.up, 90f * deltaTime, Space.World); continue; }
                if (slot.Dropped) { slots.RemoveAt(i); continue; }
                slot.Timer -= deltaTime;
                if (slot.Warning != null)
                {
                    float pulse = 0.7f + 0.2f * Mathf.Sin(Time.time * 9f);
                    slot.Warning.transform.localScale = new Vector3(pulse, 0.025f, pulse);
                    if (slot.Timer <= 0f) Spawn(slot);
                }
                else if (slot.Timer <= 0f) BeginWarning(slot);
            }
        }

        public void TryCollect(ArenaCharacter character)
        {
            var position = character.transform.position;
            foreach (var slot in slots)
            {
                if (slot.Coin == null) continue;
                var center = match.Grid.CellToWorld(slot.Position);
                var delta = position - center; delta.y = 0;
                if (delta.sqrMagnitude > 0.16f || !match.Rules.TryCollectCoin(character.Id, slot.Position)) continue;
                Destroy(slot.Coin); slot.Coin = null;
                slot.Timer = slot.Dropped ? float.PositiveInfinity : Mathf.Max(0f, respawnSeconds - Mathf.Max(0.1f, warningSeconds));
                match.CoinCollected(character.Id);
            }
        }

        public void Drop(GridPosition origin, int count)
        {
            var position = match.FindSafeDrop(origin);
            for (int i = 0; i < count; i++) { var slot = new Slot { Position = position, Dropped = true }; slots.Add(slot); Spawn(slot); }
        }

        public bool HasCoin(GridPosition position)
        {
            foreach (var slot in slots) if (slot.Coin != null && slot.Position.Equals(position)) return true;
            return false;
        }

        private bool Reserved(GridPosition position)
        {
            foreach (var slot in slots) if ((slot.Coin != null || slot.Warning != null) && slot.Position.Equals(position)) return true;
            return false;
        }

        private void BeginWarning(Slot slot)
        {
            if (slot.Random)
            {
                var candidates = new List<GridPosition>();
                for (int x = 0; x < ArenaGridView.Size; x++) for (int y = 0; y < ArenaGridView.Size; y++)
                {
                    var p = new GridPosition(x, y);
                    if (!match.IsSpawn(p) && !match.IsBlocked(p) && !Reserved(p)) candidates.Add(p);
                }
                if (candidates.Count == 0) { slot.Timer = 1f; return; }
                slot.Position = candidates[Random.Range(0, candidates.Count)];
            }
            else if (Reserved(slot.Position) || match.IsBlocked(slot.Position)) { slot.Timer = 1f; return; }
            slot.Warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slot.Warning.name = "Coin Spawn Warning";
            slot.Warning.transform.SetParent(warningRoot);
            slot.Warning.transform.position = match.Grid.CellToWorld(slot.Position) + Vector3.up * 0.04f;
            slot.Warning.transform.localScale = new Vector3(0.7f, 0.025f, 0.7f);
            slot.Warning.GetComponent<Renderer>().sharedMaterial = warningMaterial;
            slot.Warning.GetComponent<Collider>().enabled = false;
            slot.Timer = Mathf.Max(0.1f, warningSeconds);
        }

        private void Spawn(Slot slot)
        {
            if (slot.Warning != null) { Destroy(slot.Warning); slot.Warning = null; }
            slot.Coin = coinModel != null ? Instantiate(coinModel, coinRoot) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            slot.Coin.name = slot.Dropped ? "Dropped Coin" : slot.Random ? "Random Coin" : "Fixed Coin";
            slot.Coin.transform.SetParent(coinRoot);
            slot.Coin.transform.position = match.Grid.CellToWorld(slot.Position) + Vector3.up * 0.45f;
            if (coinModel == null) slot.Coin.transform.localScale = Vector3.one * 0.35f;
            foreach (var collider in slot.Coin.GetComponentsInChildren<Collider>()) collider.enabled = false;
            GroundShadow.Create(slot.Coin, 0.55f);
        }

        private void OnDestroy() { if (warningMaterial != null) Destroy(warningMaterial); }
    }
}
