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
        [Header("Doorway Interior")]
        [FormerlySerializedAs("glowColor"), ColorUsage(true, true), Tooltip("Color of the illuminated room immediately beyond the doorway.")]
        public Color interiorColor = new(.04f, .95f, .9f, 1f);

        [Min(0f), Tooltip("HDR brightness of the doorway interior surface.")]
        public float interiorBrightness = 1.35f;

        [Min(.1f), Tooltip("Width and height of the illuminated interior aperture.")]
        public Vector2 interiorSize = new(1.55f, 1.6f);

        [Tooltip("Depth of the interior surface behind the gateway. Increase to push it farther into the next room.")]
        public float interiorDepthOffset = .22f;

        [Min(0f), Tooltip("Vertical placement of the interior surface relative to the gate center.")]
        public float interiorVerticalOffset = .04f;

        [Header("Doorway Light")]
        [ColorUsage(true, true), Tooltip("Color of the light cast onto the archway stones.")]
        public Color doorwayLightColor = new(.04f, .95f, .9f, 1f);

        [Min(0f), Tooltip("Point-light intensity at the open doorway.")]
        public float glowLightIntensity = 4.5f;

        [Min(0f), Tooltip("Point-light range for the doorway glow.")]
        public float glowRange = 6.5f;

        [Header("Floor Spill")]
        [ColorUsage(true, true), Tooltip("Color of the restrained light spill on the nearby floor.")]
        public Color floorSpillColor = new(.04f, .95f, .9f, 1f);

        [Min(0f), Tooltip("How strongly the teal light reaches across the floor.")]
        public float floorSpillIntensity = .5f;

        [Min(0f), Tooltip("Range of the floor-spill light.")]
        public float floorSpillRange = 4.5f;

        [Header("Optional Pulse")]
        [Range(0f, 1f), Tooltip("Subtle brightness variation after activation.")]
        public float pulseAmount = .03f;

        [Min(0f), Tooltip("Pulse cycles per second.")]
        public float pulseSpeed = 1.5f;
    }

    [Serializable]
    public sealed class MoteSettings
    {
        [Min(.05f), Tooltip("Lifetime of each floating mote in seconds.")]
        public float particleLifetime = 3.8f;

        [Min(.005f), Tooltip("Billboard size of each mote.")]
        public float particleSize = .11f;

        [Min(0f), Tooltip("Initial upward speed of each mote.")]
        public float particleSpeed = .18f;

        [Min(0f), Tooltip("Horizontal width of the mote spawn volume.")]
        public float spawnWidth = 2.7f;

        [Min(0f), Tooltip("Horizontal drift applied while motes float.")]
        public float driftAmount = .16f;

        [ColorUsage(true, true), Tooltip("Base color of the floating motes.")]
        public Color particleColor = new(.2f, 1f, .94f, 1f);

        [Min(0f), Tooltip("HDR brightness multiplier for mote emission.")]
        public float particleBrightness = 2.2f;
    }

    [Serializable]
    public sealed class TorchSettings
    {
        [ColorUsage(true, true), Tooltip("Color of the visible flame core and flame particles.")]
        public Color flameColor = new(1f, .35f, .06f, 1f);

        [ColorUsage(true, true), Tooltip("Color of the warm light cast by each torch.")]
        public Color torchLightColor = new(1f, .38f, .08f, 1f);

        [Min(0f), Tooltip("Warm point-light intensity for each torch.")]
        public float torchLightIntensity = 2.4f;

        [Min(0f), Tooltip("Warm point-light range for each torch.")]
        public float torchLightRange = 3.8f;

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
    ParticleSystem motes;
    Light doorwayLight;
    Light floorSpillLight;
    Vector3 doorwayPosition;
    float elapsed;
    bool activated;
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
        if (!activated) return;

        elapsed += Mathf.Max(0f, deltaTime);
        float fade = EvaluateFade(elapsed, settings.activation.doorwayGlowFadeInDuration);
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
        elapsed = 0f;
        activated = false;
        ignited = false;

        if (glowSurface != null) glowSurface.SetActive(false);
        SetMaterialColor(glowMaterial, Color.clear);
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
        ignited = false;
        if (glowSurface != null)
        {
            glowSurface.SetActive(true);
            SetMaterialColor(glowMaterial, settings.doorwayGlow.interiorColor * settings.doorwayGlow.interiorBrightness);
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

        glowMaterial = CreateMaterial("Room Clear Doorway Glow", "Universal Render Pipeline/Unlit");
        moteMaterial = CreateMaterial("Room Clear Motes", "Universal Render Pipeline/Particles/Unlit");
        flameMaterial = CreateMaterial("Room Clear Torch Flame", "Universal Render Pipeline/Particles/Unlit");
        torchMaterial = CreateMaterial("Room Clear Torch Fixture", "Universal Render Pipeline/Lit");
        SetMaterialColor(flameMaterial, settings.torches.flameColor);
        SetMaterialColor(torchMaterial, new Color(.16f, .1f, .06f, 1f));

        CreateGlowSurface();
        CreateLights();
        CreateMotes();
        CreateTorches();
    }

    void CreateGlowSurface()
    {
        // A thin cube is used instead of a single-sided quad so the aperture fill
        // remains visible from the angled portrait camera regardless of winding.
        glowSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glowSurface.name = "Doorway Glow Surface";
        glowSurface.transform.SetParent(runtimeRoot, false);
        glowSurface.transform.position = doorwayPosition + Vector3.back * settings.doorwayGlow.interiorDepthOffset +
            Vector3.up * settings.doorwayGlow.interiorVerticalOffset;
        glowSurface.transform.rotation = Quaternion.identity;
        glowSurface.transform.localScale = new Vector3(settings.doorwayGlow.interiorSize.x, settings.doorwayGlow.interiorSize.y, .04f);
        DisableCollider(glowSurface);
        glowRenderer = glowSurface.GetComponent<Renderer>();
        glowRenderer.allowOcclusionWhenDynamic = false;
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;
        glowRenderer.sharedMaterial = glowMaterial;
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
        objectRoot.transform.position = doorwayPosition + Vector3.back * .35f + Vector3.up * .25f;
        motes = objectRoot.AddComponent<ParticleSystem>();
        var main = motes.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = settings.motes.particleLifetime;
        main.startSpeed = settings.motes.particleSpeed;
        main.startSize = settings.motes.particleSize;
        main.startColor = GetMoteColor();
        main.maxParticles = Mathf.Max(128, settings.activation.initialParticleBurstCount +
            Mathf.CeilToInt(settings.activation.continuousParticleEmissionRate * settings.motes.particleLifetime) + 16);

        var emission = motes.emission;
        emission.enabled = true;
        emission.rateOverTime = settings.activation.continuousParticleEmissionRate;

        var shape = motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(settings.motes.spawnWidth, 1.25f, 2.25f);

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
                doorwayPosition.x + positions[i],
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
            main.startColor = settings.torches.flameColor;
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
        var glow = settings.doorwayGlow.interiorColor *
            (pulse * settings.doorwayGlow.interiorBrightness);
        SetMaterialColor(glowMaterial, glow);
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
