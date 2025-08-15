using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Unity.Burst;

[RequireComponent(typeof(AudioSource))]
public class MainSynth : MonoBehaviour
{
    // === PUBLIC PARAMETERS (TARGETS) ===
    [Header("Synth Settings")]
    [Tooltip("The time in seconds it takes for waveforms to morph into each other.")]
    public float waveformMorphTime = 0.2f;

    [Header("Oscillators")]
    public float frequency = 440f;
    public float detune = 0.0f;
    public Waveform osc1Waveform = Waveform.Sawtooth;
    public Waveform osc2Waveform = Waveform.Sawtooth;
    public Waveform osc3Waveform = Waveform.Sine;
    public float osc2Ratio = 1.5f;
    public float osc3Ratio = 2.0f;
    public float fmIndex = 0.5f;

    [Header("Modulators")]
    public float lfoFrequency = 0.4f;
    public float lfoDepth = 0.05f;
    public float vibratoFrequency = 6f;
    public float vibratoDepth = 0.01f;

    // RE-INTEGRATED: ADSR Envelope controls are now back with a toggle.
    [Header("ADSR Envelope")]
    [Tooltip("Enable the Attack, Decay, Sustain, Release envelope. Disabling this results in an instant on/off gate.")]
    public bool enableADSR = true;
    [Tooltip("Time in seconds for the sound to reach peak amplitude.")]
    public float attackTime = 0.02f;
    [Tooltip("Time in seconds for the sound to drop to the sustain level.")]
    public float decayTime = 0.15f;
    [Tooltip("The volume level a sound maintains after the decay phase (0 to 1).")]
    [Range(0f, 1f)] public float sustainLevel = 0.8f;
    [Tooltip("Time in seconds for the sound to fade out after being disabled.")]
    public float releaseTime = 1.0f;
    
    [Header("Formant Filter")]
    [Tooltip("If checked, an LFO will modulate the formant frequencies.")]
    public bool enableFormantLfo = true;
    public float f1Freq = 500f;
    public float f1Boost = 5f;
    public float f1Bandwidth = 100f;
    public float f2Freq = 1500f;
    public float f2Boost = 5f;
    public float f2Bandwidth = 150f;
    public float f3Freq = 2500f;
    public float f3Boost = 5f;
    public float f3Bandwidth = 200f;
    public Waveform formantLfoShape = Waveform.Sine;
    public float formantLfoFrequency = 8f;
    [Range(0f, 1f)] public float formantLfoDepth = 0.2f;

    [Header("Formant LFO Modulation")]
    [Tooltip("How much the main LFO modulates the Formant LFO's frequency.")]
    [Range(0f, 1f)] public float mainLfoToFormantFreq = 0f;
    [Tooltip("How much the main LFO modulates the Formant LFO's depth.")]
    [Range(0f, 1f)] public float mainLfoToFormantDepth = 0f;

    [Header("Post-Processing Filter & EQ")]
    public float filterCutoff = 3000f;
    public float filterResonance = 1.2f;
    public float lowShelfGain = 1.2f;
    public float midPeakGain = 0.8f;
    public float highShelfGain = 1.5f;

    [Header("Limiter Settings")]
    [Tooltip("Input gain for the limiter. Increases saturation and perceived loudness.")]
    [Range(0.1f, 4.0f)] public float limiterGain = 1.0f;
    
    [Header("Real-time Safety")]
    [Tooltip("Controls how quickly continuous parameters slew to their new target values.")]
    public float parameterSlewRate = 40.0f;

    [Header("Mixer")]
    public AudioMixerGroup outputMixerGroup;

    // === PRIVATE STATE ===
    private AudioSource _audioSource;
    private float _sampleRate;
    private float _invSampleRate;
    private bool _synthActive = false;

    private float _currentFrequency, _currentDetune, _currentOsc2Ratio, _currentOsc3Ratio, _currentFmIndex;
    private float _currentLfoFrequency, _currentLfoDepth, _currentVibratoFrequency, _currentVibratoDepth;
    private float _currentFilterCutoff, _currentFilterResonance;
    private float _currentF1Freq, _currentF1Boost, _currentF1Bandwidth;
    private float _currentF2Freq, _currentF2Boost, _currentF2Bandwidth;
    private float _currentF3Freq, _currentF3Boost, _currentF3Bandwidth;
    private float _currentFormantLfoFrequency, _currentFormantLfoDepth;
    private float _currentLowShelfGain, _currentMidPeakGain, _currentHighShelfGain;
    
    private Waveform _targetOsc1Waveform, _targetOsc2Waveform, _targetOsc3Waveform;
    private Waveform _prevOsc1Waveform, _prevOsc2Waveform, _prevOsc3Waveform;
    private float _osc1Morph, _osc2Morph, _osc3Morph;
    private float _waveformMorphRate;
    
    private double _phase1, _phase2, _phase3;
    private double _time;

    // RE-INTEGRATED: ADSR state variables
    private float _adsrEnvelope;
    private enum AdsrStage { Attack, Decay, Sustain, Release, Off }
    private AdsrStage _stage;
    
    private float _previousLpSample;
    private float _globalFormantLfoPhase;
    private Biquad[] _formantFilters = new Biquad[3];
    
    public enum Waveform { Sine, Triangle, Square, Sawtooth }
    private struct Biquad { public float b0, b1, b2, a1, a2, x1, x2, y1, y2; }

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _synthActive = true; 
    }

    void Start()
    {
        if (!_synthActive) return;
        if (outputMixerGroup != null) _audioSource.outputAudioMixerGroup = outputMixerGroup;

        _sampleRate = AudioSettings.outputSampleRate;
        _invSampleRate = 1f / _sampleRate;
        
        InitializeSmoothedParameters();
        InitializeWaveformState();

        if (waveformMorphTime > 0) _waveformMorphRate = _invSampleRate / waveformMorphTime;
        else _waveformMorphRate = 1.1f;

        _audioSource.playOnAwake = true;
        _audioSource.loop = true;

        // RE-INTEGRATED: Initialize ADSR
        _stage = AdsrStage.Off;
        _adsrEnvelope = 0.0f;
        
        _audioSource.Play();
    }

    // RE-INTEGRATED: OnEnable/OnDisable to trigger Attack and Release phases.
    void OnEnable()
    {
        if(!_synthActive) return;
        _stage = AdsrStage.Attack;
    }

    void OnDisable()
    {
        if(!_synthActive) return;
        _stage = AdsrStage.Release;
    }
    
    void Update()
    {
        CheckForWaveformChanges();
    }
    
    void InitializeSmoothedParameters()
    {
        // This method remains unchanged and correctly initializes all parameters
        _currentFrequency = frequency;
        _currentDetune = detune;
        _currentOsc2Ratio = osc2Ratio;
        _currentOsc3Ratio = osc3Ratio;
        _currentFmIndex = fmIndex;
        _currentLfoFrequency = lfoFrequency;
        _currentLfoDepth = lfoDepth;
        _currentVibratoFrequency = vibratoFrequency;
        _currentVibratoDepth = vibratoDepth;
        _currentFilterCutoff = filterCutoff;
        _currentFilterResonance = filterResonance;
        _currentLowShelfGain = lowShelfGain;
        _currentMidPeakGain = midPeakGain;
        _currentHighShelfGain = highShelfGain;
        _currentF1Freq = f1Freq;
        _currentF1Boost = f1Boost;
        _currentF1Bandwidth = f1Bandwidth;
        _currentF2Freq = f2Freq;
        _currentF2Boost = f2Boost;
        _currentF2Bandwidth = f2Bandwidth;
        _currentF3Freq = f3Freq;
        _currentF3Boost = f3Boost;
        _currentF3Bandwidth = f3Bandwidth;
        _currentFormantLfoFrequency = formantLfoFrequency;
        _currentFormantLfoDepth = formantLfoDepth;
    }

    void InitializeWaveformState()
    {
        _targetOsc1Waveform = _prevOsc1Waveform = osc1Waveform;
        _targetOsc2Waveform = _prevOsc2Waveform = osc2Waveform;
        _targetOsc3Waveform = _prevOsc3Waveform = osc3Waveform;
        _osc1Morph = _osc2Morph = _osc3Morph = 1.0f;
    }
    
    void CheckForWaveformChanges()
    {
        if (osc1Waveform != _targetOsc1Waveform) { _prevOsc1Waveform = _targetOsc1Waveform; _targetOsc1Waveform = osc1Waveform; _osc1Morph = 0.0f; }
        if (osc2Waveform != _targetOsc2Waveform) { _prevOsc2Waveform = _targetOsc2Waveform; _targetOsc2Waveform = osc2Waveform; _osc2Morph = 0.0f; }
        if (osc3Waveform != _targetOsc3Waveform) { _prevOsc3Waveform = _targetOsc3Waveform; _targetOsc3Waveform = osc3Waveform; _osc3Morph = 0.0f; }
    }

    void UpdateSmoothedParameters()
    {
        // This method remains unchanged.
        float slew = parameterSlewRate * _invSampleRate;
        _currentFrequency = Mathf.MoveTowards(_currentFrequency, frequency, slew * 100f);
        _currentDetune = Mathf.MoveTowards(_currentDetune, detune, slew * 10f);
        _currentOsc2Ratio = Mathf.MoveTowards(_currentOsc2Ratio, osc2Ratio, slew);
        _currentOsc3Ratio = Mathf.MoveTowards(_currentOsc3Ratio, osc3Ratio, slew);
        _currentFmIndex = Mathf.MoveTowards(_currentFmIndex, fmIndex, slew);
        _currentLfoFrequency = Mathf.MoveTowards(_currentLfoFrequency, lfoFrequency, slew);
        _currentLfoDepth = Mathf.MoveTowards(_currentLfoDepth, lfoDepth, slew);
        _currentVibratoFrequency = Mathf.MoveTowards(_currentVibratoFrequency, vibratoFrequency, slew * 2f);
        _currentVibratoDepth = Mathf.MoveTowards(_currentVibratoDepth, vibratoDepth, slew);
        _currentFilterCutoff = Mathf.MoveTowards(_currentFilterCutoff, filterCutoff, slew * 1000f);
        _currentFilterResonance = Mathf.MoveTowards(_currentFilterResonance, filterResonance, slew);
        _currentLowShelfGain = Mathf.MoveTowards(_currentLowShelfGain, lowShelfGain, slew);
        _currentMidPeakGain = Mathf.MoveTowards(_currentMidPeakGain, midPeakGain, slew);
        _currentHighShelfGain = Mathf.MoveTowards(_currentHighShelfGain, highShelfGain, slew);
        _currentF1Freq = Mathf.MoveTowards(_currentF1Freq, f1Freq, slew * 500f);
        _currentF1Boost = Mathf.MoveTowards(_currentF1Boost, f1Boost, slew);
        _currentF1Bandwidth = Mathf.MoveTowards(_currentF1Bandwidth, f1Bandwidth, slew * 100f);
        _currentF2Freq = Mathf.MoveTowards(_currentF2Freq, f2Freq, slew * 500f);
        _currentF2Boost = Mathf.MoveTowards(_currentF2Boost, f2Boost, slew);
        _currentF2Bandwidth = Mathf.MoveTowards(_currentF2Bandwidth, f2Bandwidth, slew * 100f);
        _currentF3Freq = Mathf.MoveTowards(_currentF3Freq, f3Freq, slew * 500f);
        _currentF3Boost = Mathf.MoveTowards(_currentF3Boost, f3Boost, slew);
        _currentF3Bandwidth = Mathf.MoveTowards(_currentF3Bandwidth, f3Bandwidth, slew * 100f);
        _currentFormantLfoFrequency = Mathf.MoveTowards(_currentFormantLfoFrequency, formantLfoFrequency, slew * 2f);
        _currentFormantLfoDepth = Mathf.MoveTowards(_currentFormantLfoDepth, formantLfoDepth, slew);
    }

    [BurstCompile]
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_synthActive) return;
        
        UpdateSmoothedParameters();

        if (_osc1Morph < 1.0f) _osc1Morph = Mathf.Min(1.0f, _osc1Morph + _waveformMorphRate);
        if (_osc2Morph < 1.0f) _osc2Morph = Mathf.Min(1.0f, _osc2Morph + _waveformMorphRate);
        if (_osc3Morph < 1.0f) _osc3Morph = Mathf.Min(1.0f, _osc3Morph + _waveformMorphRate);

        int sampleCount = data.Length / channels;
        
        for (int s = 0; s < sampleCount; s++)
        {
            // RE-INTEGRATED: ADSR State Machine
            if (enableADSR)
            {
                switch (_stage)
                {
                    case AdsrStage.Attack:
                        if (attackTime > 0.001f) _adsrEnvelope += _invSampleRate / attackTime; else _adsrEnvelope = 1.0f;
                        if (_adsrEnvelope >= 1.0f) { _adsrEnvelope = 1.0f; _stage = AdsrStage.Decay; }
                        break;
                    case AdsrStage.Decay:
                        if (decayTime > 0.001f) _adsrEnvelope -= _invSampleRate / decayTime; else _adsrEnvelope = sustainLevel;
                        if (_adsrEnvelope <= sustainLevel) { _adsrEnvelope = sustainLevel; _stage = AdsrStage.Sustain; }
                        break;
                    case AdsrStage.Sustain:
                        _adsrEnvelope = sustainLevel;
                        break;
                    case AdsrStage.Release:
                        if (releaseTime > 0.001f) _adsrEnvelope -= _invSampleRate / releaseTime; else _adsrEnvelope = 0.0f;
                        if (_adsrEnvelope <= 0.0f) { _adsrEnvelope = 0.0f; _stage = AdsrStage.Off; }
                        break;
                    case AdsrStage.Off:
                        _adsrEnvelope = 0.0f;
                        break;
                }
            }
            else
            {
                _adsrEnvelope = 1.0f;
            }

            float vibrato = Mathf.Sin(2f * Mathf.PI * _currentVibratoFrequency * (float)_time) * _currentVibratoDepth;
            float mainLfoValue = Mathf.Sin(2f * Mathf.PI * _currentLfoFrequency * (float)_time);
            float lfo = mainLfoValue * _currentLfoDepth;
            float f0 = _currentFrequency + _currentDetune + vibrato + lfo;

            double phaseInc1 = f0 * _invSampleRate; _phase1 = (_phase1 + phaseInc1) % 1.0;
            float o1 = Mathf.Lerp(Generate(_prevOsc1Waveform, (float)_phase1), Generate(_targetOsc1Waveform, (float)_phase1), _osc1Morph);
            double phaseInc2 = f0 * _currentOsc2Ratio * _invSampleRate; _phase2 = (_phase2 + phaseInc2) % 1.0;
            float o2 = Mathf.Lerp(Generate(_prevOsc2Waveform, (float)_phase2), Generate(_targetOsc2Waveform, (float)_phase2), _osc2Morph);
            double phaseInc3 = (f0 * _currentOsc3Ratio + (o2 * _currentFmIndex * f0)) * _invSampleRate; _phase3 = (_phase3 + phaseInc3) % 1.0;
            float o3 = Mathf.Lerp(Generate(_prevOsc3Waveform, (float)_phase3), Generate(_targetOsc3Waveform, (float)_phase3), _osc3Morph);

            float mixedSample = o1 * o3 * _adsrEnvelope;
            
            _time += _invSampleRate;

            float formantMod = 1.0f;
            if (enableFormantLfo)
            {
                float modulatedFormantFreq = _currentFormantLfoFrequency * (1.0f + mainLfoValue * mainLfoToFormantFreq);
                float modulatedFormantDepth = _currentFormantLfoDepth * (1.0f + mainLfoValue * mainLfoToFormantDepth);
                float formantLfoPhaseStep = modulatedFormantFreq * _invSampleRate;
                float formantLfoValue = Generate(formantLfoShape, _globalFormantLfoPhase);
                _globalFormantLfoPhase = (_globalFormantLfoPhase + formantLfoPhaseStep) % 1.0f;
                formantMod = 1.0f + formantLfoValue * modulatedFormantDepth;
            }
            
            UpdateFormantFilters(
                _currentF1Freq * formantMod, _currentF2Freq * formantMod, _currentF3Freq * formantMod,
                _currentF1Bandwidth, _currentF2Bandwidth, _currentF3Bandwidth,
                _currentF1Boost, _currentF2Boost, _currentF3Boost
            );
            
            float formantedSample = ProcessFormants(mixedSample);
            float lowpassedSample = LowPassFilter(formantedSample, _currentFilterCutoff, _currentFilterResonance);
            float eqdSample = ApplyEQ(lowpassedSample);
            float finalSample = ProcessLimiter(eqdSample);
            
            for (int ch = 0; ch < channels; ch++)
            {
                data[s * channels + ch] = finalSample;
            }
        }
    }
    
    float ProcessLimiter(float sample) { return (float)System.Math.Tanh(sample * limiterGain); }
    
    float Generate(Waveform w, float phase)
    {
        float p = 2f * Mathf.PI * phase;
        switch(w) {
            case Waveform.Sine: return Mathf.Sin(p);
            case Waveform.Square: return Mathf.Sign(Mathf.Sin(p));
            case Waveform.Triangle: return 1f - 4f * Mathf.Abs(Mathf.Round(phase - 0.25f) - (phase - 0.25f));
            case Waveform.Sawtooth: return 2f * (phase - Mathf.Floor(phase + 0.5f));
            default: return 0f;
        }
    }

    float LowPassFilter(float x, float cutoff, float resonance)
    {
        float RC = 1f / (cutoff * 2 * Mathf.PI);
        float a = _invSampleRate / (RC + _invSampleRate);
        float y = a * x + (1f - a) * _previousLpSample;
        _previousLpSample = y;
        return y * resonance;
    }
    
    float ApplyEQ(float s)
    {
        return s * (_currentLowShelfGain + _currentMidPeakGain + _currentHighShelfGain) / 3.0f;
    }

    void UpdateFormantFilters(float f1, float f2, float f3, float bw1, float bw2, float bw3, float b1, float b2, float b3)
    {
        SetupBiquad(0, f1, bw1, b1);
        SetupBiquad(1, f2, bw2, b2);
        SetupBiquad(2, f3, bw3, b3);
    }

    void SetupBiquad(int i, float freq, float bw, float boostDB)
    {
        if (freq <= 0 || freq >= _sampleRate / 2) freq = 1000f; 
        if (bw <= 0) bw = 100f;

        float A = Mathf.Pow(10f, boostDB / 40f);
        float w0 = 2f * Mathf.PI * freq * _invSampleRate;
        float cosW0 = Mathf.Cos(w0);
        float sinW0 = Mathf.Sin(w0);
        float alpha = sinW0 / (2f * (freq / bw)); 

        float a0 = 1 + alpha / A;
        float a1 = -2 * cosW0;
        float a2 = 1 - alpha / A;
        float b0 = 1 + alpha * A;
        float b1 = -2 * cosW0;
        float b2 = 1 - alpha * A;

        ref var z = ref _formantFilters[i];
        float invA0 = 1f / a0;
        z.b0 = b0 * invA0; z.b1 = b1 * invA0; z.b2 = b2 * invA0;
        z.a1 = a1 * invA0; z.a2 = a2 * invA0;
    }

    float ProcessFormants(float x)
    {
        float y = x;
        for (int i = 0; i < _formantFilters.Length; i++)
        {
            ref var b = ref _formantFilters[i];
            float outp = b.b0 * y + b.b1 * b.x1 + b.b2 * b.x2 - b.a1 * b.y1 - b.a2 * b.y2;
            b.x2 = b.x1; b.x1 = y;
            b.y2 = b.y1; b.y1 = outp;
            y = outp;
        }
        return y;
    }
}