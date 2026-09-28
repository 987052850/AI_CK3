using UnityEngine;

namespace CK3Map
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CK3StrategicMapCamera : MonoBehaviour
    {
        private static readonly float[] ZoomDistances =
        {
            70, 90, 114, 142, 174, 210, 250, 295, 344, 396, 453, 513, 576, 643, 713,
            787, 865, 948, 1036, 1130, 1233, 1345, 1470, 1609, 1768, 1949, 2159, 2406,
            2699, 3050, 3477, 4000, 4649, 5464, 6500
        };

        private static readonly float[] ZoomTilts =
        {
            50, 53, 56, 59, 62, 65, 67, 70, 72, 74, 76, 77, 79, 80, 82, 83, 83, 84,
            85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 85, 70
        };

        [Header("视角")]
        [SerializeField, InspectorName("观察中心")]
        private Vector3 lookAt = new Vector3(5000.0f, 0.0f, 2300.0f);

        [SerializeField, InspectorName("初始观察中心")]
        private Vector3 startLookAt = new Vector3(5000.0f, 0.0f, 2300.0f);

        [SerializeField, InspectorName("当前缩放档位")]
        [Range(0, 34)]
        private int zoomStep = 33;

        [SerializeField, InspectorName("初始缩放档位"), Range(0, 34)]
        private int startZoomStep = 33;

        [SerializeField, InspectorName("缩放距离倍率"), Min(0.01f)]
        private float zoomDistanceMultiplier = 1.0f;

        [SerializeField, InspectorName("倾角修正")]
        [Tooltip("在原版每档倾角上追加的角度。正值更垂直，负值更倾斜。")]
        private float tiltOffset;

        [SerializeField, InspectorName("相机位置附加偏移")]
        private Vector3 cameraPositionOffset = Vector3.zero;

        [Header("移动")]
        [SerializeField, InspectorName("启用 WASD 移动")]
        private bool keyboardScrolling = true;

        [SerializeField, InspectorName("原版移动速度")]
        [Tooltip("来自 CK3 NCamera.SCROLL_SPEED。")]
        private float scrollSpeed = 0.045f;

        [SerializeField, InspectorName("移动速度总倍率"), Min(0.0f)]
        [Tooltip("Unity 预览修正倍率；不改变上面的 CK3 原版 SCROLL_SPEED。感觉过慢时直接增大此值。")]
        private float movementSpeedMultiplier = 20.0f;

        [SerializeField, InspectorName("键盘移动倍率"), Min(0.0f)]
        private float keyboardSpeedMultiplier = 1.0f;

        [SerializeField, InspectorName("边缘移动倍率"), Min(0.0f)]
        private float edgeSpeedMultiplier = 1.0f;

        [SerializeField, InspectorName("向左按键")]
        private KeyCode moveLeftKey = KeyCode.A;

        [SerializeField, InspectorName("向右按键")]
        private KeyCode moveRightKey = KeyCode.D;

        [SerializeField, InspectorName("向下按键")]
        private KeyCode moveDownKey = KeyCode.S;

        [SerializeField, InspectorName("向上按键")]
        private KeyCode moveUpKey = KeyCode.W;

        [SerializeField, InspectorName("启用屏幕边缘移动")]
        private bool edgeScrolling = true;

        [SerializeField, InspectorName("边缘触发像素")]
        [Tooltip("来自 CK3 NCamera.EDGE_SCROLLING_PIXELS。")]
        private int edgeScrollingPixels = 5;

        [Header("缩放")]
        [SerializeField, InspectorName("启用滚轮缩放")]
        private bool mouseWheelZoom = true;

        [SerializeField, InspectorName("单次缩放跨越档位"), Range(1, 5)]
        private int zoomStepsPerInput = 1;

        [SerializeField, InspectorName("反转滚轮方向")]
        private bool invertMouseWheel;

        [SerializeField, InspectorName("启用缩放平滑")]
        [Tooltip("Unity 输入载体参数。原版距离和倾角档位保持不变，只平滑档位之间的相机运动。")]
        private bool smoothZoom = true;

        [SerializeField, InspectorName("缩放平滑时间"), Min(0.01f)]
        [Tooltip("到达目标档位的大致缓动时间，单位为秒。该值是 Unity 载体参数，不是 CK3 原版常量。")]
        private float zoomSmoothTime = 0.18f;

        [SerializeField, InspectorName("缩放最大变化速度"), Min(1.0f)]
        [Tooltip("限制相机距离每秒最大变化量。数值足够大时主要由平滑时间控制。")]
        private float zoomMaximumSpeed = 30000.0f;

        [SerializeField, InspectorName("拉近按键")]
        private KeyCode zoomInKey = KeyCode.PageDown;

        [SerializeField, InspectorName("拉远按键")]
        private KeyCode zoomOutKey = KeyCode.PageUp;

        [Header("地图移动范围")]
        [SerializeField, InspectorName("观察中心最小值")]
        private Vector2 lookAtMinimum = new Vector2(0.0f, 0.0f);

        [SerializeField, InspectorName("观察中心最大值")]
        private Vector2 lookAtMaximum = new Vector2(9090.0f, 4696.0f);

        public int ZoomStep => zoomStep;
        public float VisualZoomStep => zoomInitialized ? visualZoomStep : zoomStep;
        public float CurrentZoomDistance => zoomInitialized
            ? currentZoomDistance
            : ZoomDistances[Mathf.Clamp(zoomStep, 0, ZoomDistances.Length - 1)]
                * Mathf.Max(0.01f, zoomDistanceMultiplier);

        private float currentZoomDistance;
        private float currentTilt;
        private float visualZoomStep;
        private float zoomDistanceVelocity;
        private float tiltVelocity;
        private float visualZoomStepVelocity;
        private bool zoomInitialized;

        public void ResetToCK3Start()
        {
            lookAt = startLookAt;
            zoomStep = startZoomStep;
            ApplyTransform(true);
        }

        private void OnEnable()
        {
            ApplyTransform(true);
        }

        private void Update()
        {
            Vector2 keyboardMovement = Vector2.zero;
            if (keyboardScrolling)
            {
                if (Input.GetKey(moveLeftKey)) keyboardMovement.x -= 1.0f;
                if (Input.GetKey(moveRightKey)) keyboardMovement.x += 1.0f;
                if (Input.GetKey(moveDownKey)) keyboardMovement.y -= 1.0f;
                if (Input.GetKey(moveUpKey)) keyboardMovement.y += 1.0f;
            }

            Vector2 edgeMovement = Vector2.zero;
            if (edgeScrolling && Application.isFocused)
            {
                Vector3 mouse = Input.mousePosition;
                if (mouse.x >= 0 && mouse.x <= edgeScrollingPixels) edgeMovement.x -= 1.0f;
                if (mouse.x <= Screen.width && mouse.x >= Screen.width - edgeScrollingPixels) edgeMovement.x += 1.0f;
                if (mouse.y >= 0 && mouse.y <= edgeScrollingPixels) edgeMovement.y -= 1.0f;
                if (mouse.y <= Screen.height && mouse.y >= Screen.height - edgeScrollingPixels) edgeMovement.y += 1.0f;
            }

            Vector2 movement = Vector2.ClampMagnitude(
                keyboardMovement * keyboardSpeedMultiplier + edgeMovement * edgeSpeedMultiplier,
                1.0f);
            float distance = CurrentZoomDistance;
            lookAt += new Vector3(movement.x, 0.0f, movement.y)
                * (scrollSpeed * movementSpeedMultiplier * distance * Time.unscaledDeltaTime);
            lookAt.x = Mathf.Clamp(lookAt.x, lookAtMinimum.x, lookAtMaximum.x);
            lookAt.z = Mathf.Clamp(lookAt.z, lookAtMinimum.y, lookAtMaximum.y);

            float wheel = mouseWheelZoom ? Input.mouseScrollDelta.y : 0.0f;
            if (invertMouseWheel) wheel = -wheel;
            if (wheel > 0.0f || Input.GetKeyDown(zoomInKey)) zoomStep -= zoomStepsPerInput;
            if (wheel < 0.0f || Input.GetKeyDown(zoomOutKey)) zoomStep += zoomStepsPerInput;
            zoomStep = Mathf.Clamp(zoomStep, 0, ZoomDistances.Length - 1);
            startZoomStep = Mathf.Clamp(startZoomStep, 0, ZoomDistances.Length - 1);
            zoomDistanceMultiplier = Mathf.Max(0.01f, zoomDistanceMultiplier);
            zoomStepsPerInput = Mathf.Clamp(zoomStepsPerInput, 1, 5);
            ApplyTransform(false);
        }

        private void OnValidate()
        {
            zoomStep = Mathf.Clamp(zoomStep, 0, ZoomDistances.Length - 1);
            if (!Application.isPlaying)
            {
                ApplyTransform(true);
            }
        }

        private void ApplyTransform(bool immediate)
        {
            float targetDistance = ZoomDistances[zoomStep] * Mathf.Max(0.01f, zoomDistanceMultiplier);
            float targetTilt = ZoomTilts[zoomStep] + tiltOffset;
            if (!zoomInitialized || immediate || !Application.isPlaying || !smoothZoom)
            {
                currentZoomDistance = targetDistance;
                currentTilt = targetTilt;
                visualZoomStep = zoomStep;
                zoomDistanceVelocity = 0.0f;
                tiltVelocity = 0.0f;
                visualZoomStepVelocity = 0.0f;
                zoomInitialized = true;
            }
            else
            {
                float deltaTime = Mathf.Max(0.000001f, Time.unscaledDeltaTime);
                float smoothTime = Mathf.Max(0.01f, zoomSmoothTime);
                currentZoomDistance = Mathf.SmoothDamp(
                    currentZoomDistance,
                    targetDistance,
                    ref zoomDistanceVelocity,
                    smoothTime,
                    Mathf.Max(1.0f, zoomMaximumSpeed),
                    deltaTime);
                currentTilt = Mathf.SmoothDampAngle(
                    currentTilt,
                    targetTilt,
                    ref tiltVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
                visualZoomStep = Mathf.SmoothDamp(
                    visualZoomStep,
                    zoomStep,
                    ref visualZoomStepVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }

            Quaternion rotation = Quaternion.Euler(currentTilt, 0.0f, 0.0f);
            transform.SetPositionAndRotation(
                lookAt - rotation * Vector3.forward
                    * currentZoomDistance
                    + cameraPositionOffset,
                rotation);
        }

    }
}
