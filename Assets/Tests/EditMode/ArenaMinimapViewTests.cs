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
        private PropertyInfo refreshCountProp;
        private PropertyInfo focusedSlotProp;
        private PropertyInfo activeCoinsProp;
        private PropertyInfo activeWarningsProp;

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            viewType = Type.GetType("EachOneCoin.Gameplay.ArenaMinimapView, Assembly-CSharp", true);
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

            refreshCountProp = viewType.GetProperty("TextureRefreshCount");
            focusedSlotProp = viewType.GetProperty("FocusedPlayerSlot");
            activeCoinsProp = viewType.GetProperty("ActiveCoinMarkerCount");
            activeWarningsProp = viewType.GetProperty("ActiveWarningMarkerCount");

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
    }
}
