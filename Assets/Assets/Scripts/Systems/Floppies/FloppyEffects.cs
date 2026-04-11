using UnityEngine;

/// <summary>
/// Concrete effect: gives 1 armor stack to a player piece.
/// If the piece already has armor the use is wasted (flavour — bad play!).
/// </summary>
public class ArmorFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) => piece != null && piece.isPlayer;

    public void Execute(Piece target, GameController gc)
    {
        if (target.HasArmor)
        {
            Debug.Log($"[Floppy:Armor] {target.GetType().Name} already has armor — wasted!");
            return;
        }
        target.GainArmor(1);
        Debug.Log($"[Floppy:Armor] Granted 1 armor to {target.GetType().Name}.");
    }
}

/// <summary>
/// Concrete effect: moves the selected player piece 1 square toward the player's back rank (y = 0).
/// Does nothing if the square behind is occupied or out of bounds.
/// </summary>
public class RetreatFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) => piece != null && piece.isPlayer;

    public void Execute(Piece target, GameController gc)
    {
        Vector2Int dest = target.position + Vector2Int.down;   // toward player's back rank
        if (!gc.board.IsInsideBoard(dest))
        {
            Debug.Log("[Floppy:Retreat] Already at back rank — cannot retreat.");
            return;
        }
        if (gc.board.tiles[dest.x, dest.y].occupiedPiece != null)
        {
            Debug.Log("[Floppy:Retreat] Target square is occupied — cannot retreat.");
            return;
        }

        gc.board.tiles[target.position.x, target.position.y].occupiedPiece = null;
        gc.board.tiles[dest.x, dest.y].occupiedPiece = target;
        target.position = dest;
        target.PlaceAt(gc.board.GridToWorld(dest));

        Debug.Log($"[Floppy:Retreat] {target.GetType().Name} retreated to {dest}.");
    }
}

/// <summary>
/// Concrete effect: stuns the selected enemy piece for one full turn.
/// </summary>
public class StunFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) => piece != null && !piece.isPlayer;

    public void Execute(Piece target, GameController gc)
    {
        target.isStunned = true;
        target.ApplyStunTilt();
        AudioManager.PlayStun();
        Debug.Log($"[Floppy:Stun] {target.GetType().Name} at {target.position} is stunned for 1 turn.");
    }
}

/// <summary>
/// Concrete effect: upgrades the selected player piece for this combat only.
/// The upgrade is NOT written to the squad definition and does not persist.
/// </summary>
public class UpgradeFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) =>
        piece != null && piece.isPlayer && !piece.isUpgraded;

    public void Execute(Piece target, GameController gc)
    {
        target.SetUpgraded();
        AudioManager.PlayUpgrade();
        Debug.Log($"[Floppy:Upgrade] {target.GetType().Name} temporarily upgraded for this fight.");
    }
}

/// <summary>
/// Concrete effect: marks the selected player piece to move twice this turn.
/// GameController checks Piece.pendingDoubleJump when the piece completes its first move.
/// </summary>
public class DoubleJumpFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) =>
        piece != null && piece.isPlayer && !piece.isStunned;

    public void Execute(Piece target, GameController gc)
    {
        target.pendingDoubleJump = true;
        Debug.Log($"[Floppy:DoubleJump] {target.GetType().Name} will move twice this turn.");
    }
}

/// <summary>
/// Hack: the selected enemy piece is marked as hacked.
/// Next AI turn GameController forces ONLY that piece to move.
/// If it cannot move (blocked or stunned) the AI naturally loses its turn.
/// </summary>
public class HackFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) =>
        piece != null && !piece.isPlayer && !piece.isHacked;

    public void Execute(Piece target, GameController gc)
    {
        target.isHacked = true;
        target.StartHackFlicker();   // visual feedback — piece flickers to indicate hack
        AudioManager.PlayUIClick();
        Debug.Log($"[Floppy:Hack] {target.GetType().Name} at {target.position} is hacked.");
    }
}

/// <summary>
/// Ethereal: the selected player piece can move through friendly pieces for one turn.
/// The flag is cleared at the start of the next player turn in GameController.EndTurn.
/// </summary>
public class EtherealFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) =>
        piece != null && piece.isPlayer && !piece.isEthereal;

    public void Execute(Piece target, GameController gc)
    {
        target.isEthereal = true;
        AudioManager.PlayUIClick();
        Debug.Log($"[Floppy:Ethereal] {target.GetType().Name} is ethereal for one turn.");
    }
}

/// <summary>
/// Morph: the selected player piece is temporarily replaced by a Queen.
/// The original type is stored so GameController can restore it after the AI turn.
/// </summary>
public class MorphFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) =>
        piece != null && piece.isPlayer && gc.morphQueenPrefab != null;

    public void Execute(Piece target, GameController gc)
    {
        gc.MorphPiece(target);
        AudioManager.PlayUpgrade();
        Debug.Log($"[Floppy:Morph] {target.GetType().Name} morphed into a Queen for one turn.");
    }
}

/// <summary>
/// Dismantle: kills the selected player piece and awards 10 bits.
/// The piece is removed from the board immediately.
/// </summary>
public class DismantleFloppyEffect : IFloppyEffect
{
    public const int BitReward = 10;

    public bool RequiresTarget => true;

    public bool IsValidTarget(Piece piece, GameController gc) => piece != null && piece.isPlayer;

    public void Execute(Piece target, GameController gc)
    {
        gc.DismantlePiece(target);
        gc.AddBits(BitReward);
        AudioManager.PlayUIClick();
        Debug.Log($"[Floppy:Dismantle] {target.GetType().Name} dismantled — +{BitReward} bits.");
    }
}

/// <summary>
/// Ctrl+Z: no piece target. Reverts the board to the state two half-turns ago
/// (AI's last turn + player's previous turn). Requires an explicit confirm button.
/// GameController drives the confirm flow; this effect fires only after confirmation.
/// </summary>
public class CtrlZFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => false;

    public bool IsValidTarget(Piece piece, GameController gc) => true;

    public void Execute(Piece target, GameController gc)
    {
        gc.RevertBoard();
        AudioManager.PlayUIClick();
        Debug.Log("[Floppy:CtrlZ] Board reverted two half-turns.");
    }
}

/// <summary>
/// Wall: multi-tile targeting — handled entirely by GameController's WallFloppyMode.
/// This effect object is a sentinel only; GameController calls gc.CommitWall() directly.
/// </summary>
public class WallFloppyEffect : IFloppyEffect
{
    public bool RequiresTarget => false;   // GameController starts its own multi-tile mode

    public bool IsValidTarget(Piece piece, GameController gc) => true;

    public void Execute(Piece target, GameController gc)
    {
        // Handled by GameController.ActivateWallMode — this path is unused.
    }
}
