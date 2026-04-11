using System.Collections.Generic;
using UnityEngine;

public sealed class SimKing : SimPiece
{
    public override AIPieceType Type => AIPieceType.King;

    public SimKing(bool isPlayer, Vector2Int pos,
                   int armorCount = 0, int turnsLeft = -1,
                   bool isUpgraded = false, bool isGlitched = false)
        : base(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched) { }

    public override List<SimAction> GetActions(SimBoard board) =>
        StepMoves(board, AllDirs);

    public override ISimPiece MovedTo(Vector2Int newPos) =>
        new SimKing(IsPlayer, newPos, ArmorCount, TurnsLeft, IsUpgraded, IsGlitched);

    protected override ISimPiece Clone(bool isPlayer, Vector2Int pos,
                                       int armorCount, int turnsLeft,
                                       bool isUpgraded, bool isGlitched) =>
        new SimKing(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched);
}
