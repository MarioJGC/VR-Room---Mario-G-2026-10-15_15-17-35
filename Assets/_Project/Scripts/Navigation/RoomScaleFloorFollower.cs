using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Caminar de verdad (room-scale) sin joystick: el seguimiento de las gafas ya mueve la cámara al andar,
/// pero el suelo real es plano. Este componente busca el suelo virtual que hay bajo el usuario
/// (planta baja, escalera, altillo o azotea) y ajusta la altura del XR Origin:
///  - desniveles pequeños (la rampa de la escalera): sube o baja suavemente;
///  - si el suelo desaparece bajo los pies (salir por el lado de la escalera, soltar la escalera de mano
///    a media altura, asomarse de más por un borde): cae con gravedad hasta el suelo de abajo.
/// Mientras se escala la escalera de mano, se pausa.
/// </summary>
[RequireComponent(typeof(XROrigin))]
public class RoomScaleFloorFollower : MonoBehaviour
{
    [Tooltip("Desnivel máximo que se sube siguiendo el suelo (rampa de la escalera, pequeños escalones).")]
    [SerializeField] float m_MaxStepUp = 0.4f;

    [Tooltip("Desnivel máximo que se baja suavemente; si el suelo está más abajo, se cae con gravedad.")]
    [SerializeField] float m_MaxStepDown = 0.6f;

    [Tooltip("Rapidez con la que la altura se adapta al suelo en desniveles pequeños.")]
    [SerializeField] float m_Smoothing = 12f;

    [Tooltip("Radio alrededor de la cabeza que cuenta como \"pies\": se cae solo si no hay suelo en todo ese radio.")]
    [SerializeField] float m_FootprintRadius = 0.2f;

    [SerializeField] float m_Gravity = 9.81f;

    XROrigin m_Origin;
    ClimbProvider m_Climb;
    float m_FallSpeed;
    readonly RaycastHit[] m_Hits = new RaycastHit[16];

    static readonly Vector2[] k_FootprintOffsets =
    {
        Vector2.zero, new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
    };

    void Awake()
    {
        m_Origin = GetComponent<XROrigin>();
        m_Climb = FindAnyObjectByType<ClimbProvider>();
    }

    void LateUpdate()
    {
        if (m_Origin.Camera == null || (m_Climb != null && m_Climb.isLocomotionActive))
        {
            m_FallSpeed = 0f;
            return;
        }

        // Solo cuentan los suelos a los que se puede llegar de un paso (no se "salta" a un piso superior).
        var feetY = transform.position.y;
        if (!TryGetSupportingFloor(m_Origin.Camera.transform.position, feetY + m_MaxStepUp, out var floorY))
        {
            m_FallSpeed = 0f;
            return;
        }

        var delta = floorY - feetY;
        if (delta >= -m_MaxStepDown)
        {
            // Desnivel pequeño: se sigue el suelo suavemente.
            m_FallSpeed = 0f;
            if (Mathf.Abs(delta) > 0.002f)
                Move(delta * (1f - Mathf.Exp(-m_Smoothing * Time.deltaTime)));
            return;
        }

        // El suelo ha desaparecido bajo los pies: caída con gravedad hasta el suelo de abajo.
        m_FallSpeed += m_Gravity * Time.deltaTime;
        Move(Mathf.Max(-m_FallSpeed * Time.deltaTime, delta));
    }

    void Move(float dy)
    {
        transform.position += Vector3.up * dy;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Suelo transitable (zona de teletransporte) más alto, sin pasar de <paramref name="maxY"/>, bajo la huella
    /// del usuario: el centro (la cabeza) y cuatro puntos alrededor. Así, asomarse un poco a un borde
    /// no hace caer; salirse del todo, sí.
    /// </summary>
    bool TryGetSupportingFloor(Vector3 head, float maxY, out float floorY)
    {
        floorY = float.MinValue;
        var found = false;
        foreach (var offset in k_FootprintOffsets)
        {
            var origin = head + new Vector3(offset.x, 0f, offset.y) * m_FootprintRadius;
            var count = Physics.RaycastNonAlloc(origin, Vector3.down, m_Hits, 6f, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = m_Hits[i];
                if (hit.point.y > maxY || hit.point.y <= floorY || hit.collider.transform.IsChildOf(transform))
                    continue;
                if (hit.collider.GetComponentInParent<TeleportationArea>() == null)
                    continue;
                floorY = hit.point.y;
                found = true;
            }
        }
        return found;
    }
}
