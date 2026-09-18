using System;
using System.Collections.Generic;
using UnityEngine;

// Run-driven, like SlimeMotion: no Animator, physics damage, or independent Update.
public sealed class SkeletonArcher : MonoBehaviour, IHitReceiver
{
    [Serializable]
    public sealed class Settings
    {
        [Min(1)] public int health = 3;
        [Min(1)] public int projectileDamage = 15;
        [Min(.1f)] public float preferredRange = 4.6f;
        [Min(.1f)] public float retreatDistance = 3f;
        [Min(.1f)] public float movementSpeed = 1.35f;
        [Min(.1f)] public float drawDuration = 1.05f;
        [Min(.1f)] public float aimDuration = .85f;
        [Min(.05f)] public float recoilDuration = .28f;
        [Min(.1f)] public float projectileSpeed = 6f;
    }
    public enum State { Reposition, DrawArrow, Aim, Fire, Recovery, Dead }
    public State CurrentState { get; private set; }
    public float StateTime => stateTime;
    public bool DeathFinished => CurrentState == State.Dead && stateTime >= .32f;
    public int ShotsFired { get; private set; }
    public int ActiveArrowCount => arrows.Count;
    public SkeletonArcherRig Rig => rig;
    public Settings Tuning => tuning;
    [SerializeField] Settings tuning = new();
    readonly List<SkeletonArrow> arrows = new(3);
    LootGoblinRun run;
    SkeletonArcherRig rig;
    Vector3 knockback, shotDirection;
    float stateTime, walkTime, hitStun;

    public void Initialize(LootGoblinRun owner,Settings settings)
    {
        run=owner; tuning=settings; rig=new SkeletonArcherRig(transform); Enter(State.Reposition);
    }
    void Enter(State state) { CurrentState=state; stateTime=0; }
    public void Tick(float dt)
    {
        if(rig==null) return;
        stateTime+=dt;
        if(CurrentState==State.Dead) { rig.Collapse(Mathf.Clamp01(stateTime/.32f)); return; }
        for(int i=arrows.Count-1;i>=0;i--)
            if(!arrows[i].Tick(run,dt)) { arrows[i].Dispose(); arrows.RemoveAt(i); }
        hitStun=Mathf.Max(0,hitStun-dt);
        transform.position=run.Resolve(transform.position+knockback*dt,.36f);
        knockback=Vector3.MoveTowards(knockback,Vector3.zero,14*dt);
        Vector3 delta=run.Player.position-transform.position; delta.y=0;
        float distance=delta.magnitude;
        // Last .2 seconds commit the aim direction, then firing never tracks again.
        if(CurrentState!=State.Fire && CurrentState!=State.Recovery &&
            (CurrentState!=State.Aim || stateTime<tuning.aimDuration-.2f) && delta.sqrMagnitude>.001f)
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),360*dt);
        switch(CurrentState)
        {
            case State.Reposition:
                bool visible=run.ClearSight(transform.position,run.Player.position);
                if(hitStun<=0 && ((distance>=tuning.retreatDistance && distance<=tuning.preferredRange+.5f && visible) || stateTime>=1.4f && visible))
                    Enter(State.DrawArrow);
                else
                {
                    Vector3 direction=distance<tuning.retreatDistance ? -delta.normalized : delta.normalized;
                    if(!visible && distance<=tuning.preferredRange+.5f) direction=Vector3.Cross(Vector3.up,delta.normalized);
                    Move(direction,dt);
                }
                rig.Pose(0,0,0,walkTime,false);
                break;
            case State.DrawArrow:
                float reload=Mathf.Clamp01(stateTime/Mathf.Max(.1f,tuning.drawDuration));
                rig.Pose(reload,0,0,0,reload>.48f);
                if(reload>=1) Enter(State.Aim);
                break;
            case State.Aim:
                float aim=Mathf.Clamp01(stateTime/Mathf.Max(.1f,tuning.aimDuration));
                rig.Pose(1,aim,0,0,true);
                if(aim>=1)
                {
                    if(!run.ClearSight(transform.position,transform.position+transform.forward*distance)) { Enter(State.Reposition); break; }
                    shotDirection=transform.forward;
                    // Keep projectile and visible held arrow collinear with the committed aim.
                    rig.arrow.rotation=Quaternion.LookRotation(shotDirection);
                    var visual=Instantiate(rig.arrow.gameObject,rig.arrow.position,rig.arrow.rotation,run.transform).transform;
                    visual.name="Skeleton arrow projectile"; visual.gameObject.SetActive(true);
                    arrows.Add(new SkeletonArrow(visual,shotDirection,tuning.projectileSpeed,tuning.projectileDamage));
                    ShotsFired++; Enter(State.Fire); rig.arrow.gameObject.SetActive(false);
                }
                break;
            case State.Fire:
                rig.Pose(1,1,1,0,false); Enter(State.Recovery); break;
            case State.Recovery:
                float recovery=Mathf.Clamp01(stateTime/Mathf.Max(.05f,tuning.recoilDuration));
                rig.Pose(1,1-recovery,1-recovery,0,false);
                if(recovery>=1)
                    Enter(distance<tuning.retreatDistance || distance>tuning.preferredRange+.5f || !run.ClearSight(transform.position,run.Player.position) ? State.Reposition : State.DrawArrow);
                break;
        }
    }
    void Move(Vector3 direction,float dt)
    {
        foreach(var obstacle in run.ObstacleColliders)
        {
            if(obstacle==null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy) continue;
            Vector3 ahead=transform.position+direction*1.2f;
            if(!LootGoblinRun.SegmentIntersectsBoundsXZ(transform.position,ahead,obstacle.bounds,.48f)) continue;
            Vector3 offset=transform.position-obstacle.bounds.center; offset.y=0;
            Vector3 tangent=Vector3.Cross(Vector3.up,offset.normalized);
            if(Vector3.Dot(tangent,direction)<0) tangent=-tangent;
            direction=(tangent+offset.normalized*.35f).normalized; break;
        }
        Vector3 before=transform.position;
        transform.position=run.Resolve(before+direction*(tuning.movementSpeed*dt),.36f);
        walkTime+=Vector3.Distance(before,transform.position)*7;
    }
    public void TakeHit(int damage,Vector3 direction,float force)
    {
        if(CurrentState==State.Dead) return;
        direction.y=0; knockback+=direction.normalized*force; hitStun=.22f;
        // A sword hit interrupts the shot and requires a fresh full reload.
        Enter(State.Reposition); rig.arrow.gameObject.SetActive(false);
    }
    public void BeginDeath()
    {
        if(CurrentState==State.Dead) return;
        Enter(State.Dead); ClearArrows(); rig.arrow.gameObject.SetActive(false);
    }
    void ClearArrows() { foreach(var arrow in arrows) arrow.Dispose(); arrows.Clear(); }
    void OnDestroy() { ClearArrows(); rig?.Dispose(); }
}
