using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using EachOneCoin.Gameplay;

namespace EachOneCoin.Editor
{
    public static class Task4ControlPromptsSetupUtility
    {
        private const string ControlPromptsPrefabPath = "Assets/Prefabs/UI/Common/ControlPrompts.prefab";
        private const string ArenaMinimapPrefabPath = "Assets/Prefabs/UI/Arena/ArenaMinimap.prefab";
        private const string ParticipantHudStripPrefabPath = "Assets/Prefabs/UI/Arena/ParticipantHudStrip.prefab";

        private const string KeyboardMTexturePath = "Assets/Media/kenney_input-prompts_1.5/Keyboard & Mouse/Default/keyboard_m.png";
        private const string XboxViewTexturePath = "Assets/Media/kenney_input-prompts_1.5/Xbox Series/Default/xbox_button_view.png";
        private const string FallbackFontPath = "Assets/Media/Font/SMILEYSANS-OBLIQUE SDF.asset";

        private const float DefaultCompactMapSize = 210f;
        private const float IconPreferredSize = 38f;
        private const float MinimapLabelPreferredWidth = 84f;
        private const float MinimapLabelPreferredHeight = 38f;

        [MenuItem("EachOneCoin/Setup/Apply Task 4 ControlPrompts & Minimap")]
        public static void RunBatchSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Task4ControlPromptsSetupUtility] Cannot run setup during Play Mode. Exit Play Mode and try again.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("EachOneCoin Setup", "请先退出 Play Mode 后再执行资产装配菜单。", "确定");
                }
                return;
            }

            ConfigureControlPromptsPrefab();
            ConfigureArenaMinimapPrefab();
            ConfigureParticipantHudStripPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Task4ControlPromptsSetupUtility] Task 4 ControlPrompts, ArenaMinimap, and ParticipantHudStrip prefabs updated idempotently.");
        }

        private static void ConfigureControlPromptsPrefab()
        {
            Texture2D keyboardMTex = AssetDatabase.LoadAssetAtPath<Texture2D>(KeyboardMTexturePath);
            Texture2D xboxViewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(XboxViewTexturePath);
            if (keyboardMTex == null)
            {
                throw new System.InvalidOperationException("Missing keyboard M icon texture at: " + KeyboardMTexturePath);
            }
            if (xboxViewTex == null)
            {
                throw new System.InvalidOperationException("Missing Xbox View icon texture at: " + XboxViewTexturePath);
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ControlPromptsPrefabPath);
            if (prefabRoot == null)
            {
                throw new System.InvalidOperationException("Failed to load prefab: " + ControlPromptsPrefabPath);
            }

            try
            {
                ControlPromptImages promptImages = prefabRoot.GetComponent<ControlPromptImages>();
                if (promptImages == null)
                {
                    throw new System.InvalidOperationException("ControlPrompts.prefab is missing ControlPromptImages component.");
                }

                Transform promptRow = prefabRoot.transform.Find("Prompt Row");
                if (promptRow == null)
                {
                    throw new System.InvalidOperationException("ControlPrompts.prefab is missing 'Prompt Row' child.");
                }

                TMP_FontAsset font = ResolvePromptFont(prefabRoot, promptRow);

                GameObject minimapIconObj = FindOrCreateChild(promptRow, "Minimap Icon");
                RectTransform iconRect = EnsureComponent<RectTransform>(minimapIconObj);
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(IconPreferredSize, IconPreferredSize);
                EnsureComponent<CanvasRenderer>(minimapIconObj);
                RawImage minimapIcon = EnsureComponent<RawImage>(minimapIconObj);
                minimapIcon.texture = keyboardMTex;
                minimapIcon.color = Color.white;
                minimapIcon.raycastTarget = false;
                LayoutElement iconLayout = EnsureComponent<LayoutElement>(minimapIconObj);
                iconLayout.ignoreLayout = false;
                iconLayout.minWidth = -1f;
                iconLayout.minHeight = -1f;
                iconLayout.preferredWidth = IconPreferredSize;
                iconLayout.preferredHeight = IconPreferredSize;
                iconLayout.flexibleWidth = -1f;
                iconLayout.flexibleHeight = -1f;
                iconLayout.layoutPriority = 1;

                GameObject minimapLabelObj = FindOrCreateChild(promptRow, "Minimap Label");
                RectTransform labelRect = EnsureComponent<RectTransform>(minimapLabelObj);
                labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(MinimapLabelPreferredWidth, MinimapLabelPreferredHeight);
                EnsureComponent<CanvasRenderer>(minimapLabelObj);
                TextMeshProUGUI minimapLabel = EnsureComponent<TextMeshProUGUI>(minimapLabelObj);
                if (font != null)
                {
                    minimapLabel.font = font;
                    if (font.material != null)
                    {
                        minimapLabel.fontSharedMaterial = font.material;
                    }
                }
                minimapLabel.text = "小地图";
                minimapLabel.fontSize = 24f;
                minimapLabel.fontStyle = FontStyles.Normal;
                minimapLabel.fontWeight = FontWeight.Regular;
                minimapLabel.alignment = TextAlignmentOptions.Center;
                minimapLabel.color = Color.white;
                minimapLabel.enableAutoSizing = false;
                minimapLabel.enableWordWrapping = false;
                minimapLabel.overflowMode = TextOverflowModes.Overflow;
                minimapLabel.raycastTarget = false;

                LayoutElement labelLayout = EnsureComponent<LayoutElement>(minimapLabelObj);
                labelLayout.ignoreLayout = false;
                labelLayout.minWidth = -1f;
                labelLayout.minHeight = -1f;
                labelLayout.preferredWidth = MinimapLabelPreferredWidth;
                labelLayout.preferredHeight = MinimapLabelPreferredHeight;
                labelLayout.flexibleWidth = -1f;
                labelLayout.flexibleHeight = -1f;
                labelLayout.layoutPriority = 1;

                // Place right after Action Label (and before Back Icon / Back Label) so gameplay row reads:
                // [Move] 移动  [Pause] 暂停  [M / View] 小地图
                Transform actionLabel = promptRow.Find("Action Label");
                if (actionLabel != null)
                {
                    minimapIconObj.transform.SetAsLastSibling();
                    minimapLabelObj.transform.SetAsLastSibling();
                    int afterActionIndex = actionLabel.GetSiblingIndex() + 1;
                    minimapIconObj.transform.SetSiblingIndex(afterActionIndex);
                    minimapLabelObj.transform.SetSiblingIndex(afterActionIndex + 1);
                }
                else
                {
                    minimapIconObj.transform.SetAsLastSibling();
                    minimapLabelObj.transform.SetAsLastSibling();
                }

                SerializedObject serialized = new SerializedObject(promptImages);
                AssignObjectProperty(serialized, "keyboardM", keyboardMTex);
                AssignObjectProperty(serialized, "xboxView", xboxViewTex);
                AssignObjectProperty(serialized, "minimapIcon", minimapIcon);
                AssignObjectProperty(serialized, "minimapLabel", minimapLabel);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ControlPromptsPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureArenaMinimapPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ArenaMinimapPrefabPath);
            if (prefabRoot == null)
            {
                throw new System.InvalidOperationException("Failed to load prefab: " + ArenaMinimapPrefabPath);
            }

            try
            {
                ArenaMinimapView view = prefabRoot.GetComponent<ArenaMinimapView>();
                if (view == null)
                {
                    throw new System.InvalidOperationException("ArenaMinimap.prefab is missing ArenaMinimapView component.");
                }

                SerializedObject serialized = new SerializedObject(view);
                SetFloatProperty(serialized, "compactMapSize", DefaultCompactMapSize);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ArenaMinimapPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureParticipantHudStripPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ParticipantHudStripPrefabPath);
            if (prefabRoot == null)
            {
                throw new System.InvalidOperationException("Failed to load prefab: " + ParticipantHudStripPrefabPath);
            }

            try
            {
                ParticipantHudStripView view = prefabRoot.GetComponent<ParticipantHudStripView>();
                if (view == null)
                {
                    throw new System.InvalidOperationException("ParticipantHudStrip.prefab is missing ParticipantHudStripView component.");
                }

                SerializedObject serialized = new SerializedObject(view);
                SetFloatProperty(serialized, "verticalStripWidth", 180f);
                SetFloatProperty(serialized, "verticalRowHeight", 70f);
                SetFloatProperty(serialized, "verticalAvatarSize", 60f);
                SetFloatProperty(serialized, "verticalHealthBarHeight", 12f);
                SetFloatProperty(serialized, "verticalSlotSpacing", 6f);
                SetFloatProperty(serialized, "verticalPadding", 4f);
                SetFloatProperty(serialized, "verticalMetricWidth", 68f);
                SetFloatProperty(serialized, "verticalAvatarMetricGap", 8f);
                SetFloatProperty(serialized, "verticalMetricCrownGap", 6f);
                SerializedProperty crownSizeProp = serialized.FindProperty("verticalCrownSize");
                if (crownSizeProp != null)
                {
                    crownSizeProp.vector2Value = new Vector2(32f, 25f);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ParticipantHudStripPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static TMP_FontAsset ResolvePromptFont(GameObject prefabRoot, Transform promptRow)
        {
            Transform actionLabel = promptRow.Find("Action Label");
            if (actionLabel != null)
            {
                TMP_Text existing = actionLabel.GetComponent<TMP_Text>();
                if (existing != null && existing.font != null)
                {
                    return existing.font;
                }
            }

            TMP_Text rootText = prefabRoot.GetComponent<TMP_Text>();
            if (rootText != null && rootText.font != null)
            {
                return rootText.font;
            }

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject created = new GameObject(childName, typeof(RectTransform));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        private static void AssignObjectProperty(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty prop = serialized.FindProperty(propertyName);
            if (prop == null)
            {
                throw new System.InvalidOperationException("Missing serialized property: " + propertyName);
            }
            prop.objectReferenceValue = value;
        }

        private static void SetFloatProperty(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty prop = serialized.FindProperty(propertyName);
            if (prop != null)
            {
                prop.floatValue = value;
            }
        }
    }
}
