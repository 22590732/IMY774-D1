using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A simple button that restarts the tutorial when the player grabs it (near or with the ray).
/// Needs a Collider on the same object. An XR Simple Interactable is added automatically if missing,
/// and a small "Replay tutorial" label is created above the button and turned to face the player.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialReplayButton : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private string label = "Replay tutorial";
    [Tooltip("Label height above the button, in metres.")]
    [SerializeField] private float labelOffset = 0.06f;
    [Tooltip("Label text height, in metres.")]
    [SerializeField] private float labelTextHeight = 0.025f;
    [SerializeField] private float cooldown = 1f;

    private XRSimpleInteractable interactable;
    private Transform labelRoot;
    private float lastPress = -999f;

    private void Awake()
    {
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialManager>();

        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null) interactable = gameObject.AddComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnPressed);

        BuildLabel();
    }

    private void OnDestroy()
    {
        if (interactable != null) interactable.selectEntered.RemoveListener(OnPressed);
        if (labelRoot != null) Destroy(labelRoot.gameObject);
    }

    private void OnPressed(SelectEnterEventArgs args)
    {
        if (Time.time - lastPress < cooldown) return;
        lastPress = Time.time;
        if (tutorial != null) tutorial.RestartTutorial();
    }

    private void LateUpdate()
    {
        if (labelRoot == null) return;
        labelRoot.position = transform.position + Vector3.up * labelOffset;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 away = labelRoot.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) labelRoot.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }

    private void BuildLabel()
    {
        const float ppm = 1000f;
        var go = new GameObject($"{name} Label", typeof(RectTransform));
        labelRoot = go.transform;
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(0.3f, 0.06f) * ppm;
        rt.localScale = Vector3.one / ppm;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = labelTextHeight * ppm;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.raycastTarget = false;
    }
}
