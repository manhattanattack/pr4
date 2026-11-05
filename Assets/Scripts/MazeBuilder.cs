using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WallSegmentData
{
    public float x;
    public float z;
    public float rot;
}

[Serializable]
public class ArenaCoverData
{
    public float x, z;
    public float sx, sz;
    public float height;
    public string kind;
}

[Serializable]
public class InitWorldPacket
{
    public string type;
    public string id;
    public float[] spawnPos;
    public float shieldSeconds;
    public WallSegmentData[] walls;
    public ArenaCoverData[] cover;
    public int cols, rows;
    public float cellSize;
}

public class MazeBuilder : MonoBehaviour
{
    [Header("Префабы модулей")]
    [SerializeField] private GameObject straightWallPrefab;
    [SerializeField] private GameObject pillarPrefab;

    [Header("Иерархия")]
    [SerializeField] private Transform mazeRoot;

    public event Action OnMazeBuildFinished;

    public void BuildFromNetworkData(WallSegmentData[] walls, ArenaCoverData[] cover,
        int cols, int rows, float cellSize)
    {
        if (walls == null || mazeRoot == null) return;
        if (cols <= 0) cols = 24;
        if (rows <= 0) rows = 24;
        if (cellSize <= 0) cellSize = 4f;

        for (int i = mazeRoot.childCount - 1; i >= 0; i--)
            Destroy(mazeRoot.GetChild(i).gameObject);

        HashSet<Vector2Int> pillarPositions = new HashSet<Vector2Int>();
        float offsetX = -(cols * cellSize) / 2f;
        float offsetZ = -(rows * cellSize) / 2f;

        foreach (WallSegmentData wall in walls)
        {
            Instantiate(straightWallPrefab, new Vector3(wall.x, 0f, wall.z),
                Quaternion.Euler(0f, wall.rot, 0f), mazeRoot);

            if (Mathf.Abs(wall.rot) < 1f)
            {
                AddPillar(wall.x - cellSize / 2f, wall.z);
                AddPillar(wall.x + cellSize / 2f, wall.z);
            }
            else
            {
                AddPillar(wall.x, wall.z - cellSize / 2f);
                AddPillar(wall.x, wall.z + cellSize / 2f);
            }
        }

        void AddPillar(float x, float z)
        {
            int c = Mathf.RoundToInt((x - offsetX) / cellSize);
            int r = Mathf.RoundToInt((z - offsetZ) / cellSize);
            if (c >= 0 && c <= cols && r >= 0 && r <= rows)
                pillarPositions.Add(new Vector2Int(c, r));
        }

        if (pillarPrefab != null)
        {
            foreach (Vector2Int point in pillarPositions)
            {
                Vector3 pos = new Vector3(offsetX + point.x * cellSize,
                    1.5f, offsetZ + point.y * cellSize);
                Instantiate(pillarPrefab, pos, Quaternion.identity, mazeRoot);
            }
        }

        if (cover != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (ArenaCoverData box in cover)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = box.kind == "barrier" ? "LowCover" : "Crate";
                cube.transform.SetParent(mazeRoot, false);
                cube.transform.position = new Vector3(box.x, box.height / 2f, box.z);
                cube.transform.localScale = new Vector3(box.sx, box.height, box.sz);
                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (shader != null) renderer.material = new Material(shader);
                    renderer.material.color = box.kind == "barrier"
                        ? new Color(0.36f, 0.48f, 0.59f)
                        : new Color(0.62f, 0.39f, 0.23f);
                }
            }
        }

        Debug.Log("[Arena] Стены: " + walls.Length + ", колонны: " +
            pillarPositions.Count + ", укрытия: " + (cover != null ? cover.Length : 0));
        OnMazeBuildFinished?.Invoke();
    }
}
