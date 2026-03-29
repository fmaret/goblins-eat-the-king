using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class AuraHaloEffect : MonoBehaviour
{
    [Header("Apparence")]
    public Color color = new Color(0.4f, 0f, 1f, 0.8f);
    public Color colorFade = new Color(0.2f, 0f, 0.5f, 0f);

    [Header("Géométrie")]
    [Tooltip("Rayon du halo (doit correspondre à areaRadius de l'AttackDefinition).")]
    public float radius = 1.5f;
    [Tooltip("Épaisseur de l'anneau (0 = cercle parfait, 1 = disque plein).")]
    [Range(0f, 1f)]
    public float thickness = 0.05f;

    [Header("Particules")]
    public int emissionRate = 80;
    public float particleSize = 0.12f;
    public float particleLifetime = 0.8f;
    public float particleSpeed = 0.2f;

    [Header("Mode d'émission sur l'arc")]
    [Tooltip("Loop : parcourt le cercle en continu.\nPingPong : fait l'aller-retour.\nRandom : spawn à des positions aléatoires sur le cercle.\nBurst-Only : aucune émission continue (utilise les bursts manuellement).")]
    public ArcEmissionMode arcMode = ArcEmissionMode.Loop;

    [Tooltip("Vitesse de parcours de l'arc (Loop / PingPong). Plus élevé = particules plus rapides sur l'anneau.")]
    public float arcSpeed = 1f;

    [Tooltip("Espacement entre les particules sur l'arc (0 = aucun, 1 = très espacé). Ignoré en mode Random.")]
    [Range(0f, 1f)]
    public float arcSpread = 0f;

    public enum ArcEmissionMode { Loop, PingPong, Random, BurstOnly }

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
        main.startSpeed = particleSpeed;
        main.startSize = particleSize;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;
        main.playOnAwake = false;

        // ── Emission ──────────────────────────────────────────────────────
        var emission = ps.emission;
        emission.enabled = arcMode != ArcEmissionMode.BurstOnly;
        emission.rateOverTime = emissionRate;

        // ── Shape ─────────────────────────────────────────────────────────
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = thickness;
        shape.arc = 360f;
        shape.arcSpread = arcSpread;

        switch (arcMode)
        {
            case ArcEmissionMode.Loop:
                shape.arcMode = ParticleSystemShapeMultiModeValue.Loop;
                break;
            case ArcEmissionMode.PingPong:
                shape.arcMode = ParticleSystemShapeMultiModeValue.PingPong;
                break;
            case ArcEmissionMode.Random:
                shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
                break;
            case ArcEmissionMode.BurstOnly:
                shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
                break;
        }

        shape.arcSpeed = new ParticleSystem.MinMaxCurve(arcSpeed);

        // ── Color over lifetime ───────────────────────────────────────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color,     0f),
                new GradientColorKey(colorFade, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Size over lifetime ────────────────────────────────────────────
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        // ── Renderer ─────────────────────────────────────────────────────
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 5;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");

        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.color = color;
            rend.sharedMaterial = mat;
        }
    }
}