using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the FloppySlotUI shader parameters from the Inspector.
/// Attach this to each FloppySlot root alongside its Image component.
/// All visual properties can be tweaked at edit-time and will update live.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class FloppySlotStyle : MonoBehaviour
{
    // ── Property ID cache ─────────────────────────────────────────────────────
    private static readonly int PropFillColor    = Shader.PropertyToID("_FillColor");
    private static readonly int PropCornerRadius = Shader.PropertyToID("_CornerRadius");
    private static readonly int PropBevelWidth   = Shader.PropertyToID("_BevelWidth");
    private static readonly int PropBevelBright  = Shader.PropertyToID("_BevelBright");
    private static readonly int PropBevelDark    = Shader.PropertyToID("_BevelDark");
    private static readonly int PropBorderWidth  = Shader.PropertyToID("_BorderWidth");
    private static readonly int PropBorderColor  = Shader.PropertyToID("_BorderColor");

    [Header("Shape")]
    [Tooltip("Radius of the rounded corners (0 = sharp, 0.5 = circle).")]
    [Range(0f, 0.5f)] public float cornerRadius = 0.18f;

    [Header("Fill")]
    public Color fillColor = new Color(0.10f, 0.14f, 0.26f, 1f);

    [Header("Inner Bevel")]
    [Tooltip("Width of the bevel band as a fraction of the slot size.")]
    [Range(0f, 0.3f)] public float bevelWidth  = 0.08f;
    [Tooltip("Brightness multiplier for the lit side of the bevel.")]
    [Range(0f, 2f)]   public float bevelBright = 0.6f;
    [Tooltip("Brightness multiplier for the shadow side of the bevel.")]
    [Range(0f, 2f)]   public float bevelDark   = 0.2f;

    [Header("Border")]
    [Range(0f, 0.1f)] public float borderWidth = 0.018f;
    public Color borderColor = new Color(0.28f, 0.40f, 0.70f, 1f);

    private Image    image;
    private Material instanceMat;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void OnDestroy()
    {
        if (instanceMat != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(instanceMat);
#else
            Destroy(instanceMat);
#endif
        }
    }

    /// <summary>Pushes all style properties into the Image's material instance.</summary>
    public void Apply()
    {
        if (image == null)
            image = GetComponent<Image>();
        if (image == null) return;

        // Create a per-instance material so slots can differ from each other.
        if (instanceMat == null || instanceMat != image.material)
        {
            if (image.material != null && image.material.shader != null &&
                image.material.shader.name == "Chaturanga/UI/FloppySlotUI")
            {
#if UNITY_EDITOR
                // In the editor before play we manage the instance ourselves.
                instanceMat = new Material(image.material);
                instanceMat.hideFlags = HideFlags.DontSave;
                image.material = instanceMat;
#else
                instanceMat = image.material;
#endif
            }
            else
            {
                return;
            }
        }

        instanceMat.SetColor(PropFillColor,    fillColor);
        instanceMat.SetFloat(PropCornerRadius, cornerRadius);
        instanceMat.SetFloat(PropBevelWidth,   bevelWidth);
        instanceMat.SetFloat(PropBevelBright,  bevelBright);
        instanceMat.SetFloat(PropBevelDark,    bevelDark);
        instanceMat.SetFloat(PropBorderWidth,  borderWidth);
        instanceMat.SetColor(PropBorderColor,  borderColor);
    }
}
