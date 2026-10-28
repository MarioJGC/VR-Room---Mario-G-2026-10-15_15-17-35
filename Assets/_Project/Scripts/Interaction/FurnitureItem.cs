using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Convierte un objeto en un mueble seleccionable y manipulable con el rayo.
/// Al añadirlo configura automáticamente el XRGrabInteractable, el Rigidbody,
/// las restricciones de movimiento y, si hace falta, un BoxCollider.
///
/// Feedback:
///  - Apuntar con el rayo: brillo azul + vibración suave.
///  - Coger (gatillo/grip): pasa a ser el mueble seleccionado, brillo verde + vibración.
///  - Sigue seleccionado al soltarlo, para poder editarlo desde el menú.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(FloorConstraintTransformer))]
public class FurnitureItem : MonoBehaviour
{
    static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");

    [Header("Información")]
    [Tooltip("Nombre que se mostrará en el menú.")]
    [SerializeField] string m_DisplayName;

    // Brillo suave: si es fuerte, tapa el color elegido en el menú (la emisión se suma al color).
    // Los campos se renombraron para que no se usen los valores antiguos (más intensos) guardados en la escena.
    [Header("Resaltado")]
    [SerializeField, ColorUsage(false, true)] Color m_HoverGlowColor = new Color(0.05f, 0.12f, 0.28f);
    [SerializeField, ColorUsage(false, true)] Color m_SelectedGlowColor = new Color(0.02f, 0.16f, 0.06f);
    [Tooltip("Multiplica el brillo de selección cuando el mueble tiene un color aplicado, para que el color se vea bien.")]
    [SerializeField, Range(0f, 1f)] float m_GlowWhenTinted = 0.35f;

    [Header("Vibración")]
    [SerializeField, Range(0f, 1f)] float m_HoverHaptic = 0.15f;
    [SerializeField, Range(0f, 1f)] float m_SelectHaptic = 0.5f;
    [SerializeField] float m_HapticDuration = 0.08f;

    XRGrabInteractable m_Grab;
    Renderer[] m_Renderers;
    MaterialPropertyBlock m_Block;
    int m_HoverCount;
    bool m_IsSelected;
    bool m_HasTint;
    Color m_Tint = Color.white;
    Pose m_InitialPose;
    Vector3 m_InitialScale;

    static readonly List<FurnitureItem> s_All = new List<FurnitureItem>();

    /// <summary>Todos los muebles de la escena, incluidos los ocultos (borrados desde el menú).</summary>
    public static IReadOnlyList<FurnitureItem> All => s_All;

    public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;

    /// <summary>True si se creó desde el menú (catálogo o duplicar): al resetear la sala se elimina.</summary>
    public bool SpawnedAtRuntime { get; set; }

    public bool HasTint => m_HasTint;

    /// <summary>Color que multiplica la textura del mueble (menú de colores).</summary>
    public Color Tint
    {
        get => m_Tint;
        set
        {
            m_Tint = value;
            m_HasTint = true;
            RefreshVisuals();
        }
    }

    /// <summary>Vuelve al color original del mueble.</summary>
    public void ClearTint()
    {
        m_Tint = Color.white;
        m_HasTint = false;
        RefreshVisuals();
    }

    /// <summary>Toma la posición actual como la inicial (para muebles creados o duplicados desde el menú).</summary>
    public void CaptureInitialPose()
    {
        m_InitialPose = new Pose(transform.position, transform.rotation);
        m_InitialScale = transform.localScale;
    }

    // Se llama al añadir el componente en el editor: deja todo configurado.
    void Reset()
    {
        m_DisplayName = name;
        ConfigureComponents();
    }

    void Awake()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
        ConfigureComponents();

        m_Renderers = GetComponentsInChildren<Renderer>();
        m_Block = new MaterialPropertyBlock();
        m_InitialPose = new Pose(transform.position, transform.rotation);
        m_InitialScale = transform.localScale;
        s_All.Add(this);
    }

    void OnDestroy() => s_All.Remove(this);

    void OnEnable()
    {
        m_Grab.hoverEntered.AddListener(OnHoverEntered);
        m_Grab.hoverExited.AddListener(OnHoverExited);
        m_Grab.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        m_Grab.hoverEntered.RemoveListener(OnHoverEntered);
        m_Grab.hoverExited.RemoveListener(OnHoverExited);
        m_Grab.selectEntered.RemoveListener(OnSelectEntered);

        m_HoverCount = 0;
        if (m_IsSelected && SelectionManager.Exists)
            SelectionManager.Instance.Release(this);
    }

    /// <summary>Lo llama el SelectionManager al seleccionar o deseleccionar.</summary>
    public void SetSelected(bool selected)
    {
        m_IsSelected = selected;
        RefreshVisuals();
    }

    /// <summary>Devuelve el mueble a su posición, rotación, escala y color iniciales.</summary>
    public void ResetToInitial()
    {
        transform.SetPositionAndRotation(m_InitialPose.position, m_InitialPose.rotation);
        transform.localScale = m_InitialScale;
        ClearTint();
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        m_HoverCount++;
        RefreshVisuals();
        HoverFeedback.SendHaptic(args.interactorObject, m_HoverHaptic, m_HapticDuration);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        m_HoverCount = Mathf.Max(0, m_HoverCount - 1);
        RefreshVisuals();
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        SelectionManager.Instance.Select(this);
        // Los objetos que estaban quietos encima vuelven a tener física (si no, flotarían al mover el mueble).
        GrabbableProp.ReleaseResting(WorldBounds(gameObject));
        HoverFeedback.SendHaptic(args.interactorObject, m_SelectHaptic, m_HapticDuration);
    }

    void RefreshVisuals()
    {
        if (m_Renderers == null)
            return;

        var glow = m_HoverCount > 0 ? m_HoverGlowColor : m_IsSelected ? m_SelectedGlowColor : Color.black;
        if (m_HasTint)
            glow *= m_GlowWhenTinted;

        m_Block.Clear();
        m_Block.SetColor(k_EmissionColor, glow);
        if (m_HasTint)
            m_Block.SetColor(k_BaseColor, m_Tint);

        foreach (var r in m_Renderers)
        {
            if (r != null)
                r.SetPropertyBlock(m_Block);
        }
    }

    void ConfigureComponents()
    {
        var grab = GetComponent<XRGrabInteractable>();
        grab.selectMode = InteractableSelectMode.Multiple;      // dos manos para escalar
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.useDynamicAttach = true;                            // se coge por el punto señalado
        grab.farAttachMode = InteractableFarAttachMode.Far;      // se queda a distancia, no viene a la mano
        grab.throwOnDetach = false;
        grab.trackScale = true;
        grab.retainTransformParent = true;

        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;                                 // los muebles no caen ni se empujan
        body.useGravity = false;

        EnsureCollider(gameObject);
    }

    /// <summary>
    /// Si el objeto no tiene ningún collider, añade un BoxCollider que envuelve todas sus mallas
    /// (para FBX sin collider). Debe llamarse antes de añadir el XRGrabInteractable en tiempo de ejecución.
    /// </summary>
    public static void EnsureCollider(GameObject target)
    {
        if (target.GetComponentInChildren<Collider>() != null)
            return;

        var filters = target.GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0)
            return;

        var toLocal = target.transform.worldToLocalMatrix;
        var bounds = new Bounds();
        var initialized = false;

        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;

            var meshBounds = filter.sharedMesh.bounds;
            var toRoot = toLocal * filter.transform.localToWorldMatrix;
            for (var i = 0; i < 8; i++)
            {
                var corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var point = toRoot.MultiplyPoint3x4(corner);
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        var box = target.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
    }

    /// <summary>Bounds en el mundo de todas las mallas del objeto.</summary>
    public static Bounds WorldBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(target.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
