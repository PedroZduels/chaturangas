using UnityEngine;

/// <summary>
/// Serialized board-position tweak offsets for each camera view mode.
/// Attach to ViewToggleButton for easy inspector access during Play Mode.
///
/// <see cref="CameraController"/> reads these fields directly via <c>GetTweakOffset</c> —
/// no push or sync needed. Change a value here and it takes effect on the next
/// <see cref="CameraController.Recenter"/> or view toggle.
/// </summary>
public class ViewToggleBoardTweak : MonoBehaviour
{
    [Header("Board Tweak Offsets (XY world-units)")]
    [Tooltip("Board offset added on top of auto-center in Top-Down view.")]
    public Vector2 topDownOffset = Vector2.zero;

    [Tooltip("Board offset added on top of auto-center in Isometric view.")]
    public Vector2 isoOffset = Vector2.zero;

    [Tooltip("Board offset added on top of auto-center in Isometric 2 view.")]
    public Vector2 iso2Offset = Vector2.zero;

    private CameraController cameraController;

    private Vector2 previousTopDown;
    private Vector2 previousIso;
    private Vector2 previousIso2;

    private void Awake()
    {
        cameraController = CameraController.Instance != null
            ? CameraController.Instance
            : FindFirstObjectByType<CameraController>();

        CacheValues();
    }

    private void Update()
    {
        if (cameraController == null) return;

        // Detect inspector edits during Play Mode and re-center immediately.
        if (topDownOffset != previousTopDown ||
            isoOffset      != previousIso    ||
            iso2Offset     != previousIso2)
        {
            cameraController.Recenter();
            CacheValues();
        }
    }

    private void CacheValues()
    {
        previousTopDown = topDownOffset;
        previousIso     = isoOffset;
        previousIso2    = iso2Offset;
    }
}

