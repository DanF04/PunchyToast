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

    [Header("Eye Tracking Settings")]
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    [Tooltip("Maximum allowed eye rotation angle from forward in degrees.")]
    [Range(5f, 85f)]
    [SerializeField] private float maxEyeAngle = 40f;

    [Tooltip("How smoothly eyes track the target or return to forward position.")]
    [SerializeField] private float eyeTrackingSpeed = 10f;

    [Header("Seed Settings")]
    [SerializeField] private int seed = 12345;

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

    [SerializeField] private LookBackDirection lookBackDirection = LookBackDirection.Random;

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

    private System.Random rng;

    private Vector3 initialPosition;
    private Vector3 initialBobPosition;

    private Vector3 initialIdlePosition;
    private Vector3 initialIdleScale;
    private Quaternion initialIdleRotation;

    private Quaternion initialLeftEyeRotation;
    private Quaternion initialRightEyeRotation;

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
    private TAG_Toast cachedToastTarget;

    private void Awake()
    {
        rng = new System.Random(seed);
    }

    private float GetRandomRange(float min, float max)
    {
        return (float)(min + (max - min) * rng.NextDouble());
    }

    private float GetRandomValue()
    {
        return (float)rng.NextDouble();
    }

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

        if (leftEye != null)
            initialLeftEyeRotation = leftEye.localRotation;

        if (rightEye != null)
            initialRightEyeRotation = rightEye.localRotation;

        // Prevents every puppet from moving with exactly the same timing.
        idlePhase = GetRandomRange(0f, Mathf.PI * 2f);

        SelectNewIdleVariation();
        ScheduleNextLookBack();

        transform.position = initialPosition + Vector3.down * popUpDistance;
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

        float rawIntensity = visualizer.BandBuffer[bandIndex] * sensitivity;

        float targetIntensity = Mathf.Clamp01(
            (rawIntensity - noiseThreshold) /
            Mathf.Max(0.01f, 1f - noiseThreshold)
        );

        bool isTalking = targetIntensity > 0.001f;

        HandleVisibility(isTalking);

        if (!isVisible)
            return;

        // Frame-rate independent smoothing
        smoothedIntensity = Mathf.Lerp(
            smoothedIntensity,
            targetIntensity,
            1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime)
        );

        UpdateIdleState(isTalking);
        ApplyMouthAnimation();
        ApplyBodyAnimation();
        UpdateEyeTracking();
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

        bool audioIsPlaying = audioSource != null && audioSource.isPlaying;

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

        // Smooth continuous phase accumulation wrapping at 4*PI (full period of main + half-speed waves)
        float twoCycles = Mathf.PI * 4f;
        idleTimer += Time.deltaTime * idleSpeed * currentSpeedMultiplier * Mathf.PI * 2f;
        idleTimer %= twoCycles;

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
        // Frame-rate independent smoothing
        currentLookBackAngle = Mathf.Lerp(
            currentLookBackAngle,
            0f,
            1f - Mathf.Exp(-talkingReturnSpeed * Time.deltaTime)
        );

        if (Mathf.Abs(currentLookBackAngle) < 0.001f)
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

        // Frame-rate independent smoothing
        currentIdleStrength = Mathf.Lerp(
            currentIdleStrength,
            targetIdleStrength,
            1f - Mathf.Exp(-variationSmoothSpeed * Time.deltaTime)
        );

        currentSpeedMultiplier = Mathf.Lerp(
            currentSpeedMultiplier,
            targetSpeedMultiplier,
            1f - Mathf.Exp(-variationSmoothSpeed * Time.deltaTime)
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

        nextVariationDelay = GetRandomRange(minimum, maximum);

        targetIdleStrength = GetRandomRange(0.8f, 1.05f);
        targetSpeedMultiplier = GetRandomRange(0.9f, 1.08f);
    }

    private void UpdateLookBack()
    {
        if (!useRandomLookBack || lookBackSequence != null)
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

    private void UpdateEyeTracking()
    {
        if (leftEye == null && rightEye == null)
            return;

        // Locate a TAG_Toast target in the scene if we don't currently have a valid reference
        if (cachedToastTarget == null)
        {
            cachedToastTarget = FindAnyObjectByType<TAG_Toast>();
        }

        float factor = 1f - Mathf.Exp(-eyeTrackingSpeed * Time.deltaTime);

        if (leftEye != null)
            RotateEyeTowardTarget(leftEye, initialLeftEyeRotation, factor);

        if (rightEye != null)
            RotateEyeTowardTarget(rightEye, initialRightEyeRotation, factor);
    }

    private void RotateEyeTowardTarget(Transform eyeTransform, Quaternion defaultRotation, float lerpFactor)
    {
        if (cachedToastTarget == null)
        {
            eyeTransform.localRotation = Quaternion.Slerp(
                eyeTransform.localRotation,
                defaultRotation,
                lerpFactor
            );
            return;
        }

        Vector3 targetDirection = cachedToastTarget.transform.position - eyeTransform.position;

        if (targetDirection.sqrMagnitude < 0.0001f)
            return;

        // Construct a world rotation where local +Y points towards targetDirection
        // Quaternion.FromToRotation maps Vector3.up (+Y) directly to targetDirection
        Quaternion targetWorldRotation = Quaternion.FromToRotation(Vector3.up, targetDirection.normalized);

        // Convert world target rotation into parent local space
        Quaternion targetLocalRotation;
        if (eyeTransform.parent != null)
        {
            targetLocalRotation = Quaternion.Inverse(eyeTransform.parent.rotation) * targetWorldRotation;
        }
        else
        {
            targetLocalRotation = targetWorldRotation;
        }

        // Clamp total rotation offset to maxEyeAngle from default initial rotation
        Quaternion clampedRotation = Quaternion.RotateTowards(
            defaultRotation,
            targetLocalRotation,
            maxEyeAngle
        );

        eyeTransform.localRotation = Quaternion.Slerp(
            eyeTransform.localRotation,
            clampedRotation,
            lerpFactor
        );
    }

    private void ApplyMouthAnimation()
    {
        if (mouthBone != null)
        {
            Quaternion closed = Quaternion.Euler(closedRotation);
            Quaternion open = Quaternion.Euler(openRotation);

            mouthBone.localRotation = Quaternion.Slerp(
                closed,
                open,
                smoothedIntensity
            );
        }

        if (bobbingBone != null && bobbingBone != idleBone)
        {
            bobbingBone.localPosition = initialBobPosition + bobOffset * smoothedIntensity;
        }
    }

    private void ApplyBodyAnimation()
    {
        if (idleBone == null)
            return;

        float animationTime = idleTimer;

        float mainWave = Mathf.Sin(animationTime + idlePhase);

        float slowWave = Mathf.Sin(
            animationTime * 0.5f +
            idlePhase +
            1f
        );

        float strength = currentIdleStrength * idleBlend;

        float verticalMovement = mainWave * idleBobDistance * strength;

        float horizontalMovement = slowWave * idleSideDistance * strength;

        float nod = mainWave * idleNodAngle * strength;

        float yaw = slowWave * idleYawAngle * strength;

        float tilt = slowWave * idleTiltAngle * strength;

        Vector3 targetPosition =
            initialIdlePosition +
            Vector3.up * verticalMovement +
            Vector3.right * horizontalMovement;

        if (idleBone == bobbingBone)
        {
            targetPosition += bobOffset * smoothedIntensity;
        }

        idleBone.localPosition = targetPosition;

        idleBone.localRotation =
            initialIdleRotation *
            Quaternion.Euler(
                nod,
                yaw + currentLookBackAngle,
                tilt
            );

        idleBone.localScale = initialIdleScale;
    }

    private void StartLookBack()
    {
        StopLookBackTween();

        float direction = GetLookBackDirection();

        bool useDeepTurn = GetRandomValue() < deepLookBackChance;

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
        lookBackSequence.SetUpdate(UpdateType.Normal, false);

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
                return GetRandomValue() < 0.5f ? -1f : 1f;

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

        nextLookBackDelay = GetRandomRange(minimum, maximum);

        lookBackTimer = 0f;
    }

    private void StopLookBackTween()
    {
        if (lookBackSequence == null)
            return;

        lookBackSequence.Kill();
        lookBackSequence = null;
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
            mouthBone.localRotation = Quaternion.Euler(closedRotation);
        }

        if (bobbingBone != null)
        {
            bobbingBone.localPosition = initialBobPosition;
        }

        if (idleBone != null)
        {
            idleBone.localPosition = initialIdlePosition;
            idleBone.localRotation = initialIdleRotation;
            idleBone.localScale = initialIdleScale;
        }

        if (leftEye != null)
        {
            leftEye.localRotation = initialLeftEyeRotation;
        }

        if (rightEye != null)
        {
            rightEye.localRotation = initialRightEyeRotation;
        }
    }

    private void OnDisable()
    {
        StopLookBackTween();
        transform.DOKill();
    }
}