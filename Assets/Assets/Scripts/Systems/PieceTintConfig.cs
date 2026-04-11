using UnityEngine;

/// <summary>
/// Global colour palette for all pieces.
/// Create one instance via Assets → Create → Chaturanga → Piece Tint Config
/// and assign it on the GameController. All pieces and UI read from this asset.
/// </summary>
[CreateAssetMenu(fileName = "PieceTintConfig", menuName = "Chaturanga/Piece Tint Config")]
public class PieceTintConfig : ScriptableObject
{
    [Header("Player")]
    public Color playerNormal   = new Color(0f, 1.00f, 0.95f, 1f);   // cyan
    public Color playerUpgraded = new Color(1.00f, 0.84f, 0.10f, 1f); // gold

    [Header("Enemy")]
    public Color enemyNormal    = new Color(0.90f, 0.20f, 0.20f, 1f);
    public Color enemyUpgraded  = new Color(0.65f, 0.10f, 0.90f, 1f);

    /// <summary>Returns the correct tint for a given piece state.</summary>
    public Color Resolve(bool isPlayer, bool isUpgraded)
    {
        if (isPlayer)
            return isUpgraded ? playerUpgraded : playerNormal;
        return isUpgraded ? enemyUpgraded : enemyNormal;
    }
}
