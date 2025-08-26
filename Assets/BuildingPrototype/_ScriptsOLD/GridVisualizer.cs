using UnityEngine;

// [RequireComponent(typeof(MeshRenderer))] // Може бути корисно, якщо tilePrefab - це сам MeshRenderer
public class GridVisualizer : MonoBehaviour
{
    public static GridVisualizer Instance { get; private set; }

    [HideInInspector] public int width = 10;
    [HideInInspector] public int height = 10;
    public GameObject tilePrefab;
    public Material defaultTileMaterial;
    public Material occupiedTileMaterial;

    private GameObject[,] tiles;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager != null)
        {
            width = gridManager.gridWidth;
            height = gridManager.gridHeight;
        }

        tiles = new GameObject[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Тайли сітки створюються з центром в (x + 0.5f, 0, y + 0.5f),
                // що відповідає центрам клітинок сітки (0,0) -> центр (0.5,0.5)
                Vector3 tilePosition = new Vector3(x + 0.5f, 0, y + 0.5f);
                GameObject tile = Instantiate(tilePrefab, tilePosition, Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";
                tiles[x, y] = tile;

                MeshRenderer tileRenderer = tile.GetComponentInChildren<MeshRenderer>();

                if (tileRenderer != null && defaultTileMaterial != null)
                {
                    tileRenderer.material = defaultTileMaterial;
                }
                else
                {
                    if (tileRenderer == null) Debug.LogWarning($"Tile prefab at {x},{y} (or its children) is missing a MeshRenderer!");
                    if (defaultTileMaterial == null) Debug.LogWarning("DefaultTileMaterial is not assigned in GridVisualizer!");
                }
            }
        }
    }

    public void UpdateTileMaterial(int x, int y, bool isOccupied)
    {
        if (x >= 0 && x < width && y >= 0 && y < height && tiles[x, y] != null)
        {
            MeshRenderer tileRenderer = tiles[x, y].GetComponentInChildren<MeshRenderer>();

            if (tileRenderer != null)
            {
                if (isOccupied)
                {
                    if (occupiedTileMaterial != null)
                    {
                        tileRenderer.material = occupiedTileMaterial;
                    }
                    else
                    {
                        Debug.LogWarning("OccupiedTileMaterial is not assigned in GridVisualizer!");
                    }
                }
                else
                {
                    if (defaultTileMaterial != null)
                    {
                        tileRenderer.material = defaultTileMaterial;
                    }
                    else
                    {
                        Debug.LogWarning("DefaultTileMaterial is not assigned in GridVisualizer!");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Tile at {x},{y} is missing a MeshRenderer component or its children!");
            }
        }
    }
}