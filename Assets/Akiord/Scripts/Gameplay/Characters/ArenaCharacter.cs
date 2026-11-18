using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArenaCharacter : MonoBehaviour
    {
        private readonly HashSet<Collider> touching = new HashSet<Collider>();
        private readonly List<Collider> released = new List<Collider>();
        private MatchCoordinator match;
        private CharacterController controller;
        private Transform visual;
        private Transform arrow;
        private Vector3 visualScale;
        private int facing = 1;
        private int desiredFacing = 1;
        private float turnProgress = 1f;
        private bool flipped;
        private GridPosition lastCell;
        private bool hasCell;
        private float footstepTimer;
        private bool visuallyActive = true;
        public int Id { get; private set; }

        public void Initialize(MatchCoordinator coordinator, int id, Color color)
        {
            match = coordinator; Id = id;
            controller = GetComponent<CharacterController>();
            var oldHazard = GetComponent<PlayerHazardCollision>();
            if (oldHazard != null) oldHazard.enabled = false;
            var indicator = GetComponentInChildren<PlayerDirectionIndicator>(true);
            if (indicator != null) { indicator.enabled = false; arrow = indicator.transform.Find("Arrow"); }
            visual = transform.Find("Root");
            if (visual != null) visualScale = visual.localScale;
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.name == "PlayerSprite") sprite.color = color;
        }

        public void SetActiveState(bool active)
        {
            if (visuallyActive == active) return;
            visuallyActive = active;
            if (controller != null) controller.enabled = active;
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = active;
            foreach (var line in GetComponentsInChildren<LineRenderer>(true)) line.enabled = active;
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            touching.Clear(); hasCell = false;
            ReportCell();
        }

        public void Move(Vector3 delta)
        {
            if (!controller.enabled || match.Model.Participants[Id].Status != ParticipantStatus.Active) return;
            if (arrow != null && delta.sqrMagnitude > 0.000001f) arrow.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            if (delta.x != 0f) desiredFacing = delta.x < 0f ? -1 : 1;
            UpdateFacing(Time.deltaTime);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.2f));
            Vector3 before = transform.position;
            for (int i = 0; i < steps; i++)
            {
                controller.Move(delta / steps);
                ReportCell();
                match.Coins.TryCollect(this);
                if (match.Model.Participants[Id].Status != ParticipantStatus.Active) break;
            }
            if ((transform.position - before).sqrMagnitude > 0.000001f)
            {
                footstepTimer -= Time.deltaTime;
                if (Id == 0 && footstepTimer <= 0f) { match.PlayFootstep(); footstepTimer = 0.35f; }
            }
            else footstepTimer = 0f;
        }

        private void ReportCell()
        {
            var cell = match.Grid.WorldToCell(transform.position);
            if (hasCell && lastCell.Equals(cell)) return;
            hasCell = true; lastCell = cell;
            if (match.Model.TryPaint(Id, cell)) match.Grid.Refresh(match.Model.Grid);
        }

        private void UpdateFacing(float deltaTime)
        {
            if (visual == null) return;
            if (turnProgress >= 1f && facing != desiredFacing) { turnProgress = 0f; flipped = false; }
            if (turnProgress >= 1f) return;
            turnProgress = Mathf.Min(1f, turnProgress + deltaTime / 0.15f);
            if (!flipped && turnProgress >= 0.5f) { facing = desiredFacing; flipped = true; }
            var scale = visualScale; scale.x = Mathf.Abs(visualScale.x) * facing * Mathf.Max(0.01f, Mathf.Abs(1f - 2f * turnProgress));
            visual.localScale = scale;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit) => TryHazard(hit.collider);
        private void OnTriggerEnter(Collider other) => TryHazard(other);
        private void OnTriggerStay(Collider other) => TryHazard(other);
        private void OnTriggerExit(Collider other) => touching.Remove(other);

        private void TryHazard(Collider other)
        {
            if (match == null || other == null || touching.Contains(other) || !match.IsHazard(other)) return;
            touching.Add(other);
            match.HitObstacle(Id);
        }

        private void LateUpdate()
        {
            if (controller == null || !controller.enabled || touching.Count == 0) return;
            var center = transform.TransformPoint(controller.center);
            float threshold = controller.radius + 0.08f;
            released.Clear();
            foreach (var other in touching)
                if (other == null || !other.enabled || (other.ClosestPoint(center) - center).sqrMagnitude > threshold * threshold) released.Add(other);
            foreach (var other in released) touching.Remove(other);
        }
    }
}
