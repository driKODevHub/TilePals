using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System.Collections.Generic;

[CustomEditor(typeof(PuzzlePiece))]
public class PuzzlePieceEditor : Editor
{
    private Editor cachedShapeEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PuzzlePiece puzzlePiece = (PuzzlePiece)target;

        // Перевіряємо наявність необхідних компонентів для нової архітектури
        if (puzzlePiece.GetComponent<PieceVisuals>() == null || puzzlePiece.GetComponent<PieceMovement>() == null)
        {
            EditorGUILayout.HelpBox("Missing required components (PieceVisuals or PieceMovement)!", MessageType.Error);
            if (GUILayout.Button("Fix Components"))
            {
                if (puzzlePiece.GetComponent<PieceVisuals>() == null) puzzlePiece.gameObject.AddComponent<PieceVisuals>();
                if (puzzlePiece.GetComponent<PieceMovement>() == null) puzzlePiece.gameObject.AddComponent<PieceMovement>();
            }
        }

        if (puzzlePiece.PieceTypeSO != null)
        {
            EditorGUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Assign This Prefab to SO", "Призначає цей префаб (або його варіант) в поля 'Prefab' та 'Visual' відповідного ScriptableObject.")))
            {
                AssignPrefabToSO(puzzlePiece);
            }

            if (GUILayout.Button(new GUIContent("Find and Assign Mesh from Name", "Шукає меш в 'AllShapeBlocks.fbx' за назвою префабу та призначає його в PieceVisuals.")))
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
            Debug.Log($"<color=green>Assigned prefab</color> <b>'{prefabAsset.name}'</b> <color=green>to SO</color>.");
        }
        else
        {
            Debug.LogWarning("Could not find prefab asset.", puzzlePiece);
        }
    }

    private void FindAndAssignMesh(PuzzlePiece puzzlePiece)
    {
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(puzzlePiece.gameObject);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("Not a prefab.", puzzlePiece);
            return;
        }

        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        string identifier = prefabName.Replace("POT_TilePall_", "");

        if (string.IsNullOrEmpty(identifier) || prefabName.Equals(identifier))
        {
            Debug.LogError($"Invalid prefab name format: '{prefabName}'. Expected 'POT_TilePall_...'", puzzlePiece);
            return;
        }

        string targetMeshName = "Tile_" + identifier;
        string[] fbxGuids = AssetDatabase.FindAssets("AllShapeBlocks t:Model");
        if (fbxGuids.Length == 0) return;

        string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        Mesh foundMesh = allAssets.OfType<Mesh>().FirstOrDefault(m => m.name == targetMeshName);

        if (foundMesh == null)
        {
            Debug.LogError($"Mesh '{targetMeshName}' not found in '{fbxPath}'.", puzzlePiece);
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        // --- UPDATED LOGIC FOR NEW ARCHITECTURE ---
        PieceVisuals visuals = prefabRoot.GetComponent<PieceVisuals>();
        if (visuals == null) visuals = prefabRoot.AddComponent<PieceVisuals>();

        MeshFilter meshFilter = prefabRoot.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer meshRenderer = prefabRoot.GetComponentInChildren<MeshRenderer>(true);

        if (meshFilter != null && meshRenderer != null)
        {
            meshFilter.sharedMesh = foundMesh;

            // Встановлюємо список meshesToColor в новому компоненті PieceVisuals
            // Використовуємо Reflection або Public Method, якщо поле приватне. 
            // Тут ми додали публічний метод SetMeshesToColorFromEditor в PieceVisuals
            visuals.SetMeshesToColorFromEditor(new List<MeshRenderer> { meshRenderer });

            Debug.Log($"<color=green>Assigned mesh '{targetMeshName}' to PieceVisuals on '{prefabName}'.</color>");
        }
        else
        {
            Debug.LogError("No MeshFilter/Renderer found in children.", prefabRoot);
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private void OnDisable()
    {
        if (cachedShapeEditor != null) DestroyImmediate(cachedShapeEditor);
    }
}