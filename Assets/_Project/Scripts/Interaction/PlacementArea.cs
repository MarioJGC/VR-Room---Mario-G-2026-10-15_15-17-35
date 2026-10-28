using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zonas del apartamento donde se pueden colocar muebles (una por planta).
/// FloorConstraintTransformer la usa para que, al arrastrar un mueble, su huella
/// no salga de la zona de su planta (no atraviesa paredes ni sale de la casa).
/// Las zonas están en coordenadas locales de este objeto (la raíz del apartamento).
/// </summary>
public class PlacementArea : MonoBehaviour
{
    [Serializable]
    public struct Zone
    {
        [Tooltip("Altura del suelo de la zona (local).")]
        public float floorHeight;
        [Tooltip("Rectángulo en planta: x = X local, y = Z local.")]
        public Rect rect;
    }

    [SerializeField] List<Zone> m_Zones = new List<Zone>();
    [Tooltip("Separación mínima entre el mueble y la pared.")]
    [SerializeField] float m_Margin = 0.03f;

    public static PlacementArea Instance { get; private set; }

    void OnEnable() => Instance = this;

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Devuelve el centro (mundo) corregido para que una huella de tamaño <paramref name="extents"/>
    /// quede dentro de la zona cuya altura de suelo está más cerca de <paramref name="worldBottomY"/>.
    /// </summary>
    public Vector3 ClampCenter(Vector3 worldCenter, Vector3 extents, float worldBottomY)
    {
        if (m_Zones.Count == 0)
            return worldCenter;

        var bottom = worldBottomY - transform.position.y;
        var zone = m_Zones[0];
        for (var i = 1; i < m_Zones.Count; i++)
        {
            if (Mathf.Abs(m_Zones[i].floorHeight - bottom) < Mathf.Abs(zone.floorHeight - bottom))
                zone = m_Zones[i];
        }

        var local = transform.InverseTransformPoint(worldCenter);
        var r = zone.rect;
        var ex = Mathf.Min(extents.x + m_Margin, r.width / 2f);
        var ez = Mathf.Min(extents.z + m_Margin, r.height / 2f);
        local.x = Mathf.Clamp(local.x, r.xMin + ex, r.xMax - ex);
        local.z = Mathf.Clamp(local.z, r.yMin + ez, r.yMax - ez);
        return transform.TransformPoint(local);
    }
}
