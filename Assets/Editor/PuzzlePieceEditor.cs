using UnityEngine;
using UnityEditor;
using System.Linq; // Потрібно для LINQ-запитів
using System.IO;   // Потрібно для Path.GetFileNameWithoutExtension

[CustomEditor(typeof(PuzzlePiece))]
public class PuzzlePieceEditor : Editor
{
    private Editor cachedShapeEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PuzzlePiece puzzlePiece = (PuzzlePiece)target;

        if (puzzlePiece.PieceTypeSO != null)
        {
            EditorGUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Assign This Prefab to SO", "Призначає цей префаб (або його варіант) в поля 'Prefab' та 'Visual' відповідного ScriptableObject.")))
            {
                AssignPrefabToSO(puzzlePiece);
            }

            // --- НОВА КНОПКА ---
            if (GUILayout.Button(new GUIContent("Find and Assign Mesh from Name", "Шукає меш в 'AllShapeBlocks.fbx' за назвою префабу та призначає його.")))
            {
                FindAndAssignMesh(puzzlePiece);
            }


            if (cachedShapeEditor == null || cachedShapeEditor.target != puzzlePiece.PieceTypeSO)
            {
                if (cachedShapeEditor != null)
                {
                    DestroyImmediate(cachedShapeEditor);
                }
                cachedShapeEditor = Editor.CreateEditor(puzzlePiece.PieceTypeSO);
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"Editing '{puzzlePiece.PieceTypeSO.name}'", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Зміни, внесені тут, будуть збережені безпосередньо в ScriptableObject.", MessageType.Info);

            cachedShapeEditor.OnInspectorGUI();

            EditorGUILayout.EndVertical();
        }
    }

    private void AssignPrefabToSO(PuzzlePiece puzzlePiece)
    {
        GameObject prefabAsset = null;

        if (PrefabUtility.IsPartOfAnyPrefab(puzzlePiece.gameObject))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(puzzlePiece.gameObject);

            if (!string.IsNullOrEmpty(path))
            {
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        if (prefabAsset != null)
        {
            puzzlePiece.PieceTypeSO.prefab = prefabAsset.transform;
            puzzlePiece.PieceTypeSO.visual = prefabAsset.transform;

            EditorUtility.SetDirty(puzzlePiece.PieceTypeSO);

            Debug.Log($"<color=green>Успішно призначено префаб-варіант</color> <b>'{prefabAsset.name}'</b> <color=green>до SO</color> <b>'{puzzlePiece.PieceTypeSO.name}'</b>.", puzzlePiece.PieceTypeSO);
        }
        else
        {
            Debug.LogWarning("Не вдалося знайти асет префабу.", puzzlePiece);
        }
    }

    // --- НОВИЙ, ВИПРАВЛЕНИЙ МЕТОД ---
    private void FindAndAssignMesh(PuzzlePiece puzzlePiece)
    {
        // 1. Отримуємо шлях до префабу, який інспектуємо. Це надійно працює з варіантами.
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(puzzlePiece.gameObject);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("Не вдалося отримати шлях до префабу. Переконайтеся, що ви працюєте з префабом.", puzzlePiece);
            return;
        }

        // 2. Отримуємо ім'я файлу зі шляху. Це гарантує, що ми отримаємо ім'я варіанта, а не бази.
        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        string identifier = prefabName.Replace("POT_TilePall_", "");

        // 3. Перевіряємо, чи вдалося витягти ідентифікатор
        if (string.IsNullOrEmpty(identifier) || prefabName.Equals(identifier))
        {
            Debug.LogError($"Не вдалося витягти ідентифікатор з назви префабу: '{prefabName}'. Переконайтеся, що назва відповідає формату 'POT_TilePall_...'", puzzlePiece);
            return;
        }

        string targetMeshName = "Tile_" + identifier;

        // 4. Шукаємо FBX файл 'AllShapeBlocks'
        string[] fbxGuids = AssetDatabase.FindAssets("AllShapeBlocks t:Model");
        if (fbxGuids.Length == 0)
        {
            Debug.LogError("Не знайдено FBX файл з назвою 'AllShapeBlocks' у проекті.");
            return;
        }
        string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);

        // 5. Завантажуємо всі асети (включно з мешами) з цього FBX
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        Mesh foundMesh = allAssets.OfType<Mesh>().FirstOrDefault(m => m.name == targetMeshName);

        if (foundMesh == null)
        {
            Debug.LogError($"Не знайдено меш з назвою '{targetMeshName}' всередині '{fbxPath}'.", puzzlePiece);
            return;
        }

        // 6. Завантажуємо вміст префабу для редагування
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        PuzzlePiece pieceInPrefab = prefabRoot.GetComponent<PuzzlePiece>();

        // 7. Знаходимо MeshFilter та MeshRenderer в дочірніх об'єктах
        MeshFilter meshFilter = pieceInPrefab.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer meshRenderer = pieceInPrefab.GetComponentInChildren<MeshRenderer>(true);

        if (meshFilter == null || meshRenderer == null)
        {
            Debug.LogError("У дочірніх об'єктах префабу не знайдено MeshFilter або MeshRenderer. Перевірте структуру префабу.", prefabRoot);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return;
        }

        // 8. Призначаємо меш та оновлюємо список рендерерів
        meshFilter.sharedMesh = foundMesh;

        var meshesToColorProp = new SerializedObject(pieceInPrefab).FindProperty("meshesToColor");
        meshesToColorProp.ClearArray();
        meshesToColorProp.InsertArrayElementAtIndex(0);
        meshesToColorProp.GetArrayElementAtIndex(0).objectReferenceValue = meshRenderer;
        meshesToColorProp.serializedObject.ApplyModifiedProperties();

        // 9. Зберігаємо зміни
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log($"<color=green>Успішно призначено меш</color> <b>'{targetMeshName}'</b> <color=green>до префабу</color> <b>'{prefabName}'</b>.", puzzlePiece);
    }


    private void OnDisable()
    {
        if (cachedShapeEditor != null)
        {
            DestroyImmediate(cachedShapeEditor);
        }
    }
}
