using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public sealed class HudProceduralIcons : IDisposable
    {
        private const string HudResourceFolder = "UI/HUD/";
        private const string HeartsResourceFolder = "UI/Hearts/";
        private const float DefaultPixelsPerUnit = 100f;

        private const int IconSize = 64;
        private const int PortraitSize = 128;
        private const int DotSize = 32;
        private const int PanelSize = 64;
        private const float PanelCornerRadiusPx = 12f;
        private const float PanelBorderPx = 14f;

        private struct MedalPalette
        {
            public Color RimDark;
            public Color RingLight;
            public Color RingDark;
            public Color InnerTop;
            public Color InnerBottom;
            public Color Groove;
            public Color RibbonMain;
            public Color RibbonStripe;
            public Color RibbonEdge;
        }

        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();
        private readonly Sprite[] medalSprites = new Sprite[4];
        private readonly Sprite[] miniHeartSprites = new Sprite[3];

        private Sprite coinSprite;
        private Sprite crownSprite;
        private Sprite circleMaskSprite;
        private Sprite circleFrameSprite;
        private Sprite defaultPortraitSprite;
        private Sprite statusDotSprite;
        private Sprite roundedPanelSprite;

        public Sprite GetCoinSprite()
        {
            if (coinSprite != null) return coinSprite;

            coinSprite = TryLoadResourceSprite(HudResourceFolder + "Coin");
            if (coinSprite == null)
            {
                coinSprite = CreateProceduralSprite("HudCoin", IconSize, EvaluateCoinPixel, Vector4.zero);
            }
            return coinSprite;
        }

        public Sprite GetCrownSprite()
        {
            if (crownSprite != null) return crownSprite;

            crownSprite = TryLoadResourceSprite(HudResourceFolder + "Crown");
            if (crownSprite == null)
            {
                crownSprite = CreateProceduralSprite("HudCrown", IconSize, EvaluateCrownPixel, Vector4.zero);
            }
            return crownSprite;
        }

        public Sprite GetMedalSprite(int finishOrder)
        {
            int tier = ResolveMedalTier(finishOrder);
            if (medalSprites[tier] != null) return medalSprites[tier];

            string resourceName;
            string proceduralName;
            switch (tier)
            {
                case 1:
                    resourceName = "MedalGold";
                    proceduralName = "HudMedalGold";
                    break;
                case 2:
                    resourceName = "MedalSilver";
                    proceduralName = "HudMedalSilver";
                    break;
                case 3:
                    resourceName = "MedalBronze";
                    proceduralName = "HudMedalBronze";
                    break;
                default:
                    resourceName = "MedalOther";
                    proceduralName = "HudMedalOther";
                    break;
            }

            Sprite sprite = TryLoadResourceSprite(HudResourceFolder + resourceName);
            if (sprite == null)
            {
                MedalPalette palette = GetMedalPalette(tier);
                sprite = CreateProceduralSprite(
                    proceduralName,
                    IconSize,
                    (u, v, invSize) => EvaluateMedalPixel(u, v, invSize, palette),
                    Vector4.zero);
            }

            medalSprites[tier] = sprite;
            return sprite;
        }

        public Sprite GetMiniHeartSprite(HeartVisualState state)
        {
            int index = ResolveHeartIndex(state);
            if (miniHeartSprites[index] != null) return miniHeartSprites[index];

            string hudPath;
            string heartsPath;
            string proceduralName;
            switch (state)
            {
                case HeartVisualState.Full:
                    hudPath = HudResourceFolder + "HeartMiniFull";
                    heartsPath = HeartsResourceFolder + "Heart_Full";
                    proceduralName = "HudMiniHeartFull";
                    break;
                case HeartVisualState.Dim:
                    hudPath = HudResourceFolder + "HeartMiniDim";
                    heartsPath = HeartsResourceFolder + "Heart_Dim";
                    proceduralName = "HudMiniHeartDim";
                    break;
                default:
                    hudPath = HudResourceFolder + "HeartMiniBroken";
                    heartsPath = HeartsResourceFolder + "Heart_BrokenGray";
                    proceduralName = "HudMiniHeartBroken";
                    break;
            }

            Sprite sprite = TryLoadResourceSprite(hudPath);
            if (sprite == null)
            {
                sprite = TryLoadResourceSprite(heartsPath);
            }
            if (sprite == null)
            {
                HeartVisualState capturedState = state;
                sprite = CreateProceduralSprite(
                    proceduralName,
                    IconSize,
                    (u, v, invSize) => EvaluateMiniHeartPixel(u, v, invSize, capturedState),
                    Vector4.zero);
            }

            miniHeartSprites[index] = sprite;
            return sprite;
        }

        public Sprite GetCircleMaskSprite()
        {
            if (circleMaskSprite != null) return circleMaskSprite;

            circleMaskSprite = TryLoadResourceSprite(HudResourceFolder + "PortraitCircleMask");
            if (circleMaskSprite == null)
            {
                circleMaskSprite = CreateProceduralSprite("HudCircleMask", PortraitSize, EvaluateCircleMaskPixel, Vector4.zero);
            }
            return circleMaskSprite;
        }

        public Sprite GetCircleFrameSprite()
        {
            if (circleFrameSprite != null) return circleFrameSprite;

            circleFrameSprite = TryLoadResourceSprite(HudResourceFolder + "PortraitCircleFrame");
            if (circleFrameSprite == null)
            {
                circleFrameSprite = CreateProceduralSprite("HudCircleFrame", PortraitSize, EvaluateCircleFramePixel, Vector4.zero);
            }
            return circleFrameSprite;
        }

        public Sprite GetDefaultPortraitSprite()
        {
            if (defaultPortraitSprite != null) return defaultPortraitSprite;

            defaultPortraitSprite = TryLoadResourceSprite(HudResourceFolder + "PortraitDefault");
            if (defaultPortraitSprite == null)
            {
                defaultPortraitSprite = CreateProceduralSprite("HudDefaultPortrait", PortraitSize, EvaluateDefaultPortraitPixel, Vector4.zero);
            }
            return defaultPortraitSprite;
        }

        public Sprite GetStatusDotSprite()
        {
            if (statusDotSprite != null) return statusDotSprite;

            statusDotSprite = TryLoadResourceSprite(HudResourceFolder + "StatusDot");
            if (statusDotSprite == null)
            {
                statusDotSprite = CreateProceduralSprite("HudStatusDot", DotSize, EvaluateStatusDotPixel, Vector4.zero);
            }
            return statusDotSprite;
        }

        public Sprite GetRoundedPanelSprite()
        {
            if (roundedPanelSprite != null) return roundedPanelSprite;

            roundedPanelSprite = TryLoadResourceSprite(HudResourceFolder + "RoundedPanel");
            if (roundedPanelSprite == null)
            {
                var border = new Vector4(PanelBorderPx, PanelBorderPx, PanelBorderPx, PanelBorderPx);
                roundedPanelSprite = CreateProceduralSprite("HudRoundedPanel", PanelSize, EvaluateRoundedPanelPixel, border);
            }
            return roundedPanelSprite;
        }

        public void Dispose()
        {
            for (int i = 0; i < runtimeSprites.Count; i++)
            {
                Sprite sprite = runtimeSprites[i];
                if (sprite == null) continue;
                DestroyUnityObject(sprite);
            }
            runtimeSprites.Clear();

            for (int i = 0; i < runtimeTextures.Count; i++)
            {
                Texture2D tex = runtimeTextures[i];
                if (tex == null) continue;
                DestroyUnityObject(tex);
            }
            runtimeTextures.Clear();

            coinSprite = null;
            crownSprite = null;
            circleMaskSprite = null;
            circleFrameSprite = null;
            defaultPortraitSprite = null;
            statusDotSprite = null;
            roundedPanelSprite = null;

            for (int i = 0; i < medalSprites.Length; i++)
            {
                medalSprites[i] = null;
            }

            for (int i = 0; i < miniHeartSprites.Length; i++)
            {
                miniHeartSprites[i] = null;
            }
        }

        private Sprite TryLoadResourceSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;

            Sprite assetSprite = Resources.Load<Sprite>(resourcePath);
            if (assetSprite != null) return assetSprite;

            Texture2D assetTex = Resources.Load<Texture2D>(resourcePath);
            if (assetTex == null) return null;

            Sprite wrapped = Sprite.Create(
                assetTex,
                new Rect(0f, 0f, assetTex.width, assetTex.height),
                new Vector2(0.5f, 0.5f),
                DefaultPixelsPerUnit,
                0u,
                SpriteMeshType.FullRect);
            wrapped.name = assetTex.name + "_HudRuntimeSprite";
            wrapped.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            runtimeSprites.Add(wrapped);
            return wrapped;
        }

        private Sprite CreateProceduralSprite(
            string namePrefix,
            int size,
            Func<float, float, float, Color> pixelFunc,
            Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = namePrefix + "_Tex",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };

            var pixels = new Color[size * size];
            float invSize = 1f / size;
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                float v = (y + 0.5f) * invSize;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) * invSize;
                    pixels[row + x] = pixelFunc(u, v, invSize);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            runtimeTextures.Add(tex);

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                DefaultPixelsPerUnit,
                0u,
                SpriteMeshType.FullRect,
                border);
            sprite.name = namePrefix + "_Sprite";
            sprite.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private static int ResolveMedalTier(int finishOrder)
        {
            if (finishOrder == 1) return 1;
            if (finishOrder == 2) return 2;
            if (finishOrder == 3) return 3;
            return 0;
        }

        private static int ResolveHeartIndex(HeartVisualState state)
        {
            switch (state)
            {
                case HeartVisualState.Full:
                    return 0;
                case HeartVisualState.Dim:
                    return 1;
                default:
                    return 2;
            }
        }

        private static MedalPalette GetMedalPalette(int tier)
        {
            switch (tier)
            {
                case 1:
                    return new MedalPalette
                    {
                        RimDark = new Color(0.55f, 0.33f, 0.04f, 1f),
                        RingLight = new Color(1.00f, 0.91f, 0.45f, 1f),
                        RingDark = new Color(0.86f, 0.56f, 0.08f, 1f),
                        InnerTop = new Color(0.90f, 0.66f, 0.14f, 1f),
                        InnerBottom = new Color(0.76f, 0.50f, 0.07f, 1f),
                        Groove = new Color(0.62f, 0.37f, 0.05f, 1f),
                        RibbonMain = new Color(0.84f, 0.21f, 0.23f, 1f),
                        RibbonStripe = new Color(0.98f, 0.45f, 0.43f, 1f),
                        RibbonEdge = new Color(0.52f, 0.10f, 0.12f, 1f)
                    };
                case 2:
                    return new MedalPalette
                    {
                        RimDark = new Color(0.35f, 0.40f, 0.48f, 1f),
                        RingLight = new Color(0.96f, 0.98f, 1.00f, 1f),
                        RingDark = new Color(0.63f, 0.69f, 0.78f, 1f),
                        InnerTop = new Color(0.73f, 0.78f, 0.85f, 1f),
                        InnerBottom = new Color(0.57f, 0.63f, 0.72f, 1f),
                        Groove = new Color(0.42f, 0.47f, 0.55f, 1f),
                        RibbonMain = new Color(0.18f, 0.44f, 0.86f, 1f),
                        RibbonStripe = new Color(0.45f, 0.68f, 1.00f, 1f),
                        RibbonEdge = new Color(0.10f, 0.25f, 0.54f, 1f)
                    };
                case 3:
                    return new MedalPalette
                    {
                        RimDark = new Color(0.42f, 0.21f, 0.09f, 1f),
                        RingLight = new Color(0.93f, 0.65f, 0.43f, 1f),
                        RingDark = new Color(0.68f, 0.37f, 0.18f, 1f),
                        InnerTop = new Color(0.76f, 0.46f, 0.25f, 1f),
                        InnerBottom = new Color(0.61f, 0.33f, 0.15f, 1f),
                        Groove = new Color(0.47f, 0.24f, 0.10f, 1f),
                        RibbonMain = new Color(0.18f, 0.52f, 0.56f, 1f),
                        RibbonStripe = new Color(0.38f, 0.75f, 0.77f, 1f),
                        RibbonEdge = new Color(0.10f, 0.31f, 0.34f, 1f)
                    };
                default:
                    return new MedalPalette
                    {
                        RimDark = new Color(0.26f, 0.24f, 0.22f, 1f),
                        RingLight = new Color(0.66f, 0.61f, 0.56f, 1f),
                        RingDark = new Color(0.44f, 0.40f, 0.36f, 1f),
                        InnerTop = new Color(0.50f, 0.46f, 0.42f, 1f),
                        InnerBottom = new Color(0.39f, 0.36f, 0.33f, 1f),
                        Groove = new Color(0.31f, 0.28f, 0.25f, 1f),
                        RibbonMain = new Color(0.36f, 0.39f, 0.44f, 1f),
                        RibbonStripe = new Color(0.52f, 0.55f, 0.61f, 1f),
                        RibbonEdge = new Color(0.22f, 0.24f, 0.28f, 1f)
                    };
            }
        }

        private static Color EvaluateCoinPixel(float u, float v, float invSize)
        {
            float aa = 1.25f * invSize;
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            float outerAlpha = Coverage(dist - 0.465f, aa);
            if (outerAlpha <= 0f) return Color.clear;

            float lightDir = Mathf.Clamp01(0.5f + (-dx * 0.62f + dy * 0.78f) * 1.15f);
            Color rimCol = Color.Lerp(
                new Color(0.56f, 0.31f, 0.03f, 1f),
                new Color(0.84f, 0.52f, 0.07f, 1f),
                lightDir);

            Color faceCol = Color.Lerp(
                new Color(0.88f, 0.55f, 0.06f, 1f),
                new Color(1.00f, 0.90f, 0.40f, 1f),
                lightDir);

            // Subtle diagonal specular gleam across the coin surface
            float gleam = Mathf.Exp(-Mathf.Pow((dx + dy - 0.14f) * 7.5f, 2f)) * 0.24f;
            faceCol = Color.Lerp(faceCol, new Color(1f, 0.98f, 0.80f, 1f), gleam);

            float innerFaceMask = Coverage(dist - 0.422f, aa);
            Color col = Color.Lerp(rimCol, faceCol, innerFaceMask);

            // Embossed inner highlight ring
            float ringDist = Mathf.Abs(dist - 0.345f) - 0.022f;
            float ringMask = Coverage(ringDist, aa);
            if (ringMask > 0f)
            {
                Color ringCol = Color.Lerp(
                    new Color(0.72f, 0.42f, 0.04f, 1f),
                    new Color(1.00f, 0.96f, 0.72f, 1f),
                    SmoothStep(0.25f, 0.78f, lightDir));
                col = Color.Lerp(col, ringCol, ringMask * 0.88f);
            }

            // Center embossed vertical bar motif with shadow + bevel
            var p = new Vector2(u, v);
            float shadowSdf = SdfCapsule(p, new Vector2(0.514f, 0.312f), new Vector2(0.514f, 0.672f), 0.047f);
            float shadowMask = Coverage(shadowSdf, aa);
            if (shadowMask > 0f)
            {
                col = Color.Lerp(col, new Color(0.62f, 0.34f, 0.03f, 1f), shadowMask * 0.72f);
            }

            float barSdf = SdfCapsule(p, new Vector2(0.50f, 0.325f), new Vector2(0.50f, 0.685f), 0.046f);
            float barRimMask = Coverage(barSdf, aa);
            if (barRimMask > 0f)
            {
                Color barRimCol = new Color(0.76f, 0.46f, 0.05f, 1f);
                Color barFillCol = Color.Lerp(
                    new Color(1.00f, 0.83f, 0.28f, 1f),
                    new Color(1.00f, 0.97f, 0.78f, 1f),
                    SmoothStep(0.28f, 0.74f, v));
                float barInnerMask = Coverage(barSdf + 0.014f, aa);
                Color barCol = Color.Lerp(barRimCol, barFillCol, barInnerMask);
                col = Color.Lerp(col, barCol, barRimMask);
            }

            col.a = outerAlpha;
            return col;
        }

        private static Color EvaluateCrownPixel(float u, float v, float invSize)
        {
            float aa = 1.25f * invSize;
            var p = new Vector2(u, v);

            float bandSdf = SdfRoundedBox(p, new Vector2(0.50f, 0.245f), new Vector2(0.31f, 0.072f), 0.032f);
            float leftSpire = SdfTriangle(p, new Vector2(0.19f, 0.27f), new Vector2(0.46f, 0.27f), new Vector2(0.16f, 0.70f));
            float centerSpire = SdfTriangle(p, new Vector2(0.26f, 0.27f), new Vector2(0.74f, 0.27f), new Vector2(0.50f, 0.77f));
            float rightSpire = SdfTriangle(p, new Vector2(0.54f, 0.27f), new Vector2(0.81f, 0.27f), new Vector2(0.84f, 0.70f));

            float orbLeft = SdfCircle(p, new Vector2(0.155f, 0.735f), 0.048f);
            float orbCenter = SdfCircle(p, new Vector2(0.50f, 0.815f), 0.056f);
            float orbRight = SdfCircle(p, new Vector2(0.845f, 0.735f), 0.048f);

            float bodySdf = Mathf.Min(bandSdf, Mathf.Min(leftSpire, Mathf.Min(centerSpire, rightSpire)));
            float crownSdf = Mathf.Min(bodySdf, Mathf.Min(orbLeft, Mathf.Min(orbCenter, orbRight)));

            float outlineAlpha = Coverage(crownSdf - 0.024f, aa);
            if (outlineAlpha <= 0f) return Color.clear;

            Color outlineCol = new Color(0.48f, 0.26f, 0.03f, 1f);
            float verticalT = SmoothStep(0.18f, 0.84f, v);
            float centerHighlight = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.5f) * 1.45f);
            Color goldCol = Color.Lerp(
                new Color(0.86f, 0.53f, 0.06f, 1f),
                new Color(1.00f, 0.93f, 0.48f, 1f),
                Mathf.Clamp01(verticalT * 0.78f + centerHighlight * 0.28f));

            // Slightly brighter sheen on the pearl tip orbs
            float orbsSdf = Mathf.Min(orbLeft, Mathf.Min(orbCenter, orbRight));
            float orbMask = Coverage(orbsSdf, aa);
            goldCol = Color.Lerp(goldCol, new Color(1.00f, 0.96f, 0.74f, 1f), orbMask * 0.35f);

            float fillMask = Coverage(crownSdf, aa);
            Color col = Color.Lerp(outlineCol, goldCol, fillMask);

            // Base band divider stripe
            if (fillMask > 0f)
            {
                float stripeSdf = SdfRoundedBox(p, new Vector2(0.50f, 0.312f), new Vector2(0.305f, 0.015f), 0.008f);
                float stripeMask = Coverage(stripeSdf, aa) * fillMask;
                col = Color.Lerp(col, new Color(0.68f, 0.38f, 0.04f, 1f), stripeMask * 0.85f);

                // Center crimson ruby + side gold studs on the crown band
                float centerGem = Coverage(SdfCircle(p, new Vector2(0.50f, 0.238f), 0.032f), aa) * fillMask;
                if (centerGem > 0f)
                {
                    col = Color.Lerp(col, new Color(0.92f, 0.20f, 0.26f, 1f), centerGem);
                }

                float sideGems = Mathf.Max(
                    Coverage(SdfCircle(p, new Vector2(0.33f, 0.238f), 0.023f), aa),
                    Coverage(SdfCircle(p, new Vector2(0.67f, 0.238f), 0.023f), aa)) * fillMask;
                if (sideGems > 0f)
                {
                    col = Color.Lerp(col, new Color(1.00f, 0.97f, 0.78f, 1f), sideGems * 0.9f);
                }
            }

            col.a = outlineAlpha;
            return col;
        }

        private static Color EvaluateMedalPixel(float u, float v, float invSize, MedalPalette palette)
        {
            float aa = 1.25f * invSize;
            var p = new Vector2(u, v);
            Color result = Color.clear;

            // Top V-shaped ribbon bands behind the medallion
            float leftRibbon = SdfConvexQuad(
                p,
                new Vector2(0.33f, 0.60f),
                new Vector2(0.51f, 0.60f),
                new Vector2(0.39f, 0.94f),
                new Vector2(0.18f, 0.94f));
            float rightRibbon = SdfConvexQuad(
                p,
                new Vector2(0.49f, 0.60f),
                new Vector2(0.67f, 0.60f),
                new Vector2(0.82f, 0.94f),
                new Vector2(0.61f, 0.94f));

            float ribbonSdf = Mathf.Min(leftRibbon, rightRibbon);
            float ribbonAlpha = Coverage(ribbonSdf, aa);
            if (ribbonAlpha > 0f)
            {
                float ribbonEdgeMask = Coverage(ribbonSdf + 0.018f, aa);
                Color ribbonCol = Color.Lerp(palette.RibbonEdge, palette.RibbonMain, ribbonEdgeMask);

                float leftCenterDist = SdfSegment(p, new Vector2(0.42f, 0.60f), new Vector2(0.285f, 0.94f));
                float rightCenterDist = SdfSegment(p, new Vector2(0.58f, 0.60f), new Vector2(0.715f, 0.94f));
                float stripeMask = Coverage(Mathf.Min(leftCenterDist, rightCenterDist) - 0.028f, aa) * ribbonEdgeMask;
                ribbonCol = Color.Lerp(ribbonCol, palette.RibbonStripe, stripeMask * 0.85f);

                // Subtle darken near top-bottom overlap behind the medallion
                float shade = SmoothStep(0.62f, 0.82f, v);
                ribbonCol = Color.Lerp(ribbonCol * 0.78f, ribbonCol, shade);
                ribbonCol.a = ribbonAlpha;
                result = BlendOver(result, ribbonCol);
            }

            // Medallion circular body positioned cleanly below ribbon crest
            var discCenter = new Vector2(0.50f, 0.43f);
            float dx = u - discCenter.x;
            float dy = v - discCenter.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float discAlpha = Coverage(dist - 0.365f, aa);
            if (discAlpha > 0f)
            {
                float lightT = Mathf.Clamp01(0.5f + (-dx * 0.60f + dy * 0.80f) * 1.45f);
                Color ringCol = Color.Lerp(palette.RingDark, palette.RingLight, lightT);

                float outerRimMask = Coverage(dist - 0.336f, aa);
                Color discCol = Color.Lerp(palette.RimDark, ringCol, outerRimMask);

                // Crisp inner bevel groove separating outer ring from clean center field
                float grooveMask = Coverage(Mathf.Abs(dist - 0.264f) - 0.011f, aa);
                discCol = Color.Lerp(discCol, palette.Groove, grooveMask * 0.85f);

                // Smooth clean center backdrop for overlaid rank digits (1 / 2 / 3)
                float innerMask = Coverage(dist - 0.256f, aa);
                if (innerMask > 0f)
                {
                    float innerGradT = SmoothStep(discCenter.y - 0.25f, discCenter.y + 0.25f, v);
                    Color innerCol = Color.Lerp(palette.InnerBottom, palette.InnerTop, innerGradT);
                    discCol = Color.Lerp(discCol, innerCol, innerMask);
                }

                discCol.a = discAlpha;
                result = BlendOver(result, discCol);
            }

            return result;
        }

        private static Color EvaluateMiniHeartPixel(float u, float v, float invSize, HeartVisualState state)
        {
            float aa = 1.25f * invSize;
            var hp = new Vector2((u - 0.5f) * 1.47f, (v - 0.12f) * 1.47f);
            float heartSdf = SdfHeart(hp) / 1.47f;

            float outerAlpha = Coverage(heartSdf - 0.022f, aa);
            if (outerAlpha <= 0f) return Color.clear;

            float fillMask = Coverage(heartSdf, aa);
            float gradT = SmoothStep(0.16f, 0.84f, v);

            Color outlineCol;
            Color fillCol;
            if (state == HeartVisualState.Full)
            {
                outlineCol = new Color(0.47f, 0.07f, 0.13f, 1f);
                fillCol = Color.Lerp(
                    new Color(0.84f, 0.12f, 0.22f, 1f),
                    new Color(1.00f, 0.33f, 0.42f, 1f),
                    gradT);

                float specDist = SdfCircle(new Vector2(u, v), new Vector2(0.32f, 0.67f), 0.085f);
                float specMask = Coverage(specDist, aa * 2.2f);
                fillCol = Color.Lerp(fillCol, new Color(1.00f, 0.84f, 0.86f, 1f), specMask * 0.58f);
            }
            else if (state == HeartVisualState.Dim)
            {
                outlineCol = new Color(0.18f, 0.08f, 0.11f, 1f);
                fillCol = Color.Lerp(
                    new Color(0.25f, 0.10f, 0.14f, 1f),
                    new Color(0.39f, 0.16f, 0.21f, 1f),
                    gradT);
            }
            else
            {
                outlineCol = new Color(0.22f, 0.24f, 0.27f, 1f);
                fillCol = Color.Lerp(
                    new Color(0.37f, 0.40f, 0.45f, 1f),
                    new Color(0.55f, 0.58f, 0.63f, 1f),
                    gradT);

                // Jagged dark fracture across the broken stone-gray heart
                var p = new Vector2(u, v);
                float c1 = SdfSegment(p, new Vector2(0.50f, 0.74f), new Vector2(0.44f, 0.57f));
                float c2 = SdfSegment(p, new Vector2(0.44f, 0.57f), new Vector2(0.56f, 0.43f));
                float c3 = SdfSegment(p, new Vector2(0.56f, 0.43f), new Vector2(0.47f, 0.24f));
                float crackDist = Mathf.Min(c1, Mathf.Min(c2, c3));
                float crackMask = Coverage(crackDist - 0.018f, aa);
                fillCol = Color.Lerp(fillCol, new Color(0.14f, 0.16f, 0.19f, 1f), crackMask * 0.92f);
            }

            Color col = Color.Lerp(outlineCol, fillCol, fillMask);
            col.a = outerAlpha;
            return col;
        }

        private static Color EvaluateCircleMaskPixel(float u, float v, float invSize)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Coverage(dist - 0.475f, invSize);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color EvaluateCircleFramePixel(float u, float v, float invSize)
        {
            float aa = 2.2f * invSize;
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float rn = Mathf.Sqrt(dx * dx + dy * dy) * 2f;

            // Double-ring frame: outer bold ring + dark separator groove + inner accent ring
            float frameOuter = Coverage(rn - 0.992f, aa);
            float frameInner = 1f - Coverage(rn - 0.792f, aa);
            float frameAlpha = Mathf.Min(frameOuter, frameInner);
            if (frameAlpha <= 0f) return Color.clear;

            float topShade = SmoothStep(0.10f, 0.90f, v);
            Color brightRing = Color.Lerp(
                new Color(0.88f, 0.91f, 0.96f, 1f),
                new Color(1.00f, 1.00f, 1.00f, 1f),
                topShade);
            Color darkRim = new Color(0.12f, 0.15f, 0.20f, 1f);
            Color innerAccent = Color.Lerp(
                new Color(0.84f, 0.88f, 0.95f, 1f),
                new Color(0.98f, 0.99f, 1.00f, 1f),
                topShade);

            // Compose radial bands cleanly from inner accent -> dark groove -> outer ring -> outer dark rim
            Color col = innerAccent;

            float grooveMask = 1f - Coverage(rn - 0.835f, aa);
            col = Color.Lerp(col, darkRim, grooveMask * 0.90f);

            float outerRingMask = 1f - Coverage(rn - 0.875f, aa);
            col = Color.Lerp(col, brightRing, outerRingMask);

            float outerDarkRimMask = 1f - Coverage(rn - 0.962f, aa);
            col = Color.Lerp(col, darkRim, outerDarkRimMask * 0.88f);

            col.a = frameAlpha;
            return col;
        }

        private static Color EvaluateDefaultPortraitPixel(float u, float v, float invSize)
        {
            float aa = 1.25f * invSize;
            var p = new Vector2(u, v);

            // Capsule character bust: vertical rounded capsule head smoothly blended into shoulders
            float headSdf = SdfCapsule(p, new Vector2(0.50f, 0.37f), new Vector2(0.50f, 0.58f), 0.23f);
            float shouldersSdf = SdfCapsule(p, new Vector2(0.31f, 0.06f), new Vector2(0.69f, 0.06f), 0.22f);
            float bodySdf = SmoothMin(headSdf, shouldersSdf, 0.11f);

            float bodyAlpha = Coverage(bodySdf, aa);
            if (bodyAlpha <= 0f) return Color.clear;

            // Pure white base with gentle cylindrical shading so Image.color (TintColor) dyes cleanly
            float horizontalLight = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.43f) * 1.15f);
            float verticalLight = SmoothStep(0.05f, 0.82f, v);
            float lum = Mathf.Lerp(0.81f, 1.00f, horizontalLight * 0.58f + verticalLight * 0.42f);

            // Subtle chin/collar shadow to separate capsule head from torso
            float neckShadowDist = SdfCapsule(p, new Vector2(0.41f, 0.33f), new Vector2(0.59f, 0.33f), 0.025f);
            float neckShadow = Coverage(neckShadowDist, aa * 5.0f) * 0.09f;
            lum -= neckShadow;

            // Soft specular pill highlight on top-left brow
            float browSpec = Coverage(SdfCapsule(p, new Vector2(0.385f, 0.65f), new Vector2(0.415f, 0.705f), 0.024f), aa * 3.0f);
            lum = Mathf.Clamp01(Mathf.Lerp(lum, 1.0f, browSpec * 0.65f));

            Color col = new Color(lum, lum, lum, 1f);

            // Cute dark capsule eyes + tiny bright catchlight
            float leftEye = SdfCapsule(p, new Vector2(0.42f, 0.545f), new Vector2(0.42f, 0.595f), 0.024f);
            float rightEye = SdfCapsule(p, new Vector2(0.58f, 0.545f), new Vector2(0.58f, 0.595f), 0.024f);
            float eyesMask = Coverage(Mathf.Min(leftEye, rightEye), aa);
            if (eyesMask > 0f)
            {
                Color eyeCol = new Color(0.13f, 0.14f, 0.17f, 1f);
                float leftGlint = SdfCircle(p, new Vector2(0.412f, 0.588f), 0.011f);
                float rightGlint = SdfCircle(p, new Vector2(0.572f, 0.588f), 0.011f);
                float glintMask = Coverage(Mathf.Min(leftGlint, rightGlint), aa);
                eyeCol = Color.Lerp(eyeCol, new Color(0.98f, 0.98f, 1.00f, 1f), glintMask * 0.92f);
                col = Color.Lerp(col, eyeCol, eyesMask);
            }

            col.a = bodyAlpha;
            return col;
        }

        private static Color EvaluateStatusDotPixel(float u, float v, float invSize)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Coverage(dist - 0.43f, 1.25f * invSize);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color EvaluateRoundedPanelPixel(float u, float v, float invSize)
        {
            float px = u * PanelSize;
            float py = v * PanelSize;
            float half = PanelSize * 0.5f;
            float sdf = SdfRoundedBox(
                new Vector2(px, py),
                new Vector2(half, half),
                new Vector2(half, half),
                PanelCornerRadiusPx);
            float alpha = Mathf.Clamp01(0.5f - sdf);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static float Coverage(float sdf, float aa)
        {
            return Mathf.Clamp01(0.5f - sdf / aa);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private static Color BlendOver(Color dst, Color src)
        {
            float outA = src.a + dst.a * (1f - src.a);
            if (outA <= 0.0001f) return Color.clear;
            float invOutA = 1f / outA;
            return new Color(
                (src.r * src.a + dst.r * dst.a * (1f - src.a)) * invOutA,
                (src.g * src.a + dst.g * dst.a * (1f - src.a)) * invOutA,
                (src.b * src.a + dst.b * dst.a * (1f - src.a)) * invOutA,
                outA);
        }

        private static float SdfCircle(Vector2 p, Vector2 center, float radius)
        {
            return (p - center).magnitude - radius;
        }

        private static float SdfSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float denom = Mathf.Max(0.000001f, Vector2.Dot(ba, ba));
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / denom);
            return (pa - ba * h).magnitude;
        }

        private static float SdfCapsule(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            return SdfSegment(p, a, b) - radius;
        }

        private static float SdfRoundedBox(Vector2 p, Vector2 center, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(
                Mathf.Abs(p.x - center.x) - (halfSize.x - radius),
                Mathf.Abs(p.y - center.y) - (halfSize.y - radius));
            float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            return outside + inside - radius;
        }

        private static float SdfHeart(Vector2 p)
        {
            p.x = Mathf.Abs(p.x);
            if (p.y + p.x > 1.0f)
            {
                float dx = p.x - 0.25f;
                float dy = p.y - 0.75f;
                return Mathf.Sqrt(dx * dx + dy * dy) - 0.35355339f;
            }

            float d1x = p.x;
            float d1y = p.y - 1.0f;
            float d1 = d1x * d1x + d1y * d1y;
            float m = 0.5f * Mathf.Max(p.x + p.y, 0.0f);
            float d2x = p.x - m;
            float d2y = p.y - m;
            float d2 = d2x * d2x + d2y * d2y;
            return Mathf.Sqrt(Mathf.Min(d1, d2)) * Mathf.Sign(p.x - p.y);
        }

        private static float SdfTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0;
            Vector2 e1 = p2 - p1;
            Vector2 e2 = p0 - p2;

            Vector2 v0 = p - p0;
            Vector2 v1 = p - p1;
            Vector2 v2 = p - p2;

            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Mathf.Max(0.000001f, Vector2.Dot(e0, e0)));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Mathf.Max(0.000001f, Vector2.Dot(e1, e1)));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Mathf.Max(0.000001f, Vector2.Dot(e2, e2)));

            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float minDistSq = Mathf.Min(Vector2.Dot(pq0, pq0), Mathf.Min(Vector2.Dot(pq1, pq1), Vector2.Dot(pq2, pq2)));

            float c0 = s * (v0.x * e0.y - v0.y * e0.x);
            float c1 = s * (v1.x * e1.y - v1.y * e1.x);
            float c2 = s * (v2.x * e2.y - v2.y * e2.x);
            float minCross = Mathf.Min(c0, Mathf.Min(c1, c2));

            return -Mathf.Sqrt(minDistSq) * Mathf.Sign(minCross);
        }

        private static float SdfConvexQuad(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Vector2 e0 = p1 - p0;
            Vector2 e1 = p2 - p1;
            Vector2 e2 = p3 - p2;
            Vector2 e3 = p0 - p3;

            Vector2 v0 = p - p0;
            Vector2 v1 = p - p1;
            Vector2 v2 = p - p2;
            Vector2 v3 = p - p3;

            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Mathf.Max(0.000001f, Vector2.Dot(e0, e0)));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Mathf.Max(0.000001f, Vector2.Dot(e1, e1)));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Mathf.Max(0.000001f, Vector2.Dot(e2, e2)));
            Vector2 pq3 = v3 - e3 * Mathf.Clamp01(Vector2.Dot(v3, e3) / Mathf.Max(0.000001f, Vector2.Dot(e3, e3)));

            float s = Mathf.Sign(e0.x * e3.y - e0.y * e3.x);
            float minDistSq = Mathf.Min(
                Mathf.Min(Vector2.Dot(pq0, pq0), Vector2.Dot(pq1, pq1)),
                Mathf.Min(Vector2.Dot(pq2, pq2), Vector2.Dot(pq3, pq3)));

            float c0 = s * (v0.x * e0.y - v0.y * e0.x);
            float c1 = s * (v1.x * e1.y - v1.y * e1.x);
            float c2 = s * (v2.x * e2.y - v2.y * e2.x);
            float c3 = s * (v3.x * e3.y - v3.y * e3.x);
            float minCross = Mathf.Min(Mathf.Min(c0, c1), Mathf.Min(c2, c3));

            return -Mathf.Sqrt(minDistSq) * Mathf.Sign(minCross);
        }
    }
}
