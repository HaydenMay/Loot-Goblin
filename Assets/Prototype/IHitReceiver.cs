using UnityEngine;

// Enemies opt into the shared hit contract while retaining ownership of their reaction.
public interface IHitReceiver
{
    void TakeHit(int damage, Vector3 hitDirection, float knockbackForce);
}
