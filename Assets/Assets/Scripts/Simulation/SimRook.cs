using System.Collections.Generic;
using UnityEngine;

public sealed class SimRook : SimPiece
{
    public override AIPieceType Type => AIPieceType.Rook;

    public SimRook(bool isPlayer, Vector2Int pos,
                   int armorCount = 0, int turnsLeft = -1,
                   bool isUpgraded = false, bool isGlitched = false)
        : base(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched) { }

    public override List<SimAction> GetActions(SimBoard board) =>
        SlidingMoves(board, CardinalDirs);

    public override ISimPiece MovedTo(Vector2Int newPos) =>
        new SimRook(IsPlayer, newPos, ArmorCount, TurnsLeft, IsUpgraded, IsGlitched);

    protected override ISimPiece Clone(bool isPlayer, Vector2Int pos,
                                       int armorCount, int turnsLeft,
                                       bool isUpgraded, bool isGlitched) =>
        new SimRook(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched);
}
