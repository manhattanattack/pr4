#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.Management;

[InitializeOnLoad]
public static class PR5QuestSetup
{
    private const string PrefabPath = "Assets/Resources/VR_LocalPlayer.prefab";
    private const string SettingsPath = "Assets/Resources/QuestConnectionSettings.asset";

    static PR5QuestSetup()
    {
        // Первое открытие проекта создаёт реальные prefab и LAN-config в Resources.
        EditorApplication.delayCall += EnsureAssets;
    }

    [MenuItem("Tools/PR5/1. Create VR prefab and Quest settings")]
    public static void EnsureAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject rig = VRRigFactory.Create();
            PrefabUtility.SaveAsPrefabAsset(rig, PrefabPath);
            Object.DestroyImmediate(rig);
            Debug.Log("[PR5] Создан VR_LocalPlayer.prefab (XR Origin, клинок, кисти, WristCanvas).");
        }
        if (AssetDatabase.LoadAssetAtPath<QuestConnectionSettings>(SettingsPath) == null)
        {
            var settings = ScriptableObject.CreateInstance<QuestConnectionSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            Debug.Log("[PR5] Укажи LAN IP ПК в Resources/QuestConnectionSettings.asset перед APK.");
        }
        BuildNetworkPlayerHands();
        AssetDatabase.SaveAssets();
    }

    private static void BuildNetworkPlayerHands()
    {
        const string path = "Assets/Prefabs/NetworkPlayer.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;
        var player = prefab.GetComponent<NetworkPlayer>();
        var check = new SerializedObject(player);
        if (check.FindProperty("leftHandRoot").objectReferenceValue != null) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            NetworkPlayer np = root.GetComponent<NetworkPlayer>();
            var so = new SerializedObject(np);
            Transform left = AddSphere(root.transform, "LeftHand_Mesh", new Vector3(-0.35f, 1.1f, 0.2f));
            Transform right = AddSphere(root.transform, "RightHand_Mesh", new Vector3(0.35f, 1.1f, 0.2f));
            so.FindProperty("leftHandRoot").objectReferenceValue = left;
            so.FindProperty("rightHandRoot").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform AddSphere(Transform parent, string name, Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = position;
        sphere.transform.localScale = Vector3.one * 0.12f;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.SetActive(false);
        return sphere.transform;
    }

    [MenuItem("Tools/PR5/2. Configure Android Quest 3 (OpenXR/Vulkan/ARM64)")]
    public static void ConfigureAndroid()
    {
        EnsureAssets();
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        XRGeneralSettings general =
            XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (general != null && general.Manager != null)
        {
            if (!XRPackageMetadataStore.AssignLoader(
                general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                Debug.LogWarning("[PR5] OpenXR loader проверь в XR Plug-in Management > Android.");
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
        }
        else Debug.LogWarning("[PR5] Выбери OpenXR в XR Plug-in Management > Android.");

        OpenXRSettings openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (openXR != null)
        {
            foreach (OpenXRFeature feature in openXR.GetFeatures<OpenXRFeature>())
            {
                string type = feature.GetType().Name;
                if (type.Contains("OculusTouchControllerProfile") ||
                    type.Contains("MetaQuestTouchProControllerProfile") ||
                    type.Contains("MetaQuestFeature"))
                    feature.enabled = true;
            }
            EditorUtility.SetDirty(openXR);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[PR5] Настройки Android сохранены. Проверь OpenXR Validation и Quest Touch profiles перед Build And Run.");
        EditorUtility.DisplayDialog("PR5 / Quest 3",
            "Vulkan, Linear, IL2CPP, ARM64, API 29 и ASTC настроены.\n" +
            "Проверь Android/OpenXR profiles, укажи IP сервера в QuestConnectionSettings и переключи Build Profile на Android.",
            "OK");
    }
}
#endif
