using System;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class GamepadNavigationTests
    {
        [Test]
        public void Configure_SkipsInputFieldsDisabledButtonsAndControlsOutsidePage()
        {
            var navigationType = Type.GetType("GamepadNavigation, Assembly-CSharp", true);
            var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
            var inputType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro", true);
            var page = new GameObject("page", typeof(RectTransform));
            var outside = new GameObject("outside", typeof(RectTransform));
            try
            {
                Component CreateControl(string name, Type type, Transform parent, float y)
                {
                    var go = new GameObject(name, typeof(RectTransform));
                    go.transform.SetParent(parent, false);
                    go.transform.position = new Vector3(0f, y, 0f);
                    return go.AddComponent(type);
                }
                var top = CreateControl("top", buttonType, page.transform, 30f);
                var input = CreateControl("input", inputType, page.transform, 20f);
                var bottom = CreateControl("bottom", buttonType, page.transform, 10f);
                CreateControl("outsideButton", buttonType, outside.transform, 25f);
                var configure = navigationType.GetMethod("Configure");
                object Navigation(Component c) => c.GetType().GetProperty("navigation").GetValue(c);
                object Down(Component c) => Navigation(c).GetType().GetProperty("selectOnDown").GetValue(Navigation(c));

                configure.Invoke(null, new object[] { page });
                Assert.That(Down(top), Is.SameAs(bottom));
                Assert.That(Navigation(input).GetType().GetProperty("mode").GetValue(Navigation(input)).ToString(), Is.EqualTo("None"));

                buttonType.GetProperty("interactable").SetValue(bottom, false);
                configure.Invoke(null, new object[] { page });
                Assert.That(Down(top), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(page);
                UnityEngine.Object.DestroyImmediate(outside);
            }
        }
    }
}
