using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Debug tool. Add to any GameObject, press Play, then press buttons on your controllers.
/// The Console shows the exact control path Unity receives for each press, so you can compare it
/// with the binding path on an input action. Remove when finished.
/// </summary>
public class InputPathLogger : MonoBehaviour
{
    private IDisposable subscription;

    private void OnEnable()
    {
        subscription = InputSystem.onAnyButtonPress.Call(control =>
        {
            string usages = "";
            foreach (var u in control.device.usages)
                usages += u + " ";

            Debug.Log($"[InputPathLogger] Pressed: {control.path}   device: {control.device.displayName}   hand/usages: {usages}");
        });
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
