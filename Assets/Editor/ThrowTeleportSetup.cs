using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// T19: throwing + teleport while holding.
///
///   Tools > Ping Pong > Set Up Throwing + Teleport (T19)
///
/// Safe to re-run. Adds ThrowAssist to the PingPongBall prefab (it switches XRI's own throw off at
/// runtime, so removing the component brings the old throw back), and adds HeldObjectLayer to the
/// "Input Fixes" object in D1.
/// </summary>
public static class ThrowTeleportSetup
{
    const string ScenePath = "Assets/Scenes/D1.unity";
    const string BallPath = "Assets/Prefabs/PingPongBall.prefab";

    [MenuItem("Tools/Ping Pong/Set Up Throwing + Teleport (T19)")]
    public static void Run()
    {
        // Ball prefab
        GameObject root = PrefabUtility.LoadPrefabContents(BallPath);
        bool added = false;
        if (root.GetComponent<ThrowAssist>() == null) { root.AddComponent<ThrowAssist>(); added = true; }
        PrefabUtility.SaveAsPrefabAsset(root, BallPath);
        PrefabUtility.UnloadPrefabContents(root);

        // Scene
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Scene scene = SceneManager.GetActiveScene();
        HeldObjectLayer layer = Object.FindAnyObjectByType<HeldObjectLayer>(FindObjectsInactive.Include);
        if (layer == null)
        {
            IndexGripSelect fixes = Object.FindAnyObjectByType<IndexGripSelect>(FindObjectsInactive.Include);
            GameObject host = fixes != null ? fixes.gameObject : new GameObject("Input Fixes");
            if (fixes == null) SceneManager.MoveGameObjectToScene(host, scene);
            layer = host.AddComponent<HeldObjectLayer>();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[T19] ThrowAssist {(added ? "added to" : "already on")} PingPongBall; HeldObjectLayer on '{layer.gameObject.name}'. " +
                  "Throw a few balls and watch the [ThrowAssist] lines in the Console while tuning.");
    }
}
