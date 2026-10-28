using System.Collections.Generic;
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

    [Tooltip("Quieto en su sitio hasta que alguien lo coge o mueve el mueble sobre el que está: la escena empieza ordenada.")]
    [SerializeField] bool m_StillUntilTouched = true;

    static readonly List<GrabbableProp> s_All = new List<GrabbableProp>();

    XRGrabInteractable m_Grab;
    Rigidbody m_Body;
    AudioSource m_Audio;
    float m_NextImpactTime;
    Pose m_InitialPose;

    /// <summary>Todos los objetos pequeños de la escena (para "Resetear sala").</summary>
    public static IReadOnlyList<GrabbableProp> All => s_All;

    void Reset() => ConfigureComponents();

    void Awake()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
        m_Body = GetComponent<Rigidbody>();
        ConfigureComponents();
        m_InitialPose = new Pose(transform.position, transform.rotation);
        s_All.Add(this);
        if (m_StillUntilTouched)
            Hold();

        if (m_ImpactClip != null && !TryGetComponent(out m_Audio))
        {
            m_Audio = gameObject.AddComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 1f;
            m_Audio.maxDistance = 15f;
        }
    }

    void OnDestroy() => s_All.Remove(this);

    void OnEnable() => m_Grab.selectEntered.AddListener(OnSelectEntered);

    void OnDisable() => m_Grab.selectEntered.RemoveListener(OnSelectEntered);

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        Release();
        HoverFeedback.SendHaptic(args.interactorObject, m_GrabHaptic, 0.08f);
    }

    /// <summary>
    /// Lo deja quieto en su sitio (la física no lo empuja aunque esté un poco solapado con el mueble).
    /// Se usan restricciones y no isKinematic porque XRGrabInteractable restaura isKinematic al soltar.
    /// </summary>
    void Hold()
    {
        m_Body.constraints = RigidbodyConstraints.FreezeAll;
    }

    /// <summary>Vuelve a tener física normal (al cogerlo o al mover el mueble que tiene debajo).</summary>
    public void Release()
    {
        if (m_Body.constraints == RigidbodyConstraints.None)
            return;
        m_Body.constraints = RigidbodyConstraints.None;
        m_Body.WakeUp();
    }

    /// <summary>Lo devuelve a su posición inicial, quieto.</summary>
    public void ResetToInitial()
    {
        if (m_Grab.isSelected)
            return;
        m_Body.linearVelocity = Vector3.zero;
        m_Body.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(m_InitialPose.position, m_InitialPose.rotation);
        m_Body.position = m_InitialPose.position;
        m_Body.rotation = m_InitialPose.rotation;
        if (m_StillUntilTouched)
            Hold();
    }

    /// <summary>Suelta los objetos que estén apoyados encima de un mueble que se va a mover.</summary>
    public static void ReleaseResting(Bounds support)
    {
        foreach (var prop in s_All)
        {
            if (prop == null)
                continue;
            var p = prop.transform.position;
            var onTop = p.y >= support.max.y - 0.2f && p.y <= support.max.y + 0.5f;
            var inside = p.x >= support.min.x && p.x <= support.max.x && p.z >= support.min.z && p.z <= support.max.z;
            if (onTop && inside)
                prop.Release();
        }
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
