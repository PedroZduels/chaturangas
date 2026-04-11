using UnityEngine;

/// <summary>
/// Cycles the main camera through three orthographic views:
///   TopDown    — straight overhead, no rotation.
///   Isometric  — diagonal isometric, Z roll 60°.
///   Isometric2 — same position, Z roll 30° for a flatter look.
///
/// The board is always auto-centered on screen based on board dimensions.
/// Per-view tweak offsets are applied on top of the computed center for
/// isometric views where the rotated projection shifts the visual centroid.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static CameraController Instance { get; private set; }

    // ── View Mode ─────────────────────────────────────────────────────────────

    public enum ViewMode { TopDown, Isometric, Isometric2 }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Top-Down")]
    public Vector3 topDownPosition    = new Vector3(0f, 0f, -10f);
    public Vector3 topDownEuler       = Vector3.zero;
    public float   topDownOrthoSize   = 4f;
    [Tooltip("XY offset applied to piece visuals so they appear centered on their tile.")]
    public Vector2 topDownPieceOffset = new Vector2(0f, 0f);

    [Header("Isometric")]
    public Vector3 isoPosition        = new Vector3(-5f, 10f, -7f);
    public Vector3 isoEuler           = new Vector3(35f, 45f, 60f);
    public float   isoOrthoSize       = 4f;
    [Tooltip("XY offset applied to piece visuals so they appear centered on their tile.")]
    public Vector2 isoPieceOffset     = new Vector2(0f, 0.4f);

    [Header("Isometric 2")]
    public Vector3 iso2Position       = new Vector3(-5f, 10f, -7f);
    public Vector3 iso2Euler          = new Vector3(35f, 45f, 30f);
    public float   iso2OrthoSize      = 4f;
    [Tooltip("XY offset applied to piece visuals so they appear centered on their tile.")]
    public Vector2 iso2PieceOffset    = new Vector2(0f, 0.4f);

    // ── State ─────────────────────────────────────────────────────────────────

    public ViewMode CurrentView { get; private set; } = ViewMode.TopDown;

    /// <summary>True when in any isometric mode — billboard and VisualPos logic uses this.</summary>
    public bool IsIsometric => CurrentView != ViewMode.TopDown;

    /// <summary>True only in the first isometric mode.</summary>
    public bool IsFullIsometric => CurrentView == ViewMode.Isometric;

    /// <summary>True only in the second isometric mode.</summary>
    public bool IsShallowIso => CurrentView == ViewMode.Isometric2;

    /// <summary>XY visual offset for pieces in the current view.</summary>
    public Vector2 ActivePieceOffset => CurrentView switch
    {
        ViewMode.Isometric  => isoPieceOffset,
        ViewMode.Isometric2 => iso2PieceOffset,
        _                   => topDownPieceOffset
    };

    private Camera cam;
    private BoardManager boardManager;
    private ViewToggleBoardTweak boardTweak;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance     = this;
        cam          = GetComponent<Camera>();
        cam.orthographic = true;
        boardManager = FindFirstObjectByType<BoardManager>();
        boardTweak   = FindFirstObjectByType<ViewToggleBoardTweak>();
        Apply(ViewMode.TopDown);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Raised after every view change; passes the camera's new world-space up vector.</summary>
    public static event System.Action<Vector3> OnViewChanged;

    /// <summary>Advances to the next view: TopDown → Isometric → Isometric2 → TopDown.</summary>
    public void Toggle()
    {
        CurrentView = CurrentView switch
        {
            ViewMode.TopDown    => ViewMode.Isometric,
            ViewMode.Isometric  => ViewMode.Isometric2,
            ViewMode.Isometric2 => ViewMode.TopDown,
            _                   => ViewMode.TopDown
        };

        Apply(CurrentView);
        OnViewChanged?.Invoke(transform.up);
    }

    /// <summary>
    /// Re-centers the board for the current view. Call this after the board
    /// has been expanded (e.g. after <see cref="BoardManager.Expand"/>).
    /// </summary>
    public void Recenter() => Apply(CurrentView);

    /// <summary>
    /// Updates the orthographic size for all three view modes from a
    /// <see cref="BoardManager.GridScalePreset"/> and re-applies the current view.
    /// Called automatically by <see cref="BoardManager.ApplyScalePreset"/>.
    /// </summary>
    public void SetOrthoSizes(float topDown, float iso, float iso2)
    {
        topDownOrthoSize = topDown;
        isoOrthoSize     = iso;
        iso2OrthoSize    = iso2;
        Apply(CurrentView);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Apply(ViewMode mode)
    {
        cam.orthographic = true;

        Vector3 camPos      = GetCamPos(mode);
        Vector2 tweakOffset = GetTweakOffset(mode);

        switch (mode)
        {
            case ViewMode.Isometric:
                transform.SetPositionAndRotation(camPos, Quaternion.Euler(isoEuler));
                cam.orthographicSize = isoOrthoSize;
                break;

            case ViewMode.Isometric2:
                transform.SetPositionAndRotation(camPos, Quaternion.Euler(iso2Euler));
                cam.orthographicSize = iso2OrthoSize;
                break;

            default: // TopDown
                transform.SetPositionAndRotation(camPos, Quaternion.Euler(topDownEuler));
                cam.orthographicSize = topDownOrthoSize;
                break;
        }

        if (boardManager != null)
        {
            Vector2 centered = ComputeCenteredBoardOrigin(camPos, tweakOffset);
            boardManager.transform.position = new Vector3(centered.x, centered.y, 0f);
        }
    }

    private Vector3 GetCamPos(ViewMode mode) => mode switch
    {
        ViewMode.Isometric  => isoPosition,
        ViewMode.Isometric2 => iso2Position,
        _                   => topDownPosition
    };

    /// <summary>
    /// Reads the tweak offset for <paramref name="mode"/> from <see cref="ViewToggleBoardTweak"/>.
    /// Falls back to zero if no tweak component exists in the scene.
    /// </summary>
    private Vector2 GetTweakOffset(ViewMode mode)
    {
        if (boardTweak == null) return Vector2.zero;
        return mode switch
        {
            ViewMode.Isometric  => boardTweak.isoOffset,
            ViewMode.Isometric2 => boardTweak.iso2Offset,
            _                   => boardTweak.topDownOffset
        };
    }

    /// <summary>
    /// Computes the world-space XY that the board's (0,0) corner must sit at so
    /// the board's geometric center aligns with the camera's screen center.
    /// </summary>
    private Vector2 ComputeCenteredBoardOrigin(Vector3 camWorldPos, Vector2 tweakOffset)
    {
        float halfW = (boardManager.width  * boardManager.tileSize) * 0.5f;
        float halfH = (boardManager.height * boardManager.tileSize) * 0.5f;
        return new Vector2(camWorldPos.x - halfW, camWorldPos.y - halfH) + tweakOffset;
    }

    /// <summary>
    /// The active tweak offset for the current view, read/written through
    /// <see cref="ViewToggleBoardTweak"/>. Used by <see cref="BoardNudgeUI"/>.
    /// </summary>
    public Vector2 ActiveBoardPosition
    {
        get => GetTweakOffset(CurrentView);
        set
        {
            if (boardTweak != null)
            {
                switch (CurrentView)
                {
                    case ViewMode.Isometric:  boardTweak.isoOffset     = value; break;
                    case ViewMode.Isometric2: boardTweak.iso2Offset    = value; break;
                    default:                  boardTweak.topDownOffset = value; break;
                }
            }

            if (boardManager != null)
            {
                Vector2 centered = ComputeCenteredBoardOrigin(GetCamPos(CurrentView), value);
                boardManager.transform.position = new Vector3(centered.x, centered.y, 0f);
            }
        }
    }
}
