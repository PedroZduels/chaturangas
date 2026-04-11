using System.Collections.Generic;
using UnityEngine;

public sealed class SimKnight : SimPiece
{
    public override AIPieceType Type => AIPieceType.Knight;

    public SimKnight(bool isPlayer, Vector2Int pos,
                     int armorCount = 0, int turnsLeft = -1,
                     bool isUpgraded = false, bool isGlitched = false)
        : base(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched) { }

    /// <summary>Upgraded knights use extended L-shape leaps (3,1) and (1,3).</summary>
    public override List<SimAction> GetActions(SimBoard board) =>
        StepMoves(board, IsUpgraded ? ExtendedLeaps : BaseLeaps);

    public override ISimPiece MovedTo(Vector2Int newPos) =>
        new SimKnight(IsPlayer, newPos, ArmorCount, TurnsLeft, IsUpgraded, IsGlitched);

    protected override ISimPiece Clone(bool isPlayer, Vector2Int pos,
                                       int armorCount, int turnsLeft,
                                       bool isUpgraded, bool isGlitched) =>
        new SimKnight(isPlayer, pos, armorCount, turnsLeft, isUpgraded, isGlitched);
}
