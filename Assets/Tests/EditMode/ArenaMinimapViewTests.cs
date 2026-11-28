using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArenaMinimapViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private Type sizeModeType;
        private GridModel grid;
        private GridPosition[] spawns;
        private GridPosition[] mines;

        private MethodInfo initStandaloneMethod;
        private MethodInfo refreshGridOnlyMethod;
        private MethodInfo getMapPixelMethod;
        private MethodInfo applyPlayerMethod;
        private MethodInfo applyDynamicMarkersMethod;
        private MethodInfo isPlayerVisibleMethod;
        private MethodInfo worldToNormMethod;
        private MethodInfo cellToNormMethod;
        private MethodInfo setSizeModeMethod;
        private MethodInfo toggleSizeModeMethod;
        private PropertyInfo refreshCountProp;
        private PropertyInfo focusedSlotProp;
        private PropertyInfo activeCoinsProp;
        private PropertyInfo activeWarningsProp;
        private PropertyInfo sizeModeProp;
        private PropertyInfo mapSizeProp;
        private PropertyInfo expandedMapSizeProp;
        private PropertyInfo compactMapSizeProp;
        private EventInfo sizeModeChangedEvent;

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            viewType = Type.GetType("EachOneCoin.Gameplay.ArenaMinimapView, Assembly-CSharp", true);
            sizeModeType = Type.GetType("EachOneCoin.Gameplay.MinimapSizeMode, Assembly-CSharp", true);
            initStandaloneMethod = viewType.GetMethod("InitializeStandalone", new[]
            {
                typeof(GridModel),
                typeof(IReadOnlyList<GridPosition>),
                typeof(IReadOnlyList<GridPosition>),
                typeof(Color[])
            });
            refreshGridOnlyMethod = viewType.GetMethod("RefreshGridOnly", Type.EmptyTypes);
            getMapPixelMethod = viewType.GetMethod("GetMapPixel", new[] { typeof(int), typeof(int) });
            applyPlayerMethod = viewType.GetMethod("ApplyPlayerMarkerState", new[]
            {
                typeof(int), typeof(bool), typeof(Vector3), typeof(Vector3), typeof(bool)
            });
            applyDynamicMarkersMethod = viewType.GetMethod("ApplyDynamicMarkers", new[]
            {
                typeof(IReadOnlyList<GridPosition>), typeof(IReadOnlyList<GridPosition>), typeof(int), typeof(float)
            });
            isPlayerVisibleMethod = viewType.GetMethod("IsPlayerMarkerVisible", new[] { typeof(int) });
            worldToNormMethod = viewType.GetMethod("WorldToNormalized", BindingFlags.Public | BindingFlags.Static);
            cellToNormMethod = viewType.GetMethod("CellToNormalized", BindingFlags.Public | BindingFlags.Static);
            setSizeModeMethod = viewType.GetMethod("SetSizeMode", new[] { sizeModeType });
            toggleSizeModeMethod = viewType.GetMethod("ToggleSizeMode", Type.EmptyTypes);

            refreshCountProp = viewType.GetProperty("TextureRefreshCount");
            focusedSlotProp = viewType.GetProperty("FocusedPlayerSlot");
            activeCoinsProp = viewType.GetProperty("ActiveCoinMarkerCount");
            activeWarningsProp = viewType.GetProperty("ActiveWarningMarkerCount");
            sizeModeProp = viewType.GetProperty("SizeMode");
            mapSizeProp = viewType.GetProperty("MapSize");
            expandedMapSizeProp = viewType.GetProperty("ExpandedMapSize");
            compactMapSizeProp = viewType.GetProperty("CompactMapSize");
            sizeModeChangedEvent = viewType.GetEvent("SizeModeChanged");

            spawns = new[]
            {
                new GridPosition(1, 1),
                new GridPosition(19, 1),
                new GridPosition(1, 19),
                new GridPosition(19, 19)
            };
            mines = new[]
            {
                new GridPosition(10, 5),
                new GridPosition(10, 15),
                new GridPosition(5, 10),
                new GridPosition(15, 10)
            };

            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            var match = session.CreateMatch(21, 21, spawns, 3, 100f);
            var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            rules.ConfigureMines(mines);
            rules.StartMatch();
            grid = match.Grid;

            root = new GameObject("ArenaMinimapViewTests", typeof(RectTransform));
            view = root.AddComponent(viewType);
            initStandaloneMethod.Invoke(view, new object[] { grid, spawns, mines, null });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            TestMatchArchitecture.Interface.Deinit();
        }

        private Color32 PixelAt(int x, int y) => (Color32)getMapPixelMethod.Invoke(view, new object[] { x, y });
        private int RefreshCount => (int)refreshCountProp.GetValue(view);

        [Test]
        public void WorldAndCellCoordinates_MapToNormalizedMinimapAndClampEdges()
        {
            var center = (Vector2)worldToNormMethod.Invoke(null, new object[] { Vector3.zero });
            var minCorner = (Vector2)worldToNormMethod.Invoke(null, new object[] { new Vector3(-20f, 0f, -20f) });
            var maxCorner = (Vector2)worldToNormMethod.Invoke(null, new object[] { new Vector3(20f, 0f, 20f) });
            var cellCenter = (Vector2)cellToNormMethod.Invoke(null, new object[] { new GridPosition(10, 10) });

            Assert.That(center.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(center.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(minCorner, Is.EqualTo(Vector2.zero));
            Assert.That(maxCorner, Is.EqualTo(Vector2.one));
            Assert.That(cellCenter.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(cellCenter.y, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void RectangularGrid_UsesActualTextureDimensionsAndAspect()
        {
            var positions = new[] { new GridPosition(1, 1), new GridPosition(23, 21) };
            var rectangular = new GridModel(25, 23, positions);
            initStandaloneMethod.Invoke(view, new object[] { rectangular, positions, Array.Empty<GridPosition>(), null });
            var texture = (Texture2D)viewType.GetField("gridTexture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Assert.That(texture.width, Is.EqualTo(25));
            Assert.That(texture.height, Is.EqualTo(23));
            var size = ((RectTransform)root.transform).sizeDelta;
            Assert.That(size.x / size.y, Is.EqualTo(25f / 23f).Within(0.001f));
            Assert.DoesNotThrow(() => PixelAt(24, 22));
        }

        [Test]
        public void GridTexture_DimsDisconnectedTerritoryAndBrightensConnectedTerritory()
        {
            var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            rules.TryPaint(0, new GridPosition(2, 1));
            rules.TryPaint(0, new GridPosition(3, 1));
            refreshGridOnlyMethod.Invoke(view, null);

            Color32 connectedPixel = PixelAt(3, 1);

            // Player 1 cuts cell (2,1), disconnecting player 0's cell (3,1) from spawn (1,1).
            rules.TryPaint(1, new GridPosition(2, 1));
            refreshGridOnlyMethod.Invoke(view, null);

            Color32 disconnectedPixel = PixelAt(3, 1);

            Assert.That(connectedPixel.r, Is.GreaterThan(disconnectedPixel.r + 50));
        }

        [Test]
        public void GridTexture_SkipsRedrawWhenGridRevisionIsUnchanged()
        {
            int countAfterInit = RefreshCount;
            refreshGridOnlyMethod.Invoke(view, null);
            refreshGridOnlyMethod.Invoke(view, null);
            Assert.That(RefreshCount, Is.EqualTo(countAfterInit));

            var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            rules.TryPaint(0, new GridPosition(2, 1));
            refreshGridOnlyMethod.Invoke(view, null);
            Assert.That(RefreshCount, Is.EqualTo(countAfterInit + 1));
        }

        [Test]
        public void PlayerMarkers_RespectVisibilityMaskAndSwitchFocusSlot()
        {
            applyPlayerMethod.Invoke(view, new object[] { 0, true, Vector3.zero, Vector3.right, true });
            applyPlayerMethod.Invoke(view, new object[] { 1, false, Vector3.zero, Vector3.forward, false });

            Assert.That((bool)isPlayerVisibleMethod.Invoke(view, new object[] { 0 }), Is.True);
            Assert.That((bool)isPlayerVisibleMethod.Invoke(view, new object[] { 1 }), Is.False);
            Assert.That((int)focusedSlotProp.GetValue(view), Is.EqualTo(0));

            // Switch focus (e.g., spectator switches to slot 2).
            applyPlayerMethod.Invoke(view, new object[] { 2, true, new Vector3(3f, 0f, 3f), Vector3.left, true });
            Assert.That((bool)isPlayerVisibleMethod.Invoke(view, new object[] { 2 }), Is.True);
            Assert.That((int)focusedSlotProp.GetValue(view), Is.EqualTo(2));
        }

        [Test]
        public void CoinAndWarningPools_ReuseMarkersAcrossCountChanges()
        {
            var coins = new List<GridPosition> { new GridPosition(8, 6), new GridPosition(12, 10), new GridPosition(6, 14) };
            var warnings = new List<GridPosition> { new GridPosition(9, 16), new GridPosition(11, 14) };

            applyDynamicMarkersMethod.Invoke(view, new object[] { coins, warnings, 1 << 1, 0.5f });
            Assert.That((int)activeCoinsProp.GetValue(view), Is.EqualTo(3));
            Assert.That((int)activeWarningsProp.GetValue(view), Is.EqualTo(2));

            coins.RemoveAt(0);
            warnings.Clear();
            applyDynamicMarkersMethod.Invoke(view, new object[] { coins, warnings, 0, 1.0f });
            Assert.That((int)activeCoinsProp.GetValue(view), Is.EqualTo(2));
            Assert.That((int)activeWarningsProp.GetValue(view), Is.EqualTo(0));
        }

        [Test]
        public void CoinPool_DisplaysAllTwentySixScheduledCoinsWithoutTruncation()
        {
            Assert.That(CoinSpawnSchedule.TotalCoins, Is.EqualTo(26));

            var coins = new List<GridPosition>(32);
            for (int i = 0; i < CoinSpawnSchedule.TotalCoins; i++)
            {
                coins.Add(new GridPosition(i % 21, i / 21));
            }

            Assert.DoesNotThrow(() => applyDynamicMarkersMethod.Invoke(view, new object[] { coins, Array.Empty<GridPosition>(), 0, 0.5f }));
            Assert.That((int)activeCoinsProp.GetValue(view), Is.EqualTo(26));

            int initialChildCount = root.transform.childCount;

            // Reduce active coins and confirm pooled markers shrink cleanly without allocating new objects.
            coins.RemoveRange(10, coins.Count - 10);
            Assert.DoesNotThrow(() => applyDynamicMarkersMethod.Invoke(view, new object[] { coins, Array.Empty<GridPosition>(), 0, 1.0f }));
            Assert.That((int)activeCoinsProp.GetValue(view), Is.EqualTo(10));
            Assert.That(root.transform.childCount, Is.EqualTo(initialChildCount));

            // Expand up to the full 32-marker pool (26 scheduled coins + combat drops) and confirm reuse.
            for (int i = coins.Count; i < 32; i++)
            {
                coins.Add(new GridPosition(i % 21, i / 21));
            }

            Assert.DoesNotThrow(() => applyDynamicMarkersMethod.Invoke(view, new object[] { coins, Array.Empty<GridPosition>(), 0, 1.5f }));
            Assert.That((int)activeCoinsProp.GetValue(view), Is.EqualTo(32));
            Assert.That(root.transform.childCount, Is.EqualTo(initialChildCount));
        }

        private static Action<T, float> CreateTypedCallback<T>(Action<object, float> callback)
        {
            return (value, size) => callback(value, size);
        }

        [Test]
        public void SetSizeMode_TogglesBetweenExpandedAndCompactWithoutRedrawingGridTexture()
        {
            object expandedMode = Enum.Parse(sizeModeType, "Expanded");
            object compactMode = Enum.Parse(sizeModeType, "Compact");
            var rootRect = (RectTransform)root.transform;

            Assert.That(sizeModeProp.GetValue(view), Is.EqualTo(expandedMode));
            Assert.That((float)expandedMapSizeProp.GetValue(view), Is.EqualTo(368f).Within(0.01f));
            Assert.That((float)compactMapSizeProp.GetValue(view), Is.EqualTo(210f).Within(0.01f));
            Assert.That((float)mapSizeProp.GetValue(view), Is.EqualTo(368f).Within(0.01f));
            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(368f, 368f)));

            int refreshBefore = RefreshCount;
            int childCountBefore = root.transform.childCount;
            int eventCount = 0;
            object lastEventMode = null;
            float lastEventSize = -1f;

            MethodInfo factory = typeof(ArenaMinimapViewTests)
                .GetMethod(nameof(CreateTypedCallback), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(sizeModeType);
            Delegate handler = (Delegate)factory.Invoke(null, new object[]
            {
                (Action<object, float>)((mode, size) =>
                {
                    eventCount++;
                    lastEventMode = mode;
                    lastEventSize = size;
                })
            });
            sizeModeChangedEvent.AddEventHandler(view, handler);

            try
            {
                setSizeModeMethod.Invoke(view, new[] { compactMode });
                Assert.That(sizeModeProp.GetValue(view), Is.EqualTo(compactMode));
                Assert.That((float)mapSizeProp.GetValue(view), Is.EqualTo(210f).Within(0.01f));
                Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(210f, 210f)));
                Assert.That(RefreshCount, Is.EqualTo(refreshBefore));
                Assert.That(root.transform.childCount, Is.EqualTo(childCountBefore));
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(lastEventMode, Is.EqualTo(compactMode));
                Assert.That(lastEventSize, Is.EqualTo(210f).Within(0.01f));

                // Redundant set must be a no-op.
                setSizeModeMethod.Invoke(view, new[] { compactMode });
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(RefreshCount, Is.EqualTo(refreshBefore));

                toggleSizeModeMethod.Invoke(view, null);
                Assert.That(sizeModeProp.GetValue(view), Is.EqualTo(expandedMode));
                Assert.That((float)mapSizeProp.GetValue(view), Is.EqualTo(368f).Within(0.01f));
                Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(368f, 368f)));
                Assert.That(RefreshCount, Is.EqualTo(refreshBefore));
                Assert.That(root.transform.childCount, Is.EqualTo(childCountBefore));
                Assert.That(eventCount, Is.EqualTo(2));
                Assert.That(lastEventMode, Is.EqualTo(expandedMode));
                Assert.That(lastEventSize, Is.EqualTo(368f).Within(0.01f));
            }
            finally
            {
                sizeModeChangedEvent.RemoveEventHandler(view, handler);
            }
        }

        [Test]
        public void SetSizeMode_Compact_RescalesAndRepositionsStaticDynamicAndPlayerMarkers()
        {
            object expandedMode = Enum.Parse(sizeModeType, "Expanded");
            object compactMode = Enum.Parse(sizeModeType, "Compact");

            var coins = new List<GridPosition> { new GridPosition(15, 12) };
            var warnings = new List<GridPosition> { new GridPosition(10, 15) };
            applyDynamicMarkersMethod.Invoke(view, new object[] { coins, warnings, 1 << 1, 0.5f });
            applyPlayerMethod.Invoke(view, new object[] { 0, true, new Vector3(4f, 0f, -3f), Vector3.right, true });
            applyPlayerMethod.Invoke(view, new object[] { 1, true, new Vector3(-5f, 0f, 5f), Vector3.forward, false });

            var spawnRect = (RectTransform)root.transform.Find("Spawn_1");
            var mineRect = (RectTransform)root.transform.Find("Mine_1");
            var coinRect = (RectTransform)root.transform.Find("Coin_0");
            var warningRect = (RectTransform)root.transform.Find("Warning_0");
            var player0Rect = (RectTransform)root.transform.Find("PlayerMarker_0");
            var player0Arrow = (RectTransform)root.transform.Find("PlayerMarker_0/Arrow");
            var player1Rect = (RectTransform)root.transform.Find("PlayerMarker_1");

            Assert.That(spawnRect, Is.Not.Null);
            Assert.That(mineRect, Is.Not.Null);
            Assert.That(coinRect, Is.Not.Null);
            Assert.That(warningRect, Is.Not.Null);
            Assert.That(player0Rect, Is.Not.Null);
            Assert.That(player0Arrow, Is.Not.Null);
            Assert.That(player1Rect, Is.Not.Null);

            Vector2 spawnPosExpanded = spawnRect.anchoredPosition;
            Vector2 spawnSizeExpanded = spawnRect.sizeDelta;
            Vector2 minePosExpanded = mineRect.anchoredPosition;
            Vector2 coinPosExpanded = coinRect.anchoredPosition;
            Vector2 coinSizeExpanded = coinRect.sizeDelta;
            Vector2 warningPosExpanded = warningRect.anchoredPosition;
            Vector2 player0PosExpanded = player0Rect.anchoredPosition;
            Vector2 player0SizeExpanded = player0Rect.sizeDelta;
            Vector2 player0ArrowSizeExpanded = player0Arrow.sizeDelta;
            Vector2 player1PosExpanded = player1Rect.anchoredPosition;

            float scale = 210f / 368f;
            int refreshBefore = RefreshCount;

            setSizeModeMethod.Invoke(view, new[] { compactMode });

            Assert.That(RefreshCount, Is.EqualTo(refreshBefore));
            Assert.That(spawnRect.anchoredPosition.x, Is.EqualTo(spawnPosExpanded.x * scale).Within(0.5f));
            Assert.That(spawnRect.anchoredPosition.y, Is.EqualTo(spawnPosExpanded.y * scale).Within(0.5f));
            Assert.That(spawnRect.sizeDelta.x, Is.EqualTo(spawnSizeExpanded.x * scale).Within(0.5f));
            Assert.That(mineRect.anchoredPosition.x, Is.EqualTo(minePosExpanded.x * scale).Within(0.5f));
            Assert.That(mineRect.anchoredPosition.y, Is.EqualTo(minePosExpanded.y * scale).Within(0.5f));
            Assert.That(coinRect.anchoredPosition.x, Is.EqualTo(coinPosExpanded.x * scale).Within(0.5f));
            Assert.That(coinRect.sizeDelta.x, Is.EqualTo(coinSizeExpanded.x * scale).Within(0.5f));
            Assert.That(warningRect.anchoredPosition.x, Is.EqualTo(warningPosExpanded.x * scale).Within(0.5f));
            Assert.That(player0Rect.anchoredPosition.x, Is.EqualTo(player0PosExpanded.x * scale).Within(0.5f));
            Assert.That(player0Rect.anchoredPosition.y, Is.EqualTo(player0PosExpanded.y * scale).Within(0.5f));
            Assert.That(player0Rect.sizeDelta.x, Is.EqualTo(player0SizeExpanded.x * scale).Within(0.5f));
            Assert.That(player0Arrow.sizeDelta.x, Is.LessThan(player0ArrowSizeExpanded.x));
            Assert.That(player1Rect.anchoredPosition.x, Is.EqualTo(player1PosExpanded.x * scale).Within(0.5f));

            setSizeModeMethod.Invoke(view, new[] { expandedMode });

            Assert.That(spawnRect.anchoredPosition.x, Is.EqualTo(spawnPosExpanded.x).Within(0.1f));
            Assert.That(spawnRect.sizeDelta.x, Is.EqualTo(spawnSizeExpanded.x).Within(0.1f));
            Assert.That(coinRect.anchoredPosition.x, Is.EqualTo(coinPosExpanded.x).Within(0.1f));
            Assert.That(player0Rect.anchoredPosition.x, Is.EqualTo(player0PosExpanded.x).Within(0.1f));
            Assert.That(player0Rect.sizeDelta.x, Is.EqualTo(player0SizeExpanded.x).Within(0.1f));
            Assert.That(player0Arrow.sizeDelta.x, Is.EqualTo(player0ArrowSizeExpanded.x).Within(0.1f));
        }

        [Test]
        public void ToggleSizeMode_RepeatedTogglesAreIdempotentAndPreserveActiveMarkerCounts()
        {
            var coins = new List<GridPosition> { new GridPosition(4, 4), new GridPosition(10, 10), new GridPosition(16, 16) };
            var warnings = new List<GridPosition> { new GridPosition(10, 5) };
            applyDynamicMarkersMethod.Invoke(view, new object[] { coins, warnings, 1 << 0, 0.25f });
            applyPlayerMethod.Invoke(view, new object[] { 0, true, Vector3.zero, Vector3.right, true });
            applyPlayerMethod.Invoke(view, new object[] { 2, true, new Vector3(3f, 0f, 3f), Vector3.back, false });

            int refreshBefore = RefreshCount;
            int childCountBefore = root.transform.childCount;

            for (int i = 0; i < 6; i++)
            {
                toggleSizeModeMethod.Invoke(view, null);
            }

            Assert.That(sizeModeProp.GetValue(view), Is.EqualTo(Enum.Parse(sizeModeType, "Expanded")));
            Assert.That((float)mapSizeProp.GetValue(view), Is.EqualTo(368f).Within(0.01f));
            Assert.That((int)activeCoinsProp.GetValue(view), Is.EqualTo(3));
            Assert.That((int)activeWarningsProp.GetValue(view), Is.EqualTo(1));
            Assert.That((bool)isPlayerVisibleMethod.Invoke(view, new object[] { 0 }), Is.True);
            Assert.That((bool)isPlayerVisibleMethod.Invoke(view, new object[] { 2 }), Is.True);
            Assert.That(RefreshCount, Is.EqualTo(refreshBefore));
            Assert.That(root.transform.childCount, Is.EqualTo(childCountBefore));
        }
    }
}
