using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Mientras se sostiene el objeto, pulsar el gatillo enciende o apaga un ToggleDevice.
/// Ejemplos: el mando a distancia enciende la TV, el farol se enciende en la mano.
/// Si no se asigna un objetivo, usa el ToggleDevice del propio objeto.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public class ActivateToggle : MonoBehaviour
{
    [SerializeField] ToggleDevice m_Target;
    [SerializeField, Range(0f, 1f)] float m_Haptic = 0.3f;

    XRGrabInteractable m_Grab;

    void Awake()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
        if (m_Target == null)
            m_Target = GetComponent<ToggleDevice>();
    }

    void OnEnable() => m_Grab.activated.AddListener(OnActivated);

    void OnDisable() => m_Grab.activated.RemoveListener(OnActivated);

    void OnActivated(ActivateEventArgs args)
    {
        if (m_Target != null)
            m_Target.Toggle();
        HoverFeedback.SendHaptic(args.interactorObject, m_Haptic, 0.06f);
    }
}
