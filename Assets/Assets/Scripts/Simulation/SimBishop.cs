using System.Collections.Generic;
using UnityEngine;

public sealed class SimBishop : SimPiece
{
    public override AIPieceType Type => AIPieceType.Bishop;

    public SimBishop(bool isPlayer, Vector2Int pos,
                     int armorCount = 0, int turnsLeft = -1,
                     bool isUpgraded = false, bool isGlitched = false)
        : base(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched) { }

    /// <summary>
    /// Non-upgraded : standard diagonal slides.
    /// Upgraded     : diagonal slides + one-step cardinals (move or capture).
    /// </summary>
    public override List<SimAction> GetActions(SimBoard board)
    {
        var actions = new List<SimAction>();
        actions.AddRange(SlidingMoves(board, DiagonalDirs));

        if (!IsUpgraded) return actions;

        // Upgraded bishop: one-step cardinal moves/captures.
        foreach (var cardDir in CardinalDirs)
        {
            Vector2Int target = Pos + cardDir;
            if (!board.InBounds(target)) continue;

            ISimPiece occ = board.Get(target);
            if (occ == null)
                actions.Add(new MoveAction(Pos, target));
            else if (occ.IsPlayer != IsPlayer)
                actions.Add(new CaptureAction(Pos, target));
        }

        return actions;
    }

    public override ISimPiece MovedTo(Vector2Int newPos) =>
        new SimBishop(IsPlayer, newPos, ArmorCount, TurnsLeft, IsUpgraded, IsGlitched);

    protected override ISimPiece Clone(bool isPlayer, Vector2Int pos,
                                       int armorCount, int turnsLeft,
                                       bool isUpgraded, bool isGlitched) =>
        new SimBishop(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched);
}
