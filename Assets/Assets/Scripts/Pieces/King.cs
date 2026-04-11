using System.Collections.Generic;
using UnityEngine;

public class King : Piece
{
    private static readonly Vector2Int[] AllDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1)
    };

    /// <summary>One-step moves in all 8 directions.</summary>
    public override List<Vector2Int> GetLegalMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();
        foreach (var dir in AllDirs)
        {
            Vector2Int target = position + dir;
            Tile tile;

            if (board.IsInsideBoardOrPending(target))
            {
                tile = board.tiles[target.x, target.y];
            }
            else if (board.IsInsideBoardForCapture(target))
            {
                // Danger strip — only allow stepping in if there's an enemy to capture.
                tile = board.tiles[target.x, target.y];
                if (tile.occupiedPiece == null || tile.occupiedPiece.isPlayer == isPlayer) continue;
            }
            else continue;

            if (tile.occupiedPiece == null || tile.occupiedPiece.isPlayer != isPlayer)
                moves.Add(target);
        }
        return moves;
    }

    /// <summary>
    /// Armor Guard upgrade: grants 1 armor to the three pieces on the row directly
    /// in front of this king (i.e. one rank closer to the enemy).
    /// </summary>
    public void GrantFrontArmor(BoardManager board)
    {
        int frontY = isPlayer ? position.y + 1 : position.y - 1;

        for (int x = 0; x < board.width; x++)
        {
            if (!board.IsInsideBoard(new Vector2Int(x, frontY))) continue;
            Piece front = board.tiles[x, frontY].occupiedPiece;
            if (front != null && front.isPlayer == isPlayer)
                front.GainArmor(1);
        }
    }

    public override ISimPiece ToSimPiece() =>
        new SimKing(isPlayer, position, armorCount, turnsLeft: isStunned ? 0 : -1, isUpgraded: isUpgraded, isGlitched: isGlitched);
}


