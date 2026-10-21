using System;
using UnityEngine;

/// <summary>
/// Guarda qué mueble está seleccionado en cada momento. El menú de control
/// (Fase 2) lo usará para saber sobre qué objeto aplicar color, borrar, etc.
/// Si no hay ninguno en la escena, se crea automáticamente la primera vez que se usa.
/// </summary>
public class SelectionManager : MonoBehaviour
{
    static SelectionManager s_Instance;

    public static SelectionManager Instance
    {
        get
        {
            if (s_Instance == null)
                s_Instance = FindAnyObjectByType<SelectionManager>();
            if (s_Instance == null)
                s_Instance = new GameObject(nameof(SelectionManager)).AddComponent<SelectionManager>();
            return s_Instance;
        }
    }

    /// <summary>Indica si ya existe un SelectionManager (sin crearlo).</summary>
    public static bool Exists => s_Instance != null;

    /// <summary>Mueble seleccionado actualmente, o null si no hay ninguno.</summary>
    public FurnitureItem Current { get; private set; }

    /// <summary>Se lanza cada vez que cambia la selección (el argumento puede ser null).</summary>
    public event Action<FurnitureItem> SelectionChanged;

    void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(this);
            return;
        }
        s_Instance = this;
    }

    void OnDestroy()
    {
        if (s_Instance == this)
            s_Instance = null;
    }

    public void Select(FurnitureItem item)
    {
        if (Current == item)
            return;

        var previous = Current;
        Current = item;

        if (previous != null)
            previous.SetSelected(false);
        if (item != null)
            item.SetSelected(true);

        SelectionChanged?.Invoke(item);
    }

    public void Deselect() => Select(null);

    /// <summary>Quita la selección solo si el mueble indicado es el seleccionado.</summary>
    public void Release(FurnitureItem item)
    {
        if (Current == item)
            Select(null);
    }
}
