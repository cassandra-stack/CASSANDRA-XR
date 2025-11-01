using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ReportGrabAndSwipe : MonoBehaviour
{
    [Header("References")]
    public XRGrabInteractable grab;      // XR Grab on this object
    public PageDisplay pageDisplay;      // Your script with NextPage()/PrevPage()
    public Transform swipePlane;         // Usually the Page transform

    [Header("Swipe")]
    public float swipeThreshold = 0.12f; // meters along local X to flip
    public float deadZone = 0.02f;
    public float cooldown = 0.20f;

    private IXRSelectInteractor currentInteractor;
    private IXRInteractable currentInteractable;
    private Vector3 startLocalPos;
    private float lastFlip = -999f;

    void Reset()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (swipePlane == null) swipePlane = transform;
    }

    void OnEnable()
    {
        if (!grab) grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        currentInteractor  = args.interactorObject;
        currentInteractable = args.interactableObject; // <-- keep this

        startLocalPos = ToLocalOnPlane(GetInteractorWorldPos());
    }

    void OnRelease(SelectExitEventArgs args)
    {
        currentInteractor = null;
        currentInteractable = null;
    }

    void Update()
    {
        if (currentInteractor == null || pageDisplay == null || swipePlane == null) return;

        Vector3 local = ToLocalOnPlane(GetInteractorWorldPos());
        float dx = local.x - startLocalPos.x;

        if (Mathf.Abs(dx) < deadZone) return;
        if (Time.time - lastFlip < cooldown) return;

        if (dx >= swipeThreshold)
        {
            pageDisplay.PrevPage();
            lastFlip = Time.time;
            startLocalPos = local;
        }
        else if (dx <= -swipeThreshold)
        {
            pageDisplay.NextPage();
            lastFlip = Time.time;
            startLocalPos = local;
        }
    }

    // ---- helpers ----
    Vector3 ToLocalOnPlane(Vector3 worldPos) => swipePlane.InverseTransformPoint(worldPos);

    Vector3 GetInteractorWorldPos()
    {
        if (currentInteractor == null) return transform.position;

        // NEW API: requires the interactable
        Transform attach = currentInteractor.GetAttachTransform(
            currentInteractable ?? (grab as IXRInteractable)
        );

        if (attach != null) return attach.position;

        // Fallbacks for unusual interactors
        if (currentInteractor is Component c) return c.transform.position;
        return transform.position;
    }
}
