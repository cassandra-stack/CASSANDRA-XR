using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class ShaderIncluder
{
    [MenuItem("Tools/Add Volume Shader to Build")]
    public static void AddVolumeShader()
    {
        string shaderName = "Volume/VolumeDVR_URP_Quest";

        Shader shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogError($"Impossible de trouver le shader : '{shaderName}'. Vérifiez que le nom est correct.");
            return;
        }

        var graphicsSettings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        if (graphicsSettings == null)
        {
            Debug.LogError("Impossible de charger GraphicsSettings.asset");
            return;
        }

        var serializedObject = new SerializedObject(graphicsSettings);
        
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

        bool alreadyExists = false;
        for (int i = 0; i < arrayProp.arraySize; ++i)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == shader)
            {
                alreadyExists = true;
                break;
            }
        }

        if (!alreadyExists)
        {
            int newIndex = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(newIndex);
            var newElement = arrayProp.GetArrayElementAtIndex(newIndex);
            newElement.objectReferenceValue = shader;

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log($"Shader '{shaderName}' ajouté avec succès à la liste Always Included Shaders.");
        }
        else
        {
            Debug.Log($"Le shader '{shaderName}' est déjà dans la liste.");
        }
    }
}
