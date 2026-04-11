using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implement this interface on any MonoBehaviour that owns a HUD element
/// which should be recolored during a boss fight.
/// CommunistGrandmasterEffect (and future boss effects) call these methods
/// to batch-apply and restore accent tints without hard-coding references.
/// </summary>
public interface IBossHudElement
{
    /// <summary>
    /// Store the current accent colors in <paramref name="originalColors"/> (keyed by
    /// GetInstanceID()) and apply <paramref name="tint"/> in their place.
    /// </summary>
    void ApplyBossTint(Color tint, Dictionary<int, Color> originalColors);

    /// <summary>Restore the colors previously saved in <paramref name="originalColors"/>.</summary>
    void RestoreTint(Dictionary<int, Color> originalColors);
}
