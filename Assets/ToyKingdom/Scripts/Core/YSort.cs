using UnityEngine;

namespace ToyKingdom
{
    /// <summary>Hareket eden objelerin çizim sırasını her karede derinliğe göre günceller.</summary>
    public class YSort : MonoBehaviour
    {
        public int bias = 5;
        [Tooltip("Obje zeminden bu kadar yukarıdaysa (sur/kule üstü) sıralama zemindeki noktasına göre yapılır")]
        public float heightOffset;
        public SpriteRenderer[] renderers;
        int[] offsets;

        void Awake() => Collect();

        public void Collect()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<SpriteRenderer>(true);
            offsets = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) offsets[i] = renderers[i] != null ? renderers[i].sortingOrder : 0;
        }

        void LateUpdate()
        {
            int order = Iso.Order(transform.position - Vector3.up * heightOffset, bias);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sortingOrder = order + offsets[i];
        }
    }
}
