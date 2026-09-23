using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Small, project-local builder and bounded Play Mode smoke test. No external bridge required.
[InitializeOnLoad]
public static class LootGoblinPrototypeEditor
{
    const string ScenePath="Assets/Scenes/LootGoblin.unity";
    const string Request="LootGoblin.validate";
    const string SwordAttackRequest="LootGoblin.swordattack";
    const string DepthRequest="LootGoblin.depthvalidate";
    const string CameraRequest="LootGoblin.cameravalidate";
    const string Pending="LootGoblin.SmokePending";
    static readonly List<string> checks=new();
    static string DepthRequestPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), DepthRequest);
    static string CameraRequestPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), CameraRequest);
    static double readyAt;
    static int batchExitCode=-1;
    static LootGoblinPrototypeEditor() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(batchExitCode>=0 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            int exitCode=batchExitCode; batchExitCode=-1; EditorApplication.Exit(exitCode); return;
        }
        if(SessionState.GetBool(Pending,false) && EditorApplication.isPlaying && !EditorApplication.isPaused)
        {
            var run=UnityEngine.Object.FindAnyObjectByType<LootGoblinRun>();
            if(run==null) return;
            if(run.Room==0) run.StartNewRun();
            if(readyAt==0) { readyAt=EditorApplication.timeSinceStartup+1; return; }
            if(EditorApplication.timeSinceStartup<readyAt) return;
            readyAt=0; SessionState.SetBool(Pending,false);
            bool depthValidation=File.Exists(DepthRequestPath);
            bool cameraValidation=File.Exists(CameraRequestPath);
            try
            {
                if(depthValidation) DepthSmoke(run);
                else if(cameraValidation) CameraSmoke(run);
                else Smoke(run);
                if(Application.isBatchMode) batchExitCode=0;
            }
            catch(Exception e)
            {
                string logPath=cameraValidation ? "Logs/PortraitCameraValidation.txt" : "Logs/LootGoblinValidation.txt";
                File.WriteAllText(logPath,"FAIL\n"+string.Join("\n",checks)+"\n"+e);
                Debug.LogException(e);
                if(Application.isBatchMode) batchExitCode=1;
            }
            finally
            {
                if(File.Exists(DepthRequestPath)) File.Delete(DepthRequestPath);
                if(File.Exists(CameraRequestPath)) File.Delete(CameraRequestPath);
                EditorApplication.isPlaying=false;
            }
            return;
        }
        if(File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Request);
            try { Build(); SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true; }
            catch(Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/LootGoblinValidation.txt","BUILD FAILED\n"+e); Debug.LogException(e); }
        }
        if(File.Exists(DepthRequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            try { Directory.CreateDirectory("Logs"); EditorSceneManager.OpenScene(ScenePath); SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true; }
            catch(Exception e) { File.WriteAllText("Logs/LootGoblinValidation.txt","DEPTH VALIDATION FAILED\n"+e); Debug.LogException(e); }
        }
        if(File.Exists(CameraRequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            try { Directory.CreateDirectory("Logs"); EditorSceneManager.OpenScene(ScenePath); SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true; }
            catch(Exception e) { File.WriteAllText("Logs/LootGoblinValidation.txt","CAMERA VALIDATION FAILED\n"+e); Debug.LogException(e); }
        }
        if(File.Exists(SwordAttackRequest))
        {
            // This request is deliberately file-driven so the open Unity editor can run the
            // narrowly scoped scene setup after source import. Never interrupt an active run.
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(SwordAttackRequest);
            try
            {
                GoblinSwordAttackSetup.Setup();
                SessionState.SetBool(Pending,true);
                EditorApplication.isPlaying=true;
            }
            catch(Exception e)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/GoblinSwordSetup.txt", e.ToString());
                Debug.LogException(e);
            }
        }
        if(File.Exists("LootGoblin.slime") && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete("LootGoblin.slime");
            try { CreateSlime(); File.WriteAllText("Logs/SlimeSetup.txt", "PASS Slime prefab created"); }
            catch(Exception e) { File.WriteAllText("Logs/SlimeSetup.txt", e.ToString()); Debug.LogException(e); }
        }
    }
    [MenuItem("Loot Goblin/Create Slime Prefab")]
    public static void CreateSlime()
    {
        const string path = "Assets/Prototype/Resources/Slime.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        if(!AssetDatabase.IsValidFolder("Assets/Prototype/Resources")) AssetDatabase.CreateFolder("Assets/Prototype", "Resources");
        Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            AssetDatabase.CreateAsset(material, "Assets/Prototype/Materials/"+name+".mat");
            return material;
        }
        var bodyMaterial = Material("Slime", new Color(.08f,.85f,1f));
        var eyeMaterial = Material("SlimeEyes", new Color(.025f,.035f,.08f));
        var root = new GameObject("Slime");
        try
        {
            var visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
            void Sphere(string name, Vector3 position, Vector3 scale, Material material)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Sphere); part.name = name;
                part.transform.SetParent(visual, false); part.transform.localPosition = position; part.transform.localScale = scale;
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = material;
            }
            Sphere("Body", new Vector3(0,.36f,0), new Vector3(.95f,.72f,.85f), bodyMaterial);
            Sphere("Left Eye", new Vector3(-.18f,.47f,.37f), new Vector3(.115f,.16f,.085f), eyeMaterial);
            Sphere("Right Eye", new Vector3(.18f,.47f,.37f), new Vector3(.115f,.16f,.085f), eyeMaterial);
            var motion = root.AddComponent<SlimeMotion>();
            var data = new SerializedObject(motion); data.FindProperty("visual").objectReferenceValue = visual; data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Loot Goblin/Open Playable Scene")]
    public static void Open()
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }
    [MenuItem("Loot Goblin/Run Smoke Test")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath); SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }
    public static void ValidateBatch()
    {
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene(ScenePath); SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }
    public static void ValidateDepthBatch()
    {
        File.WriteAllText(DepthRequestPath,string.Empty);
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Pending,true);
        EditorApplication.isPlaying=true;
    }
    public static void ValidateCameraBatch()
    {
        File.WriteAllText(CameraRequestPath,string.Empty);
    }
    static void Build()
    {
        Directory.CreateDirectory("Logs");
        if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Existing scene has unsaved edits; save it before running builder.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("LOOT GOBLIN - Playable Prototype");
        Undo.RegisterCreatedObjectUndo(root,"Create Loot Goblin prototype");
        var run=root.AddComponent<LootGoblinRun>();
        run.BuildArena(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
        if(!AssetDatabase.IsValidFolder("Assets/Prototype/Materials")) AssetDatabase.CreateFolder("Assets/Prototype","Materials");
        var mats=new HashSet<Material>();
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) mats.Add(renderer.sharedMaterial);
        var serialized=new SerializedObject(run);
        mats.Add((Material)serialized.FindProperty("enemyMaterial").objectReferenceValue);
        mats.Add((Material)serialized.FindProperty("lootMaterial").objectReferenceValue);
        foreach(var mat in mats) AssetDatabase.CreateAsset(mat,"Assets/Prototype/Materials/"+mat.name+".mat");
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=new List<EditorBuildSettingsScene> { new(ScenePath,true) };
        foreach(var old in EditorBuildSettings.scenes) if(old.path!=ScenePath) scenes.Add(new EditorBuildSettingsScene(old.path,false));
        EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets();
        Selection.activeGameObject=root;
    }
    static void Check(bool pass,string label)
    {
        if(!pass) throw new Exception(label);
        checks.Add("PASS "+label);
    }
    static void Steps(LootGoblinRun run,Vector2 input,int count)
    {
        for(int i=0;i<count;i++)
        {
            run.Tick(input,1f/60);
            run.PortraitCamera?.TickForValidation(1f/60);
        }
    }
    static void KeepSmokeRunAlive(LootGoblinRun run)
    {
        // The room-flow fixture represents a successful, dodging player after health behavior is tested below.
        if (run.Health.Current <= 25) run.Health.ResetHealth();
    }
    static void FightMixedEncounter(LootGoblinRun run)
    {
        // Room-flow fixture closes distance now that ranged enemies deliberately
        // stay outside sword reach. Damage still comes from the real sword Tick.
        KeepSmokeRunAlive(run);
        if(run.EnemyCount>0)
        {
            Vector3 position=run.FirstEnemyPosition+Vector3.back*1.1f;
            position.y=.65f; run.Player.position=position;
        }
        run.Tick(Vector2.zero,1f/60);
    }
    static void CollectRoomLoot(LootGoblinRun run)
    {
        foreach(Transform child in run.transform)
        {
            if(child.name!="Loot" || !child.gameObject.activeSelf) continue;
            Vector3 position=child.position; position.y=.65f; run.Player.position=position;
            Steps(run,Vector2.zero,90);
        }
        Bounds bounds=run.ArenaBounds;
        run.Player.position=new Vector3(bounds.center.x,.65f,bounds.min.z+1.1f);
    }
    static void Capture(LootGoblinRun run,string filename)
    {
        var cam=run.ArenaCamera; var old=cam.targetTexture;
        var rt=new RenderTexture(540,960,24); cam.targetTexture=rt;
        run.PortraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        run.PortraitCamera.TickForValidation(0f);
        cam.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
        var tex=new Texture2D(540,960,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,540,960),0,0); tex.Apply();
        File.WriteAllBytes(filename,tex.EncodeToPNG());
        RenderTexture.active=previous; cam.targetTexture=old; rt.Release();
        UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); run.PortraitCamera.RefreshViewport();
    }
    static void Smoke(LootGoblinRun run)
    {
        checks.Clear();
        Check(EditorSceneManager.GetActiveScene().path==ScenePath,"Correct playable scene, runtime initialized");
        CheckArenaEnvironment(run);
        CheckEquipmentV1(run);
        CheckSlime(run);
        {
            run.Player.position=run.FirstEnemyPosition+Vector3.back*1.7f+Vector3.right;
            Steps(run,Vector2.zero,20);
            bool capturedLunge=false;
            bool runtimeHeadMoved=false;
            foreach(var slime in run.GetComponentsInChildren<SlimeMotion>())
            {
                capturedLunge|=slime.IsLunging;
                if(slime.IsLunging) runtimeHeadMoved|=Vector3.Distance(slime.transform.position,slime.AttackFrontPosition)>.5f;
            }
            Check(capturedLunge,"Runtime slime reaches the committed stretch-lunge state");
            Check(runtimeHeadMoved,"Runtime lunge head travels while its trailing root stays anchored");
            run.Player.position+=Vector3.right*1.5f;
            Capture(run,"Logs/SlimeLungeValidation.png");
        }
        CheckRunGameplay(run);
        File.WriteAllText("Logs/LootGoblinValidation.txt",string.Join("\n",checks)+"\nALL PASSED");
        Debug.Log("Loot Goblin: ALL SMOKE CHECKS PASSED. Logs/LootGoblinValidation.txt");
    }
    static void DepthSmoke(LootGoblinRun run)
    {
        const string saveKey="LootGoblin.SaveData";
        bool hadSave=PlayerPrefs.HasKey(saveKey);
        string originalSave=hadSave ? PlayerPrefs.GetString(saveKey) : null;
        try
        {
            checks.Clear();
            Check(EditorSceneManager.GetActiveScene().path==ScenePath,"Correct playable scene, runtime initialized");
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            var freshSave=LootGoblinSave.Load();
            Check(freshSave.version==LootGoblinSave.CurrentVersion && freshSave.bankedGold==0,
                "Missing save loads a versioned 0-gold bank");
            if(hadSave) PlayerPrefs.SetString(saveKey,originalSave);
            else PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            CheckDepthStructure(run);
            File.WriteAllText("Logs/LootGoblinValidation.txt",string.Join("\n",checks)+"\nALL PASSED");
            Debug.Log("Loot Goblin: DEPTH SMOKE PASSED. Logs/LootGoblinValidation.txt");
        }
        finally
        {
            if(hadSave) PlayerPrefs.SetString(saveKey,originalSave);
            else PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
        }
    }
    static void CameraSmoke(LootGoblinRun run)
    {
        checks.Clear();
        Check(EditorSceneManager.GetActiveScene().path==ScenePath,"Correct playable scene, runtime initialized");
        CheckArenaEnvironment(run);
        bool canCapture=!Application.isBatchMode || SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null;
        CheckPortraitCamera(run,canCapture);
        File.WriteAllText("Logs/PortraitCameraValidation.txt",string.Join("\n",checks)+"\nALL PASSED");
        Debug.Log("Loot Goblin: PORTRAIT CAMERA SMOKE PASSED. Logs/PortraitCameraValidation.txt");
    }
    static void CheckRunGameplay(LootGoblinRun run)
    {
        run.Restart();
        Capture(run,"Logs/LootGoblinLockedDoorway.png");
        CheckPortraitCamera(run,true);
        CheckCombatFeedback(run);
        Check(run.Health.Current==run.Health.Max,"Player starts each run at full health");
        run.Player.position=run.FirstEnemyPosition+Vector3.back*.8f;
        Steps(run,Vector2.zero,36);
        Check(run.Health.Current==75,"Slime lunge damages player once");
        Check(!run.Health.TryTakeDamage(25) && run.Health.Current==75,"Player invulnerability ignores immediate repeat damage");
        run.Restart();
        while(!run.Health.TryTakeDamage(25)) run.Health.Tick(.75f);
        for(int i=0;i<3;i++) { run.Health.Tick(.75f); Check(run.Health.TryTakeDamage(25),"Player takes damage after invulnerability expires"); }
        run.Tick(Vector2.zero,1f/60);
        Check(run.Failed && run.Health.Current==0,"Zero health fails the run");
        var failedPosition=run.Player.position; Steps(run,Vector2.up,60);
        Check(run.Player.position==failedPosition,"Run failure freezes normal gameplay");
        run.Restart();
        Check(!run.Failed && run.Health.Current==run.Health.Max,"Restart restores full health and resumes gameplay");
        Capture(run,"Logs/LootGoblinPortrait.png");
        Rect joystickZone=FloatingJoystick.CalculateActivationZone(540,960,new Rect(0,0,540,960));
        Check(joystickZone.Contains(new Vector2(120,300)) && !joystickZone.Contains(new Vector2(420,300)) && !joystickZone.Contains(new Vector2(120,800)),"Floating joystick reserves a responsive lower-left movement zone");
        Check(FloatingJoystick.CalculateValue(Vector2.zero,new Vector2(6,0),100,14)==Vector2.zero,"Floating joystick dead zone suppresses small movement");
        Vector2 clampedJoystick=FloatingJoystick.CalculateValue(Vector2.zero,new Vector2(240,0),100,14);
        Check(clampedJoystick.x>.99f && Mathf.Abs(clampedJoystick.y)<.001f,"Floating joystick keeps movement when dragged past its radius");
        if(Application.isBatchMode)
        {
            var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var move=input.FindAction("Player/Move",true);
            bool hasW=false,hasUpArrow=false;
            foreach(var binding in move.bindings)
            {
                hasW|=binding.effectivePath=="<Keyboard>/w";
                hasUpArrow|=binding.effectivePath=="<Keyboard>/upArrow";
            }
            Check(hasW,"W keyboard binding is configured");
            Check(hasUpArrow,"Arrow keyboard binding is configured");
        }
        else
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); InputSystem.Update();
                Check(run.ReadMovement().y>.9f,"W keyboard binding produces movement");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow)); InputSystem.Update();
                Check(run.ReadMovement().y>.9f,"Arrow keyboard binding produces movement");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
                Check(run.ReadMovement().sqrMagnitude<.001f,"Keyboard release stops input");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        var start=run.Player.position; Steps(run,Vector2.up,60);
        Check(run.Player.position.z>start.z+4 && Vector3.Dot(run.Player.forward,Vector3.forward)>.99f,"Player moves and faces movement");
        var stopped=run.Player.position; Steps(run,Vector2.zero,1);
        Check(run.Player.position==stopped,"Player stops immediately");
        Steps(run,Vector2.right,400); Check(run.Player.position.x<=run.ArenaBounds.max.x-.399f,"Room boundary contains player");
        run.Restart();
        Bounds obstacle=run.ObstacleColliders[1].bounds;
        run.Player.position=new Vector3(obstacle.center.x,.65f,obstacle.min.z-.41f); Steps(run,Vector2.up,60);
        Check(run.Player.position.z<=obstacle.min.z-.399f,"Visible obstacle collider blocks player");
        run.Restart(); run.Player.position=run.FirstEnemyPosition+Vector3.back*1.2f;
        int hits=run.Hits; Steps(run,Vector2.right,20);
        Check(run.Hits==hits,"No attack while moving with enemy in range");
        run.Restart();
        CheckRoomLoop(run);
    }
    static void CheckPortraitCamera(LootGoblinRun run,bool capture)
    {
        run.Restart();
        if(capture) Capture(run,"Logs/LootGoblinPortrait.png");
        var portraitCamera=run.PortraitCamera;
        Check(portraitCamera!=null,"Portrait Room Camera is wired to the gameplay camera");
        float fixedOrthographicSize=portraitCamera.OrthographicSize;
        foreach(var size in new[]{new Vector2Int(540,960),new Vector2Int(540,1170),new Vector2Int(540,1200)})
        {
            // Simulate top and bottom phone insets. They may constrain HUD placement, but
            // must never create empty camera bands or reduce the playable render surface.
            Rect safeArea=new Rect(0,34,size.x,size.y-102);
            portraitCamera.RefreshViewport(size.x,size.y,safeArea);
            Check(run.ArenaCamera.rect==new Rect(0,0,1,1),"Portrait camera remains full-bleed despite safe-area HUD insets at "+size.x+"x"+size.y);
            Check(Mathf.Abs(run.ArenaCamera.orthographicSize-fixedOrthographicSize)<.0001f,"Portrait camera keeps fixed orthographic size at "+size.x+"x"+size.y);
            Bounds bounds=run.ArenaBounds;
            Check(portraitCamera.TryGetCameraClampBounds(out Bounds cameraClampBounds),"Camera has a dedicated wall-reveal clamp frame");
            Check(portraitCamera.TryGetGroundFootprint(out Bounds footprint) &&
                  footprint.min.x>=cameraClampBounds.min.x-.01f && footprint.max.x<=cameraClampBounds.max.x+.01f &&
                  footprint.min.z>=cameraClampBounds.min.z-.01f && footprint.max.z<=cameraClampBounds.max.z+.01f,
                  "Camera footprint remains inside its camera-only reveal frame at "+size.x+"x"+size.y);
        }
        portraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        Check(run.ArenaCamera.orthographic,"Orthographic camera");
        Bounds cameraBounds=run.ArenaBounds;
        Check(Mathf.Abs(portraitCamera.HorizontalWallReveal-.6f)<.001f &&
              Mathf.Abs(portraitCamera.VerticalWallReveal-.35f)<.001f &&
              Mathf.Abs(portraitCamera.NorthVisualReveal-.75f)<.001f,
            "Persistent horizontal, vertical, and north wall-reveal margins are configured");
        Check(portraitCamera.TryGetCameraClampBounds(out Bounds revealBounds) &&
              Mathf.Abs(revealBounds.min.x-(cameraBounds.min.x-.6f))<.001f && Mathf.Abs(revealBounds.max.x-(cameraBounds.max.x+.6f))<.001f &&
              Mathf.Abs(revealBounds.min.z-(cameraBounds.min.z-.35f))<.001f &&
              Mathf.Abs(revealBounds.max.z-(cameraBounds.max.z+.35f+.75f))<.001f,
              "Camera-only reveal frame expands without changing playable room bounds");
        run.Player.position=new Vector3(cameraBounds.center.x,.65f,cameraBounds.center.z-2f);
        portraitCamera.SnapToTarget();
        Vector3 deadZoneCameraPosition=run.ArenaCamera.transform.position;
        run.Player.position+=Vector3.forward*.5f;
        portraitCamera.TickForValidation(1f/60);
        Check(Vector3.Distance(run.ArenaCamera.transform.position,deadZoneCameraPosition)<.001f,"Camera ignores movement inside the vertical dead zone");
        run.Player.position+=Vector3.forward*2f;
        for(int i=0;i<30;i++) portraitCamera.TickForValidation(1f/60);
        Check(run.ArenaCamera.transform.position.z>deadZoneCameraPosition.z+.1f,"Camera follows once the player crosses the vertical dead zone");
        bool followFootprintInside=portraitCamera.TryGetGroundFootprint(out Bounds followedFootprint) &&
              followedFootprint.min.x>=revealBounds.min.x-.01f && followedFootprint.max.x<=revealBounds.max.x+.01f &&
              followedFootprint.min.z>=revealBounds.min.z-.01f && followedFootprint.max.z<=revealBounds.max.z+.01f;
        Check(followFootprintInside,"Follow camera remains inside its camera-only reveal frame; footprint="+followedFootprint.min+".."+followedFootprint.max+", reveal="+revealBounds.min+".."+revealBounds.max);
        run.Player.position=new Vector3(cameraBounds.min.x+.4f,.65f,cameraBounds.center.z); portraitCamera.SnapToTarget();
        Check(portraitCamera.TryGetGroundFootprint(out Bounds westFootprint) && westFootprint.min.x<cameraBounds.min.x && westFootprint.min.x>=revealBounds.min.x-.01f,
            "West clamp reveals the perimeter wall without exceeding the reveal frame");
        if(capture) Capture(run,"Logs/PortraitCameraWest.png");
        portraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        run.Player.position=new Vector3(cameraBounds.max.x-.4f,.65f,cameraBounds.center.z); portraitCamera.SnapToTarget();
        Check(portraitCamera.TryGetGroundFootprint(out Bounds eastFootprint) && eastFootprint.max.x>cameraBounds.max.x && eastFootprint.max.x<=revealBounds.max.x+.01f,
            "East clamp reveals the perimeter wall without exceeding the reveal frame");
        if(capture) Capture(run,"Logs/PortraitCameraEast.png");
        portraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        run.Restart(); portraitCamera.SnapToTarget();
        Check(portraitCamera.TryGetGroundFootprint(out Bounds southFootprint) && southFootprint.min.z<cameraBounds.min.z && southFootprint.min.z>=revealBounds.min.z-.01f,
            "South clamp reveals the perimeter wall without exceeding the reveal frame");
        if(capture) Capture(run,"Logs/PortraitCameraSouth.png");
        portraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        run.Player.position=new Vector3(cameraBounds.center.x,.65f,cameraBounds.center.z); portraitCamera.SnapToTarget();
        Check(portraitCamera.TryGetGroundFootprint(out Bounds middleFootprint) && middleFootprint.min.z>cameraBounds.min.z+.2f && middleFootprint.max.z<cameraBounds.max.z-.2f,"Camera scrolls through the unclamped room middle");
        if(capture) Capture(run,"Logs/PortraitCameraMiddle.png");
        portraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        // Place the player at the playable north edge so the capture exercises the
        // north-only visual allowance at its actual clamp.
        run.Player.position=new Vector3(cameraBounds.center.x,.65f,cameraBounds.max.z-.4f); portraitCamera.SnapToTarget();
        Check(portraitCamera.TryGetGroundFootprint(out Bounds northFootprint) && northFootprint.max.z>cameraBounds.max.z && northFootprint.max.z<=revealBounds.max.z+.01f,
            "North clamp reveals the gate presentation without exceeding the reveal frame");
        if(capture) Capture(run,"Logs/PortraitCameraNorth.png");
        portraitCamera.RefreshViewport(540,960,new Rect(0,0,540,960));
        run.Restart();
        CheckNorthDoorwayTransition(run);
    }
    static void CheckNorthDoorwayTransition(LootGoblinRun run)
    {
        int guard=0;
        while(!run.ExitOpen && guard++<7200) FightMixedEncounter(run);
        Check(run.ExitOpen,"Clearing the room opens the north doorway for the camera reset check");
        Bounds bounds=run.ArenaBounds;
        run.Player.position=new Vector3(bounds.center.x,.65f,bounds.max.z-1.8f);
        run.Tick(Vector2.zero,1f/60);
        Check(run.Room==2 && !run.DepthComplete,"North doorway transition begins the next room");
        Check(run.PortraitCamera.TryGetGroundFootprint(out Bounds resetFootprint) && resetFootprint.min.z<bounds.min.z,
            "Room reset snaps the portrait camera back to the south wall reveal clamp");
        run.Restart();
    }
    static void CheckRoomLoop(LootGoblinRun run)
    {
        int startingBankedGold=run.BankedGold;
        int total=0;
        for(int room=1;room<=10;room++)
        {
            Check(run.Room==room && run.EnemyCount==Mathf.Min(room+1,8) && !run.ExitOpen && run.GateActive,"Room "+room+" spawns and closes exit");
            var enemyStart=run.FirstEnemyPosition; Steps(run,Vector2.zero,30);
            Check(run.FirstEnemyPosition!=enemyStart,"Enemies approach player in room "+room);
            int count=run.EnemyCount; int guard=0;
            while(run.EnemyCount==count && guard++<7200) FightMixedEncounter(run);
            Check(run.EnemyCount<count && run.PickupCount>0,"Attack damages, kills and drops visible loot in room "+room);
            guard=0;
            while(!run.ExitOpen && guard++<7200) FightMixedEncounter(run);
            bool gateOpened=run.ExitOpen && !run.GateActive;
            string remaining="";
            if(!gateOpened)
                foreach(var slime in run.GetComponentsInChildren<SlimeMotion>())
                    if(slime.gameObject.activeInHierarchy) remaining+=$" {slime.transform.position}";
            Check(gateOpened,"All enemies die and the visible gate opens in room "+room+(gateOpened?"":"; remaining:"+remaining));
            CollectRoomLoot(run);
            total+=Mathf.Min(room+1,8);
            Check(run.Loot==total,"Magnetic pickups collect and count in room "+room);
            int healthBeforeTransition=run.Health.Current;
            if(room==1 && healthBeforeTransition==run.Health.Max)
            {
                run.Health.Tick(1f);
                Check(run.Health.TryTakeDamage(25),"Controlled damage prepares the room-transition health check");
                healthBeforeTransition=run.Health.Current;
            }
            guard=0;
            while(run.Room==room && !run.DepthComplete && guard++<400) run.Tick(Vector2.up,1f/60);
            if(room%5==0)
            {
                Check(run.DepthComplete && run.Depth==room/5 && run.Room==room,"Depth "+(room/5)+" pauses after room "+room);
                var pausedPosition=run.Player.position;
                Steps(run,Vector2.up,60);
                Check(run.Player.position==pausedPosition,"Depth choice freezes normal gameplay");
                if(room==5)
                {
                    int healthBeforeDeeper=run.Health.Current;
                    run.GoDeeper();
                    Check(!run.DepthComplete && run.Room==6 && run.Depth==2,"Go Deeper starts room 6 in Depth 2");
                    Check(run.Health.Current==healthBeforeDeeper,"Health persists when going deeper");
                }
                else Check(run.DepthComplete,"Final depth remains ready for the Cash Out persistence check");
            }
            else Check(run.Room==room+1,"Exit advances room "+room);
            if(room==1) Check(run.Health.Current==healthBeforeTransition && run.Health.Current<run.Health.Max,"Health persists through normal room transitions");
        }
        var equipment = run.Equipment;
        OwnedEquipmentItem cashOutSword = null;
        foreach (var item in equipment.OwnedItems)
            if (item.inRunBag && item.definitionId == "iron-sword") cashOutSword = item;
        Check(cashOutSword != null, "Run combat produces an at-risk Sword for the Cash Out persistence check");
        OwnedEquipmentItem cashOutReplacedSword = equipment.GetEquipped(EquipmentSlot.Weapon);
        equipment.Equip(cashOutSword.instanceId);
        string cashOutEquippedId = cashOutSword.instanceId;
        string cashOutHeadId = equipment.GetEquipped(EquipmentSlot.Head).instanceId;
        string cashOutChestId = equipment.GetEquipped(EquipmentSlot.Chest).instanceId;

        int expectedBankedGold=startingBankedGold+total;
        run.CashOut();
        Check(run.IsMainMenu && run.BankedGold==expectedBankedGold && run.CarriedGold==0 &&
              LootGoblinSave.Load().bankedGold==expectedBankedGold,
            "Cash Out banks carried gold once, persists it, clears it, and returns to the main menu");
        var cashOutSave = LootGoblinSave.Load();
        bool cashOutWeaponSurvived = false;
        bool cashOutHeadSurvived = false;
        bool cashOutChestSurvived = false;
        bool cashOutUnequippedWeaponConverted = true;
        foreach (var item in cashOutSave.ownedEquipment)
        {
            if (item.instanceId == cashOutEquippedId && item.equipped) cashOutWeaponSurvived = true;
            if (item.instanceId == cashOutHeadId && item.equipped) cashOutHeadSurvived = true;
            if (item.instanceId == cashOutChestId && item.equipped) cashOutChestSurvived = true;
            if (item.instanceId == cashOutReplacedSword.instanceId) cashOutUnequippedWeaponConverted = false;
        }
        Check(cashOutWeaponSurvived && cashOutHeadSurvived && cashOutChestSurvived && cashOutUnequippedWeaponConverted,
            "Cash Out retains the exact equipped Weapon, Head, and Chest while converting the swapped-out bag Sword");
        equipment.ReloadFromPersistence();
        Check(equipment.GetEquipped(EquipmentSlot.Weapon)?.instanceId == cashOutEquippedId,
            "Reload retains the Sword equipped at Cash Out");
        var end=run.Player.position; Steps(run,Vector2.down,60);
        Check(run.Player.position==end,"Main menu freezes gameplay");
        run.Restart(); Check(run.Room==1 && run.Loot==0 && run.EnemyCount==2 && !run.Complete,"Restart resets run");
    }
    static void CheckDepthStructure(LootGoblinRun run)
    {
        var serializedRun=new SerializedObject(run);
        var interiorColor=serializedRun.FindProperty("roomClearVfx.doorwayGlow.interiorColor");
        var interiorSize=serializedRun.FindProperty("roomClearVfx.doorwayGlow.interiorSize");
        var interiorDepth=serializedRun.FindProperty("roomClearVfx.doorwayGlow.interiorDepthOffset");
        var passageDepth=serializedRun.FindProperty("roomClearVfx.doorwayGlow.passageDepth");
        var doorwayIntensity=serializedRun.FindProperty("roomClearVfx.doorwayGlow.glowLightIntensity");
        Check(interiorColor!=null && interiorSize!=null && interiorDepth!=null && passageDepth!=null && doorwayIntensity!=null &&
              interiorColor.colorValue.g<.2f && interiorColor.colorValue.r>0f && interiorSize.vector2Value.x>1f &&
              interiorDepth.floatValue>.1f && passageDepth.floatValue>.5f && doorwayIntensity.floatValue>0f,
              "Room-clear passage settings persist on the scene component");
        run.Restart();
        Capture(run,"Logs/LootGoblinLockedDoorway.png");
        int bankedBeforeFailure=run.BankedGold;
        int guard=0;
        while(!run.ExitOpen && guard++<7200) FightMixedEncounter(run);
        CollectRoomLoot(run);
        Check(run.CarriedGold>0,"Enemy gold pickups increase carried gold");
        for(int hit=0;hit<4;hit++) { while(!run.Health.TryTakeDamage(25)) run.Health.Tick(.75f); }
        run.Tick(Vector2.zero,1f/60);
        Check(run.Failed && run.Health.Current==0 && run.CarriedGold==0 && run.BankedGold==bankedBeforeFailure,
            "Failed run discards carried gold without changing banked gold");
        run.Restart();

        int startingBankedGold=run.BankedGold;
        int totalLoot=0;
        for(int room=1;room<=10;room++)
        {
            Check(run.Room==room && run.Depth==((room-1)/5)+1 && run.EnemyCount==Mathf.Min(room+1,8),"Room and depth numbering are correct in room "+room);
            guard=0;
            while(!run.ExitOpen && guard++<14400) FightMixedEncounter(run);
            Check(run.ExitOpen,"Combat clears room "+room+" and opens the gate");
            if(room==1)
            {
                var doorwaySurface=run.transform.Find("Room Clear VFX/Doorway Glow Surface");
                Check(doorwaySurface!=null && doorwaySurface.gameObject.activeInHierarchy,"Doorway interior surface activates with the cleared room");
                var passageFloor=run.transform.Find("Room Clear VFX/Passage Floor");
                var passageLeft=run.transform.Find("Room Clear VFX/Passage Left Wall");
                Check(passageFloor!=null && passageFloor.gameObject.activeInHierarchy && passageLeft!=null && passageLeft.gameObject.activeInHierarchy,
                      "Doorway passage depth geometry activates with the cleared room");
                Steps(run,Vector2.zero,90);
                if(doorwaySurface!=null)
                    Check(doorwaySurface.GetComponent<Collider>()==null,"Doorway interior surface has no gameplay collider");
                Capture(run,"Logs/LootGoblinRoomClearFinal.png");
            }
            CollectRoomLoot(run);
            totalLoot+=Mathf.Min(room+1,8);
            Check(run.Loot==totalLoot,"Loot persists through room "+room);

            if(room==5)
            {
                while(!run.Health.TryTakeDamage(25)) run.Health.Tick(.75f);
            }
            int healthBeforeTransition=run.Health.Current;
            guard=0;
            while(run.Room==room && !run.DepthComplete && guard++<400) run.Tick(Vector2.up,1f/60);
            if(room%5!=0)
            {
                Check(run.Room==room+1 && !run.DepthComplete,"Ordinary room "+room+" advances without a decision");
                if(room==1) Capture(run,"Logs/LootGoblinNextRoomLocked.png");
                continue;
            }

            Check(run.DepthComplete && run.Room==room && run.Depth==room/5,"Depth "+(room/5)+" waits for the player's choice");
            var pausedPosition=run.Player.position;
            Steps(run,Vector2.up,60);
            Check(run.Player.position==pausedPosition,"Depth choice blocks repeated movement input");
            if(room==5)
            {
                run.GoDeeper();
                Check(run.Room==6 && run.Depth==2 && !run.DepthComplete,"Go Deeper starts room 6");
                Check(run.Health.Current==healthBeforeTransition,"Go Deeper preserves player health");
            }
            else run.CashOut();
        }
        int expectedBankedGold=startingBankedGold+totalLoot;
        run.CashOut();
        Check(run.IsMainMenu && run.BankedGold==expectedBankedGold && run.CarriedGold==0 &&
              LootGoblinSave.Load().bankedGold==expectedBankedGold,
            "Cash Out persists all carried gold and returns to the main menu");
        var endPosition=run.Player.position;
        Steps(run,Vector2.down,60);
        Check(run.Player.position==endPosition,"Main menu freezes gameplay after Cash Out");
        run.Restart();
        Check(run.Room==1 && !run.Complete && !run.DepthComplete && run.Loot==0,"Restart clears the depth result state");
    }
    static void CheckArenaEnvironment(LootGoblinRun run)
    {
        var environment=GameObject.Find("Arena Environment");
        Check(environment!=null,"Polished arena environment hierarchy exists");
        int floorTiles=0, floorRenderers=0, wallColliders=0;
        MeshRenderer floorRenderer=null;
        foreach(var filter in environment.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.name.StartsWith("Floor Tile",StringComparison.Ordinal)) floorTiles++;
            if(filter.name=="Stone Floor") { floorRenderers++; floorRenderer=filter.GetComponent<MeshRenderer>(); }
            if(filter.name.Contains("Wall",StringComparison.Ordinal) && filter.GetComponent<BoxCollider>()!=null) wallColliders++;
        }
        Check(floorTiles==0,"Stone floor has no repeated tile objects");
        Check(floorRenderers==1 && floorRenderer!=null,"Arena uses one continuous stone floor renderer");
        Check(Mathf.Abs(floorRenderer.bounds.size.x-10.8f)<.01f && Mathf.Abs(floorRenderer.bounds.size.z-22f)<.01f,
            "Stone floor uses the playtested 10.8 x 22 scrolling-camera prototype arena");
        var floorMaterial=floorRenderer.sharedMaterial;
        Check(floorMaterial!=null && floorMaterial.GetTexture("_BaseMap")!=null && floorMaterial.GetTexture("_BaseMap").name=="ArenaFloorStone",
            "Stone floor uses the supplied arena texture");
        Vector2 floorTextureScale=floorMaterial.GetTextureScale("_BaseMap");
        Vector2 floorTextureOffset=floorMaterial.GetTextureOffset("_BaseMap");
        Check(Mathf.Abs(floorTextureScale.x-10.8f/22f)<.001f && Mathf.Abs(floorTextureScale.y-1)<.001f &&
              Mathf.Abs(floorTextureOffset.x-(1-10.8f/22f)*.5f)<.001f,
            "Stone texture is center-cropped without stretching");
        Check(wallColliders==34,"Perimeter uses 34 aligned wall pieces with colliders");
        var authoredBounds=environment.GetComponentInChildren<RoomPlayableBounds>(true);
        Check(authoredBounds!=null && authoredBounds.TryGetWorldBounds(out Bounds roomBounds) &&
              Mathf.Abs(roomBounds.size.x-10.8f)<.01f && Mathf.Abs(roomBounds.size.z-22f)<.01f,
              "Room Playable Bounds matches the authored floor dimensions");
        var boundsCollider=authoredBounds.GetComponent<BoxCollider>();
        Check(boundsCollider!=null && boundsCollider.isTrigger && !boundsCollider.enabled,
            "Room Playable Bounds remains a disabled non-physical authoring volume");
        Check(run.ObstacleColliders.Count==5,"Five visible obstacle blocks own gameplay collision");
        foreach(var collider in run.ObstacleColliders)
            Check(collider!=null && collider.enabled && collider.GetComponent<MeshRenderer>()!=null,"Obstacle collider matches a visible stone block");
        Check(run.GateActive,"Centered gate starts closed");
    }
    static void CheckSlime(LootGoblinRun run)
    {
        var prefab = Resources.Load<GameObject>("Slime");
        Check(prefab != null && prefab.GetComponent<SlimeMotion>() != null, "Slime prefab and component load");
        Check(run.GetComponentsInChildren<SlimeMotion>().Length + run.GetComponentsInChildren<SkeletonArcher>().Length == run.EnemyCount &&
              run.GetComponentsInChildren<SlimeMotion>().Length > 0, "Mixed run retains Slime enemies");
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var slime = instance.GetComponent<SlimeMotion>(); var visual = instance.transform.GetChild(0);
            slime.Tick(.1f, false); var idle = visual.localScale;
            slime.Tick(.1f, false); Check(visual.localScale != idle, "Idle wobble changes body scale");
            bool lifted = false, planted = false;
            for(int i=0;i<60;i++) { float speed=slime.Tick(1f/60,true); lifted |= visual.localPosition.y>.2f && speed>0; planted |= speed==0; }
            Check(lifted && planted, "Hop alternates planted pause and lifted movement");
            Vector3 attackStart=instance.transform.position;
            slime.BeginAttackWindup();
            slime.Tick(.15f,true);
            Check(slime.IsWindingUp && visual.localScale.x>1 && visual.localScale.y<1,"Attack wind-up stops and visibly squashes lower and wider");
            slime.Tick(.15f,true);
            Check(slime.ReadyToCommit,"Attack wind-up reaches a committed lunge point");
            Vector3 landing=attackStart+Vector3.forward*2f;
            slime.CommitLunge(landing);
            slime.Tick(.075f,true);
            var body=visual.Find("Body"); var rightEye=visual.Find("Right Eye");
            var mesh=body.GetComponent<MeshFilter>().sharedMesh;
            float minZ=mesh.bounds.min.z, sizeZ=mesh.bounds.size.z, tailRadius=0, headRadius=0;
            foreach(var vertex in mesh.vertices)
            {
                float along=(vertex.z-minZ)/Mathf.Max(.0001f,sizeZ);
                float radius=new Vector2(vertex.x,vertex.y).magnitude;
                if(along<.2f) tailRadius=Mathf.Max(tailRadius,radius);
                if(along>.55f && along<.9f) headRadius=Mathf.Max(headRadius,radius);
            }
            Check(slime.IsLunging && instance.transform.position==attackStart && rightEye.localPosition.z>1f,"Lunge moves the attacking head while the root remains anchored");
            Check(mesh.bounds.size.z>1.5f && tailRadius<headRadius*.45f,"Lunge body is elongated with a dramatically narrow trailing end");
            slime.Tick(.08f,true);
            Check(slime.CurrentState==SlimeMotion.AttackState.AttackRecovery && Vector3.Distance(instance.transform.position,landing)<.001f,"Lunge lands and reforms at its committed destination");
            slime.Tick(.3f,true);
            Check(slime.CurrentState==SlimeMotion.AttackState.Chase,"Recovery completes before chase resumes");
            slime.BeginDeath(); slime.Tick(.16f,false);
            Check(!slime.DeathFinished && visual.localScale.y<.6f, "Death visibly collapses before completion");
            slime.Tick(.17f,false); Check(slime.DeathFinished, "Death animation completes");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
    static void CheckEquipmentV1(LootGoblinRun run)
    {
        const string saveKey = "LootGoblin.SaveData";
        bool hadSave = PlayerPrefs.HasKey(saveKey);
        string original = hadSave ? PlayerPrefs.GetString(saveKey) : null;
        var equipment = run.Equipment;
        try
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            equipment.ReloadFromPersistence();
            Check(equipment.GetEquipped(EquipmentSlot.Weapon) != null &&
                  equipment.GetEquipped(EquipmentSlot.Head) != null &&
                  equipment.GetEquipped(EquipmentSlot.Chest) != null,
                  "Equipment V1 seeds the three existing Iron definitions as distinct equipped instances");
            Check(equipment.IronFamilyCount == 3, "Iron family count is calculated from the equipped slots");

            int beforeDrop = equipment.OwnedItems.Count;
            equipment.GrantRunDrop();
            Check(equipment.OwnedItems.Count == beforeDrop + 1 && equipment.RunBagCount == 1,
                  "A run-found Iron item enters the at-risk run bag");
            OwnedEquipmentItem dropped = null;
            foreach (var item in equipment.OwnedItems)
                if (item.inRunBag) { dropped = item; break; }
            Check(dropped != null && dropped.definitionId == "iron-sword" && dropped.isNew,
                  "A second Iron Sword is a rolled NEW instance, not a duplicated model definition");
            var ordered = equipment.GetSortedItems(EquipmentSlot.Weapon, EquipmentSort.Power, true);
            for (int i = 1; i < ordered.Count; i++) Check(ordered[i - 1].power >= ordered[i].power, "Power sort is numerically descending");

            OwnedEquipmentItem previous = equipment.GetEquipped(EquipmentSlot.Weapon);
            equipment.Equip(dropped.instanceId);
            Check(dropped.equipped && !dropped.inRunBag && !dropped.isNew && !previous.equipped && previous.inRunBag,
                  "Equipping a run item makes it safe and swaps the replaced item into the run bag");
            Check(equipment.DamageBonus > 0 && run.Health.Max == 140,
                  "Equipped rolls apply through runtime combat and health stat owners");
            equipment.Open();
            Check(equipment.HasActivePreview, "Equipment screen creates an active RenderTexture preview from the real Goblin hierarchy");
            equipment.Close();

            string equippedId = equipment.GetEquipped(EquipmentSlot.Weapon).instanceId;
            var reloaded = LootGoblinSave.Load();
            bool persisted = false;
            foreach (var item in reloaded.ownedEquipment) if (item.instanceId == equippedId && item.equipped) persisted = true;
            Check(persisted, "Save reload retains the equipped Iron instance");
            equipment.CommitExtraction();
            reloaded = LootGoblinSave.Load();
            bool extractedWeaponSurvived = false;
            bool replacedWeaponConverted = true;
            foreach (var item in reloaded.ownedEquipment)
            {
                if (item.instanceId == equippedId && item.equipped) extractedWeaponSurvived = true;
                if (item.instanceId == previous.instanceId) replacedWeaponConverted = false;
            }
            Check(equipment.RunBagCount == 0 && extractedWeaponSurvived && replacedWeaponConverted,
                  "Extraction converts the replaced bag Sword while retaining the exact equipped Sword");

            equipment.GrantRunDrop();
            equipment.GrantRunDrop();
            equipment.GrantRunDrop();
            OwnedEquipmentItem deathDrop = null;
            foreach (var item in equipment.OwnedItems)
                if (item.inRunBag && item.definitionId == "iron-sword") deathDrop = item;
            OwnedEquipmentItem beforeDeath = equipment.GetEquipped(EquipmentSlot.Weapon);
            equipment.Equip(deathDrop.instanceId);
            string deathEquippedId = deathDrop.instanceId;
            string deathHeadId = equipment.GetEquipped(EquipmentSlot.Head).instanceId;
            string deathChestId = equipment.GetEquipped(EquipmentSlot.Chest).instanceId;
            equipment.DiscardRunBagOnDeath();
            reloaded = LootGoblinSave.Load();
            bool deathWeaponSurvived = false;
            bool deathHeadSurvived = false;
            bool deathChestSurvived = false;
            bool unequippedWeaponLost = true;
            foreach (var item in reloaded.ownedEquipment)
            {
                if (item.instanceId == deathEquippedId && item.equipped) deathWeaponSurvived = true;
                if (item.instanceId == deathHeadId && item.equipped) deathHeadSurvived = true;
                if (item.instanceId == deathChestId && item.equipped) deathChestSurvived = true;
                if (item.instanceId == beforeDeath.instanceId) unequippedWeaponLost = false;
            }
            Check(equipment.RunBagCount == 0 && deathWeaponSurvived && deathHeadSurvived && deathChestSurvived && unequippedWeaponLost,
                  "Death removes the swapped-out bag Sword while retaining the exact equipped Weapon, Head, and Chest");
        }
        finally
        {
            if (hadSave) PlayerPrefs.SetString(saveKey, original);
            else PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
            equipment.ReloadFromPersistence();
        }
    }
    static void CheckCombatFeedback(LootGoblinRun run)
    {
        run.Restart();
        var weapon = run.Player.GetComponent<GoblinSwordAttack>();
        var animator = run.Player.GetComponentInChildren<Animator>();
        var hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        var upperArm = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
        var leftHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
        Transform sword = null;
        foreach(var candidate in run.Player.GetComponentsInChildren<Transform>(true)) if(candidate.name=="Sword") { sword=candidate; break; }
        Check(weapon != null && weapon.KnockbackForce > 0, "Sword attack exposes the existing damage and knockback configuration");
        Check(hand != null && sword != null && sword.parent == hand && sword.parent != leftHand,
            "Imported Sword stays attached beneath the goblin right hand, never the left hand");
        Check(sword != null && Quaternion.Angle(sword.localRotation, Quaternion.Euler(-90, -90, 0)) < .1f,
            "Sword keeps its required X -90, Y -90 hand-socket orientation");
        Quaternion readyUpperArmRotation = upperArm != null ? upperArm.localRotation : Quaternion.identity;
        Vector3 readySwordPosition = sword != null ? sword.localPosition : Vector3.zero;
        Quaternion readySwordRotation = sword != null ? sword.localRotation : Quaternion.identity;
        Vector3 readySwordScale = sword != null ? sword.localScale : Vector3.one;

        var slimes = run.GetComponentsInChildren<SlimeMotion>();
        var target = slimes[0];
        run.Player.position = target.transform.position + Vector3.back * 1.2f;

        bool swung = false, hit = false, recovered = false, captured = false;
        for (int i = 0; i < 90; i++)
        {
            run.Tick(Vector2.zero, 1f / 60);
            swung |= upperArm != null && Quaternion.Angle(upperArm.localRotation, readyUpperArmRotation) > 8;
            if (weapon.HitThisTick)
            {
                hit = true;
                if(!captured) { Capture(run,"Logs/GoblinSwordAttack.png"); captured=true; }
                bool receiverRecoiling=false;
                foreach(var slime in slimes) receiverRecoiling|=slime.IsRecoiling;
                Check(receiverRecoiling, "Attack hit sends knockback to the slime receiver");
            }
            if (swung && !weapon.IsSwinging && upperArm != null && Quaternion.Angle(upperArm.localRotation, readyUpperArmRotation) < .1f) recovered = true;
        }
        Check(sword != null && sword.localPosition == readySwordPosition && sword.localRotation == readySwordRotation && sword.localScale == readySwordScale,
            "Sword receives no independent transform animation");
        Check(swung && hit && recovered, "Goblin arm bones make a timed sword sweep and return to ready");
        run.Restart();
    }
}
