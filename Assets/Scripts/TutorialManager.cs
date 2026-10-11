using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>What the player has to do to finish a tutorial step.</summary>
public enum TutorialTrigger
{
    Timer,              // finishes on its own after the voiceover (welcome, free play)
    ContainerPickedUp,  // BallSpawnGate.Unlocked
    BallSpawned,        // HandBallSpawner.BallSpawned
    BallInCup,          // NewCupTrigger.AnyScored
    PropRefilled,       // PropSpawner.Refilled
    PropResized,        // PropResizer.ResizeFinished (size changed by more than the threshold)
    PropDeleted         // PropDeleter.PropDeleted
}

[Serializable]
public class TutorialStep
{
    [Tooltip("Short heading shown above the instruction.")]
    public string title = "Step";
    [TextArea(2, 4)] public string instruction;
    [Tooltip("Shorter nudge shown when the player hasn't done the step yet. Empty = repeat the instruction.")]
    [TextArea(1, 3)] public string reminderText;
    public AudioClip voiceover;
    public AudioClip reminder;
    public TutorialTrigger completeWhen = TutorialTrigger.Timer;
    [Tooltip("Timer steps only: seconds to wait after the voiceover ends. Without a clip the text stays up long enough to read it.")]
    public float waitSeconds = 2f;
    [Tooltip("Optional object switched on only during this step, e.g. a marker with TutorialHighlight.")]
    public GameObject highlight;
}

/// <summary>
/// Runs the tutorial (T10). Each step shows a subtitle panel, plays an optional voiceover and
/// finishes when the player actually does the action, by listening to events raised by the
/// gameplay scripts. Gameplay works the same with or without this component.
///
/// Skip: click BOTH joysticks down. Hold 1.5 s to skip the current step, 4 s to skip the tutorial.
/// The tutorial plays every time the scene starts. Call RestartTutorial() to replay it.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Header("Steps")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();
    [SerializeField] private bool playOnStart = true;
    [Tooltip("Pause after a step is completed before the next one starts.")]
    [SerializeField] private float pauseBetweenSteps = 0.75f;
    [Tooltip("Resize step: how much the size must change (0.15 = 15%) to count.")]
    [SerializeField] private float resizeThreshold = 0.15f;

    [Header("Audio")]
    [Tooltip("2D source for the voiceover. Created automatically if empty.")]
    [SerializeField] private AudioSource voiceSource;
    [Tooltip("Optional. The music is turned down while the voiceover plays.")]
    [SerializeField] private AudioSource musicSource;
    [Range(0f, 1f)] [SerializeField] private float musicDuckFactor = 0.35f;

    [Header("Reminders")]
    [SerializeField] private float reminderDelay = 15f;
    [SerializeField] private float reminderRepeat = 20f;

    [Header("Panel")]
    [Tooltip("Optional. Use your own text instead of the panel this script builds.")]
    [SerializeField] private TMP_Text textOverride;
    [SerializeField] private float panelDistance = 1.2f;
    [SerializeField] private float panelHeightOffset = -0.25f;
    [SerializeField] private float panelWidth = 0.9f;
    [SerializeField] private float panelHeight = 0.3f;
    [Tooltip("Height of the instruction text, in metres.")]
    [SerializeField] private float textHeight = 0.035f;
    [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);
    [SerializeField] private Color titleColor = new Color(1f, 0.62f, 0.25f, 1f);

    [Header("Skip (click both joysticks)")]
    [SerializeField] private float holdToSkipStep = 1.5f;
    [SerializeField] private float holdToSkipAll = 4f;
    [SerializeField] private string leftClickBinding = "<XRController>{LeftHand}/{Primary2DAxisClick}";
    [SerializeField] private string rightClickBinding = "<XRController>{RightHand}/{Primary2DAxisClick}";

    [Header("Feedback")]
    [SerializeField] private float hapticAmplitude = 0.5f;
    [SerializeField] private float hapticDuration = 0.15f;

    [Header("Events")]
    public UnityEvent TutorialFinished;

    public bool IsRunning => running;
    public int CurrentStepIndex => index;

    private const float PixelsPerMetre = 1000f;
    private const string DefaultHint = "Click both joysticks: hold to skip a step, keep holding to skip the tutorial";

    private int index = -1;
    private bool running;
    private bool stepDone;
    private Coroutine stepRoutine;
    private float nextReminderTime;

    private float baseMusicVolume = 1f;
    private InputAction leftClick, rightClick;
    private float bothHeldTime;
    private bool skippedThisHold;

    private GameObject panelRoot;
    private RectTransform panelRect;
    private TMP_Text titleText, bodyText, hintText;
    private float pulseUntil;

    // ------------------------------------------------------------------ lifecycle

    private void OnEnable()
    {
        BallSpawnGate.Unlocked += OnContainerPickedUp;
        HandBallSpawner.BallSpawned += OnBallSpawned;
        NewCupTrigger.AnyScored += OnBallInCup;
        PropSpawner.Refilled += OnPropRefilled;
        PropResizer.ResizeFinished += OnPropResized;
        PropDeleter.PropDeleted += OnPropDeleted;

        leftClick = new InputAction("Tutorial Skip Left", InputActionType.Button, leftClickBinding);
        rightClick = new InputAction("Tutorial Skip Right", InputActionType.Button, rightClickBinding);
        leftClick.Enable();
        rightClick.Enable();
    }

    private void OnDisable()
    {
        BallSpawnGate.Unlocked -= OnContainerPickedUp;
        HandBallSpawner.BallSpawned -= OnBallSpawned;
        NewCupTrigger.AnyScored -= OnBallInCup;
        PropSpawner.Refilled -= OnPropRefilled;
        PropResizer.ResizeFinished -= OnPropResized;
        PropDeleter.PropDeleted -= OnPropDeleted;

        leftClick?.Dispose();
        rightClick?.Dispose();
        leftClick = rightClick = null;
    }

    private void Start()
    {
        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
        }
        if (musicSource != null) baseMusicVolume = musicSource.volume;

        foreach (TutorialStep s in steps)
            if (s.highlight != null) s.highlight.SetActive(false);

        BuildPanel();
        SetPanelVisible(false);

        if (playOnStart) RestartTutorial();
    }

    private void Update()
    {
        UpdateSkipInput();
        UpdateMusicDuck();
        UpdatePanelPulse();
    }

    // ------------------------------------------------------------------ public control

    /// <summary>Starts the tutorial from the first step (also used by the replay button).</summary>
    public void RestartTutorial()
    {
        if (steps.Count == 0) return;
        StopCurrentStep();
        running = true;
        index = -1;
        SetPanelVisible(true);
        NextStep();
    }

    /// <summary>Finishes the current step as if the player had done it.</summary>
    public void SkipStep()
    {
        if (running) stepDone = true;
    }

    /// <summary>Ends the tutorial immediately.</summary>
    public void SkipTutorial()
    {
        if (!running) return;
        if (voiceSource != null) voiceSource.Stop();
        Finish();
    }

    // ------------------------------------------------------------------ steps

    private void NextStep()
    {
        index++;
        if (index >= steps.Count)
        {
            Finish();
            return;
        }
        stepRoutine = StartCoroutine(RunStep(steps[index]));
    }

    private IEnumerator RunStep(TutorialStep step)
    {
        stepDone = false;
        ShowStep(step, step.instruction);
        SetHighlight(step, true);
        PlayVoice(step.voiceover);
        nextReminderTime = Time.time + reminderDelay;

        // The container may already have been picked up before this step started.
        if (step.completeWhen == TutorialTrigger.ContainerPickedUp && BallSpawnGate.IsUnlocked)
            stepDone = true;

        if (step.completeWhen == TutorialTrigger.Timer)
        {
            float wait = step.voiceover != null
                ? step.voiceover.length + step.waitSeconds
                : Mathf.Max(step.waitSeconds, ReadingTime(step.instruction));
            float end = Time.time + wait;
            while (!stepDone && Time.time < end) yield return null;
            stepDone = true;
        }
        else
        {
            while (!stepDone)
            {
                if (Time.time >= nextReminderTime)
                {
                    Remind(step);
                    nextReminderTime = Time.time + reminderRepeat;
                }
                yield return null;
            }
            Haptic();
        }

        SetHighlight(step, false);
        yield return new WaitForSeconds(pauseBetweenSteps);
        stepRoutine = null;
        NextStep();
    }

    private void Complete(TutorialTrigger trigger)
    {
        if (!running || index < 0 || index >= steps.Count) return;
        if (steps[index].completeWhen == trigger) stepDone = true;
    }

    private void Finish()
    {
        StopCurrentStep();
        running = false;
        index = steps.Count;
        SetPanelVisible(false);
        TutorialFinished?.Invoke();
    }

    private void StopCurrentStep()
    {
        if (stepRoutine != null) StopCoroutine(stepRoutine);
        stepRoutine = null;
        foreach (TutorialStep s in steps)
            if (s.highlight != null) s.highlight.SetActive(false);
    }

    private void Remind(TutorialStep step)
    {
        string text = string.IsNullOrWhiteSpace(step.reminderText) ? step.instruction : step.reminderText;
        ShowStep(step, text);
        if (step.reminder != null && !voiceSource.isPlaying) PlayVoice(step.reminder);
        pulseUntil = Time.time + 1.2f;
        if (step.highlight != null) step.highlight.SetActive(true);
    }

    private static float ReadingTime(string text)
    {
        if (string.IsNullOrEmpty(text)) return 4f;
        int words = text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return Mathf.Max(4f, words / 2.5f + 1f);
    }

    // ------------------------------------------------------------------ event handlers

    private void OnContainerPickedUp() => Complete(TutorialTrigger.ContainerPickedUp);
    private void OnBallSpawned(GameObject ball) => Complete(TutorialTrigger.BallSpawned);
    private void OnBallInCup(NewCupTrigger cup) => Complete(TutorialTrigger.BallInCup);
    private void OnPropRefilled(PropSpawner spawner) => Complete(TutorialTrigger.PropRefilled);
    private void OnPropDeleted() => Complete(TutorialTrigger.PropDeleted);

    private void OnPropResized(float factor)
    {
        if (Mathf.Abs(factor - 1f) >= resizeThreshold) Complete(TutorialTrigger.PropResized);
    }

    // ------------------------------------------------------------------ skip input

    private void UpdateSkipInput()
    {
        bool both = leftClick != null && rightClick != null && leftClick.IsPressed() && rightClick.IsPressed();

        if (!both || !running)
        {
            if (bothHeldTime > 0f && hintText != null) hintText.text = DefaultHint;
            bothHeldTime = 0f;
            skippedThisHold = false;
            return;
        }

        bothHeldTime += Time.deltaTime;

        if (!skippedThisHold && bothHeldTime >= holdToSkipStep)
        {
            skippedThisHold = true;
            SkipStep();
        }

        if (hintText != null)
            hintText.text = $"Keep holding to skip the whole tutorial ({Mathf.Max(0f, holdToSkipAll - bothHeldTime):0.0} s)";

        if (bothHeldTime >= holdToSkipAll)
            SkipTutorial();
    }

    // ------------------------------------------------------------------ audio

    private void PlayVoice(AudioClip clip)
    {
        if (voiceSource == null) return;
        voiceSource.Stop();
        if (clip == null) return;
        voiceSource.clip = clip;
        voiceSource.Play();
    }

    private void UpdateMusicDuck()
    {
        if (musicSource == null || voiceSource == null) return;
        float target = voiceSource.isPlaying ? baseMusicVolume * musicDuckFactor : baseMusicVolume;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, baseMusicVolume * 2f * Time.deltaTime);
    }

    private void Haptic()
    {
        foreach (HapticImpulsePlayer player in FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None))
            player.SendHapticImpulse(hapticAmplitude, hapticDuration);
    }

    // ------------------------------------------------------------------ panel

    private void ShowStep(TutorialStep step, string text)
    {
        if (titleText != null) titleText.text = $"{step.title}   <size=70%><color=#BBBBBB>{index + 1} / {steps.Count}</color></size>";
        if (bodyText != null) bodyText.text = text;
    }

    private void SetHighlight(TutorialStep step, bool on)
    {
        if (step.highlight != null) step.highlight.SetActive(on);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null) panelRoot.SetActive(visible);
        else if (textOverride != null) textOverride.gameObject.SetActive(visible);
    }

    private void UpdatePanelPulse()
    {
        if (panelRect == null) return;
        float s = Time.time < pulseUntil ? 1f + 0.04f * Mathf.Sin(Time.time * 12f) : 1f;
        panelRect.localScale = Vector3.one * (s / PixelsPerMetre);
    }

    private void BuildPanel()
    {
        if (textOverride != null)
        {
            bodyText = textOverride;
            return;
        }

        panelRoot = new GameObject("Tutorial Panel", typeof(RectTransform));
        Canvas canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelRect = (RectTransform)panelRoot.transform;
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight) * PixelsPerMetre;
        panelRect.localScale = Vector3.one / PixelsPerMetre;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(panelRoot.transform, false);
        Stretch((RectTransform)bg.transform, 0f, 1f, 0f);
        bg.GetComponent<Image>().color = panelColor;

        float body = textHeight * PixelsPerMetre;
        titleText = MakeText("Title", 0.72f, 0.96f, body * 0.85f, titleColor, FontStyles.Bold);
        bodyText = MakeText("Instruction", 0.22f, 0.74f, body, Color.white, FontStyles.Normal);
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = body * 0.6f;
        bodyText.fontSizeMax = body;
        hintText = MakeText("Hint", 0.03f, 0.2f, body * 0.5f, new Color(0.75f, 0.75f, 0.75f, 1f), FontStyles.Italic);
        hintText.text = DefaultHint;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Transform head = cam.transform;
            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
            panelRoot.transform.position = head.position + flatForward * panelDistance + Vector3.up * panelHeightOffset;
            panelRoot.transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }

        LazyFollow follow = panelRoot.AddComponent<LazyFollow>();
        if (cam != null) follow.target = cam.transform;
        follow.targetOffset = new Vector3(0f, panelHeightOffset, panelDistance);
        follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
        follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
    }

    private TMP_Text MakeText(string objectName, float yMin, float yMax, float size, Color color, FontStyles style)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(panelRoot.transform, false);
        Stretch((RectTransform)go.transform, yMin, yMax, 0.04f * PixelsPerMetre);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt, float yMin, float yMax, float sidePadding)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(sidePadding, 0f);
        rt.offsetMax = new Vector2(-sidePadding, 0f);
    }
}
