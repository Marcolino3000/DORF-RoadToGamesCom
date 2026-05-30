using System.Collections;
using DefaultNamespace;
using UnityEngine;

public class CameraMovementFirstScene : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private float duration;
    
    [Header("References")]
    [SerializeField] private Countdown countdown;
    
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        countdown = GetComponent<Countdown>();
        Countdown.OnCountdownFinished += MoveCameraToPosition;
    }

    private void MoveCameraToPosition()
    {
        StartCoroutine(MoveCameraCoroutine());
    }
    
    private IEnumerator MoveCameraCoroutine()
    {
        if (cam == null)
        {
            Debug.LogWarning("Camera was null");
            yield break;
        }
    
        Vector3 start = cam.transform.position;
        float elapsed = 0f;
    
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cam.transform.position = Vector3.Lerp(start, targetPosition, t);
            yield return null;
        }
    
        // cam.transform.position = targetPosition;;
    }
}
