using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Limpieza de una sola vez de la configuración antigua de la escena (restos del curso).
/// Menú: Tools > VR Room > Limpiar configuración antigua de la escena
/// </summary>
public static class SceneCleanup
{
    const string k_ControllersPath = "Assets/Samples/XR Interaction Toolkit/3.1.3/Starter Assets/Prefabs/Controllers/";
    const string k_TeleportFloorName = "Teleport Floor";

    [MenuItem("Tools/VR Room/Limpiar configuración antigua de la escena")]
    static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        Undo.SetCurrentGroupName("Limpiar configuración antigua");
        var undoGroup = Undo.GetCurrentGroup();

        // 1. Quitar teletransportes de los muebles (ahora se cogen y se mueven).
        //    Guardamos la configuración del primero para reutilizarla en el suelo.
        InteractionLayerMask? teleportLayers = null;
        BaseTeleportationInteractable.TeleportTrigger? teleportTrigger = null;
        foreach (var furniture in FindAll<FurnitureItem>(scene))
        {
            foreach (var teleport in furniture.GetComponentsInChildren<BaseTeleportationInteractable>(true))
            {
                teleportLayers ??= teleport.interactionLayers;
                teleportTrigger ??= teleport.teleportTrigger;
                log.Add($"Quitado {teleport.GetType().Name} de '{teleport.name}'");
                Undo.DestroyObjectImmediate(teleport);
            }
        }

        // 2. Borrar el Plane suelto que quedó a 45 m de la habitación.
        var plane = FindByName(scene, "Plane");
        if (plane != null && plane.transform.parent == null && plane.GetComponent<FurnitureItem>() == null)
        {
            log.Add($"Borrado 'Plane' (estaba en {plane.transform.position})");
            Undo.DestroyObjectImmediate(plane);
        }

        // 3. Zona de teletransporte sobre todo el suelo de la habitación.
        var floor = FindByName(scene, "Room_Modern_Floor");
        float? floorHeight = null;
        if (floor != null && floor.TryGetComponent<Renderer>(out var floorRenderer))
        {
            var bounds = floorRenderer.bounds;
            floorHeight = bounds.max.y;

            var area = FindByName(scene, k_TeleportFloorName);
            if (area == null)
            {
                area = new GameObject(k_TeleportFloorName);
                Undo.RegisterCreatedObjectUndo(area, "Crear Teleport Floor");
                SceneManager.MoveGameObjectToScene(area, scene);
            }

            // Collider fino justo por encima del suelo, para que el rayo lo toque
            // antes que el collider del suelo que viene con la habitación.
            area.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), Quaternion.identity);
            area.transform.localScale = Vector3.one;
            if (!area.TryGetComponent<BoxCollider>(out var box))
                box = Undo.AddComponent<BoxCollider>(area);
            box.center = new Vector3(0f, 0.005f, 0f);
            box.size = new Vector3(bounds.size.x, 0.01f, bounds.size.z);

            if (!area.TryGetComponent<TeleportationArea>(out var teleportArea))
                teleportArea = Undo.AddComponent<TeleportationArea>(area);
            if (teleportLayers.HasValue)
                teleportArea.interactionLayers = teleportLayers.Value;
            if (teleportTrigger.HasValue)
                teleportArea.teleportTrigger = teleportTrigger.Value;
            log.Add($"Creada zona de teletransporte '{k_TeleportFloorName}' de {bounds.size.x:0.0} x {bounds.size.z:0.0} m sobre el suelo");
        }
        else
        {
            log.Add("AVISO: no se encontró 'Room_Modern_Floor'; no se creó la zona de teletransporte");
        }

        // 4. Cambiar las esferas de las manos por modelos de mandos.
        ReplaceSphere(scene, "LeftSphere", "Left Hand Controller", "XR Controller Left", log);
        ReplaceSphere(scene, "RightSphere", "Right Hand Controller", "XR Controller Right", log);

        // 5. Poner el XR Origin a la altura del suelo (la habitación está elevada 0.3 m).
        var origin = FindByName(scene, "XR Origin (VR)");
        if (origin != null && floorHeight.HasValue)
        {
            Undo.RecordObject(origin.transform, "Altura XR Origin");
            var p = origin.transform.position;
            origin.transform.position = new Vector3(p.x, floorHeight.Value, p.z);
            log.Add($"XR Origin colocado a la altura del suelo (y = {floorHeight.Value:0.###})");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("<b>[Limpieza de escena]</b> Hecho y escena guardada:\n- " + string.Join("\n- ", log));
    }

    static void ReplaceSphere(Scene scene, string sphereName, string controllerName, string prefabName, List<string> log)
    {
        var controller = FindByName(scene, controllerName);
        if (controller == null)
        {
            log.Add($"AVISO: no se encontró '{controllerName}'");
            return;
        }

        var sphere = FindByName(scene, sphereName);
        if (sphere != null)
        {
            log.Add($"Borrada '{sphereName}'");
            Undo.DestroyObjectImmediate(sphere);
        }

        if (controller.transform.Find(prefabName) != null)
            return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_ControllersPath + prefabName + ".prefab");
        if (prefab == null)
        {
            log.Add($"AVISO: no se encontró el prefab '{prefabName}'");
            return;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(model, "Añadir modelo de mando");
        log.Add($"Añadido modelo '{prefabName}' en '{controllerName}'");
    }

    static GameObject FindByName(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == objectName)?.gameObject;
    }

    static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
