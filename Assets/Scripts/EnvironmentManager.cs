using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Calculates environmental values by interpolating 8 zones.
/// Also exposes and controls a global simulation timeScale via an optional UI slider.
/// Robust against accidental re-initialisations (ExecuteAlways-safe).
/// </summary>
[ExecuteAlways]
public class EnvironmentManager : MonoBehaviour
{
    public static EnvironmentManager Instance { get; private set; }

    [Header("Time Control")]
    [Tooltip("Controls the flow of time (e.g., 0.5 for half speed, 2 for double speed)")]
    [Range(0f, 5f)]
    public float timeScale = 0f;

    [Tooltip("Optional: slider that edits timeScale in play mode.")]
    public Slider timeScaleSlider;

    [Tooltip("Also mirror into UnityEngine.Time.timeScale in play mode.")]
    public bool alsoAffectUnityTimeScale = false;

    [Header("Universe Bounds")]
    [Tooltip("The min/max extents of the simulation world.")]
    public Bounds universeBounds;

    [Header("Zone Data (updated live)")]
    [Tooltip("Internal array of 8 environment zones, updated by Initialise().")]
    public Universe.EnvironmentZone[] zones = new Universe.EnvironmentZone[0];

    [Header("Runtime")]
    [Tooltip("True once Initialise(...) has been called at least once.")]
    public bool isInitialised = false;

    // --- internals for safe UI binding ---
    private bool _uiBound = false;
    private UnityAction<float> _sliderHandler;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyImmediate(this.gameObject);
            return;
        }
        Instance = this;

        // Keep field in range even in edit mode
        timeScale = Mathf.Clamp(timeScale, 0f, 5f);

        // Do not bind UI here (edit mode can call Awake due to ExecuteAlways)
        MirrorUnityTimeScaleIfNeeded();
    }

    void OnEnable()
    {
        // In edit mode, keep slider display in sync without firing events.
        if (!Application.isPlaying)
            SetSliderSafely_NoEvent(timeScale);
    }

    void Start()
    {
        // Only bind UI in play mode to avoid editor-time surprises.
        if (Application.isPlaying)
            BindSliderIfNeeded();

        MirrorUnityTimeScaleIfNeeded();
    }

    void Update()
    {
        // If user tweaks the field in Inspector during play, mirror to Unity time if asked.
        if (Application.isPlaying)
            MirrorUnityTimeScaleIfNeeded();
    }

    void OnDisable()
    {
        UnbindSliderIfBound();
    }

    void OnDestroy()
    {
        UnbindSliderIfBound();
        if (Instance == this) Instance = null;
    }

    // ----------------- Public API -----------------

    /// <summary>
    /// Called by the Universe (or MIDIManager) at startup or whenever zones change.
    /// Updates both the private interpolation data and the Inspector-visible array.
    /// </summary>
    public void Initialize(Bounds bounds, Universe.EnvironmentZone[] environmentZones)
    {
        universeBounds = bounds;

        zones = new Universe.EnvironmentZone[environmentZones.Length];
        environmentZones.CopyTo(zones, 0);

        isInitialised = true;
    }

    /// <summary> Returns the interpolated environment at any world point. </summary>
    public EnvironmentSample GetEnvironmentAtPoint(Vector3 worldPosition)
    {
        if (!isInitialised)
        {
            return new EnvironmentSample { temperature = 20f, pH = 7f, uvLightIntensity = 0f, toxinFieldStrength = 0f };
        }

        Vector3 p = new Vector3(
            Mathf.InverseLerp(universeBounds.min.x, universeBounds.max.x, worldPosition.x),
            Mathf.InverseLerp(universeBounds.min.y, universeBounds.max.y, worldPosition.y),
            Mathf.InverseLerp(universeBounds.min.z, universeBounds.max.z, worldPosition.z)
        );

        return new EnvironmentSample
        {
            temperature        = TrilinearInterpolate(z => z.temperature, p),
            pH                 = TrilinearInterpolate(z => z.pH, p),
            uvLightIntensity   = TrilinearInterpolate(z => z.uvLightIntensity, p),
            toxinFieldStrength = TrilinearInterpolate(z => z.toxinFieldStrength, p)
        };
    }

    [ContextMenu("Sample At Origin")]
    public void DebugSampleAtOrigin()
    {
        var s = GetEnvironmentAtPoint(Vector3.zero);
        Debug.Log($"Env @ origin → Temp:{s.temperature:F2}, pH:{s.pH:F2}, UV:{s.uvLightIntensity:F2}, Toxin:{s.toxinFieldStrength:F2}");
    }

    // ----------------- UI binding -----------------

    private void BindSliderIfNeeded()
    {
        if (timeScaleSlider == null || _uiBound) return;

        // Normalise slider config and set value without firing events
        timeScaleSlider.wholeNumbers = false;
        timeScaleSlider.minValue = 0f;
        timeScaleSlider.maxValue = 5f;
        SetSliderSafely_NoEvent(timeScale);

        _sliderHandler = OnSliderChanged;
        timeScaleSlider.onValueChanged.AddListener(_sliderHandler);
        _uiBound = true;
    }

    private void UnbindSliderIfBound()
    {
        if (timeScaleSlider != null && _uiBound && _sliderHandler != null)
        {
            timeScaleSlider.onValueChanged.RemoveListener(_sliderHandler);
        }
        _uiBound = false;
        _sliderHandler = null;
    }

    private void SetSliderSafely_NoEvent(float v)
    {
        if (timeScaleSlider == null) return;
        // Guard against null target graphic during domain reloads
        try { timeScaleSlider.SetValueWithoutNotify(Mathf.Clamp(v, timeScaleSlider.minValue, timeScaleSlider.maxValue)); }
        catch { /* ignore */ }
    }

    private void OnSliderChanged(float v)
    {
        timeScale = Mathf.Clamp(v, 0f, 5f);
        MirrorUnityTimeScaleIfNeeded();
    }

    private void MirrorUnityTimeScaleIfNeeded()
    {
        if (Application.isPlaying && alsoAffectUnityTimeScale)
            Time.timeScale = Mathf.Max(0f, timeScale); // don’t go negative
    }

    // ----------------- Maths -----------------

    private float TrilinearInterpolate(System.Func<Universe.EnvironmentZone, float> selector, Vector3 p)
    {
        if (zones == null || zones.Length != 8) return 0f;

        float[] c = new float[8];
        for (int i = 0; i < 8; i++)
            c[i] = selector(zones[i]);

        float c00 = Mathf.Lerp(c[0], c[1], p.x);
        float c01 = Mathf.Lerp(c[4], c[5], p.x);
        float c10 = Mathf.Lerp(c[2], c[3], p.x);
        float c11 = Mathf.Lerp(c[6], c[7], p.x);

        float c0 = Mathf.Lerp(c00, c10, p.y);
        float c1 = Mathf.Lerp(c01, c11, p.y);

        return Mathf.Lerp(c0, c1, p.z);
    }

    // ----------------- Types -----------------

    public struct EnvironmentSample
    {
        public float temperature;
        public float pH;
        public float uvLightIntensity;
        public float toxinFieldStrength;
    }
}
