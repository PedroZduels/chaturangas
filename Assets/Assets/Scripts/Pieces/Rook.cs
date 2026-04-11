using System.Collections.Generic;
using UnityEngine;

public class Rook : Piece
{
    private static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// Cardinal sliding moves.
    /// Upgraded rooks phase through enemy pawns without capturing them.
    /// Ethereal rooks also phase through friendly pieces.
    /// </summary>
    public override List<Vector2Int> GetLegalMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();

        foreach (var dir in CardinalDirs)
        {
            Vector2Int cur = position;
            while (true)
            {
                cur += dir;

                if (board.IsInsideBoardOrPending(cur))
                {
                    Tile tile = board.tiles[cur.x, cur.y];
                    if (tile.occupiedPiece == null)
                    {
                        moves.Add(cur);
                    }
                    else if (tile.occupiedPiece.isPlayer == isPlayer)
                    {
                        // Friendly piece: Ethereal can pass through, otherwise blocked.
                        if (isEthereal) continue;
                        break;
                    }
                    else
                    {
                        moves.Add(cur);
                        if (isUpgraded && tile.occupiedPiece is Pawn)
                            continue;
                        break;
                    }
                }
                else if (board.IsInsideBoardForCapture(cur))
                {
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

    public override ISimPiece ToSimPiece() =>
        new SimRook(isPlayer, position, armorCount, turnsLeft: isStunned ? 0 : -1, isUpgraded: isUpgraded, isGlitched: isGlitched);
}
