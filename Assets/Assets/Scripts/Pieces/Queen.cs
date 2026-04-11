using System.Collections.Generic;
using UnityEngine;

public class Queen : Piece
{
    private static readonly Vector2Int[] AllDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1)
    };

    /// <summary>Slides in all 8 directions. Upgraded queen retreats after capturing (handled in GameController).</summary>
    public override List<Vector2Int> GetLegalMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();
        foreach (var dir in AllDirs)
        {
            Vector2Int cur = position;
            while (true)
            {
                cur += dir;

                if (board.IsInsideBoardOrPending(cur))
                {
                    Tile tile = board.tiles[cur.x, cur.y];
                    if (tile.occupiedPiece == null)
                        moves.Add(cur);
                    else
                    {
                        if (tile.occupiedPiece.isPlayer != isPlayer) moves.Add(cur);
                        break;
                    }
                }
                else if (board.IsInsideBoardForCapture(cur))
                {
                    // Danger strip — include only if there's an enemy to capture.
                    Tile tile = board.tiles[cur.x, cur.y];
                    if (tile.occupiedPiece != null && tile.occupiedPiece.isPlayer != isPlayer)
                        moves.Add(cur);
                    break;
                }
                else break;
            }
        }
        return moves;
    }

    /// <summary>
    /// Compound Strike upgrade: returns the square the queen retreats to after capturing.
    /// Retreats along the same ray back toward her starting square, landing on the first
    /// empty square (or her origin if nothing is free).
    /// Returns null if the queen cannot retreat (e.g., blocked immediately).
    /// </summary>
    public Vector2Int? GetRetreatSquare(Vector2Int from, Vector2Int capturePos, BoardManager board)
    {
        Vector2Int dir = new Vector2Int(
            Mathf.Clamp(capturePos.x - from.x, -1, 1),
            Mathf.Clamp(capturePos.y - from.y, -1, 1));

        Vector2Int retreatDir = -dir;
        Vector2Int cur = capturePos + retreatDir;

        while (board.IsInsideBoard(cur))
        {
            Tile tile = board.tiles[cur.x, cur.y];
            if (tile.occupiedPiece == null)
                return cur;

            if (cur == from)
                return from;

            cur += retreatDir;
        }

        return from; // fallback: return to origin
    }

    public override ISimPiece ToSimPiece() =>
        new SimQueen(isPlayer, position, armorCount, turnsLeft: isStunned ? 0 : -1, isUpgraded: isUpgraded, isGlitched: isGlitched);
}


