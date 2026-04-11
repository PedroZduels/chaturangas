using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SquadDefinition", menuName = "Chaturanga/Squad Definition")]
public class SquadDefinition : ScriptableObject
{
    public string squadName;

    [TextArea(2, 4)]
    public string description;

    public List<PieceSpawnData> pieces;

    [Tooltip("Optional boss effect activated when this squad is the boss fight squad. Leave empty for regular fights.")]
    public BossEffect bossEffect;

    /// <summary>
    /// Returns a runtime-only deep copy of this squad.
    /// Use this whenever the game needs a mutable working copy so the original
    /// ScriptableObject asset is never modified during a run.
    /// </summary>
    public SquadDefinition Clone()
    {
        SquadDefinition copy = CreateInstance<SquadDefinition>();
        copy.name      = squadName;
        copy.squadName = squadName;
        copy.description = description;
        copy.pieces    = new List<PieceSpawnData>(pieces.Count);

        foreach (PieceSpawnData src in pieces)
        {
            copy.pieces.Add(new PieceSpawnData
            {
                piecePrefab   = src.piecePrefab,
                startPosition = src.startPosition,
                isUpgraded    = src.isUpgraded,
                startingArmor = src.startingArmor
            });
        }

        return copy;
    }
}

[System.Serializable]
public class PieceSpawnData
{
    public GameObject piecePrefab;
    public Vector2Int startPosition;
    public bool isUpgraded;
    public int startingArmor;
}
