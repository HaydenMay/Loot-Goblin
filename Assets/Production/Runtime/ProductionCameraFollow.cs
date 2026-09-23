using UnityEngine;

/// <summary>Compact third-person style camera follow used by the production foundation scene.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class ProductionCameraFollow : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] Transform target;
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 7.5f, -7.5f);
    [SerializeField] Vector3 lookAtOffset = new Vector3(0f, .85f, 0f);
    [SerializeField, Min(.001f)] float followSmoothTime = .16f;

    Vector3 followVelocity;

    public Camera TargetCamera => targetCamera;
    public Transform Target => target;

    void Reset() => targetCamera = GetComponent<Camera>();

    void Awake()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Max(0f, Time.deltaTime);
        transform.position = Vector3.SmoothDamp(transform.position,
            target.position + worldOffset, ref followVelocity, followSmoothTime,
            Mathf.Infinity, dt);
        transform.rotation = Quaternion.LookRotation(target.position + lookAtOffset - transform.position,
            Vector3.up);
    }

    public void SetTarget(Transform followTarget, bool snap = true)
    {
        target = followTarget;
        if (snap && target != null)
        {
            transform.position = target.position + worldOffset;
            transform.rotation = Quaternion.LookRotation(target.position + lookAtOffset - transform.position,
                Vector3.up);
            followVelocity = Vector3.zero;
        }
    }
}
