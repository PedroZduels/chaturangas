using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pool of enemy SquadDefinitions for one difficulty tier.
/// The GameController picks a random entry from the pool each fight,
/// avoiding repeating the previous squad.
/// </summary>
[System.Serializable]
public class EnemyTier
{
    [Tooltip("Display name for this tier (e.g. 'Tier 1 — Scouts').")]
    public string tierName;

    [Tooltip("This tier becomes active once FightCount reaches this value. " +
             "Tier 0 should be 0. Example: set to 8 for elites that unlock after the red boss.")]
    public int unlockAfterFight = 0;

    [Tooltip("When true, boss music playlist plays instead of the regular one for fights in this tier.")]
    public bool isBossTier;

    [Tooltip("All enemy squads available in this tier. At least one entry required.")]
    public List<SquadDefinition> pool;
}
