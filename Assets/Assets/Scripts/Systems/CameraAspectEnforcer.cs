using UnityEngine;

/// <summary>
/// Enforces a fixed 16:9 aspect ratio on all screen sizes by adjusting the
/// camera viewport rect so the game always renders in a centred 16:9 region.
/// Any remaining screen area is filled with solid black (letterbox / pillarbox).
/// Attach to the Main Camera alongside a Camera component.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraAspectEnforcer : MonoBehaviour
{
    private const float TargetAspect  = 16f / 9f;

    private Camera _camera;
    private int    _lastScreenWidth;
    private int    _lastScreenHeight;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        Apply();
    }

    void Update()
    {
        // Re-apply only when the resolution actually changes (cheap check).
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            Apply();
    }

    /// <summary>Recalculates and sets the camera viewport rect for 16:9 output.</summary>
    private void Apply()
    {
        _lastScreenWidth  = Screen.width;
        _lastScreenHeight = Screen.height;

        float screenAspect = (float)Screen.width / Screen.height;

        if (Mathf.Approximately(screenAspect, TargetAspect))
        {
            // Exact match — use the full viewport.
            _camera.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        if (screenAspect > TargetAspect)
        {
            // Screen is wider than 16:9 → pillarbox (black bars on left / right).
            float normalizedWidth = TargetAspect / screenAspect;
            float offset          = (1f - normalizedWidth) * 0.5f;
            _camera.rect = new Rect(offset, 0f, normalizedWidth, 1f);
        }
        else
        {
            // Screen is taller than 16:9 → letterbox (black bars on top / bottom).
            float normalizedHeight = screenAspect / TargetAspect;
            float offset           = (1f - normalizedHeight) * 0.5f;
            _camera.rect = new Rect(0f, offset, 1f, normalizedHeight);
        }

        Debug.Log($"[AspectEnforcer] {Screen.width}×{Screen.height} " +
                  $"(aspect {screenAspect:F3}) → rect {_camera.rect}");
    }
}
