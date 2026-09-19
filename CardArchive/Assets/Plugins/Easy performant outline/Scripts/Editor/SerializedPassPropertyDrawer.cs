using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EPOOutline
{
    [CustomPropertyDrawer(typeof(SerializedPass))]
    public class SerializedPassPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var drawingPosition = position;
            drawingPosition.height = EditorGUIUtility.singleLineHeight;

            var shaderProperty = property.FindPropertyRelative("shader");

            var currentShaderReference = shaderProperty.objectReferenceValue as Shader;

            var prefix = "Hidden/EPO/Fill/";
            var fillLabel = currentShaderReference == null ? "none" : currentShaderReference.name.Substring(prefix.Length);
            if (shaderProperty.hasMultipleDifferentValues)
                fillLabel = "-";

            if (EditorGUI.DropdownButton(position, new GUIContent(label.text + " : " + fillLabel), FocusType.Passive))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("none"), currentShaderReference == null && !shaderProperty.hasMultipleDifferentValues, () =>
                    {
                        shaderProperty.objectReferenceValue = null;
                        shaderProperty.serializedObject.ApplyModifiedProperties();
                    });

                var shaders = AssetDatabase.FindAssets("t:Shader");
                foreach (var shader in shaders)
                {
                    var loadedShader = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(shader), typeof(Shader)) as Shader;
                    if (!loadedShader.name.StartsWith(prefix))
                        continue;

                    var shaderName = loadedShader.name.Substring(prefix.Length);
                    if (shaderName.StartsWith("Utils"))
                        continue;

                    menu.AddItem(new GUIContent(shaderName), loadedShader == shaderProperty.objectReferenceValue && !shaderProperty.hasMultipleDifferentValues, () =>
                        {
                            shaderProperty.objectReferenceValue = loadedShader;
                            shaderProperty.serializedObject.ApplyModifiedProperties();
                        });
                }

                menu.ShowAsContext();
            }
            
            if (shaderProperty.hasMultipleDifferentValues)
                return;

            if (currentShaderReference != null)
            {
                position.x += EditorGUIUtility.singleLineHeight;
                position.width -= EditorGUIUtility.singleLineHeight;
                var properties = new Dictionary<string, SerializedProperty>();

                var serializedProperties = property.FindPropertyRelative("serializedProperties");

                for (var index = 0; index < serializedProperties.arraySize; index++)
                {
                    var subProperty = serializedProperties.GetArrayElementAtIndex(index);

                    var propertyName = subProperty.FindPropertyRelative("PropertyName");
                    var propertyValue = subProperty.FindPropertyRelative("Property");

                    if (propertyName == null || propertyValue == null)
                        break;

                    properties.Add(propertyName.stringValue, propertyValue);
                }

                var fillParametersPosition = position;
                fillParametersPosition.height = EditorGUIUtility.singleLineHeight;
                for (var index = 0; index < currentShaderReference.GetPropertyCount(); index++)
                {
                    var propertyName = currentShaderReference.GetPropertyName(index);
                    if (!propertyName.StartsWith("_Public"))
                        continue;

                    fillParametersPosition.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    SerializedProperty currentProperty;
                    if (!properties.TryGetValue(propertyName, out currentProperty))
                    {
                        serializedProperties.InsertArrayElementAtIndex(serializedProperties.arraySize);
                        currentProperty = serializedProperties.GetArrayElementAtIndex(serializedProperties.arraySize - 1);
                        currentProperty.FindPropertyRelative("PropertyName").stringValue = propertyName;
                        currentProperty = currentProperty.FindPropertyRelative("Property");

                        var tempMaterial = new Material(currentShaderReference);

                        switch (currentShaderReference.GetPropertyType(index))
                        {
                            case ShaderPropertyType.Color:
                                currentProperty.FindPropertyRelative("ColorValue").colorValue = tempMaterial.GetColor(propertyName);
                                break;
                            case ShaderPropertyType.Vector:
                                currentProperty.FindPropertyRelative("VectorValue").vector4Value = tempMaterial.GetVector(propertyName);
                                break;
                            case ShaderPropertyType.Float:
                                currentProperty.FindPropertyRelative("FloatValue").floatValue = tempMaterial.GetFloat(propertyName);
                                break;
                            case ShaderPropertyType.Range:
                                currentProperty.FindPropertyRelative("FloatValue").floatValue = tempMaterial.GetFloat(propertyName);
                                break;
                            case ShaderPropertyType.Texture:
                                currentProperty.FindPropertyRelative("TextureValue").objectReferenceValue = tempMaterial.GetTexture(propertyName);
                                break;
                        }

                        GameObject.DestroyImmediate(tempMaterial);

                        properties.Add(propertyName, currentProperty);
                    }

                    if (currentProperty == null)
                        continue;

                    var content = new GUIContent(currentShaderReference.GetPropertyDescription(index));

                    switch (currentShaderReference.GetPropertyType(index))
                    {
                        case ShaderPropertyType.Color:
                            var colorProperty = currentProperty.FindPropertyRelative("ColorValue");
                            colorProperty.colorValue = EditorGUI.ColorField(fillParametersPosition, content, colorProperty.colorValue, true, true, true);
                            break;
                        case ShaderPropertyType.Vector:
                            var vectorProperty = currentProperty.FindPropertyRelative("VectorValue");
                            vectorProperty.vector4Value = EditorGUI.Vector4Field(fillParametersPosition, content, vectorProperty.vector4Value);
                            break;
                        case ShaderPropertyType.Float:
                            EditorGUI.PropertyField(fillParametersPosition, currentProperty.FindPropertyRelative("FloatValue"), content);
                            break;
                        case ShaderPropertyType.Range:
                            var floatProperty = currentProperty.FindPropertyRelative("FloatValue");
                            floatProperty.floatValue = EditorGUI.Slider(fillParametersPosition, content, floatProperty.floatValue, 
                                currentShaderReference.GetPropertyRangeLimits(index).x,
                                currentShaderReference.GetPropertyRangeLimits(index).y);
                            break;
                        case ShaderPropertyType.Texture:
                            EditorGUI.PropertyField(fillParametersPosition, currentProperty.FindPropertyRelative("TextureValue"), content);
                            break;
                    }

                    currentProperty.FindPropertyRelative("PropertyType").intValue = (int)currentShaderReference.GetPropertyType(index);
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.FindPropertyRelative("shader").hasMultipleDifferentValues)
                return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var shaderProperty = property.FindPropertyRelative("shader");
            var currentShaderReference = shaderProperty.objectReferenceValue as Shader;

            var additionalCount = 0;
            if (currentShaderReference != null)
            {
                for (var index = 0; index < currentShaderReference.GetPropertyCount(); index++)
                {
                    var propertyName = currentShaderReference.GetPropertyName(index);
                    if (!propertyName.StartsWith("_Public"))
                        continue;

                    additionalCount++;
                }
            }

            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * (additionalCount + 1);
        }
    }
}
