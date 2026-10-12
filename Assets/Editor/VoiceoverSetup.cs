using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// T11: puts your recorded voiceover clips on the tutorial steps.
///
///   Tools > Ping Pong > Assign Tutorial Voiceovers
///
/// Put the files in Assets/Sound/VO, named by step number (see Claude/VO_Recording_Script.md):
///   VO_01_Welcome.wav            main line of step 1
///   VO_02_Container.wav          main line of step 2
///   VO_02_Container_Reminder.wav reminder of step 2
/// Only the number and the "_Reminder" ending matter; the word in between is just for you.
/// Clips are imported as 2D-friendly mono, decompressed on load. Missing clips are listed and the
/// step keeps its text-only behaviour, so you can record and test a few at a time.
/// </summary>
public static class VoiceoverSetup
{
    const string ScenePath = "Assets/Scenes/D1.unity";
    const string VoDir = "Assets/Sound/VO";
    static readonly Regex NamePattern = new Regex(@"^VO_(\d+)(?:_[^_]+)*?(_Reminder)?$", RegexOptions.IgnoreCase);

    [MenuItem("Tools/Ping Pong/Assign Tutorial Voiceovers")]
    public static void Run()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        if (!AssetDatabase.IsValidFolder(VoDir))
        {
            Debug.LogWarning($"[Voiceover] Put the clips in {VoDir} first.");
            return;
        }

        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null) { Debug.LogError("[Voiceover] No TutorialManager in D1."); return; }

        var main = new Dictionary<int, AudioClip>();
        var reminders = new Dictionary<int, AudioClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VoDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            var m = NamePattern.Match(file);
            if (!m.Success) { Debug.LogWarning($"[Voiceover] Ignored '{file}': name must start with VO_<number>."); continue; }

            if (AssetImporter.GetAtPath(path) is AudioImporter ai)
            {
                ai.forceToMono = true;
                ai.loadInBackground = false;
                var s = ai.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.8f;
                s.preloadAudioData = true;
                ai.defaultSampleSettings = s;
                ai.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            int step = int.Parse(m.Groups[1].Value) - 1;
            (m.Groups[2].Success ? reminders : main)[step] = clip;
        }

        var so = new SerializedObject(tm);
        var steps = so.FindProperty("steps");
        var report = new List<string>();
        for (int i = 0; i < steps.arraySize; i++)
        {
            var st = steps.GetArrayElementAtIndex(i);
            string title = st.FindPropertyRelative("title").stringValue;
            bool wantsReminder = st.FindPropertyRelative("completeWhen").enumValueIndex != 0;   // not a Timer step

            main.TryGetValue(i, out var vo);
            reminders.TryGetValue(i, out var rem);
            if (vo != null) st.FindPropertyRelative("voiceover").objectReferenceValue = vo;
            if (rem != null) st.FindPropertyRelative("reminder").objectReferenceValue = rem;

            string line = $"{i + 1:00} {title,-15} main: {(vo != null ? $"{vo.name} ({vo.length:F1} s)" : "MISSING")}";
            if (wantsReminder) line += $"   reminder: {(rem != null ? $"{rem.name} ({rem.length:F1} s)" : "MISSING")}";
            if (vo != null && vo.length > 9f) line += "   <- long: the player waits for it";
            report.Add(line);
        }
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(tm.gameObject.scene);
        EditorSceneManager.SaveScene(tm.gameObject.scene);
        Debug.Log("[Voiceover] Tutorial clips:\n" + string.Join("\n", report));
    }
}
