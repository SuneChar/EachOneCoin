using UnityEngine;
using UnityEngine.Rendering;

namespace EachOneCoin.Gameplay
{
    // A separate object avoids inheriting the owner's rotation or scale.
    public sealed class GroundShadow : MonoBehaviour
    {
        private Transform shadow;
        private MatchCoordinator match;

        public static GroundShadow Create(GameObject owner, float size, MatchCoordinator coordinator = null)
        {
            var sprite = Resources.Load<Sprite>("GroundShadow");
            if (sprite == null) return null;
            var component = owner.AddComponent<GroundShadow>();
            component.match = coordinator;
            var instance = new GameObject("Ground Shadow", typeof(SpriteRenderer));
            instance.transform.SetParent(owner.transform.parent, false);
            component.shadow = instance.transform;
            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            component.shadow.rotation = Quaternion.Euler(90f, 0f, 0f);
            component.shadow.localScale = Vector3.one * size;
            component.UpdatePosition();
            return component;
        }

        public void SetVisible(bool visible)
        {
            if (shadow != null) shadow.gameObject.SetActive(visible);
        }

        private void LateUpdate() => UpdatePosition();

        private void UpdatePosition()
        {
            if (shadow == null) return;
            var position = transform.position;
            float ground = match != null && match.Grid != null ? match.Grid.SurfaceHeightAt(position) : 0f;
            shadow.position = new Vector3(position.x, ground + 0.04f, position.z);
        }

        private void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }
    }
}
