using System.Collections.Generic;
using UnityEngine;

// Client-side only. The server never simulates corpses, so each player
// sees their own slightly different, deliberately over-the-top death physics.
public static class ChaosRagdoll
{
    private const int MaxCorpses = 8;
    private const float CorpseLifetime = 7f;
    private static readonly Queue<GameObject> corpses = new Queue<GameObject>();

    public static void Spawn(Vector3 feetPosition, Quaternion facing, Color color, bool explosive)
    {
        GameObject root = new GameObject("ChaosRagdoll");
        root.transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));

        Rigidbody torso = CreatePart(root.transform, "Torso", PrimitiveType.Capsule,
            new Vector3(0f, 1.26f, 0f), new Vector3(0.54f, 0.60f, 0.42f), color, 3f);
        Rigidbody head = CreatePart(root.transform, "Head", PrimitiveType.Sphere,
            new Vector3(0f, 2.04f, 0f), Vector3.one * 0.46f, Color.Lerp(color, Color.white, 0.24f), 0.8f);
        Rigidbody leftArm = CreatePart(root.transform, "LeftArm", PrimitiveType.Capsule,
            new Vector3(-0.48f, 1.33f, 0f), new Vector3(0.22f, 0.43f, 0.22f), color, 0.6f);
        Rigidbody rightArm = CreatePart(root.transform, "RightArm", PrimitiveType.Capsule,
            new Vector3(0.48f, 1.33f, 0f), new Vector3(0.22f, 0.43f, 0.22f), color, 0.6f);
        Rigidbody leftLeg = CreatePart(root.transform, "LeftLeg", PrimitiveType.Capsule,
            new Vector3(-0.2f, 0.53f, 0f), new Vector3(0.27f, 0.52f, 0.27f), color, 1f);
        Rigidbody rightLeg = CreatePart(root.transform, "RightLeg", PrimitiveType.Capsule,
            new Vector3(0.2f, 0.53f, 0f), new Vector3(0.27f, 0.52f, 0.27f), color, 1f);

        Attach(head, torso, root.transform.TransformPoint(new Vector3(0f, 1.78f, 0f)));
        Attach(leftArm, torso, root.transform.TransformPoint(new Vector3(-0.35f, 1.59f, 0f)));
        Attach(rightArm, torso, root.transform.TransformPoint(new Vector3(0.35f, 1.59f, 0f)));
        Attach(leftLeg, torso, root.transform.TransformPoint(new Vector3(-0.2f, 0.96f, 0f)));
        Attach(rightLeg, torso, root.transform.TransformPoint(new Vector3(0.2f, 0.96f, 0f)));

        // Corpses should bounce off the arena, not block player movement.
        CharacterController[] controllers = Object.FindObjectsOfType<CharacterController>();
        foreach (Rigidbody part in new[] {torso, head, leftArm, rightArm, leftLeg, rightLeg})
        {
            Collider collider = part.GetComponent<Collider>();
            foreach (CharacterController controller in controllers)
                if (controller != null) Physics.IgnoreCollision(collider, controller, true);
        }

        Vector3 blast = new Vector3(Random.Range(-6f, 6f),
            explosive ? Random.Range(13f, 17f) : Random.Range(6f, 10f),
            Random.Range(-6f, 6f));
        foreach (Rigidbody part in new[] {torso, head, leftArm, rightArm, leftLeg, rightLeg})
        {
            part.AddForce(blast * Random.Range(0.7f, 1.2f), ForceMode.VelocityChange);
            part.AddTorque(Random.onUnitSphere * Random.Range(6f, 15f), ForceMode.VelocityChange);
        }

        corpses.Enqueue(root);
        Object.Destroy(root, CorpseLifetime);
        while (corpses.Count > MaxCorpses)
        {
            GameObject oldest = corpses.Dequeue();
            if (oldest != null) Object.Destroy(oldest);
        }
    }

    private static Rigidbody CreatePart(Transform parent, string name, PrimitiveType primitive,
        Vector3 localPosition, Vector3 localScale, Color color, float mass)
    {
        GameObject piece = GameObject.CreatePrimitive(primitive);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = color;
        Rigidbody body = piece.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        return body;
    }

    private static void Attach(Rigidbody part, Rigidbody torso, Vector3 anchor)
    {
        CharacterJoint joint = part.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = torso;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = part.transform.InverseTransformPoint(anchor);
        joint.connectedAnchor = torso.transform.InverseTransformPoint(anchor);

        SoftJointLimit low = joint.lowTwistLimit;
        low.limit = -25f;
        joint.lowTwistLimit = low;
        SoftJointLimit high = joint.highTwistLimit;
        high.limit = 25f;
        joint.highTwistLimit = high;
        SoftJointLimit swing = joint.swing1Limit;
        swing.limit = 65f;
        joint.swing1Limit = swing;
        joint.swing2Limit = swing;
        joint.enableCollision = false;
    }
}
