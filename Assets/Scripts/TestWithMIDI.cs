using UnityEngine;
using MidiJack;

/// <summary>
/// Manages MIDI input from a Novation Launch Control XL to control the 8 environmental zones in the Universe.
/// Implements a "takeover" feature: initial values are taken from the Universe inspector, and MIDI controls
/// only take effect after they are first moved by the user.
/// </summary>
public class MIDIManager : MonoBehaviour
{
    [Header("Target Universe")]
    [Tooltip("Drag your Universe GameObject here.")]
    public Universe universe;

    // --- MIDI CC Mapping for Novation Launch Control XL (Default Template) ---
    private const int TOXIN_KNOB_START_CC = 13; // Top row of knobs (Send A)
    private const int PH_KNOB_START_CC = 29;   // Middle row of knobs (Send B)
    private const int UV_KNOB_START_CC = 49;   // Bottom row of knobs (Pan/Device)
    private const int TEMP_FADER_START_CC = 77; // Faders

    [Header("Value Mapping Ranges")]
    [Tooltip("Set the min (X) and max (Y) temperature range.")]
    public Vector2 temperatureRange = new Vector2(0f, 100f);
    [Tooltip("Set the min (X) and max (Y) pH range.")]
    public Vector2 phRange = new Vector2(0f, 14f);
    [Tooltip("Set the min (X) and max (Y) UV intensity range.")]
    public Vector2 uvRange = new Vector2(0f, 2.0f);
    [Tooltip("Set the min (X) and max (Y) toxin strength range.")]
    public Vector2 toxinRange = new Vector2(0f, 2.0f);

    // --- State variables for the Takeover logic ---
    // [channel, parameter]
    private float[,] lastMidiValues;
    private bool[,] hasControlBeenTaken;

    // Enum to make parameter indexing more readable
    private enum MidiParam { Toxin, pH, UV, Temp }

    void Start()
    {
        // Initialize the arrays to hold the state for our 8 channels and 4 parameters
        lastMidiValues = new float[8, 4];
        hasControlBeenTaken = new bool[8, 4];

        // At the start of the simulation, record the initial position of all MIDI controls.
        // We will compare against these values to detect the first movement.
        for (int i = 0; i < 8; i++)
        {
            lastMidiValues[i, (int)MidiParam.Toxin] = MidiMaster.GetKnob(TOXIN_KNOB_START_CC + i);
            lastMidiValues[i, (int)MidiParam.pH]    = MidiMaster.GetKnob(PH_KNOB_START_CC + i);
            lastMidiValues[i, (int)MidiParam.UV]     = MidiMaster.GetKnob(UV_KNOB_START_CC + i);
            lastMidiValues[i, (int)MidiParam.Temp]  = MidiMaster.GetKnob(TEMP_FADER_START_CC + i);
        }
    }

    void Update()
    {
        if (universe == null || universe.zones == null || universe.zones.Length != 8)
        {
            return;
        }

        bool valuesChanged = false;

        // Loop through all 8 channels/zones
        for (int i = 0; i < 8; i++)
        {
            // --- Process each parameter with the takeover logic ---
            
            // Toxin
            valuesChanged |= ProcessMidiInput(i, MidiParam.Toxin, TOXIN_KNOB_START_CC + i, toxinRange, universe.zones[i].toxinFieldStrength, out universe.zones[i].toxinFieldStrength);

            // pH
            valuesChanged |= ProcessMidiInput(i, MidiParam.pH, PH_KNOB_START_CC + i, phRange, universe.zones[i].pH, out universe.zones[i].pH);

            // UV
            valuesChanged |= ProcessMidiInput(i, MidiParam.UV, UV_KNOB_START_CC + i, uvRange, universe.zones[i].uvLightIntensity, out universe.zones[i].uvLightIntensity);

            // Temperature
            valuesChanged |= ProcessMidiInput(i, MidiParam.Temp, TEMP_FADER_START_CC + i, temperatureRange, universe.zones[i].temperature, out universe.zones[i].temperature);
        }

        // If any of the values were actively changed by the MIDI controller...
        if (valuesChanged && EnvironmentManager.Instance != null)
        {
            // ...re-initialize the EnvironmentManager to update the simulation.
            EnvironmentManager.Instance.Initialize(universe.Bounds, universe.zones);
        }
    }

    /// <summary>
    /// Processes a single MIDI control, handles the takeover logic, and updates the simulation value.
    /// </summary>
    /// <returns>True if the simulation value was changed, false otherwise.</returns>
    private bool ProcessMidiInput(int channel, MidiParam param, int cc, Vector2 range, float currentSimValue, out float newSimValue)
    {
        int paramIndex = (int)param;
        float currentMidiValue = MidiMaster.GetKnob(cc);
        bool valueWasChanged = false;

        // First, check if control for this parameter has been taken over yet.
        if (!hasControlBeenTaken[channel, paramIndex])
        {
            // If not taken, check if the knob has moved from its initial position.
            if (!Mathf.Approximately(currentMidiValue, lastMidiValues[channel, paramIndex]))
            {
                // The user has moved the knob for the first time! Take control.
                hasControlBeenTaken[channel, paramIndex] = true;
                Debug.Log($"<color=cyan>MIDI Takeover:</color> Channel {channel + 1}, Parameter '{param}'");
            }
        }
        
        // If control has been taken, we can now update the simulation value.
        if (hasControlBeenTaken[channel, paramIndex])
        {
            float mappedValue = Mathf.Lerp(range.x, range.y, currentMidiValue);
            
            // Only flag a change if the new value is different from the current simulation value
            if (!Mathf.Approximately(currentSimValue, mappedValue))
            {
                valueWasChanged = true;
            }
            newSimValue = mappedValue;
        }
        else
        {
            // If control has not been taken, do not change the simulation value.
            newSimValue = currentSimValue;
        }

        // Always update the last known MIDI value for the next frame's comparison.
        lastMidiValues[channel, paramIndex] = currentMidiValue;

        return valueWasChanged;
    }
}