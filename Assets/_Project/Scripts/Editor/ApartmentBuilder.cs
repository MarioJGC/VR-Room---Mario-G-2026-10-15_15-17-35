using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Construye un apartamento de dos plantas alrededor de la habitación original:
/// planta baja de 10 x 8 m con salón a doble altura, altillo (segunda planta) de 10 x 4 m,
/// escalera, barandilla de cristal, ventanas, luces y muebles/objetos interactivos.
///
/// Menú: Tools > VR Room > Construir apartamento (2 plantas)
/// Se puede volver a ejecutar: borra el objeto "Apartment" anterior y lo genera de nuevo.
///
/// Coordenadas locales: el origen es el centro de la habitación original (suelo a y = 0).
/// X va de -3 (pared de la TV) a 7, Z de -2 (pared de la chimenea) a 6.
/// </summary>
public static class ApartmentBuilder
{
    const float k_MinX = -3f, k_MaxX = 7f, k_MinZ = -2f, k_MaxZ = 6f;
    const float k_StoreyHeight = 2.75f;
    const float k_LoftStartZ = 2f;
    const float k_WallThickness = 0.1f;
    const float k_ModuleWidth = 2f;
    // La escalera sube en dirección +X pegada al borde del altillo y termina en un rellano en la esquina.
    const float k_StairStartX = 2f, k_StairEndX = 6f;
    const float k_StairMinZ = 1f, k_StairMaxZ = k_LoftStartZ;
    const int k_StairSteps = 15;
    static readonly Vector3 k_Origin = new Vector3(-0.44f, 0.3f, 0.01f);

    const string k_RootName = "Apartment";
    const string k_PrefabRoot = "Assets/_Course Library/_Prefabs/";
    const string k_AudioRoot = "Assets/_Course Library/Audio/";
    const string k_MaterialFolder = "Assets/_Project/Materials";

    enum Wall { Left, Right, Front, Back }

    class Palette
    {
        public Material Wall, Floor, Ceiling, Wood, Metal, Glass, Switch, TvScreen;
        public RenderTexture TvTexture;
    }

    static Transform s_Root, s_Structure, s_Furniture, s_Props, s_Lights;
    static Palette s_Mat;
    static List<string> s_Log;
    static InteractionLayerMask? s_TeleportLayers;
    static BaseTeleportationInteractable.TeleportTrigger? s_TeleportTrigger;
    static AudioClip s_ClickClip;

    [MenuItem("Tools/VR Room/Construir apartamento (2 plantas)")]
    static void Build()
    {
        if (!EditorUtility.DisplayDialog("Construir apartamento",
                "Se construirá un apartamento de dos plantas con escalera, luces y objetos interactivos.\n\n" +
                "- Se desactiva 'Room_Modern' (no se borra).\n" +
                "- Si ya existe 'Apartment', se reemplaza (se pierden los cambios manuales hechos dentro de él).\n\n" +
                "¿Continuar?", "Construir", "Cancelar"))
            return;

        var scene = SceneManager.GetActiveScene();
        s_Log = new List<string>();
        s_ClickClip = LoadAsset<AudioClip>(k_AudioRoot + "FX/SFX_UI_Click_1.ogg");
        Undo.SetCurrentGroupName("Construir apartamento");
        var undoGroup = Undo.GetCurrentGroup();

        RemovePrevious(scene);
        s_Mat = CreateMaterials();

        var root = new GameObject(k_RootName);
        Undo.RegisterCreatedObjectUndo(root, "Crear apartamento");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = k_Origin;
        s_Root = root.transform;
        s_Structure = CreateChild(s_Root, "Structure");
        s_Lights = CreateChild(s_Root, "Lights");
        s_Furniture = CreateChild(s_Root, "Furniture");
        s_Props = CreateChild(s_Root, "Props");

        // Zonas donde se pueden mover los muebles: no salen de la casa ni del altillo.
        root.AddComponent<PlacementArea>().SetZones(new[]
        {
            new PlacementArea.Zone { floorHeight = 0f, rect = new Rect(k_MinX, k_MinZ, k_MaxX - k_MinX, k_MaxZ - k_MinZ) },
            new PlacementArea.Zone { floorHeight = k_StoreyHeight, rect = new Rect(k_MinX, k_LoftStartZ, k_MaxX - k_MinX, k_MaxZ - k_LoftStartZ) },
        });

        BuildFloors();
        BuildWalls();
        BuildStairs();
        BuildRailing();
        BuildLights();
        RelocateMirror(scene);
        RelocateDiningArea(scene);
        var tv = SetupTv(scene);
        FurnishLivingRoom(scene, tv);
        FurnishUnderLoft();
        FurnishLoft();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<b>[Apartamento]</b> Construido y escena guardada:\n- " + string.Join("\n- ", s_Log));
    }

    // ------------------------------------------------------------------ Preparación

    static void RemovePrevious(Scene scene)
    {
        // Guardamos la configuración del teletransporte actual para reutilizarla.
        var oldArea = FindByName(scene, "Teleport Floor");
        var previous = FindByName(scene, k_RootName);
        var reference = oldArea != null ? oldArea.GetComponentInChildren<TeleportationArea>(true)
            : previous != null ? previous.GetComponentInChildren<TeleportationArea>(true) : null;
        if (reference != null)
        {
            s_TeleportLayers = reference.interactionLayers;
            s_TeleportTrigger = reference.teleportTrigger;
        }

        if (previous != null)
        {
            Undo.DestroyObjectImmediate(previous);
            s_Log.Add("Reemplazado el apartamento anterior");
        }
        if (oldArea != null)
        {
            Undo.DestroyObjectImmediate(oldArea);
            s_Log.Add("Quitado 'Teleport Floor' (sustituido por las zonas de cada planta)");
        }

        var room = FindByName(scene, "Room_Modern");
        if (room != null && room.activeSelf)
        {
            Undo.RecordObject(room, "Desactivar Room_Modern");
            room.SetActive(false);
            s_Log.Add("Desactivada la habitación original 'Room_Modern'");
        }
    }

    static Palette CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");

        const string objectMaterials = "Assets/_Course Library/Materials/Object Materials/";
        var palette = new Palette
        {
            Wall = LitMaterial("M_Wall", new Color(0.92f, 0.91f, 0.88f), 0.15f),
            Ceiling = LitMaterial("M_Ceiling", new Color(0.97f, 0.97f, 0.96f), 0.05f),
            Metal = LitMaterial("M_Metal", new Color(0.16f, 0.16f, 0.17f), 0.6f, 0.8f),
            Switch = LitMaterial("M_Switch", Color.white, 0.4f),
            Floor = CopyMaterial("M_Floor_Wood", objectMaterials + "Material_Wood_Light.mat", new Vector2(5f, 4f)),
            Wood = CopyMaterial("M_Stairs_Wood", objectMaterials + "Material_Wood_Dark.mat", Vector2.one),
            Glass = LoadAsset<Material>(objectMaterials + "Material_Glass.mat"),
        };

        var rtPath = k_MaterialFolder + "/RT_TV.renderTexture";
        palette.TvTexture = LoadAsset<RenderTexture>(rtPath);
        if (palette.TvTexture == null)
        {
            palette.TvTexture = new RenderTexture(1280, 720, 0) { name = "RT_TV" };
            AssetDatabase.CreateAsset(palette.TvTexture, rtPath);
        }

        palette.TvScreen = GetOrCreateMaterial("M_TV_Screen", Shader.Find("Universal Render Pipeline/Unlit"));
        palette.TvScreen.SetTexture("_BaseMap", palette.TvTexture);
        palette.TvScreen.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(palette.TvScreen);

        AssetDatabase.SaveAssets();
        return palette;
    }

    // ------------------------------------------------------------------ Estructura

    static void BuildFloors()
    {
        var width = k_MaxX - k_MinX;
        var depth = k_MaxZ - k_MinZ;
        var centerX = (k_MinX + k_MaxX) / 2f;
        var centerZ = (k_MinZ + k_MaxZ) / 2f;
        var loftDepth = k_MaxZ - k_LoftStartZ;
        var loftCenterZ = (k_LoftStartZ + k_MaxZ) / 2f;

        Box("Floor_Ground", s_Structure, new Vector3(centerX, -0.05f, centerZ), new Vector3(width, 0.1f, depth), s_Mat.Floor);
        Box("Floor_Loft", s_Structure, new Vector3(centerX, k_StoreyHeight - 0.025f, loftCenterZ), new Vector3(width, 0.05f, loftDepth), s_Mat.Floor);
        Box("Ceiling_UnderLoft", s_Structure, new Vector3(centerX, k_StoreyHeight - 0.125f, loftCenterZ), new Vector3(width, 0.15f, loftDepth), s_Mat.Ceiling);
        Box("Roof", s_Structure, new Vector3(centerX, 2f * k_StoreyHeight + 0.05f, centerZ), new Vector3(width + 0.2f, 0.1f, depth + 0.2f), s_Mat.Ceiling);

        // Rellano de la escalera (esquina entre el final de la escalera y la pared derecha).
        var landingCenter = new Vector3((k_StairEndX + k_MaxX) / 2f, 0f, (k_StairMinZ + k_StairMaxZ) / 2f);
        var landingSize = new Vector3(k_MaxX - k_StairEndX, 0f, k_StairMaxZ - k_StairMinZ);
        Box("Landing", s_Structure, landingCenter + Vector3.up * (k_StoreyHeight - 0.025f), landingSize + Vector3.up * 0.05f, s_Mat.Floor);
        Box("Ceiling_UnderLanding", s_Structure, landingCenter + Vector3.up * (k_StoreyHeight - 0.125f), landingSize + Vector3.up * 0.15f, s_Mat.Ceiling);

        // Planta baja sin la zona de debajo de la escalera (no hay altura para estar ahí).
        TeleportSurface("Teleport_Ground", 0f,
            new Rect(k_MinX, k_MinZ, width, k_StairMinZ - k_MinZ),
            new Rect(k_MinX, k_StairMinZ, k_StairStartX - k_MinX, k_StairMaxZ - k_StairMinZ),
            new Rect(k_StairEndX, k_StairMinZ, k_MaxX - k_StairEndX, k_StairMaxZ - k_StairMinZ),
            new Rect(k_MinX, k_LoftStartZ, width, loftDepth));
        TeleportSurface("Teleport_Loft", k_StoreyHeight,
            new Rect(k_MinX, k_LoftStartZ, width, loftDepth),
            new Rect(k_StairEndX, k_StairMinZ, k_MaxX - k_StairEndX, k_StairMaxZ - k_StairMinZ));
        s_Log.Add($"Suelos: planta baja {width} x {depth} m y altillo {width} x {loftDepth} m a {k_StoreyHeight} m de altura");
    }

    static void BuildWalls()
    {
        var walls = CreateChild(s_Structure, "Walls");
        // S = pared, W = ventana, D = puerta. Planta baja / planta alta, un carácter por cada módulo de 2 m.
        BuildWall(walls, "Wall_Front", new Vector3(k_MinX, 0f, k_MinZ), Vector3.right, Vector3.forward, "SWDWS", "SWWWS");
        BuildWall(walls, "Wall_Back", new Vector3(k_MinX, 0f, k_MaxZ), Vector3.right, Vector3.back, "SWSWS", "SWWWS");
        BuildWall(walls, "Wall_Left", new Vector3(k_MinX, 0f, k_MinZ), Vector3.forward, Vector3.right, "SSWS", "SWWS");
        BuildWall(walls, "Wall_Right", new Vector3(k_MaxX, 0f, k_MinZ), Vector3.forward, Vector3.left, "SSWS", "SWWS");

        // Pilares de esquina para cerrar las juntas exteriores.
        var height = 2f * k_StoreyHeight;
        var half = k_WallThickness / 2f;
        foreach (var x in new[] { k_MinX - half, k_MaxX + half })
        foreach (var z in new[] { k_MinZ - half, k_MaxZ + half })
            Box("Corner", walls, new Vector3(x, height / 2f, z), new Vector3(k_WallThickness, height, k_WallThickness), s_Mat.Wall);
    }

    static void BuildWall(Transform parent, string name, Vector3 start, Vector3 along, Vector3 inward, string ground, string upper)
    {
        var wall = CreateChild(parent, name);
        for (var storey = 0; storey < 2; storey++)
        {
            var pattern = storey == 0 ? ground : upper;
            for (var i = 0; i < pattern.Length; i++)
            {
                var moduleStart = start + along * (i * k_ModuleWidth) + Vector3.up * (storey * k_StoreyHeight);
                WallModule(wall, moduleStart, along, inward, pattern[i]);
            }
        }
    }

    /// <summary>Un módulo de pared de 2 m de ancho por una planta de alto.</summary>
    static void WallModule(Transform parent, Vector3 start, Vector3 along, Vector3 inward, char type)
    {
        var rotation = Quaternion.LookRotation(inward);
        const float h = k_StoreyHeight;
        const float t = k_WallThickness;

        // a0..a1 = tramo a lo largo de la pared, y0..y1 = altura, offset = desplazamiento hacia dentro.
        void Piece(string n, float a0, float a1, float y0, float y1, Material mat, float offset = -t / 2f, float thickness = t, bool collider = true)
        {
            var center = start + along * ((a0 + a1) / 2f) + inward * offset + Vector3.up * ((y0 + y1) / 2f);
            Box(n, parent, center, new Vector3(a1 - a0, y1 - y0, thickness), mat, rotation, collider);
        }

        switch (type)
        {
            case 'W':
                const float sill = 0.9f, head = 2.2f;
                Piece("Wall", 0f, 2f, 0f, sill, s_Mat.Wall);
                Piece("Wall", 0f, 2f, head, h, s_Mat.Wall);
                Piece("Wall", 0f, 0.4f, sill, head, s_Mat.Wall);
                Piece("Wall", 1.6f, 2f, sill, head, s_Mat.Wall);
                Piece("Window_Glass", 0.4f, 1.6f, sill, head, s_Mat.Glass, -t / 2f, 0.02f);
                Piece("Window_Ledge", 0.35f, 1.65f, sill - 0.04f, sill, s_Mat.Metal, 0.03f, 0.08f, false);
                break;
            case 'D':
                Piece("Wall", 0f, 2f, 0f, h, s_Mat.Wall);
                Piece("Door", 0.55f, 1.45f, 0f, 2.05f, s_Mat.Wood, 0.02f, 0.04f);
                Piece("Door_Frame", 0.5f, 0.55f, 0f, 2.1f, s_Mat.Metal, 0.015f, 0.03f, false);
                Piece("Door_Frame", 1.45f, 1.5f, 0f, 2.1f, s_Mat.Metal, 0.015f, 0.03f, false);
                Piece("Door_Frame", 0.5f, 1.5f, 2.05f, 2.1f, s_Mat.Metal, 0.015f, 0.03f, false);
                Piece("Door_Handle", 1.25f, 1.37f, 1.0f, 1.03f, s_Mat.Metal, 0.06f, 0.03f, false);
                break;
            default:
                Piece("Wall", 0f, 2f, 0f, h, s_Mat.Wall);
                break;
        }
    }

    static void BuildStairs()
    {
        var stairs = CreateChild(s_Structure, "Stairs");
        var horizontal = k_StairEndX - k_StairStartX;
        var run = horizontal / k_StairSteps;
        var rise = k_StoreyHeight / k_StairSteps;
        var width = k_StairMaxZ - k_StairMinZ;
        var centerZ = (k_StairMinZ + k_StairMaxZ) / 2f;
        var length = Mathf.Sqrt(horizontal * horizontal + k_StoreyHeight * k_StoreyHeight);
        var angle = Mathf.Atan2(k_StoreyHeight, horizontal) * Mathf.Rad2Deg;
        var slope = Quaternion.Euler(0f, 0f, angle);   // sube hacia +X
        var midX = (k_StairStartX + k_StairEndX) / 2f;
        var openSideZ = k_StairMinZ + 0.03f;           // lado abierto (hacia el salón)

        // Peldaños sin collider: se camina/teletransporta sobre una rampa invisible.
        for (var i = 0; i < k_StairSteps; i++)
        {
            var top = (i + 1) * rise;
            var x0 = k_StairStartX + i * run;
            Box($"Step_{i + 1:00}", stairs, new Vector3(x0 + run / 2f, top - 0.03f, centerZ), new Vector3(run + 0.02f, 0.06f, width), s_Mat.Wood, collider: false);
        }

        Box("Stringer", stairs, new Vector3(midX, k_StoreyHeight / 2f - 0.1f, openSideZ), new Vector3(length, 0.3f, 0.06f), s_Mat.Metal, slope, false);

        // Rampa invisible: el CharacterController la sube caminando y también sirve de zona de teletransporte.
        // Va de (inicio, suelo) a (fin, altillo), así empieza y termina a ras, sin escalón.
        var ramp = new GameObject("Teleport_Stairs");
        ramp.transform.SetParent(stairs, false);
        ramp.transform.localPosition = new Vector3(midX, k_StoreyHeight / 2f - 0.01f, centerZ);
        ramp.transform.localRotation = slope;
        var rampCollider = ramp.AddComponent<BoxCollider>();
        rampCollider.size = new Vector3(length, 0.02f, width);
        ConfigureTeleportArea(ramp.AddComponent<TeleportationArea>());

        // Pasamanos en el lado abierto.
        const float railHeight = 0.95f;
        Box("Handrail", stairs, new Vector3(midX, k_StoreyHeight / 2f + rise / 2f + railHeight, openSideZ), new Vector3(length, 0.05f, 0.05f), s_Mat.Metal, slope, false);
        for (var k = 0; k <= 4; k++)
        {
            var x = k_StairStartX + 0.15f + k * (horizontal - 0.3f) / 4f;
            var stepTop = rise * (Mathf.Floor((x - k_StairStartX) / run) + 1f);
            var railY = rise / 2f + (x - k_StairStartX) * (k_StoreyHeight / horizontal) + railHeight;
            Box("Handrail_Post", stairs, new Vector3(x, (stepTop + railY) / 2f, openSideZ), new Vector3(0.04f, railY - stepTop, 0.04f), s_Mat.Metal, collider: false);
        }

        s_Log.Add($"Escalera de {k_StairSteps} peldaños (de x = {k_StairStartX} a {k_StairEndX}) con rellano, pasamanos y rampa caminable");
    }

    static void BuildRailing()
    {
        var railing = CreateChild(s_Structure, "Loft_Railing");
        const float height = 1.0f;

        // Borde del altillo (se deja libre el hueco por el que se llega desde el rellano).
        RailingSegment(railing, k_MinX, k_StairEndX, k_LoftStartZ + 0.03f, height);
        // Lado del rellano que da al salón.
        RailingSegment(railing, k_StairEndX, k_MaxX, k_StairMinZ + 0.03f, height);
    }

    static void RailingSegment(Transform parent, float x0, float x1, float z, float height)
    {
        var length = x1 - x0;
        var centerX = (x0 + x1) / 2f;
        Box("Railing_Glass", parent, new Vector3(centerX, k_StoreyHeight + height / 2f, z), new Vector3(length, height, 0.02f), s_Mat.Glass);
        Box("Railing_Top", parent, new Vector3(centerX, k_StoreyHeight + height + 0.025f, z), new Vector3(length + 0.05f, 0.05f, 0.06f), s_Mat.Metal);
        for (var x = x0 + 0.05f; x <= x1; x += 1.5f)
            Box("Railing_Post", parent, new Vector3(x, k_StoreyHeight + height / 2f, z), new Vector3(0.05f, height, 0.05f), s_Mat.Metal, collider: false);
    }

    static void BuildLights()
    {
        var warm = new Color(1f, 0.95f, 0.88f);
        PointLight("Light_Living", s_Lights, new Vector3(1f, 4.9f, 0f), 9f, 1.2f, warm);
        PointLight("Light_UnderLoft_West", s_Lights, new Vector3(-0.5f, 2.35f, 4f), 6f, 1.0f, warm);
        PointLight("Light_UnderLoft_East", s_Lights, new Vector3(4.5f, 2.35f, 4f), 6f, 1.0f, warm);
        PointLight("Light_Loft", s_Lights, new Vector3(2f, 5.1f, 4f), 8f, 1.4f, warm);
    }

    // ------------------------------------------------------------------ Mobiliario

    static void RelocateMirror(Scene scene)
    {
        // El espejo estaba en la pared derecha antigua (x = 3); lo llevamos a la nueva pared derecha.
        var mirror = FindByName(scene, "Mirror_Square_Black");
        if (mirror == null)
            return;
        Undo.RecordObject(mirror.transform, "Mover espejo");
        var p = mirror.transform.position;
        mirror.transform.position = new Vector3(k_Origin.x + k_MaxX - 0.06f, p.y, k_Origin.z + 5f);
        s_Log.Add("Espejo movido a la nueva pared derecha (bajo el altillo)");
    }

    static void RelocateDiningArea(Scene scene)
    {
        // La mesa de comedor y su silla ocupaban el arranque de la escalera: se adelantan hacia la ventana.
        MoveExisting(scene, "Table_Dining_Modern_Light", 3.2f, -0.6f);
        MoveExisting(scene, "Chair_Modern", 3.2f, 0.3f);
    }

    static void MoveExisting(Scene scene, string objectName, float x, float z)
    {
        var item = FindAll<FurnitureItem>(scene).FirstOrDefault(f => f.name == objectName);
        if (item == null)
            return;
        Undo.RecordObject(item.transform, "Mover " + objectName);
        var b = WorldBounds(item.gameObject);
        var target = s_Root.TransformPoint(new Vector3(x, 0f, z));
        item.transform.position += new Vector3(target.x - b.center.x, 0f, target.z - b.center.z);
        s_Log.Add($"'{objectName}' movido para dejar libre la escalera");
    }

    static ToggleDevice SetupTv(Scene scene)
    {
        var tv = FindAll<FurnitureItem>(scene).FirstOrDefault(f => f.name == "TV_FlatWithStand");
        if (tv == null)
        {
            s_Log.Add("AVISO: no se encontró la TV");
            return null;
        }

        var screenTransform = tv.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Screen");
        if (screenTransform == null || !screenTransform.TryGetComponent<Renderer>(out var screen))
        {
            s_Log.Add("AVISO: la TV no tiene objeto 'Screen'");
            return null;
        }

        if (!screen.TryGetComponent<VideoPlayer>(out var video))
            video = Undo.AddComponent<VideoPlayer>(screen.gameObject);
        Undo.RecordObject(video, "Configurar vídeo");
        video.playOnAwake = false;
        video.isLooping = true;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = s_Mat.TvTexture;
        video.clip = LoadAsset<VideoClip>("Assets/_Course Library/Video/Video_Unity-for-everyone.mp4");
        video.audioOutputMode = VideoAudioOutputMode.Direct;

        if (!tv.TryGetComponent<ToggleDevice>(out var device))
            device = Undo.AddComponent<ToggleDevice>(tv.gameObject);
        Undo.RecordObject(device, "Configurar TV");
        device.SetVideo(video, screen, s_Mat.TvScreen);
        device.Configure(false, s_ClickClip);
        EditorUtility.SetDirty(video);
        EditorUtility.SetDirty(device);

        s_Log.Add("TV: reproduce vídeo al encenderla con el mando a distancia");
        return device;
    }

    static void FurnishLivingRoom(Scene scene, ToggleDevice tv)
    {
        // Lámpara de techo a doble altura con interruptor junto a la puerta.
        var chandelier = Spawn("Lighting/Chandelier_Modern", s_Furniture);
        if (chandelier != null)
        {
            HangFromCeiling(chandelier, 0.5f, 0f, 2f * k_StoreyHeight);
            var device = chandelier.AddComponent<ToggleDevice>();
            device.AddLight(PointLight("Chandelier_Light", chandelier.transform, Vector3.zero, 9f, 1.8f, new Color(1f, 0.9f, 0.75f), WorldBounds(chandelier).center));
            device.Configure(true, s_ClickClip);

            var lightSwitch = Box("Light_Switch", s_Furniture, new Vector3(k_MinX + 4.25f, 1.2f, k_MinZ + 0.01f), new Vector3(0.08f, 0.12f, 0.02f), s_Mat.Switch);
            lightSwitch.AddComponent<ClickToggle>().Target = device;
            s_Log.Add("Lámpara de techo con interruptor de pared junto a la puerta");
        }

        // Mesita junto al sofá con lámpara y el mando de la TV.
        var couch = FindAll<FurnitureItem>(scene).FirstOrDefault(f => f.name == "Couch_Modern_Gray");
        if (couch != null)
        {
            var couchBounds = WorldBounds(couch.gameObject);
            var local = s_Root.InverseTransformPoint(couchBounds.center);
            var sideTable = Furniture("Tables/Table_Side_Modern_Black", local.x, s_Root.InverseTransformPoint(couchBounds.max).z + 0.35f, 0f, 0f);
            if (sideTable != null)
            {
                Lamp("Lighting/Lamp_Table_Red", sideTable, -0.15f, 0f, false);
                var remote = Prop("Objects/Electronics/Remote", sideTable, 0.25f, 0.1f, "SFX_Impact_Plastic");
                if (remote != null && tv != null)
                    remote.AddComponent<ActivateToggle>().Target = tv;
            }
        }

        var dining = FindAll<FurnitureItem>(scene).FirstOrDefault(f => f.name == "Table_Dining_Modern_Light");
        if (dining != null)
        {
            Prop("Objects/Fruit/Food_Apple_Red", dining.gameObject, -0.2f, 0.1f, "SFX_Impact_Plastic");
            Prop("Objects/Fruit/Food_Apple_Green", dining.gameObject, -0.1f, -0.15f, "SFX_Impact_Plastic");
            Prop("Objects/Fruit/Food_Orange", dining.gameObject, 0.05f, 0.05f, "SFX_Impact_Plastic");
            Prop("Objects/Containers/Container_Bottle_Blue", dining.gameObject, 0.25f, -0.1f, "SFX_Impact_Glass_1");
        }

        Furniture("Plants/Plant_A", k_MinX + 0.45f, 2.6f, 0f, 0f);
        Art("Art/Art_Painting_Mountain", Wall.Left, -1f, k_StoreyHeight + 1.4f);
        Art("Art/Art_Painting_Sunset", Wall.Front, -2f, k_StoreyHeight + 1.4f);
    }

    static void FurnishUnderLoft()
    {
        Furniture("Rugs/Rug_Rect_Gray", 2f, 4.4f, 0f, 0f);
        Furniture("Seating/Couch_Modern_Brown", 2f, 5.4f, 0f, 180f, Wall.Back);
        Art("Art/Art_Painting_Desert", Wall.Back, 2f, 1.85f);

        var coffeeTable = Furniture("Tables/Table_Coffee_Modern", 2f, 4.2f, 0f, 0f);
        if (coffeeTable != null)
        {
            Prop("Objects/Books/Book_A_Red", coffeeTable, -0.25f, 0f, "SFX_Impact_Books_1");
            Prop("Objects/Books/Book_B_Blue", coffeeTable, -0.05f, 0.1f, "SFX_Impact_Books_2");
            Prop("Objects/Electronics/Gaming_Controller", coffeeTable, 0.25f, 0f, "SFX_Impact_Plastic");
        }

        var shelving = Furniture("Storage/Shelving_Modern_White", k_MinX + 0.4f, 5f, 0f, 90f, Wall.Left);
        if (shelving != null)
        {
            Prop("Objects/Books/Book_C_Green", shelving, 0f, -0.25f, "SFX_Impact_Books_1");
            var lantern = Prop("Objects/Light Sources/Lightsource_Lantern", shelving, 0f, 0.2f, "SFX_Impact_Metal");
            if (lantern != null)
            {
                var device = lantern.AddComponent<ToggleDevice>();
                device.AddLight(PointLight("Lantern_Light", lantern.transform, Vector3.zero, 4f, 1.2f, new Color(1f, 0.8f, 0.55f), WorldBounds(lantern).center));
                device.Configure(false, s_ClickClip);
                lantern.AddComponent<ActivateToggle>();
            }
        }

        var cabinet = Furniture("Storage/Cabinet_Modern_Gray", 6f, 5.6f, 0f, 180f, Wall.Back);
        if (cabinet != null)
        {
            var player = SpawnOnSurface("Objects/Speakers/Speaker_RecordPlayer_Gray", cabinet, 0f, 0f, s_Props);
            if (player != null)
            {
                var audio = player.AddComponent<AudioSource>();
                audio.clip = LoadAsset<AudioClip>(k_AudioRoot + "Music/Music_Classical_Chopin_Nocturne-no-2.ogg");
                audio.loop = true;
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.volume = 0.6f;
                audio.minDistance = 1f;
                audio.maxDistance = 15f;
                var device = player.AddComponent<ToggleDevice>();
                device.SetAudio(audio);
                device.Configure(false, s_ClickClip);
                EnsureCollider(player);
                player.AddComponent<ClickToggle>();
                s_Log.Add("Tocadiscos con música (apuntar y pulsar grip)");
            }
        }

        Lamp("Lighting/Lamp_Floor_Double", null, 4f, 5.65f, false, 0f);
    }

    static void FurnishLoft()
    {
        const float y = k_StoreyHeight;
        Furniture("Rugs/Rug_Oval_Blue", 2f, 4.0f, y, 0f);

        var desk = Furniture("Tables/Table_Dining_Modern_Dark", 2f, 5.5f, y, 0f, Wall.Back);
        if (desk != null)
        {
            var deskBounds = WorldBounds(desk);
            Furniture("Seating/Chair_Modern_Black", 2f, s_Root.InverseTransformPoint(deskBounds.min).z - 0.35f, y, 0f);
            Prop("Objects/Electronics/Tablet_Black", desk, -0.25f, 0f, "SFX_Impact_Plastic");
            Prop("Objects/Notebooks/Notebook_Stripes", desk, 0f, 0.1f, "SFX_Impact_Books_2");
            Prop("Objects/Electronics/Clock_Digital", desk, 0.3f, 0.2f, "SFX_Impact_Plastic");
        }

        var drawers = Furniture("Storage/Drawers_Modern_Light", k_MinX + 0.4f, 5f, y, 90f, Wall.Left);
        if (drawers != null)
        {
            Lamp("Lighting/Lamp_Table_Blue", drawers, 0f, -0.2f, false);
            Prop("Objects/Hats/Hat_Cap", drawers, 0f, 0.25f, "SFX_Impact_Plastic");
        }
        Art("Art/Art_Painting_Birds", Wall.Left, 5f, y + 1.75f);

        var shelving = Furniture("Storage/Shelving_Modern_Gray", k_MaxX - 0.4f, 5f, y, -90f, Wall.Right);
        if (shelving != null)
        {
            Prop("Objects/Books/Book_A_Blue", shelving, 0f, -0.2f, "SFX_Impact_Books_1");
            Prop("Objects/Books/Book_B_BrownRed", shelving, 0f, 0.15f, "SFX_Impact_Books_2");
        }

        Lamp("Lighting/Lamp_Floor_Square", null, k_MinX + 0.4f, 2.5f, false, y);
        Furniture("Plants/Plant_B", k_MaxX - 0.45f, 3.7f, y, 0f);

        FloorProp("Objects/Sports/Tennis_Ball_Green", 1.2f, 3.5f, y, "SFX_Impact_Ball_1");
        FloorProp("Objects/Sports/Baseball_Ball", 2.8f, 3.6f, y, "SFX_Impact_Ball_1");
        s_Log.Add("Altillo amueblado: escritorio, cajonera con lámpara, estantería, plantas y pelotas");
    }

    // ------------------------------------------------------------------ Ayudantes de colocación

    /// <summary>Mueble manipulable (FurnitureItem) apoyado en el suelo y, opcionalmente, pegado a una pared.</summary>
    static GameObject Furniture(string path, float x, float z, float floorY, float rotY, Wall? wall = null)
    {
        var go = Spawn(path, s_Furniture);
        if (go == null)
            return null;
        PlaceOnFloor(go, x, z, floorY, rotY);
        if (wall.HasValue)
            PushToWall(go, wall.Value);
        EnsureCollider(go);
        go.AddComponent<FurnitureItem>();
        return go;
    }

    /// <summary>Objeto pequeño con física colocado encima de otro objeto.</summary>
    static GameObject Prop(string path, GameObject surface, float offsetX, float offsetZ, string impactClip)
    {
        var go = SpawnOnSurface(path, surface, offsetX, offsetZ, s_Props);
        if (go == null)
            return null;
        MakeProp(go, impactClip);
        return go;
    }

    static void FloorProp(string path, float x, float z, float floorY, string impactClip)
    {
        var go = Spawn(path, s_Props);
        if (go == null)
            return;
        PlaceOnFloor(go, x, z, floorY + 0.02f, 0f);
        MakeProp(go, impactClip);
    }

    static void MakeProp(GameObject go, string impactClip)
    {
        EnsureCollider(go);
        var prop = go.AddComponent<GrabbableProp>();
        prop.ImpactClip = LoadAsset<AudioClip>(k_AudioRoot + "FX/" + impactClip + ".ogg");
    }

    /// <summary>Lámpara que se enciende/apaga apuntando y pulsando. Sobre una superficie o en el suelo.</summary>
    static void Lamp(string path, GameObject surface, float a, float b, bool startOn, float floorY = 0f)
    {
        var go = surface != null ? SpawnOnSurface(path, surface, a, b, s_Furniture) : Spawn(path, s_Furniture);
        if (go == null)
            return;
        if (surface == null)
            PlaceOnFloor(go, a, b, floorY, 0f);

        var bounds = WorldBounds(go);
        var device = go.AddComponent<ToggleDevice>();
        device.AddLight(PointLight("Lamp_Light", go.transform, Vector3.zero, 4f, 1.0f, new Color(1f, 0.85f, 0.65f),
            new Vector3(bounds.center.x, bounds.max.y - 0.15f, bounds.center.z)));
        device.Configure(startOn, s_ClickClip);
        EnsureCollider(go);
        go.AddComponent<ClickToggle>();
    }

    static GameObject SpawnOnSurface(string path, GameObject surface, float offsetX, float offsetZ, Transform parent)
    {
        var go = Spawn(path, parent);
        if (go == null)
            return null;
        var s = WorldBounds(surface);
        var target = new Vector3(s.center.x + offsetX * s.size.x, s.max.y + 0.005f, s.center.z + offsetZ * s.size.z);
        MoveBoundsTo(go, target);
        return go;
    }

    static void PlaceOnFloor(GameObject go, float x, float z, float floorY, float rotY)
    {
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f) * go.transform.rotation;
        MoveBoundsTo(go, s_Root.TransformPoint(new Vector3(x, floorY, z)));
    }

    /// <summary>Mueve el objeto para que la base de sus bounds quede centrada en el punto indicado.</summary>
    static void MoveBoundsTo(GameObject go, Vector3 bottomCenter)
    {
        var b = WorldBounds(go);
        go.transform.position += new Vector3(bottomCenter.x - b.center.x, bottomCenter.y - b.min.y, bottomCenter.z - b.center.z);
    }

    static void PushToWall(GameObject go, Wall wall)
    {
        const float gap = 0.03f;
        var b = WorldBounds(go);
        var o = s_Root.position;
        var delta = wall switch
        {
            Wall.Left => new Vector3(o.x + k_MinX + gap - b.min.x, 0f, 0f),
            Wall.Right => new Vector3(o.x + k_MaxX - gap - b.max.x, 0f, 0f),
            Wall.Front => new Vector3(0f, 0f, o.z + k_MinZ + gap - b.min.z),
            _ => new Vector3(0f, 0f, o.z + k_MaxZ - gap - b.max.z),
        };
        go.transform.position += delta;
    }

    static void HangFromCeiling(GameObject go, float x, float z, float ceilingY)
    {
        var b = WorldBounds(go);
        var target = s_Root.TransformPoint(new Vector3(x, ceilingY, z));
        go.transform.position += new Vector3(target.x - b.center.x, target.y - b.max.y, target.z - b.center.z);
    }

    /// <summary>Cuadro colgado en una pared, mirando hacia dentro de la habitación.</summary>
    static void Art(string path, Wall wall, float along, float centerY)
    {
        var go = Spawn(path, s_Furniture);
        if (go == null)
            return;

        var facing = wall switch { Wall.Left => 90f, Wall.Right => -90f, Wall.Back => 180f, _ => 0f };
        var baseRotation = go.transform.rotation;
        go.transform.rotation = Quaternion.Euler(0f, facing, 0f) * baseRotation;

        // Si el cuadro quedó de canto (lo más fino no es perpendicular a la pared), se gira 90°.
        var b = WorldBounds(go);
        var alongX = wall == Wall.Front || wall == Wall.Back;
        if ((alongX ? b.size.z : b.size.x) > (alongX ? b.size.x : b.size.z))
            go.transform.rotation = Quaternion.Euler(0f, facing + 90f, 0f) * baseRotation;

        var position = alongX ? new Vector3(along, centerY, 0f) : new Vector3(0f, centerY, along);
        b = WorldBounds(go);
        var target = s_Root.TransformPoint(position);
        go.transform.position += new Vector3(target.x - b.center.x, target.y - b.center.y, target.z - b.center.z);
        PushToWall(go, wall);
    }

    // ------------------------------------------------------------------ Ayudantes básicos

    static GameObject Spawn(string path, Transform parent)
    {
        var prefab = LoadAsset<GameObject>(k_PrefabRoot + path + ".prefab");
        if (prefab == null)
        {
            s_Log.Add($"AVISO: no se encontró el prefab '{path}'");
            return null;
        }
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    static GameObject Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material material, Quaternion? rotation = null, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = rotation ?? Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collider)
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        if (parent.IsChildOf(s_Structure))
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Light PointLight(string name, Transform parent, Vector3 localPosition, float range, float intensity, Color color, Vector3? worldPosition = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (worldPosition.HasValue)
            go.transform.position = worldPosition.Value;
        else
            go.transform.localPosition = localPosition;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        return light;
    }

    static void TeleportSurface(string name, float height, params Rect[] areas)
    {
        var go = new GameObject(name);
        go.transform.SetParent(s_Structure, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        foreach (var area in areas)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(area.center.x, 0.005f, area.center.y);
            box.size = new Vector3(area.width, 0.01f, area.height);
        }
        ConfigureTeleportArea(go.AddComponent<TeleportationArea>());
    }

    static void ConfigureTeleportArea(TeleportationArea area)
    {
        if (s_TeleportLayers.HasValue)
            area.interactionLayers = s_TeleportLayers.Value;
        if (s_TeleportTrigger.HasValue)
            area.teleportTrigger = s_TeleportTrigger.Value;
    }

    static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null)
            return;
        var b = WorldBounds(go);
        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(b.center);
        var size = go.transform.InverseTransformVector(b.size);
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers.Skip(1))
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static Material LitMaterial(string name, Color color, float smoothness, float metallic = 0f)
    {
        var mat = GetOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EnableEmission(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CopyMaterial(string name, string sourcePath, Vector2 tiling)
    {
        var path = $"{k_MaterialFolder}/{name}.mat";
        var mat = LoadAsset<Material>(path);
        if (mat == null)
        {
            var source = LoadAsset<Material>(sourcePath);
            mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetTextureScale("_BaseMap", tiling);
        EnableEmission(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material GetOrCreateMaterial(string name, Shader shader)
    {
        var path = $"{k_MaterialFolder}/{name}.mat";
        var mat = LoadAsset<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    // Emisión activada en negro: así el brillo de "apuntar" funciona también en estos materiales.
    static void EnableEmission(Material mat)
    {
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    static T LoadAsset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

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
