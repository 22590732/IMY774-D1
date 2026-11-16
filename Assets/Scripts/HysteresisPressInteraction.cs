using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// An Input System interaction for analog buttons that has separate press and release points
/// (hysteresis). The action becomes Performed when the value rises to Press Point and stays
/// Performed until the value drops to Release Point.
///
/// Used by IndexGripSelect for the Valve Index grip force sensor: a firm squeeze grabs, and only
/// a clearly relaxed hand lets go. The built-in Press interaction can't do this because its
/// release point is a global percentage of the press point.
///
/// In a binding's interactions string: HysteresisPress(pressPoint=0.3,releasePoint=0.1)
/// </summary>
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public class HysteresisPressInteraction : IInputInteraction
{
    [Tooltip("Value (0-1) at which the button counts as pressed.")]
    public float pressPoint = 0.3f;

    [Tooltip("Value (0-1) at which a pressed button counts as released. Keep it below Press Point.")]
    public float releasePoint = 0.1f;

    [Tooltip("Optional (T19): also release when the value drops below this fraction of its peak since the press. " +
             "0 = off. 0.5 lets go as soon as a firm squeeze is halved, which releases throws earlier.")]
    public float releaseRatio = 0f;

    public const string Name = "HysteresisPress";

    private float peak;

    static HysteresisPressInteraction()
    {
        Register();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Register()
    {
        InputSystem.RegisterInteraction<HysteresisPressInteraction>(Name);
    }

    public void Process(ref InputInteractionContext context)
    {
        float value = context.ReadValue<float>();
        float release = Mathf.Min(releasePoint, pressPoint);

        switch (context.phase)
        {
            case InputActionPhase.Waiting:
            case InputActionPhase.Canceled:
                if (value >= pressPoint)
                {
                    peak = value;
                    context.Started();
                    context.PerformedAndStayPerformed();
                }
                break;

            case InputActionPhase.Started:
                if (value >= pressPoint)
                {
                    peak = Mathf.Max(peak, value);
                    context.PerformedAndStayPerformed();
                }
                else if (value <= release)
                    context.Canceled();
                break;

            case InputActionPhase.Performed:
                peak = Mathf.Max(peak, value);
                if (value <= Mathf.Max(release, peak * releaseRatio))
                    context.Canceled();
                break;
        }
    }

    public void Reset()
    {
        peak = 0f;
    }
}
