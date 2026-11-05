using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Один шрифт с русскими буквами для меню и игрового интерфейса.
public static class GameFont
{
    private static TMP_FontAsset fontAsset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (fontAsset == null)
        {
            Font fontFile = Resources.Load<Font>("Fonts/Ubuntu-Regular");
            if (fontFile == null)
            {
                Debug.LogError("[ШРИФТ] Не найден Ubuntu-Regular.ttf в Resources/Fonts.");
                return;
            }

            fontAsset = TMP_FontAsset.CreateFontAsset(fontFile);
            if (fontAsset == null)
            {
                Debug.LogError("[ШРИФТ] Не удалось создать TMP Font Asset.");
                return;
            }
            fontAsset.name = "Ubuntu Russian Dynamic";

            // Для новых объектов, которые создаются после загрузки сцены.
            if (TMP_Settings.fallbackFontAssets != null &&
                !TMP_Settings.fallbackFontAssets.Contains(fontAsset))
                TMP_Settings.fallbackFontAssets.Add(fontAsset);
        }

        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text text in texts)
        {
            text.font = fontAsset;
            text.fontSharedMaterial = fontAsset.material;
            text.SetAllDirty();
        }
    }
}
