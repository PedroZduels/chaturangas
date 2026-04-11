using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating tooltip panel shown when hovering a floppy slot.
/// Place this on a child of the HUD Canvas. It repositions itself
/// below whichever slot triggered it.
/// Call Show / Hide from FloppyHUD.
/// </summary>
public class FloppyTooltip : MonoBehaviour
{
    [Header("References")]
    public TMP_Text titleText;
    public TMP_Text descText;
    public RectTransform panel;

    [Header("Layout")]
    [Tooltip("Vertical gap in pixels between the bottom of the slot and the top of the tooltip.")]
    public float verticalOffset = 8f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha          = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Display the tooltip below the given slot RectTransform.</summary>
    public void Show(RectTransform slotRect, string title, string description)
    {
        if (titleText != null) titleText.text = title;
        if (descText  != null) descText.text  = description;

        PositionBelow(slotRect);

        canvasGroup.alpha          = 1f;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Hide the tooltip.</summary>
    public void Hide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void PositionBelow(RectTransform slotRect)
    {
        if (panel == null) return;

        RectTransform parentRect = panel.parent as RectTransform;
        if (parentRect == null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        // Bottom-center of the slot in screen space.
        // GetWorldCorners order: [0]=bottom-left, [1]=top-left, [2]=top-right, [3]=bottom-right.
        Vector3[] corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector2 bottomLeft  = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
        Vector2 bottomCenterScreen = (bottomLeft + bottomRight) * 0.5f;

        // Convert screen-space bottom-center to the panel's parent local space,
        // then apply to anchoredPosition accounting for the anchor offset.
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, bottomCenterScreen, cam, out localPoint);

        // localPoint is in parentRect's local space (origin = parentRect pivot).
        // anchoredPosition is relative to the anchor position in parentRect.
        // Convert by subtracting the anchor's offset from parentRect's pivot.
        Rect  parentBounds  = parentRect.rect;
        Vector2 anchorOffset = new Vector2(
            Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, panel.anchorMin.x),
            Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, panel.anchorMin.y));

        panel.anchoredPosition = new Vector2(
            localPoint.x - anchorOffset.x,
            localPoint.y - anchorOffset.y - verticalOffset);
    }
}
