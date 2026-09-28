using UnityEngine;

namespace CK3Map
{
    [ExecuteAlways]
    public sealed class CK3TabletopVisibility : MonoBehaviour
    {
        [SerializeField, InspectorName("桌面显示起始档位")]
        [Tooltip("game/gfx/map/map_object_data/layers.txt：map_table_layer_western fade_in = 21。")]
        private int fadeInZoomStep = 21;

        private bool lastVisible = true;

        private void OnEnable()
        {
            ApplyVisibility();
        }

        private void Update()
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            // Static tabletop assets remain visible and selectable in Edit mode.
            bool visible = true;
            if (Application.isPlaying)
            {
                CK3StrategicMapCamera strategicCamera = Camera.main != null
                    ? Camera.main.GetComponent<CK3StrategicMapCamera>()
                    : null;
                visible = strategicCamera == null || strategicCamera.VisualZoomStep >= fadeInZoomStep;
            }
            if (visible == lastVisible && Application.isPlaying)
                return;
            lastVisible = visible;
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
                renderer.enabled = visible;
        }
    }
}
