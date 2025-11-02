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
    public XRRayInteractor ray;
    public InputActionProperty pinchValue;

    [Header("Tuning")]
    public float scrollSpeed = 1.2f;
    public float deadZone = 0.1f;
    public bool invert = true;

    readonly List<RaycastResult> _results = new List<RaycastResult>();
    PointerEventData _ped;

    void Awake()
    {
        _ped = new PointerEventData(EventSystem.current);
        pinchValue.action.Enable();
    }

    void Update()
    {
        if (!ray || !ray.TryGetCurrent3DRaycastHit(out var hit))
            return;

        var cam = Camera.main;
        if (!cam) return;

        Vector2 screenPos = cam.WorldToScreenPoint(hit.point);
        _ped.position = screenPos;

        _results.Clear();
        EventSystem.current.RaycastAll(_ped, _results);

        ScrollRect scroll = null;
        foreach (var r in _results)
        {
            scroll = r.gameObject.GetComponentInParent<ScrollRect>();
            if (scroll) break;
        }
        if (!scroll) return;

        float pinch = pinchValue.action.ReadValue<float>();
        if (pinch < deadZone) return;

        float dir = invert ? -1f : 1f;
        float delta = dir * pinch * scrollSpeed * Time.deltaTime;

        float v = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta);
        scroll.verticalNormalizedPosition = v;
    }
}
