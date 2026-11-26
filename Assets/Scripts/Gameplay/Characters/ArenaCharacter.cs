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
        private float obstacleProtection;
        private Vector3 pendingKnockback;
        private float knockbackTime;
        private Vector3 attackKnockbackRemaining;
        private float attackKnockbackTime;
        private Vector3 lastMovement;
        private Vector3 movementVelocity;
        private SpriteRenderer[] sprites;
        private Color[] spriteColors;
        private bool flashing;
        private GroundShadow groundShadow;
        private AttackVisual attackVisual;
        public int Id { get; private set; }
        public Vector3 MovementVelocity => movementVelocity;
        public float RegionSpeedMultiplier
        {
            get
            {
                if (match == null || match.Model == null || match.Grid == null) return 1f;
                if (match.IsNetworkMovementTest && !Mirror.NetworkServer.active) return 1f;
                var owner = match.Model.Grid.OwnerAt(match.Grid.WorldToCell(transform.position));
                return !owner.HasValue ? 1f : owner.Value == Id ? match.OwnRegionSpeedMultiplier : match.EnemyRegionSpeedMultiplier;
            }
        }
        public bool IsKnockedBack => knockbackTime > 0f || attackKnockbackTime > 0f || lastKnockbackFrame == Time.frameCount;
        public int LastTeleportFrame { get; private set; } = -1;
        private int lastKnockbackFrame = -1;

        public void Initialize(MatchCoordinator coordinator, int id, Color color)
        {
            match = coordinator; Id = id;
            controller = GetComponent<CharacterController>();
            controller.stepOffset = 0f;
            arrow = transform.Find("Direction Indicator/Arrow");
            visual = transform.Find("Root");
            if (visual != null) visualScale = visual.localScale;
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            spriteColors = new Color[sprites.Length];
            SpriteRenderer characterSprite = null;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i].name == "PlayerSprite") { sprites[i].color = color; characterSprite = sprites[i]; }
                spriteColors[i] = sprites[i].color;
            }
            groundShadow = GroundShadow.Create(gameObject, 0.85f);
            attackVisual = gameObject.AddComponent<AttackVisual>();
            attackVisual.Initialize(coordinator, visual);
            if (characterSprite != null && characterSprite.transform.Find("Outline Outer") == null)
            {
                CreateOutline(characterSprite, "Outline Outer", new Color(0.07f, 0.08f, 0.1f), 1.22f, -2);
                CreateOutline(characterSprite, "Outline Inner", Color.white, 1.1f, -1);
            }
        }

        private static void CreateOutline(SpriteRenderer source, string name, Color color, float scale, int orderOffset)
        {
            var outline = new GameObject(name, typeof(SpriteRenderer));
            outline.transform.SetParent(source.transform, false);
            outline.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = outline.GetComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + orderOffset;
            renderer.color = color;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
        }

        public void SetActiveState(bool active)
        {
            if (visuallyActive == active) return;
            visuallyActive = active;
            if (groundShadow != null) groundShadow.SetVisible(active);
            if (!active && attackVisual != null) attackVisual.Clear();
            if (!active) { movementVelocity = Vector3.zero; pendingKnockback = Vector3.zero; knockbackTime = 0f; attackKnockbackRemaining = Vector3.zero; attackKnockbackTime = 0f; obstacleProtection = 0f; SetFlash(false); }
            if (controller != null) controller.enabled = active;
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = active;
            foreach (var line in GetComponentsInChildren<LineRenderer>(true)) line.enabled = active;
        }

        public void SetAttackAim(bool visible, Vector3 direction, float range, float angle)
        {
            if (attackVisual != null) attackVisual.SetAim(visible, direction, range, angle);
        }

        public void PlayAttack(Vector3 direction, float range, float angle)
        {
            if (attackVisual != null) attackVisual.Play(direction, range, angle);
        }

        public void Teleport(Vector3 position)
        {
            LastTeleportFrame = Time.frameCount;
            controller.enabled = false;
            // CellToWorld returns ground height; leave room for the controller's skin.
            position.y += controller.height * 0.5f - controller.center.y + controller.skinWidth;
            transform.position = position;
            controller.enabled = true;
            touching.Clear(); hasCell = false;
            movementVelocity = Vector3.zero;
            pendingKnockback = Vector3.zero; knockbackTime = 0f; attackKnockbackRemaining = Vector3.zero; attackKnockbackTime = 0f; obstacleProtection = 0f; SetFlash(false);
            ReportCell();
        }

        public void MoveWithVelocity(Vector3 desiredVelocity, float deltaTime)
        {
            if (controller == null || !controller.enabled || match == null || match.Model.Participants[Id].Status != ParticipantStatus.Active || IsKnockedBack)
            {
                movementVelocity = Vector3.zero;
                return;
            }
            desiredVelocity = PlanarVelocity.Cardinal(desiredVelocity, Vector3.zero) * RegionSpeedMultiplier;
            movementVelocity = PlanarVelocity.AdvanceCardinal(movementVelocity, desiredVelocity, match.MovementAcceleration, match.MovementDeceleration, deltaTime);
            Move(movementVelocity * Mathf.Max(0f, deltaTime));
        }

        public void Move(Vector3 delta)
        {
            if (!controller.enabled || match.Model.Participants[Id].Status != ParticipantStatus.Active || IsKnockedBack) return;
            delta = PlanarVelocity.Cardinal(delta, Vector3.zero);
            if (delta.sqrMagnitude > 0.000001f) lastMovement = delta;
            if (arrow != null && delta.sqrMagnitude > 0.000001f) arrow.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            if (delta.x != 0f) desiredFacing = delta.x < 0f ? -1 : 1;
            UpdateFacing(Time.deltaTime);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.2f));
            Vector3 before = transform.position;
            for (int i = 0; i < steps; i++)
            {
                MoveOnPlane(delta / steps);
                ReportCell();
                if (!match.IsNetworkMovementTest || Mirror.NetworkServer.active) match.Coins.TryCollect(this);
                if (match.Model.Participants[Id].Status != ParticipantStatus.Active || knockbackTime > 0f) break;
            }
            if ((transform.position - before).sqrMagnitude > 0.000001f)
            {
                footstepTimer -= Time.deltaTime;
                if (Id == 0 && footstepTimer <= 0f) { match.PlayFootstep(); footstepTimer = 0.35f; }
            }
            else footstepTimer = 0f;
        }

        public void ApplyNetworkPosition(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.000001f)
            {
                if (arrow != null) arrow.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                if (Mathf.Abs(delta.x) > 0.0001f) desiredFacing = delta.x < 0f ? -1 : 1;
                UpdateFacing(Time.deltaTime);
            }
            transform.position = position;
        }

        public void StartAttackKnockback(Vector3 displacement, float duration)
        {
            if (controller == null || !controller.enabled || match.Model.Participants[Id].Status != ParticipantStatus.Active) return;
            displacement.y = 0f;
            if (displacement.sqrMagnitude < 0.000001f || duration <= 0f) return;
            movementVelocity = Vector3.zero;
            pendingKnockback = Vector3.zero;
            knockbackTime = 0f;
            attackKnockbackRemaining = displacement;
            attackKnockbackTime = duration;
        }

        public void TickAttackKnockback(float deltaTime)
        {
            if (attackKnockbackTime <= 0f || deltaTime <= 0f || !controller.enabled || match.Model.Participants[Id].Status != ParticipantStatus.Active) return;
            lastKnockbackFrame = Time.frameCount;
            float stepTime = Mathf.Min(deltaTime, attackKnockbackTime);
            Vector3 movement = attackKnockbackRemaining * (stepTime / attackKnockbackTime);
            attackKnockbackRemaining -= movement;
            attackKnockbackTime -= stepTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / 0.2f));
            Vector3 step = movement / steps;
            bool blocked = false;
            for (int i = 0; i < steps; i++)
            {
                Vector3 before = transform.position;
                CollisionFlags flags = MoveOnPlane(step);
                if (match.Model.Participants[Id].Status != ParticipantStatus.Active) return;
                if (knockbackTime > 0f || (flags & CollisionFlags.Sides) != 0 || Vector3.Dot(transform.position - before, step.normalized) < step.magnitude * 0.5f)
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked && attackKnockbackTime > 0f) return;
            attackKnockbackTime = 0f;
            attackKnockbackRemaining = Vector3.zero;
            ReportCell();
            match.Coins.TryCollect(this);
        }

        private CollisionFlags MoveOnPlane(Vector3 delta)
        {
            Vector3 before = transform.position;
            CollisionFlags flags = controller.Move(delta);
            Vector3 after = transform.position;
            // Reject a step that collision resolution lifted off the arena plane.
            if (Mathf.Abs(after.y - before.y) > 0.001f) transform.position = before;
            else if (after.y != before.y) transform.position = new Vector3(after.x, before.y, after.z);
            return flags;
        }

        private void ReportCell()
        {
            if (match.IsNetworkMovementTest && (!Mirror.NetworkServer.active || match.NetworkController == null || !match.NetworkController.IsRunning)) return;
            var cell = match.Grid.WorldToCell(transform.position);
            if (hasCell && lastCell.Equals(cell)) return;
            hasCell = true; lastCell = cell;
            if (match.Rules.TryPaint(Id, cell))
            {
                match.Grid.Refresh(match.Model.Grid);
                if (match.IsNetworkMovementTest) match.NetworkController.BroadcastGridChange(cell, Id);
                if (match.Coins != null) match.Coins.NotifyGridChanged();
            }
            if (!match.IsNetworkMovementTest && match.Coins != null) match.Coins.UpdateDisguisedObstacleVisibility();
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

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 normal = hit.normal; normal.y = 0f;
            if (normal.sqrMagnitude > 0.000001f)
            {
                normal.Normalize();
                float intoSurface = Vector3.Dot(movementVelocity, normal);
                if (intoSurface < 0f) movementVelocity -= normal * intoSurface;
            }
            TryHazard(hit.collider);
        }
        private void OnTriggerEnter(Collider other) => TryHazard(other);
        private void OnTriggerStay(Collider other) => TryHazard(other);
        private void OnTriggerExit(Collider other) => touching.Remove(other);

        public void ContactDisguisedObstacle(DisguisedObstacle obstacle)
        {
            if (obstacle != null) TryHazard(obstacle.ContactCollider);
        }

        private void TryHazard(Collider other)
        {
            if (match == null || match.IsNetworkMovementTest || other == null || touching.Contains(other) || !match.IsHazard(other)) return;
            var disguised = other.GetComponentInParent<DisguisedObstacle>();
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 direction = center - other.ClosestPoint(center);
            if (disguised != null)
            {
                if (match.IsPaused || match.Model.Status != MatchStatus.Playing || match.Model.Participants[Id].Status != ParticipantStatus.Active || !disguised.TryConsume()) return;
            }
            touching.Add(other);
            if (obstacleProtection > 0f || !match.HitObstacle(Id)) return;
            if (match.Model.Participants[Id].Status != ParticipantStatus.Active) return;
            obstacleProtection = match.ObstacleInvulnerabilitySeconds;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.000001f) { direction = -lastMovement; direction.y = 0f; }
            if (direction.sqrMagnitude < 0.000001f) { direction = transform.forward; direction.y = 0f; }
            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.back;
            movementVelocity = Vector3.zero;
            pendingKnockback = direction.normalized * match.ObstacleKnockbackDistance;
            knockbackTime = pendingKnockback.sqrMagnitude > 0f ? match.ObstacleKnockbackSeconds : 0f;
        }

        private void Update()
        {
            if (match == null || match.IsPaused || match.Model == null || match.Model.Status != MatchStatus.Playing) return;
            if (obstacleProtection > 0f) obstacleProtection = Mathf.Max(0f, obstacleProtection - Time.deltaTime);
            SetFlash(obstacleProtection > 0f && Mathf.FloorToInt(obstacleProtection * 10f) % 2 == 0);
        }

        private void SetFlash(bool dim)
        {
            if (flashing == dim || sprites == null) return;
            flashing = dim;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color color = spriteColors[i];
                if (dim) color = Color.Lerp(color, Color.white, 0.85f);
                sprites[i].color = color;
            }
        }

        private void LateUpdate()
        {
            if (match != null && !match.IsPaused && match.Model != null && match.Model.Status == MatchStatus.Playing && controller != null && controller.enabled && knockbackTime > 0f)
            {
                lastKnockbackFrame = Time.frameCount;
                float stepTime = Mathf.Min(Time.deltaTime, knockbackTime);
                Vector3 knockback = pendingKnockback * (stepTime / knockbackTime);
                pendingKnockback -= knockback;
                knockbackTime -= stepTime;
                MoveOnPlane(knockback);
                ReportCell();
                if (!match.IsNetworkMovementTest || Mirror.NetworkServer.active) match.Coins.TryCollect(this);
            }
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
