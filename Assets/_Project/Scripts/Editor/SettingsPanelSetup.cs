using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Crea el panel 3D de ajustes de navegación y prepara la escena para usar UI con el rayo:
/// EventSystem con XRUIInputModule e interacción UI activada en los rayos.
///
/// Menú: Tools > VR Room > Crear panel de ajustes de navegación. Se puede volver a ejecutar.
/// Requiere haber ejecutado antes "Configurar navegación".
/// </summary>
public static class SettingsPanelSetup
{
    const string k_RootName = "Menus";
    const string k_PanelName = "Navigation Settings Panel";
    const string k_Fx = "Assets/_Course Library/Audio/FX/";
    const int k_UILayer = 5;

    // Tamaño del panel en unidades de UI (1 unidad = 1 mm en el mundo).
    const float k_Width = 640f, k_Height = 620f, k_Padding = 32f, k_RowHeight = 70f, k_FirstRowY = -110f;

    static readonly Color k_Background = new Color(0.08f, 0.10f, 0.13f, 0.94f);
    static readonly Color k_Text = new Color(0.93f, 0.95f, 0.97f);
    static readonly Color k_SubtleText = new Color(0.65f, 0.70f, 0.76f);
    static readonly Color k_Track = new Color(0.25f, 0.27f, 0.32f);
    static readonly Color k_Accent = new Color(0.20f, 0.55f, 0.95f);

    static Sprite s_Rounded, s_Knob, s_Check;

    [MenuItem("Tools/VR Room/Crear panel de ajustes de navegación")]
    static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var origin = FindAll<XROrigin>(scene).FirstOrDefault();
        var settings = FindAll<LocomotionSettings>(scene).FirstOrDefault();
        var miniature = FindAll<WorldInMiniature>(scene).FirstOrDefault();
        if (origin == null || settings == null)
        {
            EditorUtility.DisplayDialog("Panel de ajustes", "Primero ejecuta 'Tools > VR Room > Configurar navegación'.", "OK");
            return;
        }

        s_Rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_Knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        s_Check = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

        var log = new List<string>();
        Undo.SetCurrentGroupName("Crear panel de ajustes");
        var undoGroup = Undo.GetCurrentGroup();

        SetupEventSystem(scene, log);
        EnableRayUI(scene, log);

        var previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == k_RootName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous);

        var root = new GameObject(k_RootName);
        Undo.RegisterCreatedObjectUndo(root, "Crear menús");
        SceneManager.MoveGameObjectToScene(root, scene);

        var panel = BuildPanel(root.transform, origin.Camera, out var bindings);
        var summoner = root.AddComponent<PanelSummoner>();
        summoner.Configure(panel, origin.Camera.transform,
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_SciFi_Forward.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_SciFi_Back.ogg"));

        var panelLogic = panel.AddComponent<NavigationSettingsPanel>();
        panelLogic.Configure(settings, miniature, summoner, AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_Click_2.ogg"));
        bindings(panelLogic);
        EditorUtility.SetDirty(summoner);
        EditorUtility.SetDirty(panelLogic);
        log.Add("Panel de ajustes de navegación creado (botón X del mando izquierdo para abrirlo)");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<b>[Panel de ajustes]</b> Hecho y escena guardada:\n- " + string.Join("\n- ", log));
    }

    // ------------------------------------------------------------------ Preparación de la escena

    static void SetupEventSystem(Scene scene, List<string> log)
    {
        var eventSystem = FindAll<EventSystem>(scene).FirstOrDefault();
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
            SceneManager.MoveGameObjectToScene(go, scene);
            eventSystem = go.AddComponent<EventSystem>();
            log.Add("Creado EventSystem");
        }

        // Solo debe haber un módulo de entrada: el de XR.
        foreach (var module in eventSystem.GetComponents<BaseInputModule>().Where(m => !(m is XRUIInputModule)).ToList())
            Undo.DestroyObjectImmediate(module);
        if (!eventSystem.TryGetComponent<XRUIInputModule>(out _))
        {
            Undo.AddComponent<XRUIInputModule>(eventSystem.gameObject);
            log.Add("Añadido XRUIInputModule al EventSystem");
        }
    }

    static void EnableRayUI(Scene scene, List<string> log)
    {
        foreach (var ray in FindAll<XRRayInteractor>(scene))
        {
            if (ray.enableUIInteraction)
                continue;
            Undo.RecordObject(ray, "Activar UI en el rayo");
            ray.enableUIInteraction = true;
            EditorUtility.SetDirty(ray);
            log.Add($"Interacción con UI activada en '{ray.name}'");
        }
    }

    // ------------------------------------------------------------------ Construcción del panel

    static GameObject BuildPanel(Transform parent, Camera eventCamera, out System.Action<NavigationSettingsPanel> bindings)
    {
        var panel = new GameObject(k_PanelName, typeof(RectTransform));
        panel.layer = k_UILayer;
        panel.transform.SetParent(parent, false);
        panel.transform.localScale = Vector3.one * 0.001f;

        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;
        panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        var rect = (RectTransform)panel.transform;
        rect.sizeDelta = new Vector2(k_Width, k_Height);

        var background = AddImage(panel, s_Rounded, k_Background);
        background.raycastTarget = true;   // bloquea el rayo detrás del panel

        var title = Label(panel.transform, "Ajustes de navegación", 38, FontStyles.Bold, TextAlignmentOptions.Left, k_Text);
        Place(title.rectTransform, k_Padding, -k_Padding, k_Width - 2 * k_Padding - 70f, 50f);

        var close = MakeButton(panel.transform, "X", k_Track, 30);
        Place((RectTransform)close.transform, k_Width - k_Padding - 56f, -k_Padding, 56f, 50f);

        var moveToggle = SwitchRow(panel.transform, 0, "Movimiento continuo");
        var (moveSpeed, moveSpeedValue) = SliderRow(panel.transform, 1, "Velocidad al andar", 0.5f, 4f, false);
        var (snap, smooth) = SegmentedRow(panel.transform, 2, "Tipo de giro", "Por saltos", "Continuo");
        var (snapAngle, snapAngleValue) = SliderRow(panel.transform, 3, "Ángulo de giro", 1f, 6f, true);
        var (turnSpeed, turnSpeedValue) = SliderRow(panel.transform, 4, "Velocidad de giro", 30f, 180f, true);
        var miniatureToggle = SwitchRow(panel.transform, 5, "Maqueta del apartamento");

        var hint = Label(panel.transform,
            "Teletransporte: apunta al suelo y pulsa grip  ·  Andar: joystick izquierdo\n" +
            "Maqueta: botón menú izquierdo  ·  Este panel: botón X",
            20, FontStyles.Normal, TextAlignmentOptions.Left, k_SubtleText);
        Place(hint.rectTransform, k_Padding, -(k_Height - k_Padding - 56f), k_Width - 2 * k_Padding, 56f);

        bindings = logic => logic.BindControls(moveToggle, moveSpeed, moveSpeedValue, snap, smooth,
            snapAngle, snapAngleValue, turnSpeed, turnSpeedValue, miniatureToggle, close);
        return panel;
    }

    static RectTransform Row(Transform parent, int index, string text)
    {
        var row = CreateRect("Row_" + text, parent);
        Place(row, k_Padding, k_FirstRowY - index * k_RowHeight, k_Width - 2 * k_Padding, k_RowHeight - 14f);
        var label = Label(row, text, 26, FontStyles.Normal, TextAlignmentOptions.Left, k_Text);
        Stretch(label.rectTransform, 0f, 0.48f);
        return row;
    }

    static Toggle SwitchRow(Transform parent, int index, string text)
    {
        var row = Row(parent, index, text);

        var track = CreateRect("Switch", row);
        track.anchorMin = track.anchorMax = new Vector2(1f, 0.5f);
        track.pivot = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(110f, 48f);
        var trackImage = AddImage(track.gameObject, s_Rounded, k_Track);

        var fill = CreateRect("On", track);
        Stretch(fill, 0f, 1f);
        var fillImage = AddImage(fill.gameObject, s_Rounded, k_Accent);
        var check = CreateRect("Check", fill);
        check.anchorMin = check.anchorMax = new Vector2(0.5f, 0.5f);
        check.sizeDelta = new Vector2(34f, 34f);
        AddImage(check.gameObject, s_Check, Color.white, Image.Type.Simple);

        var toggle = track.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = trackImage;
        toggle.graphic = fillImage;
        return toggle;
    }

    static (Slider, TMP_Text) SliderRow(Transform parent, int index, string text, float min, float max, bool wholeNumbers)
    {
        var row = Row(parent, index, text);

        var sliderRect = CreateRect("Slider", row);
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.offsetMin = new Vector2(0f, -14f);
        sliderRect.offsetMax = new Vector2(-110f, 14f);

        var background = CreateRect("Background", sliderRect);
        Stretch(background, 0f, 1f);
        AddImage(background.gameObject, s_Rounded, k_Track);

        var fillArea = CreateRect("Fill Area", sliderRect);
        Stretch(fillArea, 0f, 1f);
        var fill = CreateRect("Fill", fillArea);
        Stretch(fill, 0f, 1f);
        AddImage(fill.gameObject, s_Rounded, k_Accent);

        var handleArea = CreateRect("Handle Slide Area", sliderRect);
        Stretch(handleArea, 0f, 1f);
        handleArea.offsetMin = new Vector2(14f, 0f);
        handleArea.offsetMax = new Vector2(-14f, 0f);
        var handle = CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(40f, 40f);
        var handleImage = AddImage(handle.gameObject, s_Knob, Color.white, Image.Type.Simple);

        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;

        var value = Label(row, "-", 24, FontStyles.Bold, TextAlignmentOptions.Right, k_Text);
        value.rectTransform.anchorMin = new Vector2(1f, 0f);
        value.rectTransform.anchorMax = new Vector2(1f, 1f);
        value.rectTransform.pivot = new Vector2(1f, 0.5f);
        value.rectTransform.sizeDelta = new Vector2(100f, 0f);
        return (slider, value);
    }

    static (Button, Button) SegmentedRow(Transform parent, int index, string text, string optionA, string optionB)
    {
        var row = Row(parent, index, text);
        var a = MakeButton(row, optionA, k_Track, 24);
        var b = MakeButton(row, optionB, k_Track, 24);
        var ra = (RectTransform)a.transform;
        var rb = (RectTransform)b.transform;
        ra.anchorMin = new Vector2(0.5f, 0f); ra.anchorMax = new Vector2(0.75f, 1f);
        rb.anchorMin = new Vector2(0.75f, 0f); rb.anchorMax = new Vector2(1f, 1f);
        ra.offsetMin = rb.offsetMin = new Vector2(3f, 0f);
        ra.offsetMax = rb.offsetMax = new Vector2(-3f, 0f);
        return (a, b);
    }

    static Button MakeButton(Transform parent, string text, Color color, float fontSize)
    {
        var rect = CreateRect("Button_" + text, parent);
        var image = AddImage(rect.gameObject, s_Rounded, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);   // aclara al apuntar
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        var label = Label(rect, text, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, k_Text);
        Stretch(label.rectTransform, 0f, 1f);
        return button;
    }

    // ------------------------------------------------------------------ Ayudantes de UI

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = k_UILayer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image AddImage(GameObject go, Sprite sprite, Color color, Image.Type type = Image.Type.Sliced)
    {
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        return image;
    }

    static TextMeshProUGUI Label(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        var rect = CreateRect("Text", parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    /// <summary>Coloca un rect anclado arriba a la izquierda (x hacia la derecha, y negativa hacia abajo).</summary>
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Ocupa todo el alto del padre y el tramo horizontal [xMin, xMax] (0..1).</summary>
    static void Stretch(RectTransform rect, float xMin, float xMax)
    {
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
