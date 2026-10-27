using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Caminar de verdad (room-scale) sin joystick: el seguimiento de las gafas ya mueve la cámara al andar,
/// pero el suelo real es plano. Este componente busca el suelo virtual que hay bajo la cabeza
/// (planta baja, escalera, altillo o azotea) y ajusta suavemente la altura del XR Origin,
/// de modo que al caminar físicamente hacia la escalera se sube por ella y al bajar se desciende.
/// Los desniveles grandes (salirse del altillo) los resuelve la gravedad; mientras se escala, se pausa.
/// </summary>
[RequireComponent(typeof(XROrigin))]
public class RoomScaleFloorFollower : MonoBehaviour
{
    [Tooltip("Desnivel máximo que se sube siguiendo el suelo (rampa de la escalera, pequeños escalones).")]
    [SerializeField] float m_MaxStepUp = 0.4f;

    [Tooltip("Desnivel máximo que se baja siguiendo el suelo; más que esto lo resuelve la gravedad.")]
    [SerializeField] float m_MaxStepDown = 0.6f;

    [Tooltip("Rapidez con la que la altura se adapta al suelo.")]
    [SerializeField] float m_Smoothing = 12f;

    XROrigin m_Origin;
    ClimbProvider m_Climb;
    readonly RaycastHit[] m_Hits = new RaycastHit[16];

    void Awake()
    {
        m_Origin = GetComponent<XROrigin>();
        m_Climb = FindAnyObjectByType<ClimbProvider>();
    }

    void LateUpdate()
    {
        if (m_Origin.Camera == null || (m_Climb != null && m_Climb.isLocomotionActive))
            return;

        var head = m_Origin.Camera.transform.position;
        if (!TryGetFloorUnder(head, out var floorY))
            return;

        var delta = floorY - transform.position.y;
        if (delta > m_MaxStepUp || delta < -m_MaxStepDown || Mathf.Abs(delta) < 0.002f)
            return;

        transform.position += Vector3.up * (delta * (1f - Mathf.Exp(-m_Smoothing * Time.deltaTime)));
        Physics.SyncTransforms();
    }

    /// <summary>Primer suelo transitable (zona de teletransporte) debajo de la cabeza.</summary>
    bool TryGetFloorUnder(Vector3 head, out float floorY)
    {
        var count = Physics.RaycastNonAlloc(head, Vector3.down, m_Hits, 4f, ~0, QueryTriggerInteraction.Ignore);
        var best = float.MaxValue;
        floorY = 0f;
        for (var i = 0; i < count; i++)
        {
            var hit = m_Hits[i];
            if (hit.distance >= best || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.collider.GetComponentInParent<TeleportationArea>() == null)
                continue;
            best = hit.distance;
            floorY = hit.point.y;
        }
        return best < float.MaxValue;
    }
}
