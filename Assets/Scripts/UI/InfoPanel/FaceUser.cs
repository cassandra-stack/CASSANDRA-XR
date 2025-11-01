using UnityEngine;
using UnityEngine.XR;

public class FaceUser : MonoBehaviour
{
    [Tooltip("Caméra principale VR (souvent la main camera du XR Origin)")]
    public Transform targetCamera;

    void Start()
    {
        // Si aucune caméra n’est assignée, on prend la main camera automatiquement
        if (targetCamera == null)
            targetCamera = Camera.main?.transform;
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // Oriente le canvas vers la caméra
        Vector3 direction = transform.position - targetCamera.position;
        direction.y = 0f; // garde le canvas horizontal si tu veux éviter qu’il s’incline

        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}
