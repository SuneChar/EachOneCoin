#if UNITY_EDITOR
using System;
using EachOneCoin.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EachOneCoin.Editor
{
    public static class Task1CosmeticAssetSetupUtility
    {
        private const string AppearancesDir = "Assets/Data/Characters/Appearances";
        private const string Slot0Path = AppearancesDir + "/Appearance_Slot0.asset";
        private const string Slot1Path = AppearancesDir + "/Appearance_Slot1.asset";
        private const string Slot2Path = AppearancesDir + "/Appearance_Slot2.asset";
        private const string Slot3Path = AppearancesDir + "/Appearance_Slot3.asset";
        private const string NonePath = AppearancesDir + "/Appearance_None.asset";
        private const string CatalogPath = AppearancesDir + "/DefaultAppearanceCatalog.asset";

        private const string BootUiDir = "Assets/Prefabs/UI/Boot";
        private const string StartPanelPrefabPath = BootUiDir + "/StartPanel.prefab";
        private const string LanRoomPanelPrefabPath = BootUiDir + "/LanRoomPanel.prefab";
        private const string WardrobePanelPrefabPath = BootUiDir + "/WardrobePanel.prefab";

        private const string BootScenePath = "Assets/Scenes/BootScene.unity";
        private const string ArenaScenePath = "Assets/Scenes/ArenaScene.unity";

        private const string FontAssetPath = "Assets/Media/Font/SMILEYSANS-OBLIQUE SDF.asset";
        private const string PortraitDefaultSpritePath = "Assets/Art/UI/HUD/PlayerStatus/PortraitDefault.asset";
        private const string PortraitMaskSpritePath = "Assets/Art/UI/HUD/PlayerStatus/PortraitCircleMask.asset";
        private const string PortraitFrameSpritePath = "Assets/Art/UI/HUD/PlayerStatus/PortraitCircleFrame.asset";

        private const string SportCapPath = "Assets/Art/CharacterAccessories/SportCap.png";
        private const string SportScarfPath = "Assets/Art/CharacterAccessories/SportScarf.png";
        private const string SportVisorPath = "Assets/Art/CharacterAccessories/SportVisor.png";
        private const string HeadsetPath = "Assets/Art/CharacterAccessories/Headset.png";
        private const string MedalSashPath = "Assets/Art/CharacterAccessories/MedalSash.png";

        private static readonly Color ButtonBlue = new Color(0.19f, 0.46f, 0.62f, 1f);
        private static readonly Color PanelDark = new Color(0.02f, 0.05f, 0.09f, 0.92f);
        private static readonly Color CardDark = new Color(0.07f, 0.11f, 0.17f, 0.95f);
        private static readonly Color FocusGold = new Color(1f, 0.8f, 0.25f, 1f);
        private static readonly Color[] FactionColors =
        {
            new Color(0.94f, 0.27f, 0.28f, 1f),
            new Color(0.25f, 0.51f, 0.95f, 1f),
            new Color(0.26f, 0.79f, 0.43f, 1f),
            new Color(0.94f, 0.70f, 0.19f, 1f)
        };

        [MenuItem("EachOneCoin/Setup/Apply Task 1 Cosmetic Assets")]
        public static void RunBatchSetup()
        {
            RunBatchSetupInternal(true, true);
        }

        internal static bool RunBatchSetupInternal(bool promptSaveCurrentScene, bool restorePreviousScene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Task1CosmeticAssetSetup] Cannot run asset setup while Unity is in Play Mode. Please exit Play Mode first.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("无法在运行模式下装配资产", "请先退出 Unity Play Mode，再执行资产装配菜单。", "确定");
                }
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string previousScenePath = activeScene.IsValid() ? activeScene.path : string.Empty;
            if (promptSaveCurrentScene && !Application.isBatchMode && activeScene.IsValid() && activeScene.isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[Task1CosmeticAssetSetup] Setup cancelled by user because the current scene has unsaved changes.");
                    return false;
                }
            }

            Debug.Log("[Task1CosmeticAssetSetup] Starting idempotent setup for Task 1 cosmetic assets...");

            try
            {
                EnsureFolderExists(AppearancesDir);
                EnsureFolderExists(BootUiDir);

                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                if (font == null)
                    throw new InvalidOperationException("Missing required TMP font asset at: " + FontAssetPath);

                Sprite sportCap = AssetDatabase.LoadAssetAtPath<Sprite>(SportCapPath);
                Sprite sportScarf = AssetDatabase.LoadAssetAtPath<Sprite>(SportScarfPath);
                Sprite sportVisor = AssetDatabase.LoadAssetAtPath<Sprite>(SportVisorPath);
                Sprite headset = AssetDatabase.LoadAssetAtPath<Sprite>(HeadsetPath);
                Sprite medalSash = AssetDatabase.LoadAssetAtPath<Sprite>(MedalSashPath);

                // 1-3. Appearance SOs & Catalog
                CharacterAppearanceProfile slot0 = UpsertAppearanceProfile(Slot0Path, "slot_0", "Slot 0", "运动帽围巾", sportCap);
                CharacterAppearanceProfile slot1 = UpsertAppearanceProfile(Slot1Path, "slot_1", "Slot 1", "遮阳帽", sportVisor);
                CharacterAppearanceProfile slot2 = UpsertAppearanceProfile(Slot2Path, "slot_2", "Slot 2", "运动耳机", headset);
                CharacterAppearanceProfile slot3 = UpsertAppearanceProfile(Slot3Path, "slot_3", "Slot 3", "荣誉绶带", medalSash);
                CharacterAppearanceProfile none = UpsertAppearanceProfile(NonePath, "none", string.Empty, "无配饰", null);

                CharacterAppearanceCatalog catalog = UpsertAppearanceCatalog(
                    CatalogPath,
                    new[] { slot0, slot1, slot2, slot3, none });

                // 4. WardrobePanel.prefab
                GameObject wardrobePrefab = UpsertWardrobePanelPrefab(font, sportScarf, sportCap, sportVisor, headset, medalSash);

                // 5. StartPanel.prefab & LanRoomPanel.prefab
                UpdateStartPanelPrefab(font);
                UpdateLanRoomPanelPrefab(font);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // 6. BootScene.unity & ArenaScene.unity
                UpdateBootScene(wardrobePrefab, catalog);
                UpdateArenaScene(catalog);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Task1CosmeticAssetSetup] Completed successfully: 5 Appearance SOs, DefaultAppearanceCatalog, WardrobePanel/StartPanel/LanRoomPanel prefabs, BootScene & ArenaScene wired.");
                return true;
            }
            finally
            {
                if (restorePreviousScene)
                {
                    RestoreSceneIfSaved(previousScenePath);
                }
            }
        }

        internal static void RestoreSceneIfSaved(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrEmpty(scenePath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return;
            }

            Scene current = SceneManager.GetActiveScene();
            if (!current.IsValid() || !string.Equals(current.path, scenePath, StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }

        private static CharacterAppearanceProfile UpsertAppearanceProfile(
            string assetPath,
            string appearanceId,
            string accessorySlotName,
            string displayName,
            Sprite previewIcon)
        {
            CharacterAppearanceProfile profile = AssetDatabase.LoadAssetAtPath<CharacterAppearanceProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CharacterAppearanceProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("appearanceId").stringValue = appearanceId;
            serialized.FindProperty("accessorySlotName").stringValue = accessorySlotName;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("previewIcon").objectReferenceValue = previewIcon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static CharacterAppearanceCatalog UpsertAppearanceCatalog(
            string assetPath,
            CharacterAppearanceProfile[] profiles)
        {
            CharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterAppearanceCatalog>(assetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterAppearanceCatalog>();
                AssetDatabase.CreateAsset(catalog, assetPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty listProp = serialized.FindProperty("profiles");
            listProp.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static GameObject UpsertWardrobePanelPrefab(
            TMP_FontAsset font,
            Sprite sportScarf,
            Sprite sportCap,
            Sprite sportVisor,
            Sprite headset,
            Sprite medalSash)
        {
            Sprite portraitDefault = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitDefaultSpritePath);
            Sprite portraitMask = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitMaskSpritePath);
            Sprite portraitFrame = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitFrameSpritePath);

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(WardrobePanelPrefabPath) != null;
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(WardrobePanelPrefabPath)
                : new GameObject("WardrobePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            try
            {
                root.name = "WardrobePanel";
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0) root.layer = uiLayer;
                RectTransform rootRect = EnsureRectTransform(root);
                SetStretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                Image rootBg = EnsureComponent<Image>(root);
                rootBg.sprite = null;
                rootBg.color = PanelDark;
                rootBg.raycastTarget = true;

                // Title
                TMP_Text title = EnsureLabel(root.transform, "Title", "角色换装", font, 42f, new Vector2(0f, 395f), new Vector2(600f, 70f));
                title.alignment = TextAlignmentOptions.Center;

                // Left-Center Preview Card
                GameObject previewCard = FindOrCreateChild(root.transform, "PreviewCard");
                RectTransform cardRect = EnsureRectTransform(previewCard);
                SetCentered(cardRect, new Vector2(-260f, 20f), new Vector2(460f, 520f));
                Image cardBg = EnsureComponent<Image>(previewCard);
                cardBg.sprite = null;
                cardBg.color = CardDark;
                cardBg.raycastTarget = false;

                // Circular Preview Portrait (300x300)
                GameObject portraitFrameRoot = FindOrCreateChild(previewCard.transform, "PortraitRoot");
                RectTransform portraitRootRect = EnsureRectTransform(portraitFrameRoot);
                SetCentered(portraitRootRect, new Vector2(0f, 85f), new Vector2(300f, 300f));

                GameObject maskObj = FindOrCreateChild(portraitFrameRoot.transform, "CircleMaskRoot");
                RectTransform maskRect = EnsureRectTransform(maskObj);
                SetStretch(maskRect, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                Image maskImg = EnsureComponent<Image>(maskObj);
                maskImg.sprite = portraitMask;
                maskImg.preserveAspect = true;
                maskImg.color = new Color(0.102f, 0.129f, 0.184f, 0.94f);
                maskImg.raycastTarget = false;
                Mask mask = EnsureComponent<Mask>(maskObj);
                mask.showMaskGraphic = true;

                GameObject bodyObj = FindOrCreateChild(maskObj.transform, "BodyPreview");
                RectTransform bodyRect = EnsureRectTransform(bodyObj);
                SetStretch(bodyRect, new Vector2(0.04f, 0f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);
                Image bodyImg = EnsureComponent<Image>(bodyObj);
                bodyImg.sprite = portraitDefault;
                bodyImg.preserveAspect = true;
                bodyImg.color = FactionColors[0];
                bodyImg.raycastTarget = false;

                GameObject accRootObj = FindOrCreateChild(maskObj.transform, "AccessoryPreviewRoot");
                RectTransform accRootRect = EnsureRectTransform(accRootObj);
                SetStretch(accRootRect, new Vector2(0.04f, 0f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);

                // 5 Normalized Accessory Preview Layers (aligned with PortraitDefault geometry)
                EnsureAccessoryLayer(accRootObj.transform, "Preview_SportScarf", sportScarf, new Vector2(0.382f, 0.292f), new Vector2(1.389f, 0.463f));
                EnsureAccessoryLayer(accRootObj.transform, "Preview_SportCap", sportCap, new Vector2(0.552f, 0.856f), new Vector2(1.205f, 0.803f));
                EnsureAccessoryLayer(accRootObj.transform, "Preview_SportVisor", sportVisor, new Vector2(0.537f, 0.704f), new Vector2(1.071f, 0.510f));
                EnsureAccessoryLayer(accRootObj.transform, "Preview_Headset", headset, new Vector2(0.475f, 0.679f), new Vector2(1.112f, 0.742f));
                EnsureAccessoryLayer(accRootObj.transform, "Preview_MedalSash", medalSash, new Vector2(0.544f, 0.269f), new Vector2(1.668f, 0.949f));

                GameObject frameObj = FindOrCreateChild(portraitFrameRoot.transform, "FrameImage");
                RectTransform frameRect = EnsureRectTransform(frameObj);
                SetStretch(frameRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Image frameImg = EnsureComponent<Image>(frameObj);
                frameImg.sprite = portraitFrame;
                frameImg.preserveAspect = true;
                frameImg.color = Color.white;
                frameImg.raycastTarget = false;

                EnsureLabel(previewCard.transform, "OutfitNameText", "运动帽围巾", font, 30f, new Vector2(0f, -100f), new Vector2(420f, 44f));
                TMP_Text indexLabel = EnsureLabel(previewCard.transform, "IndexIndicatorText", "1 / 5", font, 20f, new Vector2(0f, -138f), new Vector2(420f, 30f));
                indexLabel.color = new Color(0.76f, 0.83f, 0.90f, 1f);

                TMP_Text colorHint = EnsureLabel(previewCard.transform, "ColorHintLabel", "预览阵营色", font, 18f, new Vector2(0f, -174f), new Vector2(220f, 28f));
                colorHint.color = new Color(0.76f, 0.83f, 0.90f, 1f);

                // 4 Faction Preview Color Buttons
                GameObject colorRow = FindOrCreateChild(previewCard.transform, "ColorRow");
                RectTransform colorRowRect = EnsureRectTransform(colorRow);
                SetCentered(colorRowRect, new Vector2(0f, -218f), new Vector2(300f, 56f));
                for (int i = 0; i < 4; i++)
                {
                    float x = -99f + i * 66f;
                    EnsureColorSwatchButton(colorRow.transform, "ColorButton_" + i, FactionColors[i], new Vector2(x, 0f), i == 0);
                }

                // Right-Center Outfit Selector Column
                GameObject optionsCol = FindOrCreateChild(root.transform, "OptionsColumn");
                RectTransform optionsRect = EnsureRectTransform(optionsCol);
                SetCentered(optionsRect, new Vector2(260f, 20f), new Vector2(460f, 520f));
                Image optionsBg = EnsureComponent<Image>(optionsCol);
                optionsBg.sprite = null;
                optionsBg.color = CardDark;
                optionsBg.raycastTarget = false;

                EnsureStyledButton(optionsCol.transform, "PrevButton", "<", font, 28f, new Vector2(-170f, 215f), new Vector2(76f, 52f));
                EnsureLabel(optionsCol.transform, "OptionsHeader", "选择装扮", font, 28f, new Vector2(0f, 215f), new Vector2(240f, 52f));
                EnsureStyledButton(optionsCol.transform, "NextButton", ">", font, 28f, new Vector2(170f, 215f), new Vector2(76f, 52f));

                string[] defaultNames = { "运动帽围巾", "遮阳帽", "运动耳机", "荣誉绶带", "无配饰" };
                Sprite[] defaultIcons = { sportCap, sportVisor, headset, medalSash, null };
                for (int i = 0; i < 5; i++)
                {
                    float y = 128f - i * 78f;
                    EnsurePresetOptionButton(
                        optionsCol.transform,
                        "PresetButton_" + i,
                        defaultNames[i],
                        defaultIcons[i],
                        font,
                        new Vector2(0f, y),
                        new Vector2(416f, 64f));
                }

                // Bottom Back Button
                EnsureStyledButton(root.transform, "BackButton", "返回", font, 26f, new Vector2(0f, -350f), new Vector2(250f, 65f));

                PrefabUtility.SaveAsPrefabAsset(root, WardrobePanelPrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(WardrobePanelPrefabPath);
        }

        private static void UpdateStartPanelPrefab(TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(StartPanelPrefabPath);
            try
            {
                EnsureStyledButton(root.transform, "WardrobeButton", "换装", font, 26f, new Vector2(-300f, -30f), new Vector2(250f, 65f));
                PrefabUtility.SaveAsPrefabAsset(root, StartPanelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpdateLanRoomPanelPrefab(TMP_FontAsset font)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LanRoomPanelPrefabPath);
            try
            {
                EnsureStyledButton(root.transform, "WardrobeButton", "切换装扮", font, 26f, new Vector2(-521f, -230f), new Vector2(250f, 65f));
                PrefabUtility.SaveAsPrefabAsset(root, LanRoomPanelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpdateBootScene(GameObject wardrobePrefab, CharacterAppearanceCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            BootMenu bootMenu = FindInScene<BootMenu>(scene);
            if (bootMenu == null)
                throw new InvalidOperationException("BootMenu not found in " + BootScenePath);

            LanRoomUI lanRoom = FindInScene<LanRoomUI>(scene);
            Transform bootCanvas = bootMenu.transform;

            // Find or instantiate WardrobePanel under BootCanvas
            Transform wardrobeTransform = bootCanvas.Find("WardrobePanel");
            GameObject wardrobeInstance;
            if (wardrobeTransform == null)
            {
                wardrobeInstance = (GameObject)PrefabUtility.InstantiatePrefab(wardrobePrefab, bootCanvas);
                wardrobeInstance.name = "WardrobePanel";
            }
            else
            {
                wardrobeInstance = wardrobeTransform.gameObject;
            }

            RectTransform wardrobeRect = EnsureRectTransform(wardrobeInstance);
            SetStretch(wardrobeRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Keep ControlPrompts rendered above WardrobePanel
            Transform controlPrompts = bootCanvas.Find("ControlPrompts");
            if (controlPrompts != null)
            {
                int targetIndex = controlPrompts.GetSiblingIndex();
                if (wardrobeInstance.transform.GetSiblingIndex() > targetIndex)
                    wardrobeInstance.transform.SetSiblingIndex(targetIndex);
            }
            wardrobeInstance.SetActive(false);

            // Attach or reuse WardrobeUI on BootCanvas
            WardrobeUI wardrobeUi = bootCanvas.GetComponent<WardrobeUI>();
            if (wardrobeUi == null)
                wardrobeUi = bootCanvas.gameObject.AddComponent<WardrobeUI>();

            WireWardrobeUiReferences(wardrobeUi, wardrobeInstance, catalog);

            // Wire BootMenu
            Transform startPanel = bootCanvas.Find("StartPanel");
            Button startWardrobeButton = startPanel != null ? FindComponent<Button>(startPanel, "WardrobeButton") : null;
            if (startWardrobeButton == null)
                throw new InvalidOperationException("StartPanel/WardrobeButton not found in " + BootScenePath);
            var bootSerialized = new SerializedObject(bootMenu);
            AssignObjectProperty(bootSerialized, "wardrobe", wardrobeUi);
            AssignObjectProperty(bootSerialized, "wardrobeButton", startWardrobeButton);
            bootSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootMenu);

            // Wire LanRoomUI
            if (lanRoom != null)
            {
                Transform lanPanel = bootCanvas.Find("LanRoomPanel");
                Button lanWardrobeButton = lanPanel != null ? FindComponent<Button>(lanPanel, "WardrobeButton") : null;
                if (lanWardrobeButton == null)
                    throw new InvalidOperationException("LanRoomPanel/WardrobeButton not found in " + BootScenePath);
                var lanSerialized = new SerializedObject(lanRoom);
                AssignObjectProperty(lanSerialized, "appearanceCatalog", catalog);
                AssignObjectProperty(lanSerialized, "wardrobe", wardrobeUi);
                AssignObjectProperty(lanSerialized, "wardrobeButton", lanWardrobeButton);
                lanSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lanRoom);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save " + BootScenePath);
        }

        private static void UpdateArenaScene(CharacterAppearanceCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);
            MatchCoordinator coordinator = FindInScene<MatchCoordinator>(scene);
            if (coordinator == null)
                throw new InvalidOperationException("MatchCoordinator not found in " + ArenaScenePath);

            var serialized = new SerializedObject(coordinator);
            SerializedProperty catalogProp = serialized.FindProperty("appearanceCatalog");
            if (catalogProp == null)
                throw new InvalidOperationException("MatchCoordinator.appearanceCatalog property not found.");
            catalogProp.objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinator);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save " + ArenaScenePath);
        }

        private static void WireWardrobeUiReferences(
            WardrobeUI wardrobeUi,
            GameObject wardrobePanel,
            CharacterAppearanceCatalog catalog)
        {
            Transform root = wardrobePanel.transform;
            var so = new SerializedObject(wardrobeUi);
            so.FindProperty("panel").objectReferenceValue = wardrobePanel;
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("titleText").objectReferenceValue = FindComponent<TMP_Text>(root, "Title");
            so.FindProperty("outfitNameText").objectReferenceValue = FindComponent<TMP_Text>(root, "PreviewCard/OutfitNameText");
            so.FindProperty("indexIndicatorText").objectReferenceValue = FindComponent<TMP_Text>(root, "PreviewCard/IndexIndicatorText");
            so.FindProperty("circleBackdropImage").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot");
            so.FindProperty("circleFrameImage").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/FrameImage");
            so.FindProperty("bodyPreviewImage").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/BodyPreview");
            so.FindProperty("accessoryPreviewRoot").objectReferenceValue = FindComponent<RectTransform>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/AccessoryPreviewRoot");
            so.FindProperty("previewSportScarf").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/AccessoryPreviewRoot/Preview_SportScarf");
            so.FindProperty("previewSportCap").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/AccessoryPreviewRoot/Preview_SportCap");
            so.FindProperty("previewSportVisor").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/AccessoryPreviewRoot/Preview_SportVisor");
            so.FindProperty("previewHeadset").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/AccessoryPreviewRoot/Preview_Headset");
            so.FindProperty("previewMedalSash").objectReferenceValue = FindComponent<Image>(root, "PreviewCard/PortraitRoot/CircleMaskRoot/AccessoryPreviewRoot/Preview_MedalSash");
            so.FindProperty("prevButton").objectReferenceValue = FindComponent<Button>(root, "OptionsColumn/PrevButton");
            so.FindProperty("nextButton").objectReferenceValue = FindComponent<Button>(root, "OptionsColumn/NextButton");
            so.FindProperty("backButton").objectReferenceValue = FindComponent<Button>(root, "BackButton");

            SerializedProperty presetButtonsProp = so.FindProperty("presetButtons");
            SerializedProperty presetLabelsProp = so.FindProperty("presetLabels");
            SerializedProperty presetBgsProp = so.FindProperty("presetBackgrounds");
            SerializedProperty presetIconsProp = so.FindProperty("presetIcons");
            presetButtonsProp.arraySize = 5;
            presetLabelsProp.arraySize = 5;
            presetBgsProp.arraySize = 5;
            presetIconsProp.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                string p = "OptionsColumn/PresetButton_" + i;
                presetButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Button>(root, p);
                presetBgsProp.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Image>(root, p);
                presetLabelsProp.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<TMP_Text>(root, p + "/Label");
                presetIconsProp.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Image>(root, p + "/Icon");
            }

            SerializedProperty colorBtnsProp = so.FindProperty("colorPreviewButtons");
            SerializedProperty colorRingsProp = so.FindProperty("colorPreviewRings");
            colorBtnsProp.arraySize = 4;
            colorRingsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                string cp = "PreviewCard/ColorRow/ColorButton_" + i;
                colorBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Button>(root, cp);
                colorRingsProp.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Image>(root, cp + "/SelectedRing");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(wardrobeUi);
        }

        private static void EnsureAccessoryLayer(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 normalizedCenter,
            Vector2 normalizedSize)
        {
            GameObject obj = FindOrCreateChild(parent, name);
            RectTransform rt = EnsureRectTransform(obj);
            Vector2 half = normalizedSize * 0.5f;
            SetStretch(rt, normalizedCenter - half, normalizedCenter + half, Vector2.zero, Vector2.zero);
            Image img = EnsureComponent<Image>(obj);
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = false;
            img.raycastTarget = false;
        }

        private static void EnsureColorSwatchButton(
            Transform parent,
            string name,
            Color swatchColor,
            Vector2 position,
            bool initiallySelected)
        {
            GameObject obj = FindOrCreateChild(parent, name);
            RectTransform rt = EnsureRectTransform(obj);
            SetCentered(rt, position, new Vector2(48f, 48f));

            Image img = EnsureComponent<Image>(obj);
            img.sprite = null;
            img.color = swatchColor;
            img.raycastTarget = true;

            Button btn = EnsureComponent<Button>(obj);
            ConfigureButtonStyle(btn, img);
            EnsureFocusHighlight(obj);

            GameObject ringObj = FindOrCreateChild(obj.transform, "SelectedRing");
            RectTransform ringRt = EnsureRectTransform(ringObj);
            SetStretch(ringRt, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            Image ringImg = EnsureComponent<Image>(ringObj);
            ringImg.sprite = null;
            ringImg.color = new Color(1f, 0.94f, 0.55f, 0.45f);
            ringImg.raycastTarget = false;
            ringImg.enabled = initiallySelected;
        }

        private static void EnsurePresetOptionButton(
            Transform parent,
            string name,
            string labelText,
            Sprite iconSprite,
            TMP_FontAsset font,
            Vector2 position,
            Vector2 size)
        {
            GameObject obj = FindOrCreateChild(parent, name);
            RectTransform rt = EnsureRectTransform(obj);
            SetCentered(rt, position, size);

            Image bg = EnsureComponent<Image>(obj);
            bg.sprite = null;
            bg.color = ButtonBlue;
            bg.raycastTarget = true;

            Button btn = EnsureComponent<Button>(obj);
            ConfigureButtonStyle(btn, bg);
            EnsureFocusHighlight(obj);

            GameObject iconObj = FindOrCreateChild(obj.transform, "Icon");
            RectTransform iconRt = EnsureRectTransform(iconObj);
            SetCentered(iconRt, new Vector2(-162f, 0f), new Vector2(46f, 46f));
            Image iconImg = EnsureComponent<Image>(iconObj);
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
            iconImg.enabled = iconSprite != null;

            TMP_Text label = EnsureLabel(obj.transform, "Label", labelText, font, 26f, new Vector2(20f, 0f), new Vector2(310f, 54f));
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static Button EnsureStyledButton(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            GameObject obj = FindOrCreateChild(parent, name);
            RectTransform rt = EnsureRectTransform(obj);
            SetCentered(rt, position, size);

            Image img = EnsureComponent<Image>(obj);
            img.sprite = null;
            img.color = ButtonBlue;
            img.raycastTarget = true;

            Button btn = EnsureComponent<Button>(obj);
            ConfigureButtonStyle(btn, img);
            EnsureFocusHighlight(obj);

            TMP_Text label = EnsureLabel(obj.transform, "Label", text, font, fontSize, Vector2.zero, new Vector2(Mathf.Max(40f, size.x - 10f), Mathf.Max(30f, size.y - 5f)));
            label.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        private static TMP_Text EnsureLabel(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Vector2 position,
            Vector2 size)
        {
            GameObject obj = FindOrCreateChild(parent, name);
            RectTransform rt = EnsureRectTransform(obj);
            SetCentered(rt, position, size);

            TextMeshProUGUI tmp = EnsureComponent<TextMeshProUGUI>(obj);
            tmp.font = font;
            if (font != null && font.material != null)
                tmp.fontSharedMaterial = font.material;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void ConfigureButtonStyle(Button button, Image targetGraphic)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = targetGraphic;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.8f, 0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
            colors.selectedColor = new Color(1f, 0.85f, 0.45f, 1f);
            colors.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void EnsureFocusHighlight(GameObject target)
        {
            UIFocusHighlight highlight = EnsureComponent<UIFocusHighlight>(target);
            var so = new SerializedObject(highlight);
            so.FindProperty("borderColor").colorValue = FocusGold;
            so.FindProperty("borderWidth").floatValue = 4f;
            so.FindProperty("padding").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.layer = parent.gameObject.layer;
                return existing.gameObject;
            }
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void AssignObjectProperty(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty prop = serialized.FindProperty(propertyName);
            if (prop == null)
                throw new InvalidOperationException("Missing serialized property: " + propertyName);
            prop.objectReferenceValue = value;
        }

        private static RectTransform EnsureRectTransform(GameObject obj)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            return rt != null ? rt : obj.AddComponent<RectTransform>();
        }

        private static T EnsureComponent<T>(GameObject obj) where T : Component
        {
            T existing = obj.GetComponent<T>();
            return existing != null ? existing : obj.AddComponent<T>();
        }

        private static T FindComponent<T>(Transform root, string path) where T : Component
        {
            Transform target = root.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetCentered(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private static void SetStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }
}
#endif
