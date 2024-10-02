using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject {

    [SerializeField] Shader shader = default;


    [System.NonSerialized] Material material;
    // 可在面板查看的，可调节成员数值的结构体。
    [System.Serializable]
    public struct BloomSettings
    {

        [Range(0f, 16f)] public int maxIterations;

        [Min(1f)] public int downscaleLimit;

        // 判断是否开启双三次上采样
        public bool bicubicUpsampling;

        [Min(0f)] public float threshold;

        [Range(0f, 1f)] public float thresholdKnee;

        [Min(0f)] public float intensity;
    }



    [SerializeField] BloomSettings bloom = default;

    public BloomSettings Bloom => bloom;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }
}