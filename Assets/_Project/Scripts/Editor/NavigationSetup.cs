using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Configura la navegación de la escena (Fase 3):
///  1. Gravedad, para bajar escaleras y no quedarse flotando.
///  2. Movimiento continuo con el joystick izquierdo (sube la escalera caminando).
///  3. LocomotionSettings: ajustes de movimiento y giro para el menú.
///  4. Maqueta en miniatura del apartamento en la mano izquierda (teletransporte por maqueta).
///
/// Menú: Tools > VR Room > Configurar navegación. Se puede volver a ejecutar.
/// Requiere haber construido antes el apartamento.
/// </summary>
public static class NavigationSetup
{
    const float k_MiniatureScale = 0.03f;
    const string k_InputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.1.3/Starter Assets/XRI Default Input Actions.inputactions";
    const string k_MiniatureName = "World In Miniature";
    const string k_SolidColors = "Assets/_Course Library/Materials/Solid Colors/";
    const string k_Fx = "Assets/_Course Library/Audio/FX/";

    // Centro aproximado del apartamento en sus coordenadas locales (para centrar la maqueta en la mano).
    static readonly Vector3 k_ApartmentCenter = new Vector3(2f, 0f, 2f);

    [MenuItem("Tools/VR Room/Configurar navegación (movimiento, gravedad y maqueta)")]
    static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var origin = FindAll<XROrigin>(scene).FirstOrDefault();
        var mediator = FindAll<LocomotionMediator>(scene).FirstOrDefault();
        var apartment = FindByName(scene, "Apartment");
        var leftHand = FindByName(scene, "Left Hand Controller");

        if (origin == null || mediator == null || leftHand == null)
        {
            EditorUtility.DisplayDialog("Configurar navegación", "No se encontró el XR Origin, el LocomotionMediator o el 'Left Hand Controller' en la escena.", "OK");
            return;
        }
        if (apartment == null)
        {
            EditorUtility.DisplayDialog("Configurar navegación", "Primero construye el apartamento (Tools > VR Room > Construir apartamento).", "OK");
            return;
        }

        var log = new List<string>();
        Undo.SetCurrentGroupName("Configurar navegación");
        var undoGroup = Undo.GetCurrentGroup();
        var locomotion = mediator.gameObject;

        // 1. Gravedad
        if (!locomotion.TryGetComponent<GravityProvider>(out var gravity))
            gravity = Undo.AddComponent<GravityProvider>(locomotion);
        Undo.RecordObject(gravity, "Gravedad");
        gravity.mediator = mediator;
        gravity.useGravity = true;
        log.Add("Gravedad activada (GravityProvider)");

        // Cuerpo del jugador: el radio original (0,5 m = 1 m de ancho) no cabía por la escalera
        // ni por la entrada al altillo, y se "subía" a la barandilla de cristal.
        if (origin.TryGetComponent<CharacterController>(out var body))
        {
            Undo.RecordObject(body, "Cuerpo del jugador");
            body.radius = 0.2f;
            body.skinWidth = 0.02f;
            body.stepOffset = 0.3f;
            body.slopeLimit = 45f;
            EditorUtility.SetDirty(body);
            log.Add("CharacterController: radio 0,2 m (cabe por la escalera y la entrada al altillo)");
        }

        // 2. Movimiento continuo con el joystick izquierdo
        var moveObject = locomotion.transform.Find("Move")?.gameObject;
        if (moveObject == null)
        {
            moveObject = new GameObject("Move");
            Undo.RegisterCreatedObjectUndo(moveObject, "Crear Move");
            moveObject.transform.SetParent(locomotion.transform, false);
        }
        if (!moveObject.TryGetComponent<ContinuousMoveProvider>(out var move))
            move = Undo.AddComponent<ContinuousMoveProvider>(moveObject);
        Undo.RecordObject(move, "Movimiento continuo");
        move.mediator = mediator;
        move.moveSpeed = 1.5f;
        move.enableStrafe = true;
        move.enableFly = false;
        move.forwardSource = origin.Camera.transform;
        move.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.InputActionReference;
        move.leftHandMoveInput.inputActionReference = FindActionReference("XRI Left Locomotion/Move");
        move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        EditorUtility.SetDirty(move);
        log.Add(move.leftHandMoveInput.inputActionReference != null
            ? "Movimiento continuo con el joystick izquierdo (hacia donde miras)"
            : "AVISO: no se encontró la acción 'XRI Left Locomotion/Move'; revisa el Continuous Move Provider");

        // 3. Ajustes de locomoción
        if (!locomotion.TryGetComponent<LocomotionSettings>(out _))
            Undo.AddComponent<LocomotionSettings>(locomotion);
        log.Add("LocomotionSettings añadido (giro por saltos/continuo, velocidad, pausa al sostener objetos)");

        // 4. Maqueta
        BuildMiniature(scene, apartment, leftHand, log);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<b>[Navegación]</b> Configurada y escena guardada:\n- " + string.Join("\n- ", log));
    }

    static void BuildMiniature(Scene scene, GameObject apartment, GameObject leftHand, List<string> log)
    {
        var structure = apartment.transform.Find("Structure");
        if (structure == null)
        {
            log.Add("AVISO: el apartamento no tiene 'Structure'; no se creó la maqueta");
            return;
        }

        var previous = FindByName(scene, k_MiniatureName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous);

        // Encima de la mano izquierda (no delante, para no tapar el rayo izquierdo).
        var wim = new GameObject(k_MiniatureName);
        Undo.RegisterCreatedObjectUndo(wim, "Crear maqueta");
        wim.transform.SetParent(leftHand.transform, false);
        wim.transform.localPosition = new Vector3(0f, 0.1f, 0.02f);

        var model = new GameObject("Model").transform;
        model.SetParent(wim.transform, false);
        model.localScale = Vector3.one * k_MiniatureScale;
        model.localPosition = -k_ApartmentCenter * k_MiniatureScale;

        var copy = Object.Instantiate(structure.gameObject, model);
        copy.name = "Structure";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one;
        StripMiniature(copy);

        var playerMarker = CreateMarker("Player Marker", model, PrimitiveType.Capsule, new Vector3(0.8f, 1.2f, 0.8f), 1.2f, k_SolidColors + "BlueGlow.mat");
        // Pequeño cubo delante del marcador para ver hacia dónde mira el usuario.
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.name = "Facing";
        nose.transform.SetParent(playerMarker, false);
        nose.transform.localPosition = new Vector3(0f, 1.8f, 0.6f);
        nose.transform.localScale = new Vector3(0.4f, 0.4f, 0.6f);
        nose.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_SolidColors + "BlueGlow.mat");

        var targetMarker = CreateMarker("Target Marker", model, PrimitiveType.Sphere, Vector3.one * 0.6f, 0f, k_SolidColors + "YellowGlow.mat");

        var miniature = wim.AddComponent<WorldInMiniature>();
        miniature.Configure(apartment.transform, model, playerMarker, targetMarker,
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_SciFi_Forward.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_Quick_1.ogg"));
        EditorUtility.SetDirty(miniature);

        log.Add($"Maqueta 1:{Mathf.RoundToInt(1f / k_MiniatureScale)} en la mano izquierda (botón menú para mostrarla, rayo derecho + grip para teletransportarse)");
    }

    /// <summary>Deja solo lo visual de la copia, más los colliders de las zonas de teletransporte (para apuntar).</summary>
    static void StripMiniature(GameObject copy)
    {
        foreach (var t in copy.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Roof").ToList())
            Object.DestroyImmediate(t.gameObject);

        var keep = new HashSet<Collider>(copy.GetComponentsInChildren<TeleportationArea>(true).SelectMany(a => a.GetComponents<Collider>()));
        foreach (var c in copy.GetComponentsInChildren<Collider>(true).Where(c => !keep.Contains(c)).ToList())
            Object.DestroyImmediate(c);
        foreach (var area in copy.GetComponentsInChildren<TeleportationArea>(true).ToList())
            Object.DestroyImmediate(area);

        foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Transform CreateMarker(string name, Transform parent, PrimitiveType shape, Vector3 scale, float lift, string materialPath)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        var visual = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.name = "Visual";
        visual.transform.SetParent(marker, false);
        visual.transform.localPosition = new Vector3(0f, lift, 0f);
        visual.transform.localScale = scale;
        var renderer = visual.GetComponent<Renderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return marker;
    }

    static InputActionReference FindActionReference(string fullName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(k_InputActionsPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(r => r.name == fullName);
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
