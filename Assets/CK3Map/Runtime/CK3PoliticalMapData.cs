using UnityEngine;

namespace CK3Map
{
    [CreateAssetMenu(fileName = "CK3政治地图数据", menuName = "CK3 地图/政治地图数据")]
    public sealed class CK3PoliticalMapData : ScriptableObject
    {
        [SerializeField, InspectorName("省份编号间接寻址图")]
        [Tooltip("对应 CK3 的 JominiProvinceColorIndirection；RG 分别保存省份编号的低、高 8 位。")]
        private Texture2D provinceIdIndirection;

        [SerializeField, InspectorName("法理王国颜色表")]
        [Tooltip("对应 CK3 dejure_kingdoms color_mode 的 256×256 ProvinceColorTexture。")]
        private Texture2D deJureKingdomPalette;

        [SerializeField, InspectorName("实际领地颜色表")]
        [Tooltip("按 CK3 title history 在 1066.9.15 解析的 realms 颜色表。")]
        private Texture2D actualRealmPalette;

        [SerializeField, InspectorName("法理王国边界距离场")]
        [Tooltip("由 CK3 原版 Jomini 无符号距离场流程生成，分辨率为省份图的四分之一。")]
        private Texture2D deJureKingdomDistanceField;

        [SerializeField, InspectorName("实际领地边界距离场")]
        [Tooltip("由 CK3 原版 Jomini 无符号距离场流程生成，分辨率为省份图的四分之一。")]
        private Texture2D actualRealmDistanceField;

        [SerializeField, InspectorName("法理王国无水岸边界距离场")]
        private Texture2D deJureKingdomDistanceFieldWithoutShoreline;

        [SerializeField, InspectorName("实际领地无水岸边界距离场")]
        private Texture2D actualRealmDistanceFieldWithoutShoreline;

        [SerializeField, InspectorName("实际地图日期")]
        private string actualMapDate = "1066.9.15";

        public Texture2D ProvinceIdIndirection => provinceIdIndirection;
        public Texture2D DeJureKingdomPalette => deJureKingdomPalette;
        public Texture2D ActualRealmPalette => actualRealmPalette;
        public Texture2D DeJureKingdomDistanceField => deJureKingdomDistanceField;
        public Texture2D ActualRealmDistanceField => actualRealmDistanceField;
        public Texture2D DeJureKingdomDistanceFieldWithoutShoreline => deJureKingdomDistanceFieldWithoutShoreline;
        public Texture2D ActualRealmDistanceFieldWithoutShoreline => actualRealmDistanceFieldWithoutShoreline;
        public string ActualMapDate => actualMapDate;

        public void ReplaceData(
            Texture2D indirection,
            Texture2D deJure,
            Texture2D actual,
            Texture2D deJureDistance,
            Texture2D actualDistance,
            Texture2D deJureDistanceWithoutShoreline,
            Texture2D actualDistanceWithoutShoreline,
            string date)
        {
            provinceIdIndirection = indirection;
            deJureKingdomPalette = deJure;
            actualRealmPalette = actual;
            deJureKingdomDistanceField = deJureDistance;
            actualRealmDistanceField = actualDistance;
            deJureKingdomDistanceFieldWithoutShoreline = deJureDistanceWithoutShoreline;
            actualRealmDistanceFieldWithoutShoreline = actualDistanceWithoutShoreline;
            actualMapDate = date;
        }
    }
}
