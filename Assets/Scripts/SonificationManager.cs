using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Manages all agent-based and environment-based sonification.
/// This script is the SINGLE SOURCE OF TRUTH for all audio decisions.
/// NEW: Implements interpolated volume normalization for smooth fade-ins on spawn
/// and smooth cross-fades when the number of audible agents changes.
/// </summary>
public class SonificationManager : MonoBehaviour
{
    [Header("Sonification Modes")]
    [Tooltip("Enable sonification based on a agent states.")]
    public bool agentBasedSonification = true;
    [Tooltip("Enable sonification from objects tagged 'EnvironmentField'.")]
    public bool environmentBasedSonification = false;

    [Header("Agent-Based Settings")]
    [Tooltip("Chance (0–1) for a running agent to become audible if a slot is available.")]
    [Range(0f, 1f)]
    public float listenProbability = 0.1f;
    [Tooltip("Maximum number of simultaneously active agent synths.")]
    public int maxActiveSynths = 5;
    [Tooltip("How many seconds it takes for a dead agent's sound to fade out.")]
    public float deathFadeOutDuration = 3.0f;
    
    // YENİ: Ses seviyesi geçişlerinin ne kadar yumuşak/hızlı olacağını belirler.
    [Tooltip("Controls how quickly volumes interpolate to their new target levels. Higher is faster.")]
    public float volumeTransitionSpeed = 1.0f;

    private readonly List<AudioSource> _liveAudibleSources = new List<AudioSource>();
    private readonly Dictionary<AudioSource, (float initialVolume, float timeLeft)> _fadingOutSources = new Dictionary<AudioSource, (float, float)>();

    void Update()
    {
        ProcessFadingAgents();

        if (agentBasedSonification)
        {
            ProcessAgentStates();
        }
        else
        {
            DisableAllAgentSynths();
        }

        if (environmentBasedSonification)
        {
            ProcessEnvironmentSonification();
        }
    }

    void LateUpdate()
    {
        var sourcesToNormalize = _liveAudibleSources.Where(s => s != null && s.enabled && s.isPlaying).ToList();
        int totalActiveCount = sourcesToNormalize.Count;

        if (totalActiveCount > 0)
        {
            // Her bir canlı kaynak için olması gereken hedef ses seviyesini hesapla.
            float targetVolume = 1.0f / totalActiveCount;
            
            // DEĞİŞTİRİLDİ: Sesi anında eşitlemek yerine, hedefe doğru yumuşakça hareket ettir.
            // Bu tek değişiklik hem fade-in hem de yumuşak geçişi sağlar.
            foreach (var source in sourcesToNormalize)
            {
                if (source != null && source.enabled && !_fadingOutSources.ContainsKey(source))
                {
                    source.volume = Mathf.MoveTowards(source.volume, targetVolume, volumeTransitionSpeed * Time.deltaTime);
                }
            }
        }
    }

    private void ProcessFadingAgents()
    {
        if (_fadingOutSources.Count == 0) return;

        var sourcesToRemove = new List<AudioSource>();
        var fadingKeys = _fadingOutSources.Keys.ToList();

        foreach (var src in fadingKeys)
        {
            if (src == null)
            {
                sourcesToRemove.Add(src);
                continue;
            }

            var fadeData = _fadingOutSources[src];
            fadeData.timeLeft -= Time.deltaTime;

            if (fadeData.timeLeft <= 0)
            {
                src.volume = 0;
                SetComponentEnabled(src, false);
                sourcesToRemove.Add(src);
            }
            else
            {
                src.volume = Mathf.Lerp(0f, fadeData.initialVolume, fadeData.timeLeft / deathFadeOutDuration);
                _fadingOutSources[src] = fadeData;
            }
        }

        foreach (var src in sourcesToRemove)
        {
            _fadingOutSources.Remove(src);
        }
    }

    private void ProcessAgentStates()
    {
        _liveAudibleSources.Clear();
        var potentialCandidates = new List<EColiAgent>();

        if (EColiAgent.Agents == null) return;

        foreach (var agent in EColiAgent.Agents)
        {
            if (agent == null) continue;
            var src = agent.GetComponentInChildren<AudioSource>(true);
            if (src == null) continue;

            switch (agent.state)
            {
                case EColiAgent.State.Dead:
                    if (src.enabled && !_fadingOutSources.ContainsKey(src))
                    {
                        _fadingOutSources.Add(src, (src.volume, deathFadeOutDuration));
                    }
                    // Ölü hücre, canlı ve sesli listesinden çıkarılır.
                    if (_liveAudibleSources.Contains(src))
                    {
                        _liveAudibleSources.Remove(src);
                    }
                    break;

                case EColiAgent.State.Dormant:
                    if (src.enabled)
                    {
                        SetComponentEnabled(src, false);
                    }
                    if (_liveAudibleSources.Contains(src))
                    {
                        _liveAudibleSources.Remove(src);
                    }
                    break;

                case EColiAgent.State.Run:
                case EColiAgent.State.Tumble:
                    if (src.enabled)
                    {
                        if (!_fadingOutSources.ContainsKey(src))
                        {
                            _liveAudibleSources.Add(src);
                        }
                    }
                    else
                    {
                        potentialCandidates.Add(agent);
                    }
                    break;
            }
        }

        int liveAudibleCount = _liveAudibleSources.Count;

        while (liveAudibleCount > maxActiveSynths)
        {
            int indexToSilence = Random.Range(0, liveAudibleCount);
            SetComponentEnabled(_liveAudibleSources[indexToSilence], false);
            _liveAudibleSources.RemoveAt(indexToSilence);
            liveAudibleCount--;
        }

        int slotsToFill = maxActiveSynths - liveAudibleCount;
        if (slotsToFill > 0)
        {
            Shuffle(potentialCandidates);
            foreach (var candidate in potentialCandidates)
            {
                if (_liveAudibleSources.Count >= maxActiveSynths) break;
                if (Random.value < listenProbability)
                {
                    var src = candidate.GetComponentInChildren<AudioSource>(true);
                    if (src != null)
                    {
                        SetComponentEnabled(src, true);
                        _liveAudibleSources.Add(src);
                    }
                }
            }
        }
    }

    private void SetComponentEnabled(AudioSource src, bool isAudible)
    {
        if (src == null) return;
        var synth = src.GetComponent<Synth>();
        if (synth == null) return;

        if (synth.enabled == isAudible) return;

        synth.enabled = isAudible;
        src.enabled = isAudible;

        if (isAudible)
        {
            // Sesi açarken ses seviyesini 0'dan başlat. LateUpdate onu yumuşakça yükseltecek.
            src.volume = 0.0f;
            if (!src.isPlaying)
            {
                src.Play();
            }
        }
    }

    private void DisableAllAgentSynths()
    {
        if (EColiAgent.Agents == null) return;
        foreach (var agent in EColiAgent.Agents)
        {
            if (agent != null)
            {
                var src = agent.GetComponentInChildren<AudioSource>(true);
                if (src != null) SetComponentEnabled(src, false);
            }
        }
        _liveAudibleSources.Clear();
        _fadingOutSources.Clear();
    }

    private void ProcessEnvironmentSonification()
    {
        GameObject[] environmentFields = GameObject.FindGameObjectsWithTag("EnvironmentField");
        foreach (var field in environmentFields)
        {
            var synth = field.GetComponent<MainSynth>();
            var src = field.GetComponent<AudioSource>();
            if (synth != null && src != null)
            {
                if (synth.enabled != environmentBasedSonification)
                    synth.enabled = environmentBasedSonification;

                if (src.enabled != environmentBasedSonification)
                    src.enabled = environmentBasedSonification;

                if (environmentBasedSonification && !src.isPlaying)
                {
                    src.Play();
                }
            }
        }
    }

    private void Shuffle<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}