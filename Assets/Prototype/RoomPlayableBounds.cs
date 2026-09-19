using UnityEngine;

// Serialized room-authoring data. The backing BoxCollider stays disabled so it never
// becomes gameplay physics; LootGoblinRun and PortraitRoomCamera read its geometry only.
[DisallowMultipleComponent]
public sealed class RoomPlayableBounds : MonoBehaviour
{
    [SerializeField] BoxCollider volume;

    Bounds worldBounds;
    bool hasWorldBounds;

    public Bounds WorldBounds => worldBounds;
    public bool HasWorldBounds => hasWorldBounds;

    void Awake() => RebuildWorldBounds();

    void OnValidate()
    {
        ConfigureAsAuthoringVolume();
        RebuildWorldBounds();
    }

    public void Configure(BoxCollider authoredVolume)
    {
        volume = authoredVolume;
        ConfigureAsAuthoringVolume();
        RebuildWorldBounds();
    }

    public bool TryGetWorldBounds(out Bounds bounds)
    {
        if (!hasWorldBounds) RebuildWorldBounds();
        bounds = worldBounds;
        return hasWorldBounds;
    }

    void ConfigureAsAuthoringVolume()
    {
        if (volume == null) volume = GetComponent<BoxCollider>();
        if (volume == null) return;

        volume.isTrigger = true;
        volume.enabled = false;
    }

    void RebuildWorldBounds()
    {
        if (volume == null) volume = GetComponent<BoxCollider>();
        if (volume == null)
        {
            hasWorldBounds = false;
            worldBounds = default;
            return;
        }

        Vector3 center = volume.center;
        Vector3 extents = volume.size * .5f;
        bool initialized = false;
        Bounds calculated = default;

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 localPoint = center + Vector3.Scale(extents, new Vector3(x, y, z));
            Vector3 worldPoint = volume.transform.TransformPoint(localPoint);
            if (!initialized)
            {
                calculated = new Bounds(worldPoint, Vector3.zero);
                initialized = true;
            }
            else calculated.Encapsulate(worldPoint);
        }

        worldBounds = calculated;
        hasWorldBounds = initialized;
    }
}
