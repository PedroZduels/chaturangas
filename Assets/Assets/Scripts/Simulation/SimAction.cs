using System.Collections.Generic;
using UnityEngine;

/// <summary>Base class for all simulated board actions.</summary>
public abstract class SimAction { }

/// <summary>Move to an empty square.</summary>
public sealed class MoveAction : SimAction
{
    public readonly Vector2Int From;
    public readonly Vector2Int To;
    public MoveAction(Vector2Int from, Vector2Int to) { From = from; To = to; }
}

/// <summary>Move onto an enemy square (capture).</summary>
public sealed class CaptureAction : SimAction
{
    public readonly Vector2Int From;
    public readonly Vector2Int To;
    public CaptureAction(Vector2Int from, Vector2Int to) { From = from; To = to; }
}

/// <summary>
/// Upgraded-queen compound strike: capture then retreat along the same ray.
/// </summary>
public sealed class CompoundAction : SimAction
{
    public readonly Vector2Int From;
    public readonly Vector2Int CaptureAt;
    public readonly Vector2Int RetreatTo;

    public CompoundAction(Vector2Int from, Vector2Int captureAt, Vector2Int retreatTo)
    {
        From      = from;
        CaptureAt = captureAt;
        RetreatTo = retreatTo;
    }
}

/// <summary>Area-of-effect action targeting multiple squares simultaneously.</summary>
public sealed class AOEAction : SimAction
{
    public readonly Vector2Int       From;
    public readonly List<Vector2Int> Targets;

    public AOEAction(Vector2Int from, List<Vector2Int> targets)
    {
        From    = from;
        Targets = targets;
    }
}

/// <summary>Shifts an entire row or column one step in a direction (future mechanic).</summary>
public sealed class BoardShiftAction : SimAction
{
    public readonly bool IsRow;
    public readonly int  Index;
    public readonly int  Direction; // +1 or -1

    public BoardShiftAction(bool isRow, int index, int direction)
    {
        IsRow     = isRow;
        Index     = index;
        Direction = direction;
    }
}
