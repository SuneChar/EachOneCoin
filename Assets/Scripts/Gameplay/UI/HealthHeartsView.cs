using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public enum HeartVisualState
    {
        Full,
        Dim,
        Broken
    }

    public sealed class HealthHeartsView : MonoBehaviour
    {
        private const string ResourceFolder = "UI/Hearts/";
        private const int MaxPooledShards = 14;
        private const int ChunkCount = 3;

        [Header("Heart Sprites (leave empty to auto-load from Resources/UI/Hearts)")]
        [SerializeField] private Sprite heartFullSprite;
        [SerializeField] private Sprite heartDimSprite;
        [SerializeField] private Sprite heartBrokenGraySprite;
        [SerializeField] private Sprite heartFlashMaskSprite;
        [SerializeField] private Sprite[] crackOverlaySprites;
        [SerializeField] private Sprite chunkTopLeftSprite;
        [SerializeField] private Sprite chunkTopRightSprite;
        [SerializeField] private Sprite chunkBottomSprite;
        [SerializeField] private Sprite[] shardSprites;
        [SerializeField] private Sprite shatterRingSprite;

        [Header("Prefab Template (optional)")]
        [SerializeField] private HealthHeartSlotView slotTemplate;

        [Header("Layout")]
        [SerializeField, Min(16f)] private float heartSize = 87f;
        [SerializeField, Min(0f)] private float heartSpacing = 10f;
        [SerializeField] private Vector2 topLeftPosition = new Vector2(28f, -24f);

        [Header("Animation Timing")]
        [SerializeField, Min(0.05f)] private float damageBlinkDuration = 0.30f;
        [SerializeField, Range(1, 5)] private int damageBlinkCount = 2;
        [SerializeField, Min(0.05f)] private float healPopDuration = 0.26f;
        [SerializeField, Min(0.03f)] private float shatterCrackDuration = 0.11f;
        [SerializeField, Min(0.10f)] private float shatterBurstDuration = 0.48f;

        private struct ChunkMotion
        {
            public Vector2 Velocity;
            public float Rotation;
            public float Gravity;
        }

        private struct ShardMotion
        {
            public Vector2 Velocity;
            public float StartRotation;
            public float AngularVelocity;
            public float SizeRatio;
            public float Gravity;
            public int SpriteVariant;
            public bool BrightSpark;
        }

        private sealed class HeartSlot
        {
            public HeartVisualState State;
            public RectTransform Root;
            public Vector2 BasePosition;
            public UnityEngine.UI.Image BaseImage;
            public UnityEngine.UI.Image FadeOverlay;
            public UnityEngine.UI.Image FlashOverlay;
            public UnityEngine.UI.Image CrackOverlay;
            public UnityEngine.UI.Image RingOverlay;
            public UnityEngine.UI.Image[] ChunkImages;
            public UnityEngine.UI.Image[] ShardImages;

            public float BlinkTimer = -1f;
            public float HealTimer = -1f;
            public float ShatterTimer = -1f;
            public bool WasDimBeforeShatter;
            public int CrackVariant;
            public bool VfxFlipX;
            public float RingStartAngle;
            public float RingWarmth;
            public readonly ChunkMotion[] Chunks = new ChunkMotion[ChunkCount];
            public readonly ShardMotion[] Shards = new ShardMotion[MaxPooledShards];
            public int ActiveShardCount;

            public bool IsAnimating => BlinkTimer >= 0f || HealTimer >= 0f || ShatterTimer >= 0f;
        }

        private readonly List<Sprite> runtimeCreatedSprites = new List<Sprite>();
        private readonly int[] vfxStylePool = { 0, 1, 2 };
        private int vfxStylePoolRemaining;
        private HeartSlot[] slots;
        private bool spritesLoaded;
        private bool initialized;
        private bool hasAppliedState;

        public int Capacity => slots != null ? slots.Length : 0;
        public int CurrentHealth { get; private set; }
        public int CurrentMaxHealth { get; private set; }
        public float HeartSize => heartSize;
        public Vector2 TopLeftPosition => topLeftPosition;

        public HeartVisualState GetSlotState(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return slots[index].State;
        }

        public bool IsSlotAnimating(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return slots[index].IsAnimating;
        }

        public void Initialize(int initialMaxHealth)
        {
            int count = Mathf.Max(1, initialMaxHealth);
            if (initialized && slots != null && slots.Length == count) return;

            ClearBuiltSlots();
            EnsureSpritesLoaded();

            var rect = transform as RectTransform;
            if (rect != null)
            {
                float totalWidth = count * heartSize + Mathf.Max(0, count - 1) * heartSpacing;
                rect.sizeDelta = new Vector2(totalWidth, heartSize);
            }

            slots = new HeartSlot[count];
            for (int i = 0; i < count; i++)
            {
                slots[i] = CreateSlot(i);
            }

            CurrentHealth = count;
            CurrentMaxHealth = count;
            hasAppliedState = false;
            initialized = true;
            RefillVfxStylePool();
        }

        public void SetHealth(int current, int maxHealth, bool animate = true)
        {
            if (!initialized) Initialize(Mathf.Max(1, maxHealth));

            int clampedMax = Mathf.Clamp(maxHealth, 0, slots.Length);
            int clampedCurrent = Mathf.Clamp(current, 0, clampedMax);

            if (!hasAppliedState || !animate)
            {
                CurrentHealth = clampedCurrent;
                CurrentMaxHealth = clampedMax;
                hasAppliedState = true;
                for (int i = 0; i < slots.Length; i++)
                {
                    HeartVisualState target = i < clampedCurrent
                        ? HeartVisualState.Full
                        : (i < clampedMax ? HeartVisualState.Dim : HeartVisualState.Broken);
                    ApplyStaticSlot(slots[i], target);
                }
                return;
            }

            int prevHealth = CurrentHealth;
            int prevMaxHealth = CurrentMaxHealth;
            if (clampedCurrent == prevHealth && clampedMax == prevMaxHealth) return;

            CurrentHealth = clampedCurrent;
            CurrentMaxHealth = clampedMax;

            for (int i = 0; i < slots.Length; i++)
            {
                HeartSlot slot = slots[i];
                if (i >= clampedMax)
                {
                    if (i < prevMaxHealth)
                    {
                        StartShatter(slot, i >= prevHealth);
                    }
                    else if (slot.State != HeartVisualState.Broken)
                    {
                        ApplyStaticSlot(slot, HeartVisualState.Broken);
                    }
                }
                else if (i < clampedCurrent)
                {
                    if (slot.State != HeartVisualState.Full)
                    {
                        StartHealPop(slot);
                    }
                }
                else
                {
                    if (i < prevHealth)
                    {
                        StartDamageBlink(slot);
                    }
                    else if (slot.State != HeartVisualState.Dim)
                    {
                        ApplyStaticSlot(slot, HeartVisualState.Dim);
                    }
                }
            }
        }

        public void Tick(float deltaTime)
        {
            if (!initialized || slots == null || deltaTime <= 0f) return;

            float blinkDur = Mathf.Max(0.05f, damageBlinkDuration);
            float healDur = Mathf.Max(0.05f, healPopDuration);
            float shatterTotal = Mathf.Max(0.03f, shatterCrackDuration) + Mathf.Max(0.10f, shatterBurstDuration);

            for (int i = 0; i < slots.Length; i++)
            {
                HeartSlot slot = slots[i];

                if (slot.ShatterTimer >= 0f)
                {
                    slot.ShatterTimer += deltaTime;
                    if (slot.ShatterTimer >= shatterTotal)
                    {
                        ApplyStaticSlot(slot, HeartVisualState.Broken);
                    }
                    else
                    {
                        UpdateShatterVisual(slot);
                    }
                    continue;
                }

                if (slot.BlinkTimer >= 0f)
                {
                    slot.BlinkTimer += deltaTime;
                    if (slot.BlinkTimer >= blinkDur)
                    {
                        ApplyStaticSlot(slot, HeartVisualState.Dim);
                    }
                    else
                    {
                        UpdateBlinkVisual(slot, slot.BlinkTimer / blinkDur);
                    }
                    continue;
                }

                if (slot.HealTimer >= 0f)
                {
                    slot.HealTimer += deltaTime;
                    if (slot.HealTimer >= healDur)
                    {
                        ApplyStaticSlot(slot, HeartVisualState.Full);
                    }
                    else
                    {
                        UpdateHealVisual(slot, slot.HealTimer / healDur);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeCreatedSprites.Count; i++)
            {
                Sprite sprite = runtimeCreatedSprites[i];
                if (sprite == null) continue;
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
            runtimeCreatedSprites.Clear();
        }

        private void EnsureSpritesLoaded()
        {
            if (spritesLoaded) return;
            spritesLoaded = true;

            heartFullSprite = ResolveSprite(heartFullSprite, ResourceFolder + "Heart_Full");
            heartDimSprite = ResolveSprite(heartDimSprite, ResourceFolder + "Heart_Dim");
            heartBrokenGraySprite = ResolveSprite(heartBrokenGraySprite, ResourceFolder + "Heart_BrokenGray");
            heartFlashMaskSprite = ResolveSprite(heartFlashMaskSprite, ResourceFolder + "Heart_FlashMask");
            chunkTopLeftSprite = ResolveSprite(chunkTopLeftSprite, ResourceFolder + "Heart_ChunkTL");
            chunkTopRightSprite = ResolveSprite(chunkTopRightSprite, ResourceFolder + "Heart_ChunkTR");
            chunkBottomSprite = ResolveSprite(chunkBottomSprite, ResourceFolder + "Heart_ChunkBottom");
            shatterRingSprite = ResolveSprite(shatterRingSprite, ResourceFolder + "Heart_ShatterRing");

            if (crackOverlaySprites == null || crackOverlaySprites.Length == 0)
            {
                crackOverlaySprites = new[]
                {
                    ResolveSprite(null, ResourceFolder + "Heart_CrackOverlay_0"),
                    ResolveSprite(null, ResourceFolder + "Heart_CrackOverlay_1")
                };
            }

            if (shardSprites == null || shardSprites.Length == 0)
            {
                shardSprites = new[]
                {
                    ResolveSprite(null, ResourceFolder + "Heart_ShardA"),
                    ResolveSprite(null, ResourceFolder + "Heart_ShardB")
                };
            }
        }

        private Sprite ResolveSprite(Sprite existing, string resourcePath)
        {
            if (existing != null) return existing;
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;

            Sprite created = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            created.name = tex.name + "_RuntimeSprite";
            runtimeCreatedSprites.Add(created);
            return created;
        }

        private HeartSlot CreateSlot(int index)
        {
            if (slotTemplate != null && slotTemplate.HasRequiredLayers)
                return CreatePrefabSlot(index);

            var slotObj = new GameObject("HeartSlot_" + index, typeof(RectTransform));
            var slotRect = (RectTransform)slotObj.transform;
            slotRect.SetParent(transform, false);
            slotRect.anchorMin = new Vector2(0f, 1f);
            slotRect.anchorMax = new Vector2(0f, 1f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(heartSize, heartSize);

            Vector2 basePos = new Vector2(heartSize * 0.5f + index * (heartSize + heartSpacing), -heartSize * 0.5f);
            slotRect.anchoredPosition = basePos;

            var slot = new HeartSlot
            {
                State = HeartVisualState.Full,
                Root = slotRect,
                BasePosition = basePos,
                BaseImage = CreateChildImage(slotRect, "Base", heartFullSprite, heartSize),
                FadeOverlay = CreateChildImage(slotRect, "FadeOverlay", heartFullSprite, heartSize),
                FlashOverlay = CreateChildImage(slotRect, "FlashOverlay", heartFlashMaskSprite != null ? heartFlashMaskSprite : heartFullSprite, heartSize),
                CrackOverlay = CreateChildImage(slotRect, "CrackOverlay", GetCrackSprite(0), heartSize),
                RingOverlay = CreateChildImage(slotRect, "ShatterRing", shatterRingSprite, heartSize),
                ChunkImages = new UnityEngine.UI.Image[ChunkCount],
                ShardImages = new UnityEngine.UI.Image[MaxPooledShards]
            };

            Sprite[] chunkSprites = { chunkTopLeftSprite, chunkTopRightSprite, chunkBottomSprite };
            for (int c = 0; c < ChunkCount; c++)
            {
                slot.ChunkImages[c] = CreateChildImage(slotRect, "Chunk_" + c, chunkSprites[c], heartSize);
            }

            for (int s = 0; s < MaxPooledShards; s++)
            {
                slot.ShardImages[s] = CreateChildImage(slotRect, "Shard_" + s, GetShardSprite(s % 2), heartSize * 0.2f);
            }

            ApplyStaticSlot(slot, HeartVisualState.Full);
            return slot;
        }

        private HeartSlot CreatePrefabSlot(int index)
        {
            var template = Instantiate(slotTemplate, transform);
            template.name = "HeartSlot_" + index;
            var rect = (RectTransform)template.transform;
            float templateSize = rect.sizeDelta.x;
            float scale = templateSize > 0.001f ? heartSize / templateSize : 1f;
            foreach (var image in template.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                image.rectTransform.sizeDelta *= scale;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(heartSize, heartSize);
            var basePos = new Vector2(heartSize * 0.5f + index * (heartSize + heartSpacing), -heartSize * 0.5f);
            rect.anchoredPosition = basePos;

            var slot = new HeartSlot
            {
                State = HeartVisualState.Full,
                Root = rect,
                BasePosition = basePos,
                BaseImage = template.baseImage,
                FadeOverlay = template.fadeOverlay,
                FlashOverlay = template.flashOverlay,
                CrackOverlay = template.crackOverlay,
                RingOverlay = template.ringOverlay,
                ChunkImages = template.chunkImages,
                ShardImages = template.shardImages
            };
            slot.FadeOverlay.sprite = heartFullSprite;
            slot.FlashOverlay.sprite = heartFlashMaskSprite != null ? heartFlashMaskSprite : heartFullSprite;
            slot.CrackOverlay.sprite = GetCrackSprite(0);
            slot.RingOverlay.sprite = shatterRingSprite;
            Sprite[] chunkSprites = { chunkTopLeftSprite, chunkTopRightSprite, chunkBottomSprite };
            for (int c = 0; c < ChunkCount; c++) slot.ChunkImages[c].sprite = chunkSprites[c];
            for (int s = 0; s < MaxPooledShards; s++) slot.ShardImages[s].sprite = GetShardSprite(s % 2);
            ApplyStaticSlot(slot, HeartVisualState.Full);
            return slot;
        }

        private static UnityEngine.UI.Image CreateChildImage(RectTransform parent, string objectName, Sprite sprite, float size)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private void ClearBuiltSlots()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].Root == null) continue;
                if (Application.isPlaying) Destroy(slots[i].Root.gameObject);
                else DestroyImmediate(slots[i].Root.gameObject);
            }
            slots = null;
        }

        private void ApplyStaticSlot(HeartSlot slot, HeartVisualState state)
        {
            slot.State = state;
            slot.BlinkTimer = -1f;
            slot.HealTimer = -1f;
            slot.ShatterTimer = -1f;

            slot.Root.anchoredPosition = slot.BasePosition;
            slot.Root.localScale = Vector3.one;
            slot.Root.localRotation = Quaternion.identity;

            slot.BaseImage.enabled = true;
            slot.BaseImage.rectTransform.anchoredPosition = Vector2.zero;
            slot.BaseImage.rectTransform.localScale = Vector3.one;
            slot.BaseImage.rectTransform.localRotation = Quaternion.identity;
            slot.BaseImage.color = Color.white;

            switch (state)
            {
                case HeartVisualState.Full:
                    slot.BaseImage.sprite = heartFullSprite;
                    break;
                case HeartVisualState.Dim:
                    slot.BaseImage.sprite = heartDimSprite != null ? heartDimSprite : heartFullSprite;
                    if (heartDimSprite == null) slot.BaseImage.color = new Color(0.35f, 0.16f, 0.2f, 0.92f);
                    break;
                default:
                    slot.BaseImage.sprite = heartBrokenGraySprite != null ? heartBrokenGraySprite : heartDimSprite;
                    if (heartBrokenGraySprite == null) slot.BaseImage.color = new Color(0.45f, 0.47f, 0.52f, 0.9f);
                    break;
            }

            HideAllTransientLayers(slot);
        }

        private static void HideAllTransientLayers(HeartSlot slot)
        {
            slot.FadeOverlay.enabled = false;
            slot.FlashOverlay.enabled = false;
            slot.CrackOverlay.enabled = false;
            slot.RingOverlay.enabled = false;
            for (int c = 0; c < ChunkCount; c++) slot.ChunkImages[c].enabled = false;
            for (int s = 0; s < MaxPooledShards; s++) slot.ShardImages[s].enabled = false;
        }

        private void StartDamageBlink(HeartSlot slot)
        {
            slot.State = HeartVisualState.Dim;
            slot.BlinkTimer = 0f;
            slot.HealTimer = -1f;
            slot.ShatterTimer = -1f;
            HideAllTransientLayers(slot);
            UpdateBlinkVisual(slot, 0f);
        }

        private void UpdateBlinkVisual(HeartSlot slot, float progress)
        {
            float p = Mathf.Clamp01(progress);
            float blinkPhaseEnd = 0.68f;

            float shake = Mathf.Sin(p * Mathf.PI * (damageBlinkCount * 3f)) * (heartSize * 0.06f) * (1f - p);
            float scale = 1f + 0.13f * Mathf.Sin(p * Mathf.PI);

            slot.Root.anchoredPosition = slot.BasePosition + new Vector2(shake, 0f);
            slot.Root.localScale = new Vector3(scale, scale, 1f);

            if (p < blinkPhaseEnd)
            {
                slot.BaseImage.enabled = true;
                slot.BaseImage.sprite = heartFullSprite;
                slot.BaseImage.color = Color.white;
                slot.FadeOverlay.enabled = false;

                int totalSubSlots = Mathf.Max(1, damageBlinkCount * 2 - 1);
                int subIndex = Mathf.Min(totalSubSlots - 1, Mathf.FloorToInt((p / blinkPhaseEnd) * totalSubSlots));
                bool flashOn = (subIndex % 2) == 0;

                slot.FlashOverlay.enabled = true;
                slot.FlashOverlay.sprite = heartFlashMaskSprite != null ? heartFlashMaskSprite : heartFullSprite;
                slot.FlashOverlay.color = new Color(1f, 0.96f, 0.96f, flashOn ? 0.80f : 0.08f);
            }
            else
            {
                float dimProgress = (p - blinkPhaseEnd) / (1f - blinkPhaseEnd);
                slot.Root.anchoredPosition = slot.BasePosition;
                slot.Root.localScale = Vector3.one;
                slot.FlashOverlay.enabled = false;

                slot.BaseImage.enabled = true;
                slot.BaseImage.sprite = heartDimSprite != null ? heartDimSprite : heartFullSprite;
                slot.BaseImage.color = heartDimSprite != null ? Color.white : new Color(0.35f, 0.16f, 0.2f, 0.92f);

                slot.FadeOverlay.enabled = true;
                slot.FadeOverlay.sprite = heartFullSprite;
                slot.FadeOverlay.color = new Color(1f, 1f, 1f, 1f - dimProgress);
            }
        }

        private void StartHealPop(HeartSlot slot)
        {
            slot.State = HeartVisualState.Full;
            slot.HealTimer = 0f;
            slot.BlinkTimer = -1f;
            slot.ShatterTimer = -1f;
            HideAllTransientLayers(slot);
            UpdateHealVisual(slot, 0f);
        }

        private void UpdateHealVisual(HeartSlot slot, float progress)
        {
            float p = Mathf.Clamp01(progress);
            float scale = p < 0.6f
                ? Mathf.Lerp(0.74f, 1.15f, p / 0.6f)
                : Mathf.Lerp(1.15f, 1.0f, (p - 0.6f) / 0.4f);

            slot.Root.anchoredPosition = slot.BasePosition;
            slot.Root.localScale = new Vector3(scale, scale, 1f);
            slot.BaseImage.enabled = true;
            slot.BaseImage.sprite = heartFullSprite;
            slot.BaseImage.color = Color.white;
        }

        private void StartShatter(HeartSlot slot, bool wasDim)
        {
            slot.State = HeartVisualState.Broken;
            slot.WasDimBeforeShatter = wasDim;
            slot.ShatterTimer = 0f;
            slot.BlinkTimer = -1f;
            slot.HealTimer = -1f;
            HideAllTransientLayers(slot);

            int style = DrawNextVfxStyle();
            slot.CrackVariant = UnityEngine.Random.Range(0, crackOverlaySprites != null && crackOverlaySprites.Length > 0 ? crackOverlaySprites.Length : 1);
            slot.VfxFlipX = UnityEngine.Random.value < 0.5f;
            slot.RingStartAngle = UnityEngine.Random.Range(0f, 360f);
            slot.RingWarmth = UnityEngine.Random.value;

            float dir = slot.VfxFlipX ? -1f : 1f;
            float scaleFactor = heartSize / 68f;

            // 3 distinct burst choreographies (UI Y is positive upward)
            if (style == 0)
            {
                // Wide lateral burst
                SetChunkMotion(ref slot.Chunks[0], -50f, 24f, 42f, 44f, dir, scaleFactor);
                SetChunkMotion(ref slot.Chunks[1], 52f, 20f, -46f, 44f, dir, scaleFactor);
                SetChunkMotion(ref slot.Chunks[2], 8f, -36f, -26f, 58f, dir, scaleFactor);
            }
            else if (style == 1)
            {
                // High upward pop + heavy gravity tumble
                SetChunkMotion(ref slot.Chunks[0], -32f, 52f, 65f, 112f, dir, scaleFactor);
                SetChunkMotion(ref slot.Chunks[1], 34f, 48f, -68f, 112f, dir, scaleFactor);
                SetChunkMotion(ref slot.Chunks[2], -6f, 14f, 52f, 128f, dir, scaleFactor);
            }
            else
            {
                // Asymmetric diagonal cleave sweep
                SetChunkMotion(ref slot.Chunks[0], -62f, 36f, 58f, 52f, dir, scaleFactor);
                SetChunkMotion(ref slot.Chunks[1], 26f, 8f, -24f, 66f, dir, scaleFactor);
                SetChunkMotion(ref slot.Chunks[2], 38f, -42f, -54f, 76f, dir, scaleFactor);
            }

            int shardCount = UnityEngine.Random.Range(10, MaxPooledShards + 1);
            slot.ActiveShardCount = shardCount;
            int variantCount = shardSprites != null && shardSprites.Length > 0 ? shardSprites.Length : 1;
            for (int k = 0; k < shardCount; k++)
            {
                float angle = (Mathf.PI * 2f * k / shardCount) + UnityEngine.Random.Range(-0.22f, 0.22f);
                float speed = UnityEngine.Random.Range(62f, 128f) * (style == 1 ? 0.92f : 1.05f) * scaleFactor;
                float upBias = (style == 1 ? 36f : 10f) * scaleFactor;
                slot.Shards[k] = new ShardMotion
                {
                    Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + upBias),
                    StartRotation = UnityEngine.Random.Range(0f, 360f),
                    AngularVelocity = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(200f, 440f),
                    SizeRatio = UnityEngine.Random.Range(0.13f, 0.23f),
                    Gravity = UnityEngine.Random.Range(85f, 130f) * scaleFactor,
                    SpriteVariant = UnityEngine.Random.Range(0, variantCount),
                    BrightSpark = (k % 3) == 0
                };
            }

            UpdateShatterVisual(slot);
        }

        private static void SetChunkMotion(ref ChunkMotion motion, float vx, float vy, float rot, float grav, float dir, float scaleFactor)
        {
            float jitter = UnityEngine.Random.Range(0.84f, 1.18f);
            motion.Velocity = new Vector2(vx * jitter * dir * scaleFactor, vy * jitter * scaleFactor);
            motion.Rotation = rot * jitter * dir;
            motion.Gravity = grav * UnityEngine.Random.Range(0.90f, 1.10f) * scaleFactor;
        }

        private void UpdateShatterVisual(HeartSlot slot)
        {
            float crackDur = Mathf.Max(0.03f, shatterCrackDuration);
            float burstDur = Mathf.Max(0.10f, shatterBurstDuration);
            float t = slot.ShatterTimer;

            if (t < crackDur)
            {
                float p = t / crackDur;
                float dir = slot.VfxFlipX ? -1f : 1f;
                float shakeX = Mathf.Sin(p * Mathf.PI * 8f) * (heartSize * 0.075f) * (1f - 0.25f * p) * dir;
                float shakeY = Mathf.Cos(p * Mathf.PI * 6f) * (heartSize * 0.032f);
                float scale = 1f + 0.16f * p;

                slot.Root.anchoredPosition = slot.BasePosition + new Vector2(shakeX, shakeY);
                slot.Root.localScale = new Vector3(scale, scale, 1f);

                slot.BaseImage.enabled = true;
                slot.BaseImage.sprite = slot.WasDimBeforeShatter && heartDimSprite != null ? heartDimSprite : heartFullSprite;
                slot.BaseImage.color = Color.white;
                slot.BaseImage.rectTransform.localScale = Vector3.one;

                slot.FlashOverlay.enabled = p > 0.35f;
                if (slot.FlashOverlay.enabled)
                {
                    float lift = ((p - 0.35f) / 0.65f) * 0.70f;
                    slot.FlashOverlay.sprite = heartFlashMaskSprite != null ? heartFlashMaskSprite : heartFullSprite;
                    slot.FlashOverlay.color = new Color(1f, 0.96f, 0.94f, lift);
                }

                slot.CrackOverlay.enabled = true;
                slot.CrackOverlay.sprite = GetCrackSprite(slot.CrackVariant);
                slot.CrackOverlay.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * p);
                slot.CrackOverlay.rectTransform.localScale = new Vector3(slot.VfxFlipX ? -1f : 1f, 1f, 1f);
                return;
            }

            float bt = Mathf.Clamp01((t - crackDur) / burstDur);
            float fastOut = 1f - Mathf.Pow(1f - bt, 3f);

            slot.Root.anchoredPosition = slot.BasePosition;
            slot.Root.localScale = Vector3.one;
            slot.FlashOverlay.enabled = false;
            slot.CrackOverlay.enabled = false;

            // 1. Unified final broken stone-gray heart emerges cleanly once outer shell clears
            if (bt >= 0.14f)
            {
                float hp = Mathf.Clamp01((bt - 0.14f) / 0.28f);
                float settleScale = hp < 0.65f
                    ? Mathf.Lerp(0.86f, 1.03f, hp / 0.65f)
                    : Mathf.Lerp(1.03f, 1.0f, (hp - 0.65f) / 0.35f);
                float alpha = Mathf.Min(1f, hp * 1.15f);

                slot.BaseImage.enabled = true;
                slot.BaseImage.sprite = heartBrokenGraySprite != null ? heartBrokenGraySprite : heartDimSprite;
                slot.BaseImage.rectTransform.anchoredPosition = Vector2.zero;
                slot.BaseImage.rectTransform.localScale = new Vector3(settleScale, settleScale, 1f);
                slot.BaseImage.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                slot.BaseImage.enabled = false;
            }

            // 2. Shockwave ring + starburst
            if (bt < 0.45f && shatterRingSprite != null)
            {
                float rp = bt / 0.45f;
                float rScale = 0.65f + (1.18f + 0.22f * slot.RingWarmth) * (1f - (1f - rp) * (1f - rp));
                float rAlpha = (1f - rp) * (1f - rp);
                Color ringColor = new Color(
                    1f,
                    Mathf.Lerp(0.98f, 0.45f + 0.22f * slot.RingWarmth, rp),
                    Mathf.Lerp(0.92f, 0.49f - 0.14f * slot.RingWarmth, rp),
                    rAlpha);

                slot.RingOverlay.enabled = true;
                slot.RingOverlay.color = ringColor;
                slot.RingOverlay.rectTransform.localScale = new Vector3(slot.VfxFlipX ? -rScale : rScale, rScale, 1f);
                float rotZ = slot.RingStartAngle + rp * 22f * (slot.VfxFlipX ? 1f : -1f);
                slot.RingOverlay.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            }
            else
            {
                slot.RingOverlay.enabled = false;
            }

            // 3. Three crimson shell chunks explode along the randomized choreography
            if (bt < 0.72f)
            {
                float cp = bt / 0.72f;
                float cOut = 1f - (1f - cp) * (1f - cp);
                float fade = cp < 0.28f ? 1f : 1f - (cp - 0.28f) / 0.72f;
                Color startCol = slot.WasDimBeforeShatter
                    ? new Color(0.50f, 0.21f, 0.27f, fade)
                    : new Color(1.00f, 0.34f, 0.41f, fade);
                Color endCol = slot.WasDimBeforeShatter
                    ? new Color(0.35f, 0.16f, 0.20f, fade)
                    : new Color(0.67f, 0.22f, 0.28f, fade);
                Color chunkCol = Color.Lerp(startCol, endCol, cp);
                float cScale = 1.00f - 0.32f * cp;

                for (int c = 0; c < ChunkCount; c++)
                {
                    UnityEngine.UI.Image img = slot.ChunkImages[c];
                    if (img.sprite == null) { img.enabled = false; continue; }
                    ChunkMotion m = slot.Chunks[c];
                    img.enabled = true;
                    img.color = chunkCol;
                    img.rectTransform.anchoredPosition = new Vector2(
                        m.Velocity.x * cOut,
                        m.Velocity.y * cOut - m.Gravity * cp * cp);
                    img.rectTransform.localScale = new Vector3(slot.VfxFlipX ? -cScale : cScale, cScale, 1f);
                    img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, m.Rotation * cOut);
                }
            }
            else
            {
                for (int c = 0; c < ChunkCount; c++) slot.ChunkImages[c].enabled = false;
            }

            // 4. Pooled sharp crystal shards bursting outward with gravity
            if (bt < 0.96f)
            {
                float sFade = bt < 0.30f ? 1f : 1f - (bt - 0.30f) / 0.66f;
                float grayT = Mathf.Min(1f, bt * 1.4f);
                Color grayEnd = new Color(0.61f, 0.64f, 0.68f, sFade);
                Color sparkStart = new Color(1.00f, 0.91f, 0.86f, sFade);
                Color bodyStart = slot.WasDimBeforeShatter
                    ? new Color(0.55f, 0.24f, 0.30f, sFade)
                    : new Color(0.98f, 0.32f, 0.39f, sFade);

                for (int k = 0; k < MaxPooledShards; k++)
                {
                    UnityEngine.UI.Image img = slot.ShardImages[k];
                    if (k >= slot.ActiveShardCount)
                    {
                        img.enabled = false;
                        continue;
                    }
                    Sprite shardSprite = GetShardSprite(slot.Shards[k].SpriteVariant);
                    if (shardSprite == null) { img.enabled = false; continue; }

                    ShardMotion sm = slot.Shards[k];
                    img.enabled = true;
                    img.sprite = shardSprite;
                    img.color = Color.Lerp(sm.BrightSpark ? sparkStart : bodyStart, grayEnd, grayT);

                    float sz = heartSize * sm.SizeRatio * (1.06f - 0.42f * bt);
                    img.rectTransform.sizeDelta = new Vector2(sz, sz);
                    img.rectTransform.anchoredPosition = new Vector2(
                        sm.Velocity.x * fastOut * 0.85f,
                        sm.Velocity.y * fastOut * 0.85f - sm.Gravity * bt * bt);
                    img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sm.StartRotation + sm.AngularVelocity * bt);
                }
            }
            else
            {
                for (int k = 0; k < MaxPooledShards; k++) slot.ShardImages[k].enabled = false;
            }
        }

        private Sprite GetCrackSprite(int index)
        {
            if (crackOverlaySprites == null || crackOverlaySprites.Length == 0) return null;
            return crackOverlaySprites[Mathf.Abs(index) % crackOverlaySprites.Length];
        }

        private Sprite GetShardSprite(int index)
        {
            if (shardSprites == null || shardSprites.Length == 0) return null;
            return shardSprites[Mathf.Abs(index) % shardSprites.Length];
        }

        private void RefillVfxStylePool()
        {
            vfxStylePool[0] = 0;
            vfxStylePool[1] = 1;
            vfxStylePool[2] = 2;
            for (int i = vfxStylePool.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = vfxStylePool[i];
                vfxStylePool[i] = vfxStylePool[j];
                vfxStylePool[j] = temp;
            }
            vfxStylePoolRemaining = vfxStylePool.Length;
        }

        private int DrawNextVfxStyle()
        {
            if (vfxStylePoolRemaining <= 0) RefillVfxStylePool();
            vfxStylePoolRemaining--;
            return vfxStylePool[vfxStylePoolRemaining];
        }
    }
}
