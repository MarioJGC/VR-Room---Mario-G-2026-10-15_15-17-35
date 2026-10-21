using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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

    [Header("Resaltado")]
    [SerializeField, ColorUsage(false, true)] Color m_HoverGlow = new Color(0.10f, 0.25f, 0.55f);
    [SerializeField, ColorUsage(false, true)] Color m_SelectedGlow = new Color(0.05f, 0.45f, 0.15f);

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

    public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;
    public bool IsSelected => m_IsSelected;
    public XRGrabInteractable GrabInteractable => m_Grab;

    /// <summary>Color que multiplica la textura del mueble (lo usará el menú de colores).</summary>
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
    }

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
        m_Tint = Color.white;
        m_HasTint = false;
        RefreshVisuals();
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        m_HoverCount++;
        RefreshVisuals();
        SendHaptic(args.interactorObject, m_HoverHaptic);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        m_HoverCount = Mathf.Max(0, m_HoverCount - 1);
        RefreshVisuals();
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        SelectionManager.Instance.Select(this);
        SendHaptic(args.interactorObject, m_SelectHaptic);
    }

    void SendHaptic(IXRInteractor interactor, float amplitude)
    {
        if (amplitude > 0f && interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(amplitude, m_HapticDuration);
    }

    void RefreshVisuals()
    {
        if (m_Renderers == null)
            return;

        var glow = m_IsSelected ? m_SelectedGlow : m_HoverCount > 0 ? m_HoverGlow : Color.black;

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

        if (GetComponentInChildren<Collider>() == null)
            AddFittedBoxCollider();
    }

    /// <summary>Añade un BoxCollider que envuelve todas las mallas del objeto (para FBX sin collider).</summary>
    void AddFittedBoxCollider()
    {
        var filters = GetComponentsInChildren<MeshFilter>();
        if (filters.Length == 0)
            return;

        var toLocal = transform.worldToLocalMatrix;
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

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
    }
}
