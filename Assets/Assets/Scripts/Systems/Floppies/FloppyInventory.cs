using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime container for the player's floppy discs.
/// Holds up to 3 slots. Fires OnChanged whenever the inventory mutates.
/// Not a ScriptableObject — lives on a MonoBehaviour so it resets between runs naturally.
/// </summary>
public class FloppyInventory : MonoBehaviour
{
    public const int SlotCount = 3;

    /// <summary>Current slots. Null entries are empty slots.</summary>
    private readonly FloppyDefinition[] _slots = new FloppyDefinition[SlotCount];

    /// <summary>Fired after any Add / Remove operation. Subscribers should re-read Slots.</summary>
    public event Action OnChanged;

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>Read-only view of the 3 slots.</summary>
    public IReadOnlyList<FloppyDefinition> Slots => _slots;

    /// <summary>True when at least one slot is empty.</summary>
    public bool HasRoom => IndexOfEmpty() != -1;

    /// <summary>Number of floppies currently held.</summary>
    public int Count
    {
        get
        {
            int n = 0;
            foreach (var s in _slots) if (s != null) n++;
            return n;
        }
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a floppy to the first empty slot.
    /// Returns true on success, false when all slots are occupied.
    /// </summary>
    public bool TryAdd(FloppyDefinition floppy)
    {
        int idx = IndexOfEmpty();
        if (idx == -1) return false;

        _slots[idx] = floppy;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Removes the floppy at slot index and returns it. Null if slot was empty.</summary>
    public FloppyDefinition RemoveAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        FloppyDefinition removed = _slots[slotIndex];
        _slots[slotIndex] = null;
        OnChanged?.Invoke();
        return removed;
    }

    /// <summary>Returns the floppy at slot index without removing it.</summary>
    public FloppyDefinition PeekAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;
        return _slots[slotIndex];
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private int IndexOfEmpty()
    {
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i] == null) return i;
        return -1;
    }
}
