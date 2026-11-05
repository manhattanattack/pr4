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

    public void SetWeaponColor(string weapon)
    {
        Color color = weapon == "shotgun" ? new Color(1f, 0.58f, 0.18f) :
            weapon == "smg" ? new Color(0.25f, 0.95f, 0.72f) :
            weapon == "railgun" ? new Color(0.76f, 0.4f, 1f) : Color.cyan;
        MeshRenderer mesh = GetComponent<MeshRenderer>();
        if (mesh != null) mesh.material.color = color;
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.widthMultiplier = weapon == "railgun" ? 0.32f : 0.16f;
        }
        if (weapon == "shotgun") transform.localScale *= 0.6f;
        if (weapon == "railgun") transform.localScale *= 1.5f;
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
