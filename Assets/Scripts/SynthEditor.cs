// Assets/Editor/SynthEditor.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(Synth))]
public class SynthEditor : Editor
{
    private List<SynthPreset> presets = new List<SynthPreset>();
    private string[] presetNames = new string[0];
    private int selectedIndex = 0;
    private const string presetsFolder = "Assets/SynthPresets";

    // Temporary fields for new‐preset metadata
    private string newPresetName  = "NewPreset";
    private string newPresetLabel = "";

    void OnEnable()
    {
        LoadPresets();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1) Draw the “Script” field (readonly) and then all other serialized fields manually,
        // so we avoid any AudioFilterGUI calls.
        var scriptProp = serializedObject.FindProperty("m_Script");
        EditorGUILayout.PropertyField(scriptProp, true);

        var prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            if (prop.name == "m_Script") continue;
            EditorGUILayout.PropertyField(prop, true);
            enterChildren = false;
        }
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preset Management", EditorStyles.boldLabel);

        // 2) Existing presets dropdown + delete
        if (presetNames.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Select Preset", selectedIndex, presetNames);
            if (EditorGUI.EndChangeCheck())
                ApplyPreset(presets[selectedIndex]);

            if (GUILayout.Button("Delete Preset"))
            {
                string path = AssetDatabase.GetAssetPath(presets[selectedIndex]);
                if (EditorUtility.DisplayDialog(
                    "Delete Preset",
                    $"Are you sure you want to delete '{presetNames[selectedIndex]}'?",
                    "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.SaveAssets();
                    LoadPresets();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"No presets found in {presetsFolder}", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 3) New preset metadata
        EditorGUILayout.LabelField("Create New Preset", EditorStyles.boldLabel);
        newPresetName  = EditorGUILayout.TextField("Preset Name", newPresetName);
        newPresetLabel = EditorGUILayout.TextField("Labels (comma-separated)", newPresetLabel);

        if (GUILayout.Button("Save Preset"))
            SavePreset();
    }

    private void LoadPresets()
    {
        presets.Clear();
        if (!AssetDatabase.IsValidFolder(presetsFolder))
            AssetDatabase.CreateFolder("Assets", "SynthPresets");

        string[] guids = AssetDatabase.FindAssets("t:SynthPreset", new[] { presetsFolder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var p = AssetDatabase.LoadAssetAtPath<SynthPreset>(path);
            if (p != null) presets.Add(p);
        }

        presets.Sort((a, b) => string.Compare(a.presetName, b.presetName, StringComparison.Ordinal));
        presetNames = new string[presets.Count];
        for (int i = 0; i < presets.Count; i++)
            presetNames[i] = $"{presets[i].presetName} [{presets[i].label}]";

        selectedIndex = Mathf.Clamp(selectedIndex, 0, presetNames.Length - 1);
    }

    private void ApplyPreset(SynthPreset preset)
    {
        var synth = (Synth)target;
        Undo.RecordObject(synth, "Apply Synth Preset");
        string json = EditorJsonUtility.ToJson(preset);
        EditorJsonUtility.FromJsonOverwrite(json, synth);
        EditorUtility.SetDirty(synth);
    }

    private void SavePreset()
    {
        var synth = (Synth)target;
        if (!AssetDatabase.IsValidFolder(presetsFolder))
            AssetDatabase.CreateFolder("Assets", "SynthPresets");

        string safeName = newPresetName.Replace(" ", "_");
        string assetPath = $"{presetsFolder}/{safeName}.asset";

        var preset = ScriptableObject.CreateInstance<SynthPreset>();
        preset.presetName = newPresetName;
        preset.label      = newPresetLabel.Trim();

        string json = EditorJsonUtility.ToJson(synth);
        EditorJsonUtility.FromJsonOverwrite(json, preset);

        AssetDatabase.CreateAsset(preset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LoadPresets();

        for (int i = 0; i < presets.Count; i++)
            if (presets[i].presetName == newPresetName)
                selectedIndex = i;

        newPresetName  = "NewPreset";
        newPresetLabel = "";
    }
}
