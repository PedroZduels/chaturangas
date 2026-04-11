/// <summary>
/// Identifies which runtime behaviour a FloppyDefinition executes.
/// Add a new enum value here, then handle it in FloppyEffectFactory.
/// </summary>
public enum FloppyEffectType
{
    Armor,      // gives 1 armor to a selected player piece
    Retreat,    // moves selected player piece 1 square toward player's back rank
    Stun,       // stuns a selected enemy piece for one turn
    Upgrade,    // upgrades a selected player piece for this combat only
    DoubleJump, // selected player piece must move twice this turn
    Hack,       // forces a selected enemy piece to be the ONLY piece that moves next AI turn
    Ethereal,   // selected player piece can pass through friendly pieces for one turn
    Morph,      // selected player piece becomes a Queen for one turn
    Dismantle,  // selected player piece is killed; player earns 10 bits
    CtrlZ,      // no target — confirm-gated; reverts the board 2 half-turns
    Wall,       // select up to 3 connected empty tiles; AI cannot place/move to them next turn
}
