using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Crea el menú 3D de control de muebles (botón Y del mando izquierdo): color, duplicar,
/// borrar, resetear, catálogo para añadir muebles y resetear la sala.
/// Usa el mismo estilo y los mismos ayudantes de UI que el panel de ajustes.
///
/// Menú: Tools > VR Room > Crear menú de muebles. Se puede volver a ejecutar.
/// </summary>
public static class ObjectMenuSetup
{
    const string k_RootName = "Object Menu";
    const string k_Fx = "Assets/_Course Library/Audio/FX/";
    const string k_Prefabs = "Assets/_Course Library/_Prefabs/";

    const float k_Width = 720f, k_Height = 660f, k_Padding = 32f;

    // Índice 0 = color original.
    static readonly (string label, Color color)[] k_Palette =
    {
        ("Orig.", Color.white),
        ("", new Color(0.90f, 0.30f, 0.28f)),   // rojo
        ("", new Color(0.98f, 0.62f, 0.25f)),   // naranja
        ("", new Color(0.98f, 0.88f, 0.35f)),   // amarillo
        ("", new Color(0.45f, 0.80f, 0.42f)),   // verde
        ("", new Color(0.35f, 0.60f, 0.95f)),   // azul
        ("", new Color(0.65f, 0.45f, 0.90f)),   // morado
        ("", new Color(0.35f, 0.35f, 0.38f)),   // gris oscuro
    };

    static readonly (string label, string prefab)[] k_Catalog =
    {
        ("Sofá", "Seating/Couch_Modern_Gray"),
        ("Silla", "Seating/Chair_Modern_Brown"),
        ("Mesa centro", "Tables/Table_Coffee_Modern"),
        ("Mesita", "Tables/Table_Side_Modern_Brown"),
        ("Estantería", "Storage/Shelving_Modern_White"),
        ("Cajonera", "Storage/Drawers_Modern_Dark"),
        ("Planta", "Plants/Plant_A"),
        ("Alfombra", "Rugs/Rug_Rect_Purple"),
    };

    [MenuItem("Tools/VR Room/Crear menú de muebles")]
    static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var origin = SettingsPanelSetup.FindAll<XROrigin>(scene).FirstOrDefault();
        var apartment = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Apartment");
        if (origin == null || apartment == null)
        {
            EditorUtility.DisplayDialog("Menú de muebles", "Hace falta el XR Origin y el apartamento construido (Tools > VR Room > Construir apartamento).", "OK");
            return;
        }

        SettingsPanelSetup.LoadSprites();
        var log = new List<string>();
        Undo.SetCurrentGroupName("Crear menú de muebles");
        var undoGroup = Undo.GetCurrentGroup();

        // Por si se ejecuta antes que el panel de ajustes: EventSystem y UI en los rayos.
        SettingsPanelSetup.SetupEventSystem(scene, log);
        SettingsPanelSetup.EnableRayUI(scene, log);

        var previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == k_RootName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous);

        var root = new GameObject(k_RootName);
        Undo.RegisterCreatedObjectUndo(root, "Crear menú de muebles");
        SceneManager.MoveGameObjectToScene(root, scene);

        var panel = BuildPanel(root.transform, origin.Camera, out var bind);
        var summoner = root.AddComponent<PanelSummoner>();
        summoner.Configure(panel, origin.Camera.transform,
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_SciFi_Forward.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_SciFi_Back.ogg"),
            PanelSummoner.LeftButton.Y);

        var logic = panel.AddComponent<ObjectMenuPanel>();
        logic.Configure(summoner, origin.Camera.transform, apartment.transform.Find("Furniture") ?? apartment.transform,
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_Click_2.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_Game_Positive.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(k_Fx + "SFX_UI_SciFi_Back.ogg"));
        bind(logic);
        EditorUtility.SetDirty(summoner);
        EditorUtility.SetDirty(logic);
        log.Add("Menú de muebles creado (botón Y del mando izquierdo)");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<b>[Menú de muebles]</b> Hecho y escena guardada:\n- " + string.Join("\n- ", log));
    }

    static GameObject BuildPanel(Transform parent, Camera eventCamera, out System.Action<ObjectMenuPanel> bind)
    {
        var panel = new GameObject("Object Menu Panel", typeof(RectTransform));
        panel.layer = SettingsPanelSetup.k_UILayer;
        panel.transform.SetParent(parent, false);
        panel.transform.localScale = Vector3.one * 0.001f;

        var canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;
        panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        ((RectTransform)panel.transform).sizeDelta = new Vector2(k_Width, k_Height);
        SettingsPanelSetup.AddImage(panel, SettingsPanelSetup.s_Rounded, SettingsPanelSetup.k_Background).raycastTarget = true;

        var inner = k_Width - 2f * k_Padding;

        // Cabecera
        var title = Text(panel.transform, "Muebles", 38, FontStyles.Bold, TextAlignmentOptions.Left, SettingsPanelSetup.k_Text);
        SettingsPanelSetup.Place(title.rectTransform, k_Padding, -k_Padding, inner - 70f, 50f);
        var close = SettingsPanelSetup.MakeButton(panel.transform, "X", SettingsPanelSetup.k_Track, 30);
        SettingsPanelSetup.Place((RectTransform)close.transform, k_Width - k_Padding - 56f, -k_Padding, 56f, 50f);

        var selection = Text(panel.transform, "-", 26, FontStyles.Normal, TextAlignmentOptions.Left, SettingsPanelSetup.k_Text);
        SettingsPanelSetup.Place(selection.rectTransform, k_Padding, -88f, inner, 40f);

        // Color
        Section(panel.transform, "Color", -140f);
        var swatchSize = 64f;
        var swatchGap = (inner - k_Palette.Length * swatchSize) / (k_Palette.Length - 1);
        var colorButtons = new Button[k_Palette.Length];
        for (var i = 0; i < k_Palette.Length; i++)
        {
            var (label, color) = k_Palette[i];
            var swatch = SettingsPanelSetup.MakeButton(panel.transform, label, color, 20);
            swatch.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0.15f, 0.15f, 0.18f);
            SettingsPanelSetup.Place((RectTransform)swatch.transform, k_Padding + i * (swatchSize + swatchGap), -180f, swatchSize, swatchSize);
            colorButtons[i] = swatch;
        }

        // Acciones sobre el seleccionado
        var actionWidth = (inner - 2f * 12f) / 3f;
        var duplicate = ActionButton(panel.transform, "Duplicar", k_Padding, -264f, actionWidth);
        var delete = ActionButton(panel.transform, "Borrar", k_Padding + actionWidth + 12f, -264f, actionWidth);
        var reset = ActionButton(panel.transform, "Resetear", k_Padding + 2f * (actionWidth + 12f), -264f, actionWidth);

        // Catálogo
        Section(panel.transform, "Añadir mueble (aparece delante de ti)", -346f);
        var columns = 4;
        var cellWidth = (inner - (columns - 1) * 12f) / columns;
        var catalogButtons = new Button[k_Catalog.Length];
        var catalog = new ObjectMenuPanel.CatalogEntry[k_Catalog.Length];
        for (var i = 0; i < k_Catalog.Length; i++)
        {
            var (label, path) = k_Catalog[i];
            var x = k_Padding + (i % columns) * (cellWidth + 12f);
            var y = -386f - (i / columns) * 72f;
            catalogButtons[i] = ActionButton(panel.transform, label, x, y, cellWidth);
            catalog[i] = new ObjectMenuPanel.CatalogEntry
            {
                label = label,
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_Prefabs + path + ".prefab"),
            };
        }

        // Pie
        var resetRoom = ActionButton(panel.transform, "Resetear sala", k_Padding, -(k_Height - k_Padding - 60f), 260f);
        var hint = Text(panel.transform, "Coge un mueble con el grip para seleccionarlo.\nGatillo para pulsar los botones.",
            20, FontStyles.Normal, TextAlignmentOptions.Left, SettingsPanelSetup.k_SubtleText);
        SettingsPanelSetup.Place(hint.rectTransform, k_Padding + 280f, -(k_Height - k_Padding - 60f), inner - 280f, 60f);

        var colors = k_Palette.Select(p => p.color).ToArray();
        bind = logic => logic.BindControls(selection, colorButtons, colors, duplicate, delete, reset, resetRoom, close, catalogButtons, catalog);
        return panel;
    }

    static void Section(Transform parent, string text, float y)
    {
        var label = Text(parent, text, 24, FontStyles.Bold, TextAlignmentOptions.Left, SettingsPanelSetup.k_SubtleText);
        SettingsPanelSetup.Place(label.rectTransform, k_Padding, y, k_Width - 2f * k_Padding, 36f);
    }

    static Button ActionButton(Transform parent, string text, float x, float y, float width)
    {
        var button = SettingsPanelSetup.MakeButton(parent, text, SettingsPanelSetup.k_Track, 24);
        SettingsPanelSetup.Place((RectTransform)button.transform, x, y, width, 60f);
        return button;
    }

    static TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
        => SettingsPanelSetup.Label(parent, text, size, style, alignment, color);
}
