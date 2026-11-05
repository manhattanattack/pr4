using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class WeaponPickupData
{
    public string id;
    public string weapon;
    public float x, z;
    public bool available;
}

// Оружие видно всем игрокам. Сервер решает, кто его подобрал.
public class ArenaWeaponPickups : MonoBehaviour
{
    private class PickupView
    {
        public GameObject root;
        public Transform model;
        public GameObject label;
        public bool available;
    }

    private readonly Dictionary<string, PickupView> views =
        new Dictionary<string, PickupView>();

    public void SetPickups(WeaponPickupData[] pickups)
    {
        if (pickups == null) return;
        foreach (WeaponPickupData data in pickups)
        {
            if (string.IsNullOrEmpty(data.id)) continue;
            if (!views.TryGetValue(data.id, out PickupView view))
            {
                view = CreatePickup(data);
                views.Add(data.id, view);
            }
            view.available = data.available;
            if (view.model != null) view.model.gameObject.SetActive(data.available);
            if (view.label != null) view.label.SetActive(data.available);
        }
    }

    private PickupView CreatePickup(WeaponPickupData data)
    {
        GameObject root = new GameObject("Pickup_" + data.id);
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(data.x, 0f, data.z);
        Color color = WeaponColor(data.weapon);

        // Подставка остаётся даже после подбора, чтобы было понятно, где оружие.
        GameObject pedestal = CreatePart("Pedestal", PrimitiveType.Cylinder,
            root.transform, new Vector3(0f, 0.07f, 0f), new Vector3(1.5f, 0.07f, 1.5f),
            new Color(0.16f, 0.22f, 0.28f));
        GameObject model = new GameObject("SpinningWeapon");
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = new Vector3(0f, 1.1f, 0f);

        if (data.weapon == "shotgun")
        {
            CreatePart("Body", PrimitiveType.Cube, model.transform,
                Vector3.zero, new Vector3(1.2f, 0.32f, 0.42f), color);
            CreatePart("Barrel", PrimitiveType.Cylinder, model.transform,
                new Vector3(0.8f, 0f, 0f), new Vector3(0.14f, 0.65f, 0.14f), Color.gray)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else if (data.weapon == "smg")
        {
            CreatePart("Body", PrimitiveType.Cube, model.transform,
                Vector3.zero, new Vector3(0.85f, 0.33f, 0.3f), color);
            CreatePart("Magazine", PrimitiveType.Cube, model.transform,
                new Vector3(-0.1f, -0.32f, 0f), new Vector3(0.24f, 0.48f, 0.26f), Color.gray);
            CreatePart("Muzzle", PrimitiveType.Cube, model.transform,
                new Vector3(0.56f, 0.02f, 0f), new Vector3(0.35f, 0.14f, 0.14f), color);
        }
        else
        {
            CreatePart("Railgun", PrimitiveType.Cube, model.transform,
                Vector3.zero, new Vector3(1.55f, 0.2f, 0.3f), color);
            CreatePart("Core", PrimitiveType.Cube, model.transform,
                new Vector3(0f, 0.22f, 0f), new Vector3(0.55f, 0.15f, 0.4f), Color.white);
        }

        GameObject label = new GameObject("Name");
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = data.weapon == "shotgun" ? "SHOTGUN" :
            data.weapon == "smg" ? "SMG" : "RAILGUN";
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = new Vector2(3.5f, 1f);

        return new PickupView {root = root, model = model.transform,
            label = label, available = true};
    }

    private GameObject CreatePart(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = color;
        }
        return part;
    }

    private static Color WeaponColor(string weapon)
    {
        if (weapon == "shotgun") return new Color(1f, 0.52f, 0.15f);
        if (weapon == "smg") return new Color(0.2f, 0.95f, 0.72f);
        return new Color(0.8f, 0.44f, 1f);
    }

    private void Update()
    {
        foreach (PickupView view in views.Values)
        {
            if (!view.available) continue;
            if (view.model != null)
            {
                view.model.Rotate(0f, Time.deltaTime * 65f, 0f, Space.World);
                view.model.localPosition = new Vector3(0f,
                    1.05f + Mathf.Sin(Time.time * 2.5f) * 0.13f, 0f);
            }
            if (view.label != null && Camera.main != null)
                view.label.transform.rotation = Quaternion.LookRotation(
                    view.label.transform.position - Camera.main.transform.position);
        }
    }

    private void OnDestroy()
    {
        foreach (PickupView view in views.Values)
            if (view.root != null) Destroy(view.root);
        views.Clear();
    }
}
