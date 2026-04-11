using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that builds the player and enemy glow particle prefabs.
/// Run via Tools → Chaturanga → Create Glow Particle Prefabs.
/// Recreates both prefabs from scratch so they always match the current config.
/// </summary>
public static class GlowParticleBuilder
{
    private const string PlayerPrefabPath = "Assets/Assets/Prefabs/FX_GlowParticles_Player.prefab";
    private const string EnemyPrefabPath  = "Assets/Assets/Prefabs/FX_GlowParticles_Enemy.prefab";

    [MenuItem("Tools/Chaturanga/Create Glow Particle Prefabs")]
    public static void CreateBoth()
    {
        Build(PlayerPrefabPath, new Color(1.00f, 0.84f, 0.10f, 1f), "Player");
        Build(EnemyPrefabPath,  new Color(0.65f, 0.10f, 0.90f, 1f), "Enemy");
        AssetDatabase.SaveAssets();
        Debug.Log("[GlowParticleBuilder] Prefabs created:\n  " + PlayerPrefabPath + "\n  " + EnemyPrefabPath);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static void Build(string savePath, Color color, string label)
    {
        // Create a temporary scene object, configure it, then save as prefab.
        var go = new GameObject($"FX_GlowParticles_{label}");
        var ps = go.AddComponent<ParticleSystem>();

        ConfigureParticleSystem(ps, color);

        // Renderer settings.
        var rend              = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode       = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder     = 300;
        rend.sortingLayerName = "Default";
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows   = false;

        PrefabUtility.SaveAsPrefabAsset(go, savePath);
        Object.DestroyImmediate(go);
    }

    private static void ConfigureParticleSystem(ParticleSystem ps, Color color)
    {
        // ── Particle counts / timing ─────────────────────────────────────────
        const int   particleCount     = 4;
        const float particleLifetime  = 1.8f;
        const float particleSpeed     = 0.25f;
        const float spawnHalfWidth    = 0.15f;
        const float particleSize      = 0.06f;

        // ── Main module ──────────────────────────────────────────────────────
        var main             = ps.main;
        main.loop            = true;
        main.startLifetime   = particleLifetime;
        main.startSpeed      = 0f;
        main.startSize       = particleSize;
        // Use a constant opaque colour; alpha fade is handled entirely by colorOverLifetime.
        main.startColor      = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, 1f));
        main.maxParticles    = particleCount + 2;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        // ── Emission ─────────────────────────────────────────────────────────
        var emission          = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = particleCount / particleLifetime;

        // ── Shape: horizontal edge ───────────────────────────────────────────
        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
        shape.radius    = spawnHalfWidth;

        // ── Velocity over lifetime (world space, upward) ─────────────────────
        var vel     = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(0f);
        vel.y       = new ParticleSystem.MinMaxCurve(particleSpeed);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        // ── Size over lifetime: off (constant size) ──────────────────────────
        var size     = ps.sizeOverLifetime;
        size.enabled = false;

        // ── Colour over lifetime: alpha fade only ────────────────────────────
        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.55f, 0.15f),
                new GradientAlphaKey(0.45f, 0.6f),
                new GradientAlphaKey(0f,    1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);
    }
}
