using UnityEngine;

// A straight, immutable trajectory and a swept planar hit test match the run's
// existing XZ gameplay collision. No Rigidbody, homing or repeated-hit window.
public sealed class SkeletonArrow
{
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
        if(Mathf.Abs(b.x)>5.3f || Mathf.Abs(b.z)>7.7f || age>3.5f) return false;
        Vector3 p=run.Player.position; a.y=b.y=p.y=0;
        Vector3 segment=b-a;
        float t=segment.sqrMagnitude<.00001f ? 0 : Mathf.Clamp01(Vector3.Dot(p-a,segment)/segment.sqrMagnitude);
        if((p-(a+segment*t)).sqrMagnitude<=.46f*.46f)
        {
            run.Health.TryTakeDamage(damage); return false;
        }
        visual.position+=velocity*dt; return true;
    }
    public void Dispose() { if(visual!=null) { visual.gameObject.SetActive(false); Object.Destroy(visual.gameObject); } }
}
