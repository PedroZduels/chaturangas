using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for all boss-specific fight effects.
/// Subclass this and assign an instance to SquadDefinition.bossEffect.
/// GameController calls Activate / Deactivate and the turn hooks around the boss fight.
/// </summary>
public abstract class BossEffect : ScriptableObject
{
    /// <summary>Called once after the boss fight loads and all pieces are spawned.</summary>
    public abstract void Activate(GameController gameController);

    /// <summary>Called each time it becomes the player's turn during the boss fight.</summary>
    public abstract void OnPlayerTurnStarted(GameController gameController);

    /// <summary>
    /// Called at the very start of the AI's turn, before the AI selects its own move.
    /// Override to perform extra actions (e.g. move a hacked player piece first).
    /// Returns an IEnumerator that GameController will yield on, or null for a no-op.
    /// Default implementation returns null.
    /// </summary>
    public virtual IEnumerator OnEnemyTurnStarted(GameController gameController) => null;

    /// <summary>Called when the boss fight ends (win or loss) to restore any modified state.</summary>
    public abstract void Deactivate(GameController gameController);

    /// <summary>
    /// Applies this effect's tint to a single piece spawned mid-fight (morph, promotion, etc.).
    /// Override in subclasses that use piece tinting. Default is a no-op.
    /// </summary>
    public virtual void ApplyTintToPiece(Piece piece) { }

    /// <summary>
    /// Delegates a coroutine to the GameController MonoBehaviour so ScriptableObject-based
    /// boss effects can run time-based animations without needing their own MonoBehaviour.
    /// </summary>
    protected Coroutine StartCoroutine(GameController gameController, IEnumerator routine)
    {
        return gameController.StartEffectCoroutine(routine);
    }
}
