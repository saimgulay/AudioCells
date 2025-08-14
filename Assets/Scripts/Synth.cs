using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

#region Data Classes and Structs

[System.Serializable]
public class OscillatorSettings
{
    [Header("Standard Waveform Mixer")]
    [Tooltip("The mix level for the Sine wave.")]     [Range(0.0f, 1.0f)] public float Sine_Mix = 1.0f;
    [Tooltip("The mix level for the Square wave.")]   [Range(0.0f, 1.0f)] public float Square_Mix = 0.0f;
    [Tooltip("The mix level for the Sawtooth wave.")] [Range(0.0f, 1.0f)] public float Sawtooth_Mix = 0.0f;
    [Tooltip("The mix level for the Triangle wave.")] [Range(0.0f, 1.0f)] public float Triangle_Mix = 0.0f;
    [Tooltip("The mix level for the Ramp wave.")]     [Range(0.0f, 1.0f)] public float Ramp_Mix = 0.0f;
    [Tooltip("The mix level for the generatively created Wavetable.")] [Range(0.0f, 1.0f)] public float Wavetable_Mix = 0.0f;

    [Header("Additive Wavetable Generator")]
    [Tooltip("Amplitudes of the first 16 harmonics used to generate the wavetable.")]
    public float[] Harmonics = new float[16];

    [Header("Core Properties")]
    public float Frequency = 440.0f;
    [Range(0.0f, 1.0f)] public float Amplitude = 0.5f;
    public float Offset = 0.0f;

    public OscillatorSettings()
    {
        if (Harmonics != null && Harmonics.Length > 0)
            Harmonics[0] = 1.0f;
    }
}

[System.Serializable]
public class ParametricEQBand
{
    public bool Enabled = true;
    public float Frequency = 1000.0f;
    [Range(0.1f, 10.0f)] public float Bandwidth = 3.0f;
    [Range(-24.0f, 24.0f)] public float Boost_dB = 0.0f;
    [Header("Direct LFO Modulation Input")]
    public float Frequency_Mod_Amount = 0.0f;
    public float Bandwidth_Mod_Amount = 0.0f;
    public float Boost_Mod_Amount = 0.0f;
}

[System.Serializable]
public class FrequencyFilterSettings
{
    public enum FilterType { LowPass, HighPass, BandPass }
    public bool Enabled = true;
    public FilterType Type = FilterType.LowPass;
    [Range(20.0f, 20000.0f)] public float Cutoff = 20000.0f;
    [Range(0.707f, 20.0f)] public float Resonance = 1.0f;
    [Header("Direct LFO Modulation Input")]
    public float Cutoff_Mod_Amount = 0.0f;
    public float Resonance_Mod_Amount = 0.0f;
}

[System.Serializable]
public class WaveshaperSettings
{
    public enum Function { None, Atan, Tanh, Fuzz }
    public bool Enabled;
    public Function Shape = Function.Atan;
    [Range(1.0f, 100.0f)] public float Drive = 1.0f;
    [Range(0.0f, 1.0f)] public float Mix = 1.0f;
}

[System.Serializable]
public class CombFilterSettings
{
    public bool Enabled;
    [Range(1.0f, 100.0f)] public float Delay_ms = 20.0f;
    [Range(-0.99f, 0.99f)] public float Feedback = 0.5f;
    [Range(0.0f, 1.0f)] public float Mix = 0.5f;
}

[System.Serializable]
public class OperationSettings
{
    public enum CombineOperation { Add, Multiply, PhaseMod }
    public CombineOperation Operation = CombineOperation.Add;
    [Header("Pre-OP")] public float PreOp_Multiply = 1.0f;
    public float PreOp_Add = 0.0f;
    [Header("Post-OP")] public float PostOp_Multiply = 1.0f;
    public float PostOp_Add = 0.0f;
}

public enum ModSource { None, LFO1, LFO2 }
public enum ModDestination
{
    None, Osc1_Freq, Osc1_Amp, Osc2_Freq, Osc2_Amp,
    LFO1_Freq, LFO1_Amp, LFO2_Freq, LFO2_Amp,
    Waveshaper_Drive, Waveshaper_Mix,
    ParaEQ1_Freq, ParaEQ1_BW, ParaEQ1_Boost,
    ParaEQ2_Freq, ParaEQ2_BW, ParaEQ2_Boost,
    ParaEQ3_Freq, ParaEQ3_BW, ParaEQ3_Boost,
    FreqFilt1_Cutoff, FreqFilt1_Reso,
    FreqFilt2_Cutoff, FreqFilt2_Reso,
    FreqFilt3_Cutoff, FreqFilt3_Reso,
    FreqFilt4_Cutoff, FreqFilt4_Reso,
    Comb_Delay, Comb_Feedback, Comb_Mix
}

[System.Serializable]
public class ModMatrixEntry
{
    public bool Enabled = true;
    public ModSource Source = ModSource.None;
    public ModDestination Destination = ModDestination.None;
    [Range(-1.0f, 1.0f)] public float Amount = 1.0f;
}

public struct OscillatorSettingsStruct
{
    public float Sine_Mix, Square_Mix, Sawtooth_Mix, Triangle_Mix, Ramp_Mix, Wavetable_Mix;
    public float Frequency, Amplitude, Offset;
}

public struct OperationSettingsStruct
{
    public OperationSettings.CombineOperation op;
    public float PreOp_Multiply, PreOp_Add, PostOp_Multiply, PostOp_Add;
}

public struct WaveshaperSettingsStruct
{
    public bool Enabled;
    public WaveshaperSettings.Function Shape;
    public float Drive, Mix;
}

public struct BiquadFilterStruct
{
    public bool Enabled;
    public double a0, a1, a2, b1, b2;
}

#endregion

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct SynthJob : IJob
{
    public NativeArray<float> OutputBuffer;
    public NativeArray<double> Phases;
    [ReadOnly] public NativeArray<float> Wavetable1, Wavetable2;
    public NativeArray<double> FilterStates;
    public OscillatorSettingsStruct Osc1, Osc2;
    public OperationSettingsStruct AudioOps;
    public WaveshaperSettingsStruct Waveshaper;
    [ReadOnly] public NativeArray<BiquadFilterStruct> EQFilters;
    [ReadOnly] public NativeArray<BiquadFilterStruct> FrequencyFilters;
    public int Channels;
    public double SampleRate;

    public void Execute()
    {
        double p1 = Phases[0], p2 = Phases[1];
        double p1_inc = Osc1.Frequency * 2.0 * math.PI / SampleRate;
        double p2_inc = Osc2.Frequency * 2.0 * math.PI / SampleRate;

        for (int i = 0; i < OutputBuffer.Length; i += Channels)
        {
            float osc1Sample, osc2Sample, combinedAudio;
            osc2Sample = GenerateSample(Osc2, p2, Wavetable2);

            if (AudioOps.op == OperationSettings.CombineOperation.PhaseMod)
            {
                osc1Sample = GenerateSample(Osc1, p1 + (osc2Sample * math.PI), Wavetable1);
                combinedAudio = osc1Sample;
            }
            else
            {
                osc1Sample = GenerateSample(Osc1, p1, Wavetable1);
                if (AudioOps.op == OperationSettings.CombineOperation.Add)
                    combinedAudio = osc1Sample + osc2Sample;
                else
                    combinedAudio = osc1Sample * osc2Sample;
            }

            float opsAudio = (combinedAudio * AudioOps.PreOp_Multiply + AudioOps.PreOp_Add)
                            * AudioOps.PostOp_Multiply + AudioOps.PostOp_Add;
            float shapedAudio = ProcessWaveshaper(opsAudio, Waveshaper);

            float leftSample = shapedAudio, rightSample = shapedAudio;

            // Parametric EQ (3 bands)
            for (int b = 0; b < 3; b++)
            {
                if (EQFilters[b].Enabled)
                {
                    int s = b * 4;
                    leftSample = ProcessFilter(FilterStates, s, EQFilters[b], leftSample);
                    if (Channels > 1)
                        rightSample = ProcessFilter(FilterStates, s + 2, EQFilters[b], rightSample);
                }
            }

            // Frequency Filters (4 bands)
            for (int b = 0; b < 4; b++)
            {
                if (FrequencyFilters[b].Enabled)
                {
                    int s = (3 + b) * 4;
                    leftSample = ProcessFilter(FilterStates, s, FrequencyFilters[b], leftSample);
                    if (Channels > 1)
                        rightSample = ProcessFilter(FilterStates, s + 2, FrequencyFilters[b], rightSample);
                }
            }

            OutputBuffer[i] = leftSample;
            if (Channels > 1) OutputBuffer[i + 1] = rightSample;

            p1 += p1_inc;
            p2 += p2_inc;
        }

        Phases[0] = p1;
        Phases[1] = p2;
    }

    private float GenerateSample(
        OscillatorSettingsStruct s, double phase, NativeArray<float> wavetable)
    {
        float pnm = (float)(phase / (math.PI * 2));
        pnm -= math.floor(pnm);
        float sine = math.sin((float)phase);
        float square = math.sign(sine);
        float saw = 1f - 2f * pnm;
        float tri = math.abs(pnm * 2f - 1f) * 2f - 1f;
        float ramp = 2f * pnm - 1f;
        float wt = 0f;
        if (s.Wavetable_Mix > 0.001f && wavetable.IsCreated && wavetable.Length > 1)
        {
            float fi = pnm * (wavetable.Length - 1);
            int i1 = (int)fi;
            int i2 = math.min(i1 + 1, wavetable.Length - 1);
            wt = math.lerp(wavetable[i1], wavetable[i2], fi - i1);
        }
        float mix =
            (sine * s.Sine_Mix) +
            (square * s.Square_Mix) +
            (saw * s.Sawtooth_Mix) +
            (tri * s.Triangle_Mix) +
            (ramp * s.Ramp_Mix) +
            (wt * s.Wavetable_Mix);
        float tm =
            s.Sine_Mix +
            s.Square_Mix +
            s.Sawtooth_Mix +
            s.Triangle_Mix +
            s.Ramp_Mix +
            s.Wavetable_Mix;
        if (tm > 1e-6f) mix /= tm;
        return (mix * s.Amplitude) + s.Offset;
    }

    private float ProcessWaveshaper(float input, WaveshaperSettingsStruct s)
    {
        if (!s.Enabled) return input;
        float dr = input * s.Drive;
        float shp = dr;
        switch (s.Shape)
        {
            case WaveshaperSettings.Function.Atan:
                shp = math.atan(dr);
                break;
            case WaveshaperSettings.Function.Tanh:
                shp = math.tanh(dr);
                break;
            case WaveshaperSettings.Function.Fuzz:
                shp = math.sign(dr) * (1f - math.exp(-math.abs(dr)));
                break;
        }
        return math.lerp(input, shp, s.Mix);
    }

    private float ProcessFilter(
        NativeArray<double> states, int si, BiquadFilterStruct f, float i)
    {
        double z1 = states[si], z2 = states[si + 1];
        double o = i * f.a0 + z1;
        z1 = i * f.a1 + z2 - f.b1 * o;
        z2 = i * f.a2 - f.b2 * o;
        states[si] = z1;
        states[si + 1] = z2;
        return (float)o;
    }
}

[RequireComponent(typeof(AudioSource))]
public class Synth : MonoBehaviour
{
    [Header("Oscillator 1")] public OscillatorSettings oscillator1;
    [Header("Oscillator 2")] public OscillatorSettings oscillator2;
    [Header("Oscillator Operations")] public OperationSettings audioOperations;
    [Header("Waveshaper")] public WaveshaperSettings waveshaper;
    [Header("Frequency Filters")] public FrequencyFilterSettings[] frequencyFilters = new FrequencyFilterSettings[4];
    [Header("Audio Parameters EQ")] public ParametricEQBand[] eqBands = new ParametricEQBand[3];
    [Header("Comb Filter")] public CombFilterSettings combFilter;
    [Header("Formant LFO 1")] public OscillatorSettings lfo1;
    [Header("Formant LFO 2")] public OscillatorSettings lfo2;
    [Header("Formant LFO Operations")] public OperationSettings lfoOperations;
    [Header("Modulation Matrix")] public ModMatrixEntry[] modMatrix = new ModMatrixEntry[8];
    [Header("Master Output")]
    [Range(-20f, 0f)] public float Limiter_Threshold_dB = -0.1f;
    [Range(0.1f, 50f)] public float Limiter_Attack_ms = 5f;
    [Range(5f, 500f)] public float Limiter_Release_ms = 100f;
    [SerializeField, Range(-60f, 6f)] private float Output_Peak_dB = -60f;

    private double sampleRate;
    private float limiterEnvelope = 1.0f;
    private float[] combBuffer;
    private int combWriteIndex;
    private Dictionary<ModDestination, float> modMatrixValues = new Dictionary<ModDestination, float>();
    private bool wavetable1Dirty = true, wavetable2Dirty = true;

    private NativeArray<float> jobOutputBuffer;
    private NativeArray<float> wavetable1Data, wavetable2Data;
    private NativeArray<double> jobPhases;
    private NativeArray<BiquadFilterStruct> jobEQFilters, jobFrequencyFilters;
    private NativeArray<double> jobFilterStates;
    private JobHandle jobHandle;

    private const int WAVETABLE_SIZE = 2048;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        int bufferSize, numBuffers;
        AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);

        // Başlangıçta stereo için
        jobOutputBuffer = new NativeArray<float>(bufferSize * 2, Allocator.Persistent);

        jobPhases = new NativeArray<double>(4, Allocator.Persistent);
        jobEQFilters = new NativeArray<BiquadFilterStruct>(3, Allocator.Persistent);
        jobFrequencyFilters = new NativeArray<BiquadFilterStruct>(4, Allocator.Persistent);
        jobFilterStates = new NativeArray<double>((jobEQFilters.Length + jobFrequencyFilters.Length) * 4, Allocator.Persistent);

        combBuffer = new float[(int)sampleRate * 2];

        // Null-reference hatalarını önlemek için dizi elemanlarını instantiate ediyoruz
        for (int i = 0; i < eqBands.Length; i++)
            if (eqBands[i] == null) eqBands[i] = new ParametricEQBand();
        for (int i = 0; i < frequencyFilters.Length; i++)
            if (frequencyFilters[i] == null) frequencyFilters[i] = new FrequencyFilterSettings();
        for (int i = 0; i < modMatrix.Length; i++)
            if (modMatrix[i] == null) modMatrix[i] = new ModMatrixEntry();
    }

    void OnDestroy()
    {
        jobHandle.Complete();
        if (jobOutputBuffer.IsCreated) jobOutputBuffer.Dispose();
        if (wavetable1Data.IsCreated) wavetable1Data.Dispose();
        if (wavetable2Data.IsCreated) wavetable2Data.Dispose();
        if (jobPhases.IsCreated) jobPhases.Dispose();
        if (jobEQFilters.IsCreated) jobEQFilters.Dispose();
        if (jobFrequencyFilters.IsCreated) jobFrequencyFilters.Dispose();
        if (jobFilterStates.IsCreated) jobFilterStates.Dispose();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            jobHandle.Complete();
            wavetable1Dirty = wavetable2Dirty = true;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        jobHandle.Complete();

        // Kanal sayısına göre buffer yeniden oluşturma
        if (jobOutputBuffer.Length != data.Length)
        {
            if (jobOutputBuffer.IsCreated) jobOutputBuffer.Dispose();
            jobOutputBuffer = new NativeArray<float>(data.Length, Allocator.Persistent);
        }

        if (wavetable1Dirty)
        {
            GenerateWavetable(oscillator1, ref wavetable1Data);
            wavetable1Dirty = false;
        }
        if (wavetable2Dirty)
        {
            GenerateWavetable(oscillator2, ref wavetable2Data);
            wavetable2Dirty = false;
        }

        float finalLFOValue = ProcessLFOsAndModMatrix(data.Length, channels);

        var osc1_mod = GetModulatedOsc(oscillator1, ModDestination.Osc1_Freq, ModDestination.Osc1_Amp);
        var osc2_mod = GetModulatedOsc(oscillator2, ModDestination.Osc2_Freq, ModDestination.Osc2_Amp);

        UpdateEQCoefficients(finalLFOValue);
        UpdateFrequencyFilterCoefficients(finalLFOValue);

        var job = new SynthJob
        {
            OutputBuffer     = jobOutputBuffer,
            Phases           = jobPhases,
            Wavetable1       = wavetable1Data,
            Wavetable2       = wavetable2Data,
            FilterStates     = jobFilterStates,
            Osc1             = osc1_mod,
            Osc2             = osc2_mod,
            AudioOps         = CopyOpSettings(audioOperations),
            Waveshaper       = GetModulatedWaveshaper(),
            EQFilters        = jobEQFilters,
            FrequencyFilters = jobFrequencyFilters,
            Channels         = channels,
            SampleRate       = sampleRate
        };

        jobHandle = job.Schedule();
        jobHandle.Complete();

        jobOutputBuffer.Slice(0, data.Length).CopyTo(data);

        ApplyCombFilter(data, channels);
        ApplyLimiterAndMetering(data);
    }
    public void SetWavetablesDirty()
    {
        wavetable1Dirty = true;
        wavetable2Dirty = true;
    }

    void GenerateWavetable(OscillatorSettings osc, ref NativeArray<float> dataArray)
    {
        if (dataArray.IsCreated) dataArray.Dispose();
        var buffer = new float[WAVETABLE_SIZE];
        float maxAmp = 0f;

        for (int i = 0; i < WAVETABLE_SIZE; i++)
        {
            double currentSample = 0.0;
            for (int h = 0; h < osc.Harmonics.Length; h++)
            {
                if (Mathf.Abs(osc.Harmonics[h]) > 0.001f)
                    currentSample += osc.Harmonics[h] *
                        math.sin((h + 1) * 2.0 * math.PI * i / WAVETABLE_SIZE);
            }
            buffer[i] = (float)currentSample;
            if (math.abs(buffer[i]) > maxAmp) maxAmp = math.abs(buffer[i]);
        }

        if (maxAmp > 0.001f)
            for (int i = 0; i < WAVETABLE_SIZE; i++)
                buffer[i] /= maxAmp;

        dataArray = new NativeArray<float>(buffer, Allocator.Persistent);
    }

    float ProcessLFOsAndModMatrix(int bufferLength, int numChannels)
    {
        modMatrixValues.Clear();
        var lfo1Settings = GetModulatedOsc(lfo1, ModDestination.LFO1_Freq, ModDestination.LFO1_Amp);
        var lfo2Settings = GetModulatedOsc(lfo2, ModDestination.LFO2_Freq, ModDestination.LFO2_Amp);

        jobPhases[2] += lfo1Settings.Frequency * 2.0 * Mathf.PI * bufferLength / (numChannels * (float)sampleRate);
        jobPhases[3] += lfo2Settings.Frequency * 2.0 * Mathf.PI * bufferLength / (numChannels * (float)sampleRate);

        float lfo1Sample = GenerateMonoSample(lfo1Settings, jobPhases[2]);
        float lfo2Sample = GenerateMonoSample(lfo2Settings, jobPhases[3]);
        float combinedLFO = ProcessOp(lfo1Sample, lfo2Sample, lfoOperations);

        float[] lfoVals = { 0, lfo1Sample, lfo2Sample };
        foreach (var entry in modMatrix)
        {
            if (entry.Enabled && entry.Source != ModSource.None)
            {
                if (!modMatrixValues.ContainsKey(entry.Destination))
                    modMatrixValues[entry.Destination] = 0;
                modMatrixValues[entry.Destination] += lfoVals[(int)entry.Source] * entry.Amount;
            }
        }

        return combinedLFO;
    }

    void UpdateEQCoefficients(float directLFOValue)
    {
        for (int i = 0; i < eqBands.Length; i++)
        {
            var b = eqBands[i];
            float mf = GetModValue(ModDestination.ParaEQ1_Freq + i * 3);
            float mb = GetModValue(ModDestination.ParaEQ1_BW + i * 3);
            float mg = GetModValue(ModDestination.ParaEQ1_Boost + i * 3);
            var m = new ParametricEQBand
            {
                Enabled = b.Enabled,
                Frequency = b.Frequency + b.Frequency_Mod_Amount * directLFOValue + mf,
                Bandwidth = b.Bandwidth + b.Bandwidth_Mod_Amount * directLFOValue + mb,
                Boost_dB = b.Boost_dB + b.Boost_Mod_Amount * directLFOValue * 24f + mg * 24f
            };
            jobEQFilters[i] = CalculateParaEQBiquad(b.Enabled, m);
        }
    }

    void UpdateFrequencyFilterCoefficients(float directLFOValue)
    {
        for (int i = 0; i < frequencyFilters.Length; i++)
        {
            var f = frequencyFilters[i];
            float mc = GetModValue(ModDestination.FreqFilt1_Cutoff + i * 2);
            float mr = GetModValue(ModDestination.FreqFilt1_Reso + i * 2);
            var m = new FrequencyFilterSettings
            {
                Enabled = f.Enabled,
                Type = f.Type,
                Cutoff = f.Cutoff + f.Cutoff_Mod_Amount * directLFOValue * 10000f + mc * 10000f,
                Resonance = f.Resonance + f.Resonance_Mod_Amount * directLFOValue * 19f + mr * 19f
            };
            jobFrequencyFilters[i] = CalculateFreqFilterBiquad(f, m);
        }
    }

    float GetModValue(ModDestination dest)
    {
        return modMatrixValues.ContainsKey(dest) ? modMatrixValues[dest] : 0f;
    }

    OscillatorSettingsStruct GetModulatedOsc(
        OscillatorSettings s, ModDestination freqDest, ModDestination ampDest)
    {
        var o = CopyOscSettings(s);
        float fM = GetModValue(freqDest);
        o.Frequency += fM * (s == lfo1 || s == lfo2 ? 20f : 2000f);
        o.Frequency = Mathf.Max(0.01f, o.Frequency);
        o.Amplitude += GetModValue(ampDest);
        o.Amplitude = Mathf.Clamp01(o.Amplitude);
        return o;
    }

    WaveshaperSettingsStruct GetModulatedWaveshaper()
    {
        var s = waveshaper;
        var ws = CopyWaveshaperSettings(s);
        ws.Drive += GetModValue(ModDestination.Waveshaper_Drive) * 100f;
        ws.Mix += GetModValue(ModDestination.Waveshaper_Mix);
        ws.Drive = Mathf.Max(1f, ws.Drive);
        ws.Mix = Mathf.Clamp01(ws.Mix);
        return ws;
    }

    void ApplyCombFilter(float[] d, int c)
    {
        if (!combFilter.Enabled) return;
        float dM = GetModValue(ModDestination.Comb_Delay) * 50f;
        float fM = GetModValue(ModDestination.Comb_Feedback);
        float mM = GetModValue(ModDestination.Comb_Mix);
        float dis = (combFilter.Delay_ms + dM) * (float)sampleRate / 1000f;
        float f = Mathf.Clamp(combFilter.Feedback + fM, -0.99f, 0.99f);
        float m = Mathf.Clamp01(combFilter.Mix + mM);

        if (dis < 1) dis = 1;
        for (int i = 0; i < d.Length; i += c)
        {
            float ri = combWriteIndex - dis * c;
            while (ri < 0) ri += combBuffer.Length;
            int i1 = (int)ri;
            int i2 = (i1 + c) % combBuffer.Length;
            float fr = ri - i1;
            float ds = Mathf.Lerp(combBuffer[i1], combBuffer[i2], fr / c);
            float ip = d[i];
            float op = ip + ds * f;
            combBuffer[combWriteIndex] = op;
            if (c > 1) combBuffer[combWriteIndex + 1] = op;
            d[i] = Mathf.Lerp(ip, op, m);
            if (c > 1) d[i + 1] = Mathf.Lerp(ip, op, m);
            combWriteIndex = (combWriteIndex + c) % combBuffer.Length;
        }
    }

    void ApplyLimiterAndMetering(float[] d)
    {
        float ac = 1 - Mathf.Exp(-2.2f / (float)sampleRate / (Limiter_Attack_ms / 1000f));
        float rc = 1 - Mathf.Exp(-2.2f / (float)sampleRate / (Limiter_Release_ms / 1000f));
        float t = Mathf.Pow(10f, Limiter_Threshold_dB / 20f);
        float p = 0f;

        for (int i = 0; i < d.Length; i++)
        {
            float et = Mathf.Abs(d[i]);
            limiterEnvelope += (et - limiterEnvelope) * (et > limiterEnvelope ? ac : rc);
            float g = (limiterEnvelope > t) ? t / limiterEnvelope : 1f;
            d[i] *= g;
            if (Mathf.Abs(d[i]) > p) p = Mathf.Abs(d[i]);
        }

        Output_Peak_dB = 20f * Mathf.Log10(p + 1e-6f);
    }

    float GenerateMonoSample(OscillatorSettingsStruct s, double phase)
    {
        float pnm = (float)(phase / (Mathf.PI * 2));
        pnm -= Mathf.Floor(pnm);
        float sn = Mathf.Sin((float)phase);
        float sq = Mathf.Sign(sn);
        float saw = 1f - 2f * pnm;
        float tri = Mathf.PingPong(2f * pnm, 1f) * 2f - 1f;
        float ramp = 2f * pnm - 1f;
        float mix = (sn * s.Sine_Mix) + (sq * s.Square_Mix) + (saw * s.Sawtooth_Mix)
                  + (tri * s.Triangle_Mix) + (ramp * s.Ramp_Mix);
        float tm = s.Sine_Mix + s.Square_Mix + s.Sawtooth_Mix + s.Triangle_Mix + s.Ramp_Mix;
        if (tm > 1e-6f) mix /= tm;
        return (mix * s.Amplitude) + s.Offset;
    }

    float ProcessOp(float i1, float i2, OperationSettings o)
    {
        float c = o.Operation == OperationSettings.CombineOperation.Add ? i1 + i2 : i1 * i2;
        float p1 = (c * o.PreOp_Multiply) + o.PreOp_Add;
        return (p1 * o.PostOp_Multiply) + o.PostOp_Add;
    }

    private OscillatorSettingsStruct CopyOscSettings(OscillatorSettings s)
    {
        return new OscillatorSettingsStruct
        {
            Sine_Mix = s.Sine_Mix,
            Square_Mix = s.Square_Mix,
            Sawtooth_Mix = s.Sawtooth_Mix,
            Triangle_Mix = s.Triangle_Mix,
            Ramp_Mix = s.Ramp_Mix,
            Wavetable_Mix = s.Wavetable_Mix,
            Frequency = s.Frequency,
            Amplitude = s.Amplitude,
            Offset = s.Offset
        };
    }

    private OperationSettingsStruct CopyOpSettings(OperationSettings s)
    {
        return new OperationSettingsStruct
        {
            op = s.Operation,
            PreOp_Multiply = s.PreOp_Multiply,
            PreOp_Add = s.PreOp_Add,
            PostOp_Multiply = s.PostOp_Multiply,
            PostOp_Add = s.PostOp_Add
        };
    }

    private WaveshaperSettingsStruct CopyWaveshaperSettings(WaveshaperSettings s)
    {
        return new WaveshaperSettingsStruct
        {
            Enabled = s.Enabled,
            Shape = s.Shape,
            Drive = s.Drive,
            Mix = s.Mix
        };
    }

    BiquadFilterStruct CalculateParaEQBiquad(bool e, ParametricEQBand m)
    {
        float f = Mathf.Clamp(m.Frequency, 20f, (float)sampleRate / 2.1f);
        float b = Mathf.Clamp(m.Bandwidth, 0.1f, 10f);
        float o = Mathf.Clamp(m.Boost_dB, -24f, 24f);
        var x = new BiquadFilterStruct { Enabled = e };
        if (Mathf.Approximately(o, 0f))
        {
            x.a0 = 1; x.a1 = x.a2 = x.b1 = x.b2 = 0;
            return x;
        }
        double w = 2 * Mathf.PI * f / sampleRate;
        double cw = Mathf.Cos((float)w);
        double sw = Mathf.Sin((float)w);
        double A = Mathf.Pow(10f, o / 40f);
        double al = sw * System.Math.Sinh(Mathf.Log(2f) / 2f * b * w / sw);
        double a0t = 1 + al / A;
        x.a0 = (1 + al * A) / a0t;
        x.a1 = (-2 * cw) / a0t;
        x.a2 = (1 - al * A) / a0t;
        x.b1 = (-2 * cw) / a0t;
        x.b2 = (1 - al / A) / a0t;
        return x;
    }

    BiquadFilterStruct CalculateFreqFilterBiquad(
        FrequencyFilterSettings s, FrequencyFilterSettings m)
    {
        var x = new BiquadFilterStruct { Enabled = s.Enabled };
        float f = Mathf.Clamp(m.Cutoff, 20f, (float)sampleRate / 2.1f);
        float q = Mathf.Max(0.707f, m.Resonance);
        double w = 2 * Mathf.PI * f / sampleRate;
        double cw = Mathf.Cos((float)w);
        double sw = Mathf.Sin((float)w);
        double al = sw / (2 * q);
        double b0 = 0, b1 = 0, b2 = 0, a0 = 0, a1 = 0, a2 = 0;
        switch (s.Type)
        {
            case FrequencyFilterSettings.FilterType.LowPass:
                b0 = (1 - cw) / 2;
                b1 = 1 - cw;
                b2 = (1 - cw) / 2;
                a0 = 1 + al;
                a1 = -2 * cw;
                a2 = 1 - al;
                break;
            case FrequencyFilterSettings.FilterType.HighPass:
                b0 = (1 + cw) / 2;
                b1 = -(1 + cw);
                b2 = (1 + cw) / 2;
                a0 = 1 + al;
                a1 = -2 * cw;
                a2 = 1 - al;
                break;
            case FrequencyFilterSettings.FilterType.BandPass:
                b0 = al;
                b1 = 0;
                b2 = -al;
                a0 = 1 + al;
                a1 = -2 * cw;
                a2 = 1 - al;
                break;
        }
        x.a0 = b0 / a0;
        x.a1 = b1 / a0;
        x.a2 = b2 / a0;
        x.b1 = a1 / a0;
        x.b2 = a2 / a0;
        return x;
    }
}