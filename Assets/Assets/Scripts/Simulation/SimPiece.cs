using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable base for all simulated pieces. Subclasses implement Type, GetActions, MovedTo, and Clone.
/// </summary>
public abstract class SimPiece : ISimPiece
{
    public bool IsPlayer  { get; }
    public Vector2Int Pos { get; }
    public int ArmorCount { get; }
    public bool HasArmor  => ArmorCount > 0;
    public int TurnsLeft  { get; }
    public bool IsUpgraded { get; }
    public bool IsGlitched { get; }
    public abstract AIPieceType Type { get; }

    protected SimPiece(bool isPlayer, Vector2Int pos,
                        int armorCount = 0, int turnsLeft = -1,
                        bool isUpgraded = false, bool isGlitched = false)
    {
        IsPlayer   = isPlayer;
        Pos        = pos;
        ArmorCount = armorCount;
        TurnsLeft  = turnsLeft;
        IsUpgraded = isUpgraded;
        IsGlitched = isGlitched;
    }

    public abstract List<SimAction> GetActions(SimBoard board);
    public abstract ISimPiece MovedTo(Vector2Int newPos);

    protected abstract ISimPiece Clone(bool isPlayer, Vector2Int pos,
                                       int armorCount, int turnsLeft,
                                       bool isUpgraded, bool isGlitched);

    public ISimPiece WithoutArmor()
    {
        int newArmor     = Mathf.Max(0, ArmorCount - 1);
        int newTurnsLeft = (newArmor == 0) ? 0 : TurnsLeft;
        return Clone(IsPlayer, Pos, newArmor, newTurnsLeft, IsUpgraded, IsGlitched);
    }

    /// <summary>Returns a copy with TurnsLeft decremented by one (minimum -1).</summary>
    public ISimPiece TickStun() =>
        Clone(IsPlayer, Pos, ArmorCount, Mathf.Max(-1, TurnsLeft - 1), IsUpgraded, IsGlitched);

    /// <summary>Returns a copy with IsPlayer set to <paramref name="isPlayer"/>.</summary>
    public ISimPiece WithSide(bool isPlayer) =>
        Clone(isPlayer, Pos, ArmorCount, TurnsLeft, IsUpgraded, IsGlitched);

    // ── Direction tables ─────────────────────────────────────────────────────

    protected static readonly Vector2Int[] AllDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1)
    };

    protected static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    protected static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int( 1,  1), new Vector2Int( 1, -1),
        new Vector2Int(-1,  1), new Vector2Int(-1, -1)
    };

    protected static readonly Vector2Int[] BaseLeaps =
    {
        new Vector2Int( 1,  2), new Vector2Int( 2,  1),
        new Vector2Int(-1,  2), new Vector2Int(-2,  1),
        new Vector2Int( 1, -2), new Vector2Int( 2, -1),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1)
    };

    protected static readonly Vector2Int[] ExtendedLeaps =
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

    // ── Move generators ───────────────────────────────────────────────────────

    /// <summary>
    /// Sliding moves along the given directions.
    /// Upgraded rooks phase through enemy pawns — those squares are treated as
    /// empty for movement purposes. Only the actual landing square is a capture.
    /// </summary>
    protected List<SimAction> SlidingMoves(SimBoard board, Vector2Int[] dirs)
    {
        var actions = new List<SimAction>();
        bool upgradedRook = Type == AIPieceType.Rook && IsUpgraded;

        foreach (var dir in dirs)
        {
            Vector2Int cur = Pos;
            while (true)
            {
                cur += dir;
                if (!board.InBounds(cur)) break;

                ISimPiece occupant = board.Get(cur);
                if (occupant == null)
                {
                    actions.Add(new MoveAction(Pos, cur));
                }
                else
                {
                    if (occupant.IsPlayer != IsPlayer)
                    {
                        actions.Add(new CaptureAction(Pos, cur));
                        // Upgraded rook phases through enemy pawns — treated as
                        // passable, not a real capture. Keep sliding.
                        if (upgradedRook && occupant.Type == AIPieceType.Pawn)
                            continue;
                    }
                    break;
                }
            }
        }
        return actions;
    }

    /// <summary>Step (leap) moves — knights, kings.</summary>
    protected List<SimAction> StepMoves(SimBoard board, Vector2Int[] leaps)
    {
        var actions = new List<SimAction>();
        foreach (var leap in leaps)
        {
            Vector2Int target = Pos + leap;
            if (!board.InBounds(target)) continue;

            ISimPiece occupant = board.Get(target);
            if (occupant == null)
                actions.Add(new MoveAction(Pos, target));
            else if (occupant.IsPlayer != IsPlayer)
                actions.Add(new CaptureAction(Pos, target));
        }
        return actions;
    }

    /// <summary>Standard pawn moves — forward step + diagonal captures.</summary>
    protected List<SimAction> PawnMoves(SimBoard board)
    {
        var actions = new List<SimAction>();
        int dir = IsPlayer ? 1 : -1;

        Vector2Int fwd = Pos + new Vector2Int(0, dir);
        if (board.InBounds(fwd) && board.Get(fwd) == null)
            actions.Add(new MoveAction(Pos, fwd));

        foreach (var capOff in new[] { new Vector2Int(1, dir), new Vector2Int(-1, dir) })
        {
            Vector2Int cap = Pos + capOff;
            if (!board.InBounds(cap)) continue;
            ISimPiece occ = board.Get(cap);
            if (occ != null && occ.IsPlayer != IsPlayer)
                actions.Add(new CaptureAction(Pos, cap));
        }
        return actions;
    }
}
