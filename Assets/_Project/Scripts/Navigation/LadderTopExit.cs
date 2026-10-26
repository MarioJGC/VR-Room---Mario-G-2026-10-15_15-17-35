using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Escalera de mano hacia la azotea (escalada con XRI: ClimbInteractable + ClimbProvider).
///  - Al soltar la escalera con la cabeza asomando por la trampilla, el usuario pasa a la azotea
///    (sin esto, la gravedad lo devolvería al altillo).
///  - Los peldaños no chocan con el cuerpo del jugador, para que pegarse a la escalera no bloquee la subida.
/// Basado en la escalera "Ladder" del proyecto Unity-VR-Basics-2022 (CC0), adaptado al sistema de escalada de XRI 3.
/// </summary>
public class LadderTopExit : MonoBehaviour
{
    [Tooltip("Punto de la azotea donde aparece el usuario al salir.")]
    [SerializeField] Transform m_ExitPoint;

    [Tooltip("Altura mínima de la cabeza sobre el suelo de la azotea para poder salir al soltar.")]
    [SerializeField] float m_HeadAboveExit = 0.9f;

    [SerializeField] ClimbProvider m_Climb;
    [SerializeField] TeleportationProvider m_Teleport;
    [SerializeField] XROrigin m_Origin;
    [SerializeField] AudioClip m_ExitClip;

    void Awake()
    {
        if (m_Climb == null) m_Climb = FindAnyObjectByType<ClimbProvider>();
        if (m_Teleport == null) m_Teleport = FindAnyObjectByType<TeleportationProvider>();
        if (m_Origin == null) m_Origin = FindAnyObjectByType<XROrigin>();
    }

    void Start()
    {
        if (m_Origin != null && m_Origin.TryGetComponent<CharacterController>(out var body))
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(body, c);
        }
    }

    void OnEnable()
    {
        if (m_Climb != null)
            m_Climb.locomotionEnded += OnClimbEnded;
    }

    void OnDisable()
    {
        if (m_Climb != null)
            m_Climb.locomotionEnded -= OnClimbEnded;
    }

    void OnClimbEnded(LocomotionProvider provider)
    {
        if (m_ExitPoint == null || m_Teleport == null || m_Origin == null)
            return;

        var head = m_Origin.Camera.transform.position;
        if (head.y < m_ExitPoint.position.y + m_HeadAboveExit)
            return;

        m_Teleport.QueueTeleportRequest(new TeleportRequest
        {
            destinationPosition = m_ExitPoint.position,
            destinationRotation = m_ExitPoint.rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward,
            requestTime = Time.time,
        });
        if (m_ExitClip != null)
            AudioSource.PlayClipAtPoint(m_ExitClip, m_ExitPoint.position, 0.5f);
    }

    public void Configure(Transform exitPoint, AudioClip exitClip)
    {
        m_ExitPoint = exitPoint;
        m_ExitClip = exitClip;
    }
}
