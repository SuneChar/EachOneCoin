using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArenaGridCoordinateTests
    {
        [TestCase(-9.5f, -9.5f, 0, 0)]
        [TestCase(0.5f, -0.5f, 10, 9)]
        [TestCase(9.5f, 9.5f, 19, 19)]
        [TestCase(100f, -100f, 19, 0)]
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
                Assert.That(center.x, Is.EqualTo(expectedX - 9.5f).Within(0.001f));
                Assert.That(center.z, Is.EqualTo(expectedY - 9.5f).Within(0.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
