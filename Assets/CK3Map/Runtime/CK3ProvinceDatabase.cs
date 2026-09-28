using System;
using UnityEngine;

namespace CK3Map
{
    [Flags]
    public enum CK3ProvinceCategory : byte
    {
        普通陆地 = 0,
        海区 = 1 << 0,
        大河省份 = 1 << 1,
        湖泊 = 1 << 2,
        不可通行山地 = 1 << 3,
        不可通行海域 = 1 << 4
    }

    [Serializable]
    public struct CK3ProvinceDefinition
    {
        [SerializeField, InspectorName("省份编号")] private int id;
        [SerializeField, InspectorName("原版颜色")] private Color32 color;
        [SerializeField, InspectorName("原版名称")] private string name;

        public int Id => id;
        public Color32 Color => color;
        public string Name => name;

        public CK3ProvinceDefinition(int provinceId, Color32 provinceColor, string provinceName)
        {
            id = provinceId;
            color = provinceColor;
            name = provinceName;
        }
    }

    [Serializable]
    public struct CK3ProvinceAdjacency
    {
        [SerializeField, InspectorName("起点省份")] private int from;
        [SerializeField, InspectorName("终点省份")] private int to;
        [SerializeField, InspectorName("连接类型")] private string type;
        [SerializeField, InspectorName("经过省份")] private int through;
        [SerializeField, InspectorName("起点像素")] private Vector2Int start;
        [SerializeField, InspectorName("终点像素")] private Vector2Int stop;
        [SerializeField, InspectorName("原版备注")] private string comment;

        public int From => from;
        public int To => to;
        public string Type => type;
        public int Through => through;
        public Vector2Int Start => start;
        public Vector2Int Stop => stop;
        public string Comment => comment;

        public CK3ProvinceAdjacency(
            int source,
            int destination,
            string adjacencyType,
            int throughProvince,
            Vector2Int startPixel,
            Vector2Int stopPixel,
            string sourceComment)
        {
            from = source;
            to = destination;
            type = adjacencyType;
            through = throughProvince;
            start = startPixel;
            stop = stopPixel;
            comment = sourceComment;
        }
    }

    [CreateAssetMenu(fileName = "CK3原版省份数据库", menuName = "CK3 地图/原版省份数据库")]
    public sealed class CK3ProvinceDatabase : ScriptableObject
    {
        [Header("原版省份图")]
        [SerializeField, InspectorName("省份颜色编号图")]
        [Tooltip("game/map_data/provinces.png 的完整 9216×4608 RGB 颜色编号图。")]
        private Texture2D provinceColorTexture;

        [SerializeField, InspectorName("省份图尺寸")]
        private Vector2Int textureSize;

        [Header("definition.csv")]
        [SerializeField, InspectorName("最大省份编号")]
        private int maxProvinceId;

        [SerializeField, InspectorName("省份定义")]
        private CK3ProvinceDefinition[] definitions = Array.Empty<CK3ProvinceDefinition>();

        [Header("default.map")]
        [SerializeField, InspectorName("按编号排列的省份分类")]
        [Tooltip("数组下标就是省份编号；位标记来自 default.map 的 sea_zones、river_provinces、lakes、impassable_mountains 和 impassable_seas。")]
        private byte[] categoriesByProvinceId = Array.Empty<byte>();

        [Header("adjacencies.csv")]
        [SerializeField, InspectorName("特殊相邻关系")]
        private CK3ProvinceAdjacency[] adjacencies = Array.Empty<CK3ProvinceAdjacency>();

        public Texture2D ProvinceColorTexture => provinceColorTexture;
        public Vector2Int TextureSize => textureSize;
        public int MaxProvinceId => maxProvinceId;
        public CK3ProvinceDefinition[] Definitions => definitions;
        public byte[] CategoriesByProvinceId => categoriesByProvinceId;
        public CK3ProvinceAdjacency[] Adjacencies => adjacencies;

        public CK3ProvinceCategory GetCategory(int provinceId)
        {
            return provinceId >= 0 && provinceId < categoriesByProvinceId.Length
                ? (CK3ProvinceCategory)categoriesByProvinceId[provinceId]
                : CK3ProvinceCategory.普通陆地;
        }

        public void ReplaceData(
            Texture2D colorTexture,
            Vector2Int size,
            int maximumId,
            CK3ProvinceDefinition[] provinceDefinitions,
            byte[] categories,
            CK3ProvinceAdjacency[] specialAdjacencies)
        {
            provinceColorTexture = colorTexture;
            textureSize = size;
            maxProvinceId = maximumId;
            definitions = provinceDefinitions;
            categoriesByProvinceId = categories;
            adjacencies = specialAdjacencies;
        }
    }
}
