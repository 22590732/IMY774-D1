using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// UX update (2026-11-14): prop menu on B, delete moved to the trigger.
///
///   Tools > Ping Pong > Set Up Prop Menu (UX)
///
/// Safe to re-run. It
///  - creates (or refreshes) the "Prop Menu" object in D1 with a PropMenu component,
///  - fills its prop list from the cupboard's PropSpawners only (the trick-shot props, not the
///    ball container and not the T16 room furniture grabbables),
///  - gives it the XRI input actions so teleport is switched off while the menu is open,
///  - rewrites tutorial steps 5 (prop menu) and 7 (delete with the trigger) and clears their old
///    voiceover clips, which describe the old controls. Record the new lines over the same file names
///    and run Assign Tutorial Voiceovers again (see Claude/VO_Recording_Script.md).
/// The delete action itself was rebound in XRI Default Input Actions (Delete = trigger click).
/// </summary>
public static class PropMenuSetup
{
    const string ScenePath = "Assets/Scenes/D1.unity";
    const string InputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/XRI Default Input Actions.inputactions";
    // Index of TutorialTrigger values (see TutorialManager.cs).
    const int TriggerPropDeleted = 6;
    const int TriggerMenuItemTaken = 7;

    [MenuItem("Tools/Ping Pong/Set Up Prop Menu (UX)")]
    public static void Run()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Scene scene = SceneManager.GetActiveScene();
        var log = new List<string>();

        // ---- prop list
        var prefabs = new List<GameObject>();
        foreach (PropSpawner sp in Object.FindObjectsByType<PropSpawner>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(sp);
            if (so.FindProperty("unlockGateWhenGrabbed").boolValue || so.FindProperty("stopAfterFirstGrab").boolValue)
                continue;   // the ball container
            var prefab = so.FindProperty("propPrefab").objectReferenceValue as GameObject;
            if (prefab != null && !prefabs.Contains(prefab)) prefabs.Add(prefab);
        }
        prefabs.Sort((a, b) => a.name == "Cup" ? -1 : b.name == "Cup" ? 1 : string.CompareOrdinal(a.name, b.name));

        // ---- menu object
        PropMenu menu = Object.FindAnyObjectByType<PropMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            var go = new GameObject("Prop Menu");
            SceneManager.MoveGameObjectToScene(go, scene);
            menu = go.AddComponent<PropMenu>();
            log.Add("created 'Prop Menu'");
        }
        var mso = new SerializedObject(menu);
        var list = mso.FindProperty("prefabs");
        list.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        mso.FindProperty("inputActions").objectReferenceValue = actions;
        if (actions == null) log.Add("XRI input actions not found: teleport stays on while the menu is open");
        mso.ApplyModifiedProperties();
        log.Add($"menu props ({prefabs.Count}): " + string.Join(", ", prefabs.ConvertAll(p => p.name)));

        // ---- tutorial steps 5 and 7
        var tm = Object.FindAnyObjectByType<TutorialManager>(FindObjectsInactive.Include);
        if (tm != null)
        {
            var tso = new SerializedObject(tm);
            var steps = tso.FindProperty("steps");
            SetStep(steps, 4, "Prop menu",
                "Press B to open the prop menu. Push the joystick left or right to browse, then grab the prop you want.",
                "Press B, then grab a prop from the menu.", TriggerMenuItemTaken, log);
            SetStep(steps, 6, "Delete",
                "Made a mess? Pull the trigger all the way and point the laser at a prop to delete it.",
                "Pull the trigger and point at a prop.", TriggerPropDeleted, log);
            tso.ApplyModifiedProperties();
        }
        else log.Add("no TutorialManager found");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Prop Menu] " + string.Join("\n", log));
    }

    static void SetStep(SerializedProperty steps, int i, string title, string text, string reminder, int trigger, List<string> log)
    {
        if (i >= steps.arraySize) { log.Add($"step {i + 1} missing"); return; }
        var st = steps.GetArrayElementAtIndex(i);
        string old = st.FindPropertyRelative("title").stringValue;
        st.FindPropertyRelative("title").stringValue = title;
        st.FindPropertyRelative("instruction").stringValue = text;
        st.FindPropertyRelative("reminderText").stringValue = reminder;
        st.FindPropertyRelative("completeWhen").enumValueIndex = trigger;
        // The old recordings describe the old controls.
        st.FindPropertyRelative("voiceover").objectReferenceValue = null;
        st.FindPropertyRelative("reminder").objectReferenceValue = null;
        log.Add($"step {i + 1}: '{old}' -> '{title}' (voiceover cleared until re-recorded)");
    }
}
