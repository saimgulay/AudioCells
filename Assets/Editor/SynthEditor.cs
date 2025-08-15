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

    // Yeni preset metadata için geçici alanlar
    private string newPresetName = "NewPreset";
    private string newPresetLabel = "";

    void OnEnable()
    {
        LoadPresets();
    }

    public override void OnInspectorGUI()
    {
        // 1) Synth bileşenini çiz
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preset Management", EditorStyles.boldLabel);

        // 2) Varolan preset’ler
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

        // 3) Yeni preset oluşturma
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
            var preset = AssetDatabase.LoadAssetAtPath<SynthPreset>(path);
            if (preset != null)
                presets.Add(preset);
        }

        // presetName’e göre sırala
        presets.Sort((a, b) => string.Compare(a.presetName, b.presetName, StringComparison.Ordinal));

        // Dropdown’a basılacak metinleri oluştur
        presetNames = new string[presets.Count];
        for (int i = 0; i < presets.Count; i++)
        {
            // Label, virgülle ayrılmış haliyle zaten hazır
            var p = presets[i];
            presetNames[i] = $"{p.presetName} [{p.label}]";
        }

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

        // Asset adı olarak boşluksuz presetName kullan
        string safeName = newPresetName.Replace(" ", "_");
        string assetPath = $"{presetsFolder}/{safeName}.asset";

        // ScriptableObject oluştur ve alanları kopyala
        var preset = ScriptableObject.CreateInstance<SynthPreset>();
        preset.presetName = newPresetName;
        preset.label = newPresetLabel.Trim();  // virgülle ayrılmış etiketler

        // Synth’ten JSON ile al ver
        string json = EditorJsonUtility.ToJson(synth);
        EditorJsonUtility.FromJsonOverwrite(json, preset);

        AssetDatabase.CreateAsset(preset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LoadPresets();

        // Yeni ekleneni seçili yap
        for (int i = 0; i < presets.Count; i++)
            if (presets[i].presetName == newPresetName)
                selectedIndex = i;
    }
}
