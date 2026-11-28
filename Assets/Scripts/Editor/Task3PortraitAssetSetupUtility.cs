#if UNITY_EDITOR
using System;
using EachOneCoin.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EachOneCoin.Editor
{
    public static class Task3PortraitAssetSetupUtility
    {
        private const string AppearancesDir = "Assets/Data/Characters/Appearances";
        private const string Slot0Path = AppearancesDir + "/Appearance_Slot0.asset";
        private const string Slot1Path = AppearancesDir + "/Appearance_Slot1.asset";
        private const string Slot2Path = AppearancesDir + "/Appearance_Slot2.asset";
        private const string Slot3Path = AppearancesDir + "/Appearance_Slot3.asset";
        private const string NonePath = AppearancesDir + "/Appearance_None.asset";

        private const string SportScarfPath = "Assets/Art/CharacterAccessories/SportScarf.png";
        private const string SportCapPath = "Assets/Art/CharacterAccessories/SportCap.png";
        private const string SportVisorPath = "Assets/Art/CharacterAccessories/SportVisor.png";
        private const string HeadsetPath = "Assets/Art/CharacterAccessories/Headset.png";
        private const string MedalSashPath = "Assets/Art/CharacterAccessories/MedalSash.png";

        private const string ParticipantHudSlotPrefabPath = "Assets/Prefabs/UI/Arena/ParticipantHudSlot.prefab";
        private const string OverlayContainerPath = "AvatarRoot/AvatarMask/OverlayContainer";

        [MenuItem("EachOneCoin/Setup/Apply Task 3 Portrait Cosmetic Assets")]
        public static void RunBatchSetup()
        {
            if (!EnsureNotInPlayMode("[Task3PortraitAssetSetup]"))
            {
                return;
            }

            Debug.Log("[Task3PortraitAssetSetup] Starting idempotent setup for portrait overlay layers and ParticipantHudSlot anchor...");

            Sprite sportScarf = LoadRequiredSprite(SportScarfPath);
            Sprite sportCap = LoadRequiredSprite(SportCapPath);
            Sprite sportVisor = LoadRequiredSprite(SportVisorPath);
            Sprite headset = LoadRequiredSprite(HeadsetPath);
            Sprite medalSash = LoadRequiredSprite(MedalSashPath);

            ConfigureAppearanceProfileLayers(Slot0Path, true, new[]
            {
                CreateLayer(sportScarf, new Vector2(0.382f, 0.292f), new Vector2(1.389f, 0.463f)),
                CreateLayer(sportCap, new Vector2(0.552f, 0.856f), new Vector2(1.205f, 0.803f))
            });

            ConfigureAppearanceProfileLayers(Slot1Path, true, new[]
            {
                CreateLayer(sportVisor, new Vector2(0.537f, 0.704f), new Vector2(1.071f, 0.510f))
            });

            ConfigureAppearanceProfileLayers(Slot2Path, true, new[]
            {
                CreateLayer(headset, new Vector2(0.475f, 0.679f), new Vector2(1.112f, 0.742f))
            });

            ConfigureAppearanceProfileLayers(Slot3Path, true, new[]
            {
                CreateLayer(medalSash, new Vector2(0.544f, 0.269f), new Vector2(1.668f, 0.949f))
            });

            ConfigureAppearanceProfileLayers(NonePath, false, Array.Empty<PortraitOverlayLayer>());

            SyncParticipantHudSlotOverlayAnchor();

            AssetDatabase.SaveAssets();
            Debug.Log("[Task3PortraitAssetSetup] Completed successfully: Appearance_Slot0~3/None PortraitOverlayLayers & ParticipantHudSlot OverlayContainer anchor updated.");
        }

        [MenuItem("EachOneCoin/Setup/Apply All 4 Tasks Assets (1+4+2+3)")]
        public static void RunAllFourTasksBatchSetup()
        {
            if (!EnsureNotInPlayMode("[EachOneCoinSetup]"))
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string previousScenePath = activeScene.IsValid() ? activeScene.path : string.Empty;
            if (!Application.isBatchMode && activeScene.IsValid() && activeScene.isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[EachOneCoinSetup] Unified setup cancelled by user because the current scene has unsaved changes.");
                    return;
                }
            }

            Debug.Log("[EachOneCoinSetup] Starting unified batch setup for Tasks 1, 4, 2, and 3...");
            bool completed = false;
            try
            {
                if (!Task1CosmeticAssetSetupUtility.RunBatchSetupInternal(false, false))
                {
                    return;
                }

                Task4ControlPromptsSetupUtility.RunBatchSetup();
                Task2OverheadHealthBarSetupUtility.RunBatchSetup();
                RunBatchSetup();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                completed = true;
            }
            finally
            {
                Task1CosmeticAssetSetupUtility.RestoreSceneIfSaved(previousScenePath);
            }

            if (completed)
            {
                Scene finalScene = SceneManager.GetActiveScene();
                string finalPath = finalScene.IsValid() ? finalScene.path : string.Empty;
                string bootHint = string.Equals(finalPath, "Assets/Scenes/BootScene.unity", StringComparison.Ordinal)
                    ? "当前已停留在 BootScene，可直接运行体验主界面与房间换装。"
                    : "当前场景为 '" + (string.IsNullOrEmpty(finalPath) ? "Untitled" : finalPath) + "'；如需体验主界面与房间换装入口，请打开 Assets/Scenes/BootScene.unity 后运行。";
                Debug.Log("[EachOneCoinSetup] Completed all 4 task asset setups successfully (Task 1 Cosmetics + Task 4 Minimap/ControlPrompts + Task 2 Overhead HealthBar + Task 3 Portrait Overlays). " + bootHint);
            }
        }

        private static bool EnsureNotInPlayMode(string logPrefix)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return true;
            }

            Debug.LogError(logPrefix + " Cannot run asset setup while Unity is in Play Mode. Please exit Play Mode first.");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("无法在运行模式下装配资产", "请先退出 Unity Play Mode，再执行资产装配菜单。", "确定");
            }
            return false;
        }

        private static Sprite LoadRequiredSprite(string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Missing required accessory Sprite at: " + assetPath);
            }

            return sprite;
        }

        private static PortraitOverlayLayer CreateLayer(Sprite sprite, Vector2 center, Vector2 size)
        {
            return new PortraitOverlayLayer
            {
                Sprite = sprite,
                NormalizedCenter = center,
                NormalizedSize = size,
                RotationZ = 0f,
                UseFactionTint = false
            };
        }

        private static void ConfigureAppearanceProfileLayers(
            string assetPath,
            bool required,
            PortraitOverlayLayer[] layers)
        {
            CharacterAppearanceProfile profile = AssetDatabase.LoadAssetAtPath<CharacterAppearanceProfile>(assetPath);
            if (profile == null)
            {
                if (required)
                {
                    throw new InvalidOperationException("Missing required CharacterAppearanceProfile asset at: " + assetPath);
                }

                Debug.LogWarning("[Task3PortraitAssetSetup] Optional asset not found yet (run Task 1 setup first): " + assetPath);
                return;
            }

            var serialized = new SerializedObject(profile);
            SerializedProperty layersProp = serialized.FindProperty("portraitOverlayLayers");
            if (layersProp == null)
            {
                throw new InvalidOperationException("CharacterAppearanceProfile is missing serialized property 'portraitOverlayLayers'.");
            }

            int count = layers != null ? layers.Length : 0;
            layersProp.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = layersProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Sprite").objectReferenceValue = layers[i].Sprite;
                element.FindPropertyRelative("NormalizedCenter").vector2Value = layers[i].NormalizedCenter;
                element.FindPropertyRelative("NormalizedSize").vector2Value = layers[i].NormalizedSize;
                element.FindPropertyRelative("RotationZ").floatValue = layers[i].RotationZ;
                element.FindPropertyRelative("UseFactionTint").boolValue = layers[i].UseFactionTint;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void SyncParticipantHudSlotOverlayAnchor()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ParticipantHudSlotPrefabPath);
            if (prefabRoot == null)
            {
                throw new InvalidOperationException("Failed to load prefab at: " + ParticipantHudSlotPrefabPath);
            }

            try
            {
                Transform overlayTransform = prefabRoot.transform.Find(OverlayContainerPath);
                if (overlayTransform == null)
                {
                    throw new InvalidOperationException(
                        "ParticipantHudSlot.prefab is missing child at path: " + OverlayContainerPath);
                }

                var overlayRect = overlayTransform as RectTransform;
                if (overlayRect == null)
                {
                    throw new InvalidOperationException("OverlayContainer is missing RectTransform.");
                }

                AvatarPortraitVisualRenderer.AlignOverlayContainer(overlayRect);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ParticipantHudSlotPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
#endif
