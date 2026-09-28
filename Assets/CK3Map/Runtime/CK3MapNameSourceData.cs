using System;
using TMPro;
using UnityEngine;

namespace CK3Map
{
    [Serializable]
    public struct CK3MapNameRegion
    {
        [SerializeField, InspectorName("原版名称模式键")] private string groupKey;
        [SerializeField, InspectorName("本地化显示名称")] private string displayName;
        [SerializeField, InspectorName("连通域省份编号")] private int[] provinceIds;
        [SerializeField, InspectorName("原版像素包围范围")] private RectInt pixelBounds;
        [SerializeField, InspectorName("原版像素数量")] private int pixelCount;

        public string GroupKey => groupKey;
        public string DisplayName => displayName;
        public int[] ProvinceIds => provinceIds;
        public RectInt PixelBounds => pixelBounds;
        public int PixelCount => pixelCount;

        public CK3MapNameRegion(string key, string name, int[] provinces, RectInt bounds, int pixels)
        {
            groupKey = key;
            displayName = name;
            provinceIds = provinces;
            pixelBounds = bounds;
            pixelCount = pixels;
        }
    }

    [CreateAssetMenu(fileName = "CK3原版地图名称源数据", menuName = "CK3 地图/原版地图名称源数据")]
    public sealed class CK3MapNameSourceData : ScriptableObject
    {
        [SerializeField, InspectorName("名称模式")] private string nameMode = "dejure_kingdoms";
        [SerializeField, InspectorName("原版地图字体")] private TMP_FontAsset mapFont;
        [SerializeField, InspectorName("法理王国连通名称区域")]
        private CK3MapNameRegion[] deJureKingdomRegions = Array.Empty<CK3MapNameRegion>();

        [SerializeField, InspectorName("实际领地连通名称区域")]
        private CK3MapNameRegion[] actualRealmRegions = Array.Empty<CK3MapNameRegion>();

        public string NameMode => nameMode;
        public TMP_FontAsset MapFont => mapFont;
        public CK3MapNameRegion[] DeJureKingdomRegions => deJureKingdomRegions;
        public CK3MapNameRegion[] ActualRealmRegions => actualRealmRegions;

        public void ReplaceData(
            string mode,
            TMP_FontAsset font,
            CK3MapNameRegion[] deJureRegions,
            CK3MapNameRegion[] actualRegions)
        {
            nameMode = mode;
            mapFont = font;
            deJureKingdomRegions = deJureRegions;
            actualRealmRegions = actualRegions;
        }
    }
}
