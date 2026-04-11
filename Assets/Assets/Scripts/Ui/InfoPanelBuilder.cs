using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedurally fills the InfoPanel scroll content with styled cards at runtime.
/// Attach to the ScrollView's Content GameObject. Call <see cref="Build"/> from
/// <see cref="MainMenuController"/> after ApplyMenuData.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InfoPanelBuilder : MonoBehaviour
{
    // ── Palette ────────────────────────────────────────────────────────────────

    private static readonly Color SectionHeaderBg = new Color(0.14f, 0.11f, 0.26f, 1.00f);
    private static readonly Color CardBg          = new Color(0.11f, 0.09f, 0.20f, 1.00f);
    private static readonly Color TextPrimary     = new Color(0.95f, 0.95f, 0.95f, 1.00f);
    private static readonly Color TextSecondary   = new Color(0.72f, 0.70f, 0.82f, 1.00f);
    private static readonly Color UpgradeLabelCol = new Color(0.85f, 0.75f, 1.00f, 1.00f);
    private static readonly Color ArmorAccent     = new Color(0.30f, 0.55f, 1.00f, 1.00f);
    private static readonly Color SuddenAccent    = new Color(1.00f, 0.28f, 0.28f, 1.00f);
    private static readonly Color OverviewAccent  = new Color(0.55f, 0.45f, 0.85f, 1.00f);
    private static readonly Color FloppyAccent    = new Color(0.30f, 0.85f, 0.70f, 1.00f);
    private static readonly Color RelicAccent     = new Color(1.00f, 0.75f, 0.20f, 1.00f);

    // ── Layout constants ───────────────────────────────────────────────────────

    private const int   SectionPadH     = 16;
    private const int   SectionPadV     = 12;
    private const int   CardPadH        = 14;
    private const int   CardPadV        = 12;
    private const int   IconSize        = 58;
    private const int   AccentBarWidth  = 5;
    private const float HeaderFontSize  = 16f;
    private const float NameFontSize    = 20f;
    private const float BodyFontSize    = 16f;
    private const float LabelFontSize   = 13f;

    private TMP_FontAsset _font;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Clears existing dynamic content and rebuilds all cards from <paramref name="data"/>.</summary>
    public void Build(MainMenuData data)
    {
        if (data == null) return;
        _font = TMP_Settings.defaultFontAsset;

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        BuildTextSection("OVERVIEW",      data.overviewText,    OverviewAccent);
        BuildTextSection("RUN STRUCTURE", data.runStructureText, OverviewAccent);

        BuildSectionHeader("PIECES & UPGRADES");
        if (data.pieces != null)
            foreach (PieceInfo piece in data.pieces)
                BuildPieceCard(piece, data.GetSprite(piece));

        BuildTextSection("ARMOR",        data.armorText,       ArmorAccent);
        BuildTextSection("SUDDEN DEATH", data.suddenDeathText, SuddenAccent);

        BuildSectionHeader("FLOPPIES");
        if (!string.IsNullOrWhiteSpace(data.floppyIntroText))
            BuildBodyText(data.floppyIntroText);
        if (data.floppies != null)
            foreach (FloppyInfo floppy in data.floppies)
                BuildFloppyCard(floppy);

        BuildSectionHeader("DISCS");
        if (!string.IsNullOrWhiteSpace(data.discIntroText))
            BuildBodyText(data.discIntroText);
        if (data.discs != null)
            foreach (DiscInfo disc in data.discs)
                BuildDiscCard(disc);
    }

    // ── Section builders ──────────────────────────────────────────────────────

    private void BuildSectionHeader(string title)
    {
        GameObject row = NewBox("Header_" + title, transform, SectionHeaderBg,
            new RectOffset(SectionPadH, SectionPadH, SectionPadV - 2, SectionPadV - 2));
        NewFill(row);
        TextMeshProUGUI lbl = NewText(row.transform, title, HeaderFontSize,
            TextPrimary, FontStyles.Bold, HorizontalAlignmentOptions.Left);
        lbl.characterSpacing = 2f;
        lbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private void BuildTextSection(string title, string body, Color accent)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        BuildSectionHeader(title);
        BuildBodyText(body);
    }

    /// <summary>Renders a plain body-text block with padding, no header.</summary>
    private void BuildBodyText(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        GameObject go = NewGO("BodyText", transform, Color.clear);
        VerticalLayoutGroup vl = go.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(CardPadH, CardPadH, CardPadV, CardPadV);
        vl.childControlWidth  = true;
        vl.childControlHeight = true;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        NewText(go.transform, body, BodyFontSize, TextSecondary,
            FontStyles.Normal, HorizontalAlignmentOptions.Left);
    }
    private void BuildPieceCard(PieceInfo piece, Sprite icon)
    {
        GameObject card = NewGO("Card_" + piece.pieceName, transform, CardBg);
        NewFill(card);

        // childControlWidth TRUE → LayoutElement flexibleWidth on children is honoured
        HorizontalLayoutGroup hl = card.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment         = TextAnchor.UpperLeft;
        hl.childControlWidth      = true;
        hl.childControlHeight     = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = false;
        hl.padding                = new RectOffset(0, CardPadH, CardPadV, CardPadV);
        hl.spacing                = 10;
        card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        NewFlexAccentBar(card.transform, piece.accentColor, AccentBarWidth);

        if (icon != null)
        {
            GameObject iconGO = NewGO("Icon", card.transform, Color.clear);
            Image img = iconGO.GetComponent<Image>();
            img.sprite         = icon;
            img.preserveAspect = true;
            img.color          = piece.accentColor;
            LayoutElement ile  = iconGO.AddComponent<LayoutElement>();
            ile.minWidth       = IconSize;
            ile.preferredWidth = IconSize;
            ile.flexibleWidth  = 0;
            ile.minHeight      = IconSize;
            ile.preferredHeight= IconSize;
        }

        // Text column — flexible, takes all remaining width
        GameObject col = NewVStack("TextCol", card.transform, Color.clear,
            new RectOffset(0, 0, 0, 0), 6f);
        LayoutElement cle = col.AddComponent<LayoutElement>();
        cle.flexibleWidth = 1;
        cle.minWidth      = 0;

        NewText(col.transform, piece.pieceName.ToUpper(), NameFontSize,
            piece.accentColor, FontStyles.Bold, HorizontalAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        NewText(col.transform, piece.moveText, BodyFontSize,
            TextPrimary, FontStyles.Normal, HorizontalAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Upgrade pill
        Color upgBg = new Color(
            piece.accentColor.r * 0.12f,
            piece.accentColor.g * 0.12f,
            piece.accentColor.b * 0.20f, 0.90f);
        GameObject upgRow = NewBox("Upgrade", col.transform, upgBg,
            new RectOffset(8, 8, 6, 6));
        NewFill(upgRow);

        // Horizontal row: fixed badge | flexible description
        GameObject inner = NewHStack("UpgradeInner", upgRow.transform, Color.clear,
            new RectOffset(0, 0, 0, 0), 8f);
        inner.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
        NewFill(inner);

        // "UPGRADE" badge — fixed width, never wraps
        TextMeshProUGUI upgLabel = NewText(inner.transform, "UPGRADE", LabelFontSize,
            UpgradeLabelCol, FontStyles.Bold, HorizontalAlignmentOptions.Left);
        LayoutElement ule   = upgLabel.gameObject.AddComponent<LayoutElement>();
        ule.minWidth        = 80;
        ule.preferredWidth  = 80;
        ule.flexibleWidth   = 0;

        // Description — takes all remaining width
        TextMeshProUGUI upgDesc = NewText(inner.transform, piece.upgradeText, BodyFontSize,
            TextSecondary, FontStyles.Italic, HorizontalAlignmentOptions.Left);
        upgDesc.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private void BuildFloppyCard(FloppyInfo floppy)
    {
        GameObject card = NewGO("Floppy_" + floppy.floppyName, transform, CardBg);
        NewFill(card);

        HorizontalLayoutGroup hl = card.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment         = TextAnchor.UpperLeft;
        hl.childControlWidth      = true;
        hl.childControlHeight     = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = false;
        hl.padding                = new RectOffset(0, CardPadH, CardPadV, CardPadV);
        hl.spacing                = 10;
        card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Color accent = FloppyAccent;
        NewFlexAccentBar(card.transform, accent, AccentBarWidth);

        if (floppy.icon != null)
        {
            GameObject iconGO = NewGO("Icon", card.transform, Color.clear);
            Image img = iconGO.GetComponent<Image>();
            img.sprite         = floppy.icon;
            img.preserveAspect = true;
            img.color          = accent;
            LayoutElement ile  = iconGO.AddComponent<LayoutElement>();
            ile.minWidth       = IconSize;
            ile.preferredWidth = IconSize;
            ile.flexibleWidth  = 0;
            ile.minHeight      = IconSize;
            ile.preferredHeight= IconSize;
        }

        GameObject col = NewVStack("TextCol", card.transform, Color.clear,
            new RectOffset(0, 0, 0, 0), 4f);
        col.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Name row — name + cost badge
        GameObject nameRow = NewHStack("NameRow", col.transform, Color.clear,
            new RectOffset(0, 0, 0, 0), 8f);
        nameRow.AddComponent<LayoutElement>().flexibleWidth = 1;
        nameRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;

        TextMeshProUGUI nameLbl = NewText(nameRow.transform, floppy.floppyName.ToUpper(),
            NameFontSize, accent, FontStyles.Bold, HorizontalAlignmentOptions.Left);
        nameLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        if (floppy.cost > 0)
        {
            TextMeshProUGUI costLbl = NewText(nameRow.transform, $"{floppy.cost} bits",
                LabelFontSize, RelicAccent, FontStyles.Bold, HorizontalAlignmentOptions.Right);
            costLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0;
        }

        NewText(col.transform, floppy.description, BodyFontSize,
            TextSecondary, FontStyles.Normal, HorizontalAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private void BuildDiscCard(DiscInfo disc)
    {
        GameObject card = NewGO("Disc_" + disc.discName, transform, CardBg);
        NewFill(card);

        HorizontalLayoutGroup hl = card.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment         = TextAnchor.UpperLeft;
        hl.childControlWidth      = true;
        hl.childControlHeight     = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = false;
        hl.padding                = new RectOffset(0, CardPadH, CardPadV, CardPadV);
        hl.spacing                = 10;
        card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        NewFlexAccentBar(card.transform, RelicAccent, AccentBarWidth);

        if (disc.icon != null)
        {
            GameObject iconGO = NewGO("Icon", card.transform, Color.clear);
            Image img = iconGO.GetComponent<Image>();
            img.sprite         = disc.icon;
            img.preserveAspect = true;
            img.color          = RelicAccent;
            LayoutElement ile  = iconGO.AddComponent<LayoutElement>();
            ile.minWidth       = IconSize;
            ile.preferredWidth = IconSize;
            ile.flexibleWidth  = 0;
            ile.minHeight      = IconSize;
            ile.preferredHeight= IconSize;
        }

        GameObject col = NewVStack("TextCol", card.transform, Color.clear,
            new RectOffset(0, 0, 0, 0), 4f);
        col.AddComponent<LayoutElement>().flexibleWidth = 1;

        NewText(col.transform, disc.discName.ToUpper(), NameFontSize,
            RelicAccent, FontStyles.Bold, HorizontalAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        Color badgeBg = new Color(RelicAccent.r * 0.12f, RelicAccent.g * 0.12f, 0.05f, 0.90f);
        GameObject badge = NewBox("Badge", col.transform, badgeBg, new RectOffset(6, 6, 4, 4));
        NewFill(badge);
        TextMeshProUGUI badgeLbl = NewText(badge.transform, "PERMANENT DISC", LabelFontSize,
            RelicAccent, FontStyles.Bold, HorizontalAlignmentOptions.Left);
        badgeLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        NewText(col.transform, disc.description, BodyFontSize,
            TextSecondary, FontStyles.Normal, HorizontalAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    // ── Factory helpers ────────────────────────────────────────────────────────
    private static GameObject NewGO(string name, Transform parent, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color         = bg;
        img.raycastTarget = false;
        return go;
    }

    private static GameObject NewBox(string name, Transform parent, Color bg, RectOffset padding)
    {
        GameObject go = NewGO(name, parent, bg);
        VerticalLayoutGroup vl  = go.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment       = TextAnchor.UpperLeft;
        vl.childControlWidth    = true;
        vl.childControlHeight   = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight= false;
        vl.padding              = padding;
        vl.spacing              = 4;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private static GameObject NewVStack(string name, Transform parent, Color bg, RectOffset padding, float spacing)
    {
        GameObject go = NewGO(name, parent, bg);
        VerticalLayoutGroup vl  = go.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment       = TextAnchor.UpperLeft;
        vl.childControlWidth    = true;
        vl.childControlHeight   = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight= false;
        vl.padding              = padding;
        vl.spacing              = spacing;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private static GameObject NewHStack(string name, Transform parent, Color bg, RectOffset padding, float spacing)
    {
        GameObject go = NewGO(name, parent, bg);
        HorizontalLayoutGroup hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment         = TextAnchor.UpperLeft;
        hl.childControlWidth      = true;
        hl.childControlHeight     = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = false;
        hl.padding                = padding;
        hl.spacing                = spacing;
        go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go;
    }

    private TextMeshProUGUI NewText(Transform parent, string text, float fontSize,
        Color color, FontStyles style, HorizontalAlignmentOptions alignment)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        // Stretch horizontally so TMP knows how wide to wrap
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text                = text;
        tmp.fontSize            = fontSize;
        tmp.color               = color;
        tmp.fontStyle           = style;
        tmp.horizontalAlignment = alignment;
        tmp.verticalAlignment   = VerticalAlignmentOptions.Top;
        tmp.textWrappingMode    = TextWrappingModes.Normal;
        tmp.overflowMode        = TextOverflowModes.Overflow;
        tmp.raycastTarget       = false;
        if (_font != null) tmp.font = _font;
        return tmp;
    }

    private static void MakeAbsoluteAccentBar(Transform parent, Color color)
    {
        var bar = new GameObject("AccentBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().color = color;
        RectTransform rt    = bar.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.sizeDelta        = new Vector2(AccentBarWidth, 0f);
        rt.anchoredPosition = Vector2.zero;
    }

    private static void NewFlexAccentBar(Transform parent, Color color, float width)
    {
        var bar = new GameObject("AccentBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().color = color;
        LayoutElement le  = bar.AddComponent<LayoutElement>();
        le.minWidth       = width;
        le.preferredWidth = width;
        le.flexibleWidth  = 0;
        le.flexibleHeight = 1;
    }

    private static void NewFill(GameObject go)
    {
        go.AddComponent<LayoutElement>().flexibleWidth = 1;
    }
}
