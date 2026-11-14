using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the controller hint images on the tutorial steps.
///
///   Tools > Ping Pong > Assign Tutorial Images
///
/// Files live in Assets/Tutorial/Hints and are matched to steps by number:
///   TUT_01_Controls.png        step 1, a still picture
///   TUT_03_Spawn_6x4.png       step 3, a flipbook sheet of 6 columns x 4 rows
/// "_<cols>x<rows>" at the end marks a flipbook (frames left to right, top to bottom, 12 fps).
/// The word in between is just for you. Steps without a file keep the text-only panel.
///
/// The hint art is rendered from Valve's Index Controller CAD (CC BY-NC-SA 4.0), see CREDITS.txt.
/// </summary>
public static class TutorialImageSetup
{
    const string ScenePath = "Assets/Scenes/D1.unity";
    const string HintDir = "Assets/Tutorial/Hints";
    const float Fps = 12f;
    static readonly Regex NamePattern = new Regex(@"^TUT_(\d+)(?:_.*?)?(?:_(\d+)x(\d+))?$", RegexOptions.IgnoreCase);

    [MenuItem("Tools/Ping Pong/Assign Tutorial Images")]
    public static void Run()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        if (!AssetDatabase.IsValidFolder(HintDir))
        {
            Debug.LogWarning($"[Tutorial Images] Put the images in {HintDir} first.");
            return;
        }

        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null) { Debug.LogError("[Tutorial Images] No TutorialManager in D1."); return; }

        var found = new Dictionary<int, (Texture2D tex, int cols, int rows)>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { HintDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            var m = NamePattern.Match(file);
            if (!m.Success) { Debug.LogWarning($"[Tutorial Images] Ignored '{file}': name must start with TUT_<number>."); continue; }

            int cols = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
            int rows = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 1;

            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.npotScale = TextureImporterNPOTScale.None;   // keep the sheet grid exact (2304 x 1536)
                ti.alphaIsTransparency = true;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;                        // the panel is read from a few metres away
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            int step = int.Parse(m.Groups[1].Value) - 1;
            found[step] = (AssetDatabase.LoadAssetAtPath<Texture2D>(path), cols, rows);
        }

        var so = new SerializedObject(tm);
        var steps = so.FindProperty("steps");
        var report = new List<string>();
        for (int i = 0; i < steps.arraySize; i++)
        {
            var st = steps.GetArrayElementAtIndex(i);
            string title = st.FindPropertyRelative("title").stringValue;
            if (found.TryGetValue(i, out var f))
            {
                st.FindPropertyRelative("image").objectReferenceValue = f.tex;
                st.FindPropertyRelative("imageColumns").intValue = f.cols;
                st.FindPropertyRelative("imageRows").intValue = f.rows;
                st.FindPropertyRelative("imageFrames").intValue = 0;
                st.FindPropertyRelative("imageFps").floatValue = Fps;
                report.Add($"{i + 1:00} {title,-15} {f.tex.name}" + (f.cols * f.rows > 1 ? $"  ({f.cols}x{f.rows} flipbook)" : ""));
            }
            else report.Add($"{i + 1:00} {title,-15} (no image)");
        }
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(tm.gameObject.scene);
        EditorSceneManager.SaveScene(tm.gameObject.scene);
        Debug.Log("[Tutorial Images] Step images:\n" + string.Join("\n", report));
    }
}
