using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Audio; // Added for List

using UnityEngine.Events;

public class Client : MonoBehaviour
{
    [Header("Settings")]
    public Transform mouthBone;
    public Transform recoilBone;
    public Transform pivot;
    public Transform TargetForToast;
    public Vector3 mouthOpenRotation = new Vector3(-45, 0, 0);
    public float entranceDuration = 0.6f;
    public float popUpDistance = 2.0f;

    public Transform flavorObject;
    public Transform flavorWantedUI;
    public Transform positionForWrongFlavor;

    [SerializeField] private bool useRandomVisual = true;

    public int toastsToSatisfy = 1;
    private int currentToastsEaten = 0;

    public GameObject bossBar;
    [SerializeField] private Image bossFill;

    [Header("Rendering")]
    [SerializeField] private Renderer[] hair;
    [SerializeField] private Renderer[] shirt;
    [SerializeField] private Renderer[] skin;
    [SerializeField] private Material[] materials;
    [SerializeField] private Material specialMaterial;
    [SerializeField] private float specialChance = 0.25f;
    [SerializeField] private float randomHairColor = 0.25f;
    [SerializeField] private float randomBaldChance = 0.1f;

    [SerializeField] private Material redMaterial;
    // Add this near your other private variables
    private Dictionary<Renderer, Material> originalMaterialMap = new Dictionary<Renderer, Material>();

    [Header("Eye Tracking")]
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;
    private Transform currentEyeTarget;
    private float searchTimer = 0f;
    private float searchInterval = 0.2f; // Search for toast 5 times a second, not every frame

    [Header("Waiting Animation Settings")]
    [SerializeField] private Vector3 waitingMouthClosed = new Vector3(-10, 0, 0);
    [SerializeField] private Vector3 waitingMouthOpen = new Vector3(-20, 0, 0);
    [SerializeField] private float waitingCycleDuration = 0.4f;

    [Header("Randomized Hopping")]
    [SerializeField] private float minHopHeight = 0.02f;
    [SerializeField] private float maxHopHeight = 0.08f;
    [SerializeField] private float minHopSpeed = 0.3f;
    [SerializeField] private float maxHopSpeed = 0.6f;

    [SerializeField] private GameObject eyeToastL;
    [SerializeField] private GameObject eyeToastR;

    [Header("Current Order")]
    public string desiredCondiment;
    public Color condimentColor;
    private bool hasEaten = false;
    public bool isSatisfied = false;
    public bool isSat = false;

    [SerializeField] private int numberOfBites = 2;

    private Vector3 originalMouthRot;
    private Vector3 pivotInitialLocalPos;
    private Transform mySeat;
    private Vector3 targetPosition;

    [SerializeField] private Transform eyeLidL;
    [SerializeField] private Transform eyeRidL;

    [SerializeField] private Renderer leftEyeRenderer;
    private Material originalLeftEyeMat;
    [SerializeField] private Renderer rightEyeRenderer;
    private Material originalRightEyeMat;

    private Tween hopTween;
    private Tween mouthTween;

    [SerializeField] private GameObject thoughtBubble;
    [SerializeField] private Image wantIcon;
    [SerializeField] private GameObject satisfiedIcon;

    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private AudioMixer puppetMixer;

    [SerializeField] private AudioClip[] gettingUpSound;
    [SerializeField] private AudioClip[] goingDownSound;

    [SerializeField] private AudioClip[] hurtRecoilSoundAnt;
    [SerializeField] private AudioClip[] muchingFoodSoundAnt;
    [SerializeField] private AudioClip[] mouthIsOpenSoundAnt;

    [SerializeField] private AudioClip[] hurtRecoilSoundDan;
    [SerializeField] private AudioClip[] muchingFoodSoundDan;
    [SerializeField] private AudioClip[] mouthIsOpenSoundDan;

    [SerializeField] private AudioClip[] hurtRecoilSoundFrei;
    [SerializeField] private AudioClip[] muchingFoodSoundFrei;
    [SerializeField] private AudioClip[] mouthIsOpenSoundFrei;

     private AudioClip[] hurtRecoilSound;
     private AudioClip[] muchingFoodSound;
     private AudioClip[] mouthIsOpenSound;

    [SerializeField] private AudioSource mouthAudioSource;
    private float originalMouthVolume;

    private Vector3 originalEyelidLRot;
    private Vector3 originalEyelidRRot;

    private bool isFrozen = false;

    [SerializeField] private UnityEvent onBossEaten;

    void Start()
    {
        originalEyelidLRot = eyeLidL.localEulerAngles;
        originalEyelidRRot = eyeRidL.localEulerAngles;

        originalLeftEyeMat = leftEyeRenderer.material;
        originalRightEyeMat = rightEyeRenderer.material;

        // choose a random from 1 to 3, and assign the corresponding audio arrays to the generic ones
        int characterVariant = Random.Range(1, 4);
        switch (characterVariant)
        {
            case 1:
                hurtRecoilSound = hurtRecoilSoundAnt;
                muchingFoodSound = muchingFoodSoundAnt;
                mouthIsOpenSound = mouthIsOpenSoundAnt;
                break;
            case 2:
                hurtRecoilSound = hurtRecoilSoundDan;
                muchingFoodSound = muchingFoodSoundDan;
                mouthIsOpenSound = mouthIsOpenSoundDan;
                break;
            case 3:
                hurtRecoilSound = hurtRecoilSoundFrei;
                muchingFoodSound = muchingFoodSoundFrei;
                mouthIsOpenSound = mouthIsOpenSoundFrei;
                break;
        }

        // Store the designer-set volume and ensure we start silent
        originalMouthVolume = mouthAudioSource.volume;
        mouthAudioSource.priority = 0;
        mouthAudioSource.volume = 0;
        mouthAudioSource.loop = true; // Ensure it loops for the chatter
        mouthAudioSource.playOnAwake = false; // We'll trigger it manually
        mouthAudioSource.Play();

        if(mouthIsOpenSound != null && mouthIsOpenSound.Length > 0)
            mouthAudioSource.clip = mouthIsOpenSound[Random.Range(0, mouthIsOpenSound.Length)];

        originalMouthRot = mouthBone.localEulerAngles;
        if (pivot != null) pivotInitialLocalPos = pivot.localPosition;

        ApplyRandomVisuals();

        CaptureMaterials();

        StartBlinking();
    }

    private void CaptureMaterials()
    {
        originalMaterialMap.Clear();
        Renderer[][] allGroups = { hair, shirt, skin };

        foreach (var group in allGroups)
        {
            foreach (Renderer r in group)
            {
                if (r != null && !originalMaterialMap.ContainsKey(r))
                    originalMaterialMap.Add(r, r.material);
            }
        }
    }

    void Update()
    {
        if (isFrozen) return; // Add this line
        HandleEyeTracking();
    }

    private void HandleEyeTracking()
    {
        if (leftEye == null || rightEye == null) return;

        // Optimization: Only search for the closest toast occasionally
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            FindClosestToast();
            searchTimer = searchInterval;
        }

        // Determine look target (Toast or Camera)
        Vector3 targetPos;
        if (currentEyeTarget != null)
        {
            targetPos = currentEyeTarget.position;
        }
        else if (Camera.main != null)
        {
            targetPos = Camera.main.transform.position;
        }
        else
        {
            return;
        }

        // Apply LookAt logic
        RotateEye(leftEye, targetPos);
        RotateEye(rightEye, targetPos);
    }

    [SerializeField] private float eyeClampAngle = 45f;

    private void RotateEye(Transform eye, Vector3 targetWorldPos)
    {
        Vector3 direction = targetWorldPos - eye.position;
        if (direction != Vector3.zero)
        {
            // 1. Calculate the "Perfect Tracking" rotation (World Space)
            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.forward);
            Quaternion targetWorldRot = lookRotation * Quaternion.Euler(90, 0, 0);

            // 2. Define the Rest Pose (-90, 0, 0 local) in World Space
            // We calculate this by taking the Parent's rotation and applying your offset
            Quaternion restWorldRot = eye.parent.rotation * Quaternion.Euler(-90, 0, 0);

            // 3. Calculate the angle between the Rest Pose and the Target Pose
            float angleDiff = Quaternion.Angle(restWorldRot, targetWorldRot);

            // 4. Apply Constraint
            if (angleDiff <= eyeClampAngle)
            {
                // If within 45 degrees, track perfectly
                eye.rotation = targetWorldRot;
            }
            else
            {
                // If it tries to go past 45 degrees, stop exactly at the limit (45 deg away from rest)
                eye.rotation = Quaternion.RotateTowards(restWorldRot, targetWorldRot, eyeClampAngle);
            }
        }
    }

    private void FindClosestToast()
    {
        // Find all potential toasts
        ToastBehavior[] allToasts = Object.FindObjectsByType<ToastBehavior>(FindObjectsSortMode.None);

        if (allToasts.Length == 0)
        {
            currentEyeTarget = null;
            return;
        }

        float closestDist = Mathf.Infinity;
        Transform closestTrans = null;

        foreach (var toast in allToasts)
        {
            // Check if the toast component exists and if it is puncheable
            if (toast != null && toast.isPunchable)
            {
                float dist = Vector3.Distance(transform.position, toast.transform.position);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestTrans = toast.transform;
                }
            }
        }

        // If no puncheable toasts were found, closestTrans will be null
        // Your HandleEyeTracking logic already handles looking at the camera if this is null
        currentEyeTarget = closestTrans;
    }


    public void Initialize(Transform seat, Sprite wantedJam)
    {
        mySeat = seat;
        targetPosition = transform.position;
        wantIcon.sprite = wantedJam;
        EnterScene();
    }

    public void SetWantSprite(Sprite wantedJam)
        {
        wantIcon.sprite = wantedJam;
    }

    private void EnterScene()
    {

        AudioManager.Instance.PlaySound(gettingUpSound, sfxMixer, transform.position);

        transform.position = targetPosition + Vector3.down * popUpDistance;
        transform.DOMove(targetPosition, entranceDuration).SetEase(Ease.Linear).OnComplete(() => {
            isSat = true;
            thoughtBubble.SetActive(true);
            StartRandomHop();
            StartWaitingForFood();
        });
    }

    private void StartRandomHop()
    {
        // 1. HARD GUARD: If the script or the pivot bone is gone, stop everything immediately.
        if (this == null || pivot == null || isSatisfied || isFrozen) return;

        float randomHeight = Random.Range(minHopHeight, maxHopHeight);
        float randomSpeed = Random.Range(minHopSpeed, maxHopSpeed);

        // 2. Kill any existing hop on the pivot before starting a new one
        pivot.DOKill();

        hopTween = pivot.DOLocalMoveY(pivotInitialLocalPos.y + randomHeight, randomSpeed)
            .SetEase(Ease.InOutQuad)
            .SetLink(pivot.gameObject) // 3. LINK: Automatically kills this tween if pivot is destroyed
            .OnComplete(() => {
                // 4. RE-CHECK: Ensure we are still alive before starting the downward move
                if (this == null || pivot == null) return;

                pivot.DOLocalMoveY(pivotInitialLocalPos.y, randomSpeed)
                    .SetEase(Ease.InOutQuad)
                    .SetLink(pivot.gameObject)
                    .OnComplete(() => {
                        // 5. RE-CHECK: Ensure we are still alive before looping back
                        if (this == null || pivot == null) return;
                        StartRandomHop();
                    });
            });
    }

    private void StartWaitingForFood()
    {
        if (mouthBone == null || isSatisfied) return;

        mouthBone.DOKill();

        mouthBone.DOLocalRotate(waitingMouthClosed, 1f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            CreateWaitingSequence();
        });
    }

    private void CreateWaitingSequence()
    {
        if (mouthBone == null || isSatisfied) return;

        float xOffset = Random.Range(-5f, 5f);
        Vector3 dynamicClosed = new Vector3(waitingMouthClosed.x + xOffset, waitingMouthClosed.y, waitingMouthClosed.z);
        Vector3 dynamicOpen = new Vector3(waitingMouthOpen.x + xOffset, waitingMouthOpen.y, waitingMouthOpen.z);

        Sequence waitSeq = DOTween.Sequence();
        int chatterCycles = Random.Range(3, 7);

        for (int i = 0; i < chatterCycles; i++)
        {
            // MOUTH OPEN + FADE IN SOUND
            waitSeq.Append(mouthBone.DOLocalRotate(dynamicOpen, waitingCycleDuration).SetEase(Ease.InOutSine));
            waitSeq.Join(mouthAudioSource.DOFade(originalMouthVolume, waitingCycleDuration));

            // MOUTH CLOSE + FADE OUT SOUND
            waitSeq.Append(mouthBone.DOLocalRotate(dynamicClosed, waitingCycleDuration).SetEase(Ease.InOutSine));
            waitSeq.Join(mouthAudioSource.DOFade(0, waitingCycleDuration));
        }

        if (Random.value > 0.5f)
        {
            waitSeq.Append(mouthBone.DOLocalRotate(originalMouthRot, 0.2f).SetEase(Ease.InOutQuad));
            waitSeq.Join(mouthAudioSource.DOFade(0, 0.2f)); // Ensure sound is off when pausing
            waitSeq.AppendInterval(0.5f);
            waitSeq.Append(mouthBone.DOLocalRotate(dynamicClosed, 0.2f).SetEase(Ease.InOutQuad));
        }

        waitSeq.OnComplete(() => {
            if (!isSatisfied) CreateWaitingSequence();
        });

        mouthTween = waitSeq;
    }

    public void Satisfy()
    {


        currentToastsEaten++;

        if(bossFill != null)
        {
            float fillAmount = (float)currentToastsEaten / toastsToSatisfy;
            bossFill.DOFillAmount(fillAmount, 0.3f).SetEase(Ease.OutQuad);
        }

        if (currentToastsEaten >= toastsToSatisfy)
        {
            isSatisfied = true;
            isSat = false;
        }

        if(ClientManager.Instance.isBossFight)
            SetWantSprite(ClientManager.Instance.GetSpriteFromJam(ClientManager.Instance.GetCurrentBossRequiredJam()));
    }

    private void StartBlinking()
    {
        float delay = Random.Range(2f, 4f);

        // Give the delayed call a specific ID so we can kill it in OnDestroy
        DOVirtual.DelayedCall(delay, () => {
            // The "this == null" check is vital for destroyed objects
            if (this == null || isFrozen || eyeLidL == null) return;

            eyeLidL.DOLocalRotate(new Vector3(-90, 0, 0), 0.1f);
            eyeRidL.DOLocalRotate(new Vector3(-90, 0, 0), 0.1f).OnComplete(() => {
                if (this == null || isFrozen) return;
                eyeLidL.DOLocalRotate(originalEyelidLRot, 0.1f);
                eyeRidL.DOLocalRotate(originalEyelidRRot, 0.1f);
                StartBlinking();
            });
        }).SetId("ClientBlink" + gameObject.GetInstanceID());
    }

    public void SetAngryEyelids()
    {
        // Kill any existing eyelid tweens (like blinking)
        eyeLidL.DOKill();
        eyeRidL.DOKill();

        leftEyeRenderer.material = redMaterial;
        rightEyeRenderer.material = redMaterial;

        // Snap to the angry/squint position
        eyeLidL.DOLocalRotate(new Vector3(-65, -9, 38), 0.15f).SetEase(Ease.OutBack);
        eyeRidL.DOLocalRotate(new Vector3(-65, -9, -38), 0.15f).SetEase(Ease.OutBack);
    }

    public void RestoreEyelids()
    {
        leftEyeRenderer.material = originalLeftEyeMat;
        rightEyeRenderer.material = originalRightEyeMat;

        // Smoothly return to the original cached rotations
        eyeLidL.DOLocalRotate(originalEyelidLRot, 0.3f).SetEase(Ease.OutQuad);
        eyeRidL.DOLocalRotate(originalEyelidRRot, 0.3f).SetEase(Ease.OutQuad);
    }

    public void SetOrder(string jamName, Color jamColor)
    {
        desiredCondiment = jamName;
        condimentColor = jamColor;

        TAG_Thought thought = GetComponentInChildren<TAG_Thought>();
        if (thought != null)
        {
            thought.GetComponent<Renderer>().material.SetColor("_BaseColor", condimentColor);
        }
    }

    public void OpenMouth()
    {
        mouthBone.DOLocalRotate(mouthOpenRotation, 0.15f)
                 .SetEase(Ease.OutQuad)
                 .SetLink(mouthBone.gameObject); // Safety link
    }

    public void PlayMunchAnimation(System.Action onComplete)
    {
        eyeToastL.SetActive(true);
        eyeToastR.SetActive(true);
        AudioManager.Instance.PlaySound(muchingFoodSound, puppetMixer, transform.position);

        Sequence munchSeq = DOTween.Sequence();

        for (int i = 0; i < numberOfBites; i++)
        {
            munchSeq.Append(mouthBone.DOLocalRotate(originalMouthRot, 0.08f).SetEase(Ease.Linear));
            munchSeq.Append(mouthBone.DOLocalRotate(mouthOpenRotation / 3f, 0.08f).SetEase(Ease.Linear));
        }

        munchSeq.Append(mouthBone.DOLocalRotate(originalMouthRot, 0.05f).SetEase(Ease.Linear));
        munchSeq.OnComplete(() => onComplete?.Invoke());
    }

    public void Recoil()
    {
        if (recoilBone != null)
        {
            mouthAudioSource.DOKill(); // Stop the active fade
            mouthAudioSource.volume = 0; // Instant silence

            recoilBone.DOKill();
            Sequence recoilSeq = DOTween.Sequence();
            recoilSeq.Append(recoilBone.DOLocalRotate(new Vector3(40, 0, 0), 0.1f).SetEase(Ease.OutBack));
            recoilSeq.Append(recoilBone.DOLocalRotate(Vector3.zero, 0.2f).SetEase(Ease.InQuad));
        }
    }

    public void HardRecoil()
    {
        if (recoilBone != null)
        {
            mouthAudioSource.DOKill(); // Stop the active fade
            mouthAudioSource.volume = 0; // Instant silence

            AudioManager.Instance.PlaySound(hurtRecoilSound, puppetMixer, transform.position);
            recoilBone.DOKill();
            Sequence hardRecoilSeq = DOTween.Sequence();

            hardRecoilSeq.Append(recoilBone.DOLocalRotate(new Vector3(70, 0, 0), 0.05f).SetEase(Ease.OutBack));
            hardRecoilSeq.Append(recoilBone.DOLocalRotate(Vector3.zero, 0.15f).SetEase(Ease.InQuad));

            transform.DOShakePosition(0.2f, 0.1f, 20);

            // --- HIT FLASH EFFECT ---
            ApplyFlashMaterial(redMaterial);

            // Change back after a short delay (e.g., 0.1 seconds)
            DOVirtual.DelayedCall(0.3f, () => {
                ResetMaterials();
            });
        }
    }

    private void ApplyFlashMaterial(Material flashMat)
    {
        if (flashMat == null) return;

        foreach (var entry in originalMaterialMap)
        {
            if (entry.Key != null) entry.Key.material = flashMat;
        }
    }

    private void ResetMaterials()
    {
        foreach (var entry in originalMaterialMap)
        {
            if (entry.Key != null) entry.Key.material = entry.Value;
        }
    }

    public void TryEatToast(string incomingJam, GameObject toast)
    {
        if (incomingJam == desiredCondiment)
        {
            OpenMouth();

            if (isSatisfied && !hasEaten)
            {
                ClientManager.Instance.AddClientAndUpdateUI();
                if (satisfiedIcon != null)
                    satisfiedIcon.SetActive(true);
            }
            DOVirtual.DelayedCall(0.4f, () => {
                if (toast != null)
                {
                    toast.transform.DOKill();
                    Destroy(toast);
                }

                

                PlayMunchAnimation(() => {
                    onBossEaten.Invoke();
                    eyeToastL.SetActive(false);
                    eyeToastR.SetActive(false);
                    if (isSatisfied && !hasEaten)
                        ReceiveFood();
                });
            });
        }
        else
        {
            transform.DOShakePosition(0.4f, new Vector3(0.2f, 0, 0), 10, 90);
        }
    }

    public void ReceiveFood()
    {
        hasEaten = true;
        thoughtBubble.GetComponent<HoverAndScale>().Descale();

        eyeToastL.SetActive(true);
        eyeToastR.SetActive(true);

        ClientManager.Instance.OnClientFinished();

        if (hopTween != null) hopTween.Kill();
        if (mouthTween != null) mouthTween.Kill();

        if (pivot != null)
        {
            pivot.DOKill();
            pivot.DOLocalRotate(Vector3.zero, 0.2f);
            pivot.DOLocalMove(pivotInitialLocalPos, 0.2f);
        }

        
        if (mySeat != null) ClientManager.Instance.ClearSeat(mySeat);
        ExitScene();
    }

    private void ExitScene()
    {

        AudioManager.Instance.PlaySound(goingDownSound, sfxMixer, transform.position);

        Sequence exitSeq = DOTween.Sequence();
        exitSeq.Append(transform.DOMoveY(targetPosition.y + 0.1f, 0.15f).SetEase(Ease.OutQuad));
        exitSeq.Append(transform.DOMoveY(targetPosition.y - popUpDistance, 0.4f).SetEase(Ease.InBack));
        exitSeq.OnComplete(() =>
        {
            transform.DOKill(true);
            Destroy(gameObject);
        });
    }

    public void FreezeClient()
    {
        isFrozen = true;

        // 1. Kill the chatter and the hops immediately
        if (mouthTween != null) mouthTween.Kill();
        if (hopTween != null) hopTween.Kill();

        // 2. Kill all active tweens on the bones to stop them mid-motion
        pivot.DOKill();
        mouthBone.DOKill();
        recoilBone.DOKill();
        eyeLidL.DOKill();
        eyeRidL.DOKill();

        // 3. Force the client into a "stunned" pose
        mouthBone.DOLocalRotate(waitingMouthClosed, 0.1f); // Shut the mouth

        // 4. Silence
        mouthAudioSource.Pause();
    }

    public void UnfreezeClient()
    {
        isFrozen = false;

        mouthAudioSource.UnPause();

        // Restart the loops
        if (!isSatisfied)
        {
            StartRandomHop();
            StartWaitingForFood();
            StartBlinking(); // Restart the blinking loop
        }
    }

    private void ApplyRandomVisuals()
    {

        if (!useRandomVisual) return;

        // 1. Check for Special Material Trigger
        if (Random.value < specialChance && specialMaterial != null)
        {
            ApplySpecialMaterial();
            return;
        }

        // 2. Standard Material Randomization
        if (materials.Length > 0)
        {
            // Pick one material for all skin and one for all shirt
            Material selectedSkinMat = materials[Random.Range(0, materials.Length)];
            Material selectedShirtMat = materials[Random.Range(0, materials.Length)];

            ApplyToGroup(skin, selectedSkinMat);
            ApplyToGroup(shirt, selectedShirtMat);

            // 3. Handle Hair (Check for Individual Randomization Trigger)
            if (Random.value < randomBaldChance)
            {
                // Bald client, disable all hair renderers
                foreach (Renderer r in hair)
                {
                    if (r != null) r.enabled = false;
                }
            }
            else if (Random.value < randomHairColor)
            {
                // Every hair piece gets a different random material
                foreach (Renderer r in hair)
                {
                    if (r != null) r.material = materials[Random.Range(0, materials.Length)];
                }
            }
            else
            {
                // All hair pieces share the same random material
                Material selectedHairMat = materials[Random.Range(0, materials.Length)];
                ApplyToGroup(hair, selectedHairMat);
            }
        }
    }

    private void OnDestroy()
    {
        // Kill every tween linked to this transform and its children
        transform.DOKill(true);

        // Kill any virtual delays (like the blinker) by using the unique ID
        DOTween.Kill("ClientBlink" + gameObject.GetInstanceID());
    }

    public void KillAllClientTweens()
    {
        // The 'true' parameter tells DOTween to also kill tweens 
        // on all children of this transform.
        transform.DOKill(true);

        // If you are using DOVirtual.DelayedCall (like in your StartBlinking method),
        // those aren't attached to a Transform. 
        // It's safer to also manually kill your stored tween references:
        if (hopTween != null) hopTween.Kill();
        if (mouthTween != null) mouthTween.Kill();

        // Note: If you want to stop EVERYTHING in the whole game, 
        // you would use DOTween.KillAll(); but that's usually overkill.
    }

    private void ApplySpecialMaterial()
    {
        // Create a unique instance so we don't change the project asset
        Material instance = new Material(specialMaterial);

        // Generate 3 random colors
        instance.SetColor("_SkinColor", Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f));
        instance.SetColor("_ShirtColor", Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f));
        instance.SetColor("_HairColor", Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f));

        // Apply this single material instance to every renderer
        ApplyToGroup(skin, instance);
        ApplyToGroup(shirt, instance);
        ApplyToGroup(hair, instance);
    }

    private void ApplyToGroup(Renderer[] group, Material mat)
    {
        if (group == null) return;
        foreach (Renderer r in group)
        {
            if (r != null) r.material = mat;
        }
    }
}