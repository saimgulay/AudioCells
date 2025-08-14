// Assets/Editor/SynthChordPlayerEditor.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(SynthChordPlayer))]
public class SynthChordPlayerEditor : Editor
{
    private const string PresetsFolder = "Assets/SynthChordPresets";

    private List<SynthChordPreset> presets = new List<SynthChordPreset>();
    private string[] presetNames = new string[0];
    private int selectedIndex = 0;

    // Yeni preset metadata
    private string newPresetName = "NewPreset";
    private string newPresetLabel = "";

    private void OnEnable()
    {
        LoadPresets();
    }

    public override void OnInspectorGUI()
    {
        // 1) Orijinal alanları çiz
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preset Management", EditorStyles.boldLabel);

        // 2) Varolan presetler
        if (presetNames.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Select Preset", selectedIndex, presetNames);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreset(presets[selectedIndex]);
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Delete Preset"))
            {
                if (EditorUtility.DisplayDialog(
                    "Delete Preset",
                    $"Are you sure you want to delete '{presetNames[selectedIndex]}'?",
                    "Delete", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(presets[selectedIndex]));
                    AssetDatabase.SaveAssets();
                    LoadPresets();
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"No presets found in {PresetsFolder}", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Create New Preset", EditorStyles.boldLabel);

        // 3) Yeni preset oluşturma alanları
        newPresetName  = EditorGUILayout.TextField("Preset Name", newPresetName);
        newPresetLabel = EditorGUILayout.TextField("Label", newPresetLabel);

        if (GUILayout.Button("Save Preset"))
        {
            SavePreset();
        }
    }

    private void LoadPresets()
    {
        presets.Clear();

        // Klasör yoksa oluştur
        if (!AssetDatabase.IsValidFolder(PresetsFolder))
            AssetDatabase.CreateFolder("Assets", "SynthChordPresets");

        // Tüm SynthChordPreset asset'lerini tara
        string[] guids = AssetDatabase.FindAssets("t:SynthChordPreset", new[] { PresetsFolder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var p = AssetDatabase.LoadAssetAtPath<SynthChordPreset>(path);
            if (p != null) presets.Add(p);
        }

        // İsim sırasına göre sırala
        presets.Sort((a, b) => string.Compare(a.presetName, b.presetName, System.StringComparison.Ordinal));

        // Popup için dizi oluştur
        presetNames = new string[presets.Count];
        for (int i = 0; i < presets.Count; i++)
            presetNames[i] = $"{presets[i].presetName} [{presets[i].label}]";

        selectedIndex = Mathf.Clamp(selectedIndex, 0, presets.Count - 1);
    }

    private void ApplyPreset(SynthChordPreset preset)
    {
        var player = (SynthChordPlayer)target;
        Undo.RecordObject(player, "Apply SynthChordPlayer Preset");

        // JSON tek adımda kopyalıyor, tüm alanları kapsar
        string json = EditorJsonUtility.ToJson(preset);
        EditorJsonUtility.FromJsonOverwrite(json, player);
    }

    private void SavePreset()
    {
        var player = (SynthChordPlayer)target;

        // Klasör yoksa oluştur
        if (!AssetDatabase.IsValidFolder(PresetsFolder))
            AssetDatabase.CreateFolder("Assets", "SynthChordPresets");

        string safeName = newPresetName.Replace(" ", "_");
        string assetPath = $"{PresetsFolder}/{safeName}.asset";

        // Yeni preset asset'i oluştur
        var preset = ScriptableObject.CreateInstance<SynthChordPreset>();
        preset.presetName       = newPresetName;
        preset.label            = newPresetLabel.Trim();

        // Tüm tetrachord ve diğer parametreleri manuel kopyala
        preset.tetrachord1       = player.tetrachord1;
        preset.tetrachord2       = player.tetrachord2;
        preset.tetrachord3       = player.tetrachord3;
        preset.tetrachord4       = player.tetrachord4;
        preset.tetrachord5       = player.tetrachord5;
        preset.lastChordMultiplier = player.lastChordMultiplier;

        preset.chordMasterGain   = player.chordMasterGain;
        preset.sustainDuration   = player.sustainDuration;
        preset.interChordPause   = player.interChordPause;
        preset.restDuration      = player.restDuration;

        preset.enableLowPass     = player.enableLowPass;
        preset.maxCutoff         = player.maximumCutoffFrequency;
        preset.cutoffMultiplier  = player.cutoffFrequencyMultiplier;

        preset.attackTime        = player.attackTime;
        preset.decayTime         = player.decayTime;
        preset.sustainLevel      = player.sustainLevel;
        preset.releaseTime       = player.releaseTime;

        preset.baseFrequency     = player.baseFrequency;

        // Asset olarak kaydet
        AssetDatabase.CreateAsset(preset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Listeyi güncelle ve yeni preset'i seç
        LoadPresets();
        for (int i = 0; i < presets.Count; i++)
        {
            if (presets[i] == preset)
            {
                selectedIndex = i;
                break;
            }
        }
    }
}
