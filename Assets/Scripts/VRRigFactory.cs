using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

// Собирает полноценный XR Origin и WristCanvas без зависимости от Samples XRI.
// Используется при отсутствии сгенерированного prefab и в редакторе (Tools > PR5).
public static class VRRigFactory
{
    public static GameObject Create()
    {
        var root = new GameObject("VR_LocalPlayer");
        CharacterController cc = root.AddComponent<CharacterController>();
        cc.slopeLimit = 45f; cc.stepOffset = 0.3f; cc.skinWidth = 0.05f;
        cc.minMoveDistance = 0f; cc.radius = 0.3f; cc.height = 1.8f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        XROrigin xrOrigin = root.AddComponent<XROrigin>();
        xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        var offset = new GameObject("Camera Offset").transform;
        offset.SetParent(root.transform, false);

        var head = new GameObject("Main Camera").transform;
        head.SetParent(offset, false);
        head.gameObject.tag = "MainCamera";
        Camera camera = head.gameObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        head.gameObject.AddComponent<AudioListener>();
        head.gameObject.AddComponent<VRTrackedNode>().Configure(XRNode.Head);
        xrOrigin.Camera = camera;
        xrOrigin.CameraFloorOffsetObject = offset.gameObject;

        Transform left = Hand(offset, "Left Controller", XRNode.LeftHand, new Color(0.1f,0.8f,1f));
        Transform right = Hand(offset, "Right Controller", XRNode.RightHand, new Color(1f,0.75f,0.2f));

        GameObject sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sword.name = "VR_Sword";
        sword.transform.SetParent(right, false);
        sword.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        sword.transform.localEulerAngles = new Vector3(45f, 0f, 0f);
        sword.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
        sword.GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody rigid = sword.AddComponent<Rigidbody>();
        rigid.useGravity = false; rigid.isKinematic = true;
        VRMeleeWeapon weapon = sword.AddComponent<VRMeleeWeapon>();

        BuildWrist(left);
        VRPlayerController controller = root.AddComponent<VRPlayerController>();
        controller.Configure(head, left, right, weapon);
        return root;
    }

    private static Transform Hand(Transform parent, string name, XRNode node, Color color)
    {
        var hand = new GameObject(name).transform;
        hand.SetParent(parent, false);
        hand.gameObject.AddComponent<VRTrackedNode>().Configure(node);
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "HandMesh";
        sphere.transform.SetParent(hand, false);
        sphere.transform.localScale = Vector3.one * 0.08f;
        Collider collider = sphere.GetComponent<Collider>();
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
        // Материалы шариков создаются движком, без Asset Store.
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (Application.isPlaying)
        {
            renderer.material.color = color;
        }
        return hand;
    }

    private static RectTransform Element(string name, RectTransform parent, Vector2 size, Vector2 pos)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        return rect;
    }

    private static void BuildWrist(Transform leftHand)
    {
        GameObject obj = new GameObject("WristCanvas", typeof(RectTransform), typeof(Canvas));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(leftHand, false);
        rect.localPosition = new Vector3(0f, 0.06f, -0.05f);
        rect.localEulerAngles = new Vector3(60f, 0f, 0f);
        rect.localScale = Vector3.one * 0.0006f;
        rect.sizeDelta = new Vector2(300f, 200f);
        Canvas canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        var panel = Element("Background", rect, new Vector2(300f, 200f), Vector2.zero);
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.07f, 0.15f, 0.85f);

        RectTransform hpBase = Element("WristHealthSlider", rect,
            new Vector2(125f, 20f), new Vector2(-78f, 38f));
        Slider slider = hpBase.gameObject.AddComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0; slider.maxValue = 100f; slider.value = 100f;
        RectTransform sliderBackground = Element("SliderBackground", hpBase,
            new Vector2(125f, 20f), Vector2.zero);
        sliderBackground.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f);
        RectTransform fill = Element("Fill", hpBase, new Vector2(120f, 15f), Vector2.zero);
        fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(1, 1);
        fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);
        fill.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.9f, 0.38f);
        slider.fillRect = fill;

        RectTransform hpRect = Element("HealthText", rect,
            new Vector2(125f, 25f), new Vector2(-78f, 10f));
        TextMeshProUGUI text = hpRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = "100 / 100"; text.fontSize = 18f; text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform minimap = Element("WristMinimapDisplay", rect,
            new Vector2(120f, 120f), new Vector2(70f, -10f));
        RawImage raw = minimap.gameObject.AddComponent<RawImage>();
        raw.color = Color.white;
        RectTransform markers = Element("MarkersLayer", minimap,
            new Vector2(120f, 120f), Vector2.zero);
        var hud = obj.AddComponent<VRWristHUD>();
        hud.Configure(slider, text, raw, markers);
    }
}
