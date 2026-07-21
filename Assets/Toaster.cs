using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Toaster : MonoBehaviour
{
    public GameObject toastPrefab;
    public Transform ejectPoint;
    public static Toaster Instance;

    [Header("Punishment Sequence Timings")]
    public float impactPauseDuration = 1.0f;     // The "Sticky" pause
    public float toastSlideDuration = 3.0f;      // Total slide time
    public float secondaryZoomDelay = 2.0f;      // When the zoom starts during the slide
    public float initialZoomSpeed = 0.25f;      // Speed of the first zoom
    public float secondaryZoomSpeed = 0.6f;     // Speed of the second zoom

    [Header("Punch Game References")]
    public List<Transform> targets;
    public GameObject armPrefab;

    [SerializeField] public float punishmentCooldown = 4.0f;
    private float? nextLaunchOverride = null; // Nullable float to track if we have a punishment pending
    [SerializeField] private float timeToLaunchToast = 2f;
    private float lastLaunchTime = 0f;

    [Header("Launch Physics")]
    public float upForce = 9f;
    public float xSpread = 1.5f;
    public float zSpread = 0.5f;
    [SerializeField] private float simultaneousStep = 0.25f; // Gap between toasts

    [Header("Hover Logic")]
    public float hoverTime = 2.5f;
    public float minPreHoverDelay = 0.1f;
    public float maxPreHoverDelay = 0.4f;
    public float driftFactor = 0.2f;

    [Header("Bobbing")]
    public float bobAmount = 0.2f;
    public float bobSpeed = 0.4f;

    [Header("Punch Settings")]
    public bool debugAlwaysL = true;
    public float armSpawnOffset = 4f;
    public float armPunchDuration = 0.15f;
    public float targetFlightForce = 25f;
    [SerializeField] private float toastFlightDuration = 0.2f;

    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeStrength = 0.3f;
    [SerializeField] private int shakeVibrato = 20;

    [SerializeField] private AudioSource ding;
    [SerializeField] private AudioSource popUp;
    [SerializeField] private Toggle easyModeToggle;
    public bool easyMode = false;

    public List<ToastBehavior> activeToasts = new List<ToastBehavior>();

    // Tracking for spread logic
    private int simultaneousCount = 0;
    private int simultaneousIndex = 0;

    public AudioMixer sfxMixer;
    public AudioClip[] punchSounds;
    public AudioClip[] toastGettingIntoMouth;
    public AudioClip[] toastFlying;
    public AudioClip[] toastLandingNaturally;

    public AudioClip toastComboSound;
    public AudioClip toastFinalComboSound;

    [SerializeField] private GameObject fireLvl1;
    [SerializeField] private GameObject fireLvl2;
    [SerializeField] private GameObject fireLvl3;

    [Header("Combo UI & VFX")]
    [SerializeField] private RectTransform comboTextParent;
    [SerializeField] private TextMeshProUGUI comboTextMain;   // Drag your main text here
    [SerializeField] private TextMeshProUGUI comboTextShadow; // Drag your shadow text here
    [SerializeField] private GameObject comboFireParticle;   // Drag your fire particle here
    [SerializeField] private float comboBounceForce = 1.2f;
    [SerializeField] private float comboShakeStrength = 10f;

    public int currentCombo = 0;
    public int highestCombo = 0;

    private Vector3 originalParentScale;
    private Vector3 originalParentPos;
    private Vector3 originalComboScale;

    [SerializeField] private GameObject punchHitEffect;

    [Header("Cinematic Settings")]
    public Camera cinematicCamera; // Assign a side-view camera in the Inspector (disabled by default)
    public Camera defaultCamera;

    public Vector3 originalCameraPosition = Vector3.zero;
    public Vector3 originalCameraRotation = Vector3.zero;
    public float originalCameraFOV = 60f;

    public Vector3 cinematicCameraPosition = Vector3.zero;
    public Vector3 cinematicCameraRotation = Vector3.zero;
    public float cinematicCameraFOV = 60f;

    public float GetComboPitch()
    {
        if (currentCombo >= 1)
        {
            float pitch = 1f + (currentCombo * 0.1f);
            return Mathf.Min(pitch, 1.7f); // Cap the pitch increase at 2x
        }
        else
        {
            return -1f; // Normal pitch when no combo
        }
    }



    public void IncrementCombo()
    {
        currentCombo++;

        if (currentCombo > highestCombo)
        {
            highestCombo = currentCombo;
        }

        UpdateComboUI();
    }

    public void ResetCombo()
    {
        currentCombo = 0;
        comboTextParent.gameObject.SetActive(false);
        UpdateComboUI();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (comboTextParent != null)
        {
            originalParentScale = comboTextParent.localScale;
            originalParentPos = comboTextParent.localPosition;
        }


        UpdateComboUI();
    }

    private void Start()
    {
        originalCameraPosition = defaultCamera.transform.position;
        originalCameraRotation = defaultCamera.transform.rotation.eulerAngles;
        originalCameraFOV = defaultCamera.fieldOfView;

        cinematicCameraPosition = cinematicCamera.transform.position;
        cinematicCameraRotation = cinematicCamera.transform.rotation.eulerAngles;
        cinematicCameraFOV = cinematicCamera.fieldOfView;
    }

    void Update()
    {
        // Use the override if it exists, otherwise use the normal level time
        float currentCooldown = nextLaunchOverride ?? timeToLaunchToast;
        if (Time.time - lastLaunchTime >= currentCooldown && ClientManager.Instance.areThereClients)
        {
            if (!AreTherePunchableToasts())
            {
                nextLaunchOverride = null; // Reset the punishment so the next one is back to normal
                LaunchToast();
            }
        }
    }

    private void UpdateComboUI()
    {
        bool hasCombo = currentCombo > 0;

        if (comboTextParent != null) comboTextParent.gameObject.SetActive(hasCombo);

        if (comboFireParticle != null)
            comboFireParticle.SetActive(currentCombo >= 7);

        if (hasCombo)
        {
            string comboStr = "x" + currentCombo;
            if (comboTextMain != null) comboTextMain.text = comboStr;
            if (comboTextShadow != null) comboTextShadow.text = comboStr;

            if (comboTextParent != null)
            {
                comboTextParent.DOKill();

                comboTextParent.localScale = originalParentScale;
                comboTextParent.localPosition = originalParentPos;

                comboTextParent.DOPunchScale(originalParentScale * (comboBounceForce - 1f), 0.2f, 5, 1);

                comboTextParent.DOShakePosition(0.2f, comboShakeStrength);
            }
        }
    }

    public bool AreTherePunchableToasts()
    {
        activeToasts.RemoveAll(t => t == null || !t.isPunchable);
        return activeToasts.Count > 0;
    }

    public void ResetHighestCombo() => highestCombo = 0;

    public void SetupToasterSettings(LevelConfiguration config)
    {
        this.hoverTime = config.hoverTime;
        this.minPreHoverDelay = config.minPreHoverDelay;
        this.maxPreHoverDelay = config.maxPreHoverDelay;
        this.driftFactor = config.driftFactor;
        ResetCameraAngle();
    }

    public void ResetCameraAngle()
    {
        if (defaultCamera != null)
        {
            defaultCamera.transform.position = originalCameraPosition;
            defaultCamera.transform.rotation = Quaternion.Euler(originalCameraRotation);
            defaultCamera.fieldOfView = originalCameraFOV;

            defaultCamera.gameObject.SetActive(true);
            if (cinematicCamera != null)
                cinematicCamera.gameObject.SetActive(false);
        }

        if (cinematicCamera != null)
        {
            cinematicCamera.transform.position = cinematicCameraPosition;
            cinematicCamera.transform.rotation = Quaternion.Euler(cinematicCameraRotation);
            cinematicCamera.fieldOfView = cinematicCameraFOV;
        }
    }

    public void LaunchToast(string customLetter = "", bool summonFromSimul = false)
    {
        if (!ClientManager.Instance.areThereClients) return;

        // Spread Calculation Logic
        float currentXSpread;
        bool isSimul = ClientManager.Instance.GetSimultaneousStatusForNextToast();

        Debug.Log($"Launching toast. Simultaneous: {isSimul}, Current Simultaneous Count: {simultaneousCount}, Current Simultaneous Index: {simultaneousIndex}");

        if (isSimul || simultaneousCount > 0)
        {
            // If this is the start of a burst, find out how many are coming
            if (simultaneousCount == 0)
            {
                simultaneousCount = ClientManager.Instance.GetSimultaneousBurstCount();
                simultaneousIndex = 0;
            }

            // Calculate centered position
            // Formula: (Index - (Total-1)/2) * Step
            float offset = (simultaneousIndex - (simultaneousCount - 1) * 0.5f) * simultaneousStep;
            currentXSpread = offset;

            simultaneousIndex++;
        }
        else
        {
            // Normal random spread
            currentXSpread = Random.Range(-xSpread, xSpread);
            simultaneousCount = 0;
            simultaneousIndex = 0;
        }

        ding.Play();
        popUp.Play();
        transform.DOComplete();
        transform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato);

        lastLaunchTime = Time.time;

        GameObject toast = Instantiate(toastPrefab, ejectPoint.position, ejectPoint.rotation);
        Rigidbody rb = toast.GetComponent<Rigidbody>();
        ToastBehavior behavior = toast.AddComponent<ToastBehavior>();


        activeToasts.Add(behavior);
        behavior.isSimultaneous = isSimul;

        TAG_ToastMesh toastMesh = toast.GetComponentInChildren<TAG_ToastMesh>();
        if (toastMesh != null)
        {
            Renderer rend = toastMesh.GetComponent<Renderer>();
            if (rend != null)
            {
                Color baseColor = rend.material.GetColor("baseColorFactor");
                float h, s, v;
                Color.RGBToHSV(baseColor, out h, out s, out v);
                s = Random.Range(0f, 100f) / 100f;
                rend.material.SetColor("baseColorFactor", Color.HSVToRGB(h, s, v));
            }
        }


        behavior.impactPauseDuration = impactPauseDuration;
        behavior.toastSlideDuration = toastSlideDuration;
        behavior.secondaryZoomDelay = secondaryZoomDelay;
        behavior.initialZoomSpeed = initialZoomSpeed;
        behavior.secondaryZoomSpeed = secondaryZoomSpeed;

        behavior.potentialTargets = targets;
        behavior.armPrefab = armPrefab;
        behavior.flightDuration = toastFlightDuration;
        behavior.hoverDuration = easyModeToggle.isOn ? 600f : hoverTime;
        behavior.driftFactor = easyModeToggle.isOn ? 0f : driftFactor;
        behavior.bobAmount = bobAmount;
        behavior.bobSpeed = bobSpeed;
        behavior.preHoverDelay = Random.Range(minPreHoverDelay, maxPreHoverDelay);
        behavior.debugAlwaysL = debugAlwaysL;
        behavior.armSpawnOffset = armSpawnOffset;
        behavior.armPunchDuration = armPunchDuration;
        behavior.targetFlightForce = targetFlightForce;
        behavior.armShrinkDuration = 0.6f;

        behavior.summonedFromSimul = summonFromSimul;

        behavior.sfxMixer = sfxMixer;

        behavior.slapSpinDuration = 0.4f;

        behavior.punchEffect = punchHitEffect;

        behavior.originalCameraPosition = originalCameraPosition;
        behavior.originalCameraRotation = originalCameraRotation;

        behavior.punchSounds = punchSounds;
        behavior.toastGettingIntoMouth = toastGettingIntoMouth;
        behavior.toastFlying = toastFlying;
        behavior.toastLandingNaturally = toastLandingNaturally;

        toast.transform.eulerAngles += new Vector3(Random.Range(-15f, 15f), Random.Range(-15f, 15f), Random.Range(-15f, 15f));

        // Apply the calculated spread here
        rb.AddForce(new Vector3(currentXSpread, Random.Range(upForce - 0.25f, upForce), Random.Range(-zSpread, zSpread)), ForceMode.Impulse);

        if (isSimul)
        {
            ClientManager.Instance.PrepareNextLetterForSimultaneous();
            LaunchToast(summonFromSimul: true);
        }
        else
        {
            // Reset for next normal launch
            simultaneousCount = 0;
            simultaneousIndex = 0;
        }

        if (EndlessModeManager.Instance.isRunning)
        {
            float randomChance = Random.Range(0f, 1f);
            if (randomChance <= EndlessModeManager.Instance.GetCurrentSimultaneousChance())
            {
                if (ClientManager.Instance.notSatisfiedClientCount > activeToasts.Count)
                    LaunchToast();
            }
        }
    }

    public void UnregisterToast(ToastBehavior toast) => activeToasts.Remove(toast);

    public void TriggerPunishmentCooldown()
    {
        lastLaunchTime = Time.time;
        nextLaunchOverride = punishmentCooldown;
    }
}