using UnityEngine;

// 单纯用来存放阴影配置选项的容器
[System.Serializable]
public class ShadowSettings
{
    // maxDistance决定视野内多大范围会被渲染到阴影贴图上，距离主摄像机超过maxDistance的物体不会渲染
    // 具体逻辑如下
    // 1.根据maxDistance来得到一个BoundingBox，这个BoundingBox容纳了所有要渲染的物体
    // 2.根据这个BoundingBox和方向光源的方向，确定渲染阴影贴图用的正交摄像机的视椎体，渲染阴影贴图
    [Min(0.001f)]
    public float maxDistance = 100f;
    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;




    // 阴影贴图的所有尺寸，使用枚举防止出现其他数值，范围为256-8192
    public enum MapSize {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    public enum FilterMode {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7
    }

    // 定义方向光源的阴影贴图配置
    [System.Serializable]
    public struct Directional
    {
        public MapSize atlasSize;

        public FilterMode filter;

        [Range(1, 4)]
        public int cascadeCount;

        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

        [Range(0.001f, 1f)]
        public float cascadeFade;

        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }

        public CascadeBlendMode cascadeBlend;
    }

    // 创建一个1024大小的Directional Shadow Map
    public Directional directional = new Directional
    {
        atlasSize = MapSize._1024,
        filter = FilterMode.PCF2x2,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        cascadeBlend = Directional.CascadeBlendMode.Hard
    };

    // 定义其它光源的阴影贴图配置
    public struct Other
    {
        public MapSize atlasSize;
        public FilterMode filter;
    }
    // 构造方法，1024的size
    public Other other = new Other
    {
        atlasSize = MapSize._1024,
        filter = FilterMode.PCF2x2
    };

}

