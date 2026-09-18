using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Runtime presentation for the cleared-room doorway. The serialized settings live on
// LootGoblinRun so the effect can be tuned in the normal Inspector without an editor tool.
public sealed class LootGoblinRoomClearVfx
{
    [Serializable]
    public sealed class Settings
    {
        [Header("Doorway Glow")]
        public DoorwayGlowSettings doorwayGlow = new();

        [Header("Particles / Motes")]
        public MoteSettings motes = new();

        [Header("Torches")]
        public TorchSettings torches = new();

        [Header("Activation")]
        public ActivationSettings activation = new();
    }

    [Serializable]
    public sealed class DoorwayGlowSettings
    {
        [Header("Doorway Interior / Passage")]
        [FormerlySerializedAs("glowColor"), ColorUsage(true, true), Tooltip("Base color of the dark recess beyond the open doorway. Keep this subdued so the space reads as depth, not a portal.")]
        public Color interiorColor = new(.045f, .026f, .018f, 1f);

        [Min(0f), Tooltip("Brightness of the recessed passage back wall.")]
        public float interiorBrightness = .24f;

        [Min(.1f), Tooltip("Width and height of the visible passage aperture.")]
        public Vector2 interiorSize = new(1.8f, 1.65f);

        [Min(0f), Tooltip("Inset from the threshold to the front of the short passage illusion.")]
        public float interiorDepthOffset = .45f;

        [Min(.1f), Tooltip("Depth of the dark passage recess behind the doorway.")]
        public float passageDepth = 1.35f;

        [Min(.01f), Tooltip("Thickness of the passage side walls used to sell the inset depth.")]
        public float passageWallThickness = .12f;

        [Tooltip("Vertical placement of the interior surface relative to the gate center. Negative values lower it toward the threshold.")]
        public float interiorVerticalOffset = -.6648817f;

        [Range(0f, 1f), Tooltip("Strength of the doorway's subtle brighter-threshold to darker-recess gradient.")]
        public float interiorGradientStrength = .9f;

        [Range(0f, .25f), Tooltip("Very subtle spatial value variation that keeps the interior from reading as a flat card.")]
        public float interiorVariationStrength = .04f;

        [Header("Doorway Light")]
        [ColorUsage(true, true), Tooltip("Color of the restrained light cast onto the archway stones.")]
        public Color doorwayLightColor = new(1f, .24f, .08f, 1f);

        [ColorUsage(false, true),
        Tooltip("Base stone color for the fake passage floor and side walls.")]
        public Color passageColor = new(.22f, .17f, .13f, 1f);

        [Min(0f), Tooltip("Point-light intensity for the subtle doorway glow.")]
        public float glowLightIntensity = .6f;

        [Min(0f), Tooltip("Point-light range for the doorway glow.")]
        public float glowRange = 3.2f;

        [Header("Floor Spill")]
        [ColorUsage(true, true), Tooltip("Color of the restrained warm light spill on the nearby floor.")]
        public Color floorSpillColor = new(1f, .22f, .06f, 1f);

        [Min(0f), Tooltip("How strongly the doorway light reaches across the floor.")]
        public float floorSpillIntensity = .16f;

        [Min(0f), Tooltip("Range of the floor-spill light.")]
        public float floorSpillRange = 2.8f;

        [Header("Optional Pulse")]
        [Range(0f, 1f), Tooltip("Subtle brightness variation after activation.")]
        public float pulseAmount = .02f;

        [Min(0f), Tooltip("Pulse cycles per second.")]
        public float pulseSpeed = 1.5f;
    }

    [Serializable]
    public sealed class MoteSettings
    {
        [Range(0, 256), Tooltip("Maximum number of doorway motes alive at once.")]
        public int particleCount = 32;

        [Min(.05f), Tooltip("Lifetime of each floating mote in seconds.")]
        public float particleLifetime = 3.8f;

        [Min(.005f), Tooltip("Billboard size of each mote.")]
        public float particleSize = .05f;

        [Min(0f), Tooltip("Initial upward speed of each mote.")]
        public float particleSpeed = .08f;

        [Min(0f), Tooltip("Horizontal width of the mote spawn volume.")]
        public float spawnWidth = 1.7f;

        [Min(0f), Tooltip("Vertical height of the mote spawn area.")]
        public float spawnHeight = .7f;

        [Min(0f), Tooltip("Depth of the mote spawn area inside the passage.")]
        public float spawnDepth = 1.1f;

        [Min(0f), Tooltip("Horizontal drift applied while motes float.")]
        public float driftAmount = .08f;

        [ColorUsage(true, true), Tooltip("Base color of the floating motes.")]
        public Color particleColor = new(1f, .48f, .18f, 1f);

        [Min(0f), Tooltip("HDR brightness multiplier for mote emission.")]
        public float particleBrightness = .7f;
    }

    [Serializable]
    public sealed class TorchSettings
    {
        [ColorUsage(true, true), Tooltip("Color of the visible flame core and flame particles.")]
        public Color flameColor = new(1f, .35f, .06f, 1f);

        [Min(0f), Tooltip("HDR brightness multiplier for the visible flame core and particles.")]
        public float flameBrightness = 2.2f;

        [ColorUsage(true, true), Tooltip("Color of the warm light cast by each torch.")]
        public Color torchLightColor = new(1f, .38f, .08f, 1f);

        [Min(0f), Tooltip("Warm point-light intensity for each torch.")]
        public float torchLightIntensity = 1.8f;

        [Min(0f), Tooltip("Warm point-light range for each torch.")]
        public float torchLightRange = 3.2f;

        [Min(.01f), Tooltip("Size of the visible flame core and flame particles.")]
        public float flameSize = .28f;

        [Min(0f), Tooltip("Flame particles emitted by each torch per second.")]
        public float flameParticleEmissionRate = 12f;

        [Range(0f, 1f), Tooltip("Amount of warm-light flicker.")]
        public float flickerAmount = .18f;

        [Min(0f), Tooltip("Flicker cycles per second.")]
        public float flickerSpeed = 8f;
    }
 
    [Serializable]
    public sealed class ActivationSettings
    {
        [Min(0f), Tooltip("Delay between doorway activation and torch ignition.")]
        public float torchIgnitionDelay = .14f;

        [Min(0f), Tooltip("Time for the doorway and floor light spill to reach full strength.")]
        public float doorwayGlowFadeInDuration = .85f;

        [Min(0f), Tooltip("Time for an active doorway presentation to fade away when the next room closes the exit.")]
        public float doorwayGlowFadeOutDuration = .2f;

        [Range(0, 128), Tooltip("One-shot mote burst emitted when the torches ignite.")]
        public int initialParticleBurstCount = 24;

        [Min(0f), Tooltip("Continuous mote emission rate after the initial burst.")]
        public float continuousParticleEmissionRate = 8f;
    }

    sealed class Torch
    {
        public Light light;
        public ParticleSystem flame;
        public Transform flameCore;
        public float phase;
    }

    readonly Settings settings;
    readonly Transform owner;
    readonly GameObject gate;
    readonly List<Torch> torchInstances = new();

    Transform runtimeRoot;
    GameObject glowSurface;
    Renderer glowRenderer;
    Material glowMaterial;
    Material moteMaterial;
    Material flameMaterial;
    Material torchMaterial;
    Material passageMaterial;
    ParticleSystem motes;
    Light doorwayLight;
    Light floorSpillLight;
    Vector3 doorwayPosition;
    float elapsed;
    bool activated;
    bool closing;
    bool ignited;

    public LootGoblinRoomClearVfx(Settings settings, Transform owner, GameObject gate)
    {
        this.settings = settings ?? new Settings();
        this.owner = owner;
        this.gate = gate;
        EnsureSettings();
        BuildRuntimeEffect();
        SetLocked();
    }

    public void Tick(float deltaTime)
    {
        float fade;
        if (closing)
        {
            elapsed += Mathf.Max(0f, deltaTime);
            fade = 1f - EvaluateFade(elapsed, settings.activation.doorwayGlowFadeOutDuration);
            UpdateGlow(fade, 1f);
            if (fade <= .001f) HideLocked();
            return;
        }

        if (!activated) return;

        elapsed += Mathf.Max(0f, deltaTime);
        fade = EvaluateFade(elapsed, settings.activation.doorwayGlowFadeInDuration);
        float pulse = 1f + Mathf.Sin(elapsed * Mathf.PI * 2f * settings.doorwayGlow.pulseSpeed) *
            settings.doorwayGlow.pulseAmount * fade;
        UpdateGlow(fade, pulse);

        if (!ignited && elapsed >= settings.activation.torchIgnitionDelay)
        {
            ignited = true;
            IgniteMotesAndTorches();
        }

        UpdateTorches();
    }

    public void SetLocked()
    {
        if (activated && settings.activation.doorwayGlowFadeOutDuration > .001f)
        {
            elapsed = 0f;
            activated = false;
            closing = true;
            ignited = false;
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var torch in torchInstances)
            {
                torch.light.enabled = false;
                torch.flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (torch.flameCore != null) torch.flameCore.gameObject.SetActive(false);
            }
            return;
        }

        HideLocked();
    }

    public void SetLockedImmediate()
    {
        HideLocked();
    }

    void HideLocked()
    {
        elapsed = 0f;
        activated = false;
        closing = false;
        ignited = false;

        SetPassageActive(false);
        SetMaterialColor(glowMaterial, Color.clear);
        SetMaterialFloat(glowMaterial, "_Brightness", 0f);
        if (doorwayLight != null) doorwayLight.enabled = false;
        if (floorSpillLight != null) floorSpillLight.enabled = false;
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (var torch in torchInstances)
        {
            torch.light.enabled = false;
            torch.flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (torch.flameCore != null) torch.flameCore.gameObject.SetActive(false);
        }

    }

    public void Activate()
    {
        if (activated) return;

        elapsed = 0f;
        activated = true;
        closing = false;
        ignited = false;
        SetPassageActive(true);
        if (glowSurface != null)
        {
            SetMaterialColor(glowMaterial, settings.doorwayGlow.interiorColor);
            SetMaterialFloat(glowMaterial, "_Brightness", settings.doorwayGlow.interiorBrightness);
            ConfigureDoorwayMaterial();
        }
        if (doorwayLight != null) doorwayLight.enabled = true;
        if (floorSpillLight != null) floorSpillLight.enabled = true;
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void Dispose()
    {
        if (runtimeRoot != null) UnityEngine.Object.Destroy(runtimeRoot.gameObject);
        DestroyMaterial(glowMaterial);
        DestroyMaterial(moteMaterial);
        DestroyMaterial(flameMaterial);
        DestroyMaterial(torchMaterial);
        DestroyMaterial(passageMaterial);
        runtimeRoot = null;
    }

    void EnsureSettings()
    {
        if (settings.doorwayGlow == null) settings.doorwayGlow = new DoorwayGlowSettings();
        if (settings.motes == null) settings.motes = new MoteSettings();
        if (settings.torches == null) settings.torches = new TorchSettings();
        if (settings.activation == null) settings.activation = new ActivationSettings();
    }

    void BuildRuntimeEffect()
    {
        runtimeRoot = new GameObject("Room Clear VFX").transform;
        runtimeRoot.SetParent(owner, false);
        doorwayPosition = gate != null ? gate.transform.position : new Vector3(0f, 1f, 7.9f);

        glowMaterial = CreateMaterial("Room Clear Doorway Glow", "LootGoblin/Room Clear Doorway");
        moteMaterial = CreateMaterial("Room Clear Motes", "Universal Render Pipeline/Particles/Unlit");
        flameMaterial = CreateMaterial("Room Clear Torch Flame", "Universal Render Pipeline/Particles/Unlit");
        torchMaterial = CreateMaterial("Room Clear Torch Fixture", "Universal Render Pipeline/Lit");
        passageMaterial = CreateMaterial("Room Clear Passage", "Universal Render Pipeline/Lit");
        SetMaterialColor(flameMaterial, GetFlameColor());
        SetMaterialColor(torchMaterial, new Color(.16f, .1f, .06f, 1f));
        SetMaterialColor(passageMaterial, GetPassageColor());

        CreateGlowSurface();
        CreatePassageGeometry();
        CreateLights();
        CreateMotes();
        CreateTorches();
    }

    void CreateGlowSurface()
    {
        // The back wall remains a thin cube so the recessed darkness is visible from
        // the angled portrait camera regardless of winding. It is deliberately not
        // placed at the threshold: the side walls and floor establish an inset.
        glowSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glowSurface.name = "Doorway Glow Surface";
        glowSurface.transform.SetParent(runtimeRoot, false);
        glowSurface.transform.position = new Vector3(
            0f,
            doorwayPosition.y + settings.doorwayGlow.interiorVerticalOffset,
            doorwayPosition.z + settings.doorwayGlow.interiorDepthOffset + settings.doorwayGlow.passageDepth);
        glowSurface.transform.rotation = Quaternion.identity;
        glowSurface.transform.localScale = new Vector3(settings.doorwayGlow.interiorSize.x, settings.doorwayGlow.interiorSize.y, .06f);
        DisableCollider(glowSurface);
        glowRenderer = glowSurface.GetComponent<Renderer>();
        glowRenderer.allowOcclusionWhenDynamic = false;
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.sharedMaterial = glowMaterial;
    }

    void CreatePassageGeometry()
    {
        float width = Mathf.Max(.2f, settings.doorwayGlow.interiorSize.x);
        float height = Mathf.Max(.2f, settings.doorwayGlow.interiorSize.y);
        float depth = Mathf.Max(.1f, settings.doorwayGlow.passageDepth);
        float wall = Mathf.Max(.01f, settings.doorwayGlow.passageWallThickness);
        float centerY = doorwayPosition.y + settings.doorwayGlow.interiorVerticalOffset;
        float frontZ = doorwayPosition.z + settings.doorwayGlow.interiorDepthOffset;
        float centerZ = frontZ + depth * .5f;

        var left = CreatePassagePrimitive("Passage Left Wall",
            new Vector3(-width * .5f - wall * .5f, centerY, centerZ),
            new Vector3(wall, height, depth));
        var right = CreatePassagePrimitive("Passage Right Wall",
            new Vector3(width * .5f + wall * .5f, centerY, centerZ),
            new Vector3(wall, height, depth));
        var floor = CreatePassagePrimitive("Passage Floor",
            new Vector3(0f, .045f, centerZ),
            new Vector3(width + wall * 2f, .09f, depth));

        left.SetActive(false);
        right.SetActive(false);
        floor.SetActive(false);
    }

    GameObject CreatePassagePrimitive(string name, Vector3 position, Vector3 scale)
    {
        var objectRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objectRoot.name = name;
        objectRoot.transform.SetParent(runtimeRoot, false);
        objectRoot.transform.position = position;
        objectRoot.transform.localScale = scale;
        DisableCollider(objectRoot);
        var renderer = objectRoot.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.sharedMaterial = passageMaterial;
        return objectRoot;
    }

    void SetPassageActive(bool active)
    {
        if (glowSurface != null) glowSurface.SetActive(active);
        if (runtimeRoot == null) return;
        for (int i = 0; i < runtimeRoot.childCount; i++)
        {
            var child = runtimeRoot.GetChild(i);
            if (child.name.StartsWith("Passage ", StringComparison.Ordinal)) child.gameObject.SetActive(active);
        }
    }

    void CreateLights()
    {
        doorwayLight = CreatePointLight("Doorway Glow Light", doorwayPosition + Vector3.forward * .1f + Vector3.up * 1.3f);
        doorwayLight.range = settings.doorwayGlow.glowRange;
        doorwayLight.color = settings.doorwayGlow.doorwayLightColor;

        floorSpillLight = CreatePointLight("Doorway Floor Spill", doorwayPosition + Vector3.back * .9f + Vector3.up * .65f);
        floorSpillLight.range = settings.doorwayGlow.floorSpillRange;
        floorSpillLight.color = settings.doorwayGlow.floorSpillColor;
    }

    void CreateMotes()
    {
        var objectRoot = new GameObject("Floating Motes");
        objectRoot.transform.SetParent(runtimeRoot, false);
        objectRoot.transform.position = doorwayPosition + Vector3.forward *
            (settings.doorwayGlow.interiorDepthOffset + settings.doorwayGlow.passageDepth * .35f) + Vector3.up * .15f;
        motes = objectRoot.AddComponent<ParticleSystem>();
        var main = motes.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = settings.motes.particleLifetime;
        main.startSpeed = settings.motes.particleSpeed;
        main.startSize = settings.motes.particleSize;
        main.startColor = GetMoteColor();
        main.maxParticles = Mathf.Max(8, settings.motes.particleCount);

        var emission = motes.emission;
        emission.enabled = true;
        emission.rateOverTime = settings.activation.continuousParticleEmissionRate;

        var shape = motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(settings.motes.spawnWidth, settings.motes.spawnHeight, settings.motes.spawnDepth);

        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-settings.motes.driftAmount, settings.motes.driftAmount);
        velocity.y = new ParticleSystem.MinMaxCurve(.025f, .1f);
        velocity.z = new ParticleSystem.MinMaxCurve(-settings.motes.driftAmount * .5f, settings.motes.driftAmount * .5f);

        var renderer = objectRoot.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = moteMaterial;
    }

    void CreateTorches()
    {
        float[] positions = { -1f, 1f };
        for (int i = 0; i < positions.Length; i++)
        {
            var torchRoot = new GameObject("Torch " + (i + 1));
            torchRoot.transform.SetParent(runtimeRoot, false);
            // Keep the X/Z doorway offsets while placing the runtime anchor on the floor
            // plane. The gate mesh is centered above that plane at roughly Y 1.01.
            torchRoot.transform.position = new Vector3(
                doorwayPosition.x + positions[i] * .95f,
                0f,
                doorwayPosition.z - .18f);

            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fixture.name = "Torch Fixture";
            fixture.transform.SetParent(torchRoot.transform, false);
            fixture.transform.localPosition = Vector3.up * .55f;
            fixture.transform.localScale = new Vector3(.09f, .55f, .09f);
            DisableCollider(fixture);
            fixture.GetComponent<Renderer>().sharedMaterial = torchMaterial;

            var flameCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flameCore.name = "Flame Core";
            flameCore.transform.SetParent(torchRoot.transform, false);
            flameCore.transform.localPosition = Vector3.up * 1.2f;
            DisableCollider(flameCore);
            flameCore.GetComponent<Renderer>().sharedMaterial = flameMaterial;

            var flameObject = new GameObject("Flame Particles");
            flameObject.transform.SetParent(torchRoot.transform, false);
            flameObject.transform.localPosition = Vector3.up * 1.15f;
            var flame = flameObject.AddComponent<ParticleSystem>();
            var main = flame.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = .42f;
            main.startSpeed = .22f;
            main.startSize = settings.torches.flameSize;
            main.startColor = GetFlameColor();
            main.maxParticles = Mathf.Max(16, Mathf.CeilToInt(settings.torches.flameParticleEmissionRate * .6f) + 8);
            var emission = flame.emission;
            emission.enabled = true;
            emission.rateOverTime = settings.torches.flameParticleEmissionRate;
            var shape = flame.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = .08f;
            var particleRenderer = flameObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.material = flameMaterial;

            var torchLight = CreatePointLight("Torch Light", flameObject.transform.position);
            torchLight.transform.SetParent(torchRoot.transform, true);
            torchLight.color = settings.torches.torchLightColor;
            torchLight.range = settings.torches.torchLightRange;

            torchInstances.Add(new Torch
            {
                light = torchLight,
                flame = flame,
                flameCore = flameCore.transform,
                phase = i * 1.71f
            });
        }
    }

    void IgniteMotesAndTorches()
    {
        if (motes != null)
        {
            motes.Play();
            if (settings.activation.initialParticleBurstCount > 0)
                motes.Emit(settings.activation.initialParticleBurstCount);
        }

        foreach (var torch in torchInstances)
        {
            torch.flame.Play();
            torch.flameCore.gameObject.SetActive(true);
            torch.light.enabled = true;
        }
    }

    void UpdateGlow(float fade, float pulse)
    {
        SetMaterialColor(glowMaterial, settings.doorwayGlow.interiorColor);
        SetMaterialFloat(glowMaterial, "_Brightness",
            fade * pulse * settings.doorwayGlow.interiorBrightness);
        SetMaterialColor(passageMaterial, GetPassageColor());
        ConfigureDoorwayMaterial();
        if (doorwayLight != null)
        {
            doorwayLight.intensity = settings.doorwayGlow.glowLightIntensity * fade * pulse;
            doorwayLight.range = settings.doorwayGlow.glowRange;
            doorwayLight.color = settings.doorwayGlow.doorwayLightColor;
        }
        if (floorSpillLight != null)
        {
            floorSpillLight.intensity = settings.doorwayGlow.floorSpillIntensity * fade * pulse;
            floorSpillLight.range = settings.doorwayGlow.floorSpillRange;
            floorSpillLight.color = settings.doorwayGlow.floorSpillColor;
        }

    }

    void ConfigureDoorwayMaterial()
    {
        SetMaterialFloat(glowMaterial, "_GradientStrength", settings.doorwayGlow.interiorGradientStrength);
        SetMaterialFloat(glowMaterial, "_VariationStrength", settings.doorwayGlow.interiorVariationStrength);
    }

    void UpdateTorches()
    {
        if (!ignited) return;

        float ignitionProgress = Mathf.Clamp01((elapsed - settings.activation.torchIgnitionDelay) / .2f);
        foreach (var torch in torchInstances)
        {
            float wave = Mathf.Sin((elapsed * Mathf.PI * 2f * settings.torches.flickerSpeed) + torch.phase) * .65f +
                Mathf.Sin((elapsed * Mathf.PI * 2f * settings.torches.flickerSpeed * .47f) + torch.phase * 1.7f) * .35f;
            float flicker = 1f + wave * settings.torches.flickerAmount;
            torch.light.intensity = settings.torches.torchLightIntensity * ignitionProgress * flicker;
            torch.light.range = settings.torches.torchLightRange;
            torch.light.color = settings.torches.torchLightColor;
            float coreScale = settings.torches.flameSize * (1f + wave * settings.torches.flickerAmount * .45f);
            torch.flameCore.localScale = new Vector3(coreScale * .8f, coreScale * 1.45f, coreScale * .8f);
        }
    }

    static float EvaluateFade(float time, float duration)
    {
        if (duration <= .001f) return 1f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
    }

    Color GetMoteColor()
    {
        return settings.motes.particleColor * settings.motes.particleBrightness;
    }

    Color GetFlameColor()
    {
        return settings.torches.flameColor * settings.torches.flameBrightness;
    }

    Color GetPassageColor()
    {
        return settings.doorwayGlow.passageColor;
    }

    Light CreatePointLight(string name, Vector3 position)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(runtimeRoot, false);
        lightObject.transform.position = position;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    Material CreateMaterial(string name, string shaderName)
    {
        Shader shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = name };
        material.enableInstancing = true;
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        material.renderQueue = 2000;
        if (shaderName.Contains("Particles"))
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.renderQueue = 3000;
        }
        return material;
    }

    static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color);
    }

    static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
    }

    static void DisableCollider(GameObject gameObject)
    {
        var collider = gameObject.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.Destroy(collider);
    }

    static void DestroyMaterial(Material material)
    {
        if (material != null) UnityEngine.Object.Destroy(material);
    }
}
