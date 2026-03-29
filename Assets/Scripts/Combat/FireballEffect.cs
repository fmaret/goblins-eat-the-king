using UnityEngine;

/// <summary>
/// Effet visuel de boule de feu pour un projectile.
/// Attache ce script sur un Empty GameObject, ajoute un Collider2D (IsTrigger),
/// mets-le en prefab et glisse-le dans "Projectile Prefab" de ton AttackDefinition.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class FireballEffect : MonoBehaviour
{
    [Header("Couleurs")]
    [Tooltip("Couleur du cœur de la flamme.")]
    public Color coreColor = new Color(1f, 0.6f, 0f, 1f);       // orange vif
    [Tooltip("Couleur des bords / fin de vie.")]
    public Color outerColor = new Color(1f, 0.1f, 0f, 0f);      // rouge transparent

    [Header("Taille")]
    [Tooltip("Taille globale de la boule de feu.")]
    public float ballRadius = 0.3f;
    [Tooltip("Taille de chaque particule.")]
    public float particleSize = 0.25f;

    [Header("Particules")]
    public int emissionRate = 60;
    [Tooltip("Durée de vie des particules de flamme.")]
    public float particleLifetime = 0.3f;
    [Tooltip("Vitesse de dispersion des particules depuis le centre.")]
    public float particleSpeed = 0.8f;

    [Header("Traînée")]
    [Tooltip("Ajouter une traînée derrière la boule (recommandé).")]
    public bool enableTrail = true;
    public Color trailColor = new Color(1f, 0.3f, 0f, 0.5f);
    public float trailLifetime = 0.15f;

    void OnEnable()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetupParticleSystem();
        if (ps != null)
            ps.Play(true);
    }

    void OnDisable()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnValidate()
    {
        SetupParticleSystem();
    }

    private void SetupParticleSystem()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null) return;

        // ── Main ──────────────────────────────────────────────────────────
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = particleLifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, particleSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize);
        main.startColor = coreColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.playOnAwake = false;
        main.gravityModifier = -0.05f; // légère montée des flammes

        // ── Emission ──────────────────────────────────────────────────────
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        // ── Shape : sphère = boule de feu ────────────────────────────────
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = ballRadius;
        shape.radiusThickness = 1f; // émet depuis tout le volume

        // ── Color over lifetime ───────────────────────────────────────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(coreColor,  0f),
                new GradientColorKey(outerColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f,  1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Size over lifetime : gonfle puis rétrécit ─────────────────────
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f,   0.4f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f,   0f)
            ));

        // ── Rotation over lifetime : tourbillon ──────────────────────────
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        // ── Trails ───────────────────────────────────────────────────────
        var trails = ps.trails;
        trails.enabled = enableTrail;
        if (enableTrail)
        {
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.lifetime = new ParticleSystem.MinMaxCurve(trailLifetime);
            trails.minVertexDistance = 0.05f;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(trailColor, new Color(trailColor.r, trailColor.g, trailColor.b, 0f));
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        }

        // ── Renderer ─────────────────────────────────────────────────────
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 5;
        rend.trailMaterial = rend.sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");

        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 3f);  // Additive
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.color = coreColor;
            rend.sharedMaterial = mat;
            rend.trailMaterial = mat;
        }
    }
}
