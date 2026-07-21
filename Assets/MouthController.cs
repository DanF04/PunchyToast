using UnityEngine;
using DG.Tweening;

public class MouthController : MonoBehaviour
{
    public enum LookBackDirection
    {
        Left,
        Right,
        Random
    }

    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioVisualizer visualizer;
    [SerializeField] private Transform mouthBone;
    [SerializeField] private Transform bobbingBone;

    [Tooltip("The body, upper-body, or head transform used for idle movement.")]
    [SerializeField] private Transform idleBone;

    [Header("Visibility Logic")]
    [SerializeField] private bool useSilenceThreshold = true;

    [Tooltip("Keeps the puppet visible during silent sections of an audio clip.")]
    [SerializeField] private bool keepVisibleWhileAudioIsPlaying = true;

    [Header("Entrance and Exit")]
    [SerializeField] private float popUpDistance = 2f;
    [SerializeField] private float duration = 0.5f;

    [Tooltip("How long the puppet remains visible after the audio finishes.")]
    [SerializeField] private float silenceThresholdTime = 2f;

    [Header("Audio Settings")]
    [SerializeField] private int bandIndex = 0;

    [Range(0f, 0.99f)]
    [SerializeField] private float noiseThreshold = 0.15f;

    [SerializeField] private float sensitivity = 1f;

    [Range(1f, 20f)]
    [SerializeField] private float interpolationSpeed = 10f;

    [Header("Mouth Animation")]
    [SerializeField] private Vector3 closedRotation;
    [SerializeField] private Vector3 openRotation;
    [SerializeField] private Vector3 bobOffset = new Vector3(0f, 0.1f, 0f);

    [Header("Idle Activation")]
    [SerializeField] private bool useIdleAnimation = true;

    [Tooltip("Continuous silence required before idle movement starts.")]
    [SerializeField] private float idleStartDelay = 3f;

    [Tooltip("How quickly the puppet returns to its talking pose.")]
    [SerializeField] private float talkingReturnSpeed = 8f;

    [Header("Gentle Idle Movement")]
    [Tooltip("Overall speed of the normal idle.")]
    [SerializeField] private float idleSpeed = 0.45f;

    [Tooltip("Small vertical movement.")]
    [SerializeField] private float idleBobDistance = 0.018f;

    [Tooltip("Small horizontal movement.")]
    [SerializeField] private float idleSideDistance = 0.006f;

    [Tooltip("Forward and backward rotation around local X.")]
    [SerializeField] private float idleNodAngle = 0.8f;

    [Tooltip("Gentle left and right rotation around local Y.")]
    [SerializeField] private float idleYawAngle = 1.2f;

    [Tooltip("Gentle sideways lean around local Z.")]
    [SerializeField] private float idleTiltAngle = 1.5f;

    [Header("Subtle Idle Variation")]
    [SerializeField] private bool useIdleVariation = true;

    [Tooltip("Minimum time before the idle changes slightly.")]
    [SerializeField] private float variationChangeMin = 5f;

    [Tooltip("Maximum time before the idle changes slightly.")]
    [SerializeField] private float variationChangeMax = 8f;

    [Tooltip("How smoothly the idle changes.")]
    [SerializeField] private float variationSmoothSpeed = 0.6f;

    [Header("Look Back Direction")]
    [SerializeField] private bool useRandomLookBack = true;

    [SerializeField] private LookBackDirection lookBackDirection =
        LookBackDirection.Random;

    [Header("Normal Look Back")]
    [Tooltip("Angle used for the normal turn.")]
    [SerializeField] private float normalLookBackAngle = 35f;

    [SerializeField] private float normalTurnDuration = 0.3f;
    [SerializeField] private float normalHoldDuration = 0.5f;
    [SerializeField] private float normalReturnDuration = 0.35f;

    [Header("Deep Look Back")]
    [Tooltip("A larger turn that looks farther behind the puppet.")]
    [SerializeField] private float deepLookBackAngle = 80f;

    [Range(0f, 1f)]
    [Tooltip("Chance that a look-back will be the deeper version.")]
    [SerializeField] private float deepLookBackChance = 0.25f;

    [SerializeField] private float deepTurnDuration = 0.5f;
    [SerializeField] private float deepHoldDuration = 0.7f;
    [SerializeField] private float deepReturnDuration = 0.5f;

    [Header("Look Back Timing")]
    [Tooltip("Minimum idle time before another turn.")]
    [SerializeField] private float lookBackDelayMin = 4f;

    [Tooltip("Maximum idle time before another turn.")]
    [SerializeField] private float lookBackDelayMax = 9f;

    private Vector3 initialPosition;
    private Vector3 initialBobPosition;

    private Vector3 initialIdlePosition;
    private Vector3 initialIdleScale;
    private Quaternion initialIdleRotation;

    private float smoothedIntensity;

    private float exitSilenceTimer;
    private float continuousSilenceTimer;

    private float idleTimer;
    private float idleBlend;
    private float idlePhase;

    private float currentLookBackAngle;
    private float lookBackTimer;
    private float nextLookBackDelay;

    private float variationTimer;
    private float nextVariationDelay;

    private float currentIdleStrength = 1f;
    private float targetIdleStrength = 1f;

    private float currentSpeedMultiplier = 1f;
    private float targetSpeedMultiplier = 1f;

    private bool isVisible;
    private bool idleHasStarted;

    private Sequence lookBackSequence;

    private void Start()
    {
        initialPosition = transform.position;

        if (bobbingBone != null)
            initialBobPosition = bobbingBone.localPosition;

        if (idleBone == null)
            idleBone = bobbingBone;

        if (idleBone != null)
        {
            initialIdlePosition = idleBone.localPosition;
            initialIdleRotation = idleBone.localRotation;
            initialIdleScale = idleBone.localScale;
        }

        // Prevents every puppet from moving with exactly the same timing.
        idlePhase = Random.Range(0f, Mathf.PI * 2f);

        SelectNewIdleVariation();
        ScheduleNextLookBack();

        transform.position =
            initialPosition + Vector3.down * popUpDistance;
    }

    private void Update()
    {
        if (visualizer == null ||
            visualizer.BandBuffer == null ||
            bandIndex < 0 ||
            bandIndex >= visualizer.BandBuffer.Length)
        {
            return;
        }

        float rawIntensity =
            visualizer.BandBuffer[bandIndex] * sensitivity;

        float targetIntensity = Mathf.Clamp01(
            (rawIntensity - noiseThreshold) /
            Mathf.Max(0.01f, 1f - noiseThreshold)
        );

        bool isTalking = targetIntensity > 0.001f;

        HandleVisibility(isTalking);

        if (!isVisible)
            return;

        smoothedIntensity = Mathf.Lerp(
            smoothedIntensity,
            targetIntensity,
            Time.deltaTime * interpolationSpeed
        );

        UpdateIdleState(isTalking);
        ApplyMouthAnimation();
        ApplyBodyAnimation();
    }

    private void HandleVisibility(bool isTalking)
    {
        if (isTalking)
        {
            exitSilenceTimer = 0f;

            if (!isVisible)
                ShowPuppet();

            return;
        }

        if (!isVisible)
            return;

        bool audioIsPlaying =
            audioSource != null &&
            audioSource.isPlaying;

        if (keepVisibleWhileAudioIsPlaying && audioIsPlaying)
        {
            exitSilenceTimer = 0f;
            return;
        }

        if (useSilenceThreshold)
        {
            exitSilenceTimer += Time.deltaTime;

            if (exitSilenceTimer >= silenceThresholdTime)
                HidePuppet();
        }
        else
        {
            HidePuppet();
        }
    }

    private void UpdateIdleState(bool isTalking)
    {
        if (idleBone == null || !useIdleAnimation)
            return;

        if (isTalking)
        {
            continuousSilenceTimer = 0f;
            idleHasStarted = false;

            // Stops the turn tween but preserves its current angle.
            // The angle then returns smoothly instead of snapping.
            StopLookBackTween();

            idleBlend = Mathf.MoveTowards(
                idleBlend,
                0f,
                Time.deltaTime * talkingReturnSpeed
            );

            ReturnLookBackAngleToZero();

            return;
        }

        continuousSilenceTimer += Time.deltaTime;

        if (continuousSilenceTimer < idleStartDelay)
        {
            idleHasStarted = false;

            StopLookBackTween();

            idleBlend = Mathf.MoveTowards(
                idleBlend,
                0f,
                Time.deltaTime * talkingReturnSpeed
            );

            ReturnLookBackAngleToZero();

            return;
        }

        if (!idleHasStarted)
        {
            BeginIdle();
            idleHasStarted = true;
        }

        idleBlend = Mathf.MoveTowards(
            idleBlend,
            1f,
            Time.deltaTime * 2f
        );

        idleTimer += Time.deltaTime;

        UpdateIdleVariation();
        UpdateLookBack();
    }

    private void BeginIdle()
    {
        lookBackTimer = 0f;
        variationTimer = 0f;

        SelectNewIdleVariation();
        ScheduleNextLookBack();
    }

    private void ReturnLookBackAngleToZero()
    {
        currentLookBackAngle = Mathf.Lerp(
            currentLookBackAngle,
            0f,
            Time.deltaTime * talkingReturnSpeed
        );

        if (Mathf.Abs(currentLookBackAngle) < 0.01f)
            currentLookBackAngle = 0f;
    }

    private void UpdateIdleVariation()
    {
        if (useIdleVariation)
        {
            variationTimer += Time.deltaTime;

            if (variationTimer >= nextVariationDelay)
            {
                SelectNewIdleVariation();
                variationTimer = 0f;
            }
        }
        else
        {
            targetIdleStrength = 1f;
            targetSpeedMultiplier = 1f;
        }

        currentIdleStrength = Mathf.Lerp(
            currentIdleStrength,
            targetIdleStrength,
            Time.deltaTime * variationSmoothSpeed
        );

        currentSpeedMultiplier = Mathf.Lerp(
            currentSpeedMultiplier,
            targetSpeedMultiplier,
            Time.deltaTime * variationSmoothSpeed
        );
    }

    private void SelectNewIdleVariation()
    {
        float minimum = Mathf.Min(
            variationChangeMin,
            variationChangeMax
        );

        float maximum = Mathf.Max(
            variationChangeMin,
            variationChangeMax
        );

        nextVariationDelay = Random.Range(minimum, maximum);

        // Only small changes are used now.
        targetIdleStrength = Random.Range(0.8f, 1.05f);
        targetSpeedMultiplier = Random.Range(0.9f, 1.08f);
    }

    private void UpdateLookBack()
    {
        if (!useRandomLookBack ||
            lookBackSequence != null)
        {
            return;
        }

        lookBackTimer += Time.deltaTime;

        if (lookBackTimer >= nextLookBackDelay)
        {
            StartLookBack();
            lookBackTimer = 0f;
        }
    }

    private void ApplyMouthAnimation()
    {
        if (mouthBone != null)
        {
            Quaternion closed =
                Quaternion.Euler(closedRotation);

            Quaternion open =
                Quaternion.Euler(openRotation);

            mouthBone.localRotation = Quaternion.Slerp(
                closed,
                open,
                smoothedIntensity
            );
        }

        if (bobbingBone != null &&
            bobbingBone != idleBone)
        {
            bobbingBone.localPosition =
                initialBobPosition +
                bobOffset * smoothedIntensity;
        }
    }

    private void ApplyBodyAnimation()
    {
        if (idleBone == null)
            return;

        float animationTime =
            idleTimer *
            idleSpeed *
            currentSpeedMultiplier *
            Mathf.PI *
            2f;

        // Fewer waves are used now, creating a calmer movement.
        float mainWave = Mathf.Sin(
            animationTime + idlePhase
        );

        float slowWave = Mathf.Sin(
            animationTime * 0.5f +
            idlePhase +
            1f
        );

        float strength =
            currentIdleStrength * idleBlend;

        float verticalMovement =
            mainWave *
            idleBobDistance *
            strength;

        float horizontalMovement =
            slowWave *
            idleSideDistance *
            strength;

        float nod =
            mainWave *
            idleNodAngle *
            strength;

        float yaw =
            slowWave *
            idleYawAngle *
            strength;

        float tilt =
            slowWave *
            idleTiltAngle *
            strength;

        Vector3 targetPosition =
            initialIdlePosition +
            Vector3.up * verticalMovement +
            Vector3.right * horizontalMovement;

        if (idleBone == bobbingBone)
        {
            targetPosition +=
                bobOffset * smoothedIntensity;
        }

        idleBone.localPosition = targetPosition;

        idleBone.localRotation =
            initialIdleRotation *
            Quaternion.Euler(
                nod,
                yaw + currentLookBackAngle,
                tilt
            );

        // The scale no longer changes during normal idle.
        idleBone.localScale = initialIdleScale;
    }

    private void StartLookBack()
    {
        StopLookBackTween();

        float direction = GetLookBackDirection();

        bool useDeepTurn =
            Random.value < deepLookBackChance;

        float targetAngle;
        float turnDuration;
        float holdDuration;
        float returnDuration;

        if (useDeepTurn)
        {
            targetAngle = deepLookBackAngle * direction;
            turnDuration = deepTurnDuration;
            holdDuration = deepHoldDuration;
            returnDuration = deepReturnDuration;
        }
        else
        {
            targetAngle = normalLookBackAngle * direction;
            turnDuration = normalTurnDuration;
            holdDuration = normalHoldDuration;
            returnDuration = normalReturnDuration;
        }

        lookBackSequence = DOTween.Sequence();

        lookBackSequence.Append(
            DOTween.To(
                () => currentLookBackAngle,
                value => currentLookBackAngle = value,
                targetAngle,
                turnDuration
            ).SetEase(Ease.OutBack)
        );

        lookBackSequence.AppendInterval(holdDuration);

        lookBackSequence.Append(
            DOTween.To(
                () => currentLookBackAngle,
                value => currentLookBackAngle = value,
                0f,
                returnDuration
            ).SetEase(Ease.InOutSine)
        );

        lookBackSequence.OnComplete(() =>
        {
            currentLookBackAngle = 0f;
            lookBackSequence = null;
            ScheduleNextLookBack();
        });
    }

    private float GetLookBackDirection()
    {
        switch (lookBackDirection)
        {
            case LookBackDirection.Left:
                return -1f;

            case LookBackDirection.Right:
                return 1f;

            case LookBackDirection.Random:
                return Random.value < 0.5f ? -1f : 1f;

            default:
                return 1f;
        }
    }

    private void ScheduleNextLookBack()
    {
        float minimum = Mathf.Min(
            lookBackDelayMin,
            lookBackDelayMax
        );

        float maximum = Mathf.Max(
            lookBackDelayMin,
            lookBackDelayMax
        );

        nextLookBackDelay =
            Random.Range(minimum, maximum);

        lookBackTimer = 0f;
    }

    private void StopLookBackTween()
    {
        if (lookBackSequence == null)
            return;

        lookBackSequence.Kill();
        lookBackSequence = null;

        // Do not set currentLookBackAngle to zero here.
        // That caused the snapping in the previous version.
    }

    private void ShowPuppet()
    {
        isVisible = true;

        exitSilenceTimer = 0f;
        continuousSilenceTimer = 0f;

        idleBlend = 0f;
        idleHasStarted = false;

        StopLookBackTween();
        currentLookBackAngle = 0f;

        transform.DOKill();

        transform.DOMove(initialPosition, duration)
            .SetEase(Ease.OutBack);
    }

    private void HidePuppet()
    {
        isVisible = false;

        exitSilenceTimer = 0f;
        continuousSilenceTimer = 0f;

        smoothedIntensity = 0f;
        idleBlend = 0f;
        idleHasStarted = false;

        ResetAnimations();

        transform.DOKill();

        transform.DOMove(
            initialPosition + Vector3.down * popUpDistance,
            duration
        ).SetEase(Ease.InBack);
    }

    private void ResetAnimations()
    {
        StopLookBackTween();

        currentLookBackAngle = 0f;

        if (mouthBone != null)
        {
            mouthBone.localRotation =
                Quaternion.Euler(closedRotation);
        }

        if (bobbingBone != null)
        {
            bobbingBone.localPosition =
                initialBobPosition;
        }

        if (idleBone != null)
        {
            idleBone.localPosition =
                initialIdlePosition;

            idleBone.localRotation =
                initialIdleRotation;

            idleBone.localScale =
                initialIdleScale;
        }
    }

    private void OnDisable()
    {
        StopLookBackTween();
        transform.DOKill();
    }
}