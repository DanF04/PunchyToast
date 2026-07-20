using UnityEngine;
using DG.Tweening;

public class MouthController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource; // Added reference to check if playing
    [SerializeField] private AudioVisualizer visualizer;
    [SerializeField] private Transform mouthBone;
    [SerializeField] private Transform bobbingBone;

    [Header("Logic Toggle")]
    [SerializeField] private bool useSilenceThreshold = true;

    [Header("Entrance/Exit Settings")]
    [SerializeField] private float popUpDistance = 2.0f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float silenceThresholdTime = 2.0f;

    [Header("Audio Settings")]
    [SerializeField] private int bandIndex = 0;
    [SerializeField] private float noiseThreshold = 0.15f;
    [SerializeField] private float sensitivity = 1.0f;
    [Range(1f, 20f)][SerializeField] private float interpolationSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private Vector3 closedRotation;
    [SerializeField] private Vector3 openRotation;
    [SerializeField] private Vector3 bobOffset = new Vector3(0, 0.1f, 0);

    private Vector3 initialPos;
    private Vector3 initialBobPosition;
    private float smoothedIntensity;
    private float silenceTimer;
    private bool isVisible = false;

    void Start()
    {
        initialPos = transform.position;
        initialBobPosition = (bobbingBone != null) ? bobbingBone.localPosition : Vector3.zero;
        transform.position = initialPos + Vector3.down * popUpDistance;
    }

    void Update()
    {
        if (visualizer == null) return;

        float rawIntensity = visualizer.BandBuffer[bandIndex] * sensitivity;
        float targetIntensity = Mathf.Clamp01((rawIntensity - noiseThreshold) / (1f - noiseThreshold));

        // Logic: Show Up
        if (targetIntensity > 0)
        {
            silenceTimer = 0;
            if (!isVisible) ShowPuppet();
        }
        // Logic: Leave
        else
        {
            bool shouldExit = false;

            if (useSilenceThreshold)
            {
                silenceTimer += Time.deltaTime;
                if (silenceTimer >= silenceThresholdTime) shouldExit = true;
            }
            else
            {
                // Leave only when AudioSource stops playing
                if (audioSource != null && !audioSource.isPlaying) shouldExit = true;
            }

            if (isVisible && shouldExit) HidePuppet();
        }

        // Apply animations
        if (isVisible)
        {
            smoothedIntensity = Mathf.Lerp(smoothedIntensity, targetIntensity, Time.deltaTime * interpolationSpeed);

            if (mouthBone != null)
                mouthBone.localEulerAngles = Vector3.Lerp(closedRotation, openRotation, smoothedIntensity);

            if (bobbingBone != null)
                bobbingBone.localPosition = initialBobPosition + (bobOffset * smoothedIntensity);
        }
    }

    void ShowPuppet()
    {
        isVisible = true;
        transform.DOKill();
        transform.DOMove(initialPos, duration).SetEase(Ease.OutBack);
    }

    void HidePuppet()
    {
        isVisible = false;
        transform.DOKill();
        transform.DOMove(initialPos + Vector3.down * popUpDistance, duration).SetEase(Ease.InBack);
    }
}