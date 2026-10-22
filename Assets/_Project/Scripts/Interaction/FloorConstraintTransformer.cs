using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Restringe la manipulación de un mueble para que se comporte como uno real:
/// se desliza por el suelo (no cambia de altura), solo gira sobre el eje vertical
/// y su escala queda limitada entre un mínimo y un máximo.
/// Se ejecuta después del XRGeneralGrabTransformer, que calcula el movimiento,
/// la rotación y el escalado con dos manos.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class FloorConstraintTransformer : XRBaseGrabTransformer
{
    [Tooltip("Mantiene el mueble a la misma altura mientras se arrastra.")]
    [SerializeField] bool m_LockHeight = true;

    [Tooltip("Solo permite girar el mueble sobre el eje vertical (Y).")]
    [SerializeField] bool m_YawOnly = true;

    [Tooltip("Permite escalar el mueble cogiéndolo con los dos mandos a la vez.")]
    [SerializeField] bool m_AllowTwoHandedScaling = true;

    [Tooltip("Escala mínima respecto al tamaño original.")]
    [SerializeField, Range(0.1f, 1f)] float m_MinScale = 0.5f;

    [Tooltip("Escala máxima respecto al tamaño original.")]
    [SerializeField, Range(1f, 5f)] float m_MaxScale = 2f;

    [Tooltip("Impide sacar el mueble de la zona de su planta (PlacementArea del apartamento).")]
    [SerializeField] bool m_KeepInsideArea = true;

    protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

    Vector3 m_OriginalScale;
    float m_GrabHeight;
    Quaternion m_GrabRotation;
    Renderer[] m_Renderers;

    void Awake()
    {
        m_OriginalScale = transform.localScale;
        m_Renderers = GetComponentsInChildren<Renderer>();
    }

    protected override void Start()
    {
        // No se llama a base.Start(): registramos los transformers a mano para
        // garantizar el orden (primero el general y después esta restricción).
        var grab = GetComponent<XRGrabInteractable>();

        if (!TryGetComponent<XRGeneralGrabTransformer>(out var general))
            general = gameObject.AddComponent<XRGeneralGrabTransformer>();

        general.permittedDisplacementAxes = m_LockHeight
            ? XRGeneralGrabTransformer.ManipulationAxes.X | XRGeneralGrabTransformer.ManipulationAxes.Z
            : XRGeneralGrabTransformer.ManipulationAxes.All;
        general.allowTwoHandedScaling = m_AllowTwoHandedScaling;

        grab.MoveSingleGrabTransformerTo(general, 0);
        grab.MoveSingleGrabTransformerTo(this, 1);
        grab.MoveMultipleGrabTransformerTo(general, 0);
        grab.MoveMultipleGrabTransformerTo(this, 1);
    }

    public override void OnGrab(XRGrabInteractable grabInteractable)
    {
        // Guardamos la altura y la rotación solo al empezar a coger el mueble,
        // no cuando se une una segunda mano.
        if (grabInteractable.interactorsSelecting.Count <= 1)
        {
            m_GrabHeight = transform.position.y;
            m_GrabRotation = transform.rotation;
        }
    }

    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
    {
        if (m_LockHeight)
            targetPose.position.y = m_GrabHeight;

        if (m_YawOnly)
            targetPose.rotation = KeepOnlyYaw(targetPose.rotation, m_GrabRotation);

        localScale = ClampScale(localScale);

        if (m_KeepInsideArea && PlacementArea.Instance != null)
            KeepInsideArea(ref targetPose);
    }

    /// <summary>Corrige la posición para que la huella del mueble no salga de la zona de su planta.</summary>
    void KeepInsideArea(ref Pose targetPose)
    {
        if (!TryGetBounds(out var bounds))
            return;

        // Desplazamiento del pivote al centro visual, girado como quedará el mueble.
        var offset = targetPose.rotation * Quaternion.Inverse(transform.rotation) * (bounds.center - transform.position);
        var center = targetPose.position + offset;
        var bottom = targetPose.position.y + (bounds.min.y - transform.position.y);
        var clamped = PlacementArea.Instance.ClampCenter(center, bounds.extents, bottom);
        targetPose.position += new Vector3(clamped.x - center.x, 0f, clamped.z - center.z);
    }

    bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        var found = false;
        foreach (var r in m_Renderers)
        {
            if (r == null || !r.enabled)
                continue;
            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return found;
    }

    /// <summary>
    /// Devuelve la rotación inicial girada solo la parte vertical (twist en Y) del giro
    /// que se ha hecho desde que se cogió. Funciona aunque el modelo venga rotado
    /// (por ejemplo, FBX importados con -90 en X).
    /// </summary>
    static Quaternion KeepOnlyYaw(Quaternion target, Quaternion start)
    {
        var delta = target * Quaternion.Inverse(start);
        var twist = new Quaternion(0f, delta.y, 0f, delta.w);
        var magnitude = Mathf.Sqrt(twist.y * twist.y + twist.w * twist.w);
        if (magnitude < 1e-5f)
            return start;

        twist.y /= magnitude;
        twist.w /= magnitude;
        return twist * start;
    }

    Vector3 ClampScale(Vector3 scale)
    {
        if (m_OriginalScale.x == 0f)
            return scale;

        var ratio = Mathf.Clamp(scale.x / m_OriginalScale.x, m_MinScale, m_MaxScale);
        return m_OriginalScale * ratio;
    }
}
