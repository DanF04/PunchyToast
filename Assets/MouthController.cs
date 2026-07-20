using UnityEngine;

public class MouthController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioVisualizer visualizer;
    [SerializeField] private Transform mouthBone;
    [SerializeField] private Transform bobbingBone;

    [Header("Settings")]
    [SerializeField] private int bandIndex = 0;
    [SerializeField] private float noiseThreshold = 0.15f;
    [SerializeField] private float sensitivity = 1.0f;
    [Range(1f, 100f)][SerializeField] private float interpolationSpeed = 10f; // NEW: Controls smoothing

    [Header("Mouth Rotation")]
    [SerializeField] private Vector3 closedRotation;
    [SerializeField] private Vector3 openRotation;

    [Header("Bobbing Movement")]
    [SerializeField] private Vector3 bobOffset = new Vector3(0, 0.1f, 0);
    private Vector3 initialBobPosition;

    private float smoothedIntensity; // The internal variable for the "glide"

    void Start()
    {
        if (bobbingBone != null)
        {
            initialBobPosition = bobbingBone.localPosition;
        }
    }

    void Update()
    {
        if (visualizer == null) return;

        // 1. Calculate target intensity
        float rawIntensity = visualizer.BandBuffer[bandIndex] * sensitivity;
        float targetIntensity = Mathf.Clamp01((rawIntensity - noiseThreshold) / (1f - noiseThreshold));

        // 2. Smooth the intensity over time (The "Jitter" Fix)
        smoothedIntensity = Mathf.Lerp(smoothedIntensity, targetIntensity, Time.deltaTime * interpolationSpeed);

        // 3. Apply smoothed movement
        if (mouthBone != null)
        {
            mouthBone.localEulerAngles = Vector3.Lerp(closedRotation, openRotation, smoothedIntensity);
        }

        if (bobbingBone != null)
        {
            bobbingBone.localPosition = initialBobPosition + (bobOffset * smoothedIntensity);
        }
    }
}