using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PinchToScroll : MonoBehaviour
{
    [Header("XR Inputs")]
    public XRRayInteractor ray;               // Ray interactor de la main
    public InputActionProperty pinchValue;    // <XRHand>/pinchStrength [0..1] ou un bouton Press

    [Header("Tuning")]
    public float scrollSpeed = 1.2f;          // Vitesse de scroll
    public float deadZone = 0.1f;             // Pour ignorer les micro-pinch
    public bool invert = true;                // Inverser le sens

    readonly List<RaycastResult> _results = new List<RaycastResult>();
    PointerEventData _ped;

    void Awake()
    {
        _ped = new PointerEventData(EventSystem.current);
        pinchValue.action.Enable();
    }

    void Update()
    {
        // 1) Raycast UI depuis l’XR Ray Interactor
        if (!ray || !ray.TryGetCurrent3DRaycastHit(out var hit))
            return;

        // Position écran requise par l’EventSystem; on la simule via la cam principale
        var cam = Camera.main;
        if (!cam) return;

        Vector2 screenPos = cam.WorldToScreenPoint(hit.point);
        _ped.position = screenPos;

        _results.Clear();
        EventSystem.current.RaycastAll(_ped, _results);

        // 2) Cherche un ScrollRect sous le pointeur
        ScrollRect scroll = null;
        foreach (var r in _results)
        {
            scroll = r.gameObject.GetComponentInParent<ScrollRect>();
            if (scroll) break;
        }
        if (!scroll) return;

        // 3) Lis le pinch et scrolle
        float pinch = pinchValue.action.ReadValue<float>(); // 0..1
        if (pinch < deadZone) return;

        float dir = invert ? -1f : 1f;
        float delta = dir * pinch * scrollSpeed * Time.deltaTime;

        // verticalNormalizedPosition: 1 en haut, 0 en bas
        float v = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta);
        scroll.verticalNormalizedPosition = v;
    }
}
