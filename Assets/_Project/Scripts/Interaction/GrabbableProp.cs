using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Objeto pequeño con física (libros, fruta, pelotas, mando de la TV...):
/// se atrae a la mano con el rayo, se puede lanzar y suena al chocar.
/// A diferencia de los muebles, cae por gravedad y rota libremente.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(HoverFeedback))]
public class GrabbableProp : MonoBehaviour
{
    [Tooltip("Sonido al chocar contra algo (opcional).")]
    [SerializeField] AudioClip m_ImpactClip;

    [Tooltip("Velocidad mínima del choque para que suene.")]
    [SerializeField] float m_MinImpactSpeed = 0.8f;

    [SerializeField, Range(0f, 1f)] float m_GrabHaptic = 0.35f;

    XRGrabInteractable m_Grab;
    AudioSource m_Audio;
    float m_NextImpactTime;

    public AudioClip ImpactClip
    {
        get => m_ImpactClip;
        set => m_ImpactClip = value;
    }

    void Reset() => ConfigureComponents();

    void Awake()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
        ConfigureComponents();

        if (m_ImpactClip != null && !TryGetComponent(out m_Audio))
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 1f;
            m_Audio.maxDistance = 15f;
        }
    }

    void OnEnable() => m_Grab.selectEntered.AddListener(OnSelectEntered);

    void OnDisable() => m_Grab.selectEntered.RemoveListener(OnSelectEntered);

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        HoverFeedback.SendHaptic(args.interactorObject, m_GrabHaptic, 0.08f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (m_Audio == null || m_ImpactClip == null || Time.time < m_NextImpactTime)
            return;

        var speed = collision.relativeVelocity.magnitude;
        if (speed < m_MinImpactSpeed)
            return;

        m_NextImpactTime = Time.time + 0.1f;
        m_Audio.PlayOneShot(m_ImpactClip, Mathf.Clamp01(speed / 4f));
    }

    void ConfigureComponents()
    {
        var grab = GetComponent<XRGrabInteractable>();
        grab.selectMode = InteractableSelectMode.Single;
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.useDynamicAttach = true;
        grab.farAttachMode = InteractableFarAttachMode.Near;    // viene a la mano
        grab.throwOnDetach = true;

        var body = GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
}
