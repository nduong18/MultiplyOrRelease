using UnityEngine;

namespace MultiplyOrRelease
{

/// <summary>
/// A compact, world-space celebration effect made from Unity particle systems.
/// Call <see cref="BurstAt"/> from scoring code to celebrate at a world position.
/// </summary>
[DisallowMultipleComponent]
public sealed class SimulationCelebrationFirework : MonoBehaviour
{
    private static readonly Color[] CelebrationColors =
    {
        new(1f, 0.93f, 0.33f, 1f),
        new(1f, 0.36f, 0.32f, 1f),
        new(0.24f, 0.83f, 1f, 1f),
        new(0.48f, 0.94f, 0.56f, 1f),
        new(1f, 0.62f, 0.16f, 1f)
    };

    [Header("Burst")]
    [SerializeField, Min(1)] private int sparkCount = 96;
    [SerializeField, Min(1)] private int confettiCount = 55;
    [SerializeField, Min(0.1f)] private float burstRadius = 3.2f;
    [SerializeField] private int sortingOrder = 40;

    private ParticleSystem sparks;
    private ParticleSystem glow;
    private ParticleSystem confetti;
    private ParticleSystem flash;
    private Material particleMaterial;
    public Shader particleShader;
    public Shader glowShader;
    private Mesh confettiMesh;
    private Material glowMaterial;

    /// <summary>Creates a burst at this component's world position.</summary>
    [ContextMenu("Play Celebration Burst")]
    public void Burst()
    {
        BurstAt(transform.position);
    }

    /// <summary>
    /// Plays a firework-style spark burst followed by falling colored confetti.
    /// This method is safe to call from any goal or scoring system.
    /// </summary>
    public void BurstAt(Vector3 worldPosition)
    {
        EnsureParticleSystems();
        EmitFlash(worldPosition);
        EmitSparks(worldPosition);
        EmitConfetti(worldPosition);
    }

    public static void BurstAt(Vector3 worldPosition, bool createHostIfMissing = true)
    {
        SimulationCelebrationFirework effect = FindFirstObjectByType<SimulationCelebrationFirework>();
        if (effect == null && createHostIfMissing)
        {
            effect = CreateRuntimeHost();
        }

        if (effect != null)
        {
            effect.BurstAt(worldPosition);
        }
    }

    /// <summary>Raises this effect above UI that uses a Screen Space Camera canvas.</summary>
    public void SetSortingOrder(int order)
    {
        sortingOrder = order;
        foreach (ParticleSystem system in new[] { sparks, glow, confetti, flash })
        {
            if (system != null)
            {
                system.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder;
            }
        }
    }

    // Lazy creation allows the imported shaders to be assigned before the first burst.

    private void EnsureParticleSystems()
    {
        if (sparks != null)
        {
            return;
        }

        sparks = CreateParticleSystem("Spark Trails", 512, ParticleSystemRenderMode.Stretch);
        ParticleSystem.MainModule sparkMain = sparks.main;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.25f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        sparkMain.gravityModifier = 0.22f;

        ParticleSystem.TrailModule trails = sparks.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = 0.13f;
        trails.dieWithParticles = true;
        trails.widthOverTrail = 0.55f;
        trails.colorOverLifetime = CreateSparkTrailGradient();

        glow = CreateParticleSystem("Soft Spark Glow", 512, ParticleSystemRenderMode.Stretch, GetGlowMaterial());
        ParticleSystem.MainModule glowMain = glow.main;
        glowMain.startLifetime = new ParticleSystem.MinMaxCurve(0.52f, 0.9f);
        glowMain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
        glowMain.gravityModifier = 0.1f;

        confetti = CreateParticleSystem("Falling Confetti", 1024, ParticleSystemRenderMode.Mesh);
        ParticleSystem.MainModule confettiMain = confetti.main;
        confettiMain.startLifetime = new ParticleSystem.MinMaxCurve(1.15f, 1.9f);
        confettiMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
        confettiMain.gravityModifier = 0.55f;
        confettiMain.startRotation3D = true;
        confettiMain.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        confettiMain.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        confettiMain.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.RotationOverLifetimeModule rotation = confetti.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rotation.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
        rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

        ParticleSystemRenderer confettiRenderer = confetti.GetComponent<ParticleSystemRenderer>();
        confettiMesh = CreateConfettiMesh();
        confettiRenderer.mesh = confettiMesh;

        flash = CreateParticleSystem("White Flash", 64, ParticleSystemRenderMode.Billboard);
        ParticleSystem.MainModule flashMain = flash.main;
        flashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        flashMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        flashMain.gravityModifier = 0f;
    }

    private ParticleSystem CreateParticleSystem(
        string systemName,
        int maxParticles,
        ParticleSystemRenderMode renderMode,
        Material material = null)
    {
        GameObject systemObject = new(systemName);
        systemObject.transform.SetParent(transform, false);
        systemObject.layer = gameObject.layer;

        ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.useUnscaledTime = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        renderer.sortingOrder = sortingOrder;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 10f;
        Material resolvedMaterial = material != null ? material : GetParticleMaterial();
        renderer.sharedMaterial = resolvedMaterial;
        renderer.trailMaterial = resolvedMaterial;
        return system;
    }

    private Material GetParticleMaterial()
    {
        if (particleMaterial != null)
        {
            return particleMaterial;
        }

        // The built-in particle shader is not reliably available in this 2D project.
        // Sprites/Default is always present here and supports ParticleSystem vertex colors.
        Shader shader = particleShader != null ? particleShader : Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        particleMaterial = new Material(shader) { name = "Celebration Particle Material" };
        return particleMaterial;
    }

    private Material GetGlowMaterial()
    {
        if (glowMaterial != null)
        {
            return glowMaterial;
        }

        Shader shader = glowShader != null ? glowShader : Shader.Find("SquareCountry/Soft Particle Glow");
        if (shader == null)
        {
            return GetParticleMaterial();
        }

        glowMaterial = new Material(shader) { name = "Celebration Soft Glow Material" };
        return glowMaterial;
    }

    private void EmitFlash(Vector3 position)
    {
        ParticleSystem.EmitParams particle = new()
        {
            position = position,
            startColor = Color.white,
            startSize = 0.18f,
            velocity = Vector3.zero
        };
        flash.Emit(particle, 15);
    }

    private void EmitSparks(Vector3 position)
    {
        for (int index = 0; index < sparkCount; index++)
        {
            float angle = index * Mathf.PI * 2f / sparkCount + Random.Range(-0.035f, 0.035f);
            float speed = Random.Range(burstRadius * 0.72f, burstRadius * 1.12f);
            Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            ParticleSystem.EmitParams particle = new()
            {
                position = position + Random.insideUnitSphere * 0.035f,
                velocity = direction * speed,
                startColor = index % 5 == 0 ? Color.white : CelebrationColors[index % CelebrationColors.Length],
                startSize = Random.Range(0.035f, 0.085f),
                startLifetime = Random.Range(0.82f, 1.28f)
            };
            sparks.Emit(particle, 1);

            Color glowColor = particle.startColor;
            glowColor.a = 0.26f;
            particle.startColor = glowColor;
            particle.startSize *= 3.2f;
            particle.startLifetime *= 0.72f;
            glow.Emit(particle, 1);
        }
    }

    private void EmitConfetti(Vector3 position)
    {
        for (int index = 0; index < confettiCount; index++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Vector3 velocity = new(direction.x * Random.Range(1.1f, 2.9f),
                Random.Range(1.6f, 3.4f) + direction.y * 1.2f, 0f);
            ParticleSystem.EmitParams particle = new()
            {
                position = position + Random.insideUnitSphere * 0.06f,
                velocity = velocity,
                startColor = CelebrationColors[Random.Range(0, CelebrationColors.Length)],
                startSize = Random.Range(0.07f, 0.14f),
                startLifetime = Random.Range(1.15f, 1.9f),
                rotation3D = Random.insideUnitSphere * 360f
            };
            confetti.Emit(particle, 1);
        }
    }

    private static ParticleSystem.MinMaxGradient CreateSparkTrailGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.88f, 0.2f), 0.35f),
                new GradientColorKey(new Color(1f, 0.5f, 0.12f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.55f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static Mesh CreateConfettiMesh()
    {
        Mesh mesh = new() { name = "Confetti Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.22f, 0f), new Vector3(0.5f, -0.22f, 0f),
            new Vector3(-0.5f, 0.22f, 0f), new Vector3(0.5f, 0.22f, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static SimulationCelebrationFirework CreateRuntimeHost()
    {
        GameObject host = new("Celebration Fireworks");
        return host.AddComponent<SimulationCelebrationFirework>();
    }

    private void OnDestroy()
    {
        Release(particleMaterial); Release(glowMaterial); Release(confettiMesh);
    }
    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}
}

