using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Crea (si falta) una capa de interacción de XRI con nombre, editando el asset InteractionLayerSettings.
/// Se usa para la capa "Climb": solo los interactores de mano cercanos pueden agarrar la escalera.
/// </summary>
public static class InteractionLayerUtility
{
    const string k_SettingsPath = "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset";

    public static InteractionLayerMask EnsureLayer(string layerName)
    {
        var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(k_SettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"No se encontró {k_SettingsPath}; se usará la capa Default.");
            return 1;
        }

        var serialized = new SerializedObject(settings);
        var names = serialized.FindProperty("m_LayerNames");
        var freeIndex = -1;
        for (var i = 0; i < names.arraySize; i++)
        {
            var value = names.GetArrayElementAtIndex(i).stringValue;
            if (value == layerName)
                return 1 << i;
            // La 0 (Default) y la 31 (Teleport) están reservadas.
            if (freeIndex < 0 && i > 0 && i < 31 && string.IsNullOrEmpty(value))
                freeIndex = i;
        }

        if (freeIndex < 0)
        {
            Debug.LogWarning($"No hay capas de interacción libres para '{layerName}'.");
            return 1;
        }

        names.GetArrayElementAtIndex(freeIndex).stringValue = layerName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return 1 << freeIndex;
    }
}
