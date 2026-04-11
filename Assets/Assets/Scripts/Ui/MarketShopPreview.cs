using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only preview companion for <see cref="MarketShop"/>.
///
/// Attach to the same GameObject as <see cref="MarketShop"/>. In the editor (outside
/// Play mode) it populates <see cref="MarketShop.slotRoot"/> with representative slot
/// cards so you can tweak layout, sizes, and colours without entering Play mode.
///
/// Tweak <see cref="MarketShop"/>'s "Slot Visual" fields in the Inspector — the
/// preview rebuilds automatically and those same values are used at runtime by
/// <see cref="MarketShop.GeneratePool"/>, so what you see is what you get.
///
/// All preview children are tagged so they are destroyed the moment Play mode starts —
/// <see cref="MarketShop.GeneratePool"/> then builds the real runtime slots as usual.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MarketShop))]
public class MarketShopPreview : MonoBehaviour
{
    private const string PreviewTag = "__MarketPreview__";

    [Header("Preview Settings")]
    [Tooltip("How many preview slots to show (mirrors MarketShop.PoolSize = 3).")]
    [Range(1, 6)]
    public int previewCount = 3;

    [Tooltip("Show one slot as 'purchased' (greyed out) so you can preview that state.")]
    public bool showOnePurchased = true;

    [Tooltip("Show one slot as 'upgraded' (gold tint) so you can preview that state.")]
    public bool showOneUpgraded = true;

    [Tooltip("Automatically rebuild the preview whenever a value changes in the Inspector.")]
    public bool autoRefresh = true;

    private static Color PlayerNormalTint =>
        GameController.TintConfig != null
            ? GameController.TintConfig.playerNormal
            : new Color(0f, 1f, 0.95f, 1f);

    private static Color PlayerUpgradedTint =>
        GameController.TintConfig != null
            ? GameController.TintConfig.playerUpgraded
            : new Color(1f, 0.84f, 0.10f, 1f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (!Application.isPlaying && autoRefresh)
        {
#if UNITY_EDITOR
            // Defer so all serialized fields on MarketShop are fully loaded
            // before we try to read slotWidth, slotPadLeft, etc.
            EditorApplication.delayCall += () =>
            {
                if (this != null) BuildPreview();
            };
#endif
        }
    }

    private void OnDisable()
    {
        // Entering Play mode triggers OnDisable → destroy all preview slots so
        // GeneratePool() starts from a clean slotRoot.
        DestroyPreview();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && autoRefresh)
        {
            // Defer one frame to avoid modifying the scene during serialisation.
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null) BuildPreview();
            };
#endif
        }
    }

    // ── Preview build / destroy ───────────────────────────────────────────────

    /// <summary>Destroys any existing preview slots and rebuilds them from the catalogue.</summary>
    public void BuildPreview()
    {
        if (Application.isPlaying) return;

        MarketShop shop = GetComponent<MarketShop>();
        if (shop == null || shop.slotRoot == null) return;

        DestroyPreview();

        MarketShop.ShopEntry[] catalogue = shop.catalogue;
        if (catalogue == null || catalogue.Length == 0) return;

        int count = Mathf.Min(previewCount, catalogue.Length);

        for (int i = 0; i < count; i++)
        {
            MarketShop.ShopEntry entry = catalogue[i % catalogue.Length];
            bool purchased = showOnePurchased && i == count - 1;
            bool upgraded  = showOneUpgraded  && i == 1;
            int  cost      = entry.baseCost + (upgraded ? 5 : 0);

            BuildSlotCard(shop.slotRoot, shop, entry, cost, upgraded, purchased);
        }
    }

    /// <summary>Removes all preview children from slotRoot.</summary>
    public void DestroyPreview()
    {
        MarketShop shop = GetComponent<MarketShop>();
        if (shop == null || shop.slotRoot == null) return;

        for (int i = shop.slotRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = shop.slotRoot.GetChild(i);
            if (child.name.StartsWith(PreviewTag))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child.gameObject);
                else
#endif
                    Destroy(child.gameObject);
            }
        }
    }

    // ── Slot card construction ────────────────────────────────────────────────

    private void BuildSlotCard(Transform parent, MarketShop shop, MarketShop.ShopEntry entry,
                                int cost, bool upgraded, bool purchased)
    {
        GameObject card = new GameObject($"{PreviewTag}ShopSlot", typeof(RectTransform));
        card.transform.SetParent(parent, worldPositionStays: false);

        LayoutElement le    = card.AddComponent<LayoutElement>();
        le.preferredWidth   = shop.slotWidth;
        le.preferredHeight  = shop.slotHeight;

        Image cardBg  = card.AddComponent<Image>();
        if (shop.slotBackground != null)
            cardBg.sprite = shop.slotBackground;
        cardBg.color  = shop.slotColor;
        cardBg.type   = Image.Type.Simple;
        Button btn    = card.AddComponent<Button>();
        btn.targetGraphic     = cardBg;
        btn.interactable      = !purchased;

        VerticalLayoutGroup vlg      = card.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment           = TextAnchor.MiddleCenter;
        shop.ApplySlotPadding(vlg.padding);
        vlg.spacing                  = shop.slotSpacing;
        vlg.childControlWidth        = true;
        vlg.childControlHeight       = false;
        vlg.childForceExpandWidth    = true;
        vlg.childForceExpandHeight   = false;

        // Piece sprite
        GameObject imgGO   = new GameObject("Sprite", typeof(RectTransform));
        imgGO.transform.SetParent(card.transform, worldPositionStays: false);
        Image spriteImg    = imgGO.AddComponent<Image>();
        spriteImg.preserveAspect = true;
        spriteImg.raycastTarget  = false;
        LayoutElement imgLE      = imgGO.AddComponent<LayoutElement>();
        imgLE.preferredWidth     = shop.spriteSize;
        imgLE.preferredHeight    = shop.spriteSize;

        Sprite sprite = GetSpriteFromPrefab(entry.prefab);
        if (sprite != null) spriteImg.sprite = sprite;

        spriteImg.color = purchased
            ? shop.slotPurchasedColor
            : upgraded ? PlayerUpgradedTint : PlayerNormalTint;

        // Cost / sold label
        GameObject costGO       = new GameObject("CostLabel", typeof(RectTransform));
        costGO.transform.SetParent(card.transform, worldPositionStays: false);
        TextMeshProUGUI costLbl = costGO.AddComponent<TextMeshProUGUI>();
        costLbl.text            = purchased ? "Sold" : $"{cost} bits";
        costLbl.fontSize        = shop.labelFontSize;
        costLbl.fontStyle       = FontStyles.Bold;
        costLbl.alignment       = TextAlignmentOptions.Center;
        costLbl.color           = Color.white;
        costLbl.raycastTarget   = false;
        LayoutElement costLE    = costGO.AddComponent<LayoutElement>();
        costLE.preferredHeight  = 20f;
    }

    private static Sprite GetSpriteFromPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        return sr != null ? sr.sprite : null;
    }
}

// ── Custom Inspector ──────────────────────────────────────────────────────────

#if UNITY_EDITOR
[CustomEditor(typeof(MarketShopPreview))]
public class MarketShopPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6);

        MarketShopPreview preview = (MarketShopPreview)target;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Refresh Preview", GUILayout.Height(28)))
                preview.BuildPreview();

            if (GUILayout.Button("Clear Preview", GUILayout.Height(22)))
                preview.DestroyPreview();
        }
    }
}
#endif
