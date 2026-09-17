using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rebuilds the playable room from the existing environment kit with deterministic spacing.
public static class ArenaPolishEditor
{
    const string ScenePath = "Assets/Scenes/LootGoblin.unity";
    const string EnvironmentAssetPath = "Assets/Environment_Asset_Pack 1.fbx";
    const string FloorTexturePath = "Assets/Prototype/Textures/ArenaFloorStone.jpg";
    const string FloorMaterialPath = "Assets/Prototype/Materials/ArenaFloor.mat";
    const string EnvironmentRootName = "Arena Environment";
    const float FloorTop = -.02f;
    const float FloorWidth = 10.8f;
    const float FloorDepth = 15.6f;

    [MenuItem("Loot Goblin/Apply Arena Polish")]
    public static void ApplyArenaPolish()
    {
        ApplyArenaPolishInternal();
        Selection.activeGameObject = GameObject.Find(EnvironmentRootName);
    }

    public static void ApplyArenaPolishBatch()
    {
        Directory.CreateDirectory("Logs");
        ApplyArenaPolishInternal();
        File.WriteAllText("Logs/ArenaPolish.txt", "PASS Arena environment rebuilt from existing kit\n");
    }

    static void ApplyArenaPolishInternal()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var run = UnityEngine.Object.FindAnyObjectByType<LootGoblinRun>();
        if (run == null) throw new InvalidOperationException("LootGoblinRun was not found in the playable scene.");
        Material floorMaterial = BuildFloorMaterial();

        var meshes = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
        Material environmentMaterial = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(EnvironmentAssetPath))
        {
            if (asset is Mesh mesh) meshes[mesh.name] = mesh;
            else if (asset is Material material && environmentMaterial == null) environmentMaterial = material;
        }

        foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            if (filter.sharedMesh == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != EnvironmentAssetPath) continue;
            if (environmentMaterial == null) environmentMaterial = filter.GetComponent<MeshRenderer>()?.sharedMaterial;
        }
        if (environmentMaterial == null) throw new InvalidOperationException("The environment kit material could not be loaded.");

        Mesh wallMesh = RequireMesh(meshes, "wall");
        Mesh pillarMesh = RequireMesh(meshes, "stone_pillar");
        Mesh gatewayMesh = RequireMesh(meshes, "stone_gateway");
        Mesh gateMesh = RequireMesh(meshes, "stone_gate");
        Mesh obstacleMesh = RequireMesh(meshes, "stone_obstacle");

        var oldEnvironment = GameObject.Find(EnvironmentRootName);
        if (oldEnvironment != null) UnityEngine.Object.DestroyImmediate(oldEnvironment);

        var kitObjects = new List<GameObject>();
        foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            if (filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh) == EnvironmentAssetPath)
                kitObjects.Add(filter.gameObject);
        foreach (var kitObject in kitObjects) UnityEngine.Object.DestroyImmediate(kitObject);

        Transform root = run.transform;
        var oldFloor = root.Find("Stone Floor");
        if (oldFloor != null) UnityEngine.Object.DestroyImmediate(oldFloor.gameObject);

        var environment = new GameObject(EnvironmentRootName).transform;
        environment.SetParent(root, false);

        BuildFloor(environment, floorMaterial);

        var perimeter = new GameObject("Perimeter").transform;
        perimeter.SetParent(environment, false);
        BuildPerimeter(perimeter, wallMesh, pillarMesh, environmentMaterial);

        var gateAssembly = new GameObject("North Gate Assembly").transform;
        gateAssembly.SetParent(environment, false);
        CreateSizedPiece("Stone Gateway", gatewayMesh, environmentMaterial, gateAssembly,
            new Vector3(0, 0, 8.02f), 0, new Vector3(3f, 2.5f, .7f), false);
        GameObject gate = CreateSizedPiece("Stone Gate", gateMesh, environmentMaterial, gateAssembly,
            new Vector3(0, 0, 7.94f), 0, new Vector3(2.2f, 1.95f, .3f), true);

        var obstaclesRoot = new GameObject("Obstacles").transform;
        obstaclesRoot.SetParent(environment, false);
        var obstacleColliders = new List<BoxCollider>
        {
            CreateObstacle("Obstacle - North West", obstacleMesh, environmentMaterial, obstaclesRoot, new Vector3(-2.4f, 0, 3.8f), 0),
            CreateObstacle("Obstacle - South West", obstacleMesh, environmentMaterial, obstaclesRoot, new Vector3(-3.1f, 0, -3.5f), 180),
            CreateObstacle("Obstacle Cluster - Lower", obstacleMesh, environmentMaterial, obstaclesRoot, new Vector3(2f, 0, -.25f), 0),
            CreateObstacle("Obstacle Cluster - Upper Left", obstacleMesh, environmentMaterial, obstaclesRoot, new Vector3(2f, 0, 1.2f), 180),
            CreateObstacle("Obstacle Cluster - Upper Right", obstacleMesh, environmentMaterial, obstaclesRoot, new Vector3(3.45f, 0, 1.2f), 180)
        };

        var serializedRun = new SerializedObject(run);
        serializedRun.FindProperty("gate").objectReferenceValue = gate;
        var obstacleProperty = serializedRun.FindProperty("obstacleColliders");
        obstacleProperty.arraySize = obstacleColliders.Count;
        for (int i = 0; i < obstacleColliders.Count; i++)
            obstacleProperty.GetArrayElementAtIndex(i).objectReferenceValue = obstacleColliders[i];
        serializedRun.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Unity could not save the polished arena scene.");
        AssetDatabase.SaveAssets();
    }

    static Material BuildFloorMaterial()
    {
        AssetDatabase.ImportAsset(FloorTexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(FloorTexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"The arena floor texture is missing at '{FloorTexturePath}'.");
        if (!importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp ||
            importer.npotScale != TextureImporterNPOTScale.None || importer.maxTextureSize != 2048 ||
            importer.textureCompression != TextureImporterCompression.Compressed || importer.compressionQuality != 80)
        {
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 80;
            importer.SaveAndReimport();
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath);
        if (texture == null) throw new InvalidOperationException("Unity could not import the arena floor texture.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("The URP Lit shader could not be found.");
        if (material == null)
        {
            material = new Material(shader) { name = "Arena Floor" };
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
        }
        else material.shader = shader;

        float horizontalCrop = FloorWidth / FloorDepth;
        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", new Vector2(horizontalCrop, 1));
        material.SetTextureOffset("_BaseMap", new Vector2((1 - horizontalCrop) * .5f, 0));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", .18f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void BuildFloor(Transform parent, Material material)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Stone Floor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0, FloorTop, 0);
        floor.transform.localScale = new Vector3(FloorWidth / 10f, 1, FloorDepth / 10f);
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        var renderer = floor.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        GameObjectUtility.SetStaticEditorFlags(floor,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    static void BuildPerimeter(Transform parent, Mesh wallMesh, Mesh pillarMesh, Material material)
    {
        const float horizontalSpan = 12f;
        const float southZ = -8.05f;
        const float northZ = 8.05f;
        const float sideX = 5.75f;
        const float gateGap = 2.6f;

        float southLength = horizontalSpan / 6f;
        for (int i = 0; i < 6; i++)
        {
            float x = -horizontalSpan * .5f + southLength * (i + .5f);
            CreateWall($"South Wall {i + 1:D2}", wallMesh, material, parent, new Vector3(x, 0, southZ), 0, southLength);
        }

        float northLength = (horizontalSpan - gateGap) / 4f;
        for (int i = 0; i < 2; i++)
        {
            float leftX = -horizontalSpan * .5f + northLength * (i + .5f);
            float rightX = horizontalSpan * .5f - northLength * (i + .5f);
            CreateWall($"North Wall Left {i + 1:D2}", wallMesh, material, parent, new Vector3(leftX, 0, northZ), 0, northLength);
            CreateWall($"North Wall Right {i + 1:D2}", wallMesh, material, parent, new Vector3(rightX, 0, northZ), 180, northLength);
        }

        float sideLength = (northZ - southZ) / 8f;
        for (int i = 0; i < 8; i++)
        {
            float z = southZ + sideLength * (i + .5f);
            CreateWall($"West Wall {i + 1:D2}", wallMesh, material, parent, new Vector3(-sideX, 0, z), 90, sideLength);
            CreateWall($"East Wall {i + 1:D2}", wallMesh, material, parent, new Vector3(sideX, 0, z), 90, sideLength);
        }

        foreach (var corner in new[]
                 {
                     new Vector3(-sideX, 0, southZ), new Vector3(sideX, 0, southZ),
                     new Vector3(-sideX, 0, northZ), new Vector3(sideX, 0, northZ)
                 })
            CreateSizedPiece("Corner Pillar", pillarMesh, material, parent, corner, 0, new Vector3(1.35f, 1.75f, 1.35f), true);
    }

    static void CreateWall(string name, Mesh mesh, Material material, Transform parent, Vector3 center, float yaw, float length)
    {
        CreateSizedPiece(name, mesh, material, parent, center, yaw, new Vector3(length, 1.5f, .65f), true);
    }

    static BoxCollider CreateObstacle(string name, Mesh mesh, Material material, Transform parent, Vector3 center, float yaw)
    {
        return CreateSizedPiece(name, mesh, material, parent, center, yaw, new Vector3(1.45f, 1.45f, 1.45f), true)
            .GetComponent<BoxCollider>();
    }

    static GameObject CreateSizedPiece(string name, Mesh mesh, Material material, Transform parent,
        Vector3 center, float yaw, Vector3 worldSize, bool addCollider)
    {
        var piece = CreateMeshObject(name, mesh, material, parent);
        Vector3 size = mesh.bounds.size;
        Vector3 scale = new(
            worldSize.x / Mathf.Max(.001f, size.x),
            worldSize.y / Mathf.Max(.001f, size.y),
            worldSize.z / Mathf.Max(.001f, size.z));
        Quaternion rotation = Quaternion.Euler(0, yaw, 0);
        piece.transform.localScale = scale;
        piece.transform.localRotation = rotation;
        Vector3 scaledCenter = rotation * Vector3.Scale(mesh.bounds.center, scale);
        piece.transform.localPosition = new Vector3(
            center.x - scaledCenter.x,
            FloorTop - mesh.bounds.min.y * scale.y,
            center.z - scaledCenter.z);

        if (addCollider)
        {
            var collider = piece.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }
        return piece;
    }

    static GameObject CreateMeshObject(string name, Mesh mesh, Material material, Transform parent)
    {
        var piece = new GameObject(name);
        piece.transform.SetParent(parent, false);
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;
        piece.AddComponent<MeshRenderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(piece,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        return piece;
    }

    static Mesh RequireMesh(Dictionary<string, Mesh> meshes, string name)
    {
        if (meshes.TryGetValue(name, out Mesh mesh)) return mesh;
        throw new InvalidOperationException($"The environment kit is missing the '{name}' mesh.");
    }
}
