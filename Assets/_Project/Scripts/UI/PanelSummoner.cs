using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Abre y cierra un panel flotante delante del usuario con un botón del mando izquierdo:
/// X = botón primario (simulador: tecla 1), Y = botón secundario (simulador: tecla 2).
/// Cada vez que se abre, se coloca delante de la cabeza, a la altura del pecho,
/// y se cierran los demás paneles para que no se superpongan.
/// </summary>
public class PanelSummoner : MonoBehaviour
{
    public enum LeftButton { X, Y }

    static readonly System.Collections.Generic.List<PanelSummoner> s_All = new System.Collections.Generic.List<PanelSummoner>();

    [SerializeField] GameObject m_Panel;
    [SerializeField] LeftButton m_Button = LeftButton.X;
    [SerializeField] Transform m_Head;
    [SerializeField] float m_Distance = 0.7f;
    [SerializeField] float m_HeightOffset = -0.15f;
    [SerializeField] bool m_StartOpen;
    [SerializeField] AudioClip m_OpenClip;
    [SerializeField] AudioClip m_CloseClip;

    InputAction m_ToggleAction;

    public bool IsOpen => m_Panel != null && m_Panel.activeSelf;

    void Awake()
    {
        if (m_Head == null && Camera.main != null)
            m_Head = Camera.main.transform;

        var usage = m_Button == LeftButton.X ? "PrimaryButton" : "SecondaryButton";
        var control = m_Button == LeftButton.X ? "primaryButton" : "secondaryButton";
        m_ToggleAction = new InputAction("Toggle Panel", InputActionType.Button);
        m_ToggleAction.AddBinding($"<XRController>{{LeftHand}}/{{{usage}}}");
        m_ToggleAction.AddBinding($"<XRController>{{LeftHand}}/{control}");
    }

    void OnEnable()
    {
        s_All.Add(this);
        m_ToggleAction.performed += OnTogglePerformed;
        m_ToggleAction.Enable();
    }

    void OnDisable()
    {
        s_All.Remove(this);
        m_ToggleAction.performed -= OnTogglePerformed;
        m_ToggleAction.Disable();
    }

    void OnDestroy() => m_ToggleAction?.Dispose();

    void Start()
    {
        if (m_StartOpen) Open();
        else m_Panel.SetActive(false);
    }

    void OnTogglePerformed(InputAction.CallbackContext context) => Toggle();

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        foreach (var other in s_All)
        {
            if (other != this && other.IsOpen)
                other.m_Panel.SetActive(false);
        }

        if (m_Head != null)
        {
            var forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(m_Head.up, Vector3.up);
            forward.Normalize();
            m_Panel.transform.SetPositionAndRotation(
                m_Head.position + forward * m_Distance + Vector3.up * m_HeightOffset,
                Quaternion.LookRotation(forward, Vector3.up));
        }
        m_Panel.SetActive(true);
        PlayClip(m_OpenClip);
    }

    public void Close()
    {
        m_Panel.SetActive(false);
        PlayClip(m_CloseClip);
    }

    void PlayClip(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, m_Panel.transform.position, 0.5f);
    }
}
