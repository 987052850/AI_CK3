using System;
using UnityEngine;

namespace CK3Map
{
    [Serializable]
    public struct CK3RiverRecord
    {
        [SerializeField, InspectorName("河流类型标记")]
        private int type;

        [SerializeField, InspectorName("父河流索引")]
        private int parentRiverIndex;

        [SerializeField, InspectorName("路径起始索引")]
        private int pointStart;

        [SerializeField, InspectorName("路径点数量")]
        private int pointCount;

        public int Type => type;
        public int ParentRiverIndex => parentRiverIndex;
        public int PointStart => pointStart;
        public int PointCount => pointCount;

        public CK3RiverRecord(int sourceType, int parentIndex, int start, int count)
        {
            type = sourceType;
            parentRiverIndex = parentIndex;
            pointStart = start;
            pointCount = count;
        }
    }

    [Serializable]
    public struct CK3RiverPixelPoint
    {
        [SerializeField, InspectorName("像素 X")]
        private int x;

        [SerializeField, InspectorName("像素 Y")]
        private int y;

        [SerializeField, InspectorName("原版调色板索引")]
        private int paletteIndex;

        [SerializeField, InspectorName("原版河宽")]
        private float width;

        public int X => x;
        public int Y => y;
        public int PaletteIndex => paletteIndex;
        public float Width => width;

        public CK3RiverPixelPoint(int pixelX, int pixelY, byte index, float riverWidth)
        {
            x = pixelX;
            y = pixelY;
            paletteIndex = index;
            width = riverWidth;
        }
    }

    [CreateAssetMenu(fileName = "CK3原版位图河网", menuName = "CK3 地图/原版位图河网")]
    public sealed class CK3RiverNetworkData : ScriptableObject
    {
        [Header("原版输入")]
        [SerializeField, InspectorName("河流位图尺寸")]
        private Vector2Int bitmapSize;

        [SerializeField, InspectorName("宽度像素值数量")]
        private int widthPixelValueCount;

        [SerializeField, InspectorName("最小河宽")]
        private float minimumWidth;

        [SerializeField, InspectorName("最大河宽")]
        private float maximumWidth;

        [SerializeField, InspectorName("源点固定宽度")]
        private float sourceWidth;

        [Header("原版河网拓扑")]
        [SerializeField, InspectorName("河流记录")]
        private CK3RiverRecord[] rivers = Array.Empty<CK3RiverRecord>();

        [SerializeField, InspectorName("有序像素路径")]
        private CK3RiverPixelPoint[] points = Array.Empty<CK3RiverPixelPoint>();

        public Vector2Int BitmapSize => bitmapSize;
        public int WidthPixelValueCount => widthPixelValueCount;
        public float MinimumWidth => minimumWidth;
        public float MaximumWidth => maximumWidth;
        public float SourceWidth => sourceWidth;
        public CK3RiverRecord[] Rivers => rivers;
        public CK3RiverPixelPoint[] Points => points;

        public void ReplaceData(
            Vector2Int size,
            int widthValueCount,
            float widthMin,
            float widthMax,
            float fixedSourceWidth,
            CK3RiverRecord[] riverRecords,
            CK3RiverPixelPoint[] riverPoints)
        {
            bitmapSize = size;
            widthPixelValueCount = widthValueCount;
            minimumWidth = widthMin;
            maximumWidth = widthMax;
            sourceWidth = fixedSourceWidth;
            rivers = riverRecords ?? Array.Empty<CK3RiverRecord>();
            points = riverPoints ?? Array.Empty<CK3RiverPixelPoint>();
        }
    }
}
