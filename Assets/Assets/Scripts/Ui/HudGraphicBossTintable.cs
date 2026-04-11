using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic adapter: attach to any HUD GameObject that owns a <see cref="Graphic"/>
/// (Image, Text, TMP_Text, etc.) to make it participate in the boss tint system
/// without modifying the owning script.
///
/// Useful for buttons and panels that have no dedicated MonoBehaviour of their own
/// (ViewToggleButton, AutoWinButton, BoardNudge arrows, etc.).
/// </summary>
[RequireComponent(typeof(Graphic))]
public class HudGraphicBossTintable : MonoBehaviour, IBossHudElement
{
    private Graphic _graphic;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
    }

    /// <summary>Saves the current Graphic color and applies the boss tint.</summary>
    public void ApplyBossTint(Color tint, Dictionary<int, Color> originalColors)
    {
        if (_graphic == null) return;

        int key = _graphic.GetInstanceID();
        if (!originalColors.ContainsKey(key))
            originalColors[key] = _graphic.color;

        _graphic.color = tint;
    }

    /// <summary>Restores the original Graphic color saved during <see cref="ApplyBossTint"/>.</summary>
    public void RestoreTint(Dictionary<int, Color> originalColors)
    {
        if (_graphic == null) return;

        int key = _graphic.GetInstanceID();
        if (originalColors.TryGetValue(key, out Color original))
            _graphic.color = original;
    }
}
