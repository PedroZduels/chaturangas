using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight hover handler attached to each floppy slot by <see cref="FloppyHUD"/>.
/// Implements <see cref="IPointerEnterHandler"/> and <see cref="IPointerExitHandler"/>
/// directly so that hover events fire even when the sibling <see cref="UnityEngine.UI.Button"/>
/// is non-interactable (non-interactable Buttons suppress EventTrigger events).
/// </summary>
public class FloppySlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private int              _slotIndex;
    private Action<int>      _onEnter;
    private Action<int>      _onExit;

    /// <summary>Called once by FloppyHUD after AddComponent.</summary>
    public void Initialize(int slotIndex, Action<int> onEnter, Action<int> onExit)
    {
        _slotIndex = slotIndex;
        _onEnter   = onEnter;
        _onExit    = onExit;
    }

    public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke(_slotIndex);
    public void OnPointerExit(PointerEventData eventData)  => _onExit?.Invoke(_slotIndex);
}
