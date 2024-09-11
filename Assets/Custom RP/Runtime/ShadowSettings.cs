using UnityEngine;

// 单纯用来存放阴影配置选项的容器
[System.Serializable]
public class ShadowSettings
{
    // maxDistance决定视野内多大范围会被渲染到阴影贴图上，距离主摄像机超过maxDistance的物体不会渲染
    // 具体逻辑如下
    // 1.根据maxDistance来得到一个BoundingBox，这个BoundingBox容纳了所有要渲染的物体
    // 2.根据这个BoundingBox和方向光源的方向，确定渲染阴影贴图用的正交摄像机的视椎体，渲染阴影贴图
    [Min(0f)]
    public float maxDistance = 100f;

    // 阴影贴图的所有尺寸，使用枚举防止出现其他数值，范围为256-8192
    public enum MapSize {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    // 定义方向光源的阴影贴图配置
    [System.Serializable]
    public struct Directional
    {
        public MapSize atlasSize;
    }

    // 创建一个1024大小的Directional Shadow Map
    public Directional directional = new Directional
    {
        atlasSize = MapSize._1024
    };

}

