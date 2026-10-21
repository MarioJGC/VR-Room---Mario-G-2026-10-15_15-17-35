using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Feedback al apuntar a un objeto interactuable (objetos pequeños, lámparas, interruptores):
/// brillo azul mientras el rayo está encima y una vibración suave al entrar.
/// Funciona con cualquier interactuable de XRI que haya en el mismo objeto.
/// </summary>
[DisallowMultipleComponent]
public class HoverFeedback : MonoBehaviour
{
    static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

    [SerializeField, ColorUsage(false, true)] Color m_HoverGlow = new Color(0.10f, 0.25f, 0.55f);
    [SerializeField, Range(0f, 1f)] float m_HoverHaptic = 0.15f;
    [SerializeField] float m_HapticDuration = 0.06f;

    XRBaseInteractable m_Interactable;
    Renderer[] m_Renderers;
    MaterialPropertyBlock m_Block;
    int m_HoverCount;

    void Awake()
    {
        m_Interactable = GetComponent<XRBaseInteractable>();
        m_Renderers = GetComponentsInChildren<Renderer>();
        m_Block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (m_Interactable == null)
            return;
        m_Interactable.hoverEntered.AddListener(OnHoverEntered);
        m_Interactable.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        if (m_Interactable == null)
            return;
        m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
        m_Interactable.hoverExited.RemoveListener(OnHoverExited);
        m_HoverCount = 0;
        SetGlow(Color.black);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        m_HoverCount++;
        SetGlow(m_HoverGlow);
        SendHaptic(args.interactorObject, m_HoverHaptic, m_HapticDuration);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        m_HoverCount = Mathf.Max(0, m_HoverCount - 1);
        if (m_HoverCount == 0)
            SetGlow(Color.black);
    }

    void SetGlow(Color glow)
    {
        foreach (var r in m_Renderers)
        {
            if (r == null)
                continue;
            // Se lee el bloque actual para no pisar otras propiedades del renderer.
            r.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_EmissionColor, glow);
            r.SetPropertyBlock(m_Block);
        }
    }

    /// <summary>Envía una vibración al mando que está interactuando (si es un mando).</summary>
    public static void SendHaptic(IXRInteractor interactor, float amplitude, float duration)
    {
        if (amplitude > 0f && interactor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(amplitude, duration);
    }
}
