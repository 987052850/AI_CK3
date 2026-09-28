using System;
using UnityEngine;

namespace CK3Map
{
    public enum CK3TitleRank : byte
    {
        帝国,
        王国,
        公国,
        伯爵领,
        男爵领,
        霸权
    }

    public enum CK3TitleColorEncoding : byte
    {
        未指定,
        RGB255,
        HSV01,
        HSV360
    }

    [Serializable]
    public struct CK3DeJureTitle
    {
        [SerializeField, InspectorName("头衔键名")] private string key;
        [SerializeField, InspectorName("头衔等级")] private CK3TitleRank rank;
        [SerializeField, InspectorName("父头衔索引")] private int parentIndex;
        [SerializeField, InspectorName("关联省份编号")] private int provinceId;
        [SerializeField, InspectorName("首都头衔键名")] private string capitalKey;
        [SerializeField, InspectorName("颜色编码")] private CK3TitleColorEncoding colorEncoding;
        [SerializeField, InspectorName("原始颜色数值")] private Vector3 colorValues;

        public string Key => key;
        public CK3TitleRank Rank => rank;
        public int ParentIndex => parentIndex;
        public int ProvinceId => provinceId;
        public string CapitalKey => capitalKey;
        public CK3TitleColorEncoding ColorEncoding => colorEncoding;
        public Vector3 ColorValues => colorValues;

        public CK3DeJureTitle(
            string titleKey,
            CK3TitleRank titleRank,
            int parent,
            int province,
            string capital,
            CK3TitleColorEncoding encoding,
            Vector3 rawColor)
        {
            key = titleKey;
            rank = titleRank;
            parentIndex = parent;
            provinceId = province;
            capitalKey = capital;
            colorEncoding = encoding;
            colorValues = rawColor;
        }
    }

    [CreateAssetMenu(fileName = "CK3原版法理头衔层级", menuName = "CK3 地图/原版法理头衔层级")]
    public sealed class CK3DeJureTitleHierarchy : ScriptableObject
    {
        [SerializeField, InspectorName("法理头衔记录")]
        private CK3DeJureTitle[] titles = Array.Empty<CK3DeJureTitle>();

        [Header("按省份编号直接索引")]
        [SerializeField, InspectorName("男爵领头衔索引")] private int[] baronyByProvince = Array.Empty<int>();
        [SerializeField, InspectorName("伯爵领头衔索引")] private int[] countyByProvince = Array.Empty<int>();
        [SerializeField, InspectorName("公国头衔索引")] private int[] duchyByProvince = Array.Empty<int>();
        [SerializeField, InspectorName("王国头衔索引")] private int[] kingdomByProvince = Array.Empty<int>();
        [SerializeField, InspectorName("帝国头衔索引")] private int[] empireByProvince = Array.Empty<int>();

        public CK3DeJureTitle[] Titles => titles;
        public int[] BaronyByProvince => baronyByProvince;
        public int[] CountyByProvince => countyByProvince;
        public int[] DuchyByProvince => duchyByProvince;
        public int[] KingdomByProvince => kingdomByProvince;
        public int[] EmpireByProvince => empireByProvince;

        public void ReplaceData(
            CK3DeJureTitle[] titleRecords,
            int[] baronies,
            int[] counties,
            int[] duchies,
            int[] kingdoms,
            int[] empires)
        {
            titles = titleRecords;
            baronyByProvince = baronies;
            countyByProvince = counties;
            duchyByProvince = duchies;
            kingdomByProvince = kingdoms;
            empireByProvince = empires;
        }
    }
}
