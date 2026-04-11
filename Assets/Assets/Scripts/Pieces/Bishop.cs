using System.Collections.Generic;
using UnityEngine;

public class Bishop : Piece
{
    private static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1)
    };

    private static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// Diagonal sliding moves for all bishop tiers.
    /// Upgraded bishop also adds one-step cardinal moves.
    /// </summary>
    public override List<Vector2Int> GetLegalMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();

        // All bishops slide diagonally.
        foreach (var dir in DiagonalDirs)
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
                    else if (tile.occupiedPiece.isPlayer == isPlayer)
                    {
                        if (isEthereal) continue;
                        break;
                    }
                    else
                    {
                        moves.Add(cur);
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

        // Upgraded bishop: also move one step in any cardinal direction.
        if (isUpgraded)
        {
            foreach (var dir in CardinalDirs)
            {
                Vector2Int target = position + dir;
                if (!board.IsInsideBoardOrPending(target)) continue;

                Tile tile = board.tiles[target.x, target.y];
                if (tile.occupiedPiece != null && tile.occupiedPiece.isPlayer == isPlayer) continue;

                moves.Add(target);
            }
        }

        return moves;
    }

    public override ISimPiece ToSimPiece() =>
        new SimBishop(isPlayer, position, armorCount,
                      turnsLeft: isStunned ? 0 : -1,
                      isUpgraded: isUpgraded,
                      isGlitched: isGlitched);
}

