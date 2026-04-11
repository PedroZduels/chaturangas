using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Randomises enemy spawn positions on the enemy's back rows before a fight starts.
/// Enemies always spawn on the top two rows of the board (highest Y values).
/// Pawns are preferred on the front enemy row (second-to-last) so they face the
/// player. All other pieces are preferred on the back row (last row).
/// Pieces fall back to the opposite row when their preferred row is full.
/// No two pieces share the same tile.
/// </summary>
public static class EnemySquadPlacer
{
    /// <summary>
    /// Assigns random, collision-free board positions to all entries in
    /// <paramref name="pieces"/> using the top two rows of the board.
    /// Call this on the runtime clone of a SquadDefinition, never on the asset.
    /// </summary>
    public static void Randomise(List<PieceSpawnData> pieces, int boardWidth, int boardHeight)
    {
        if (pieces == null || pieces.Count == 0) return;

        // Enemy rows: last row (back) and second-to-last row (front).
        int backRow  = boardHeight - 1;
        int frontRow = boardHeight - 2;

        // Guard: board must be at least 2 rows tall.
        if (frontRow < 0)
        {
            Debug.LogWarning("[EnemySquadPlacer] Board is too small for two enemy rows — using row 0 only.");
            frontRow = backRow;
        }

        // Build pools of available columns for each row.
        List<int> backCols  = ShuffledColumns(boardWidth);
        List<int> frontCols = ShuffledColumns(boardWidth);

        // Shared occupied set so pieces from one row can't collide with the other.
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        // Separate pawns from the rest so we can place them on the preferred row.
        List<PieceSpawnData> pawns  = new List<PieceSpawnData>();
        List<PieceSpawnData> others = new List<PieceSpawnData>();

        foreach (PieceSpawnData p in pieces)
        {
            if (p.piecePrefab != null && p.piecePrefab.GetComponent<Pawn>() != null)
                pawns.Add(p);
            else
                others.Add(p);
        }

        // Place pawns on the front row first, fall back to back row.
        AssignRow(pawns,  frontRow, backRow,  frontCols, backCols, occupied, boardWidth);

        // Place other pieces on the back row first, fall back to front row.
        AssignRow(others, backRow,  frontRow, backCols,  frontCols, occupied, boardWidth);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AssignRow(
        List<PieceSpawnData> group,
        int preferredRow,
        int fallbackRow,
        List<int> preferredCols,
        List<int> fallbackCols,
        HashSet<Vector2Int> occupied,
        int boardWidth)
    {
        foreach (PieceSpawnData piece in group)
        {
            Vector2Int pos;

            if (TryTakeFreeColumn(preferredCols, preferredRow, occupied, boardWidth, out pos) ||
                TryTakeFreeColumn(fallbackCols,  fallbackRow,  occupied, boardWidth, out pos))
            {
                piece.startPosition = pos;
                occupied.Add(pos);
            }
            else
            {
                Debug.LogWarning($"[EnemySquadPlacer] No free slot for '{piece.piecePrefab?.name}' — " +
                                 "squad has more pieces than the board's top two rows can fit.");
            }
        }
    }

    /// <summary>
    /// Tries to claim a free column from <paramref name="cols"/> on <paramref name="row"/>.
    /// Skips columns that are already occupied. Returns false if no column is available.
    /// </summary>
    private static bool TryTakeFreeColumn(
        List<int>            cols,
        int                  row,
        HashSet<Vector2Int>  occupied,
        int                  boardWidth,
        out Vector2Int       result)
    {
        for (int i = 0; i < cols.Count; i++)
        {
            var candidate = new Vector2Int(cols[i], row);
            if (!occupied.Contains(candidate))
            {
                cols.RemoveAt(i);
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>Returns all column indices 0..width-1 in a randomised order.</summary>
    private static List<int> ShuffledColumns(int width)
    {
        List<int> cols = new List<int>(width);
        for (int x = 0; x < width; x++)
            cols.Add(x);

        for (int i = cols.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cols[i], cols[j]) = (cols[j], cols[i]);
        }

        return cols;
    }
}
