using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// T14 one-click audio set-up:  Tools > Ping Pong > Set Up Audio (T14)
///
///   1. Import settings for Assets/Sound/Surfaces, Assets/Sound/Music.
///   2. Assets/Sound/SurfaceAudioLibrary.asset: one entry per surface with its Surf_*_n.wav clips.
///      Re-running refreshes the clips but keeps any volume/pitch numbers you tuned.
///   3. PingPongBall prefab: BallCollisionAudio gets the library.
///   4. Prop prefabs: SurfaceMaterial (by name) + PropImpactAudio.
///   5. D1: SurfaceMaterial on every scene collider whose name (or a parent's) matches a rule below,
///      DoorAudio on the hinged doors, MusicPlayer replacing the old music AudioSource.
///   Unmatched colliders are listed in the Console so you can tag them by hand.
///
/// Safe to run again. EnvironmentBuilder (T15/T16) runs it too, so regenerated props keep their audio.
/// </summary>
public static class AudioSetup
{
    const string ScenePath = "Assets/Scenes/D1.unity";
    const string SurfDir = "Assets/Sound/Surfaces";
    const string MusicDir = "Assets/Sound/Music";
    const string LibraryPath = "Assets/Sound/SurfaceAudioLibrary.asset";
    const string BallPrefab = "Assets/Prefabs/PingPongBall.prefab";
    const string ImpactPrefab = "Assets/Prefabs/ImpactAudio.prefab";
    const string PropPrefabDir = "Assets/Prefabs/Props";

    // name prefix -> surface. The collider's own name is tried first, then its parents'.
    static readonly (string prefix, Surface surface)[] Rules =
    {
        // house shell (T01) and doors (T08/T09)
        ("House_Floor_G", Surface.Stone), ("House_Floor_1", Surface.Wood), ("House_Stairs", Surface.Wood),
        ("House_Walls", Surface.Plaster), ("House_Roof", Surface.Plaster),
        ("House_Glass", Surface.Glass), ("House_Rails", Surface.Glass),
        ("Door_", Surface.Wood),
        // original furniture
        ("Counter_DrawerFront", Surface.Wood), ("Kitchen_Counter", Surface.Stone), ("KitchenCounter", Surface.Stone),
        ("Props_Cupboard", Surface.Wood), ("Cupboard", Surface.Wood), ("Table", Surface.Wood),
        // T16 furniture
        ("Fur_Fridge", Surface.Metal), ("Fur_Hob", Surface.Glass), ("Fur_Oven", Surface.Metal), ("Fur_Hood", Surface.Metal),
        ("Fur_UpperCabinet", Surface.Wood), ("Fur_Island", Surface.Stone), ("Fur_Stool", Surface.Wood),
        ("Fur_Dining", Surface.Wood), ("Fur_Sofa", Surface.Fabric), ("Fur_CoffeeTable", Surface.Wood),
        ("Fur_TVUnit", Surface.Wood), ("Fur_FloorLamp", Surface.Metal), ("Fur_Plant", Surface.Ceramic),
        ("Fur_BedsideTable", Surface.Wood), ("Fur_Bed", Surface.Fabric), ("Fur_Wardrobe", Surface.Wood),
        ("Fur_Desk", Surface.Wood), ("Fur_StandingMirror", Surface.Glass), ("Fur_Vanity", Surface.Ceramic),
        ("Fur_Toilet", Surface.Ceramic), ("Fur_Shower", Surface.Ceramic), ("Fur_TowelRail", Surface.Metal),
    };

    // prop prefab name -> (surface, pitch). Heavier props a little lower.
    static readonly Dictionary<string, (Surface s, float pitch)> PropRules = new Dictionary<string, (Surface, float)>
    {
        ["Cup"] = (Surface.Plastic, 1.1f), ["Pan"] = (Surface.Metal, 0.85f), ["Pot"] = (Surface.Metal, 0.75f),
        ["ToyTrack"] = (Surface.Plastic, 1.0f), ["WoodBoard"] = (Surface.Wood, 1.05f),
        ["BallContainer"] = (Surface.Plastic, 0.85f), ["Kettle"] = (Surface.Metal, 0.8f),
        ["Toaster"] = (Surface.Metal, 0.7f), ["FruitBowl"] = (Surface.Ceramic, 0.85f),
        ["Book_A"] = (Surface.Wood, 1.2f), ["Book_B"] = (Surface.Wood, 1.2f), ["Pillow"] = (Surface.Fabric, 0.9f),
    };

    // surface -> default (layerVolume, bouncePitch, bounceVolume)
    static readonly Dictionary<Surface, (float layer, float pitch, float vol)> Defaults = new Dictionary<Surface, (float, float, float)>
    {
        [Surface.Wood] = (0.70f, 0.97f, 1.00f), [Surface.Metal] = (0.55f, 1.03f, 0.90f),
        [Surface.Plastic] = (0.60f, 1.05f, 0.90f), [Surface.Glass] = (0.50f, 1.08f, 0.85f),
        [Surface.Ceramic] = (0.55f, 1.04f, 0.90f), [Surface.Stone] = (0.65f, 1.06f, 1.00f),
        [Surface.Fabric] = (0.80f, 0.80f, 0.25f), [Surface.Plaster] = (0.60f, 0.95f, 0.90f),
    };

    /// <summary>True once the audio set-up has been run (the library asset exists).</summary>
    public static bool IsSetUp => AssetDatabase.LoadAssetAtPath<SurfaceAudioLibrary>(LibraryPath) != null;

    [MenuItem("Tools/Ping Pong/Set Up Audio (T14)")]
    public static void Run() => Apply(true);

    /// <summary>Does everything. saveScene = false when another tool saves the scene itself.</summary>
    public static void Apply(bool saveScene)
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        ConfigureClips(SurfDir, false);
        ConfigureClips(MusicDir, true);
        var lib = BuildLibrary();
        var impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefab)?.GetComponent<TemporaryAudio>();
        if (impact == null) Debug.LogWarning($"[AudioSetup] {ImpactPrefab} with TemporaryAudio not found.");

        SetupBall(lib);
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropPrefabDir }))
            SetupPropPrefab(AssetDatabase.GUIDToAssetPath(guid), lib, impact);
        TagScene();
        SetupDoors();
        SetupMusic();

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene) EditorSceneManager.SaveScene(scene);
        Debug.Log("[AudioSetup] Done.");
    }

    // ------------------------------------------------------------------ clips + library
    static void ConfigureClips(string dir, bool music)
    {
        if (!AssetDatabase.IsValidFolder(dir)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) continue;
            ai.forceToMono = !music;
            ai.loadInBackground = music;
            var s = ai.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            s.quality = 0.7f;
            s.preloadAudioData = !music;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }

    static SurfaceAudioLibrary BuildLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SurfaceAudioLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<SurfaceAudioLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { SurfDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null).OrderBy(c => c.name).ToList();

        foreach (Surface s in System.Enum.GetValues(typeof(Surface)))
        {
            if (s == Surface.Default) continue;
            var entry = lib.entries.FirstOrDefault(e => e.surface == s);
            if (entry == null)
            {
                var d = Defaults[s];
                entry = new SurfaceAudioLibrary.Entry { surface = s, layerVolume = d.layer, bouncePitch = d.pitch, bounceVolume = d.vol };
                lib.entries.Add(entry);
            }
            entry.clips = clips.Where(c => c.name.StartsWith($"Surf_{s}_")).ToArray();
            if (entry.clips.Length == 0) Debug.LogWarning($"[AudioSetup] No Surf_{s}_*.wav clips in {SurfDir}.");
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    // ------------------------------------------------------------------ prefabs
    static void SetupBall(SurfaceAudioLibrary lib)
    {
        var root = PrefabUtility.LoadPrefabContents(BallPrefab);
        try
        {
            var bca = root.GetComponentInChildren<BallCollisionAudio>(true);
            if (bca == null) { Debug.LogWarning("[AudioSetup] PingPongBall has no BallCollisionAudio."); return; }
            var so = new SerializedObject(bca);
            so.FindProperty("surfaces").objectReferenceValue = lib;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BallPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void SetupPropPrefab(string path, SurfaceAudioLibrary lib, TemporaryAudio impact)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!PropRules.TryGetValue(name, out var rule))
        {
            Debug.Log($"[AudioSetup] No surface rule for prop prefab '{name}', skipped.");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<Rigidbody>() == null) return;
            var sm = root.GetComponent<SurfaceMaterial>();
            if (sm == null) sm = root.AddComponent<SurfaceMaterial>();
            sm.surface = rule.s;
            var pia = root.GetComponent<PropImpactAudio>();
            if (pia == null) pia = root.AddComponent<PropImpactAudio>();
            var so = new SerializedObject(pia);
            so.FindProperty("surfaces").objectReferenceValue = lib;
            so.FindProperty("impactAudioPrefab").objectReferenceValue = impact;
            so.FindProperty("basePitch").floatValue = rule.pitch;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Adds the audio components to a prop instance before it is saved as a prefab.</summary>
    public static void ApplyToNewProp(GameObject root, string prefabName)
    {
        var lib = AssetDatabase.LoadAssetAtPath<SurfaceAudioLibrary>(LibraryPath);
        var impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefab)?.GetComponent<TemporaryAudio>();
        if (lib == null || !PropRules.TryGetValue(prefabName, out var rule)) return;
        var sm = root.GetComponent<SurfaceMaterial>();
        if (sm == null) sm = root.AddComponent<SurfaceMaterial>();
        sm.surface = rule.s;
        var pia = root.GetComponent<PropImpactAudio>();
        if (pia == null) pia = root.AddComponent<PropImpactAudio>();
        var so = new SerializedObject(pia);
        so.FindProperty("surfaces").objectReferenceValue = lib;
        so.FindProperty("impactAudioPrefab").objectReferenceValue = impact;
        so.FindProperty("basePitch").floatValue = rule.pitch;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ scene
    static void TagScene()
    {
        int tagged = 0;
        var unmatched = new SortedSet<string>();
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (col.isTrigger) continue;
            if (col.GetComponentInParent<XRGrabInteractable>(true) != null) continue;   // props: from their prefabs
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue; // balls etc.
            if (col.name.StartsWith("Safety_")) continue;                                  // inside the slabs
            if (col.GetComponentInParent<SurfaceMaterial>(true) != null) continue;          // already tagged

            Surface? found = null;
            for (var t = col.transform; t != null && found == null; t = t.parent)
                foreach (var r in Rules)
                    if (t.name.StartsWith(r.prefix)) { found = r.surface; break; }

            if (found == null) { unmatched.Add(Path(col.transform)); continue; }
            var sm = Undo.AddComponent<SurfaceMaterial>(col.gameObject);
            sm.surface = found.Value;
            tagged++;
        }
        Debug.Log($"[AudioSetup] Tagged {tagged} colliders with a SurfaceMaterial.");
        if (unmatched.Count > 0)
            Debug.Log("[AudioSetup] Colliders without a surface (ball bounce only). Add a SurfaceMaterial by hand if needed:\n  " +
                      string.Join("\n  ", unmatched));
    }

    static void SetupDoors()
    {
        var creak = AssetDatabase.LoadAssetAtPath<AudioClip>(SurfDir + "/Door_Creak_Loop.wav");
        var latch = AssetDatabase.LoadAssetAtPath<AudioClip>(SurfDir + "/Door_Latch.wav");
        foreach (var door in Object.FindObjectsByType<HingedDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var da = door.GetComponent<DoorAudio>();
            if (da == null) da = Undo.AddComponent<DoorAudio>(door.gameObject);
            var so = new SerializedObject(da);
            so.FindProperty("creakLoop").objectReferenceValue = creak;
            so.FindProperty("latchClip").objectReferenceValue = latch;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void SetupMusic()
    {
        var tm = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm == null) { Debug.LogWarning("[AudioSetup] No TutorialManager in the scene; music left as it was."); return; }
        var so = new SerializedObject(tm);
        var srcProp = so.FindProperty("musicSource");
        var playerProp = so.FindProperty("musicPlayer");
        var oldSource = srcProp.objectReferenceValue as AudioSource;

        MusicPlayer player = playerProp.objectReferenceValue as MusicPlayer;
        if (player == null)
        {
            var host = oldSource != null ? oldSource.gameObject : tm.gameObject;
            player = host.GetComponent<MusicPlayer>();
            if (player == null) player = Undo.AddComponent<MusicPlayer>(host);
        }

        // Tracks: everything in Assets/Sound/Music, plus the clip the old source played.
        var tracks = new List<AudioClip>();
        if (AssetDatabase.IsValidFolder(MusicDir))
            tracks.AddRange(AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))));
        if (oldSource != null && oldSource.clip != null && !tracks.Contains(oldSource.clip)) tracks.Add(oldSource.clip);

        var pso = new SerializedObject(player);
        var tp = pso.FindProperty("tracks");
        var existing = Enumerable.Range(0, tp.arraySize).Select(i => tp.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip);
        tracks = existing.Concat(tracks).Where(c => c != null).Distinct().ToList();
        tp.arraySize = tracks.Count;
        for (int i = 0; i < tracks.Count; i++) tp.GetArrayElementAtIndex(i).objectReferenceValue = tracks[i];
        if (oldSource != null && oldSource.enabled)
            pso.FindProperty("volume").floatValue = Mathf.Clamp(oldSource.volume, 0.02f, 1f);
        pso.ApplyModifiedPropertiesWithoutUndo();

        if (oldSource != null)
        {
            Undo.RecordObject(oldSource, "Disable old music source");
            oldSource.playOnAwake = false;
            oldSource.enabled = false;
        }
        playerProp.objectReferenceValue = player;
        srcProp.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[AudioSetup] MusicPlayer on '{player.gameObject.name}' with {tracks.Count} track(s). " +
                  $"Drop more tracks into {MusicDir} and run this again.");
    }

    static string Path(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Insert(0, t.name);
        return string.Join("/", parts);
    }
}
