using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Aparato que se enciende y apaga: lámparas (luces), tocadiscos (música) o TV (vídeo).
/// Lo activan otros componentes: ClickToggle (apuntar y pulsar) o ActivateToggle
/// (gatillo mientras se sostiene un objeto, por ejemplo el mando de la TV).
/// </summary>
public class ToggleDevice : MonoBehaviour
{
    [SerializeField] bool m_StartOn;

    [Header("Qué controla")]
    [SerializeField] List<Light> m_Lights = new List<Light>();
    [SerializeField] AudioSource m_Audio;
    [SerializeField] VideoPlayer m_Video;
    [Tooltip("Pantalla cuyo material se cambia al encender (TV).")]
    [SerializeField] Renderer m_Screen;
    [SerializeField] Material m_ScreenOnMaterial;

    [Header("Feedback")]
    [SerializeField] AudioClip m_ToggleClip;

    Material m_ScreenOffMaterial;

    public bool IsOn { get; private set; }

    /// <summary>Se lanza al encender (true) o apagar (false).</summary>
    public event Action<bool> Toggled;

    void Awake()
    {
        if (m_Screen != null)
            m_ScreenOffMaterial = m_Screen.sharedMaterial;
    }

    void Start() => Apply(m_StartOn, false);

    public void Toggle() => Apply(!IsOn, true);

    public void SetOn(bool on) => Apply(on, true);

    void Apply(bool on, bool playFeedback)
    {
        IsOn = on;

        foreach (var light in m_Lights)
        {
            if (light != null)
                light.enabled = on;
        }

        if (m_Audio != null)
        {
            if (on) m_Audio.Play();
            else m_Audio.Stop();
        }

        if (m_Video != null)
        {
            if (on) m_Video.Play();
            else m_Video.Stop();
        }

        if (m_Screen != null && m_ScreenOnMaterial != null)
            m_Screen.sharedMaterial = on ? m_ScreenOnMaterial : m_ScreenOffMaterial;

        if (playFeedback && m_ToggleClip != null)
            AudioSource.PlayClipAtPoint(m_ToggleClip, transform.position, 0.6f);

        Toggled?.Invoke(on);
    }

    // --- Configuración desde scripts de editor (constructor del apartamento) ---

    public void Configure(bool startOn, AudioClip toggleClip)
    {
        m_StartOn = startOn;
        m_ToggleClip = toggleClip;
    }

    public void AddLight(Light light)
    {
        if (light != null && !m_Lights.Contains(light))
            m_Lights.Add(light);
    }

    public void SetAudio(AudioSource audioSource) => m_Audio = audioSource;

    public void SetVideo(VideoPlayer video, Renderer screen, Material screenOnMaterial)
    {
        m_Video = video;
        m_Screen = screen;
        m_ScreenOnMaterial = screenOnMaterial;
    }
}
