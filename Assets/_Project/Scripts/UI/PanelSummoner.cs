using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Abre y cierra un panel flotante delante del usuario con el botón X del mando izquierdo
/// (en el simulador: tecla 1 con el mando izquierdo activo).
/// Cada vez que se abre, se coloca delante de la cabeza, a la altura del pecho.
/// </summary>
public class PanelSummoner : MonoBehaviour
{
    [SerializeField] GameObject m_Panel;
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

        m_ToggleAction = new InputAction("Toggle Panel", InputActionType.Button);
        m_ToggleAction.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
        m_ToggleAction.AddBinding("<XRController>{LeftHand}/primaryButton");
    }

    void OnEnable()
    {
        m_ToggleAction.performed += OnTogglePerformed;
        m_ToggleAction.Enable();
    }

    void OnDisable()
    {
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

    public void Configure(GameObject panel, Transform head, AudioClip openClip, AudioClip closeClip)
    {
        m_Panel = panel;
        m_Head = head;
        m_OpenClip = openClip;
        m_CloseClip = closeClip;
    }
}
