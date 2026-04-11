using UnityEngine;

/// <summary>Converts grid positions and moves to human-readable chess notation.</summary>
public static class ChessNotation
{
    /// <summary>Converts a Vector2Int board position to algebraic notation (e.g. A1, D6).</summary>
    public static string ToNotation(Vector2Int pos)
    {
        char col = (char)('A' + pos.x);
        int  row = pos.y + 1;
        return $"{col}{row}";
    }

    /// <summary>Formats a move log string like "Queen A1 → D4" or "Queen A1 × D4 (captures Pawn)".</summary>
    public static string FormatMove(string pieceName, Vector2Int from, Vector2Int to, string capturedName)
    {
        string arrow = capturedName != null ? "×" : "→";
        string suffix = capturedName != null ? $" (captures {capturedName})" : "";
        return $"{pieceName} {ToNotation(from)} {arrow} {ToNotation(to)}{suffix}";
    }
}
