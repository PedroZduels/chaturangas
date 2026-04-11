using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages the Sudden Death mechanic.
///
/// Per-round sequence once active:
///   1. OnBeforeAITurn   — select the next pattern; add tiles to _collapsed so
///                         IsLiveTile returns false (AI will avoid them).
///   2. AI plays         — it already treats those tiles as walls.
///   3. OnAfterAITurn    — paint the pattern red so the player can see the warning.
///   4. Player plays.
///   5. OnAfterPlayerTurn — collapse (kill pieces + animate tiles disappear),
///                          then stalemate-check for the NEXT round.
///
/// Activation round (first time SD triggers):
///   OnAfterPlayerTurn detects the stalemate, shows the announcement splash,
///   then returns — the NEXT call to OnBeforeAITurn begins step 1 above.
///
/// Each round a pattern is chosen at random from three types:
///   • Single row      — top or bottom edge, chosen randomly
///   • Single column   — left or right edge, chosen randomly
///   • 2×2 block       — any position within the live bounds
///
/// The same type can appear multiple times in a row — there is no alternation.
/// </summary>
public class SuddenDeathManager : MonoBehaviour
{
    [Header("References")]
    public GameController gameController;
    public BoardManager   board;

    [Header("UI")]
    [Tooltip("Full-screen overlay shown for the SUDDEN DEATH announcement.")]
    public GameObject announcementPanel;
    public TMP_Text   announcementLabel;

    [Header("Timing")]
    [Tooltip("How long the SUDDEN DEATH splash is shown the first time SD activates.")]
    public float AnnouncementDuration = 2f;

    [Tooltip("Pause before the highlighted column/row collapses (lets the player register it).")]
    public float CollapseDelay = 1.0f;

    [Tooltip("Consecutive full rounds with no possible capture before Sudden Death activates.")]
    public int StalemateThreshold = 2;

    // Color used to highlight the column/row that is about to collapse.
    private static readonly Color DangerColor = new Color(1f, 0.15f, 0.15f, 0.85f);

    // ── Live board bounds (inclusive grid coords) ─────────────────────────────

    // Exposed as properties so ChessAI / SimBoard can read them.
    public int MinX { get; private set; }
    public int MaxX { get; private set; }
    public int MinY { get; private set; }
    public int MaxY { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private int  _staleCounter        = 0;
    private bool _active              = false;
    private bool _announcementPending = false;

    // How many AI turns have elapsed since activation.
    // 0 = first AI turn after announcement (highlight only, no collapse yet).
    private int _sdRound = 0;

    // Every tile position that has been highlighted (and therefore locked out of
    // gameplay) is added here immediately. CollapseStrip uses this set to know
    // which tiles to animate; ResetForFight clears it.
    private readonly HashSet<Vector2Int> _collapsed = new HashSet<Vector2Int>();

    // Tiles currently highlighted and waiting to collapse. Null when none pending.
    private List<Vector2Int> _pendingStrip = null;

    // ── Pattern type ──────────────────────────────────────────────────────────

    private enum CollapsePattern { Row, Column, Corner2x2 }

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();
        if (board == null)
            board = Object.FindAnyObjectByType<BoardManager>();

        if (announcementPanel != null)
            announcementPanel.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Resets for a fresh fight. All tiles re-enabled, counters cleared.</summary>
    public void ResetForFight()
    {
        if (board == null) return;

        MinX = 0;
        MaxX = board.width  - 1;
        MinY = 0;
        MaxY = board.height - 1;

        _staleCounter        = 0;
        _active              = false;
        _announcementPending = false;
        _pendingStrip        = null;
        _collapsed.Clear();

        if (board.tiles != null)
            foreach (Tile t in board.tiles)
            {
                if (t == null) continue;
                t.gameObject.SetActive(true);
                t.ClearDanger();
                t.ResetColor();
            }
    }

    /// <summary>True once Sudden Death has been activated this fight.</summary>
    public bool IsActive => _active;

    /// <summary>
    /// The set of tiles currently highlighted red (selected but not yet collapsed).
    /// The AI passes this to ChessAI so it treats them as walls even before IsLiveTile
    /// would catch them. Returns null when no pattern is pending.
    /// </summary>
    public HashSet<Vector2Int> GetDangerTiles()
    {
        if (_pendingStrip == null || _pendingStrip.Count == 0) return null;
        return new HashSet<Vector2Int>(_pendingStrip);
    }

    /// <summary>
    /// Returns true if the given grid position is within the current live board bounds
    /// AND has not been selected/collapsed this fight.
    /// </summary>
    public bool IsLiveTile(Vector2Int pos) =>
        pos.x >= MinX && pos.x <= MaxX &&
        pos.y >= MinY && pos.y <= MaxY &&
        !_collapsed.Contains(pos);

    /// <summary>
    /// Returns true if the position is part of the current pending strip —
    /// the tiles that are highlighted red but have not collapsed yet.
    /// Players may voluntarily move onto these tiles.
    /// </summary>
    public bool IsPendingTile(Vector2Int pos) =>
        _pendingStrip != null && _pendingStrip.Contains(pos);

    /// <summary>
    /// Returns a read-only view of all positions that have been locked out this fight
    /// (both already-collapsed tiles and the current pending strip).
    /// Used by the AI snapshot so SimBoard.InBounds rejects them individually.
    /// </summary>
    public HashSet<Vector2Int> GetCollapsedTiles() =>
        new HashSet<Vector2Int>(_collapsed);

    // ── GameController hooks (called in this exact order each round) ──────────

    /// <summary>
    /// Step 1 — called by GameController BEFORE the AI takes its turn.
    /// If SD is active: picks the next random pattern and adds its tiles to
    /// _collapsed immediately so the AI's IsLiveTile checks already exclude them.
    /// No visual change yet — the player sees nothing until OnAfterAITurn.
    /// </summary>
    public void OnBeforeAITurn()
    {
        if (!_active) return;
        SelectNextPattern();
    }

    /// <summary>
    /// Step 3 — called by GameController AFTER the AI turn, BEFORE the player turn.
    /// If SD is active and a pattern was selected: paints those tiles red so the
    /// player has their full turn to see and react to the warning.
    /// </summary>
    public Coroutine OnAfterAITurn()
    {
        if (!_active || _pendingStrip == null || _pendingStrip.Count == 0) return null;
        return StartCoroutine(HighlightPendingRoutine());
    }

    /// <summary>
    /// Step 5 — called by GameController AFTER the player takes their turn.
    /// • If SD is not yet active: checks for stalemate and shows the announcement.
    /// • If SD is active and tiles are highlighted: collapses them immediately.
    /// Returns a coroutine to yield on, or null if nothing to do.
    /// </summary>
    public Coroutine OnAfterPlayerTurn()
    {
        if (!_active)
        {
            // Check whether SD should now activate.
            if (!EvaluateStalemate()) return null;
            // SD just activated — show the splash, then return so GameController
            // calls OnBeforeAITurn next (which will select the first pattern).
            return StartCoroutine(AnnouncementRoutine());
        }

        // SD already active — collapse the highlighted pattern.
        if (_pendingStrip != null && _pendingStrip.Count > 0)
            return StartCoroutine(CollapseRoutine(_pendingStrip));

        return null;
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    /// <summary>Shows the SUDDEN DEATH splash once then returns.</summary>
    private IEnumerator AnnouncementRoutine()
    {
        if (_announcementPending && announcementPanel != null)
        {
            _announcementPending = false;
            if (announcementLabel != null)
                announcementLabel.text = "SUDDEN DEATH";

            announcementPanel.SetActive(true);
            AudioManager.PlaySuddenDeathAnnouncement();
            yield return new WaitForSeconds(AnnouncementDuration);
            announcementPanel.SetActive(false);
        }
    }

    /// <summary>Paints the already-selected pattern red so the player can see the warning.</summary>
    private IEnumerator HighlightPendingRoutine()
    {
        foreach (Vector2Int pos in _pendingStrip)
        {
            if (pos.x >= 0 && pos.x < board.width &&
                pos.y >= 0 && pos.y < board.height &&
                board.tiles[pos.x, pos.y] != null)
            {
                board.tiles[pos.x, pos.y].Highlight(DangerColor, isDanger: true);
            }
        }

        AudioManager.PlayRedTileHighlight();

        Debug.Log($"[SuddenDeath] {_pendingStrip.Count} tile(s) shown to player. " +
                  $"Live bounds → ({MinX},{MinY})–({MaxX},{MaxY}).");
        yield break;
    }

    /// <summary>
    /// Kills any pieces on the pending strip, plays tile collapse animations,
    /// and waits for them to finish.
    /// </summary>
    private IEnumerator CollapseRoutine(List<Vector2Int> strip)
    {
        _pendingStrip = null;

        // Kill pieces first so they disappear before the tile animation.
        foreach (Vector2Int pos in strip)
        {
            if (pos.x < 0 || pos.x >= board.width ||
                pos.y < 0 || pos.y >= board.height)
                continue;

            Tile  tile     = board.tiles[pos.x, pos.y];
            Piece occupant = tile?.occupiedPiece;

            if (occupant != null)
            {
                tile.occupiedPiece = null;

                if (occupant.isPlayer) gameController.playerPieces.Remove(occupant);
                else                   gameController.enemyPieces .Remove(occupant);

                occupant.FadeOutAndDestroy();
                Debug.Log($"[SuddenDeath] {occupant.GetType().Name} crushed at {pos}.");

                if (gameController.CheckAndResolveWin())
                    yield break;
            }
        }

        Debug.Log($"[SuddenDeath] Collapsing {strip.Count} tile(s). " +
                  $"Live bounds → ({MinX},{MinY})–({MaxX},{MaxY}).");

        AudioManager.PlayTileCollapse();

        // Animate all tiles in parallel; wait for the longest to finish.
        var animations = new List<Coroutine>();
        foreach (Vector2Int pos in strip)
        {
            if (pos.x < 0 || pos.x >= board.width ||
                pos.y < 0 || pos.y >= board.height)
                continue;

            Tile tile = board.tiles[pos.x, pos.y];
            if (tile != null && tile.gameObject.activeSelf)
                animations.Add(tile.PlayCollapseAnimation());
        }

        foreach (Coroutine anim in animations)
            if (anim != null) yield return anim;
    }

    // ── Pattern selection ─────────────────────────────────────────────────────

    /// <summary>
    /// Picks the next random collapse pattern and immediately adds its tiles to
    /// <see cref="_collapsed"/> so <see cref="IsLiveTile"/> excludes them.
    /// Tiles occupied by player pieces are excluded from the strip so they can
    /// still move during the player's turn and are not silently crushed.
    /// No visual change yet — call <see cref="HighlightPendingRoutine"/> after the AI turn.
    /// </summary>
    private void SelectNextPattern()
    {
        if (MinX > MaxX || MinY > MaxY)
        {
            Debug.Log("[SuddenDeath] Board fully collapsed.");
            return;
        }

        List<Vector2Int> strip = PickRandomPattern();
        if (strip == null || strip.Count == 0)
        {
            Debug.LogWarning("[SuddenDeath] No valid collapse pattern found — board may be exhausted.");
            return;
        }

        // Lock tiles out of gameplay immediately so the AI treats them as walls.
        // Player-occupied tiles are intentionally kept in the strip: they are
        // highlighted red so the player is warned and can move off before collapse.
        // CollapseRoutine checks occupancy at collapse time, so pieces that moved
        // away are safe; pieces that stayed are crushed.
        foreach (Vector2Int pos in strip)
            _collapsed.Add(pos);

        AdvanceBoundsIfFullyCollapsed();

        _pendingStrip = strip;

        Debug.Log($"[SuddenDeath] Pattern selected: {strip.Count} tile(s). " +
                  $"Live bounds → ({MinX},{MinY})–({MaxX},{MaxY}). " +
                  $"Player will see highlight after AI turn.");
    }

    /// <summary>
    /// Randomly picks one of the three pattern types (row, column, 2×2 block),
    /// then picks a random position for that pattern anywhere within the live bounds.
    /// Falls back gracefully when the live area is too small for a given pattern.
    /// </summary>
    private List<Vector2Int> PickRandomPattern()
    {
        int liveW = MaxX - MinX + 1;
        int liveH = MaxY - MinY + 1;

        // Build the pool of available pattern types for the current board size.
        var pool = new List<CollapsePattern> { CollapsePattern.Row, CollapsePattern.Column };
        if (liveW >= 2 && liveH >= 2)
            pool.Add(CollapsePattern.Corner2x2);

        // Shuffle so the choice is truly random.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        // Try each type in shuffled order; return the first that produces tiles.
        foreach (CollapsePattern type in pool)
        {
            List<Vector2Int> strip = BuildPattern(type);
            if (strip != null && strip.Count > 0)
                return strip;
        }

        return null;
    }

    /// <summary>Builds the list of live tile positions for the given pattern type.</summary>
    private List<Vector2Int> BuildPattern(CollapsePattern type)
    {
        switch (type)
        {
            case CollapsePattern.Row:
            {
                // Randomly choose top or bottom edge.
                int y = UnityEngine.Random.value < 0.5f ? MinY : MaxY;
                var strip = new List<Vector2Int>();
                for (int x = MinX; x <= MaxX; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!_collapsed.Contains(pos))
                        strip.Add(pos);
                }
                return strip;
            }

            case CollapsePattern.Column:
            {
                // Randomly choose left or right edge.
                int x = UnityEngine.Random.value < 0.5f ? MinX : MaxX;
                var strip = new List<Vector2Int>();
                for (int y = MinY; y <= MaxY; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!_collapsed.Contains(pos))
                        strip.Add(pos);
                }
                return strip;
            }

            case CollapsePattern.Corner2x2:
            {
                // Always pick one of the four corners of the live bounds.
                // Origins are clamped so the 2×2 block never exceeds the live area.
                Vector2Int[] corners =
                {
                    new Vector2Int(MinX,     MinY),      // bottom-left
                    new Vector2Int(MaxX - 1, MinY),      // bottom-right
                    new Vector2Int(MinX,     MaxY - 1),  // top-left
                    new Vector2Int(MaxX - 1, MaxY - 1),  // top-right
                };

                // Shuffle corners so one isn't always preferred over another.
                for (int i = corners.Length - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (corners[i], corners[j]) = (corners[j], corners[i]);
                }

                foreach (Vector2Int origin in corners)
                {
                    var strip = new List<Vector2Int>();
                    for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        var pos = new Vector2Int(origin.x + dx, origin.y + dy);
                        if (!_collapsed.Contains(pos))
                            strip.Add(pos);
                    }

                    // Use the first corner that still has at least one live tile.
                    if (strip.Count > 0) return strip;
                }

                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Advances MinX/MaxX/MinY/MaxY inward whenever an entire edge row or column
    /// is now fully contained in <see cref="_collapsed"/>. Called after every highlight
    /// so the bounds always represent the tightest live rectangle.
    /// </summary>
    private void AdvanceBoundsIfFullyCollapsed()
    {
        while (MinX <= MaxX && IsColumnFullyCollapsed(MinX)) MinX++;
        while (MaxX >= MinX && IsColumnFullyCollapsed(MaxX)) MaxX--;
        while (MinY <= MaxY && IsRowFullyCollapsed(MinY))    MinY++;
        while (MaxY >= MinY && IsRowFullyCollapsed(MaxY))    MaxY--;
    }

    private bool IsColumnFullyCollapsed(int x)
    {
        for (int y = MinY; y <= MaxY; y++)
            if (!_collapsed.Contains(new Vector2Int(x, y))) return false;
        return true;
    }

    private bool IsRowFullyCollapsed(int y)
    {
        for (int x = MinX; x <= MaxX; x++)
            if (!_collapsed.Contains(new Vector2Int(x, y))) return false;
        return true;
    }

    // ── Stalemate detection ───────────────────────────────────────────────────

    private bool EvaluateStalemate()
    {
        List<Piece> playerAlive = gameController.playerPieces.FindAll(IsPieceAlive);
        List<Piece> enemyAlive  = gameController.enemyPieces .FindAll(IsPieceAlive);

        // 1v1 is always a draw — activate immediately.
        if (playerAlive.Count == 1 && enemyAlive.Count == 1)
        {
            _active              = true;
            _announcementPending = true;
            Debug.Log("[SuddenDeath] 1v1 detected — activated immediately.");
            return true;
        }

        // Opposite-color bishop pairs — structural draw.
        if (CanEverCapture(playerAlive, enemyAlive))
        {
            _staleCounter = 0;
            return false;
        }

        _staleCounter++;
        Debug.Log($"[SuddenDeath] No capture ever possible — stale count {_staleCounter}/{StalemateThreshold}.");

        if (_staleCounter < StalemateThreshold) return false;

        _active              = true;
        _announcementPending = true;
        Debug.Log("[SuddenDeath] Structural draw detected — activated.");
        return true;
    }

    private static bool CanEverCapture(List<Piece> playerAlive, List<Piece> enemyAlive)
    {
        foreach (Piece p in playerAlive)
            foreach (Piece e in enemyAlive)
                if (PiecesCanEverMeet(p, e))
                    return true;
        return false;
    }

    private static bool IsPieceAlive(Piece p) =>
        p != null && p.gameObject != null && p.gameObject.activeInHierarchy;

    /// <summary>Two pieces can ever meet unless both are bishops on different tile colors.</summary>
    private static bool PiecesCanEverMeet(Piece a, Piece b)
    {
        bool aIsBishop = a.ToSimPiece().Type == AIPieceType.Bishop;
        bool bIsBishop = b.ToSimPiece().Type == AIPieceType.Bishop;

        if (!aIsBishop || !bIsBishop) return true;

        int colorA = (a.position.x + a.position.y) % 2;
        int colorB = (b.position.x + b.position.y) % 2;
        return colorA == colorB;
    }
}
