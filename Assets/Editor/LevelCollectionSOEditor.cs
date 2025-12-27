using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

[CustomEditor(typeof(LevelCollectionSO))]
public class LevelCollectionSOEditor : Editor
{
    private ReorderableList reorderableList;
    private Dictionary<Object, SerializedObject> serializedObjects = new Dictionary<Object, SerializedObject>();
    private bool[] foldouts;

    private void OnEnable()
    {
        reorderableList = new ReorderableList(serializedObject, serializedObject.FindProperty("levels"), true, true, true, true);

        reorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Level Steps (Sequential)");
        };

        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            var element = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            float foldoutWidth = 20;
            float objectFieldWidth = rect.width - foldoutWidth - 10;

            if (foldouts == null || foldouts.Length != reorderableList.count) {
                foldouts = new bool[reorderableList.count];
            }

            foldouts[index] = EditorGUI.Foldout(new Rect(rect.x, rect.y, foldoutWidth, EditorGUIUtility.singleLineHeight), foldouts[index], "");
            
            EditorGUI.PropertyField(
                new Rect(rect.x + foldoutWidth, rect.y, objectFieldWidth, EditorGUIUtility.singleLineHeight),
                element, GUIContent.none);

            if (foldouts[index] && element.objectReferenceValue != null) {
                DrawLevelSyncedFields(rect, element.objectReferenceValue as GridDataSO);
            }
        };

        reorderableList.elementHeightCallback = (int index) => {
            if (foldouts != null && index < foldouts.Length && foldouts[index]) {
                var element = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                if (element.objectReferenceValue != null) return EditorGUIUtility.singleLineHeight * 5 + 15;
            }
            return EditorGUIUtility.singleLineHeight + 5;
        };
    }

    private void DrawLevelSyncedFields(Rect rect, GridDataSO data)
    {
        if (data == null) return;

        if (!serializedObjects.ContainsKey(data) || serializedObjects[data].targetObject != data) {
            serializedObjects[data] = new SerializedObject(data);
        }

        SerializedObject so = serializedObjects[data];
        so.Update();

        float startY = rect.y + EditorGUIUtility.singleLineHeight + 5;
        float labelWidth = 100; // Reduced label width for better fit
        float fieldWidth = rect.width - labelWidth - 30;

        // Draw Fields
        Rect drawRect = new Rect(rect.x + 20, startY, labelWidth, EditorGUIUtility.singleLineHeight);
        
        // Environment Prefab
        EditorGUI.LabelField(drawRect, "Env Prefab");
        var envProp = so.FindProperty("environmentPrefab");
        envProp.objectReferenceValue = EditorGUI.ObjectField(new Rect(drawRect.x + labelWidth, drawRect.y, fieldWidth, drawRect.height), envProp.objectReferenceValue, typeof(GameObject), false);
        
        // Spawn Offset
        drawRect.y += EditorGUIUtility.singleLineHeight + 2;
        EditorGUI.LabelField(drawRect, "Spawn Offset");
        var offsetProp = so.FindProperty("levelSpawnOffset");
        offsetProp.vector3Value = EditorGUI.Vector3Field(new Rect(drawRect.x + labelWidth, drawRect.y, fieldWidth, drawRect.height), GUIContent.none, offsetProp.vector3Value);

        // Cell Size
        drawRect.y += EditorGUIUtility.singleLineHeight + 2;
        EditorGUI.LabelField(drawRect, "Cell Size");
        var cellSizeProp = so.FindProperty("cellSize");
        cellSizeProp.floatValue = EditorGUI.FloatField(new Rect(drawRect.x + labelWidth, drawRect.y, fieldWidth, drawRect.height), cellSizeProp.floatValue);

        // Camera Center
        drawRect.y += EditorGUIUtility.singleLineHeight + 2;
        EditorGUI.LabelField(drawRect, "Cam Center");
        var camProp = so.FindProperty("cameraBoundsCenter");
        camProp.vector2Value = EditorGUI.Vector2Field(new Rect(drawRect.x + labelWidth, drawRect.y, fieldWidth, drawRect.height), GUIContent.none, camProp.vector2Value);

        if (so.ApplyModifiedProperties()) {
            EditorUtility.SetDirty(data);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("locationName"));
        EditorGUILayout.Space();

        reorderableList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}
