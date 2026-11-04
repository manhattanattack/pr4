using System;
using UnityEngine;

[Serializable]
public class WallSegmentData
{
    public float x;
    public float z;
    public float rot;
}

[Serializable]
public class InitWorldPacket
{
    public string type;
    public string id;
    public float[] spawnPos;
    public WallSegmentData[] walls;
}

public class MazeBuilder : MonoBehaviour
{
    [Header("Префабы модулей")]
    [SerializeField] private GameObject straightWallPrefab;
    [SerializeField] private GameObject pillarPrefab;

    [Header("Иерархия")]
    [SerializeField] private Transform mazeRoot;

    public event Action OnMazeBuildFinished;

    public void BuildFromNetworkData(WallSegmentData[] walls)
    {
        if (walls == null || walls.Length == 0) return;

        // Очистка старых объектов перед сборкой
        for (int i = mazeRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(mazeRoot.GetChild(i).gameObject);
        }

        // 1. Построение секций стен
        foreach (var w in walls)
        {
            Vector3 position = new Vector3(w.x, 0f, w.z);
            Quaternion rotation = Quaternion.Euler(0f, w.rot, 0f);

            Instantiate(straightWallPrefab, position, rotation, mazeRoot);
        }

        // 2. Расстановка колонн на стыках сетки
        if (pillarPrefab != null)
        {
            int cols = 12;
            int rows = 12;
            float cellSize = 4f;
            float offsetX = -(cols * cellSize) / 2f;
            float offsetZ = -(rows * cellSize) / 2f;

            for (int r = 0; r <= rows; r++)
            {
                for (int c = 0; c <= cols; c++)
                {
                    // Y = 1.5: у префаба колонны смещение высоты зашито в корневой объект
                    Vector3 pillarPos = new Vector3(offsetX + c * cellSize, 1.5f, offsetZ + r * cellSize);
                    Instantiate(pillarPrefab, pillarPos, Quaternion.identity, mazeRoot);
                }
            }
        }

        Debug.Log($"[MazeBuilder] Уровень построен. Стен: {walls.Length}");
        OnMazeBuildFinished?.Invoke();
    }
}