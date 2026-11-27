using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class SpecialTilePrefabTests
    {
        [TestCase("FixedCoinTile", 4, "Assets/Materials/FixedCoinTile.mat")]
        [TestCase("CentralMineTile", 6, "Assets/Materials/CentralMineTile.mat")]
        public void SpecialTileVariant_PreservesBaseMeshAndDecoration(string name, int childCount, string materialPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/" + name + ".prefab");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(asset)),
                Is.EqualTo("Assets/Prefabs/World/ArenaTile.prefab"));
            var mesh = asset.GetComponent<MeshFilter>();
            Assert.That(mesh, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Is.EqualTo("Assets/Media/FBX format/tile-spawn.fbx"));
            Assert.That(asset.transform.childCount, Is.EqualTo(childCount));
            foreach (Transform child in asset.transform)
            {
                var renderer = child.GetComponent<Renderer>();
                Assert.That(renderer, Is.Not.Null, child.name);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(materialPath), child.name);
            }
        }
    }
}
