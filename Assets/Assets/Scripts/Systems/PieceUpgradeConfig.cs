using UnityEngine;

/// <summary>ScriptableObject that describes a piece's upgrade — assign on each piece prefab.</summary>
[CreateAssetMenu(fileName = "PieceUpgradeConfig", menuName = "Chaturanga/Piece Upgrade Config")]
public class PieceUpgradeConfig : ScriptableObject
{
    public string upgradeName;

    [TextArea(2, 4)]
    public string upgradeDescription;

    public int upgradeCost = 10;
}
