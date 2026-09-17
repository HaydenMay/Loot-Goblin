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
    const string Pending="LootGoblin.SmokePending";
    static readonly List<string> checks=new();
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
            if(run==null || run.Room==0) return;
            if(readyAt==0) { readyAt=EditorApplication.timeSinceStartup+1; return; }
            if(EditorApplication.timeSinceStartup<readyAt) return;
            readyAt=0; SessionState.SetBool(Pending,false);
            try { Smoke(run); if(Application.isBatchMode) batchExitCode=0; }
            catch(Exception e) { File.WriteAllText("Logs/LootGoblinValidation.txt","FAIL\n"+string.Join("\n",checks)+"\n"+e); Debug.LogException(e); if(Application.isBatchMode) batchExitCode=1; }
            finally { EditorApplication.isPlaying=false; }
            return;
        }
        if(File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Request);
            try { Build(); SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true; }
            catch(Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/LootGoblinValidation.txt","BUILD FAILED\n"+e); Debug.LogException(e); }
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
    { for(int i=0;i<count;i++) run.Tick(input,1f/60); }
    static void KeepSmokeRunAlive(LootGoblinRun run)
    {
        // The room-flow fixture represents a successful, dodging player after health behavior is tested below.
        if (run.Health.Current <= 25) run.Health.ResetHealth();
    }
    static void Capture(LootGoblinRun run,string filename)
    {
        var cam=run.ArenaCamera; var old=cam.targetTexture;
        var rt=new RenderTexture(540,960,24); cam.targetTexture=rt;
        run.FitCamera(540,960,new Rect(0,0,540,960));
        cam.Render(); var previous=RenderTexture.active; RenderTexture.active=rt;
        var tex=new Texture2D(540,960,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,540,960),0,0); tex.Apply();
        File.WriteAllBytes(filename,tex.EncodeToPNG());
        RenderTexture.active=previous; cam.targetTexture=old; rt.Release();
        UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); run.FitCamera();
    }
    static void Smoke(LootGoblinRun run)
    {
        checks.Clear();
        Check(EditorSceneManager.GetActiveScene().path==ScenePath,"Correct playable scene, runtime initialized");
        CheckArenaEnvironment(run);
        CheckSlime(run);
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
        run.Restart();
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
        var cameraPosition=run.ArenaCamera.transform.position;
        foreach(var size in new[]{new Vector2Int(540,960),new Vector2Int(540,1170),new Vector2Int(540,1200)})
        {
            Rect safeArea=new Rect(0,0,size.x,size.y);
            Rect viewport=LootGoblinRun.CalculateArenaViewport(size.x,size.y,safeArea);
            Check(viewport.width==1 && viewport.height>0 && viewport.height<1,"Portrait viewport reserves top and bottom UI space at "+size.x+"x"+size.y);
            Check(Mathf.Abs(viewport.center.y-.5f)<.001f,"Portrait arena stays vertically centered at "+size.x+"x"+size.y);
            run.FitCamera(size.x,size.y,safeArea);
            Check(Mathf.Abs(run.ArenaCamera.rect.height-viewport.height)<.001f,"Camera applies responsive viewport at "+size.x+"x"+size.y);
            Bounds bounds=run.ArenaBounds;
            foreach(float x in new[]{bounds.min.x,bounds.max.x}) foreach(float z in new[]{bounds.min.z,bounds.max.z}) foreach(float y in new[]{bounds.min.y,bounds.max.y})
            {
                var point=run.ArenaCamera.WorldToViewportPoint(new Vector3(x,y,z));
                Check(point.x>0 && point.x<1 && point.y>0 && point.y<1 && point.z>0,"Camera fits current arena bounds at "+size.x+"x"+size.y);
            }
        }
        run.FitCamera();
        Check(run.ArenaCamera.orthographic,"Orthographic camera");
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
        Check(run.ArenaCamera.transform.position==cameraPosition,"Camera never follows player");
        Steps(run,Vector2.right,400); Check(run.Player.position.x<=5.001f,"Room boundary contains player");
        run.Restart();
        Bounds obstacle=run.ObstacleColliders[1].bounds;
        run.Player.position=new Vector3(obstacle.center.x,.65f,obstacle.min.z-.41f); Steps(run,Vector2.up,60);
        Check(run.Player.position.z<=obstacle.min.z-.399f,"Visible obstacle collider blocks player");
        run.Restart(); run.Player.position=run.FirstEnemyPosition+Vector3.back*1.2f;
        int hits=run.Hits; Steps(run,Vector2.right,20);
        Check(run.Hits==hits,"No attack while moving with enemy in range");
        run.Restart();
        int total=0;
        for(int room=1;room<=5;room++)
        {
            Check(run.Room==room && run.EnemyCount==room+1 && !run.ExitOpen && run.GateActive,"Room "+room+" spawns and closes exit");
            var enemyStart=run.FirstEnemyPosition; Steps(run,Vector2.zero,30);
            Check(run.FirstEnemyPosition!=enemyStart,"Enemies approach player in room "+room);
            int count=run.EnemyCount; int guard=0;
            while(run.EnemyCount==count && guard++<7200) run.Tick(Vector2.zero,1f/60);
            Check(run.EnemyCount<count && run.PickupCount>0,"Attack damages, kills and drops visible loot in room "+room);
            guard=0;
            while(!run.ExitOpen && guard++<7200) { KeepSmokeRunAlive(run); run.Tick(Vector2.zero,1f/60); }
            bool gateOpened=run.ExitOpen && !run.GateActive;
            string remaining="";
            if(!gateOpened)
                foreach(var slime in run.GetComponentsInChildren<SlimeMotion>())
                    if(slime.gameObject.activeInHierarchy) remaining+=$" {slime.transform.position}";
            Check(gateOpened,"All enemies die and the visible gate opens in room "+room+(gateOpened?"":"; remaining:"+remaining));
            Steps(run,Vector2.zero,180);
            total+=room+1;
            Check(run.Loot==total,"Magnetic pickups collect and count in room "+room);
            int healthBeforeTransition=run.Health.Current;
            if(room==1 && healthBeforeTransition==run.Health.Max)
            {
                run.Health.Tick(1f);
                Check(run.Health.TryTakeDamage(25),"Controlled damage prepares the room-transition health check");
                healthBeforeTransition=run.Health.Current;
            }
            guard=0;
            while(run.Room==room && !run.Complete && guard++<400) run.Tick(Vector2.up,1f/60);
            Check(room==5?run.Complete:run.Room==room+1,"Exit advances room "+room);
            if(room==1) Check(run.Health.Current==healthBeforeTransition && run.Health.Current<run.Health.Max,"Health persists through normal room transitions");
        }
        Check(run.Complete && run.Room==5 && run.Loot==20,"Five rooms finish with RUN COMPLETE and 20 loot");
        var end=run.Player.position; Steps(run,Vector2.down,60);
        Check(run.Player.position==end,"Completion freezes gameplay");
        run.Restart(); Check(run.Room==1 && run.Loot==0 && run.EnemyCount==2 && !run.Complete,"Restart resets run");
        File.WriteAllText("Logs/LootGoblinValidation.txt",string.Join("\n",checks)+"\nALL PASSED");
        Debug.Log("Loot Goblin: ALL SMOKE CHECKS PASSED. Logs/LootGoblinValidation.txt");
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
        Check(Mathf.Abs(floorRenderer.bounds.size.x-10.8f)<.01f && Mathf.Abs(floorRenderer.bounds.size.z-15.6f)<.01f,
            "Stone floor covers the full 10.8 x 15.6 arena");
        var floorMaterial=floorRenderer.sharedMaterial;
        Check(floorMaterial!=null && floorMaterial.GetTexture("_BaseMap")!=null && floorMaterial.GetTexture("_BaseMap").name=="ArenaFloorStone",
            "Stone floor uses the supplied arena texture");
        Vector2 floorTextureScale=floorMaterial.GetTextureScale("_BaseMap");
        Vector2 floorTextureOffset=floorMaterial.GetTextureOffset("_BaseMap");
        Check(Mathf.Abs(floorTextureScale.x-10.8f/15.6f)<.001f && Mathf.Abs(floorTextureScale.y-1)<.001f &&
              Mathf.Abs(floorTextureOffset.x-(1-10.8f/15.6f)*.5f)<.001f,
            "Stone texture is center-cropped without stretching");
        Check(wallColliders==26,"Perimeter uses 26 aligned wall pieces with colliders");
        Check(run.ObstacleColliders.Count==5,"Five visible obstacle blocks own gameplay collision");
        foreach(var collider in run.ObstacleColliders)
            Check(collider!=null && collider.enabled && collider.GetComponent<MeshRenderer>()!=null,"Obstacle collider matches a visible stone block");
        Check(run.GateActive,"Centered gate starts closed");
    }
    static void CheckSlime(LootGoblinRun run)
    {
        var prefab = Resources.Load<GameObject>("Slime");
        Check(prefab != null && prefab.GetComponent<SlimeMotion>() != null, "Slime prefab and component load");
        Check(run.GetComponentsInChildren<SlimeMotion>().Length == run.EnemyCount, "Run spawns slimes");
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
    static void CheckCombatFeedback(LootGoblinRun run)
    {
        run.Restart();
        var weapon = run.GetComponentInChildren<WeaponSwing>(true);
        Check(weapon != null && weapon.KnockbackForce > 0, "Weapon exposes a configurable knockback force");
        var readyPosition = weapon.transform.localPosition;
        var slimes = run.GetComponentsInChildren<SlimeMotion>();
        var target = slimes[0];
        run.Player.position = target.transform.position + Vector3.back * 1.2f;

        bool swung = false, hit = false, recovered = false;
        for (int i = 0; i < 28; i++)
        {
            run.Tick(Vector2.zero, 1f / 60);
            swung |= weapon.transform.localPosition != readyPosition;
            if (weapon.HitThisTick)
            {
                hit = true;
                bool receiverRecoiling=false;
                foreach(var slime in slimes) receiverRecoiling|=slime.IsRecoiling;
                Check(receiverRecoiling, "Attack hit sends knockback to the slime receiver");
            }
            if (swung && !weapon.IsSwinging && weapon.transform.localPosition == readyPosition) recovered = true;
        }
        Check(swung && hit && recovered, "Weapon uses a timed arc swing and returns to ready");
        run.Restart();
    }
}
