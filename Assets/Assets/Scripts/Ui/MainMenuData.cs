using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that holds all editable content for the main menu.
/// Create an instance via Assets > Create > Chaturanga > Main Menu Data.
/// </summary>
[CreateAssetMenu(fileName = "MainMenuData", menuName = "Chaturanga/Main Menu Data")]
public class MainMenuData : ScriptableObject
{
    // ── Main panel ─────────────────────────────────────────────────────────────

    [Header("Main Panel")]
    public string gameTitle      = "CHATURANGA";
    public string startRunLabel  = "Start Run";
    public string infoLabel      = "Info";
    public string optionsLabel   = "Options";
    public string exitLabel      = "Exit";

    // ── Info panel ─────────────────────────────────────────────────────────────

    [Header("Info Panel")]
    public string infoTitle = "How to Play";

    [Header("Piece Sprite Sheet")]
    [Tooltip("Deprecated — sprites are now set directly on each PieceInfo entry.")]
    [HideInInspector]
    public Sprite[] pieceSheet;

    [Header("Overview")]
    [TextArea(2, 6)]
    public string overviewText;

    [Header("Run Structure")]
    [TextArea(4, 10)]
    public string runStructureText;

    [Header("Pieces")]
    [Tooltip("One entry per chess piece. Each card shows the icon, move rules, and upgrade ability.")]
    public List<PieceInfo> pieces = new List<PieceInfo>();

    [Header("Armor")]
    [TextArea(3, 6)]
    public string armorText;

    [Header("Sudden Death")]
    [TextArea(3, 8)]
    public string suddenDeathText;

    [Header("Floppy Discs")]
    [Tooltip("Introductory paragraph shown above the floppy list.")]
    [TextArea(3, 8)]
    public string floppyIntroText;

    [Tooltip("One entry per floppy type. Shown in the FLOPPIES section of the info panel.")]
    public List<FloppyInfo> floppies = new List<FloppyInfo>();

    [Header("Discs")]
    [Tooltip("Introductory paragraph shown above the disc list.")]
    [TextArea(3, 8)]
    public string discIntroText;

    [Tooltip("One entry per disc. Discs are permanent passive effects that last the entire run.")]
    public List<DiscInfo> discs = new List<DiscInfo>();

    // ── Back buttons ───────────────────────────────────────────────────────────

    [Header("Back Buttons")]
    public string infoBackLabel    = "Back";
    public string optionsBackLabel = "Back";

    /// <summary>Returns the sprite for <paramref name="piece"/>.</summary>
    public Sprite GetSprite(PieceInfo piece) => piece.sprite;
}

/// <summary>Data for a single chess piece card in the Info panel.</summary>
[System.Serializable]
public class PieceInfo
{
    [Tooltip("Display name shown on the card.")]
    public string pieceName;

    [Tooltip("Sprite pulled directly from the piece's prefab SpriteRenderer.")]
    public Sprite sprite;

    [Tooltip("Accent / border color for this piece's card.")]
    public Color accentColor = Color.white;

    [Tooltip("Movement rules shown on the card.")]
    [TextArea(1, 3)]
    public string moveText;

    [Tooltip("Upgrade ability shown below the move rules.")]
    [TextArea(1, 3)]
    public string upgradeText;
}

/// <summary>Data for a single floppy card in the Info panel.</summary>
[System.Serializable]
public class FloppyInfo
{
    [Tooltip("Display name matching the FloppyDefinition.")]
    public string floppyName;

    [Tooltip("Market cost in bits. Set to 0 for items that are never sold (e.g. Dismantle).")]
    public int cost;

    [Tooltip("Optional override icon.")]
    public Sprite icon = null;

    [Tooltip("Full description of the effect shown on the card.")]
    [TextArea(2, 5)]
    public string description;
}

/// <summary>Data for a single disc card in the Info panel. Discs are permanent run-wide passives.</summary>
[System.Serializable]
public class DiscInfo
{
    [Tooltip("Display name of the disc.")]
    public string discName;

    [Tooltip("Optional icon for the disc.")]
    public Sprite icon;

    [Tooltip("Description of the permanent passive effect.")]
    [TextArea(2, 5)]
    public string description;
}

