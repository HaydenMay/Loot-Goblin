using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Bounded, reproducible Play Mode scenario, using the real scene and run Tick.
[InitializeOnLoad]
public static class SkeletonArcherValidation
{
    const string Pending="LootGoblin.ArcherValidation";
    static readonly List<string> checks=new();
    static int exit=-1;
    static SkeletonArcherValidation() { EditorApplication.update+=Poll; }
    [MenuItem("Loot Goblin/Validate Skeleton Archer")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene("Assets/Scenes/LootGoblin.unity");
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }
    static void Poll()
    {
        if(exit>=0 && !EditorApplication.isPlayingOrWillChangePlaymode) { int code=exit; exit=-1; EditorApplication.Exit(code); return; }
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var run=UnityEngine.Object.FindAnyObjectByType<LootGoblinRun>();
        if(run==null || run.Room==0) return;
        SessionState.SetBool(Pending,false); run.enabled=false;
        try { Smoke(run); File.WriteAllText("Logs/SkeletonArcherValidation.txt",string.Join("\n",checks)+"\nALL PASSED"); if(Application.isBatchMode) exit=0; }
        catch(Exception e) { File.WriteAllText("Logs/SkeletonArcherValidation.txt",string.Join("\n",checks)+"\nFAIL "+e); Debug.LogException(e); if(Application.isBatchMode) exit=1; }
        finally { EditorApplication.isPlaying=false; }
    }
    static void Check(bool value,string label)
    {
        if(!value) throw new Exception(label); checks.Add("PASS "+label);
    }
    static SkeletonArcher Fixture(LootGoblinRun run)
    {
        run.Restart();
        var archer=run.GetComponentInChildren<SkeletonArcher>();
        archer.transform.position=new Vector3(0,0,.5f);
        archer.transform.forward=Vector3.back;
        run.Player.position=new Vector3(0,.65f,-4.1f);
        return archer;
    }
    static void Step(SkeletonArcher archer,int count)
    { for(int i=0;i<count;i++) archer.Tick(1f/60); }
    static void Until(SkeletonArcher archer,SkeletonArcher.State state)
    {
        for(int i=0;i<600 && archer.CurrentState!=state;i++) archer.Tick(1f/60);
        Check(archer.CurrentState==state,"Reached "+state);
    }
    static void Smoke(LootGoblinRun run)
    {
        checks.Clear(); var archer=Fixture(run);
        Check(run.EnemyCount==2 && run.GetComponentInChildren<SlimeMotion>()!=null,"Room 1 contains one Slime and one Archer");
        Check(Vector3.Dot(archer.transform.forward,Vector3.back)>.99f,"Root +Z faces the target, +Y remains up");
        Capture(run,"01-Reposition");
        Until(archer,SkeletonArcher.State.DrawArrow); Vector3 root=archer.transform.position;
        Step(archer,30);
        Check(archer.ShotsFired==0,"Reload cannot fire");
        Check(archer.transform.InverseTransformPoint(archer.Rig.rightHand.position).z<-.12f,"Reload hand reaches behind the torso toward quiver");
        Capture(run,"02-QuiverReach"); Step(archer,18); Capture(run,"03-BringArrowForward");
        Until(archer,SkeletonArcher.State.Aim); Step(archer,42);
        Check(archer.Rig.arrow.gameObject.activeSelf,"Arrow visibly nocked during aim");
        Check(Vector3.Dot(archer.Rig.ArrowDirection,archer.transform.forward)>.98f,"Held arrow aligned with root forward before release");
        Check(Vector3.Dot(archer.Rig.bow.up,Vector3.up)>.99f,"Bow limbs remain upright");
        Check(Vector3.Distance(root,archer.transform.position)<.001f,"Attack poses do not move root");
        Capture(run,"04-Aim");
        Until(archer,SkeletonArcher.State.Fire); Check(archer.ShotsFired==1,"Aim releases exactly one arrow");
        Capture(run,"05-Fire"); Step(archer,4); Capture(run,"06-Recoil");
        int health=run.Health.Current; Step(archer,50);
        Check(run.Health.Current==health-archer.Tuning.projectileDamage,"Stationary player receives one arrow hit");
        Check(archer.ActiveArrowCount==0,"Impact removes arrow");
        Check(archer.CurrentState==SkeletonArcher.State.DrawArrow,"Recoil returns to reload downtime");
        for(int cycle=0;cycle<3;cycle++)
        {
            int previousShots=archer.ShotsFired;
            Until(archer,SkeletonArcher.State.Fire);
            Check(archer.ShotsFired==previousShots+1,"Repeated cycle fires once");
            Until(archer,SkeletonArcher.State.DrawArrow);
        }
        foreach(float angle in new[]{90f,0f,225f})
        {
            archer=Fixture(run); Until(archer,SkeletonArcher.State.Aim); Step(archer,42);
            archer.transform.rotation=Quaternion.Euler(0,angle,0);
            run.Player.position=archer.transform.position+archer.transform.forward*4.6f+Vector3.up*.65f;
            // Rotate the already exercised pose for an axes inspection. Do not
            // ask AI to navigate to an arbitrary test target inside a stone block.
            archer.Rig.Pose(1,.9f,0,0,true); Capture(run,"AimFacing"+angle);
        }
        archer=Fixture(run); Until(archer,SkeletonArcher.State.Fire);
        var projectile=GameObject.Find("Skeleton arrow projectile").transform;
        Vector3 heading=projectile.forward;
        for(int i=0;i<20;i++) run.Tick(Vector2.right,1f/60);
        Check(Vector3.Dot(heading,projectile.forward)>.9999f,"Released arrow never turns toward moved player");
        for(int i=0;i<25;i++) run.Tick(Vector2.zero,1f/60);
        Check(run.Health.Current==run.Health.Max,"Actual lateral movement input dodges the arrow");
        archer=Fixture(run); Until(archer,SkeletonArcher.State.Fire);
        run.Health.TryTakeDamage(1); Step(archer,50);
        Check(run.Health.Current==run.Health.Max-1 && archer.ActiveArrowCount==0,"Invulnerability rejects arrow damage and still consumes projectile");
        archer=Fixture(run); Until(archer,SkeletonArcher.State.Fire);
        run.Restart();
        Check(archer.ActiveArrowCount==0,"Restart immediately clears old projectiles");
        // Projectile obstruction independently of the AI's line-of-sight gate.
        archer=Fixture(run);
        var obstacle=run.ObstacleColliders[0]; var bounds=obstacle.bounds;
        var arrowVisual=UnityEngine.Object.Instantiate(archer.Rig.arrow.gameObject).transform;
        arrowVisual.gameObject.SetActive(true); arrowVisual.rotation=Quaternion.identity;
        arrowVisual.position=new Vector3(bounds.center.x,1,bounds.min.z-1.3f);
        var blockedArrow=new SkeletonArrow(arrowVisual,Vector3.forward,6,15);
        bool flying=true; for(int i=0;i<30 && flying;i++) flying=blockedArrow.Tick(run,1f/60);
        Check(!flying,"Swept arrow collision stops at visible obstacle"); blockedArrow.Dispose();
        archer=Fixture(run); Until(archer,SkeletonArcher.State.Fire);
        archer.BeginDeath(); Check(archer.ActiveArrowCount==0,"Death immediately removes outgoing damage");
        int shots=archer.ShotsFired; Step(archer,180);
        Check(archer.ShotsFired==shots && archer.DeathFinished,"Dead archer cannot fire again and collapse finishes");
        archer=Fixture(run); run.Player.position=archer.transform.position+Vector3.back*1.5f+Vector3.up*.65f;
        float near=Vector3.Distance(archer.transform.position,run.Player.position); Step(archer,30);
        Check(Vector3.Distance(archer.transform.position,run.Player.position)>near+.2f,"Close player causes retreat");
        archer=Fixture(run); archer.transform.position=new Vector3(0,0,5);
        float far=Vector3.Distance(archer.transform.position,run.Player.position); Step(archer,30);
        Check(Vector3.Distance(archer.transform.position,run.Player.position)<far-.2f,"Distant player causes approach");
        archer=Fixture(run); Until(archer,SkeletonArcher.State.Aim); Step(archer,40);
        archer.TakeHit(1,Vector3.forward,2); Step(archer,20);
        Check(archer.ShotsFired==0,"Sword hit interrupts impending shot");
        // Actual run targeting and sword impact authority; no direct enemy health edits.
        archer=Fixture(run);
        for(int frame=0;frame<1200 && archer!=null && archer.gameObject.activeSelf;frame++)
        {
            run.Player.position=archer.transform.position+Vector3.back*.95f+Vector3.up*.65f;
            run.Tick(Vector2.zero,1f/60);
            if(run.Health.Current<50) run.Health.ResetHealth();
        }
        Check(archer==null || !archer.gameObject.activeSelf,"Existing stop-to-auto-target sword kills Archer");
        Check(run.EnemyCount==1 && run.Loot+run.PickupCount==1,"Archer death counts once and produces normal loot");
        for(int frame=0;frame<1600 && run.EnemyCount>0;frame++)
        {
            run.Player.position=run.FirstEnemyPosition+Vector3.back*.95f+Vector3.up*.65f;
            run.Tick(Vector2.zero,1f/60); if(run.Health.Current<50) run.Health.ResetHealth();
        }
        Check(run.EnemyCount==0 && run.ExitOpen && !run.GateActive,"Slime still dies and mixed room opens exit");
        // Destroy is deferred until the editor yields a frame. Visit each direct loot child
        // once instead of repeatedly selecting the first pending-destroy object.
        foreach(Transform loot in run.transform)
        {
            if(loot.name!="Loot" || !loot.gameObject.activeInHierarchy) continue;
            run.Player.position=loot.position;
            for(int frame=0;frame<90;frame++) run.Tick(Vector2.zero,1f/60);
        }
        Check(run.Loot==2 && run.PickupCount==0,"Both enemy loot pickups collect normally");
        run.Restart();
        for(int i=0;i<7;i++) { run.Health.Tick(1); run.Health.TryTakeDamage(15); }
        run.Tick(Vector2.zero,1f/60); Check(run.Failed,"Player death enters Run Failed");
        run.Restart(); Check(!run.Failed && run.Health.Current==100 && run.EnemyCount==2,"Restart restores health and mixed encounter");
        Check(run.GetComponent<FloatingJoystick>()!=null && run.Player.GetComponent<GoblinSwordAttack>()!=null,"Existing joystick and Sword Attack Animation v1 remain wired");
        Debug.Log("Skeleton Archer: ALL CHECKS PASSED");
    }
    static void Capture(LootGoblinRun run,string name)
    {
        var camera=run.ArenaCamera; var old=camera.targetTexture; var active=RenderTexture.active;
        const int width=1080,height=1920;
        var rt=new RenderTexture(width,height,24); var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt; run.PortraitCamera.RefreshViewport(width,height,new Rect(0,0,width,height)); run.PortraitCamera.TickForValidation(0f); camera.Render();
            RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,width,height),0,0); tex.Apply();
            File.WriteAllBytes("Logs/Archer-"+name+".png",tex.EncodeToPNG());
            // Pixel crop only: same camera, projection and pose, for checking local axes.
            var detail=new Texture2D(240,240,TextureFormat.RGB24,false);
            detail.SetPixels(tex.GetPixels(420,900,240,240)); detail.Apply();
            File.WriteAllBytes("Logs/ArcherDetail-"+name+".png",detail.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(detail);
        }
        finally { camera.targetTexture=old; RenderTexture.active=active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); run.PortraitCamera.RefreshViewport(); }
    }
}
