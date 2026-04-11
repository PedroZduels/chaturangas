using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss effect for the Communist Grandmaster.
///
/// On activation:
///   - All non-upgraded player pieces are tinted red (cyan → red).
///   - HUD elements that implement IBossHudElement have their cyan accent switched to red.
///
/// Each player turn (every <see cref="spawnIntervalTurns"/>):
///   - A ghost pawn appears off-board above the backline and slides into a free cell on
///     row board.height-1 (enemy backline). The pawn is registered in game state only
///     after it fully lands, so it cannot interact with the board during animation.
///
/// On deactivation all tints and HUD colors are restored. Spawned pawns that survived
/// the fight remain on the board; staging pawns mid-animation are destroyed.
/// </summary>
[CreateAssetMenu(fileName = "CommunistGrandmasterEffect",
                 menuName  = "Chaturanga/Boss Effects/Communist Grandmaster")]
public class CommunistGrandmasterEffect : BossEffect
{
    [Header("Piece tint")]
    [Tooltip("Color applied to non-upgraded player pieces (replaces their normal cyan).")]
    public Color infectedTint = new Color(0.85f, 0.10f, 0.10f, 1f);

    [Header("HUD tint")]
    [Tooltip("Color applied to HUD elements that implement IBossHudElement.")]
    public Color hudTint = new Color(0.85f, 0.10f, 0.10f, 1f);

    [Header("Pawn spawn")]
    [Tooltip("Enemy pawn prefab spawned each interval.")]
    public GameObject enemyPawnPrefab;

    [Tooltip("Spawn one enemy pawn every N player turns.")]
    public int spawnIntervalTurns = 2;

    [Tooltip("How many tile-rows above the backline the pawn starts its entry animation.")]
    public float entryOffsetRows = 2f;

    [Tooltip("Duration in seconds of the pawn slide-in animation.")]
    public float entryDuration = 0.55f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private int _playerTurnCount;

    /// <summary>Original colors saved from IBossHudElement implementations, keyed by instance ID.</summary>
    private readonly Dictionary<int, Color> _originalHudColors = new Dictionary<int, Color>();

    /// <summary>GameObjects currently mid entry-animation. Destroyed on Deactivate if still in flight.</summary>
    private readonly List<GameObject> _stagingPawns = new List<GameObject>();

    // ── BossEffect overrides ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void Activate(GameController gameController)
    {
        _playerTurnCount = 0;
        _originalHudColors.Clear();
        _stagingPawns.Clear();

        TintPieces(gameController, apply: true);
        TintHUD(apply: true);
        TintBoard(gameController, apply: true);
    }

    /// <inheritdoc/>
    public override void OnPlayerTurnStarted(GameController gameController)
    {
        _playerTurnCount++;

        if (_playerTurnCount % spawnIntervalTurns == 0)
            StartCoroutine(gameController, SpawnBacklinePawnRoutine(gameController));
    }

    /// <inheritdoc/>
    public override void Deactivate(GameController gameController)
    {
        TintPieces(gameController, apply: false);
        TintBoard(gameController, apply: false);
        RestoreHUD();

        // Destroy pawns that are still mid-animation (they are off-board and unregistered).
        foreach (GameObject go in _stagingPawns)
            if (go != null) Object.Destroy(go);
        _stagingPawns.Clear();
    }

    /// <summary>
    /// Applies the infected red tint to a single piece spawned mid-fight
    /// (morph queen, promoted piece, etc.). Upgraded pieces are skipped.
    /// </summary>
    public override void ApplyTintToPiece(Piece piece)
    {
        if (piece == null || piece.isUpgraded) return;
        piece.ApplyOverrideTint(infectedTint);
    }

    // ── Piece tinting ─────────────────────────────────────────────────────────

    private void TintPieces(GameController gameController, bool apply)
    {
        // Player pieces: non-upgraded → infectedTint. Upgraded pieces keep their gold.
        foreach (Piece piece in gameController.playerPieces)
        {
            if (piece == null || piece.isUpgraded) continue;

            if (apply) piece.ApplyOverrideTint(infectedTint);
            else       piece.ClearOverrideTint();
        }

        // Enemy pieces: non-upgraded → infectedTint. Upgraded enemy pieces keep their purple.
        foreach (Piece piece in gameController.enemyPieces)
        {
            if (piece == null || piece.isUpgraded) continue;

            if (apply) piece.ApplyOverrideTint(infectedTint);
            else       piece.ClearOverrideTint();
        }
    }

    // ── Board tile tinting ────────────────────────────────────────────────────

    private void TintBoard(GameController gameController, bool apply)
    {
        BoardManager board = gameController.board;
        if (board == null) return;

        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                Tile tile = board.tiles[x, y];
                if (tile == null) continue;

                if (apply) tile.ApplyBossTint(infectedTint, _originalHudColors);
                else       tile.RestoreTint(_originalHudColors);
            }
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
                if (apply)
                    hudElement.ApplyBossTint(hudTint, _originalHudColors);
                else
                    hudElement.RestoreTint(_originalHudColors);
            }
        }
    }

    private void RestoreHUD()
    {
        TintHUD(apply: false);
        _originalHudColors.Clear();
    }

    // ── Pawn spawning ─────────────────────────────────────────────────────────

    /// <summary>
    /// Picks a free cell on the enemy backline, places the pawn off-board above it,
    /// animates it sliding onto the board, then registers it in game state on landing.
    /// Spawn is skipped entirely when Sudden Death is active (boss is in final mode).
    /// </summary>
    private IEnumerator SpawnBacklinePawnRoutine(GameController gameController)
    {
        if (enemyPawnPrefab == null)
        {
            Debug.LogWarning("[CommunistGrandmaster] enemyPawnPrefab is not assigned.");
            yield break;
        }

        // Do not spawn additional pawns once Sudden Death is active — the boss
        // should fight with only the pieces remaining on the board.
        if (gameController.suddenDeath != null && gameController.suddenDeath.IsActive)
        {
            Debug.Log("[CommunistGrandmaster] Sudden Death is active — pawn spawn suppressed.");
            yield break;
        }

        BoardManager board = gameController.board;
        int backlineY = board.height - 1;   // top row — enemy backline

        // Collect free cells in the backline that are still on the live board.
        var freeCells = new List<int>();
        for (int x = 0; x < board.width; x++)
        {
            var pos = new Vector2Int(x, backlineY);

            // Skip collapsed or pending sudden-death tiles.
            if (gameController.suddenDeath != null && !gameController.suddenDeath.IsLiveTile(pos))
                continue;

            if (board.tiles[x, backlineY].occupiedPiece == null)
                freeCells.Add(x);
        }

        if (freeCells.Count == 0)
        {
            Debug.Log("[CommunistGrandmaster] Backline is full — pawn spawn skipped.");
            yield break;
        }

        int chosenX          = freeCells[Random.Range(0, freeCells.Count)];
        Vector2Int targetPos = new Vector2Int(chosenX, backlineY);
        Vector3 targetWorld  = board.GridToWorld(targetPos);

        // ── Stage the pawn above the board ────────────────────────────────────
        // Offset by entryOffsetRows above the target cell; z slightly in front so
        // it renders above the board edge during animation.
        float yOffset    = entryOffsetRows * board.tileSize;
        Vector3 startPos = targetWorld + new Vector3(0f, yOffset, -0.1f);

        GameObject obj = Object.Instantiate(enemyPawnPrefab, startPos, Quaternion.identity, board.transform);
        _stagingPawns.Add(obj);

        Piece pawn = obj.GetComponent<Piece>();
        if (pawn == null)
        {
            Debug.LogError("[CommunistGrandmaster] enemyPawnPrefab has no Piece component.");
            Object.Destroy(obj);
            _stagingPawns.Remove(obj);
            yield break;
        }

        // Configure state so BillboardAndSort and tinting work during animation,
        // but do NOT register on the board or enemy list yet.
        pawn.position     = targetPos;
        pawn.isPlayer     = false;
        pawn.board        = board;
        pawn.sourcePrefab = enemyPawnPrefab;
        pawn.ApplyTint();
        pawn.ApplyOverrideTint(infectedTint);   // boss red, matching all other enemy pieces

        Debug.Log($"[CommunistGrandmaster] Pawn staging above column {chosenX}, entering board…");

        // ── Slide-in animation ────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < entryDuration)
        {
            elapsed            += Time.deltaTime;
            float t             = Mathf.Clamp01(elapsed / entryDuration);
            float eased         = Mathf.SmoothStep(0f, 1f, t);
            obj.transform.position = Vector3.Lerp(startPos, targetWorld, eased);
            yield return null;
        }

        // Snap to the exact board position.
        pawn.PlaceAt(targetWorld);
        _stagingPawns.Remove(obj);

        // ── Register in game state ────────────────────────────────────────────
        // Safety: the target cell might have been occupied during the animation.
        if (board.tiles[targetPos.x, targetPos.y].occupiedPiece != null)
        {
            Debug.Log("[CommunistGrandmaster] Target cell occupied on landing — pawn discarded.");
            Object.Destroy(obj);
            yield break;
        }

        board.tiles[targetPos.x, targetPos.y].occupiedPiece = pawn;
        pawn.isBossSpawned = true;
        gameController.enemyPieces.Add(pawn);

        Debug.Log($"[CommunistGrandmaster] Enemy pawn landed at column {chosenX} (turn {_playerTurnCount}).");
    }
}
