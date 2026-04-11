using UnityEngine;

/// <summary>
/// ScriptableObject that describes one floppy disc item.
/// Create instances via Assets → Create → Chaturanga → Floppy Definition.
/// Each instance is a unique item type; the effect is identified by FloppyEffectType.
/// </summary>
[CreateAssetMenu(menuName = "Chaturanga/Floppy Definition", fileName = "NewFloppy")]
public class FloppyDefinition : ScriptableObject
{
    [Tooltip("Display name shown in the HUD tooltip and market card.")]
    public string displayName = "Floppy";

    [Tooltip("Short description shown on the card.")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("Which effect this floppy executes when used.")]
    public FloppyEffectType effectType;

    [Tooltip("Cost in bits when bought from the market.")]
    public int marketCost = 3;

    [Tooltip("Override icon. Leave null to use the global floppy sprite.")]
    public Sprite icon;
}
