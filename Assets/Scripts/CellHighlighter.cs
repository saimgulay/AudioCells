// Assets/Scripts/CellHighlighter.cs

using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(EColiGenomeLogger))]
public class CellHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    [Tooltip("Material to apply to the currently sampled (locked-on) agent.")]
    public Material targetMaterial;

    private EColiGenomeLogger _logger;

    // Keep track of the currently highlighted agent…
    private EColiAgent _currentAgent;

    // …and remember each agent's original material so we can restore it later.
    private Dictionary<EColiAgent, Material> _originalMaterials = new Dictionary<EColiAgent, Material>();

    void Awake()
    {
        _logger = GetComponent<EColiGenomeLogger>();
    }

    void Update()
    {
        var genome = _logger.currentGenome;
        if (genome == null)
        {
            // Nothing locked or logger paused → restore any highlighted
            RestoreCurrent();
            return;
        }

        // Find which agent corresponds to that genome
        var nextAgent = FindAgentByGenome(genome);

        // If it’s the same as before, no change
        if (nextAgent == _currentAgent) return;

        // Otherwise, restore the old one, then highlight the new one
        RestoreCurrent();
        if (nextAgent != null)
            HighlightAgent(nextAgent);
    }

    private EColiAgent FindAgentByGenome(EColiGenome genome)
    {
        foreach (var agent in EColiAgent.Agents)
            if (agent != null && agent.genome == genome)
                return agent;
        return null;
    }

    private void HighlightAgent(EColiAgent agent)
    {
        if (targetMaterial == null)
        {
            Debug.LogWarning("[CellHighlighter] No targetMaterial assigned!");
            return;
        }

        var rend = agent.GetComponent<Renderer>();
        if (rend == null) return;

        // Cache this agent’s original material if we haven’t already
        if (!_originalMaterials.ContainsKey(agent))
            _originalMaterials[agent] = rend.sharedMaterial;

        // Assign the highlight
        rend.sharedMaterial = targetMaterial;
        _currentAgent = agent;
    }

    private void RestoreCurrent()
    {
        if (_currentAgent == null) return;

        // Look up and restore this agent’s original
        if (_originalMaterials.TryGetValue(_currentAgent, out var mat))
        {
            var rend = _currentAgent.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = mat;

            // We could remove it if you only want to ever toggle once:
            // _originalMaterials.Remove(_currentAgent);
        }

        _currentAgent = null;
    }

    void OnDisable()
    {
        // If this script is disabled or destroyed, put every
        // cached agent back to its original material
        foreach (var kv in _originalMaterials)
        {
            if (kv.Key != null)
            {
                var rend = kv.Key.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = kv.Value;
            }
        }
        _originalMaterials.Clear();
        _currentAgent = null;
    }
}
