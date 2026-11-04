using UnityEngine;

public class VisualProjectile : MonoBehaviour
{
    private void Awake()
    {
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.time = 0.15f;
            trail.widthMultiplier = 0.2f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.02f;
            trail.emitting = true;
        }
    }

    private Vector3 direction;
    private float speed;

    public void Setup(Vector3 dir, float moveSpeed)
    {
        direction = dir.normalized;
        speed = moveSpeed;
        if (direction.sqrMagnitude > 0.001f) transform.forward = direction;
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }
}
