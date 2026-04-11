using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BolbiPhisherEffect",
                 menuName = "Chaturanga/Boss Effects/Bolbi Phisher")]
public class BolbiPhisherEffect : BossEffect
{
    [Header("Piece tint")]
    public Color infectedTint = new Color(0.45f, 0.80f, 1.00f, 1f);  // light blue

    [Header("HUD tint")]
    public Color hudTint = new Color(0.45f, 0.80f, 1.00f, 1f);

    [Header("Hack ability")]
    [Tooltip("Bolbi designates one player piece to hack every N player turns.")]
    public int hackIntervalTurns = 2;

    [Tooltip("How long the target piece flickers on the player's turn before the hack executes.")]
    public float flickerDuration = 1.2f;

    [Tooltip("Animation time for the hacked piece's forced move.")]
    public float moveDuration = 0.35f;

    // Piece currently selected for hacking — flickers on the player's turn,
    // then gets force-moved at the start of Bolbi's turn.
    private Piece _hackedTarget;

    private int _playerTurnCount;
    private readonly Dictionary<int, Color> _originalHudColors = new();

    // ── BossEffect lifecycle ──────────────────────────────────────────────────

    public override void Activate(GameController gc)
    {
        _playerTurnCount = 0;
        _hackedTarget    = null;
        _originalHudColors.Clear();
        TintPieces(gc, true);
        TintBoard(gc, true);
        TintHUD(true);
        AudioManager.SwitchContext(AudioManager.MusicContext.Boss2);
    }

    /// <summary>
    /// Player turn: every hackIntervalTurns, designate a random player piece.
    /// It starts flickering immediately so the player knows which one Bolbi will move next turn.
    /// </summary>
    public override void OnPlayerTurnStarted(GameController gc)
    {
        _playerTurnCount++;

        // Clear any previous target's flicker first (safety in case fight reset).
        ClearHackedTarget();

        if (_playerTurnCount % hackIntervalTurns != 0) return;

        _hackedTarget = PickHackTarget(gc);
        if (_hackedTarget == null) return;

        _hackedTarget.StartHackFlicker();
        Debug.Log($"[Bolbi] Designated '{_hackedTarget.GetType().Name}' at {_hackedTarget.position} " +
                  $"— Bolbi will move it next turn.");
    }

    /// <summary>
    /// AI turn: force-move the hacked player piece to the empty square most
    /// threatened by Bolbi's own pieces — setting up a capture on a future turn.
    /// Never captures other pieces (the hacked piece only repositions).
    /// Then GameController proceeds with Bolbi's own normal AI move.
    /// </summary>
    public override IEnumerator OnEnemyTurnStarted(GameController gc)
    {
        if (_hackedTarget == null) yield break;

        Piece target = _hackedTarget;
        ClearHackedTarget();

        // Safety: piece may have been captured on the player's last move.
        if (target == null || !gc.playerPieces.Contains(target)) yield break;

        // Suppress during Sudden Death.
        if (gc.suddenDeath != null && gc.suddenDeath.IsActive) yield break;

        target.StopHackFlicker();

        // Find the empty legal square most attacked by Bolbi's pieces.
        Vector2Int? dest = FindMostExposedSquare(gc, target);
        if (dest == null)
        {
            Debug.Log($"[Bolbi] No threatened empty square reachable by {target.GetType().Name} — skipping hack.");
            yield break;
        }

        // Relocate: update board state directly (no rewards, no captures).
        Vector2Int from = target.position;
        gc.board.tiles[from.x, from.y].occupiedPiece = null;
        gc.board.tiles[dest.Value.x, dest.Value.y].occupiedPiece = target;
        target.position = dest.Value;

        // Animate the slide.
        Vector3 worldDest = gc.board.GridToWorld(dest.Value);
        yield return gc.StartEffectCoroutine(target.TweenMoveTo(worldDest, moveDuration));

        AudioManager.PlayMove();
        Debug.Log($"[Bolbi] Hacked {target.GetType().Name} from {from} → {dest.Value} " +
                  $"(exposed to Bolbi's pieces).");
    }

    public override void Deactivate(GameController gc)
    {
        ClearHackedTarget();
        TintPieces(gc, false);
        TintBoard(gc, false);
        RestoreHUD();
    }

    public override void ApplyTintToPiece(Piece piece)
    {
        if (piece == null || piece.isUpgraded) return;
        piece.ApplyOverrideTint(infectedTint);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ClearHackedTarget()
    {
        if (_hackedTarget == null) return;
        _hackedTarget.StopHackFlicker();
        _hackedTarget = null;
    }

    /// <summary>
    /// Picks a random non-King player piece to hack.
    /// Prefers pieces that have at least one legal move so the hack is never wasted.
    /// </summary>
    private static Piece PickHackTarget(GameController gc)
    {
        var movable  = new List<Piece>();
        var fallback = new List<Piece>();

        foreach (Piece p in gc.playerPieces)
        {
            if (p == null || p is King) continue;
            fallback.Add(p);
            if (p.GetLegalMoves(gc.board).Count > 0)
                movable.Add(p);
        }

        List<Piece> pool = movable.Count > 0 ? movable : fallback;
        return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
    }

    /// <summary>
    /// Among the empty squares that <paramref name="target"/> can legally reach,
    /// returns the one attacked by the most of Bolbi's pieces.
    /// Returns null when no such square exists (piece stays put).
    /// </summary>
    private static Vector2Int? FindMostExposedSquare(GameController gc, Piece target)
    {
        BoardManager board = gc.board;

        // Build threat map: count how many of Bolbi's pieces attack each square.
        var threatCount = new Dictionary<Vector2Int, int>();
        foreach (Piece enemy in gc.enemyPieces)
        {
            if (enemy == null) continue;
            foreach (Vector2Int sq in enemy.GetLegalMoves(board))
            {
                if (!threatCount.ContainsKey(sq)) threatCount[sq] = 0;
                threatCount[sq]++;
            }
        }

        // Consider only legal moves of the hacked piece that land on empty squares.
        // (No captures — the goal is to reposition the piece into danger, not to take anyone.)
        Vector2Int? best     = null;
        int         bestScore = 0;   // require at least 1 attacker

        foreach (Vector2Int pos in target.GetLegalMoves(board))
        {
            if (pos == target.position)            continue;
            if (board.tiles[pos.x, pos.y].occupiedPiece != null) continue;   // empty only
            if (gc.suddenDeath != null && gc.suddenDeath.IsActive &&
                !gc.suddenDeath.IsLiveTile(pos))   continue;

            int score = threatCount.TryGetValue(pos, out int t) ? t : 0;
            if (score > bestScore) { bestScore = score; best = pos; }
        }

        return best;
    }

    // ── Piece tinting ─────────────────────────────────────────────────────────

    private void TintPieces(GameController gc, bool apply)
    {
        foreach (Piece piece in gc.playerPieces)
        {
            if (piece == null || piece.isUpgraded) continue;
            if (apply) piece.ApplyOverrideTint(infectedTint);
            else       piece.ClearOverrideTint();
        }
        foreach (Piece piece in gc.enemyPieces)
        {
            if (piece == null || piece.isUpgraded) continue;
            if (apply) piece.ApplyOverrideTint(infectedTint);
            else       piece.ClearOverrideTint();
        }
    }

    // ── Board tile tinting ────────────────────────────────────────────────────

    private void TintBoard(GameController gc, bool apply)
    {
        BoardManager board = gc.board;
        if (board == null) return;

        for (int x = 0; x < board.width; x++)
        for (int y = 0; y < board.height; y++)
        {
            Tile tile = board.tiles[x, y];
            if (tile == null) continue;

            if (apply) tile.ApplyBossTint(infectedTint, _originalHudColors);
            else       tile.RestoreTint(_originalHudColors);
        }
    }

    // ── HUD tinting ───────────────────────────────────────────────────────────

    private void TintHUD(bool apply)
    {
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in all)
        {
            if (mb is IBossHudElement hudElement)
            {
                if (apply) hudElement.ApplyBossTint(hudTint, _originalHudColors);
                else       hudElement.RestoreTint(_originalHudColors);
            }
        }
    }

    private void RestoreHUD()
    {
        TintHUD(apply: false);
        _originalHudColors.Clear();
    }
}
