using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Tile : MonoBehaviour, IBossHudElement
{
    public Vector2Int gridPos;
    public Piece occupiedPiece;

    /// <summary>True for light (white) squares, false for dark (black) squares.</summary>
    public bool isLightSquare;

    // ── Normal-fight tile colors ──────────────────────────────────────────────

    /// <summary>Base sprite color for dark (tileB) squares in normal fights.</summary>
    public Color darkSquareColor  = new Color(0f, 0.55f, 0.60f, 1f);

    /// <summary>WhiteOverlay sprite color for light (tileW) squares in normal fights.</summary>
    public Color lightSquareColor = new Color(0f, 1f, 1f, 1f);

    // Set by the boss effect to make ResetColor re-apply the boss tint instead of the normal colors.
    private Color? _bossTintOverride      = null;
    private Color? _bossOverlayOverride   = null;

    // ── Overlay colors ────────────────────────────────────────────────────────

    private static readonly Color MoveHighlightColor      = new Color(0.18f, 0.85f, 0.32f, 1.00f);
    private static readonly Color CaptureHighlightColor   = new Color(0.90f, 0.20f, 0.20f, 1.00f);
    private static readonly Color PlacementHighlightColor = new Color(0.00f, 0.60f, 1.00f, 0.95f);
    private static readonly Color DangerHighlightColor    = new Color(1.00f, 0.05f, 0.05f, 0.92f);

    private const float CollapseDuration = 0.55f;

    private GameController gameController;
    private SpriteRenderer sr;

    // ── Overlay renderers — each on its own child GameObject ─────────────────

    /// <summary>Green dot — valid move destination.</summary>
    private SpriteRenderer _moveHighlightSr;

    /// <summary>Red tint — capturable enemy (bishop phase 2).</summary>
    private SpriteRenderer _captureHighlightSr;

    /// <summary>Vivid blue — valid placement square in market buy mode.</summary>
    private SpriteRenderer _placementHighlightSr;

    /// <summary>Vivid red — Sudden Death danger strip. Topmost layer.</summary>
    private SpriteRenderer _dangerHighlightSr;

    /// <summary>White square overlay — enabled for light squares only.</summary>
    private SpriteRenderer _whiteOverlaySr;

    // ── Hover label (world-space TMP — placement mode only) ───────────────────

    private TextMeshPro _hoverLabel;

    // ── Internal state ────────────────────────────────────────────────────────

    private bool _isDanger          = false;
    private bool _isPlacementActive = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        gameController = Object.FindAnyObjectByType<GameController>();
        sr = GetComponent<SpriteRenderer>();

        // Dark squares show darkSquareColor. Light squares use the same base but
        // the WhiteOverlay on top renders lightSquareColor, giving a brighter cyan.
        sr.color = darkSquareColor;

        InitOverlay("MoveHighlight",      ref _moveHighlightSr,      MoveHighlightColor,      sr.sortingOrder + 2);
        InitOverlay("CaptureHighlight",   ref _captureHighlightSr,   CaptureHighlightColor,   sr.sortingOrder + 2);
        InitOverlay("PlacementHighlight", ref _placementHighlightSr, PlacementHighlightColor, sr.sortingOrder + 2);
        InitOverlay("DangerHighlight",    ref _dangerHighlightSr,    DangerHighlightColor,    sr.sortingOrder + 3);

        Transform whiteChild = transform.Find("WhiteOverlay");
        if (whiteChild != null)
        {
            _whiteOverlaySr = whiteChild.GetComponent<SpriteRenderer>();
            if (_whiteOverlaySr != null)
            {
                _whiteOverlaySr.sortingOrder = sr.sortingOrder + 1;
                _whiteOverlaySr.enabled      = isLightSquare;

                // Apply the light-square cyan so both tileW and tileB show cyan tones.
                if (isLightSquare)
                    _whiteOverlaySr.color = lightSquareColor;
            }
        }

        Transform hoverChild = transform.Find("HoverLabel");
        if (hoverChild != null)
        {
            _hoverLabel = hoverChild.GetComponent<TextMeshPro>();
            if (_hoverLabel != null)
            {
                _hoverLabel.sortingOrder = sr.sortingOrder + 4;
                hoverChild.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Called by BoardManager immediately after instantiation to mark this as a light or dark square.
    /// Must be called before Start() runs.
    /// </summary>
    public void InitTile(bool lightSquare)
    {
        isLightSquare = lightSquare;
    }

    void OnMouseDown()
    {
        gameController.OnTileClicked(this);
    }

    void OnMouseEnter()
    {
        if (!_isPlacementActive || _hoverLabel == null) return;
        _hoverLabel.gameObject.SetActive(true);
    }

    void OnMouseExit()
    {
        if (_hoverLabel == null) return;
        _hoverLabel.gameObject.SetActive(false);
    }

    // ── Move / Capture overlays ───────────────────────────────────────────────

    /// <summary>Shows or hides the green move-destination overlay.</summary>
    public void ShowMoveHighlight(bool show)
    {
        if (_moveHighlightSr != null) _moveHighlightSr.enabled = show;
    }

    /// <summary>Shows or hides the red capturable-enemy overlay.</summary>
    public void ShowCaptureHighlight(bool show)
    {
        if (_captureHighlightSr != null) _captureHighlightSr.enabled = show;
    }

    /// <summary>Shows or hides the vivid-blue placement overlay and tracks hover state.</summary>
    public void ShowPlacementHighlight(bool show)
    {
        _isPlacementActive = show;
        if (_placementHighlightSr != null) _placementHighlightSr.enabled = show;

        // Hide hover label whenever placement is cleared.
        if (!show && _hoverLabel != null)
            _hoverLabel.gameObject.SetActive(false);
    }

    /// <summary>Turns off move and capture overlays. Does not affect placement or danger.</summary>
    public void ClearMoveHighlights()
    {
        ShowMoveHighlight(false);
        ShowCaptureHighlight(false);
    }

    // ── Danger (Sudden Death) ─────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the vivid-red Sudden Death danger overlay.
    /// </summary>
    public void ShowDangerHighlight(bool show)
    {
        _isDanger = show;
        if (_dangerHighlightSr != null) _dangerHighlightSr.enabled = show;
    }

    /// <summary>Clears the danger overlay.</summary>
    public void ClearDanger()
    {
        ShowDangerHighlight(false);
    }

    /// <summary>
    /// Legacy API — kept for SuddenDeathManager compatibility.
    /// When isDanger is true, routes through the dedicated DangerHighlight overlay.
    /// When false, tints the base renderer directly (non-danger legacy use).
    /// </summary>
    public void Highlight(Color color, bool isDanger = false)
    {
        if (isDanger)
            ShowDangerHighlight(true);
        else
            sr.color = color;
    }

    /// <summary>
    /// Restores the base color. During a boss fight this re-applies the boss tint so
    /// ClearSelection() and SuddenDeathManager resets don't strip it mid-fight.
    /// </summary>
    public void ResetColor()
    {
        if (sr != null)
            sr.color = _bossTintOverride ?? darkSquareColor;

        if (_whiteOverlaySr != null && isLightSquare)
            _whiteOverlaySr.color = _bossOverlayOverride ?? lightSquareColor;
    }

    // ── Collapse animation ────────────────────────────────────────────────────

    /// <summary>Plays a collapse animation (shrink + fade) then disables the GameObject.</summary>
    public Coroutine PlayCollapseAnimation()
    {
        return StartCoroutine(CollapseRoutine());
    }

    private IEnumerator CollapseRoutine()
    {
        Color startColor = sr.color;
        Color endColor   = new Color(startColor.r, startColor.g, startColor.b, 0f);
        Vector3 startScale = transform.localScale;
        Vector3 endScale   = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < CollapseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / CollapseDuration);
            sr.color             = Color.Lerp(startColor, endColor, t);
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        transform.localScale = startScale;
        gameObject.SetActive(false);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void InitOverlay(string childName, ref SpriteRenderer target, Color color, int sortingOrder)
    {
        Transform child = transform.Find(childName);
        if (child == null) return;

        target = child.GetComponent<SpriteRenderer>();
        if (target == null) return;

        target.color        = color;
        target.sortingOrder = sortingOrder;
        target.enabled      = false;
    }

    // ── IBossHudElement ───────────────────────────────────────────────────────

    /// <summary>
    /// Tints the tile to the boss color and stores it so ResetColor() re-applies it
    /// for the duration of the boss fight. Dark squares get a 50% dimmed version to
    /// preserve the checkerboard contrast.
    /// </summary>
    public void ApplyBossTint(Color tint, Dictionary<int, Color> originalColors)
    {
        if (sr == null) return;

        int key = sr.GetInstanceID();
        if (!originalColors.ContainsKey(key))
            originalColors[key] = sr.color;

        Color blended = isLightSquare
            ? tint
            : new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, tint.a);

        _bossTintOverride = blended;
        sr.color          = blended;

        if (_whiteOverlaySr != null && isLightSquare)
        {
            int overlayKey = _whiteOverlaySr.GetInstanceID();
            if (!originalColors.ContainsKey(overlayKey))
                originalColors[overlayKey] = _whiteOverlaySr.color;

            _bossOverlayOverride  = tint;
            _whiteOverlaySr.color = tint;
        }
    }

    /// <summary>
    /// Clears the boss tint override and restores the tile to its pre-boss colors.
    /// Falls back to ResetColor if no snapshot exists.
    /// </summary>
    public void RestoreTint(Dictionary<int, Color> originalColors)
    {
        _bossTintOverride    = null;
        _bossOverlayOverride = null;

        bool restored = false;

        if (sr != null && originalColors.TryGetValue(sr.GetInstanceID(), out Color orig))
        {
            sr.color = orig;
            restored = true;
        }

        if (_whiteOverlaySr != null &&
            originalColors.TryGetValue(_whiteOverlaySr.GetInstanceID(), out Color overlayOrig))
            _whiteOverlaySr.color = overlayOrig;

        if (!restored)
            ResetColor();
    }
}

