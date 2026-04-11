using UnityEngine;

/// <summary>
/// Contract for every floppy effect implementation.
/// Each effect either acts immediately on a target piece or requires the player
/// to click a piece first (RequiresTarget == true).
/// </summary>
public interface IFloppyEffect
{
    /// <summary>
    /// When true the HUD enters piece-selection mode after activating the floppy;
    /// Execute() is called once the player clicks a valid piece.
    /// When false Execute() fires immediately with a null target.
    /// </summary>
    bool RequiresTarget { get; }

    /// <summary>
    /// Returns true when <paramref name="piece"/> is a valid target for this effect.
    /// Only called when RequiresTarget is true.
    /// </summary>
    bool IsValidTarget(Piece piece, GameController gc);

    /// <summary>
    /// Applies the floppy effect. Called immediately (RequiresTarget false) or
    /// after the player selects a valid target piece.
    /// </summary>
    void Execute(Piece target, GameController gc);
}
