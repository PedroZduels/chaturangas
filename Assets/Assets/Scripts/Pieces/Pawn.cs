using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    /// <summary>
    /// Player pawns advance +Y, enemy pawns -Y.
    /// Upgraded pawns can double-step from any rank (both squares ahead must be empty).
    /// </summary>
    public override List<Vector2Int> GetLegalMoves(BoardManager board)
    {
        var moves      = new List<Vector2Int>();
        int forwardDir = isPlayer ? 1 : -1;

        // Single forward step
        Vector2Int fwd = position + new Vector2Int(0, forwardDir);
        if (board.IsInsideBoardOrPending(fwd) && board.tiles[fwd.x, fwd.y].occupiedPiece == null)
        {
            moves.Add(fwd);

            // Double step (upgraded — from any rank, path must be clear)
            if (isUpgraded)
            {
                Vector2Int fwd2 = position + new Vector2Int(0, forwardDir * 2);
                if (board.IsInsideBoardOrPending(fwd2) && board.tiles[fwd2.x, fwd2.y].occupiedPiece == null)
                    moves.Add(fwd2);
            }
        }

        // Diagonal captures — allowed into danger strips when an enemy is there
        foreach (var capOff in new[] { new Vector2Int(1, forwardDir), new Vector2Int(-1, forwardDir) })
        {
            Vector2Int cap = position + capOff;
            // Use the capture-aware bounds: player may step into a collapsing strip to win.
            if (!board.IsInsideBoardForCapture(cap)) continue;
            Tile tile = board.tiles[cap.x, cap.y];
            if (tile.occupiedPiece != null && tile.occupiedPiece.isPlayer != isPlayer)
                moves.Add(cap);
        }

        return moves;
    }

    public override ISimPiece ToSimPiece() =>
        new SimPawn(isPlayer, position, armorCount, turnsLeft: isStunned ? 0 : -1, isUpgraded: isUpgraded, isGlitched: isGlitched);
}

