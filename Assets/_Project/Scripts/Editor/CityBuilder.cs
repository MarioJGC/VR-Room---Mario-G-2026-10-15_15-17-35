using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera una ciudad sencilla alrededor del apartamento (unos 200 m a la redonda):
/// cuadrícula de calles con línea central, manzanas con acera, edificios de colores,
/// algunos parques con árboles y un jardín con camino hasta la puerta del apartamento.
/// Añade niebla suave para que el borde de la ciudad no se vea.
///
/// Menú: Tools > VR Room > Construir ciudad alrededor (200 m). Se puede volver a ejecutar
/// (la distribución es siempre la misma porque usa una semilla fija).
/// </summary>
public static class CityBuilder
{
    const string k_RootName = "City";
    const string k_MaterialFolder = "Assets/_Project/Materials";
    const int k_Seed = 2026;

    const float k_Extent = 200f;        // radio aproximado de la ciudad
    const float k_BlockPitch = 50f;     // distancia entre centros de manzana
    const float k_RoadWidth = 10f;
    const float k_RoadTop = 0.14f;      // altura de la calzada (el suelo del apartamento está a 0.3)
    const float k_LotTop = 0.28f;       // altura de aceras y jardines
    const float k_ParkChance = 0.18f;

    // Puerta del apartamento (mundo): centro en x y pared delantera en z.
    const float k_DoorX = 1.56f, k_FrontWallZ = -1.99f;

    static Transform s_Root;
    static System.Random s_Random;
    static Material s_Asphalt, s_Sidewalk, s_Grass, s_Line, s_Roof, s_Trunk, s_Leaves;
    static Material[] s_Facades;

    [MenuItem("Tools/VR Room/Construir ciudad alrededor (200 m)")]
    static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        var log = new List<string>();
        Undo.SetCurrentGroupName("Construir ciudad");
        var undoGroup = Undo.GetCurrentGroup();
        s_Random = new System.Random(k_Seed);

        var previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == k_RootName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous);

        // El fondo de edificios del curso se sustituye por la ciudad (se desactiva, no se borra).
        foreach (var name in new[] { "Background_Buildings", "Foreground_Buildings" })
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go != null && go.activeSelf)
            {
                Undo.RecordObject(go, "Desactivar " + name);
                go.SetActive(false);
                log.Add($"Desactivado '{name}' (sustituido por la ciudad)");
            }
        }

        CreateMaterials();

        var root = new GameObject(k_RootName);
        Undo.RegisterCreatedObjectUndo(root, "Crear ciudad");
        SceneManager.MoveGameObjectToScene(root, scene);
        s_Root = root.transform;

        var streets = Child(s_Root, "Streets");
        var blocks = Child(s_Root, "Blocks");

        // Calzada: una sola losa de asfalto bajo toda la ciudad; las manzanas van encima.
        var size = 2f * (k_Extent + k_BlockPitch / 2f);
        Box("Asphalt", streets, new Vector3(0f, k_RoadTop - 0.05f, 0f), new Vector3(size, 0.1f, size), s_Asphalt, collider: true);

        // Línea central amarilla en cada calle.
        var roadCount = 0;
        for (var c = -k_Extent + k_BlockPitch / 2f; c < k_Extent; c += k_BlockPitch)
        {
            Box("Line_X", streets, new Vector3(0f, k_RoadTop + 0.005f, c), new Vector3(size, 0.01f, 0.15f), s_Line);
            Box("Line_Z", streets, new Vector3(c, k_RoadTop + 0.005f, 0f), new Vector3(0.15f, 0.01f, size), s_Line);
            roadCount += 2;
        }

        var buildingCount = 0;
        var parkCount = 0;
        for (var bx = -k_Extent; bx <= k_Extent; bx += k_BlockPitch)
        for (var bz = -k_Extent; bz <= k_Extent; bz += k_BlockPitch)
        {
            var center = new Vector3(bx, 0f, bz);
            if (center.magnitude > k_Extent + 30f)
                continue;   // esquinas fuera del círculo

            var block = Child(blocks, $"Block_{bx:0}_{bz:0}");
            if (bx == 0f && bz == 0f)
                BuildHomeBlock(block);
            else if (s_Random.NextDouble() < k_ParkChance)
            {
                BuildPark(block, center);
                parkCount++;
            }
            else
                buildingCount += BuildCityBlock(block, center);
        }

        // Niebla suave: oculta el final de la ciudad y da sensación de distancia.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.76f, 0.83f, 0.90f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 260f;

        log.Add($"Ciudad de {size:0} x {size:0} m: {roadCount} calles, {buildingCount} edificios y {parkCount} parques");
        log.Add("Jardín con camino desde la puerta del apartamento hasta la calle");
        log.Add("Niebla lineal activada (70-260 m)");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<b>[Ciudad]</b> Construida y escena guardada:\n- " + string.Join("\n- ", log));
    }

    // ------------------------------------------------------------------ Manzanas

    static float LotSize => k_BlockPitch - k_RoadWidth;

    static void Lot(Transform parent, Vector3 center, Material material)
    {
        Box("Lot", parent, new Vector3(center.x, (k_RoadTop + k_LotTop) / 2f, center.z),
            new Vector3(LotSize, k_LotTop - k_RoadTop, LotSize), material);
    }

    /// <summary>Manzana del apartamento: jardín, acera perimetral, camino a la puerta y árboles.</summary>
    static void BuildHomeBlock(Transform block)
    {
        var half = LotSize / 2f;
        const float sidewalk = 3f;
        Lot(block, Vector3.zero, s_Grass);

        var y = k_LotTop + 0.005f;
        Box("Sidewalk_S", block, new Vector3(0f, y, -half + sidewalk / 2f), new Vector3(LotSize, 0.01f, sidewalk), s_Sidewalk);
        Box("Sidewalk_N", block, new Vector3(0f, y, half - sidewalk / 2f), new Vector3(LotSize, 0.01f, sidewalk), s_Sidewalk);
        Box("Sidewalk_W", block, new Vector3(-half + sidewalk / 2f, y, 0f), new Vector3(sidewalk, 0.01f, LotSize), s_Sidewalk);
        Box("Sidewalk_E", block, new Vector3(half - sidewalk / 2f, y, 0f), new Vector3(sidewalk, 0.01f, LotSize), s_Sidewalk);

        // Camino desde la puerta (pared delantera) hasta la acera sur.
        var pathStart = k_FrontWallZ;
        var pathEnd = -half + sidewalk;
        Box("Path_To_Door", block, new Vector3(k_DoorX, y, (pathStart + pathEnd) / 2f), new Vector3(2f, 0.01f, pathStart - pathEnd), s_Sidewalk);

        foreach (var p in new[]
                 {
                     new Vector2(-12f, -10f), new Vector2(10f, -12f), new Vector2(-6f, -14f),
                     new Vector2(-13f, 12f), new Vector2(14f, 13f), new Vector2(-9f, 3f), new Vector2(13f, 2f),
                 })
            Tree(block, new Vector3(p.x, k_LotTop, p.y), 1f);
    }

    static void BuildPark(Transform block, Vector3 center)
    {
        Lot(block, center, s_Grass);
        var treeCount = s_Random.Next(5, 9);
        for (var i = 0; i < treeCount; i++)
        {
            var offset = new Vector3(Range(-15f, 15f), 0f, Range(-15f, 15f));
            Tree(block, center + offset + Vector3.up * k_LotTop, Range(0.8f, 1.4f));
        }
    }

    /// <summary>Manzana con hasta 4 edificios (uno por parcela). Más bajos cerca del apartamento.</summary>
    static int BuildCityBlock(Transform block, Vector3 center)
    {
        Lot(block, center, s_Sidewalk);

        var distance = center.magnitude;
        var near = distance < 80f;
        var count = 0;
        for (var ix = -1; ix <= 1; ix += 2)
        for (var iz = -1; iz <= 1; iz += 2)
        {
            if (s_Random.NextDouble() < 0.1)
                continue;   // parcela vacía

            var parcel = center + new Vector3(ix * LotSize / 4f, 0f, iz * LotSize / 4f);
            var width = Range(10f, 16f);
            var depth = Range(10f, 16f);
            var height = near ? Range(6f, 18f) : Range(12f, 45f);
            if (!near && s_Random.NextDouble() < 0.12)
                height = Range(55f, 80f);   // algún rascacielos

            var facade = s_Facades[s_Random.Next(s_Facades.Length)];
            Box("Building", block, new Vector3(parcel.x, k_LotTop + height / 2f, parcel.z), new Vector3(width, height, depth), facade);
            Box("Roof_Unit", block, new Vector3(parcel.x + Range(-2f, 2f), k_LotTop + height + 1f, parcel.z + Range(-2f, 2f)),
                new Vector3(width * 0.35f, 2f, depth * 0.3f), s_Roof);
            count++;
        }
        return count;
    }

    static void Tree(Transform parent, Vector3 basePosition, float scale)
    {
        var trunkHeight = 2.4f * scale;
        var trunk = Primitive(PrimitiveType.Cylinder, "Tree_Trunk", parent, s_Trunk);
        trunk.transform.localPosition = basePosition + Vector3.up * (trunkHeight / 2f);
        trunk.transform.localScale = new Vector3(0.35f * scale, trunkHeight / 2f, 0.35f * scale);

        var canopy = Primitive(PrimitiveType.Sphere, "Tree_Canopy", parent, s_Leaves);
        canopy.transform.localPosition = basePosition + Vector3.up * (trunkHeight + 1.2f * scale);
        canopy.transform.localScale = Vector3.one * (3.2f * scale);
    }

    // ------------------------------------------------------------------ Ayudantes

    static float Range(float min, float max) => min + (float)s_Random.NextDouble() * (max - min);

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = false)
    {
        var go = Primitive(PrimitiveType.Cube, name, parent, material, collider);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        return go;
    }

    /// <summary>Primitiva estática, sin collider (salvo que se pida) y sin proyectar sombras (rendimiento en Quest).</summary>
    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        if (!collider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void CreateMaterials()
    {
        s_Asphalt = Lit("M_City_Asphalt", new Color(0.20f, 0.20f, 0.22f), 0.1f);
        s_Sidewalk = Lit("M_City_Sidewalk", new Color(0.66f, 0.66f, 0.64f), 0.05f);
        s_Grass = Lit("M_City_Grass", new Color(0.33f, 0.52f, 0.26f), 0.0f);
        s_Line = Lit("M_City_RoadLine", new Color(0.95f, 0.80f, 0.20f), 0.2f);
        s_Roof = Lit("M_City_Roof", new Color(0.35f, 0.36f, 0.38f), 0.2f);
        s_Trunk = Lit("M_City_TreeTrunk", new Color(0.36f, 0.25f, 0.16f), 0.0f);
        s_Leaves = Lit("M_City_TreeLeaves", new Color(0.22f, 0.45f, 0.20f), 0.0f);
        s_Facades = new[]
        {
            Lit("M_City_Facade_Beige", new Color(0.86f, 0.80f, 0.68f), 0.1f),
            Lit("M_City_Facade_Grey", new Color(0.72f, 0.74f, 0.76f), 0.2f),
            Lit("M_City_Facade_Blue", new Color(0.50f, 0.60f, 0.72f), 0.5f),
            Lit("M_City_Facade_Brick", new Color(0.66f, 0.38f, 0.30f), 0.1f),
            Lit("M_City_Facade_White", new Color(0.93f, 0.93f, 0.92f), 0.1f),
        };
        AssetDatabase.SaveAssets();
    }

    static Material Lit(string name, Color color, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");

        var path = $"{k_MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
