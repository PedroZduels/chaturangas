using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility: sets _Opacity on the overlapping sky material (4.mat)
/// to 0.55. Run once via the menu item, then this script can be deleted.
/// </summary>
public static class SetSkyOpacity
{
    private const string MaterialPath = "Assets/Assets/Materials/4.mat";
    private const float  TargetOpacity = 0.55f;

    /// <summary>Sets the _Opacity on the 4.mat sky overlay to 0.55.</summary>
    [MenuItem("Tools/Chaturanga/Set Sky Overlay Opacity")]
    public static void Apply()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Debug.LogError($"[SetSkyOpacity] Could not find material at: {MaterialPath}");
            return;
        }

        if (!mat.HasProperty("_Opacity"))
        {
            Debug.LogError($"[SetSkyOpacity] Material '{mat.name}' does not have _Opacity property.");
            return;
        }

        mat.SetFloat("_Opacity", TargetOpacity);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SetSkyOpacity] Set _Opacity = {TargetOpacity} on '{mat.name}'.");
    }
}
