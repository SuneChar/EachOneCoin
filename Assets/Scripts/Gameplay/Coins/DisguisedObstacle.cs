using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // The same visual as a coin, with a separate, one-shot hazard interaction.
    public sealed class DisguisedObstacle : MonoBehaviour
    {
        private CoinSpawner owner;
        private Renderer[] renderers;
        private MaterialPropertyBlock[] originalBlocks;
        private MaterialPropertyBlock blackBlock;
        public GridPosition Cell { get; private set; }
        public bool Revealed { get; private set; }
        public bool Consumed { get; private set; }
        public Collider ContactCollider { get; private set; }

        public void Initialize(CoinSpawner spawner, GridPosition cell)
        {
            owner = spawner;
            Cell = cell;
            renderers = GetComponentsInChildren<Renderer>(true);
            originalBlocks = new MaterialPropertyBlock[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(originalBlocks[i]);
            }
            blackBlock = new MaterialPropertyBlock();
            blackBlock.SetColor("_BaseColor", Color.black);
            blackBlock.SetColor("_Color", Color.black);
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            trigger.radius = 0.4f / scale;
            ContactCollider = trigger;
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public void SetRevealed(bool value)
        {
            if (Consumed || Revealed == value) return;
            Revealed = value;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(value ? blackBlock : originalBlocks[i]);
        }

        public void Expire()
        {
            if (Consumed) return;
            Consumed = true;
            if (ContactCollider != null) ContactCollider.enabled = false;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
        }

        public bool TryConsume()
        {
            if (Consumed) return false;
            Expire();
            owner.RemoveDisguisedObstacle(this);
            return true;
        }
    }
}
