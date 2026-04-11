using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Minimax with alpha-beta pruning for the enemy AI.
/// randomness ∈ [0,1] adds jitter to move ordering and evaluation so lower depths feel human-like.
/// </summary>
public static class ChessAI
{

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a SimBoard snapshot from the live scene pieces.
    /// Pass the live SD bounds so the AI never generates moves onto collapsed tiles.
    /// Call once per AI turn before passing to GetBestEnemyMove.
    /// </summary>
    public static SimBoard SnapshotBoard(BoardManager board,
                                          List<Piece> playerPieces,
                                          List<Piece> enemyPieces,
                                          SuddenDeathManager suddenDeath = null)
    {
        var sim = new SimBoard(board.width, board.height);
        foreach (Piece p in playerPieces)
            if (p != null && p.gameObject != null)
                sim.Set(p.position, p.ToSimPiece());
        foreach (Piece p in enemyPieces)
            if (p != null && p.gameObject != null)
                sim.Set(p.position, p.ToSimPiece());

        if (suddenDeath != null && suddenDeath.IsActive)
            sim.SetLiveBounds(suddenDeath.MinX, suddenDeath.MaxX,
                              suddenDeath.MinY, suddenDeath.MaxY,
                              suddenDeath.GetCollapsedTiles());
        else
            sim.SetLiveBounds(0, board.width - 1, 0, board.height - 1);

        return sim;
    }

    /// <summary>
    /// Returns the best action for a specific hacked enemy piece at <paramref name="piecePos"/>.
    /// Returns null if the piece has no legal moves (blocked or stunned).
    /// </summary>
    public static SimAction GetBestMoveForPiece(SimBoard board, Vector2Int piecePos,
                                                 int depth, float randomness,
                                                 HashSet<Vector2Int> dangerTiles = null)
    {
        ISimPiece hackedSim = board.Get(piecePos);
        if (hackedSim == null || hackedSim.TurnsLeft == 0) return null;

        var actions = hackedSim.GetActions(board);
        if (actions.Count == 0) return null;

        SimAction best      = null;
        float     bestScore = float.NegativeInfinity;

        foreach (var action in actions)
        {
            SimBoard next  = board.ApplyAction(action).TickStuns(isPlayer: false);
            float    score = Search(next, depth - 1,
                                    float.NegativeInfinity, float.PositiveInfinity,
                                    maximizing: false, randomness, dangerTiles);
            if (score > bestScore)
            {
                bestScore = score;
                best      = action;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns the best action for the enemy, or null if no legal moves exist.
    /// Pass <paramref name="dangerTiles"/> (the next ring to collapse) so the AI
    /// will strongly prefer evacuating pieces off those tiles, even at tactical cost.
    /// </summary>
    public static SimAction GetBestEnemyMove(SimBoard board, int depth, float randomness,
                                              HashSet<Vector2Int> dangerTiles = null)
    {
        var actions = CollectOrderedActions(board, forEnemy: true, randomness, dangerTiles);

        // ── Diagnostic: log every piece visible to the sim and its action count ──
        Debug.Log($"[AI-Sim] Board bounds ({board.MinX},{board.MinY})–({board.MaxX},{board.MaxY}). " +
                  $"Total root actions: {actions.Count}");
        {
            var allEnemy  = board.GetAllPieces(isPlayer: false);
            var allPlayer = board.GetAllPieces(isPlayer: true);
            Debug.Log($"[AI-Sim] Enemy pieces in snapshot: {allEnemy.Count}  " +
                      $"Player pieces in snapshot: {allPlayer.Count}");
            foreach (var p in allEnemy)
            {
                var pActs = p.GetActions(board);
                Debug.Log($"[AI-Sim]   ENEMY {p.Type} @ ({p.Pos.x},{p.Pos.y})  " +
                          $"TurnsLeft={p.TurnsLeft}  actions={pActs.Count}");
                // Log what's blocking each square ahead
                int dir = -1; // enemy always goes down
                var fwd = p.Pos + new Vector2Int(0, dir);
                var occ = board.InBounds(fwd) ? board.Get(fwd) : null;
                Debug.Log($"[AI-Sim]     fwd={fwd}  inBounds={board.InBounds(fwd)}  " +
                          $"occupant={( occ == null ? "null" : occ.Type + "(isPlayer=" + occ.IsPlayer + ")")}");
            }
        }
        // ─────────────────────────────────────────────────────────────────────────

        if (actions.Count == 0) return null;

        SimAction best      = null;
        float     bestScore = float.NegativeInfinity;

        foreach (var action in actions)
        {
            // After enemy acts, tick enemy stuns so they expire before the player moves.
            SimBoard next  = board.ApplyAction(action).TickStuns(isPlayer: false);
            float    score = Search(next, depth - 1,
                                    float.NegativeInfinity, float.PositiveInfinity,
                                    maximizing: false, randomness, dangerTiles);
            if (score > bestScore)
            {
                bestScore = score;
                best      = action;
            }
        }

        return best;
    }

    // ── Minimax ───────────────────────────────────────────────────────────────

    private static float Search(SimBoard board, int depth,
                                 float alpha, float beta,
                                 bool maximizing, float randomness,
                                 HashSet<Vector2Int> dangerTiles)
    {
        var enemyPieces  = board.GetAllPieces(isPlayer: false);
        var playerPieces = board.GetAllPieces(isPlayer: true);

        if (enemyPieces.Count  == 0) return -1000f;
        if (playerPieces.Count == 0) return  1000f;
        if (depth == 0) return Evaluate(board, randomness, dangerTiles);

        if (maximizing)
        {
            float best    = float.NegativeInfinity;
            var   actions = CollectOrderedActions(board, forEnemy: true, randomness, dangerTiles);
            if (actions.Count == 0) return Evaluate(board, randomness, dangerTiles);

            foreach (var a in actions)
            {
                // Enemy acted — tick enemy stuns before player responds.
                SimBoard next  = board.ApplyAction(a).TickStuns(isPlayer: false);
                float    score = Search(next, depth - 1, alpha, beta, false, randomness, dangerTiles);
                best  = Mathf.Max(best, score);
                alpha = Mathf.Max(alpha, best);
                if (beta <= alpha) break;
            }
            return best;
        }
        else
        {
            float best    = float.PositiveInfinity;
            var   actions = CollectOrderedActions(board, forEnemy: false, randomness, dangerTiles);
            if (actions.Count == 0) return Evaluate(board, randomness, dangerTiles);

            foreach (var a in actions)
            {
                // Player acted — tick player stuns before enemy responds.
                SimBoard next  = board.ApplyAction(a).TickStuns(isPlayer: true);
                float    score = Search(next, depth - 1, alpha, beta, true, randomness, dangerTiles);
                best = Mathf.Min(best, score);
                beta = Mathf.Min(beta, best);
                if (beta <= alpha) break;
            }
            return best;
        }
    }

    // ── Evaluation ────────────────────────────────────────────────────────────

    private static float Evaluate(SimBoard board, float randomness,
                                   HashSet<Vector2Int> dangerTiles)
    {
        float score = 0f;
        for (int x = board.MinX; x <= board.MaxX; x++)
            for (int y = board.MinY; y <= board.MaxY; y++)
            {
                var pos = new Vector2Int(x, y);
                var p   = board.Get(pos);
                if (p == null) continue;

                float val = PieceValue(p);
                score += p.IsPlayer ? -val : val;

                // Moderate penalty for any enemy piece still on a danger tile.
                // Sized below a pawn value so the AI won't sacrifice captures just to escape;
                // it still strongly prefers not leaving pieces on the strip at depth.
                if (!p.IsPlayer && dangerTiles != null && dangerTiles.Contains(pos))
                    score -= 8f;
            }

        // Small noise to avoid deterministic repetition at low depths.
        score += Random.Range(-randomness, randomness) * 2f;
        return score;
    }

    private static float PieceValue(ISimPiece piece)
    {
        float val = piece.Type switch
        {
            AIPieceType.Pawn   => 10f,
            AIPieceType.Knight => 30f,
            AIPieceType.Bishop => 30f,
            AIPieceType.Rook   => 50f,
            AIPieceType.Queen  => 90f,
            AIPieceType.King   => 900f,
            _                  => 0f
        };
        return val + piece.ArmorCount * 5f;
    }

    // ── Move ordering ─────────────────────────────────────────────────────────

    private static List<SimAction> CollectOrderedActions(SimBoard board, bool forEnemy,
                                                          float randomness,
                                                          HashSet<Vector2Int> dangerTiles)
    {
        var actions = board.GetAllActions(!forEnemy);

        // When generating actions FOR the enemy, try to strip moves whose destination
        // is a danger tile — the AI prefers not to step into the collapsing strip.
        // If filtering would leave NO moves at all we keep the full list so the AI
        // never silently passes a turn just because every square is a danger tile.
        if (forEnemy && dangerTiles != null && dangerTiles.Count > 0)
        {
            var safe = actions.FindAll(a =>
            {
                Vector2Int dest = a switch
                {
                    MoveAction     m => m.To,
                    CaptureAction  c => c.To,
                    CompoundAction q => q.RetreatTo,
                    _               => new Vector2Int(-1, -1)
                };
                return !dangerTiles.Contains(dest);
            });

            // Only apply the filter when it still leaves at least one legal move.
            if (safe.Count > 0)
                actions = safe;
            // else: all destinations are danger tiles — accept them to avoid a silent pass.
        }

        var scored = actions
            .Select(a => (action: a, score: ScoreAction(board, a, randomness, dangerTiles, forEnemy)))
            .OrderByDescending(s => s.score)
            .Select(s => s.action)
            .ToList();

        return scored;
    }

    private static float ScoreAction(SimBoard board, SimAction action, float randomness,
                                      HashSet<Vector2Int> dangerTiles, bool forEnemy)
    {
        float score = 0f;

        // ── Capture value ─────────────────────────────────────────────────────

        if (action is CaptureAction c)
        {
            ISimPiece attacker = board.Get(c.From);
            ISimPiece victim   = board.Get(c.To);
            if (attacker != null && victim != null)
                score += PieceValue(victim) - PieceValue(attacker) * 0.1f;
        }

        // ── Survival bonus (escape from danger tile) ──────────────────────────
        // Only apply escape bonus when the piece is on a danger tile.
        // Importantly, captures from a danger tile ALREADY score the victim's value
        // above, so captures remain more attractive than a pure escape move.
        // The bonus is sized so that a piece on a danger tile prefers ANY move off
        // it over sitting still (e.g. a non-capture move), but does not outweigh
        // a clean capture regardless of whether the attacker starts on danger.
        if (forEnemy && dangerTiles != null)
        {
            Vector2Int from = action switch
            {
                MoveAction    m  => m.From,
                CaptureAction ca => ca.From,
                _               => new Vector2Int(-1, -1)
            };

            if (from.x >= 0 && dangerTiles.Contains(from))
            {
                // Escape bonus is capped below the value of the weakest capturable piece (Pawn = 10),
                // so a capture always beats a plain escape, but escaping beats doing nothing.
                score += 8f;
            }
        }

        // Jitter — makes lower randomness feel more predictable and human.
        score += Random.Range(-randomness, randomness) * 20f;
        return score;
    }
}
