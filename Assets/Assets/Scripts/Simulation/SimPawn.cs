using System.Collections.Generic;
using UnityEngine;

public sealed class SimPawn : SimPiece
{
    public override AIPieceType Type => AIPieceType.Pawn;

    public SimPawn(bool isPlayer, Vector2Int pos,
                   int armorCount = 0, int turnsLeft = -1,
                   bool isUpgraded = false, bool isGlitched = false)
        : base(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched) { }

    /// <summary>
    /// Upgraded pawns can double-step from any rank (both squares ahead must be empty).
    /// </summary>
    public override List<SimAction> GetActions(SimBoard board)
    {
        List<SimAction> actions = PawnMoves(board);

        if (!IsUpgraded) return actions;

        int dir   = IsPlayer ? 1 : -1;
        Vector2Int fwd  = Pos + new Vector2Int(0, dir);
        Vector2Int fwd2 = Pos + new Vector2Int(0, dir * 2);

        if (board.InBounds(fwd)  && board.Get(fwd)  == null &&
            board.InBounds(fwd2) && board.Get(fwd2) == null)
        {
            actions.Add(new MoveAction(Pos, fwd2));
        }

        return actions;
    }

    public override ISimPiece MovedTo(Vector2Int newPos) =>
        new SimPawn(IsPlayer, newPos, ArmorCount, TurnsLeft, IsUpgraded, IsGlitched);

    protected override ISimPiece Clone(bool isPlayer, Vector2Int pos,
                                       int armorCount, int turnsLeft,
                                       bool isUpgraded, bool isGlitched) =>
        new SimPawn(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched);
}
