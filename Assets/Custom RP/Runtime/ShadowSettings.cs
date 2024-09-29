using UnityEngine;

// �������������Ӱ����ѡ�������
[System.Serializable]
public class ShadowSettings
{
    // maxDistance������Ұ�ڶ��Χ�ᱻ��Ⱦ����Ӱ��ͼ�ϣ����������������maxDistance�����岻����Ⱦ
    // �����߼�����
    // 1.����maxDistance���õ�һ��BoundingBox�����BoundingBox����������Ҫ��Ⱦ������
    // 2.�������BoundingBox�ͷ����Դ�ķ���ȷ����Ⱦ��Ӱ��ͼ�õ��������������׵�壬��Ⱦ��Ӱ��ͼ
    [Min(0.001f)]
    public float maxDistance = 100f;
    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;




    // ��Ӱ��ͼ�����гߴ磬ʹ��ö�ٷ�ֹ����������ֵ����ΧΪ256-8192
    public enum MapSize {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    public enum FilterMode {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7
    }

    // ���巽���Դ����Ӱ��ͼ����
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

    // ����һ��1024��С��Directional Shadow Map
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

    // ����������Դ����Ӱ��ͼ����
    public struct Other
    {
        public MapSize atlasSize;
        public FilterMode filter;
    }
    // ���췽����1024��size
    public Other other = new Other
    {
        atlasSize = MapSize._1024,
        filter = FilterMode.PCF2x2
    };

}

