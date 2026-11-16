using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

/// <summary>
/// Fixes teleporting while holding something (T19). The teleport ray starts at the hand, so it used
/// to hit the prop or ball in that hand first and the teleport failed or landed in a strange place.
///
/// While an object is held, its colliders are moved to Held Layer (default "Ignore Raycast", unused
/// elsewhere in this project) and put back when it is let go. The teleport rays never see that
/// layer. The grab interactors (near and far) are given the layer, so you can still pass a prop
/// from one hand to the other. Physics collisions are not affected.
/// Put one in the scene (the setup menu adds it to "Input Fixes").
/// </summary>
public class HeldObjectLayer : MonoBehaviour
{
    [Tooltip("Physics layer for held objects. Must not be used by anything else in the scene.")]
    [SerializeField] private string heldLayerName = "Ignore Raycast";
    [Tooltip("Ray interactors whose GameObject name contains this are treated as teleport rays.")]
    [SerializeField] private string teleportNameContains = "Teleport";
    [SerializeField] private bool debugLogging = true;

    private int heldLayer = -1;
    private readonly List<XRBaseInteractor> grabbers = new List<XRBaseInteractor>();
    private readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
    private readonly Dictionary<IXRSelectInteractable, List<GameObject>> held = new Dictionary<IXRSelectInteractable, List<GameObject>>();

    private void Start()
    {
        heldLayer = LayerMask.NameToLayer(heldLayerName);
        if (heldLayer < 0)
        {
            Debug.LogWarning($"[HeldObjectLayer] Layer '{heldLayerName}' doesn't exist. Teleport-while-holding fix is off.", this);
            enabled = false;
            return;
        }
        int bit = 1 << heldLayer;

        foreach (XRRayInteractor ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
        {
            if (ray.name.IndexOf(teleportNameContains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            ray.raycastMask &= ~bit;
            if (debugLogging) Debug.Log($"[HeldObjectLayer] teleport ray '{ray.name}' ignores layer '{heldLayerName}'.", ray);
        }

        foreach (NearFarInteractor nf in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
        {
            if (nf.nearInteractionCaster is SphereInteractionCaster sphere) sphere.physicsLayerMask |= bit;
            if (nf.farInteractionCaster is CurveInteractionCaster curve) curve.raycastMask |= bit;
            Watch(nf);
        }
    }

    private void Watch(XRBaseInteractor interactor)
    {
        grabbers.Add(interactor);
        interactor.selectEntered.AddListener(OnSelectEntered);
        interactor.selectExited.AddListener(OnSelectExited);
    }

    private void OnDestroy()
    {
        foreach (XRBaseInteractor i in grabbers)
        {
            if (i == null) continue;
            i.selectEntered.RemoveListener(OnSelectEntered);
            i.selectExited.RemoveListener(OnSelectExited);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        IXRSelectInteractable target = args.interactableObject;
        if (target == null || held.ContainsKey(target)) return;            // already held by the other hand
        if (!(target is XRGrabInteractable)) return;                        // doors use XRSimpleInteractable

        var moved = new List<GameObject>();
        foreach (Collider c in target.transform.GetComponentsInChildren<Collider>(true))
        {
            GameObject go = c.gameObject;
            if (originalLayers.ContainsKey(go)) continue;
            originalLayers[go] = go.layer;
            go.layer = heldLayer;
            moved.Add(go);
        }
        held[target] = moved;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        IXRSelectInteractable target = args.interactableObject;
        if (target == null || !held.TryGetValue(target, out List<GameObject> moved)) return;
        if (target.isSelected) return;                                       // still in the other hand

        foreach (GameObject go in moved)
        {
            if (go != null && originalLayers.TryGetValue(go, out int layer)) go.layer = layer;
            originalLayers.Remove(go);
        }
        held.Remove(target);
    }

    private void Update()
    {
        // Objects deleted while held never send a select exit to clean up.
        if (held.Count == 0) return;
        List<IXRSelectInteractable> gone = null;
        foreach (var kv in held)
            if (kv.Key == null || (kv.Key as Object) == null) (gone ??= new List<IXRSelectInteractable>()).Add(kv.Key);
        if (gone == null) return;
        foreach (var k in gone)
        {
            foreach (GameObject go in held[k]) originalLayers.Remove(go);
            held.Remove(k);
        }
        // Remove destroyed keys left in originalLayers.
        var dead = new List<GameObject>();
        foreach (var go in originalLayers.Keys) if (go == null) dead.Add(go);
        foreach (var go in dead) originalLayers.Remove(go);
    }
}
