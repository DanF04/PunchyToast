using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class ToastBehavior : MonoBehaviour
{
    public float hoverDuration, bobAmount, bobSpeed, preHoverDelay, driftFactor;
    [HideInInspector] public List<Transform> potentialTargets;
    [HideInInspector] public GameObject armPrefab;
    [HideInInspector] public bool debugAlwaysL;
    [HideInInspector] public float armSpawnOffset, armPunchDuration, targetFlightForce;

    [Header("Punishment Sequence Timings")]
    public float impactPauseDuration = 1.0f;     // The "Sticky" pause
    public float toastSlideDuration = 3.0f;      // Total slide time
    public float secondaryZoomDelay = 2.0f;      // When the zoom starts during the slide
    public float initialZoomSpeed = 0.25f;      // Speed of the first zoom
    public float secondaryZoomSpeed = 0.6f;     // Speed of the second zoom

    [Header("Slap Settings")]
    public int slapsLeft = 0;           // Set this in the inspector for "tough" toasts
    public float slapSpinDuration = 0.4f;
    string slapString = "";
    private bool isSlappable = false;

    public float exitMomentumScale = 0.8f;
    public float armShrinkDuration = 0.8f;
    public float flightDuration = 0.5f;
    public float fallGracePeriod = 0.2f;

    public float impactFreezeTime = 0.05f;
    public float shakeIntensity = 0.01f;
    public int shakeVibrato = 30;
    public float pushForce = 0.05f;
    public float armRecoilDistance = 2f;

    private Rigidbody rb;
    private bool isRising = false, hasLeftToaster = false, isHovering = false, hasBeenHit = false;
    public bool isPunchable = true;
    public char assignedLetter;
    private KeyCode assignedKey;
    private float capturedXVel, capturedZVel;
    private Tween bobTween;
    private Coroutine hoverRoutine;
    private TextMeshProUGUI letterText;
    private Client currentFlightTargetClient;

    public bool isSimultaneous = false;

    public int myLetterIndex = 0;

    public AudioMixer sfxMixer;
    public AudioClip[] punchSounds;
    public AudioClip[] toastGettingIntoMouth;
    public AudioClip[] toastFlying;
    public AudioClip[] toastLandingNaturally;

    public GameObject punchEffect;

    private FireController fireController;

    private string jamOnThisToast = "";

    private Vector3 originalRotation = Vector3.zero;

    public Vector3 originalCameraPosition = Vector3.zero;
    public Vector3 originalCameraRotation = Vector3.zero;

    public Vector3 cinematicCameraPosition = Vector3.zero;
    public Vector3 cinematicCameraRotation = Vector3.zero;

    public bool summonedFromSimul = false;

    private void OnCollisionEnter(Collision collision)
    {
        // Bounce off "Wall" layer objects while hovering by flipping X velocity direction
        if (isHovering && collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            capturedXVel = -capturedXVel;
            capturedZVel = 0f;
            rb.linearVelocity = new Vector3(capturedXVel * driftFactor, 0f, 0f);
            return;
        }

        AudioManager.Instance.PlaySound(toastLandingNaturally, sfxMixer, transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHovering && other.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            capturedXVel = -capturedXVel;
            capturedZVel = 0f;
            rb.linearVelocity = new Vector3(capturedXVel * driftFactor, 0f, 0f);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        int assignedIndex = ClientManager.Instance.GetAvailableIndex();

        fireController = GetComponent<FireController>();

        if (assignedIndex != -1)
        {
            myLetterIndex = assignedIndex;
            assignedLetter = ClientManager.Instance.GetCurrentWord()[assignedIndex];
        }
        else assignedLetter = (char)('A' + Random.Range(0, 26));


        slapsLeft = 0;

        // check if assigned letter is a number
        if (char.IsDigit(assignedLetter))
        {
            isSlappable = true;
            slapString = ClientManager.Instance.GetSlapWordForIndex(assignedLetter - '0');
            slapsLeft = slapString.Length;

            SetCurrentLetter(slapString[slapString.Length - slapsLeft]);
        }
        else
        {
            //int assignedIndex = ClientManager.Instance.GetAvailableIndex();

            if (assignedIndex != -1)
            {
                myLetterIndex = assignedIndex;
                assignedLetter = ClientManager.Instance.GetCurrentWord()[assignedIndex];
            }
            else assignedLetter = (char)('A' + Random.Range(0, 26));

            // Debug index and assigned letter
            Debug.Log($"Assigned Letter: {assignedLetter} at index {assignedIndex}");

            assignedLetter = char.ToUpper(assignedLetter);
            assignedKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), assignedLetter.ToString());
        }

        if(slapsLeft > 0)
        {
            GetComponentInChildren<TAG_Slap>().GetComponent<Image>().enabled = true;
        }
    }



    private void SetCurrentLetter(char c)
    {
        SetFire();
        assignedLetter = c;
        assignedKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), assignedLetter.ToString());
        if (letterText != null) letterText.text = assignedLetter.ToString();
    }

    private void SetFire()
    {
        fireController.Disable();

        if (Toaster.Instance.currentCombo > 0)
        {
            if (Toaster.Instance.currentCombo >= 7) fireController.fire3.SetActive(true);
            else if (Toaster.Instance.currentCombo >= 3) fireController.fire2.SetActive(true);
            else fireController.fire1.SetActive(true);

        }
    }

    private void Start()
    {

        originalRotation = transform.eulerAngles;
        SetFire();
        letterText = GetComponentInChildren<TextMeshProUGUI>();
        if (letterText != null) { letterText.text = assignedLetter.ToString(); }
    }

    void Update()
    {
        if (!isPunchable || hasBeenHit) return;
        if (Input.GetKeyDown(assignedKey) || (debugAlwaysL && Input.GetKeyDown(KeyCode.L))) StartPunchSequence();
    }

    void FixedUpdate()
    {
        if (hasBeenHit) return;
        if (!hasLeftToaster)
        {
            if (rb.linearVelocity.y > 0.1f) { hasLeftToaster = true; isRising = true; }
            return;
        }
        if (isRising && rb.linearVelocity.y <= 0.05f)
        {
            isRising = false;
            hoverRoutine = StartCoroutine(HoverRoutine());
        }
    }

    IEnumerator HoverRoutine()
    {
        TrailRenderer[] lineRenderers = GetComponentsInChildren<TrailRenderer>();
        foreach (TrailRenderer lr in lineRenderers) lr.enabled = false;

        yield return new WaitForSeconds(preHoverDelay);

        capturedXVel = rb.linearVelocity.x;
        capturedZVel = rb.linearVelocity.z;
        isHovering = true;
        rb.useGravity = false;

        // Reset Y linear velocity to prevent residual upward momentum
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Lock baseline Y height on first hover initialization
        if (baselineY == 0f)
        {
            baselineY = transform.position.y;
        }
        else
        {
            // Smoothly snap position back to baseline Y height after a slap
            transform.DOMoveY(baselineY, 0.15f).SetEase(Ease.OutQuad);
        }

        // Start clean Y-bobbing relative to the baseline height
        if (bobTween != null) bobTween.Kill();
        bobTween = transform.DOMoveY(baselineY + bobAmount, bobSpeed)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        float timer = 0;
        while (timer < hoverDuration)
        {
            timer += Time.fixedDeltaTime;
            // Strict 0 on Y velocity to prevent physics forces from accumulating height
            rb.linearVelocity = new Vector3(capturedXVel * driftFactor, 0f, capturedZVel * driftFactor);
            yield return new WaitForFixedUpdate();
        }

        if (!hasBeenHit) ReleaseToast();
    }

    void StartPunchSequence()
    {
        hasBeenHit = true;
        slapsLeft--;

        // Add a callback timer to reset hasbeenhit after a short delay if there are slaps left

        Toaster.Instance.IncrementToastsPunched();


        string currentJam = JamDecider.Instance.GetCurrentJamName();
        Transform targetTransform = ClientManager.Instance.GetBestTarget(currentJam);

        jamOnThisToast = JamDecider.Instance.GetCurrentJamName();

        currentFlightTargetClient = targetTransform.GetComponent<Client>();

        if (currentFlightTargetClient != null && slapsLeft <= 0)
        {
            if(!summonedFromSimul)
                ClientManager.Instance.IncreaseLetterIndex();

            currentFlightTargetClient.Satisfy();
            if (currentFlightTargetClient.isSatisfied || ClientManager.Instance.isBossFight)
                ClientManager.Instance.RemoveIndex(myLetterIndex);

            targetTransform = currentFlightTargetClient.TargetForToast;
        }

        if (currentFlightTargetClient == null)
        {
            foreach (ToastBehavior tb in Toaster.Instance.activeToasts)
            {
                if (tb != null && tb != this)
                    tb.ForceReleaseToast();
            }
        }

        Vector3 dirToTarget = (targetTransform.position - transform.position).normalized;
        GameObject arm = Instantiate(armPrefab, transform.position - (dirToTarget * armSpawnOffset), Quaternion.LookRotation(dirToTarget));
        if (Camera.main.WorldToViewportPoint(transform.position).x > 0.5f)
            arm.transform.localScale = Vector3.Scale(arm.transform.localScale, new Vector3(-1, 1, 1));

        bool isLastHit = ClientManager.Instance.IsLastToastOfLevel() && slapsLeft <= 1;
        if (isLastHit)
        {
            Time.timeScale = 0.2f; // Slow down time as punch connects
            if (Toaster.Instance.cinematicCamera != null)
            {
                Toaster.Instance.cinematicCamera.gameObject.SetActive(true);
                Toaster.Instance.defaultCamera.gameObject.SetActive(false);
            }
        }

        arm.transform.DOMove(transform.position, armPunchDuration).SetEase(Ease.Linear).OnComplete(() =>
        {

            

            StartCoroutine(ImpactSequence(arm, targetTransform, dirToTarget));

            AudioManager.Instance.PlaySound(punchSounds, sfxMixer, transform.position, volume: 0.4f);

            float pitch = Toaster.Instance.GetComboPitch();
            if (Toaster.Instance.currentCombo >= 1)
            {

                AudioManager.Instance.PlaySoundFixedPitch(Toaster.Instance.toastComboSound, pitch, sfxMixer, transform.position, volume: 2.0f);



            }

            Toaster.Instance.IncrementCombo();

            // ONLY launch a new toast if this hit is going to finish the current toast
            if (slapsLeft <= 0)
            {
                isPunchable = false;
                if (!ClientManager.Instance.IsLastClientOfWave() && !Toaster.Instance.AreTherePunchableToasts())
                {
                    if(currentFlightTargetClient != null)
                        Toaster.Instance.LaunchToast();

                }
            }
        });
    }

    private float slapHorizontalDir = 1f; // Toggles between 1 (Right) and -1 (Left)
    private float slapVerticalDir = 1f;   // Toggles between 1 (Up) and -1 (Down)
    private float baselineY; // Locked Y coordinate for hovering

    IEnumerator ImpactSequence(GameObject arm, Transform target, Vector3 dirToTarget)
    {
        if (bobTween != null) bobTween.Kill();
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // 1. Instantiate punch effect
        GameObject punchEffectInstance = Instantiate(punchEffect, transform.position, Quaternion.identity);

        ApplyJamSplat();

        // 2. Calculate movement offset (alternates horizontal knockback)
        Vector3 moveOffset;

        if (slapsLeft > 0 && isSlappable)
        {
            Camera cam = Camera.main;
            Vector3 screenRight = cam != null ? cam.transform.right : Vector3.right;

            // Pure horizontal displacement on X/Z (prevents vertical Y climbing)
            Vector3 horizontalDir = new Vector3(screenRight.x, 0f, screenRight.z).normalized;
            moveOffset = horizontalDir * (pushForce * slapHorizontalDir);

            // Toggle direction for the next slap (Right <-> Left)
            slapHorizontalDir *= -1f;
        }
        else
        {
            // Final hit: standard forward push toward target
            Vector3 flattenedDir = new Vector3(dirToTarget.x, dirToTarget.y, 0).normalized;
            moveOffset = flattenedDir * pushForce;
        }

        // 3. Fire impact move tweens
        arm.transform.DOMove(arm.transform.position + moveOffset, impactFreezeTime).SetEase(Ease.Linear);
        transform.DOMove(transform.position + moveOffset, impactFreezeTime).SetEase(Ease.Linear);

        if (punchEffectInstance != null)
        {
            punchEffectInstance.transform.DOMove(punchEffectInstance.transform.position + moveOffset, impactFreezeTime).SetEase(Ease.Linear);
        }

        yield return new WaitForSeconds(impactFreezeTime);

        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.1f, 30);
        transform.DOShakePosition(0.05f, shakeIntensity, shakeVibrato);

        yield return new WaitForSeconds(impactFreezeTime);

        if (slapsLeft <= 1)
        {
            GetComponentInChildren<TAG_Slap>().GetComponent<Image>().enabled = false;
        }

        if (slapsLeft > 0 && isSlappable)
        {
            SetCurrentLetter(slapString[slapString.Length - slapsLeft]);

            // Invert horizontal drift velocity strictly once
            capturedXVel = -capturedXVel;
            capturedZVel = -capturedZVel;

            // Ensure minimum speed threshold so momentum doesn't drop
            float minDriftSpeed = 1.5f;
            if (Mathf.Abs(capturedXVel) < minDriftSpeed)
            {
                capturedXVel = Mathf.Sign(capturedXVel) != 0 ? Mathf.Sign(capturedXVel) * minDriftSpeed : minDriftSpeed;
            }

            // Re-enable physics and immediately assign full inverted velocity
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearVelocity = new Vector3(capturedXVel * driftFactor, 0f, capturedZVel * driftFactor);

            // Reset rotation and Spin
            transform.DOKill(false);
            transform.eulerAngles = originalRotation;
            transform.DORotate(new Vector3(0, 720, 0), slapSpinDuration, RotateMode.LocalAxisAdd).SetEase(Ease.OutBack);

            // Recoil arm
            Vector3 armRecoilPos = arm.transform.position - (dirToTarget * armRecoilDistance);
            arm.transform.DOMove(armRecoilPos, armShrinkDuration).SetEase(Ease.OutBack).OnComplete(() => Destroy(arm));

            hasBeenHit = false;
            isPunchable = true;

            hoverRoutine = StartCoroutine(HoverRoutine());
        }
        else
        {
            // FINAL HIT: Launch behavior
            LaunchAtTarget(target);

            Vector3 recoilPos = arm.transform.position - (dirToTarget * armRecoilDistance);
            Sequence armSeq = DOTween.Sequence();
            armSeq.Append(arm.transform.DOMove(recoilPos, armShrinkDuration * 0.8f).SetEase(Ease.OutBack));
            armSeq.Append(arm.transform.DOScale(Vector3.zero, armShrinkDuration).SetEase(Ease.InQuad));
            armSeq.OnComplete(() =>
            {
                arm.transform.DOKill();
                Destroy(arm);
            });
        }
    }

    void ApplyJamSplat()
    {
        TAG_Splat splat = GetComponentInChildren<TAG_Splat>();
        Renderer splatRenderer = splat?.GetComponent<Renderer>();
        if (splatRenderer != null) { splatRenderer.enabled = true; splatRenderer.material.SetColor("_BaseColor", JamDecider.Instance.GetCurrentJamColor()); }
    }

    void LaunchAtTarget(Transform target)
    {
        
        bool isCinematic = Time.timeScale < 1f;

        isHovering = false;

        AudioManager.Instance.PlaySound(toastFlying, sfxMixer, transform.position);

        foreach (TrailRenderer lr in GetComponentsInChildren<TrailRenderer>()) lr.enabled = true;
        foreach (TAG_JamDroplets droplet in GetComponentsInChildren<TAG_JamDroplets>(true))
        {
            droplet.gameObject.SetActive(true);
            var main = droplet.GetComponent<ParticleSystem>().main;
            main.startColor = JamDecider.Instance.GetCurrentJamColor();
        }

        // Check if we actually have a valid client target
        // If currentFlightTargetClient is null, it means we are just aiming at a seating position
        if (currentFlightTargetClient != null)
        {
            rb.isKinematic = true;

            //float actualFlightDuration = isCinematic ? flightDuration * 2f : flightDuration;

            transform.DOLookAt(target.position, flightDuration / 4).SetEase(Ease.Linear);
            currentFlightTargetClient.OpenMouth();

            gameObject.GetComponent<Collider>().enabled = false; // Disable collider to prevent mid-flight collisions

            Sequence flightSeq = DOTween.Sequence();

            if (isCinematic && Toaster.Instance.cinematicCamera != null)
            {
                // Update camera to look at the toast during flight
                flightSeq.OnUpdate(() =>
                {
                    Toaster.Instance.cinematicCamera.transform.LookAt(transform.position);
                });
            }

            flightSeq.Append(transform.DOMove(target.position, flightDuration).SetEase(Ease.Linear));
            flightSeq.InsertCallback(flightDuration / 2f, () => { if (currentFlightTargetClient != null) currentFlightTargetClient.Recoil(); });
            flightSeq.OnComplete(() =>
            {
                rb.isKinematic = false;
                rb.useGravity = true;


                if (Toaster.Instance.cinematicCamera != null && Time.timeScale < 1f)
                {

                    DOVirtual.DelayedCall(0f, () =>
                    {
                        Time.timeScale = 1f;
                        currentFlightTargetClient.TryEatToast(jamOnThisToast, gameObject);

                        DOVirtual.DelayedCall(3f, () =>
                        {

                            Toaster.Instance.cinematicCamera.gameObject.SetActive(false);
                            Toaster.Instance.defaultCamera.gameObject.SetActive(true);
                        });
                    });
                }
                else
                {
                    currentFlightTargetClient.TryEatToast(jamOnThisToast, gameObject);

                }

                AudioManager.Instance.PlaySound(toastGettingIntoMouth, sfxMixer, transform.position);

            });
        }
        else
        {
            Toaster.Instance.TriggerPunishmentCooldown();

            rb.isKinematic = true;
            gameObject.GetComponent<Collider>().enabled = false;
            if (letterText != null) letterText.enabled = false;

            Toaster.Instance.ResetCombo();
            MusicManager.Instance.RecordScratchStop(Toaster.Instance.punishmentCooldown);

            Client clientToBonk = ClientManager.Instance.GetClientInSeat(target);

           

            SphereCollider headCollider = clientToBonk != null ? clientToBonk.GetComponentInChildren<SphereCollider>() : null;
            Vector3 impactPoint = target.position;

            if (headCollider != null)
            {
                Vector3 dirToHead = (target.position - transform.position).normalized;
                float worldRadius = headCollider.radius * headCollider.transform.lossyScale.x;
                impactPoint = target.position - (dirToHead * worldRadius);
            }

            // --- CAMERA & EYELID CACHING ---
            Camera mainCam = Camera.main;
            float originalFOV = Toaster.Instance.originalCameraFOV;
            Quaternion originalCamRot = Quaternion.Euler(originalCameraRotation);

            // --- FLAVOR OBJECT CACHING ---
            Vector3 originalFlavorPos = Vector3.zero;
            Vector3 originalFlavorRot = Vector3.zero;
            Vector3 originalFlavorScale = Vector3.zero;
            if (clientToBonk != null && clientToBonk.flavorObject != null)
            {
                originalFlavorPos = clientToBonk.flavorObject.transform.position;
                originalFlavorRot = clientToBonk.flavorObject.transform.rotation.eulerAngles;
                originalFlavorScale = clientToBonk.flavorObject.transform.localScale;
            }

            float zoomedFOV = 25f;

            Sequence splatSeq = DOTween.Sequence();

            // 1. Travel to face
            splatSeq.Append(transform.DOMove(impactPoint, flightDuration).SetEase(Ease.InQuad));

            // 2. Impact Phase
            splatSeq.AppendCallback(() => {
                if (clientToBonk != null)
                {
                    clientToBonk.HardRecoil();
                    clientToBonk.FreezeClient();
                    clientToBonk.SetAngryEyelids();

                    // Move the flavor UI to the "Wrong" position
                    if (clientToBonk.flavorObject != null && clientToBonk.positionForWrongFlavor != null)
                    {
                        clientToBonk.flavorObject.transform.DOMove(clientToBonk.positionForWrongFlavor.position, initialZoomSpeed).SetEase(Ease.OutBack);
                        clientToBonk.flavorObject.transform.DORotate(clientToBonk.positionForWrongFlavor.eulerAngles, initialZoomSpeed).SetEase(Ease.OutBack);
                        clientToBonk.flavorObject.transform.DOScale(clientToBonk.positionForWrongFlavor.localScale, initialZoomSpeed).SetEase(Ease.OutBack);
                    }
                }

                // --- CINEMATIC CAMERA: Zoom + Kickback Shake ---
                mainCam.DOFieldOfView(zoomedFOV, initialZoomSpeed).SetEase(Ease.OutExpo);
                mainCam.transform.DOLookAt(impactPoint, initialZoomSpeed).SetEase(Ease.OutExpo);
                mainCam.transform.DOShakePosition(0.3f, 0.05f, 20);
            });

            // 3. The Sticky Pause
            splatSeq.AppendInterval(impactPauseDuration);

            // 4. The Slow Slide
            float slideDistance = headCollider != null ? headCollider.radius * 3f : 2f;
            splatSeq.Append(transform.DOMoveY(impactPoint.y - slideDistance, toastSlideDuration).SetEase(Ease.InSine));
            splatSeq.Join(transform.DORotate(new Vector3(60, transform.eulerAngles.y, 0), toastSlideDuration).SetEase(Ease.InSine));

            // 5. The Secondary Zoom
            // Fixed: We calculate the position based on the PREVIOUS steps (Flight + Callback + Interval)
            float sequenceTimeBeforeSlide = flightDuration + impactPauseDuration;
            splatSeq.InsertCallback(sequenceTimeBeforeSlide + secondaryZoomDelay, () => {
                if (clientToBonk != null && clientToBonk.flavorWantedUI != null)
                {
                    mainCam.DOFieldOfView(12f, secondaryZoomSpeed).SetEase(Ease.OutSine);
                    mainCam.transform.DOLookAt(clientToBonk.flavorWantedUI.position, initialZoomSpeed).SetEase(Ease.OutSine);
                }
            });

            // 6. Cleanup & Return
            splatSeq.OnComplete(() => {
                if (clientToBonk != null)
                {
                    clientToBonk.RestoreEyelids();
                    clientToBonk.UnfreezeClient();

                    // Move the flavor UI back to where it was
                    if (clientToBonk.flavorObject != null)
                    {
                        clientToBonk.flavorObject.transform.DOMove(originalFlavorPos, 0.5f).SetEase(Ease.InOutQuad);
                        clientToBonk.flavorObject.transform.DORotate(originalFlavorRot, 0.5f).SetEase(Ease.InOutQuad);
                        clientToBonk.flavorObject.transform.DOScale(originalFlavorScale, 0.5f).SetEase(Ease.InOutQuad);
                    }
                }

                JamDecider.Instance.BlinkOutline(clientToBonk.desiredCondiment);

                // --- CINEMATIC CAMERA: Return ---
                mainCam.DOFieldOfView(originalFOV, 0.6f).SetEase(Ease.InOutBack);
                mainCam.transform.DORotateQuaternion(originalCamRot, 0.6f).SetEase(Ease.InOutBack);

                if (EndlessModeManager.Instance != null)
                {
                    if (EndlessModeManager.Instance.isRunning)
                        EndlessModeManager.Instance.AddALoss();
                }

                Destroy(gameObject);
            });

            isPunchable = false;
        }
    }

    private void OnMouseDown()
    {
        if (!isPunchable || hasBeenHit) return;
        StartPunchSequence();
    }

    void ReleaseToast()
    {

        isHovering = false;
        if (bobTween != null) bobTween.Kill();
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(capturedXVel * exitMomentumScale, -0.1f, capturedZVel * exitMomentumScale);
        StartCoroutine(GracePeriodTimer());
    }

    void ForceReleaseToast()
    {
        hasBeenHit = true;
        isHovering = false;
        if (bobTween != null) bobTween.Kill();
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(capturedXVel, -0.1f, capturedZVel);

        isPunchable = false;
        rb.constraints = RigidbodyConstraints.None;
        Toaster.Instance.ResetCombo();
        letterText.enabled = false;
        GetComponentInChildren<TAG_Slap>().GetComponent<Image>().enabled = false;

        if (summonedFromSimul)
        {
            ClientManager.Instance.HandleSimultaneousFailure();
        }
    }

    IEnumerator GracePeriodTimer()
    {
        yield return new WaitForSeconds(fallGracePeriod);
        if (!hasBeenHit)
        {
            isPunchable = false;
            rb.constraints = RigidbodyConstraints.None;
            Toaster.Instance.ResetCombo();
            letterText.enabled = false;
            GetComponentInChildren<TAG_Slap>().GetComponent<Image>().enabled = false;

            if(summonedFromSimul)
            {
                ClientManager.Instance.HandleSimultaneousFailure();
            }

            if (EndlessModeManager.Instance != null)
            {
                if (EndlessModeManager.Instance.isRunning)
                    EndlessModeManager.Instance.AddALoss();
            }
        }
    }



    private void OnDestroy() { if (Toaster.Instance != null) Toaster.Instance.UnregisterToast(this); transform.DOKill(); }
}