using UnityEngine;

// A straight, immutable trajectory and a swept planar hit test match the run's
// existing XZ gameplay collision. No Rigidbody, homing or repeated-hit window.
public sealed class SkeletonArrow
{
    const float ProjectileDespawnPadding = .25f;
    readonly Transform visual;
    readonly Vector3 velocity;
    readonly int damage;
    float age;
    public SkeletonArrow(Transform visual,Vector3 direction,float speed,int damage)
    {
        this.visual=visual; velocity=direction.normalized*speed; this.damage=damage;
    }
    public bool Tick(LootGoblinRun run,float dt)
    {
        if(visual==null) return false;
        age+=dt;
        Vector3 a=visual.position+visual.forward*.8f, b=a+velocity*dt;
        foreach(var obstacle in run.ObstacleColliders)
            if(obstacle!=null && obstacle.enabled && obstacle.gameObject.activeInHierarchy &&
                LootGoblinRun.SegmentIntersectsBoundsXZ(a,b,obstacle.bounds,.07f)) return false;
        if(age>3.5f) return false;
        Vector3 p=run.Player.position; a.y=b.y=p.y=0;
        Vector3 segment=b-a;
        float t=segment.sqrMagnitude<.00001f ? 0 : Mathf.Clamp01(Vector3.Dot(p-a,segment)/segment.sqrMagnitude);
        if((p-(a+segment*t)).sqrMagnitude<=.46f*.46f)
        {
            run.Health.TryTakeDamage(damage); return false;
        }

        Bounds playableBounds=run.ArenaBounds;
        playableBounds.Expand(new Vector3(ProjectileDespawnPadding*2f,0,ProjectileDespawnPadding*2f));
        if(!playableBounds.Contains(new Vector3(b.x,playableBounds.center.y,b.z))) return false;
        visual.position+=velocity*dt; return true;
    }
    public void Dispose() { if(visual!=null) { visual.gameObject.SetActive(false); Object.Destroy(visual.gameObject); } }
}
