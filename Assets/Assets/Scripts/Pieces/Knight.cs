using System.Collections.Generic;
using UnityEngine;

public class Knight : Piece
{
    private static readonly Vector2Int[] BaseLeaps =
    {
        new Vector2Int( 1,  2), new Vector2Int( 2,  1),
        new Vector2Int(-1,  2), new Vector2Int(-2,  1),
        new Vector2Int( 1, -2), new Vector2Int( 2, -1),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1)
    };

    private static readonly Vector2Int[] ExtendedLeaps =
    {
        new Vector2Int( 1,  2), new Vector2Int( 2,  1),
        new Vector2Int(-1,  2), new Vector2Int(-2,  1),
        new Vector2Int( 1, -2), new Vector2Int( 2, -1),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1),
        new Vector2Int( 1,  3), new Vector2Int( 3,  1),
        new Vector2Int(-1,  3), new Vector2Int(-3,  1),
        new Vector2Int( 1, -3), new Vector2Int( 3, -1),
        new Vector2Int(-1, -3), new Vector2Int(-3, -1)
    };

    /// <summary>Upgraded knights use extended (3,1) leaps. Knights always pierce armor.</summary>
    public override List<Vector2Int> GetLegalMoves(BoardManager board)
    {
        var moves  = new List<Vector2Int>();
        var leaps  = isUpgraded ? ExtendedLeaps : BaseLeaps;

        foreach (var leap in leaps)
        {
            Vector2Int target = position + leap;
            Tile tile;

            if (board.IsInsideBoardOrPending(target))
            {
                tile = board.tiles[target.x, target.y];
            }
            else if (board.IsInsideBoardForCapture(target))
            {
                // Target is in a danger strip — only allow it when there's an enemy to capture.
                tile = board.tiles[target.x, target.y];
                if (tile.occupiedPiece == null || tile.occupiedPiece.isPlayer == isPlayer) continue;
            }
            else continue;

            if (tile.occupiedPiece == null || tile.occupiedPiece.isPlayer != isPlayer)
                moves.Add(target);
        }
        return moves;
    }

    public override ISimPiece ToSimPiece() =>
        new SimKnight(isPlayer, position, armorCount, turnsLeft: isStunned ? 0 : -1, isUpgraded: isUpgraded, isGlitched: isGlitched);
}
