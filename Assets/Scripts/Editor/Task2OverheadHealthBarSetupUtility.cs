#if UNITY_EDITOR
using System;
using EachOneCoin.Gameplay;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Editor
{
    public static class Task2OverheadHealthBarSetupUtility
    {
        private const string CharacterPrefabPath = "Assets/Prefabs/Characters/ArenaCharacter.prefab";
        private const string NameplateNodeName = "Character Nameplate";
        private const string HealthBarNodeName = "HealthBarRoot";

        private const float HealthBarHeight = 16f;
        private const float FrameThickness = 3f;
        private const float SegmentGap = 3f;

        private static readonly Color FrameColor = new Color(0.075f, 0.090f, 0.122f, 1f);
        private static readonly Color FullColor = new Color(0.20f, 0.82f, 0.35f, 1f);
        private static readonly Color DimColor = new Color(0.56f, 0.66f, 0.59f, 1f);
        private static readonly Color BrokenColor = new Color(0.88f, 0.24f, 0.24f, 1f);

        [MenuItem("EachOneCoin/Setup/Apply Task 2 Overhead HealthBar")]
        public static void RunBatchSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                const string playModeMessage = "[Task2OverheadHealthBarSetup] 请先退出 Play Mode 再执行资产配置脚本。";
                Debug.LogError(playModeMessage);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "无法在 Play Mode 下执行",
                        "请先停止运行游戏（退出 Play Mode），再执行 Task 2 头顶血条预制体配置菜单。",
                        "确定");
                }
                return;
            }

            Debug.Log("[Task2OverheadHealthBarSetup] Starting idempotent setup for ArenaCharacter overhead health bar...");

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException("Failed to load prefab at: " + CharacterPrefabPath);
            }

            try
            {
                var nameplate = prefabRoot.GetComponent<CharacterNameplate>();
                if (nameplate == null)
                {
                    throw new InvalidOperationException("ArenaCharacter.prefab is missing CharacterNameplate component.");
                }

                Transform nameplateCanvas = prefabRoot.transform.Find(NameplateNodeName);
                if (nameplateCanvas == null)
                {
                    throw new InvalidOperationException("ArenaCharacter.prefab is missing child: " + NameplateNodeName);
                }

                // Preserve Background & Name untouched; idempotently upsert HealthBarRoot.
                Transform existing = nameplateCanvas.Find(HealthBarNodeName);
                GameObject barObj;
                if (existing != null)
                {
                    barObj = existing.gameObject;
                }
                else
                {
                    barObj = new GameObject(
                        HealthBarNodeName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(UnityEngine.UI.Image),
                        typeof(SegmentedHealthBarView));
                    barObj.transform.SetParent(nameplateCanvas, false);
                }

                barObj.layer = nameplateCanvas.gameObject.layer;
                barObj.SetActive(true);
                barObj.transform.SetAsLastSibling();

                var barRect = barObj.GetComponent<RectTransform>();
                if (barRect == null)
                {
                    barRect = barObj.AddComponent<RectTransform>();
                }
                barRect.localRotation = Quaternion.identity;
                barRect.localScale = Vector3.one;
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(1f, 0f);
                barRect.pivot = new Vector2(0.5f, 1f);
                barRect.anchoredPosition = Vector2.zero;
                barRect.sizeDelta = new Vector2(0f, HealthBarHeight);

                var barImage = barObj.GetComponent<UnityEngine.UI.Image>();
                if (barImage == null)
                {
                    barImage = barObj.AddComponent<UnityEngine.UI.Image>();
                }
                barImage.sprite = null;
                barImage.type = UnityEngine.UI.Image.Type.Simple;
                barImage.preserveAspect = false;
                barImage.raycastTarget = false;
                barImage.color = FrameColor;

                var barView = barObj.GetComponent<SegmentedHealthBarView>();
                if (barView == null)
                {
                    barView = barObj.AddComponent<SegmentedHealthBarView>();
                }

                var barSo = new SerializedObject(barView);
                barSo.FindProperty("frameImage").objectReferenceValue = barImage;
                barSo.FindProperty("frameColor").colorValue = FrameColor;
                barSo.FindProperty("fullColor").colorValue = FullColor;
                barSo.FindProperty("dimColor").colorValue = DimColor;
                barSo.FindProperty("brokenColor").colorValue = BrokenColor;
                barSo.FindProperty("frameThickness").floatValue = FrameThickness;
                barSo.FindProperty("segmentGap").floatValue = SegmentGap;
                barSo.ApplyModifiedPropertiesWithoutUndo();

                var nameplateSo = new SerializedObject(nameplate);
                nameplateSo.FindProperty("healthBar").objectReferenceValue = barView;
                nameplateSo.FindProperty("fallbackHealthBarHeight").floatValue = HealthBarHeight;
                nameplateSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, CharacterPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Task2OverheadHealthBarSetup] Successfully saved HealthBarRoot on " + CharacterPrefabPath);
        }
    }
}
#endif
