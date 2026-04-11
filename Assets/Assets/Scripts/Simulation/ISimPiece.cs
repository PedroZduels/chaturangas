using System.Collections.Generic;
using UnityEngine;

/// <summary>Immutable simulation snapshot of a chess piece used by the AI search.</summary>
public interface ISimPiece
{
    bool IsPlayer { get; }
    Vector2Int Pos { get; }
    int ArmorCount { get; }
    bool HasArmor { get; }
    int TurnsLeft { get; }
    bool IsUpgraded { get; }
    bool IsGlitched { get; }
    AIPieceType Type { get; }

    List<SimAction> GetActions(SimBoard board);
    ISimPiece MovedTo(Vector2Int newPos);
    ISimPiece WithoutArmor();
    ISimPiece TickStun();

    /// <summary>Returns a copy with the IsPlayer flag flipped to the given value.</summary>
    ISimPiece WithSide(bool isPlayer);
}
