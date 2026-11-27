using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EachOneCoin.EditorTools
{
    // Builds an isolated experiment; it does not modify the gameplay scene.
    public static class BuildChibiPrototype
    {
        private const string Folder = "Assets/CharacterAnimationPrototype";
        private const string TexturePath = Folder + "/ChibiParts.png";
        private const float Ppu = 200f;

        [MenuItem("Tools/Each One Coin/Build Chibi Animation Prototype")]
        public static void Build()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer == null) throw new InvalidOperationException("Chibi parts image was not imported.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritesheet = new[]
            {
                Part("Head", 20, 10, 600, 590, new Vector2(.56f, .03f)),
                Part("Torso", 670, 185, 235, 410, new Vector2(.5f, .88f)),
                Part("ArmBack", 1270, 120, 145, 190, new Vector2(.5f, .9f)),
                Part("ForearmBack", 1270, 320, 145, 300, new Vector2(.5f, .94f)),
                Part("ArmFront", 1000, 120, 150, 190, new Vector2(.5f, .9f)),
                Part("ForearmFront", 1000, 320, 150, 300, new Vector2(.5f, .94f)),
                Part("ThighBack", 550, 650, 180, 275, new Vector2(.5f, .92f)),
                Part("ShinBack", 800, 650, 135, 365, new Vector2(.5f, .94f)),
                Part("ThighFront", 90, 650, 180, 275, new Vector2(.5f, .92f)),
                Part("ShinFront", 340, 650, 145, 365, new Vector2(.5f, .94f)),
                Part("ShoeBack", 1270, 795, 225, 150, new Vector2(.25f, .8f)),
                Part("ShoeFront", 1000, 795, 225, 150, new Vector2(.25f, .8f))
            };
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().ToDictionary(s => s.name);
            var root = new GameObject("ChibiAnimationPrototype");
            try
            {
                var body = Node(root.transform, "Body", sprites["Torso"], Vector3.zero, 0);
                Node(body, "Head", sprites["Head"], new Vector3(0, .05f, 0), 2);
                var armBack = Node(body, "ArmBack", sprites["ArmBack"], new Vector3(-.04f, -.25f, 0), -2);
                Node(armBack, "Forearm", sprites["ForearmBack"], new Vector3(0, -.68f, 0), -2);
                var armFront = Node(body, "ArmFront", sprites["ArmFront"], new Vector3(-.04f, -.25f, 0), 3);
                Node(armFront, "Forearm", sprites["ForearmFront"], new Vector3(0, -.68f, 0), 3);
                var legBack = Node(body, "LegBack", sprites["ThighBack"], new Vector3(.08f, -1.62f, 0), -3);
                var shinBack = Node(legBack, "Shin", sprites["ShinBack"], new Vector3(0, -1.08f, 0), -3);
                Node(shinBack, "Shoe", sprites["ShoeBack"], new Vector3(0, -1.55f, 0), -3);
                var legFront = Node(body, "LegFront", sprites["ThighFront"], new Vector3(.08f, -1.62f, 0), 1);
                var shinFront = Node(legFront, "Shin", sprites["ShinFront"], new Vector3(0, -1.08f, 0), 1);
                Node(shinFront, "Shoe", sprites["ShoeFront"], new Vector3(0, -1.55f, 0), 1);

                var idle = CreateClip("Idle", 1.2f, false);
                var run = CreateClip("Run", .6f, true);
                var controllerPath = Folder + "/Prototype.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (var state in machine.states) machine.RemoveState(state.state);
                var idleState = machine.AddState("Idle"); idleState.motion = idle;
                var runState = machine.AddState("Run"); runState.motion = run;
                if (!controller.parameters.Any(p => p.name == "Moving")) controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
                var toRun = idleState.AddTransition(runState); toRun.hasExitTime = false; toRun.duration = .08f; toRun.AddCondition(AnimatorConditionMode.If, 0, "Moving");
                var toIdle = runState.AddTransition(idleState); toIdle.hasExitTime = false; toIdle.duration = .08f; toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
                var animator = root.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ChibiAnimationPrototype.prefab");
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static SpriteMetaData Part(string name, int x, int y, int width, int height, Vector2 pivot)
        {
            return new SpriteMetaData { name = name, rect = new Rect(x, 1024 - y - height, width, height), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
        }

        private static Transform Node(Transform parent, string name, Sprite sprite, Vector3 position, int order)
        {
            var node = new GameObject(name, typeof(SpriteRenderer)); node.transform.SetParent(parent, false); node.transform.localPosition = position;
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
            return node.transform;
        }

        private static AnimationClip CreateClip(string name, float duration, bool moving)
        {
            var path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.frameRate = 30;
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
            var d = duration;
            Set(clip, "Body", "m_LocalPosition.y", moving ? new[] { 0f, .09f, 0f, .09f, 0f } : new[] { 0f, .035f, 0f, .035f, 0f }, d);
            Rotate(clip, "Body/ArmFront", moving ? 34 : 3, d); Rotate(clip, "Body/ArmBack", moving ? -34 : -3, d);
            Rotate(clip, "Body/LegFront", moving ? -30 : 0, d); Rotate(clip, "Body/LegBack", moving ? 30 : 0, d);
            Rotate(clip, "Body/LegFront/Shin", moving ? 15 : 0, d); Rotate(clip, "Body/LegBack/Shin", moving ? -15 : 0, d);
            EditorUtility.SetDirty(clip); return clip;
        }

        private static void Rotate(AnimationClip clip, string path, float angle, float duration)
        {
            Set(clip, path, "localEulerAnglesRaw.z", new[] { angle, 0f, -angle, 0f, angle }, duration);
        }

        private static void Set(AnimationClip clip, string path, string property, float[] values, float duration)
        {
            var keys = new Keyframe[values.Length];
            for (var i = 0; i < values.Length; i++) keys[i] = new Keyframe(duration * i / (values.Length - 1), values[i]);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys));
        }
    }
}
