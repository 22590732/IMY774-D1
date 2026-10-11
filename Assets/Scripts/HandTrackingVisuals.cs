using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Shows tracked hand models instead of controller models (T07).
///
/// On the Valve Index, SteamVR provides full finger tracking from the controllers' sensors through the
/// OpenXR hand-tracking extension (XR_EXT_hand_tracking), read by the XR Hands package. This script
/// spawns the XR Hands sample's "Left/Right Hand Tracking" prefabs under the XR Origin's Camera Offset
/// (the tracking space their skeleton driver expects), and hides the controller models while a hand is
/// tracked. If a hand is not tracked (no hand data from the runtime, or a headset without it), that
/// hand's controller model is shown again so the player is never left without a visible hand.
///
/// Grabbing is unchanged: the interactors stay on the controllers. Only the visuals change.
/// Requires the OpenXR "Hand Tracking Subsystem" feature to be enabled for Standalone.
/// </summary>
public class HandTrackingVisuals : MonoBehaviour
{
    [SerializeField] private GameObject leftHandPrefab;
    [SerializeField] private GameObject rightHandPrefab;
    [Tooltip("Names of the controller model objects under the XR Origin.")]
    [SerializeField] private string leftControllerVisualName = "Left Controller Visual";
    [SerializeField] private string rightControllerVisualName = "Right Controller Visual";
    [Tooltip("Show a controller model again whenever its hand isn't tracked.")]
    [SerializeField] private bool fallBackToControllers = true;
    [SerializeField] private bool debugLogging = true;

    private XRHandSubsystem subsystem;
    private GameObject leftControllerVisual, rightControllerVisual;
    private bool? lastLeft, lastRight;
    private float nextSubsystemSearch;
    private static readonly List<XRHandSubsystem> Found = new List<XRHandSubsystem>();

    private void Start()
    {
        XROrigin origin = FindFirstObjectByType<XROrigin>();
        if (origin == null)
        {
            Debug.LogWarning("HandTrackingVisuals: no XR Origin in the scene.", this);
            return;
        }

        Transform trackingSpace = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
        if (leftHandPrefab != null) Instantiate(leftHandPrefab, trackingSpace, false).name = "Left Hand (tracked)";
        if (rightHandPrefab != null) Instantiate(rightHandPrefab, trackingSpace, false).name = "Right Hand (tracked)";

        foreach (Transform t in origin.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == leftControllerVisualName) leftControllerVisual = t.gameObject;
            else if (t.name == rightControllerVisualName) rightControllerVisual = t.gameObject;
        }

        if (debugLogging && (leftControllerVisual == null || rightControllerVisual == null))
            Debug.LogWarning($"HandTrackingVisuals: couldn't find '{leftControllerVisualName}' and/or '{rightControllerVisualName}' under the XR Origin.", this);
    }

    private void Update()
    {
        if ((subsystem == null || !subsystem.running) && Time.unscaledTime >= nextSubsystemSearch)
        {
            nextSubsystemSearch = Time.unscaledTime + 1f;
            subsystem = null;
            SubsystemManager.GetSubsystems(Found);
            foreach (XRHandSubsystem s in Found)
                if (s.running) { subsystem = s; break; }
        }

        bool leftTracked = subsystem != null && subsystem.running && subsystem.leftHand.isTracked;
        bool rightTracked = subsystem != null && subsystem.running && subsystem.rightHand.isTracked;

        SetControllerVisible(leftControllerVisual, fallBackToControllers && !leftTracked);
        SetControllerVisible(rightControllerVisual, fallBackToControllers && !rightTracked);

        if (debugLogging && (leftTracked != lastLeft || rightTracked != lastRight))
        {
            Debug.Log($"HandTrackingVisuals: hand subsystem {(subsystem != null ? "running" : "NOT running")}, " +
                      $"left hand {(leftTracked ? "tracked" : "not tracked")}, right hand {(rightTracked ? "tracked" : "not tracked")}.", this);
            lastLeft = leftTracked;
            lastRight = rightTracked;
        }
    }

    private static void SetControllerVisible(GameObject visual, bool visible)
    {
        if (visual != null && visual.activeSelf != visible) visual.SetActive(visible);
    }
}
