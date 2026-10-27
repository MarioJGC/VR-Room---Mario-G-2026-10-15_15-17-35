using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Panel 3D de control sobre los muebles (botón Y del mando izquierdo):
///  - Color del mueble seleccionado (o volver al original).
///  - Duplicar, borrar y resetear el mueble seleccionado.
///  - Catálogo: añadir muebles nuevos delante del usuario.
///  - Resetear la sala: todo vuelve a su sitio, reaparece lo borrado y se quita lo añadido.
/// El mueble seleccionado es el último que se ha cogido (queda resaltado en verde).
/// </summary>
public class ObjectMenuPanel : MonoBehaviour
{
    [Serializable]
    public struct CatalogEntry
    {
        public string label;
        public GameObject prefab;
    }

    [Header("Referencias")]
    [SerializeField] PanelSummoner m_Summoner;
    [SerializeField] Transform m_Head;
    [Tooltip("Dónde se crean los muebles nuevos (Apartment/Furniture).")]
    [SerializeField] Transform m_SpawnParent;

    [Header("Controles")]
    [SerializeField] TMP_Text m_SelectionLabel;
    [SerializeField] Button[] m_ColorButtons;
    [SerializeField] Color[] m_Colors;
    [SerializeField] Button m_DuplicateButton;
    [SerializeField] Button m_DeleteButton;
    [SerializeField] Button m_ResetButton;
    [SerializeField] Button m_ResetRoomButton;
    [SerializeField] Button m_CloseButton;
    [SerializeField] Button[] m_CatalogButtons;
    [SerializeField] CatalogEntry[] m_Catalog;

    [Header("Feedback")]
    [SerializeField] AudioClip m_ClickClip;
    [SerializeField] AudioClip m_SpawnClip;
    [SerializeField] AudioClip m_DeleteClip;

    const float k_SpawnDistance = 1.6f;

    FurnitureItem Selected => SelectionManager.Instance.Current;

    void Awake()
    {
        if (m_Head == null && Camera.main != null)
            m_Head = Camera.main.transform;

        // El índice 0 de la paleta es "original" (quita el color).
        for (var i = 0; i < m_ColorButtons.Length; i++)
        {
            var index = i;
            m_ColorButtons[i].onClick.AddListener(() => ApplyColor(index));
        }
        for (var i = 0; i < m_CatalogButtons.Length; i++)
        {
            var index = i;
            m_CatalogButtons[i].onClick.AddListener(() => Spawn(index));
        }
        m_DuplicateButton.onClick.AddListener(DuplicateSelected);
        m_DeleteButton.onClick.AddListener(DeleteSelected);
        m_ResetButton.onClick.AddListener(ResetSelected);
        m_ResetRoomButton.onClick.AddListener(ResetRoom);
        m_CloseButton.onClick.AddListener(Close);
    }

    void OnEnable()
    {
        SelectionManager.Instance.SelectionChanged += OnSelectionChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (SelectionManager.Exists)
            SelectionManager.Instance.SelectionChanged -= OnSelectionChanged;
    }

    void OnSelectionChanged(FurnitureItem item) => Refresh();

    void Refresh()
    {
        var selected = Selected;
        var hasSelection = selected != null;
        m_SelectionLabel.text = hasSelection
            ? $"Seleccionado: <b>{selected.DisplayName}</b>"
            : "Ningún mueble seleccionado: coge uno con el grip";

        foreach (var button in m_ColorButtons)
            button.interactable = hasSelection;
        m_DuplicateButton.interactable = hasSelection;
        m_DeleteButton.interactable = hasSelection;
        m_ResetButton.interactable = hasSelection;
    }

    // ------------------------------------------------------------------ Acciones sobre el seleccionado

    void ApplyColor(int index)
    {
        var selected = Selected;
        if (selected == null)
        {
            Debug.Log("[Menú de muebles] Color pulsado, pero no hay ningún mueble seleccionado.");
            return;
        }

        if (index == 0) selected.ClearTint();
        else selected.Tint = m_Colors[index];
        Debug.Log($"[Menú de muebles] Color {(index == 0 ? "original" : "#" + ColorUtility.ToHtmlStringRGB(m_Colors[index]))} aplicado a '{selected.DisplayName}'.");
        Play(m_ClickClip);
    }

    void DuplicateSelected()
    {
        var source = Selected;
        if (source == null)
            return;

        var copy = Instantiate(source.gameObject, source.transform.parent);
        copy.name = source.name;

        // Al lado del original, hacia la derecha del usuario.
        var width = FurnitureItem.WorldBounds(source.gameObject).size;
        var side = m_Head != null ? Vector3.ProjectOnPlane(m_Head.right, Vector3.up).normalized : Vector3.right;
        copy.transform.position += side * (Mathf.Max(width.x, width.z) + 0.2f);
        ClampInsideArea(copy);

        var item = copy.GetComponent<FurnitureItem>();
        item.SpawnedAtRuntime = true;
        if (source.HasTint)
            item.Tint = source.Tint;
        item.CaptureInitialPose();
        SelectionManager.Instance.Select(item);
        Play(m_SpawnClip);
    }

    void DeleteSelected()
    {
        var selected = Selected;
        if (selected == null)
            return;

        SelectionManager.Instance.Deselect();
        // Los muebles originales solo se ocultan, para poder recuperarlos con "Resetear sala".
        if (selected.SpawnedAtRuntime) Destroy(selected.gameObject);
        else selected.gameObject.SetActive(false);
        Play(m_DeleteClip);
    }

    void ResetSelected()
    {
        var selected = Selected;
        if (selected == null)
            return;
        selected.ResetToInitial();
        Play(m_ClickClip);
    }

    void ResetRoom()
    {
        SelectionManager.Instance.Deselect();
        foreach (var item in FurnitureItem.All.ToList())
        {
            if (item == null)
                continue;
            if (item.SpawnedAtRuntime)
            {
                Destroy(item.gameObject);
                continue;
            }
            if (!item.gameObject.activeSelf)
                item.gameObject.SetActive(true);
            item.ResetToInitial();
        }

        // Libros, fruta, pelotas... vuelven a su sitio y quietos.
        foreach (var prop in GrabbableProp.All.ToList())
        {
            if (prop != null)
                prop.ResetToInitial();
        }
        Play(m_DeleteClip);
    }

    // ------------------------------------------------------------------ Catálogo

    void Spawn(int index)
    {
        if (index < 0 || index >= m_Catalog.Length || m_Catalog[index].prefab == null || m_Head == null)
            return;

        var forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        var prefab = m_Catalog[index].prefab;
        // Mirando hacia el usuario (el frente de los modelos es +Z).
        var rotation = Quaternion.LookRotation(-forward, Vector3.up) * prefab.transform.rotation;
        var go = Instantiate(prefab, m_Head.position + forward * k_SpawnDistance, rotation, m_SpawnParent);
        go.name = prefab.name;

        // Apoyado en el suelo que hay delante (planta baja, altillo o azotea).
        var target = m_Head.position + forward * k_SpawnDistance;
        var floorY = FindFloorHeight(target, m_Head.position.y - 1.5f);
        var bounds = FurnitureItem.WorldBounds(go);
        go.transform.position += new Vector3(target.x - bounds.center.x, floorY - bounds.min.y, target.z - bounds.center.z);
        ClampInsideArea(go);

        // El collider tiene que existir antes de que XRGrabInteractable registre los suyos.
        FurnitureItem.EnsureCollider(go);
        if (!go.TryGetComponent<FurnitureItem>(out var item))
            item = go.AddComponent<FurnitureItem>();
        item.SpawnedAtRuntime = true;
        item.CaptureInitialPose();
        SelectionManager.Instance.Select(item);
        Play(m_SpawnClip);
    }

    static float FindFloorHeight(Vector3 point, float fallback)
    {
        var hits = Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits.OrderBy(h => h.distance))
        {
            if (hit.collider.GetComponentInParent<TeleportationArea>() != null)
                return hit.point.y;
        }
        return fallback;
    }

    static void ClampInsideArea(GameObject go)
    {
        if (PlacementArea.Instance == null)
            return;
        var b = FurnitureItem.WorldBounds(go);
        var clamped = PlacementArea.Instance.ClampCenter(b.center, b.extents, b.min.y);
        go.transform.position += new Vector3(clamped.x - b.center.x, 0f, clamped.z - b.center.z);
    }

    // ------------------------------------------------------------------ Varios

    void Close()
    {
        Play(m_ClickClip);
        if (m_Summoner != null) m_Summoner.Close();
        else gameObject.SetActive(false);
    }

    void Play(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, 0.5f);
    }

    // --- Configuración desde el script de editor ---

    public void Configure(PanelSummoner summoner, Transform head, Transform spawnParent, AudioClip click, AudioClip spawn, AudioClip delete)
    {
        m_Summoner = summoner;
        m_Head = head;
        m_SpawnParent = spawnParent;
        m_ClickClip = click;
        m_SpawnClip = spawn;
        m_DeleteClip = delete;
    }

    public void BindControls(TMP_Text selectionLabel, Button[] colorButtons, Color[] colors, Button duplicate, Button delete,
        Button reset, Button resetRoom, Button close, Button[] catalogButtons, CatalogEntry[] catalog)
    {
        m_SelectionLabel = selectionLabel;
        m_ColorButtons = colorButtons;
        m_Colors = colors;
        m_DuplicateButton = duplicate;
        m_DeleteButton = delete;
        m_ResetButton = reset;
        m_ResetRoomButton = resetRoom;
        m_CloseButton = close;
        m_CatalogButtons = catalogButtons;
        m_Catalog = catalog;
    }
}
