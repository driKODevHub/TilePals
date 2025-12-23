using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FacialExpressionController.FeatureBinding))]
[CustomPropertyDrawer(typeof(FacialExpressionController.ParticleBinding))]
public class GenericBindingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProp = property.FindPropertyRelative("featureType");
        string labelName = label.text;

        if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
        {
            if (typeProp.enumValueIndex >= 0 && typeProp.enumValueIndex < typeProp.enumDisplayNames.Length)
            {
                labelName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
            }
        }

        EditorGUI.PropertyField(position, property, new GUIContent(labelName), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

[CustomPropertyDrawer(typeof(FacialExpressionController.BoneBinding))]
public class BoneBindingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProp = property.FindPropertyRelative("featureType");
        string labelName = label.text;

        if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
        {
            if (typeProp.enumValueIndex >= 0 && typeProp.enumValueIndex < typeProp.enumDisplayNames.Length)
            {
                labelName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
            }
        }

        // Context Menu Check
        Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Event e = Event.current;
        if (e.type == EventType.ContextClick && headerRect.Contains(e.mousePosition))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Capture All Bone Transforms from Scene"), false, () => CaptureAll(property));
            menu.ShowAsContext();
            e.Use();
        }

        // Draw the property
        EditorGUI.PropertyField(position, property, new GUIContent(labelName), true);
    }

    private void CaptureAll(SerializedProperty bindingProp)
    {
        SerializedProperty statesProp = bindingProp.FindPropertyRelative("boneStates");
        if (statesProp == null) return;

        for (int i = 0; i < statesProp.arraySize; i++)
        {
            SerializedProperty stateProp = statesProp.GetArrayElementAtIndex(i);
            CaptureBoneState(stateProp);
        }
        bindingProp.serializedObject.ApplyModifiedProperties();
    }

    public static void CaptureBoneState(SerializedProperty stateProp)
    {
        SerializedProperty transformProp = stateProp.FindPropertyRelative("boneTransform");
        if (transformProp != null && transformProp.objectReferenceValue is Transform t)
        {
            stateProp.FindPropertyRelative("targetPosition").vector3Value = t.localPosition;
            stateProp.FindPropertyRelative("targetEulerAngles").vector3Value = t.localEulerAngles;
            stateProp.FindPropertyRelative("targetScale").vector3Value = t.localScale;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

[CustomPropertyDrawer(typeof(FacialExpressionController.BoneState))]
public class BoneStateDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float y = position.y;
        
        // Draw the foldout line
        Rect foldoutRect = new Rect(position.x, y, position.width - 65, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        // Add Capture Button
        Rect buttonRect = new Rect(position.x + position.width - 60, y, 60, EditorGUIUtility.singleLineHeight);
        if (GUI.Button(buttonRect, new GUIContent("Capture", "Copy current Transform values from scene"), EditorStyles.miniButton))
        {
            BoneBindingDrawer.CaptureBoneState(property);
        }

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            y += EditorGUIUtility.singleLineHeight + 2;

            SerializedProperty it = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            if (it.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(it, end)) break;
                    
                    float h = EditorGUI.GetPropertyHeight(it, true);
                    Rect r = new Rect(position.x, y, position.width, h);
                    EditorGUI.PropertyField(r, it, true);
                    y += h + 2;
                }
                while (it.NextVisible(false));
            }
            EditorGUI.indentLevel--;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

        float h = EditorGUIUtility.singleLineHeight + 2;
        SerializedProperty it = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        if (it.NextVisible(true))
        {
            do
            {
                if (SerializedProperty.EqualContents(it, end)) break;
                h += EditorGUI.GetPropertyHeight(it, true) + 2;
            }
            while (it.NextVisible(false));
        }
        return h;
    }
}
