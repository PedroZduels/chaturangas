using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pixel-art floppy tooltip. Shows only the description text.
/// Style: pure black background, white bold centered text.
/// Appears below the hovered slot in both the HUD and the market.
/// </summary>
public class MarketFloppyTooltip : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0f,    0f,    0f,    1f);
    private static readonly Color TextColor       = Color.white;

    private const float TooltipWidth   = 420f;
    private const float VerticalOffset = 28f;
    private const float TextFontSize   = 18f;
    private const float TextPadding    = 22f;

    private RectTransform _panel;
    private CanvasGroup   _canvasGroup;
    private TMP_Text      _descText;
    private Canvas        _canvas;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _panel  = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();

        _panel.sizeDelta = new Vector2(TooltipWidth, 0f);

        _canvasGroup                = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;

        BuildUI();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows the tooltip below the given slot with only its description.</summary>
    public void Show(RectTransform slotRect, string description)
    {
        if (_descText != null)
            _descText.text = (description ?? "").ToUpper();

        PositionBelow(slotRect);
        _canvasGroup.alpha = 1f;
    }

    /// <summary>Hides the tooltip.</summary>
    public void Hide()
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Root: pure black background, grows vertically with content.
        Image bg = gameObject.AddComponent<Image>();
        bg.color         = BackgroundColor;
        bg.raycastTarget = false;

        ContentSizeFitter rootCsf = gameObject.AddComponent<ContentSizeFitter>();
        rootCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        VerticalLayoutGroup rootVlg = gameObject.AddComponent<VerticalLayoutGroup>();
        rootVlg.childAlignment         = TextAnchor.UpperCenter;
        rootVlg.padding                = new RectOffset(0, 0, 0, 0);
        rootVlg.spacing                = 0f;
        rootVlg.childControlWidth      = true;
        rootVlg.childControlHeight     = false;
        rootVlg.childForceExpandWidth  = true;
        rootVlg.childForceExpandHeight = false;

        // Description text — centered, wrapping, bold, white pixel font.
        GameObject textGO = new GameObject("Desc", typeof(RectTransform));
        textGO.transform.SetParent(transform, worldPositionStays: false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text             = "";
        tmp.fontSize         = TextFontSize;
        tmp.color            = TextColor;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.margin           = new Vector4(TextPadding, TextPadding * 0.8f, TextPadding, TextPadding * 0.8f);

        TMP_FontAsset pixelFont = ResolvePixelFont();
        if (pixelFont != null) tmp.font = pixelFont;

        ContentSizeFitter textCsf = textGO.AddComponent<ContentSizeFitter>();
        textCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        textCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _descText = tmp;
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    private void PositionBelow(RectTransform slotRect)
    {
        if (_panel == null) return;

        RectTransform parentRect = _panel.parent as RectTransform;
        if (parentRect == null) return;

        Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _canvas.worldCamera : null;

        // Bottom-center of the slot.
        // GetWorldCorners: [0]=bottom-left [1]=top-left [2]=top-right [3]=bottom-right
        Vector3[] corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector2 sA = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 sB = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, (sA + sB) * 0.5f, cam, out Vector2 localPoint);

        Rect    pb     = parentRect.rect;
        Vector2 anchor = new Vector2(
            Mathf.Lerp(pb.xMin, pb.xMax, _panel.anchorMin.x),
            Mathf.Lerp(pb.yMin, pb.yMax, _panel.anchorMin.y));

        // pivot (0.5, 1) → tooltip's top edge aligns with the slot's bottom edge.
        _panel.pivot            = new Vector2(0.5f, 1f);
        _panel.anchoredPosition = new Vector2(
            localPoint.x - anchor.x,
            localPoint.y - anchor.y - VerticalOffset);
    }

    // ── Font helper ───────────────────────────────────────────────────────────

    private static TMP_FontAsset ResolvePixelFont()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset");
#else
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
#endif
    }

    // ── Static factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the shared tooltip instance on the given canvas, creating it on first call.
    /// </summary>
    public static MarketFloppyTooltip GetOrCreate(Canvas canvas)
    {
        if (canvas == null) return null;

        MarketFloppyTooltip existing =
            canvas.GetComponentInChildren<MarketFloppyTooltip>(includeInactive: true);
        if (existing != null) return existing;

        GameObject go = new GameObject("FloppyTooltip", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, worldPositionStays: false);

        RectTransform rt   = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;

        return go.AddComponent<MarketFloppyTooltip>();
    }
}

