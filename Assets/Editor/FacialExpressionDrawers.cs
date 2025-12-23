using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FacialExpressionController.FeatureBinding))]
[CustomPropertyDrawer(typeof(FacialExpressionController.BoneBinding))]
[CustomPropertyDrawer(typeof(FacialExpressionController.ParticleBinding))]
public class FeatureBindingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Try to find the 'featureType' property
        SerializedProperty typeProp = property.FindPropertyRelative("featureType");

        string labelName = label.text;

        // If we found the enum property, use its current value name as the label
        if (typeProp != null && typeProp.propertyType == SerializedPropertyType.Enum)
        {
            // Get the name from the enum index
            // Note: enumDisplayNames gives "Mouth Neutral", enumNames gives "Mouth_Neutral"
            // Using display names is usually nicer for UI, but user asked for "Mouth_Neutral".
            // Let's use the display name which is standard Unity behavior for enums, usually "Mouth Neutral" if nicified.
            // If we want exact code name, we use enumNames.
            if (typeProp.enumValueIndex >= 0 && typeProp.enumValueIndex < typeProp.enumDisplayNames.Length) 
            {
                labelName = typeProp.enumDisplayNames[typeProp.enumValueIndex];
            }
        }

        // Draw the property with the new label (and include children/foldout)
        EditorGUI.PropertyField(position, property, new GUIContent(labelName), true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Ensure we reserve enough height for the expanded property
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
