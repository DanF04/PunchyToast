using System.Collections; // Needed for Coroutines
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

public class EndlessModeManager : MonoBehaviour
{
    public static EndlessModeManager Instance;

    [Header("Easy Mode")]
    public bool easyMode = false;
    public float easyModeHoverTime = 10f;

    public int lossesAllowed = 5;
    public int lossesThisSession = 0;

    public int clientsSatisfiedThisSession = 0;
    public void AddASatisfiedClient()
    {
        clientsSatisfiedThisSession++;
        UpdateClientUI();
    }

    public void UpdateClientUI() => clientCounterText.text = $"{clientsSatisfiedThisSession}";

    [SerializeField] private AudioClip[] lossSounds;
    [SerializeField] private AudioMixer sfxMixer;

    [Header("GameOverUI")]
    public GameObject gameOverUI;
    public TextMeshProUGUI clientsSatisfied;
    public TextMeshProUGUI clientsSatisfiedUnderlay;
    public TextMeshProUGUI timeSurvived;
    public TextMeshProUGUI timeSurvivedUnderlay;
    public TextMeshProUGUI highestCombo;
    public TextMeshProUGUI highestComboUnderlay;
    public TextMeshProUGUI finalScore;
    public TextMeshProUGUI finalScoreUnderlay;

    private Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();

    [Header("End Screen Sequence")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private float timeBetweenTicks = 0.05f;
    [SerializeField] private float pitchIncreasePerTick = 0.02f;
    [SerializeField] private float maxPitch = 2.0f;

    private float timeSurvivedThisSession = 0f;

    [SerializeField] private GameObject clientCounterUI;
    [SerializeField] private TextMeshProUGUI clientCounterText;
    [SerializeField] private GameObject comboCounterUI;

    [SerializeField] private GameObject[] mistakesCounter;

    public void ResetMistakeCounter()    {
        lossesThisSession = 0;
        foreach (GameObject obj in mistakesCounter)
            obj.SetActive(false);

        clientCounterText.text = "0";

        timeSurvivedThisSession = 0f;
        clientsSatisfiedThisSession = 0;
    }

    public void AddALoss()
    {
        lossesThisSession++;

        AudioManager.Instance.PlaySound(lossSounds, sfxMixer);

        mistakesCounter[lossesThisSession - 1].SetActive(true);

        if (lossesThisSession >= lossesAllowed)
        {
            // Game over logic here. For now, just stop spawning.
            StopEndlessMode();
            ClientManager.Instance.FullResetGame();
            TriggerEndScreen();
        }
    }

    public void TriggerEndScreen()
    {
        gameOverUI.SetActive(true);
        // Start the rolling sequence instead of setting values instantly
        StartCoroutine(RollEndScreenValues());
    }

    private IEnumerator RollEndScreenValues()
    {
        // 1. Initialize UI: Set text to zero and scale to 0
        ResetUIElement(clientsSatisfied.transform.parent);
        ResetUIElement(highestCombo.transform.parent);
        ResetUIElement(timeSurvived.transform.parent);
        ResetUIElement(finalScore.transform.parent);

        clientsSatisfied.text = ": 0";
        clientsSatisfiedUnderlay.text = ": 0";
        highestCombo.text = ": 0";
        highestComboUnderlay.text = ": 0";
        timeSurvived.text = ": 00m:00s";
        timeSurvivedUnderlay.text = ": 00m:00s";
        finalScore.text = ": 0";
        finalScoreUnderlay.text = ": 0";

        float currentPitch = 1.0f;

        // --- PART C: Roll Time Survived ---
        yield return AnimateEntry(timeSurvived.transform.parent);
        int totalSecs = Mathf.FloorToInt(timeSurvivedThisSession);
        for (int i = 0; i <= totalSecs; i++)
        {
            int mins = i / 60;
            int secs = i % 60;
            UpdateText(timeSurvived, timeSurvivedUnderlay, $": {mins:00}m:{secs:00}s");

            PlayTick(ref currentPitch);
            float dynamicWait = totalSecs > 60 ? timeBetweenTicks * 0.3f : timeBetweenTicks;
            yield return new WaitForSeconds(dynamicWait);
        }

        currentPitch = 1.0f;

        // --- PART A: Roll Clients Satisfied ---
        yield return AnimateEntry(clientsSatisfied.transform.parent);
        for (int i = 0; i <= clientsSatisfiedThisSession; i++)
        {
            UpdateText(clientsSatisfied, clientsSatisfiedUnderlay, $": {i}");
            PlayTick(ref currentPitch);
            yield return new WaitForSeconds(timeBetweenTicks);
        }

        currentPitch = 1.0f;

        // --- PART B: Roll Highest Combo ---
        yield return AnimateEntry(highestCombo.transform.parent);
        int targetCombo = Toaster.Instance.highestCombo;
        for (int i = 0; i <= targetCombo; i++)
        {
            UpdateText(highestCombo, highestComboUnderlay, $": {i}");
            PlayTick(ref currentPitch);
            yield return new WaitForSeconds(timeBetweenTicks);
        }

        // --- PART D: Roll Final Score ---
        yield return AnimateEntry(finalScore.transform.parent);
        int finalScoreValue = clientsSatisfiedThisSession + Toaster.Instance.highestCombo;
        for (int i = 0; i <= finalScoreValue; i++)
        {
            UpdateText(finalScore, finalScoreUnderlay, $": {i}");
            PlayTick(ref currentPitch);
            yield return new WaitForSeconds(timeBetweenTicks);
        }


        // TP_INFINITE

        switch(finalScoreValue)
        {
            case 50:
                //TP_INFINITEFIFTY
                break;
            case 100:
                //TP_INFINITEHUNDRED
                break;
            case 200:
                //TP_INFINITETWOHUNDRED
                break;
        }


    }

    private void ResetUIElement(Transform t)
    {
        if (t != null) t.localScale = Vector3.zero;
    }

    private IEnumerator AnimateEntry(Transform t)
    {
        if (t != null && originalScales.ContainsKey(t))
        {
            // Scale up to the original Inspector value
            t.DOScale(originalScales[t], 0.4f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void UpdateText(TextMeshProUGUI main, TextMeshProUGUI underlay, string text)
    {
        main.text = text;
        underlay.text = text;
    }

    private void PlayTick(ref float pitch)
    {
        if (tickSound == null) return;
        AudioManager.Instance.PlaySoundFixedPitch(tickSound, pitch, sfxMixer);
        pitch = Mathf.Min(pitch + pitchIncreasePerTick, maxPitch);
    }

    [Header("Difficulty Thresholds (clients spawned)")]
    public int tier2At = 5;
    public int tier3At = 12;
    public int tier4At = 22;

    [Header("Difficulty Curve - Hover Time")]
    public float startingHoverTime = 4f;
    public float minimumHoverTime = 0.6f;
    public float hoverTimeDecreasePerClient = 0.05f;

    [Header("Difficulty Curve - Slappable Chance")]
    public float slappableChanceStart = 0f;
    public float slappableChanceMax = 0.25f;

    [Header("Difficulty Curve - Simultaneous Chance")]
    public float simultaneousChanceStart = 0f;
    public float simultaneousChanceMax = 0.5f;

    [Header("Client Spawn Cooldown")]
    public float normalCooldown = 3f;
    public float minCooldown = 0.2f;
    public float cooldownDecreasePerClient = 0.05f;
    public float emptyCooldown = 0.8f;
    private float currentCooldown;

    [Header("Endless Extra Logic")]
    [Range(0f, 1f)] public float instantDoubleSpawnChance = 0.2f; // 20% chance to spawn another immediately

    [Header("Slap Words")]
    public string[] possibleSlapWords = { "SLAP", "POW", "BAM", "ZAP" };

    [Header("Jam Unlock Order")]
    public List<JamFlavor> jamUnlockOrder = new List<JamFlavor>();

    public bool isRunning = false;
    private int clientsSpawned = 0;
    private float justSpawnedClient = -Mathf.Infinity;
    // Tracks whether we have spawned at least one client this session,
    // so the empty cooldown only kicks in after the first client leaves,
    // not on startup (where we want to spawn immediately)

    void Awake()
    {
        if (Instance == null) Instance = this;
        CaptureScale(clientsSatisfied.transform.parent);
        CaptureScale(highestCombo.transform.parent);
        CaptureScale(timeSurvived.transform.parent);
        CaptureScale(finalScore.transform.parent);
    }

    private void CaptureScale(Transform t)
    {
        if (t != null && !originalScales.ContainsKey(t))
            originalScales[t] = t.localScale;
    }

    public void HideUI()
    {
        clientCounterUI.SetActive(false);
        comboCounterUI.SetActive(false);
    }

    public void StartEndlessMode()
    {
        clientCounterUI.SetActive(true);
        comboCounterUI.SetActive(true);

        ResetMistakeCounter();

        Toaster.Instance.ResetCombo();
        Toaster.Instance.ResetHighestCombo();

        isRunning = true;
        timeSurvivedThisSession = 0f;
        clientsSpawned = 0;

        SetupJamsForTier();
        ClientManager.Instance.levelConfig = null;
        ClientManager.Instance.ResetWordState();
    }

    public void StopEndlessMode()
    {
        isRunning = false;
        clientCounterUI.SetActive(false);
        comboCounterUI.SetActive(false);
    }

    void Update()
    {
        if (!isRunning) return;

        timeSurvivedThisSession += Time.deltaTime;

        if (ClientManager.Instance.areThereActiveClients)
            currentCooldown = normalCooldown;
        else
            currentCooldown = Mathf.Min(normalCooldown, emptyCooldown);

        if (justSpawnedClient + currentCooldown > Time.time) return;
        if (!ClientManager.Instance.HasAvailableSeat()) return;

        // Only block on "are there clients" AFTER the first spawn,
        // and only to apply the correct cooldown — not to gate spawning entirely.
        // The actual gate is: did enough time pass?
        // That time was already set correctly when we scheduled justSpawnedClient.

        SetupJamsForTier();
        SpawnEndlessClient();
    }

    private float GetCurrentHoverTime()
    {
        if (easyMode) return easyModeHoverTime;
        float t = startingHoverTime - (clientsSpawned * hoverTimeDecreasePerClient);
        return Mathf.Max(t, minimumHoverTime);
    }

    private float GetCurrentDriftFactor()
    {
        // goes from 0.05 to 0.3 the same way currenthover time scales, with a max of 0.3
        float t = 0.05f + (0.25f * (1 - (GetCurrentHoverTime() - minimumHoverTime) / (startingHoverTime - minimumHoverTime)));
        return Mathf.Min(t, 0.3f);

    }

    private float GetCurrentSlappableChance()
    {
        return Mathf.Lerp(slappableChanceStart, slappableChanceMax,
                          Mathf.Clamp01((float)clientsSpawned / tier4At));
    }

    public float GetCurrentSimultaneousChance()
    {
        return Mathf.Lerp(simultaneousChanceStart, simultaneousChanceMax,
                          Mathf.Clamp01((float)clientsSpawned / tier4At));
    }

    private void SetupJamsForTier()
    {
        int jamCount;
        if (clientsSpawned >= tier4At) jamCount = 4;
        else if (clientsSpawned >= tier3At) jamCount = 3;
        else if (clientsSpawned >= tier2At) jamCount = 2;
        else jamCount = 1;

        jamCount = Mathf.Min(jamCount, jamUnlockOrder.Count);

        HashSet<JamFlavor> flavors = new HashSet<JamFlavor>();
        for (int i = 0; i < jamCount; i++)
            flavors.Add(jamUnlockOrder[i]);

        JamDecider.Instance.SetupLevelJams(flavors);
    }

    private void SpawnEndlessClient()
    {
        var activeJams = JamDecider.Instance.activeJams;
        bool makeSlappable = Random.value < GetCurrentSlappableChance();

        LevelConfiguration.ClientData data = new LevelConfiguration.ClientData(true);
        data.jamFlavor = activeJams[Random.Range(0, activeJams.Count)].flavor;
        data.simultaneousToast = false;
        data.isSlappable = makeSlappable;
        data.toastsNeeded = 1;

        if (makeSlappable)
        {
            data.slapString = possibleSlapWords[Random.Range(0, possibleSlapWords.Length)];
            data.customLetter = "";
        }
        else
        {
            data.customLetter = ((char)('A' + Random.Range(0, 26))).ToString();
        }

        

        Toaster.Instance.hoverTime = GetCurrentHoverTime();
        Toaster.Instance.driftFactor = GetCurrentDriftFactor();
        if (makeSlappable)
            Toaster.Instance.driftFactor = 0f;

        ClientManager.Instance.SpawnEndlessClient(data);
        clientsSpawned++;

        normalCooldown -= cooldownDecreasePerClient;
        normalCooldown = Mathf.Max(normalCooldown, minCooldown);

        justSpawnedClient = Time.time;

        // --- NEW LOGIC: Instant Double Spawn Chance ---
        if (Random.value < instantDoubleSpawnChance && ClientManager.Instance.HasAvailableSeat())
        {
            // We call this again. Because justSpawnedClient is already updated, 
            // the Update loop won't trigger a third one until the cooldown passes.
            SpawnEndlessClient();
        }
    }
}
