using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArenaGridCoordinateTests
    {
        [Test]
        public void PaintedTile_UsesTransparentBlendAndPartialOpacity()
        {
            var root = new GameObject("Arena transparency test");
            try
            {
                var type = Type.GetType("EachOneCoin.Gameplay.ArenaGridView, Assembly-CSharp", true);
                var grid = root.AddComponent(type);
                var tiles = new GameObject("Tiles");
                tiles.transform.SetParent(root.transform);
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.SetParent(tiles.transform);
                type.GetField("tileRoot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, tiles.transform);
                type.GetField("overlayRoot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, root.transform);
                type.GetMethod("Initialize").Invoke(grid, null);
                var model = new GridModel(21, 21, new[] { new GridPosition(10, 10) });
                type.GetMethod("Refresh").Invoke(grid, new object[] { model });

                var overlay = root.transform.Find("Color Overlay 10,10").GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                overlay.GetPropertyBlock(block);
                Assert.That(overlay.enabled, Is.True);
                Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(0.55f).Within(0.001f));
                Assert.That(overlay.sharedMaterial.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(overlay.sharedMaterial.GetInt("_SrcBlend"), Is.EqualTo((int)UnityEngine.Rendering.BlendMode.SrcAlpha));
                Assert.That(overlay.sharedMaterial.GetInt("_DstBlend"), Is.EqualTo((int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                Assert.That(overlay.sharedMaterial.GetInt("_ZWrite"), Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(-10f, -10f, 0, 0)]
        [TestCase(0f, 0f, 10, 10)]
        [TestCase(10f, 10f, 20, 20)]
        [TestCase(100f, -100f, 20, 0)]
        public void WorldToCell_MapsArenaAndClampsEdges(float x, float z, int expectedX, int expectedY)
        {
            var root = new GameObject("Arena grid coordinate test");
            try
            {
                var type = Type.GetType("EachOneCoin.Gameplay.ArenaGridView, Assembly-CSharp", true);
                var grid = root.AddComponent(type);
                var cell = (GridPosition)type.GetMethod("WorldToCell", BindingFlags.Public | BindingFlags.Instance).Invoke(grid, new object[] { new Vector3(x, 0f, z) });
                Assert.That(cell.X, Is.EqualTo(expectedX));
                Assert.That(cell.Y, Is.EqualTo(expectedY));
                var center = (Vector3)type.GetMethod("CellToWorld", BindingFlags.Public | BindingFlags.Instance).Invoke(grid, new object[] { cell });
                Assert.That(center.x, Is.EqualTo(expectedX - 10f).Within(0.001f));
                Assert.That(center.z, Is.EqualTo(expectedY - 10f).Within(0.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
