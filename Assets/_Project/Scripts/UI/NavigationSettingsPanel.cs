using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel 3D de ajustes de navegación. Conecta los controles de la interfaz con LocomotionSettings
/// y con la maqueta, y se mantiene sincronizado si los valores cambian desde otro sitio.
/// Los controles que no aplican se desactivan (p. ej. la velocidad de giro con giro por saltos).
/// </summary>
public class NavigationSettingsPanel : MonoBehaviour
{
    [Header("Lógica")]
    [SerializeField] LocomotionSettings m_Settings;
    [SerializeField] WorldInMiniature m_Miniature;
    [SerializeField] PanelSummoner m_Summoner;

    [Header("Controles")]
    [SerializeField] Toggle m_MoveToggle;
    [SerializeField] Slider m_MoveSpeedSlider;
    [SerializeField] TMP_Text m_MoveSpeedValue;
    [SerializeField] Button m_SnapButton;
    [SerializeField] Button m_SmoothButton;
    [SerializeField] Slider m_SnapAngleSlider;
    [SerializeField] TMP_Text m_SnapAngleValue;
    [SerializeField] Slider m_TurnSpeedSlider;
    [SerializeField] TMP_Text m_TurnSpeedValue;
    [SerializeField] Toggle m_MiniatureToggle;
    [SerializeField] Button m_CloseButton;

    [Header("Estilo y feedback")]
    [SerializeField] Color m_SelectedColor = new Color(0.20f, 0.55f, 0.95f);
    [SerializeField] Color m_NormalColor = new Color(0.25f, 0.27f, 0.32f);
    [SerializeField] AudioClip m_ClickClip;

    // El ángulo de giro se elige en pasos de 15°.
    const float k_SnapStep = 15f;

    void Awake()
    {
        if (m_Settings == null) m_Settings = FindAnyObjectByType<LocomotionSettings>();
        if (m_Miniature == null) m_Miniature = FindAnyObjectByType<WorldInMiniature>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        m_MoveToggle.onValueChanged.AddListener(OnMoveToggled);
        m_MoveSpeedSlider.onValueChanged.AddListener(OnMoveSpeedChanged);
        m_SnapButton.onClick.AddListener(OnSnapClicked);
        m_SmoothButton.onClick.AddListener(OnSmoothClicked);
        m_SnapAngleSlider.onValueChanged.AddListener(OnSnapAngleChanged);
        m_TurnSpeedSlider.onValueChanged.AddListener(OnTurnSpeedChanged);
        m_MiniatureToggle.onValueChanged.AddListener(OnMiniatureToggled);
        m_CloseButton.onClick.AddListener(OnCloseClicked);

        if (m_Settings != null) m_Settings.Changed += Refresh;
        if (m_Miniature != null) m_Miniature.VisibilityChanged += OnMiniatureVisibilityChanged;
        Refresh();
    }

    void OnDisable()
    {
        m_MoveToggle.onValueChanged.RemoveListener(OnMoveToggled);
        m_MoveSpeedSlider.onValueChanged.RemoveListener(OnMoveSpeedChanged);
        m_SnapButton.onClick.RemoveListener(OnSnapClicked);
        m_SmoothButton.onClick.RemoveListener(OnSmoothClicked);
        m_SnapAngleSlider.onValueChanged.RemoveListener(OnSnapAngleChanged);
        m_TurnSpeedSlider.onValueChanged.RemoveListener(OnTurnSpeedChanged);
        m_MiniatureToggle.onValueChanged.RemoveListener(OnMiniatureToggled);
        m_CloseButton.onClick.RemoveListener(OnCloseClicked);

        if (m_Settings != null) m_Settings.Changed -= Refresh;
        if (m_Miniature != null) m_Miniature.VisibilityChanged -= OnMiniatureVisibilityChanged;
    }

    void OnMoveToggled(bool on) { m_Settings.ContinuousMove = on; Click(); }
    void OnMoveSpeedChanged(float value) => m_Settings.MoveSpeed = value;
    void OnSnapClicked() { m_Settings.Turn = LocomotionSettings.TurnStyle.Snap; Click(); }
    void OnSmoothClicked() { m_Settings.Turn = LocomotionSettings.TurnStyle.Smooth; Click(); }
    void OnSnapAngleChanged(float steps) => m_Settings.SnapAngle = steps * k_SnapStep;
    void OnTurnSpeedChanged(float value) => m_Settings.SmoothTurnSpeed = value;
    void OnMiniatureVisibilityChanged(bool visible) => m_MiniatureToggle.SetIsOnWithoutNotify(visible);

    void OnMiniatureToggled(bool on)
    {
        if (m_Miniature != null) m_Miniature.Visible = on;
        Click();
    }

    void OnCloseClicked()
    {
        Click();
        if (m_Summoner != null) m_Summoner.Close();
        else gameObject.SetActive(false);
    }

    /// <summary>Copia los valores actuales a los controles sin lanzar sus eventos.</summary>
    void Refresh()
    {
        if (m_Settings == null)
            return;

        var snap = m_Settings.Turn == LocomotionSettings.TurnStyle.Snap;

        m_MoveToggle.SetIsOnWithoutNotify(m_Settings.ContinuousMove);
        m_MoveSpeedSlider.SetValueWithoutNotify(m_Settings.MoveSpeed);
        m_MoveSpeedSlider.interactable = m_Settings.ContinuousMove;
        m_MoveSpeedValue.text = $"{m_Settings.MoveSpeed:0.0} m/s";

        SetSelected(m_SnapButton, snap);
        SetSelected(m_SmoothButton, !snap);

        m_SnapAngleSlider.SetValueWithoutNotify(Mathf.Round(m_Settings.SnapAngle / k_SnapStep));
        m_SnapAngleSlider.interactable = snap;
        m_SnapAngleValue.text = $"{m_Settings.SnapAngle:0}°";

        m_TurnSpeedSlider.SetValueWithoutNotify(m_Settings.SmoothTurnSpeed);
        m_TurnSpeedSlider.interactable = !snap;
        m_TurnSpeedValue.text = $"{m_Settings.SmoothTurnSpeed:0}°/s";

        if (m_Miniature != null)
            m_MiniatureToggle.SetIsOnWithoutNotify(m_Miniature.Visible);
        m_MiniatureToggle.interactable = m_Miniature != null;
    }

    void SetSelected(Button button, bool selected)
    {
        if (button.targetGraphic != null)
            button.targetGraphic.color = selected ? m_SelectedColor : m_NormalColor;
    }

    void Click()
    {
        if (m_ClickClip != null)
            AudioSource.PlayClipAtPoint(m_ClickClip, transform.position, 0.4f);
    }
}
