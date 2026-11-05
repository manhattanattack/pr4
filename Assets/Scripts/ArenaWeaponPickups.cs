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

// Показываем оружие из серверного списка. Подбор считает только Node.js.
public class ArenaWeaponPickups : MonoBehaviour
{
    private class PickupVisual
    {
        public GameObject obj;
        public Transform model;
        public Transform label;
        public float height;
    }

    private readonly Dictionary<string, PickupVisual> pickups =
        new Dictionary<string, PickupVisual>();

    public void SetPickups(WeaponPickupData[] data)
    {
        if (data == null) return;
        foreach (WeaponPickupData item in data)
        {
            if (!pickups.TryGetValue(item.id, out PickupVisual p))
            {
                p = Create(item);
                pickups.Add(item.id, p);
            }
            if (p.obj != null) p.obj.SetActive(item.available);
        }
    }

    private PickupVisual Create(WeaponPickupData data)
    {
        GameObject root = new GameObject("Pickup_" + data.weapon);
        root.transform.SetParent(transform);
        root.transform.position = new Vector3(data.x, 0.8f, data.z);

        GameObject shape = new GameObject("WeaponShape");
        shape.transform.SetParent(root.transform, false);

        Color color = Color.yellow;
        string label = "SHOTGUN";
        if (data.weapon == "shotgun") color = new Color(1f, 0.55f, 0.16f);
        if (data.weapon == "smg")
        {
            color = new Color(0.69f, 0.39f, 0.98f);
            label = "SMG";
        }
        if (data.weapon == "railgun")
        {
            color = new Color(0.28f, 0.88f, 1f);
            label = "RAILGUN";
        }

        float length = data.weapon == "railgun" ? 1.25f :
            data.weapon == "shotgun" ? 0.85f : 0.58f;
        AddPart(shape.transform, PrimitiveType.Cube,
            new Vector3(0f, 0.12f, 0f),
            new Vector3(length, 0.25f, 0.29f), color);
        AddPart(shape.transform, PrimitiveType.Cylinder,
            new Vector3(length * 0.55f, 0.12f, 0f),
            new Vector3(0.09f, length * 0.38f, 0.09f), Color.gray,
            new Vector3(0f, 0f, 90f));
        AddPart(shape.transform, PrimitiveType.Cube,
            new Vector3(-0.12f, -0.12f, 0f),
            new Vector3(0.17f, 0.30f, 0.18f), color * 0.72f);
        AddPart(root.transform, PrimitiveType.Cylinder,
            new Vector3(0f, -0.55f, 0f),
            new Vector3(0.85f, 0.055f, 0.85f), color * 0.7f);

        GameObject textObj = new GameObject("PickupName");
        textObj.transform.SetParent(root.transform, false);
        textObj.transform.localPosition = new Vector3(0f, 0.78f, 0f);
        textObj.transform.localScale = Vector3.one * 0.15f;
        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = label;
        tmp.fontSize = 5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        return new PickupVisual {
            obj = root, model = shape.transform, label = textObj.transform,
            height = root.transform.position.y
        };
    }

    private static void AddPart(Transform parent, PrimitiveType kind,
        Vector3 pos, Vector3 scale, Color color, Vector3 rotation = default)
    {
        GameObject part = GameObject.CreatePrimitive(kind);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        part.transform.localEulerAngles = rotation;
        Collider col = part.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = color;
        }
    }

    private void Update()
    {
        foreach (PickupVisual p in pickups.Values)
        {
            if (p.obj == null || !p.obj.activeSelf) continue;
            Vector3 pos = p.obj.transform.position;
            pos.y = p.height + Mathf.Sin(Time.time * 2.6f) * 0.17f;
            p.obj.transform.position = pos;
            p.model.Rotate(Vector3.up, 65f * Time.deltaTime, Space.World);
            if (p.label != null && Camera.main != null)
                p.label.rotation = Camera.main.transform.rotation;
        }
    }

    private void OnDestroy()
    {
        foreach (PickupVisual p in pickups.Values)
            if (p.obj != null) Destroy(p.obj);
        pickups.Clear();
    }
}
