using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the floating dust rig into the open scene: a camera-following volume
/// (<see cref="FloatingDust"/>) holding two layers - a fine haze filling the room and
/// a few larger, dimmer motes close to the lens for parallax.
/// Everything it needs is generated on first use, so the menu entry works in any scene.
/// </summary>
public static class FloatingDustBuilder
{
    #region Fields

    private const string RootName = "Floating Dust";
    private const string ParticlesRootName = "Particles";
    private const string TexturePath = "Assets/Visuals/Particles/DustMote.png";
    private const string MaterialPath = "Assets/Visuals/Particles/DustMote.mat";
    private const string ReferenceMaterialPath = "Assets/Visuals/Shaders/ParticleNoBorder.mat";

    #endregion


    #region Menu

    [MenuItem("Tools/Coming of Dorf/Add Floating Dust")]
    public static void AddToActiveScene()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        GameObject particlesRoot = GameObject.Find(ParticlesRootName);
        GameObject root = Create(particlesRoot == null ? null : particlesRoot.transform, SceneViewPivot());

        Undo.RegisterCreatedObjectUndo(root, "Add Floating Dust");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    #endregion


    #region Methods

    /// <summary>Creates the container with both dust layers at the given world position.</summary>
    public static GameObject Create(Transform parent, Vector3 worldPosition)
    {
        Material material = LoadOrCreateMaterial();

        GameObject root = new GameObject(RootName);
        if (parent != null)
            root.transform.SetParent(parent, false);
        root.transform.position = worldPosition;

        BuildLayer(root.transform, material, new DustLayer
        {
            name = "Dust Fine",
            localPosition = Vector3.zero,
            volume = new Vector3(14f, 5f, 9f),
            lifetime = new Vector2(12f, 22f),
            size = new Vector2(0.02f, 0.05f),
            speed = new Vector2(0.01f, 0.04f),
            alpha = new Vector2(0.1f, 0.35f),
            maxParticles = 320,
            rate = 18f,
            noiseStrength = 0.04f,
            noiseFrequency = 0.18f
        });

        BuildLayer(root.transform, material, new DustLayer
        {
            name = "Dust Close",
            localPosition = new Vector3(0f, -0.3f, -3.5f),
            volume = new Vector3(7f, 4f, 3f),
            lifetime = new Vector2(10f, 18f),
            size = new Vector2(0.05f, 0.11f),
            speed = new Vector2(0.01f, 0.05f),
            alpha = new Vector2(0.05f, 0.14f),
            maxParticles = 60,
            rate = 4f,
            noiseStrength = 0.08f,
            noiseFrequency = 0.12f
        });

        root.AddComponent<FloatingDust>();
        return root;
    }

    private static void BuildLayer(Transform parent, Material material, DustLayer layer)
    {
        GameObject holder = new GameObject(layer.name, typeof(ParticleSystem));
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = layer.localPosition;

        ParticleSystem system = holder.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.duration = 20f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(layer.lifetime.x, layer.lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(layer.speed.x, layer.speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(layer.size.x, layer.size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.9f, layer.alpha.x),
                                                            new Color(1f, 0.94f, 0.84f, layer.alpha.y));
        main.gravityModifier = 0f;
        // world space, so the motes hang in the room while the volume follows the camera
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = layer.maxParticles;
        main.playOnAwake = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = layer.rate;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = layer.volume;
        shape.randomDirectionAmount = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.004f, 0.03f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

        // the drift that sells it as air rather than falling dirt
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.strength = layer.noiseStrength;
        noise.frequency = layer.noiseFrequency;
        noise.scrollSpeed = 0.03f;
        noise.octaveCount = 2;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.5f;
    }

    /// <summary>Fades in and out over the lifetime, so nothing pops into view.</summary>
    private static Gradient FadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[]
                         {
                             new GradientColorKey(Color.white, 0f),
                             new GradientColorKey(Color.white, 1f)
                         },
                         new[]
                         {
                             new GradientAlphaKey(0f, 0f),
                             new GradientAlphaKey(1f, 0.2f),
                             new GradientAlphaKey(1f, 0.8f),
                             new GradientAlphaKey(0f, 1f)
                         });
        return gradient;
    }

    /// <summary>
    /// Reuses the dust material once it exists. It is built on the same shader as
    /// ParticleNoBorder, so it behaves like the particles already in the project.
    /// </summary>
    private static Material LoadOrCreateMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        Material reference = AssetDatabase.LoadAssetAtPath<Material>(ReferenceMaterialPath);
        Shader shader = reference != null ? reference.shader : null;
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        Texture2D texture = LoadOrCreateTexture();
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        Directory.CreateDirectory(ProjectPath(Path.GetDirectoryName(MaterialPath)));
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// <summary>A soft round dot with a falloff edge - one speck of dust.</summary>
    private static Texture2D LoadOrCreateTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null)
            return existing;

        const int resolution = 64;
        float radius = resolution * 0.5f;
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                                                  new Vector2(radius, radius)) / radius;
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }
        texture.Apply();

        Directory.CreateDirectory(ProjectPath(Path.GetDirectoryName(TexturePath)));
        File.WriteAllBytes(ProjectPath(TexturePath), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 64;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    /// <summary>Absolute path for an Assets-relative one - File and Directory do not know about the project root.</summary>
    private static string ProjectPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static Vector3 SceneViewPivot()
    {
        SceneView view = SceneView.lastActiveSceneView;
        return view == null ? Vector3.zero : view.pivot;
    }

    #endregion


    #region Types

    private struct DustLayer
    {
        public string name;
        public Vector3 localPosition;
        public Vector3 volume;
        public Vector2 lifetime;
        public Vector2 size;
        public Vector2 speed;
        public Vector2 alpha;
        public int maxParticles;
        public float rate;
        public float noiseStrength;
        public float noiseFrequency;
    }

    #endregion
}
