using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

[RequireComponent(typeof(Collider))]
public class HandPinchScaleXRHands : MonoBehaviour
{
    [Header("Pinch")]
    [Range(0.01f, 0.1f)] public float pinchDistanceThreshold = 0.03f;
    public float releaseHysteresis = 0.005f;

    [Header("Scaling")]
    public float minScale = 0.1f;
    public float maxScale = 3.0f;
    [Tooltip("1 = linéaire, <1 = plus doux, >1 = plus fort")]
    public float scaleResponse = 1.0f;
    public Transform targetRoot;

    [Header("Rotation Lock")]
    public bool lockRotationWhileScaling = true;
    Quaternion lockedRotation;

    XRHandSubsystem handSubsystem;

    private Material _volumeMaterial;

    bool leftPinching, rightPinching;
    float startDist = 0f;
    Vector3 startScale;

    void Start()
    {
        targetRoot = targetRoot ? targetRoot : transform;
        _volumeMaterial = targetRoot.GetComponent<VolumeDVR>().volumeMaterial;

        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0) handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null) return;

        XRHand L = handSubsystem.leftHand;
        XRHand R = handSubsystem.rightHand;

        if (!L.isTracked || !R.isTracked)
        {
            StopScaling();
            return;
        }

        Vector3 lPinchPos, rPinchPos;
        leftPinching  = GetPinch(L,  out lPinchPos);
        rightPinching = GetPinch(R, out rPinchPos);

        if (leftPinching && rightPinching)
        {
            XRManipulationState.RotatingActive = false;
            XRManipulationState.TranslatingActive = false;

            XRManipulationState.ScalingActive = true;

            if (startDist <= 0f)
            {
                float distNow0 = Vector3.Distance(lPinchPos, rPinchPos);
                startDist  = Mathf.Max(distNow0, 1e-4f);
                startScale = targetRoot.localScale;
                if (lockRotationWhileScaling) lockedRotation = transform.rotation;
            }

            float distNow = Vector3.Distance(lPinchPos, rPinchPos);
            ApplyScale(distNow);
            return;
        }

        StopScaling();
    }

    void LateUpdate()
    {
        if (lockRotationWhileScaling && XRManipulationState.ScalingActive)
        {
            transform.rotation = lockedRotation;
        }
    }

    void ApplyScale(float distNow)
    {
        if (distNow <= 1e-6f) return;
        if (startDist <= 0f) return;

        float ratio = Mathf.Clamp(distNow / startDist, 0.01f, 100f);

        float response = Mathf.Pow(ratio, Mathf.Max(0.01f, scaleResponse));

        Vector3 newScale = startScale * response;

        // clamp min/max en respectant les proportions :
        // on clamp via un facteur global pour éviter un axe qui part trop loin.
        // On regarde l'axe X comme référence (tu peux changer pour Y ou Z si tu préfères).
        float refAxis = newScale.x;

        float clampedRef = Mathf.Clamp(refAxis, minScale, maxScale);

        if (Mathf.Abs(refAxis) > 1e-6f)
        {
            float correction = clampedRef / refAxis;
            newScale *= correction;
        }


        if (!float.IsNaN(newScale.x) && !float.IsInfinity(newScale.x) &&
            !float.IsNaN(newScale.y) && !float.IsInfinity(newScale.y) &&
            !float.IsNaN(newScale.z) && !float.IsInfinity(newScale.z))
        {
            targetRoot.localScale = newScale;
            // float baseSteps = 128f;
            // float minSteps = 32f;
            // int newStepCount = (int)Mathf.Max(minSteps, baseSteps / newScale.x); 

            // if (_volumeMaterial != null)
            // {
            //     _volumeMaterial.SetFloat("_StepCount", newStepCount);
            // }
        }

        if (_volumeMaterial != null)
        {
            // max des axes monde
            var s = targetRoot.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            scaleMax = Mathf.Max(scaleMax, 0.001f);

            _volumeMaterial.SetFloat("_ScaleCompensation", scaleMax);
            // Optionnel, si tu gardes ces champs :
            _volumeMaterial.SetFloat("_ScaleMax", scaleMax);
            _volumeMaterial.SetFloat("_DensityComp", 1.0f / scaleMax);
        }
    }

    void StopScaling()
    {
        if (XRManipulationState.ScalingActive)
        {
            // on libère le mode scale
            XRManipulationState.ScalingActive = false;
        }
        startDist = 0f;
    }

    // détecter si la main pince
    bool GetPinch(XRHand hand, out Vector3 pinchPos)
    {
        pinchPos = default;
        if (!TryGetJointPose(hand, XRHandJointID.IndexTip, out Pose i) ||
            !TryGetJointPose(hand, XRHandJointID.ThumbTip, out Pose t))
            return false;

        float d = Vector3.Distance(i.position, t.position);
        pinchPos = 0.5f * (i.position + t.position);
        return d < pinchDistanceThreshold;
    }

    bool TryGetJointPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var joint = hand.GetJoint(id);
        if (joint.TryGetPose(out pose)) return true;
        pose = default; return false;
    }
}
