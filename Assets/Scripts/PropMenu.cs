using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Prop menu: press B on either controller to open a curved shelf of props in front of you.
/// Push either thumbstick left or right to scroll. Grab any prop on the shelf (near or with the ray)
/// to take a full-size copy; the shelf refills that spot and stays open. Press B again to close.
///
/// Teleport is switched off while the menu is open, so the sticks only scroll.
/// The shelf follows your view lazily, like the tutorial panel used to.
///
/// Shelf items are real prop instances, shrunk to fit and frozen in place (Rigidbody constraints),
/// so XR Grab Interactable works on them unchanged. When one is grabbed it is unfrozen, unparented
/// and given its normal size; a fresh one pops in after Refill Delay.
/// Set up by Tools > Ping Pong > Set Up Prop Menu (UX).
/// </summary>
public class PropMenu : MonoBehaviour
{
    [Header("Props")]
    [Tooltip("Prefabs on the shelf, in order. Filled by the setup menu from the cupboard spawners and the T16 grabbables.")]
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();

    [Header("Input")]
    [SerializeField] private string leftToggleBinding = "<XRController>{LeftHand}/{SecondaryButton}";
    [SerializeField] private string rightToggleBinding = "<XRController>{RightHand}/{SecondaryButton}";
    [SerializeField] private string leftStickBinding = "<XRController>{LeftHand}/{Primary2DAxis}";
    [SerializeField] private string rightStickBinding = "<XRController>{RightHand}/{Primary2DAxis}";
    [Tooltip("Optional: the XRI input actions. Its teleport actions are switched off while the menu is open.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string[] teleportActions = { "XRI Left Locomotion/Teleport Mode", "XRI Right Locomotion/Teleport Mode" };

    [Header("Layout")]
    [Tooltip("Distance from your head to the shelf.")]
    [SerializeField] private float distance = 0.55f;
    [Tooltip("Height of the shelf relative to your head.")]
    [SerializeField] private float heightOffset = -0.35f;
    [Tooltip("How many props are visible at once (odd number).")]
    [SerializeField] private int visibleCount = 5;
    [Tooltip("Angle between neighbouring props, seen from your head.")]
    [SerializeField] private float spacingDegrees = 20f;
    [Tooltip("Longest side of a prop on the shelf, in metres.")]
    [SerializeField] private float itemSize = 0.14f;
    [Tooltip("The prop in the middle is shown this much bigger.")]
    [SerializeField] private float centreScale = 1.35f;
    [Tooltip("Degrees per second the middle prop turns.")]
    [SerializeField] private float spinSpeed = 35f;

    [Header("Follow")]
    [Tooltip("The shelf moves back in front of you when you turn more than this.")]
    [SerializeField] private float followAngle = 35f;
    [Tooltip("...or move further than this from where it was placed.")]
    [SerializeField] private float followDistance = 0.4f;
    [SerializeField] private float followSpeed = 4f;

    [Header("Scrolling")]
    [SerializeField] private float stickThreshold = 0.6f;
    [SerializeField] private float stickRelease = 0.3f;
    [Tooltip("Hold the stick to keep scrolling: first repeat after this delay...")]
    [SerializeField] private float repeatDelay = 0.45f;
    [Tooltip("...then one step every this many seconds.")]
    [SerializeField] private float repeatInterval = 0.2f;
    [SerializeField] private float scrollSmoothing = 14f;

    [Header("Refill")]
    [SerializeField] private float refillDelay = 0.35f;
    [SerializeField] private float popInTime = 0.2f;

    [Header("Feedback (optional)")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip scrollClip;
    [Range(0f, 1f)] [SerializeField] private float clipVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float hapticAmplitude = 0.2f;
    [SerializeField] private float hapticDuration = 0.04f;

    /// <summary>Raised when the menu opens.</summary>
    public static event Action Opened;
    /// <summary>Raised when a prop is taken from the shelf (used by the tutorial).</summary>
    public static event Action<GameObject> ItemTaken;

    public bool IsOpen { get; private set; }

    private const float PixelsPerMetre = 1000f;

    private class Item
    {
        public int index;
        public GameObject go;
        public XRGrabInteractable grab;
        public Rigidbody rb;
        public RigidbodyConstraints constraints;
        public RigidbodyInterpolation interpolation;
        public float born;
        public UnityEngine.Events.UnityAction<SelectEnterEventArgs> onGrab;
    }

    private Item[] items = new Item[0];
    private float[] fit = new float[0];          // display scale factor per prefab (itemSize / longest side)
    private readonly List<(int index, float time)> refills = new List<(int, float)>();
    private Transform ring;
    private TMP_Text label;
    private GameObject labelRoot;
    private AudioSource audioSource;

    private InputAction leftToggle, rightToggle, leftStick, rightStick;
    private readonly List<InputAction> disabledTeleport = new List<InputAction>();
    private HapticImpulsePlayer leftHaptics, rightHaptics;

    private int target;          // index of the prop in the middle (can be any integer; wrapped when used)
    private float scroll;        // smoothed version of target
    private int stickDir;        // -1, 0, +1 currently held
    private float nextRepeat;
    private float spin;
    private Transform head;

    // ------------------------------------------------------------------ lifecycle

    private void OnEnable()
    {
        leftToggle = MakeAction("Prop Menu Left", InputActionType.Button, leftToggleBinding);
        rightToggle = MakeAction("Prop Menu Right", InputActionType.Button, rightToggleBinding);
        leftStick = MakeAction("Prop Menu Stick Left", InputActionType.Value, leftStickBinding, "Vector2");
        rightStick = MakeAction("Prop Menu Stick Right", InputActionType.Value, rightStickBinding, "Vector2");
    }

    private void OnDisable()
    {
        if (IsOpen) Close();
        foreach (InputAction a in new[] { leftToggle, rightToggle, leftStick, rightStick }) a?.Dispose();
        leftToggle = rightToggle = leftStick = rightStick = null;
    }

    private static InputAction MakeAction(string n, InputActionType type, string binding, string layout = null)
    {
        var a = new InputAction(n, type, binding, expectedControlType: layout);
        a.Enable();
        return a;
    }

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        foreach (HapticImpulsePlayer p in FindObjectsByType<HapticImpulsePlayer>())
        {
            int hand = HandOf(p.transform);
            if (hand < 0) leftHaptics = p;
            else if (hand > 0) rightHaptics = p;
        }

        ring = new GameObject("Shelf").transform;
        ring.SetParent(transform, false);
        BuildLabel();
        ring.gameObject.SetActive(false);
        labelRoot.SetActive(false);
    }

    private void Update()
    {
        bool leftPressed = leftToggle != null && leftToggle.WasPressedThisFrame();
        bool rightPressed = rightToggle != null && rightToggle.WasPressedThisFrame();
        if (leftPressed || rightPressed)
        {
            if (IsOpen) Close(); else Open();
            Haptic(rightPressed ? 1 : -1, hapticAmplitude * 1.5f);
        }

        if (!IsOpen) return;

        UpdateScrollInput();
        UpdateRefills();
        UpdateFollow(false);
        Layout();
    }

    // ------------------------------------------------------------------ open / close

    public void Open()
    {
        if (IsOpen || prefabs.Count == 0) return;
        IsOpen = true;
        head = Camera.main != null ? Camera.main.transform : null;

        if (items.Length != prefabs.Count) BuildItems();
        for (int i = 0; i < items.Length; i++)
            if (items[i] == null || items[i].go == null) Spawn(i);

        ring.gameObject.SetActive(true);
        labelRoot.SetActive(true);
        UpdateFollow(true);
        scroll = target;
        Layout();

        disabledTeleport.Clear();
        if (inputActions != null)
            foreach (string n in teleportActions)
            {
                InputAction a = inputActions.FindAction(n);
                if (a != null && a.enabled) { a.Disable(); disabledTeleport.Add(a); }
            }

        Play(openClip);
        Opened?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        ring.gameObject.SetActive(false);
        labelRoot.SetActive(false);
        foreach (InputAction a in disabledTeleport) a.Enable();
        disabledTeleport.Clear();
        Play(closeClip);
    }

    // ------------------------------------------------------------------ items

    private void BuildItems()
    {
        foreach (Item it in items)
            if (it != null && it.go != null) Destroy(it.go);

        items = new Item[prefabs.Count];
        fit = new float[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
            fit[i] = prefabs[i] != null ? itemSize / Mathf.Max(0.01f, LongestSide(prefabs[i])) : 1f;
    }

    private void Spawn(int i)
    {
        GameObject prefab = prefabs[i];
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, ring);
        go.name = prefab.name + " (Menu)";
        var it = new Item { index = i, go = go, born = Time.time };
        it.grab = go.GetComponent<XRGrabInteractable>();
        it.rb = go.GetComponent<Rigidbody>();
        if (it.rb != null)
        {
            // Frozen instead of kinematic: XR Grab Interactable remembers isKinematic/useGravity when grabbed
            // and puts them back on release, so they must already be the normal values.
            it.constraints = it.rb.constraints;
            it.interpolation = it.rb.interpolation;
            it.rb.constraints = RigidbodyConstraints.FreezeAll;
            it.rb.interpolation = RigidbodyInterpolation.None;   // so moving the shelf doesn't jitter
            it.rb.linearVelocity = Vector3.zero;
            it.rb.angularVelocity = Vector3.zero;
        }
        if (it.grab != null)
        {
            it.onGrab = args => Take(it);
            it.grab.selectEntered.AddListener(it.onGrab);
        }
        items[i] = it;
        PlaceItem(it, Wrap(i - scroll), 0f);
    }

    private void Take(Item it)
    {
        if (items[it.index] != it) return;
        items[it.index] = null;

        GameObject go = it.go;
        go.transform.SetParent(null, true);
        go.name = prefabs[it.index].name;

        Vector3 full = prefabs[it.index].transform.localScale;
        go.transform.localScale = full;
        if (it.grab != null)
        {
            it.grab.selectEntered.RemoveListener(it.onGrab);
            it.grab.SetTargetLocalScale(full);
        }
        if (it.rb != null)
        {
            it.rb.constraints = it.constraints;
            it.rb.interpolation = it.interpolation;
        }

        // Full size is now the prop's "original" size for the resizer and impact sounds.
        ResizableProp rp = go.GetComponent<ResizableProp>();
        if (rp != null) rp.ResetBase(); else go.AddComponent<ResizableProp>();

        refills.Add((it.index, Time.time + refillDelay));
        ItemTaken?.Invoke(go);
    }

    private void UpdateRefills()
    {
        for (int r = refills.Count - 1; r >= 0; r--)
        {
            if (Time.time < refills[r].time) continue;
            int i = refills[r].index;
            refills.RemoveAt(r);
            if (items[i] == null || items[i].go == null) Spawn(i);
        }
        // Items deleted with the laser while on the shelf.
        for (int i = 0; i < items.Length; i++)
            if (items[i] != null && items[i].go == null)
            {
                items[i] = null;
                refills.Add((i, Time.time + refillDelay));
            }
    }

    // ------------------------------------------------------------------ layout

    private void Layout()
    {
        scroll = Mathf.Lerp(scroll, target, 1f - Mathf.Exp(-scrollSmoothing * Time.deltaTime));
        spin += spinSpeed * Time.deltaTime;

        for (int i = 0; i < items.Length; i++)
        {
            Item it = items[i];
            if (it == null || it.go == null) continue;
            if (it.grab != null && it.grab.isSelected) continue;   // being taken this frame
            PlaceItem(it, Wrap(i - scroll), spin);
        }

        int centre = Mod(target, Mathf.Max(1, prefabs.Count));
        if (label != null && prefabs.Count > 0 && prefabs[centre] != null)
            label.text = $"<   {Pretty(prefabs[centre].name)}   >";
    }

    // k = position relative to the middle (0 = middle, ±1 neighbours, ...).
    private void PlaceItem(Item it, float k, float spinDegrees)
    {
        float half = (Mathf.Max(1, visibleCount) - 1) * 0.5f;
        float vis = Mathf.Abs(k) <= half ? 1f : Mathf.Clamp01(1f - (Mathf.Abs(k) - half) / 0.5f);
        bool show = vis > 0.01f;
        if (it.go.activeSelf != show) it.go.SetActive(show);
        if (!show) return;

        float a = k * spacingDegrees * Mathf.Deg2Rad;
        // On an arc around the head, so every prop is the same distance away.
        it.go.transform.localPosition = new Vector3(distance * Mathf.Sin(a), 0f, distance * Mathf.Cos(a) - distance);

        float emphasis = Mathf.Lerp(centreScale, 1f, Mathf.Clamp01(Mathf.Abs(k)));
        float pop = popInTime > 0f ? Mathf.SmoothStep(0f, 1f, (Time.time - it.born) / popInTime) : 1f;
        it.go.transform.localScale = prefabs[it.index].transform.localScale * (fit[it.index] * emphasis * vis * pop);

        float turn = Mathf.Abs(k) < 0.5f ? spinDegrees : 0f;
        it.go.transform.localRotation = Quaternion.Euler(0f, k * spacingDegrees + turn, 0f) * prefabs[it.index].transform.localRotation;
    }

    private float Wrap(float k)
    {
        int n = Mathf.Max(1, items.Length);
        k %= n;
        if (k < -n * 0.5f) k += n;
        if (k >= n * 0.5f) k -= n;
        return k;
    }

    private static int Mod(int a, int n) => ((a % n) + n) % n;

    private void UpdateFollow(bool snap)
    {
        if (head == null) return;
        Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
        flat.Normalize();

        Vector3 wantedPos = head.position + flat * distance + Vector3.up * heightOffset;
        Quaternion wantedRot = Quaternion.LookRotation(flat, Vector3.up);

        if (snap)
        {
            transform.SetPositionAndRotation(wantedPos, wantedRot);
            following = false;
            return;
        }

        float angle = Quaternion.Angle(transform.rotation, wantedRot);
        float dist = Vector3.Distance(transform.position, wantedPos);
        if (!following && (angle > followAngle || dist > followDistance)) following = true;
        if (following)
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, wantedPos, t),
                                             Quaternion.Slerp(transform.rotation, wantedRot, t));
            if (angle < 2f && dist < 0.02f) following = false;
        }
    }
    private bool following;

    // ------------------------------------------------------------------ scroll input

    private void UpdateScrollInput()
    {
        float lx = leftStick != null ? leftStick.ReadValue<Vector2>().x : 0f;
        float rx = rightStick != null ? rightStick.ReadValue<Vector2>().x : 0f;
        float x = Mathf.Abs(lx) > Mathf.Abs(rx) ? lx : rx;
        int hand = Mathf.Abs(lx) > Mathf.Abs(rx) ? -1 : 1;

        int dir = 0;
        if (x > stickThreshold) dir = 1;
        else if (x < -stickThreshold) dir = -1;
        else if (Mathf.Abs(x) > stickRelease) dir = stickDir;   // hysteresis

        if (dir != 0 && dir != stickDir)
        {
            Step(dir, hand);
            nextRepeat = Time.time + repeatDelay;
        }
        else if (dir != 0 && Time.time >= nextRepeat)
        {
            Step(dir, hand);
            nextRepeat = Time.time + repeatInterval;
        }
        stickDir = dir;
    }

    private void Step(int dir, int hand)
    {
        target += dir;
        Play(scrollClip);
        Haptic(hand, hapticAmplitude);
    }

    // ------------------------------------------------------------------ helpers

    private void BuildLabel()
    {
        labelRoot = new GameObject("Shelf Label", typeof(RectTransform));
        labelRoot.transform.SetParent(transform, false);
        Canvas canvas = labelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)labelRoot.transform;
        rt.sizeDelta = new Vector2(0.5f, 0.05f) * PixelsPerMetre;
        rt.localScale = Vector3.one / PixelsPerMetre;
        rt.localPosition = new Vector3(0f, -itemSize * centreScale * 0.5f - 0.04f, 0f);

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(labelRoot.transform, false);
        var brt = (RectTransform)bg.transform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.75f);

        var txt = new GameObject("Text", typeof(RectTransform));
        txt.transform.SetParent(labelRoot.transform, false);
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = txt.AddComponent<TextMeshProUGUI>();
        t.fontSize = 30f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        label = t;
    }

    private static string Pretty(string n)
    {
        n = n.Replace('_', ' ');
        return Regex.Replace(n, "(?<=[a-z])(?=[A-Z])", " ");
    }

    // Longest side of the prefab's renderers at its own scale and rotation.
    private static float LongestSide(GameObject prefab)
    {
        bool any = false;
        Bounds b = default;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            // Prefab assets sit at the origin, so world = the prefab's own space incl. its scale.
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        if (!any) return 0.2f;
        Vector3 s = b.size;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
    }

    private static int HandOf(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) return -1;
            if (t.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        }
        return 0;
    }

    private void Haptic(int hand, float amplitude)
    {
        HapticImpulsePlayer p = hand < 0 ? leftHaptics : rightHaptics;
        if (p != null) p.SendHapticImpulse(amplitude, hapticDuration);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, clipVolume);
    }
}
