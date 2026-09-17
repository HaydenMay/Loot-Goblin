using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// One bounded arena owns the five-room prototype; all coordinates are on the XZ plane.
public sealed class LootGoblinRun : MonoBehaviour
{
    const float PortraitArenaAspect = .75f;
    const float CameraFramePadding = .35f;
    const float UiBandHeight = 80f;
    [SerializeField] Transform player;
    [SerializeField] GameObject gate, portal, strike;
    [SerializeField] Camera arenaCamera;
    [SerializeField] BoxCollider[] obstacleColliders;
    [SerializeField] Material enemyMaterial, lootMaterial;
    [SerializeField] InputActionAsset controls;
    readonly List<Enemy> enemies = new();
    readonly List<Pickup> pickups = new();
    InputAction move;
    FloatingJoystick floatingJoystick;
    GameObject slimePrefab;
    PlayerHealth health;
    float cooldown;
    WeaponSwing weaponSwing;
    Enemy pendingAttackTarget;
    Bounds arenaBounds;
    bool hasArenaBounds;
    public int Room { get; private set; }
    public int Loot { get; private set; }
    public int Hits { get; private set; }
    public int EnemyCount => enemies.Count;
    public int PickupCount => pickups.Count;
    public bool Complete { get; private set; }
    public bool Failed { get; private set; }
    public bool ExitOpen => enemies.Count == 0;
    public Transform Player => player;
    public Camera ArenaCamera => arenaCamera;
    public Bounds ArenaBounds => arenaBounds;
    public IReadOnlyList<BoxCollider> ObstacleColliders => obstacleColliders ?? System.Array.Empty<BoxCollider>();
    public bool GateActive => gate.activeSelf;
    public PlayerHealth Health => health;
    public Vector3 FirstEnemyPosition => enemies[0].body.position;
    sealed class Enemy { public Transform body; public int health = 3; public SlimeMotion slime; public IHitReceiver hitReceiver; }
    sealed class Pickup { public Transform body; public float age; public bool attracted; }

    void Awake()
    {
        CacheArenaBounds();
        move = controls.FindAction("Player/Move", true).Clone();
        floatingJoystick = GetComponent<FloatingJoystick>();
        if (floatingJoystick == null) floatingJoystick = gameObject.AddComponent<FloatingJoystick>();
        slimePrefab = Resources.Load<GameObject>("Slime");
        health = player.GetComponent<PlayerHealth>();
        if (health == null) health = player.gameObject.AddComponent<PlayerHealth>();
        ConfigureWeapon();
        Restart();
    }
    void OnEnable() { move?.Enable(); }
    void OnDisable() { move?.Disable(); }
    void OnDestroy() { move?.Dispose(); }
    public Vector2 ReadMovement()
    {
        if (floatingJoystick != null && floatingJoystick.IsTouchActive) return floatingJoystick.Value;
        return move == null ? Vector2.zero : move.ReadValue<Vector2>();
    }
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
        Tick(ReadMovement(), Time.deltaTime);
    }
    void LateUpdate() { FitCamera(); }
    public void FitCamera()
    {
        FitCamera(Screen.width, Screen.height, Screen.safeArea);
    }
    public void FitCamera(int screenWidth, int screenHeight, Rect safeArea)
    {
        if (screenWidth <= 0 || screenHeight <= 0) return;

        Rect viewport = CalculateArenaViewport(screenWidth, screenHeight, safeArea);
        arenaCamera.rect = viewport;
        float viewportAspect = screenWidth * viewport.width / Mathf.Max(1f, screenHeight * viewport.height);
        arenaCamera.aspect = viewportAspect;

        // Fit the scene's actual stone room, rather than a hard-coded primitive-room size.
        arenaCamera.orthographicSize = CalculateArenaOrthoSize(viewportAspect);
    }
    public static Rect CalculateArenaViewport(int screenWidth, int screenHeight, Rect safeArea)
    {
        if (screenWidth <= 0 || screenHeight <= 0) return new Rect(0, 0, 1, 1);
        if (screenWidth >= screenHeight) return new Rect(0, 0, 1, 1);

        float scale = Mathf.Clamp(Mathf.Min(screenWidth / 540f, screenHeight / 960f), .45f, 1.5f);
        Rect safe = Rect.MinMaxRect(
            Mathf.Clamp(safeArea.xMin, 0, screenWidth),
            Mathf.Clamp(safeArea.yMin, 0, screenHeight),
            Mathf.Clamp(safeArea.xMax, 0, screenWidth),
            Mathf.Clamp(safeArea.yMax, 0, screenHeight));
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, screenWidth, screenHeight);

        float uiBand = Mathf.Min(UiBandHeight * scale, safe.height * .25f);
        float lower = Mathf.Min(safe.yMax, safe.yMin + uiBand);
        float upper = Mathf.Max(lower, safe.yMax - uiBand);
        float maximumHeight = Mathf.Max(.01f, (upper - lower) / screenHeight);
        float targetHeight = safe.width / (screenHeight * PortraitArenaAspect);
        float height = Mathf.Min(maximumHeight, targetHeight);
        float y = (lower + upper - height * screenHeight) * .5f / screenHeight;

        return new Rect(safe.xMin / screenWidth, y, safe.width / screenWidth, height);
    }
    void CacheArenaBounds()
    {
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!hasArenaBounds) { arenaBounds = renderer.bounds; hasArenaBounds = true; }
            else arenaBounds.Encapsulate(renderer.bounds);
        }
    }
    float CalculateArenaOrthoSize(float viewportAspect)
    {
        if (!hasArenaBounds) return arenaCamera.orthographicSize;

        Vector3 min = arenaBounds.min, max = arenaBounds.max;
        float halfWidth = 0, halfHeight = 0;
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
        {
            Vector3 point = arenaCamera.transform.InverseTransformPoint(new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z));
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(point.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y));
        }
        return Mathf.Max(halfHeight, halfWidth / Mathf.Max(.2f, viewportAspect)) + CameraFramePadding;
    }
    public void Restart()
    {
        Loot = Hits = 0; Complete = Failed = false; health.ResetHealth(); BeginRoom(1);
    }
    void BeginRoom(int number)
    {
        foreach (var e in enemies) Destroy(e.body.gameObject);
        foreach (var p in pickups) Destroy(p.body.gameObject);
        enemies.Clear(); pickups.Clear(); Room = number;
        player.position = new Vector3(0,.65f,-6.7f); player.rotation = Quaternion.identity;
        cooldown = 0; pendingAttackTarget = null; weaponSwing?.Cancel();
        gate.SetActive(true); portal.SetActive(false); strike.SetActive(true);
        for (int i = 0; i < Room + 1; i++)
        {
            var position = new Vector3((i%3-1)*3.7f,0,3+i/3*2);
            var body = slimePrefab != null
                ? Instantiate(slimePrefab, position, Quaternion.identity, transform).transform
                : Shape("Enemy", PrimitiveType.Capsule, position+Vector3.up*.55f, new Vector3(.65f,.55f,.65f), enemyMaterial).transform;
            body.position = Resolve(body.position, .36f);
            enemies.Add(new Enemy
            {
                body = body,
                slime = body.GetComponent<SlimeMotion>(),
                hitReceiver = body.GetComponent(typeof(IHitReceiver)) as IHitReceiver
            });
        }
    }
    // The input boundary is a Vector2: a future joystick can supply the same value.
    public void Tick(Vector2 input, float dt)
    {
        if (Complete || Failed) return;
        dt = Mathf.Clamp(dt, 0, .05f);
        health.Tick(dt);
        if (health.IsDead) { FailRun(); return; }
        input = Vector2.ClampMagnitude(input, 1);
        bool moving = input.sqrMagnitude > .01f;
        cooldown = Mathf.Max(0, cooldown-dt);
        weaponSwing?.Tick(dt);
        if (weaponSwing != null && weaponSwing.HitThisTick) ResolveAttackHit();
        if (moving)
        {
            Vector3 direction = new(input.x,0,input.y);
            player.forward = direction;
            player.position = Resolve(player.position+direction*(4.5f*dt), .4f);
            pendingAttackTarget = null;
            weaponSwing?.Cancel();
        }
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e.health <= 0)
            {
                if (e.slime != null) e.slime.Tick(dt, false);
                ApplyKnockback(e, dt);
                if (e.slime == null || e.slime.DeathFinished) FinishDeath(e);
                continue;
            }
            Vector3 delta = Flat(player.position-e.body.position);
            float speed = e.slime != null ? e.slime.Tick(dt, delta.magnitude > .95f) : 1;
            if (e.slime != null && e.slime.IsRecoiling) speed = 0;
            if (e.slime != null && e.slime.AttackHitThisTick && delta.magnitude <= 1.35f)
            {
                health.TryTakeDamage(25);
                if (health.IsDead)
                {
                    FailRun();
                    return;
                }
            }
            if (delta.sqrMagnitude > .0001f) e.body.forward = delta;
            if (delta.magnitude > .95f)
            {
                Vector3 direction = delta.normalized;
                // Steer around the same visible box colliders used by movement resolution.
                foreach (var obstacle in ObstacleColliders)
                {
                    if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy) continue;
                    Vector3 obstacleCenter = obstacle.bounds.center;
                    Vector3 offset = Flat(obstacleCenter-e.body.position);
                    float ahead = Vector3.Dot(offset,direction);
                    float avoidanceRadius = Mathf.Max(obstacle.bounds.extents.x, obstacle.bounds.extents.z) + .55f;
                    if (ahead > 0 && ahead < 2.5f && Vector3.Cross(offset,direction).magnitude < avoidanceRadius)
                    {
                        Vector3 closest = obstacle.ClosestPoint(e.body.position);
                        Vector3 away = Flat(e.body.position-closest);
                        if(Mathf.Abs(e.body.position.x)>4.8f)
                            away=new Vector3(0,0,direction.z>=0?1:-1);
                        else if(away.sqrMagnitude<.0001f)
                            away=e.body.position.x>=obstacleCenter.x?Vector3.right:Vector3.left;
                        else if(Mathf.Abs(away.z)>=Mathf.Abs(away.x)*.75f)
                        {
                            float side=e.body.position.x>=obstacleCenter.x?1:-1;
                            away=(Vector3.right*side+away.normalized*.35f).normalized;
                        }
                        direction=(direction*.35f+away.normalized).normalized;
                        break;
                    }
                }
                e.body.position = Resolve(e.body.position+direction*(1.55f*speed*dt), .36f);
                e.body.forward = delta;
            }
            ApplyKnockback(e, dt);
        }
        if (!moving && cooldown <= 0)
        {
            Enemy target = null; float nearest = 2.4f;
            foreach (var e in enemies)
            {
                if (e.health <= 0) continue;
                float distance = Flat(e.body.position-player.position).magnitude;
                if (distance < nearest && ClearSight(player.position,e.body.position)) { target=e; nearest=distance; }
            }
            if (target != null)
            {
                player.forward = Flat(target.body.position-player.position);
                cooldown=.42f; Hits++;
                pendingAttackTarget = target;
                weaponSwing?.Play();
            }
        }
        for (int i=pickups.Count-1;i>=0;i--)
        {
            var p=pickups[i]; p.age+=dt;
            Vector3 destination=player.position; destination.y=.3f;
            float distance=Vector3.Distance(p.body.position,destination);
            if (p.age>.3f && distance<3.2f) p.attracted=true;
            if (p.attracted) p.body.position=Vector3.MoveTowards(p.body.position,destination,9*dt);
            if (p.age>.3f && Vector3.Distance(p.body.position,destination)<.4f)
            { Loot++; Destroy(p.body.gameObject); pickups.RemoveAt(i); }
        }
        if (ExitOpen && Mathf.Abs(player.position.x)<1.35f && player.position.z>6.8f)
        {
            // Bank remaining drops so a room reset never discards earned loot.
            Loot+=pickups.Count;
            foreach(var p in pickups) Destroy(p.body.gameObject);
            pickups.Clear();
            if (Room==5) { Complete=true; pendingAttackTarget=null; weaponSwing?.Cancel(); }
            else BeginRoom(Room+1);
        }
    }
    void FinishDeath(Enemy enemy)
    {
        Vector3 pos = enemy.body.position; pos.y = .3f;
        var gold = Shape("Loot", PrimitiveType.Sphere, pos, Vector3.one*.38f, lootMaterial);
        pickups.Add(new Pickup { body = gold.transform });
        enemy.body.gameObject.SetActive(false);
        Destroy(enemy.body.gameObject); enemies.Remove(enemy);
        if (ExitOpen) { gate.SetActive(false); portal.SetActive(true); }
    }
    void FailRun()
    {
        Failed = true;
        pendingAttackTarget = null;
        weaponSwing?.Cancel();
    }
    void ConfigureWeapon()
    {
        strike.SetActive(true);
        weaponSwing = strike.GetComponent<WeaponSwing>();
        if (weaponSwing == null) weaponSwing = strike.AddComponent<WeaponSwing>();
    }
    void ResolveAttackHit()
    {
        Enemy target = pendingAttackTarget;
        pendingAttackTarget = null;
        if (target == null || target.health <= 0 || !enemies.Contains(target)) return;

        Vector3 direction = Flat(target.body.position-player.position);
        if (direction.magnitude > 2.4f || !ClearSight(player.position,target.body.position)) return;

        player.forward = direction;
        int damage = weaponSwing.Damage;
        target.health -= damage;
        target.hitReceiver?.TakeHit(damage, direction, weaponSwing.KnockbackForce);
        if (target.slime == null)
            target.body.localScale = new Vector3(.65f,.55f,.65f)*(1f-.08f*(3-target.health));
        if (target.health <= 0)
        {
            if (target.slime != null) target.slime.BeginDeath();
            else FinishDeath(target);
        }
    }
    void ApplyKnockback(Enemy enemy, float dt)
    {
        if (enemy.slime == null) return;
        Vector3 displacement = enemy.slime.ConsumeKnockback(dt);
        if (displacement.sqrMagnitude > 0) enemy.body.position = Resolve(enemy.body.position+displacement, .36f);
    }
    static Vector3 Flat(Vector3 v) { v.y=0; return v; }
    Vector3 Resolve(Vector3 pos,float radius)
    {
        pos.x=Mathf.Clamp(pos.x,-5.4f+radius,5.4f-radius);
        pos.z=Mathf.Clamp(pos.z,-7.8f+radius,7.8f-radius);
        foreach(var obstacle in ObstacleColliders)
        {
            if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy) continue;
            Bounds bounds=obstacle.bounds;
            float closestX=Mathf.Clamp(pos.x,bounds.min.x,bounds.max.x);
            float closestZ=Mathf.Clamp(pos.z,bounds.min.z,bounds.max.z);
            Vector2 delta=new(pos.x-closestX,pos.z-closestZ);
            if(delta.sqrMagnitude>=radius*radius) continue;

            if(delta.sqrMagnitude>.0001f)
            {
                float distance=delta.magnitude;
                Vector2 correction=delta/distance*(radius-distance);
                pos.x+=correction.x; pos.z+=correction.y;
                continue;
            }

            float left=Mathf.Abs(pos.x-bounds.min.x);
            float right=Mathf.Abs(bounds.max.x-pos.x);
            float bottom=Mathf.Abs(pos.z-bounds.min.z);
            float top=Mathf.Abs(bounds.max.z-pos.z);
            float nearest=Mathf.Min(Mathf.Min(left,right),Mathf.Min(bottom,top));
            if(nearest==left) pos.x=bounds.min.x-radius;
            else if(nearest==right) pos.x=bounds.max.x+radius;
            else if(nearest==bottom) pos.z=bounds.min.z-radius;
            else pos.z=bounds.max.z+radius;
        }
        pos.x=Mathf.Clamp(pos.x,-5.4f+radius,5.4f-radius);
        pos.z=Mathf.Clamp(pos.z,-7.8f+radius,7.8f-radius);
        return pos;
    }
    bool ClearSight(Vector3 a,Vector3 b)
    {
        foreach(var obstacle in ObstacleColliders)
        {
            if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy) continue;
            if(SegmentIntersectsBoundsXZ(a,b,obstacle.bounds,.06f)) return false;
        }
        return true;
    }
    static bool SegmentIntersectsBoundsXZ(Vector3 a,Vector3 b,Bounds bounds,float padding)
    {
        Vector2 start=new(a.x,a.z), delta=new(b.x-a.x,b.z-a.z);
        Vector2 min=new(bounds.min.x-padding,bounds.min.z-padding);
        Vector2 max=new(bounds.max.x+padding,bounds.max.z+padding);
        float enter=0, exit=1;
        for(int axis=0;axis<2;axis++)
        {
            float origin=axis==0?start.x:start.y;
            float direction=axis==0?delta.x:delta.y;
            float low=axis==0?min.x:min.y;
            float high=axis==0?max.x:max.y;
            if(Mathf.Abs(direction)<.0001f)
            {
                if(origin<low || origin>high) return false;
                continue;
            }
            float first=(low-origin)/direction, second=(high-origin)/direction;
            if(first>second) (first,second)=(second,first);
            enter=Mathf.Max(enter,first); exit=Mathf.Min(exit,second);
            if(enter>exit) return false;
        }
        return true;
    }
    void OnGUI()
    {
        float scale=Mathf.Clamp(Mathf.Min(Screen.width/540f,Screen.height/960f),.45f,1.5f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        var style=new GUIStyle(GUI.skin.box) { fontSize=20, alignment=TextAnchor.MiddleCenter };
        Rect safe=Screen.safeArea;
        float x=safe.xMin/scale, y=safe.yMin/scale, width=safe.width/scale, height=safe.height/scale;
        GUI.Box(new Rect(x+10,y+10,width-20,65),$"LOOT GOBLIN   |   Room {Room} / 5\nHealth: {health.Current} / {health.Max}   |   Loot: {Loot}   |   Enemies: {enemies.Count}",style);
        string state=Failed?"RUN FAILED\nPress R to restart":Complete?"RUN COMPLETE\nPress R to restart":ExitOpen?(Room==5?"FINAL PORTAL OPEN - head north":"ROOM CLEAR - head north"):"WASD / arrows: move   |   Stop: auto-attack";
        GUI.Box(new Rect(x+10,y+height-80,width-20,65),state,style);
    }
    GameObject Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(transform);
        go.transform.position=position; go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;
        // Gameplay uses bounded planar geometry, not Rigidbody physics.
        // CreatePrimitive adds primitive colliders by name internally. Keep the loot sphere's
        // concrete type reachable so WebGL stripping retains it, then disable it as before.
        Collider collider = type == PrimitiveType.Sphere
            ? go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>()
            : go.GetComponent<Collider>();
        if (collider != null) collider.enabled=false;
        else Debug.LogError($"{name} primitive was created without its required collider.", go);
        return go;
    }
    public void BuildArena(InputActionAsset input)
    {
        controls=input;
        Material Make(string name,Color color)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name=name; mat.color=color; return mat;
        }
        var stone=Make("Stone",new Color(.32f,.29f,.25f));
        var walls=Make("Walls",new Color(.22f,.24f,.28f));
        var green=Make("Goblin",new Color(.3f,.9f,.22f));
        enemyMaterial=Make("Enemy",new Color(.85f,.23f,.17f));
        lootMaterial=Make("Gold",new Color(1,.72f,.06f));
        var exit=Make("Portal",new Color(.15f,.9f,.85f));
        Shape("Stone Floor",PrimitiveType.Cube,new Vector3(0,-.25f,0),new Vector3(12,.5f,17),stone);
        Shape("West Wall",PrimitiveType.Cube,new Vector3(-5.8f,.8f,0),new Vector3(.8f,1.6f,17),walls);
        Shape("East Wall",PrimitiveType.Cube,new Vector3(5.8f,.8f,0),new Vector3(.8f,1.6f,17),walls);
        foreach(float x in new[]{-3.8f,3.8f})
        {
            Shape("North Wall",PrimitiveType.Cube,new Vector3(x,.8f,8),new Vector3(4.4f,1.6f,.8f),walls);
            Shape("Entrance Wall",PrimitiveType.Cube,new Vector3(x,.45f,-8),new Vector3(4.4f,.9f,.8f),walls);
        }
        foreach(var p in new[]{new Vector3(-3,0,-2),new Vector3(3,0,1),new Vector3(-3,0,4)})
            Shape("Stone Pillar",PrimitiveType.Cylinder,p+Vector3.up*.6f,new Vector3(2.1f,.6f,2.1f),walls);
        gate=Shape("Exit Gate",PrimitiveType.Cube,new Vector3(0,1,7.9f),new Vector3(2.8f,2,.4f),walls);
        portal=Shape("Exit Portal",PrimitiveType.Cylinder,new Vector3(0,.08f,7.25f),new Vector3(2.6f,.08f,1.3f),exit); portal.SetActive(false);
        player=Shape("Player",PrimitiveType.Capsule,new Vector3(0,.65f,-6.7f),new Vector3(.7f,.65f,.7f),green).transform;
        player.gameObject.AddComponent<PlayerHealth>();
        var nose=Shape("Facing Marker",PrimitiveType.Cube,player.position+new Vector3(0,.15f,.45f),new Vector3(.18f,.18f,.4f),lootMaterial);
        nose.transform.SetParent(player,true);
        strike=Shape("Attack Flash",PrimitiveType.Cube,player.position+Vector3.forward*.9f,new Vector3(.18f,.15f,1.1f),lootMaterial);
        strike.transform.SetParent(player,true); strike.SetActive(true); strike.AddComponent<WeaponSwing>();
        var cam=new GameObject("Arena Camera"); cam.transform.SetParent(transform); arenaCamera=cam.AddComponent<Camera>(); cam.tag="MainCamera";
        cam.transform.position=new Vector3(0,19,-13); cam.transform.rotation=Quaternion.Euler(58,0,0);
        arenaCamera.orthographic=true; arenaCamera.nearClipPlane=.1f; arenaCamera.farClipPlane=70;
        arenaCamera.clearFlags=CameraClearFlags.SolidColor; arenaCamera.backgroundColor=new Color(.045f,.055f,.075f); CacheArenaBounds(); FitCamera();
        cam.AddComponent<AudioListener>();
        var sun=new GameObject("Warm Dungeon Light"); sun.transform.SetParent(transform); sun.transform.rotation=Quaternion.Euler(55,-25,0);
        var light=sun.AddComponent<Light>(); light.type=LightType.Directional; light.color=new Color(1,.8f,.58f); light.intensity=1.6f; light.shadows=LightShadows.Soft;
        RenderSettings.ambientLight=new Color(.4f,.4f,.45f);
    }
}
