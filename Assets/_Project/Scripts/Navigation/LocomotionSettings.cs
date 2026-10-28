using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// Opciones de navegación configurables por el usuario y punto central para el menú de ajustes:
///  - Movimiento continuo con el joystick izquierdo (on/off y velocidad).
///  - Giro por saltos o continuo con el joystick derecho (ángulo / velocidad).
///  - Pausa automática de moverse y girar mientras se sostiene un objeto con el rayo,
///    porque entonces el joystick sirve para acercarlo, alejarlo y girarlo.
/// Los valores se guardan entre sesiones (PlayerPrefs).
/// </summary>
public class LocomotionSettings : MonoBehaviour
{
    public enum TurnStyle { Snap, Smooth }

    // v2: el movimiento con joystick pasa a estar desactivado por defecto (se camina físicamente).
    const string k_PrefsPrefix = "VRRoom.Locomotion.v2.";

    [Header("Proveedores (se buscan solos si están vacíos)")]
    [SerializeField] ContinuousMoveProvider m_Move;
    [SerializeField] SnapTurnProvider m_SnapTurn;
    [SerializeField] ContinuousTurnProvider m_SmoothTurn;

    [Header("Valores por defecto")]
    [Tooltip("Andar con el joystick izquierdo. Desactivado por defecto: se camina físicamente y se teletransporta.")]
    [SerializeField] bool m_ContinuousMove = false;
    [SerializeField] TurnStyle m_TurnStyle = TurnStyle.Snap;
    [SerializeField, Range(0.5f, 4f)] float m_MoveSpeed = 1.5f;
    [SerializeField, Range(15f, 90f)] float m_SnapAngle = 45f;
    [SerializeField, Range(30f, 180f)] float m_SmoothTurnSpeed = 60f;

    [Tooltip("Pausa moverse y girar mientras un rayo sostiene un objeto.")]
    [SerializeField] bool m_PauseWhileHolding = true;

    readonly List<XRRayInteractor> m_Rays = new List<XRRayInteractor>();
    XRInputValueReader.InputSourceMode m_SnapLeftMode, m_SmoothLeftMode;
    int m_HeldCount;

    /// <summary>Se lanza cuando cambia cualquier ajuste (para refrescar el menú).</summary>
    public event Action Changed;

    public bool ContinuousMove
    {
        get => m_ContinuousMove;
        set { m_ContinuousMove = value; ApplyAndSave(); }
    }

    public TurnStyle Turn
    {
        get => m_TurnStyle;
        set { m_TurnStyle = value; ApplyAndSave(); }
    }

    public float MoveSpeed
    {
        get => m_MoveSpeed;
        set { m_MoveSpeed = Mathf.Clamp(value, 0.5f, 4f); ApplyAndSave(); }
    }

    public float SnapAngle
    {
        get => m_SnapAngle;
        set { m_SnapAngle = Mathf.Clamp(value, 15f, 90f); ApplyAndSave(); }
    }

    public float SmoothTurnSpeed
    {
        get => m_SmoothTurnSpeed;
        set { m_SmoothTurnSpeed = Mathf.Clamp(value, 30f, 180f); ApplyAndSave(); }
    }

    void Awake()
    {
        if (m_Move == null) m_Move = FindAnyObjectByType<ContinuousMoveProvider>(FindObjectsInactive.Include);
        if (m_SnapTurn == null) m_SnapTurn = FindAnyObjectByType<SnapTurnProvider>(FindObjectsInactive.Include);
        if (m_SmoothTurn == null) m_SmoothTurn = FindAnyObjectByType<ContinuousTurnProvider>(FindObjectsInactive.Include);

        // Modo original del joystick izquierdo en los giros, para restaurarlo si se quita el movimiento continuo.
        if (m_SnapTurn != null) m_SnapLeftMode = m_SnapTurn.leftHandTurnInput.inputSourceMode;
        if (m_SmoothTurn != null) m_SmoothLeftMode = m_SmoothTurn.leftHandTurnInput.inputSourceMode;

        Load();
    }

    void OnEnable()
    {
        m_Rays.Clear();
        m_Rays.AddRange(FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None));
        foreach (var ray in m_Rays)
        {
            ray.selectEntered.AddListener(OnRaySelectEntered);
            ray.selectExited.AddListener(OnRaySelectExited);
        }
    }

    void OnDisable()
    {
        foreach (var ray in m_Rays)
        {
            if (ray == null)
                continue;
            ray.selectEntered.RemoveListener(OnRaySelectEntered);
            ray.selectExited.RemoveListener(OnRaySelectExited);
        }
        m_HeldCount = 0;
    }

    void Start() => Apply();

    void OnRaySelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactableObject is XRGrabInteractable)
        {
            m_HeldCount++;
            Apply();
        }
    }

    void OnRaySelectExited(SelectExitEventArgs args)
    {
        if (args.interactableObject is XRGrabInteractable)
        {
            m_HeldCount = Mathf.Max(0, m_HeldCount - 1);
            Apply();
        }
    }

    void Apply()
    {
        var paused = m_PauseWhileHolding && m_HeldCount > 0;

        if (m_Move != null)
        {
            m_Move.enabled = m_ContinuousMove && !paused;
            m_Move.moveSpeed = m_MoveSpeed;
        }

        if (m_SnapTurn != null)
        {
            m_SnapTurn.enabled = m_TurnStyle == TurnStyle.Snap && !paused;
            m_SnapTurn.turnAmount = m_SnapAngle;
            // Con movimiento continuo, el joystick izquierdo es para andar y el derecho para girar.
            m_SnapTurn.leftHandTurnInput.inputSourceMode = m_ContinuousMove ? XRInputValueReader.InputSourceMode.Unused : m_SnapLeftMode;
        }

        if (m_SmoothTurn != null)
        {
            m_SmoothTurn.enabled = m_TurnStyle == TurnStyle.Smooth && !paused;
            m_SmoothTurn.turnSpeed = m_SmoothTurnSpeed;
            m_SmoothTurn.leftHandTurnInput.inputSourceMode = m_ContinuousMove ? XRInputValueReader.InputSourceMode.Unused : m_SmoothLeftMode;
        }
    }

    void ApplyAndSave()
    {
        Apply();
        Save();
        Changed?.Invoke();
    }

    void Load()
    {
        m_ContinuousMove = PlayerPrefs.GetInt(k_PrefsPrefix + "ContinuousMove", m_ContinuousMove ? 1 : 0) == 1;
        m_TurnStyle = (TurnStyle)PlayerPrefs.GetInt(k_PrefsPrefix + "TurnStyle", (int)m_TurnStyle);
        m_MoveSpeed = PlayerPrefs.GetFloat(k_PrefsPrefix + "MoveSpeed", m_MoveSpeed);
        m_SnapAngle = PlayerPrefs.GetFloat(k_PrefsPrefix + "SnapAngle", m_SnapAngle);
        m_SmoothTurnSpeed = PlayerPrefs.GetFloat(k_PrefsPrefix + "SmoothTurnSpeed", m_SmoothTurnSpeed);
    }

    void Save()
    {
        PlayerPrefs.SetInt(k_PrefsPrefix + "ContinuousMove", m_ContinuousMove ? 1 : 0);
        PlayerPrefs.SetInt(k_PrefsPrefix + "TurnStyle", (int)m_TurnStyle);
        PlayerPrefs.SetFloat(k_PrefsPrefix + "MoveSpeed", m_MoveSpeed);
        PlayerPrefs.SetFloat(k_PrefsPrefix + "SnapAngle", m_SnapAngle);
        PlayerPrefs.SetFloat(k_PrefsPrefix + "SmoothTurnSpeed", m_SmoothTurnSpeed);
        PlayerPrefs.Save();
    }
}
