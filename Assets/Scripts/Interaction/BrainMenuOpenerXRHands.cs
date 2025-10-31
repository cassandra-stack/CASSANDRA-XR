using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class BrainMenuOpenerXRHands : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Référence PREFAB (asset Project OU instance scène).")]
    public GameObject menuPrefab;

    [Tooltip("Vitesse d'apparition du menu vers la pose cible (exponentiel).")]
    public float appearLerp = 14f;

    [Tooltip("Durée approximative de l'anim d'apparition.")]
    public float appearAnimDuration = 0.18f;

    [Header("Gesture")]
    public float doublePinchWindow = 0.30f;
    [Range(0.01f, 0.1f)] public float pinchThreshold = 0.03f;

    [Header("Spawn près de la CAMÉRA (et non de la main)")]
    [Tooltip("Distance en avant de la caméra.")]
    public float distanceFromCamera = 0.6f;

    [Tooltip("Décalage vertical par rapport à la caméra (+ = au-dessus).")]
    public float upOffsetNearCamera = -0.05f;

    [Tooltip("Décalage latéral par rapport à la caméra (+ = côté droit de l'utilisateur).")]
    public float sideOffsetNearCamera = 0.18f;

    [Tooltip("Le menu regarde la caméra (recommandé).")]
    public bool faceCamera = true;

    [Tooltip("Aplatis l'orientation sur l'horizontale pour éviter les inclinaisons extrêmes.")]
    public bool flattenOnY = true;

    [Header("Lock while menu is open")]
    [Tooltip("Scripts pinchs / grabs du monde à désactiver pendant que le menu est ouvert.")]
    public MonoBehaviour[] pinchBehavioursToDisable;

    [Header("Debug")]
    public bool debugLogs = true;

    public static bool MenuActive { get; private set; } // propriété auto corrigée

    XRHandSubsystem handSubsystem;
    GameObject menuInstance;      // instance runtime unique
    CanvasGroup menuCanvasGroup;  // pour cacher/montrer sans perdre l'état

    float lastPinchTimeLeft = -999f, lastPinchTimeRight = -999f;
    bool leftPrevPinch, rightPrevPinch;

    Coroutine appearRoutine;

    // On garde quelle main a ouvert le menu pour choisir le côté (gauche/droite de la caméra)
    bool lastWasLeftHand = true;

    void Start()
    {
        // Récupère le subsystem mains XR Hands
        var subs = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count > 0)
            handSubsystem = subs[0];
    }

    void Update()
    {
        if (handSubsystem == null)
            return;

        CheckHand(handSubsystem.leftHand,  true);
        CheckHand(handSubsystem.rightHand, false);
    }

    void CheckHand(XRHand hand, bool isLeft)
    {
        if (!hand.isTracked)
        {
            SetPrev(isLeft, false);
            return;
        }

        if (!TryPose(hand, XRHandJointID.IndexTip, out var indexTipPose) ||
            !TryPose(hand, XRHandJointID.ThumbTip, out var thumbTipPose))
        {
            SetPrev(isLeft, false);
            return;
        }

        bool pinching = Vector3.Distance(indexTipPose.position, thumbTipPose.position) < pinchThreshold;
        bool wasPinching = GetPrev(isLeft);

        if (!wasPinching && pinching)
        {
            float now = Time.time;
            float last = isLeft ? lastPinchTimeLeft : lastPinchTimeRight;

            // Détection du double pinch
            if (now - last <= doublePinchWindow)
            {
                lastWasLeftHand = isLeft;
                ToggleMenuAtCamera();
            }

            if (isLeft)
                lastPinchTimeLeft = now;
            else
                lastPinchTimeRight = now;
        }

        SetPrev(isLeft, pinching);
    }

    void EnsureMenuInstance()
    {
        if (menuInstance != null)
            return;

        if (menuPrefab == null)
        {
            Debug.LogError("[BrainMenuOpenerXRHands] menuPrefab n'est pas assigné.");
            return;
        }

        // Ici on suppose que tu fais référencer l'instance de scène.
        // Si tu veux instancier un prefab asset : menuInstance = Instantiate(menuPrefab);
        menuInstance = menuPrefab;

        if (!menuInstance.activeSelf)
            menuInstance.SetActive(true);

        menuCanvasGroup = menuInstance.GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null)
            menuCanvasGroup = menuInstance.AddComponent<CanvasGroup>();

        // Démarrage caché
        menuCanvasGroup.alpha = 0f;
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        // Lien VolumeDVR + sync initiale
        var linker = menuInstance.GetComponentInChildren<BrainMenuToggleToVolumeDVR>(true);
        if (linker != null)
        {
            VolumeDVR dvr = Object.FindFirstObjectByType<VolumeDVR>();
            if (dvr != null)
            {
                linker.volumeDVR = dvr;
                linker.SyncAll();
            }
            else
            {
                Debug.LogWarning("[BrainMenuOpenerXRHands] Aucun VolumeDVR trouvé dans la scène pour lier le menu.");
            }
        }
        else
        {
            Debug.LogWarning("[BrainMenuOpenerXRHands] Pas de BrainMenuToggleToVolumeDVR sur le menuInstance.");
        }
    }

    // === CHANGEMENT PRINCIPAL : spawn par rapport à la caméra ===
    void ToggleMenuAtCamera()
    {
        if (MenuActive)
        {
            CloseMenu();
            return;
        }

        EnsureMenuInstance();
        if (menuInstance == null) return;

        Camera cam = Camera.main;
        if (!cam)
        {
            Debug.LogError("[BrainMenu] Aucune Camera.main trouvée.");
            return;
        }

        Pose spawnPose = ComputeMenuPoseNearCamera(
            cam,
            distanceFromCamera,
            upOffsetNearCamera,
            sideOffsetNearCamera,
            lastWasLeftHand,
            faceCamera,
            flattenOnY
        );

        if (appearRoutine != null)
        {
            StopCoroutine(appearRoutine);
            appearRoutine = null;
        }

        appearRoutine = StartCoroutine(AnimateAndShow(menuInstance.transform, spawnPose));

        MenuActive = true;
        LockWorldInteractions(true);

        if (debugLogs) Debug.Log("[BrainMenu] OPEN (near camera)");
    }

    IEnumerator AnimateAndShow(Transform menuTr, Pose targetPose)
    {
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 1f;
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
        }

        Vector3 endPos = targetPose.position;
        Quaternion endRot = targetPose.rotation;

        // petit offset arrière juste pour l'effet d'anim
        Vector3 startPos = endPos - (endRot * Vector3.forward * 0.03f);
        Quaternion startRot = endRot;

        menuTr.SetPositionAndRotation(startPos, startRot);

        float elapsed = 0f;
        while (elapsed < appearAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Exp(-appearLerp * elapsed);

            menuTr.position = Vector3.Lerp(startPos, endPos, t);
            menuTr.rotation = Quaternion.Slerp(startRot, endRot, t);

            yield return null;
        }

        menuTr.SetPositionAndRotation(endPos, endRot);
        appearRoutine = null;
    }

    public void CloseMenu()
    {
        if (menuInstance && menuCanvasGroup)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        MenuActive = false;
        LockWorldInteractions(false);

        if (debugLogs) Debug.Log("[BrainMenu] CLOSE (hidden)");
    }

    void LockWorldInteractions(bool enableMenu)
    {
        if (pinchBehavioursToDisable != null)
        {
            foreach (var b in pinchBehavioursToDisable)
            {
                if (b)
                    b.enabled = !enableMenu;
            }
        }
    }

    // --- helpers ---
    bool TryPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        var j = hand.GetJoint(id);
        if (j.TryGetPose(out pose))
            return true;

        pose = default;
        return false;
    }

    // NOUVELLE POSE : près de la caméra
    static Pose ComputeMenuPoseNearCamera(
        Camera cam,
        float forwardDist,
        float up,
        float side,
        bool openedWithLeftHand,
        bool faceCam,
        bool flattenY
    )
    {
        Vector3 camFwd = cam.transform.forward;
        Vector3 camUp  = cam.transform.up;
        Vector3 camRight = cam.transform.right;

        if (flattenY)
        {
            camFwd = Vector3.ProjectOnPlane(camFwd, Vector3.up).normalized;
            if (camFwd.sqrMagnitude < 1e-4f) camFwd = cam.transform.forward;
            camRight = Vector3.Cross(Vector3.up, camFwd).normalized;
            camUp = Vector3.up;
        }

        float actualSide = openedWithLeftHand ? -side : side;

        Vector3 pos = cam.transform.position
                    + camFwd * Mathf.Max(0.05f, forwardDist)
                    + camUp * up
                    + camRight * actualSide;

        Quaternion rot;
        if (faceCam)
        {
            // Le menu fait face à l'utilisateur (vers la caméra)
            Vector3 toCam = (cam.transform.position - pos).normalized;
            if (flattenY) toCam = Vector3.ProjectOnPlane(toCam, Vector3.up).normalized;
            rot = Quaternion.LookRotation(-toCam, Vector3.up);
        }
        else
        {
            // Aligné avec l'orientation utilisateur
            rot = Quaternion.LookRotation(camFwd, camUp);
        }

        return new Pose(pos, rot);
    }

    bool GetPrev(bool isLeft) => isLeft ? leftPrevPinch : rightPrevPinch;
    void SetPrev(bool isLeft, bool v)
    {
        if (isLeft) leftPrevPinch = v;
        else rightPrevPinch = v;
    }
}
