using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Простые модели подбираемого оружия. Состояние приходит с сервера.
public class WeaponPickupManager : MonoBehaviour
{
    private class PickupView
    {
        public GameObject root;
        public Vector3 position;
        public TextMeshPro text;
    }

    private readonly Dictionary<string, PickupView> pickups = new Dictionary<string, PickupView>();

    public void UpdatePickups(WeaponPickupData[] data)
    {
        if (data == null) return;
        foreach (WeaponPickupData pickup in data)
        {
            if (!pickups.TryGetValue(pickup.id, out PickupView view))
            {
                view = CreatePickup(pickup);
                pickups.Add(pickup.id, view);
            }

            view.position = new Vector3(pickup.x, 0.85f, pickup.z);
            view.root.SetActive(pickup.available);
        }
    }

    private PickupView CreatePickup(WeaponPickupData data)
    {
        GameObject root = new GameObject("Pickup_" + data.weapon + "_" + data.id);
        root.transform.position = new Vector3(data.x, 0.85f, data.z);

        Color color = GetColor(data.weapon);
        AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0, -0.48f, 0),
            new Vector3(0.8f, 0.06f, 0.8f), color * 0.55f);
        AddPart(root.transform, PrimitiveType.Cube, Vector3.zero,
            new Vector3(1.0f, 0.25f, 0.3f), color);
        AddPart(root.transform, PrimitiveType.Cube, new Vector3(-0.27f, -0.24f, 0),
            new Vector3(0.18f, 0.38f, 0.24f), color * 0.65f);

        if (data.weapon == "shotgun")
        {
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.66f, 0.07f, 0.13f),
                new Vector3(0.78f, 0.09f, 0.12f), Color.gray);
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.66f, 0.07f, -0.13f),
                new Vector3(0.78f, 0.09f, 0.12f), Color.gray);
        }
        else if (data.weapon == "smg")
        {
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.47f, 0, 0),
                new Vector3(0.58f, 0.11f, 0.12f), Color.gray);
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.08f, -0.3f, 0),
                new Vector3(0.18f, 0.37f, 0.18f), color * 0.8f);
        }
        else if (data.weapon == "railgun")
        {
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.67f, 0.17f, 0.2f),
                new Vector3(1f, 0.09f, 0.13f), Color.cyan);
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.67f, -0.17f, -0.2f),
                new Vector3(1f, 0.09f, 0.13f), Color.cyan);
        }

        GameObject labelObject = new GameObject("WeaponName");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
        text.text = data.weapon == "shotgun" ? "SHOTGUN" :
            data.weapon == "smg" ? "SMG" : "RAILGUN";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 2.5f;
        text.rectTransform.sizeDelta = new Vector2(3.4f, 0.7f);
        text.color = color;

        return new PickupView { root = root, position = root.transform.position, text = text };
    }

    private static GameObject AddPart(Transform parent, PrimitiveType type,
        Vector3 position, Vector3 scale, Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = color;
        }
        return obj;
    }

    private static Color GetColor(string weapon)
    {
        if (weapon == "shotgun") return new Color(1f, 0.58f, 0.18f);
        if (weapon == "smg") return new Color(0.28f, 0.95f, 0.45f);
        return new Color(0.76f, 0.39f, 1f);
    }

    private void Update()
    {
        Camera cam = Camera.main;
        foreach (PickupView view in pickups.Values)
        {
            if (!view.root.activeSelf) continue;
            view.root.transform.position = view.position +
                Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.12f);
            view.root.transform.Rotate(0f, 35f * Time.deltaTime, 0f);
            if (cam != null && view.text != null)
                view.text.transform.rotation = Quaternion.LookRotation(
                    view.text.transform.position - cam.transform.position);
        }
    }

    private void OnDestroy()
    {
        foreach (PickupView view in pickups.Values)
            if (view.root != null) Destroy(view.root);
        pickups.Clear();
    }
}
