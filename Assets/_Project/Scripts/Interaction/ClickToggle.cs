using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Apuntar con el rayo y pulsar (grip) enciende o apaga un ToggleDevice.
/// Se usa en lámparas, el tocadiscos y el interruptor de la pared.
/// Si no se asigna un objetivo, usa el ToggleDevice del propio objeto.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
[RequireComponent(typeof(HoverFeedback))]
public class ClickToggle : MonoBehaviour
{
    [SerializeField] ToggleDevice m_Target;
    [SerializeField, Range(0f, 1f)] float m_Haptic = 0.4f;

    XRSimpleInteractable m_Interactable;

    public ToggleDevice Target
    {
        get => m_Target;
        set => m_Target = value;
    }

    void Awake()
    {
        m_Interactable = GetComponent<XRSimpleInteractable>();
        if (m_Target == null)
            m_Target = GetComponent<ToggleDevice>();
    }

    void OnEnable() => m_Interactable.selectEntered.AddListener(OnSelectEntered);

    void OnDisable() => m_Interactable.selectEntered.RemoveListener(OnSelectEntered);

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (m_Target != null)
            m_Target.Toggle();
        HoverFeedback.SendHaptic(args.interactorObject, m_Haptic, 0.08f);
    }
}
