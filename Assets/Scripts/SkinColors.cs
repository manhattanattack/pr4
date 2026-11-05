using UnityEngine;

// Цвета одинаковые в меню и в сетевых персонажах.
public static class SkinColors
{
    public const int Count = 6;

    private static readonly string[] colorNames =
        { "ГОЛУБОЙ", "КРАСНЫЙ", "ЗЕЛЁНЫЙ", "ЖЁЛТЫЙ", "ФИОЛЕТОВЫЙ", "СИНИЙ" };

    private static readonly Color[] colors =
    {
        new Color(0.23f, 0.76f, 0.94f),
        new Color(0.92f, 0.31f, 0.32f),
        new Color(0.25f, 0.81f, 0.51f),
        new Color(0.97f, 0.72f, 0.27f),
        new Color(0.68f, 0.41f, 0.94f),
        new Color(0.29f, 0.49f, 0.94f)
    };

    public static Color Get(int index)
    {
        if (index < 0 || index >= Count) index = 0;
        return colors[index];
    }

    public static string GetName(int index)
    {
        if (index < 0 || index >= Count) index = 0;
        return colorNames[index];
    }

    public static string GetHtml(int index)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(Get(index));
    }
}
