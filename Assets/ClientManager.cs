using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.Rendering.STP;

[System.Serializable]
public class WeightedClient
{
    public GameObject prefab;
    [Range(0, 100)] public float spawnWeight; // Base chance

    [Header("Flavor Preference")]
    public List<JamFlavor> favoriteFlavors;
    public float preferenceMultiplier = 2.0f; // How much to boost weight if match found
}

public class ClientManager : MonoBehaviour
{
    public static ClientManager Instance;

    [Header("Level Configuration")]
    public LevelConfiguration levelConfig;
    private int currentWaveIndex = 0;
    private int clientsFinishedInWave = 0;
    private int totalClientsInLevel = 0;
    private int totalClientsSatisfied = 0;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI maxClientsText;
    [SerializeField] private TextMeshProUGUI satisfiedClientsText;

    [Header("Setup")]
    public List<WeightedClient> clientPrefabs;
    public List<Transform> seatingPositions;
    private Dictionary<Transform, Client> activeClients = new Dictionary<Transform, Client>();
    public bool areThereActiveClients => activeClients.Count > 0;
    public int activeClientCount => activeClients.Count;
    public int notSatisfiedClientCount => activeClients.Values.Count(c => !c.isSatisfied);
    public bool areThereClients => activeClients.Values.Count(c => c.isSat) > 0;

    private string currentWord = "";
    private List<int> availableIndexes = new List<int>();
    private int currentIndex = 0;
    private int toastsNeededInCurrentLetter = 0;
    private float levelStartTime;
    private bool levelFinished = false;

    private List<JamFlavor> jamFlavorsInCurrentLevel = new List<JamFlavor>();

    private List<string> slapWords = new List<string>();

    // Add this field alongside currentWord, availableIndexes, slapWords:
    private List<bool> simultaneousFlags = new List<bool>();

    [SerializeField] private LevelComplete levelCompleteUI;
    [SerializeField] private GameObject clientCounterUI;
    [SerializeField] private GameObject comboCounterUI;

    [SerializeField] private Sprite butterIcon;
    [SerializeField] private Sprite strawberryIcon;
    [SerializeField] private Sprite grapeIcon;
    [SerializeField] private Sprite chocolateIcon;

    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private AudioClip[] levelCompleteSound;

    public bool isBossFight => levelConfig != null && levelConfig.isBossFight;
    private Client bossClient;

    void Awake() { if (Instance == null) Instance = this; }

    public void StartLevel(LevelConfiguration config)
    {
        levelStartTime = Time.time;
        levelConfig = config;
        currentWaveIndex = 0;
        totalClientsSatisfied = 0;
        levelFinished = false;
        Toaster.Instance.SetupToasterSettings(config);
        Toaster.Instance.ResetCombo();
        clientCounterUI.SetActive(true);
        comboCounterUI.SetActive(true);

        Toaster.Instance.ResetHighestCombo();

        CalculateTotalLevelClients();
        UpdateUI();
        ConfigureJamsForLevel();
        StartCoroutine(SpawnWave(currentWaveIndex));
    }

    public void PrepareNextLetterForSimultaneous()
    {
        // This allows the Toaster to "consume" the next letter index 
        // immediately during a simultaneous burst
        currentIndex++;
    }

    public void HandleSimultaneousFailure()
    {
        Debug.Log("Simultaneous toast failed! Rolling back to previous letter index.");
        currentIndex--;
    }

    private void CalculateTotalLevelClients()
    {
        if(isBossFight)
        {
            // In a boss fight, we consider all clients across all waves as part of the level total, since they spawn together
            totalClientsInLevel = 1;
        }
        else
            totalClientsInLevel = levelConfig.waves.Sum(w => w.clientsInWave.Count);
    }

    private void UpdateUI()
    {

        if (maxClientsText != null) maxClientsText.text = totalClientsInLevel.ToString();
        if (satisfiedClientsText != null) satisfiedClientsText.text = totalClientsSatisfied.ToString();
    }

    void ConfigureJamsForLevel()
    {
        HashSet<JamFlavor> neededFlavors = new HashSet<JamFlavor>();
        foreach (var wave in levelConfig.waves)
            foreach (var client in wave.clientsInWave)
            {
                if (client.jamFlavor == JamFlavor.Random) { neededFlavors.Add(JamFlavor.Butter); neededFlavors.Add(JamFlavor.StrawberryJam); }
                else if (client.jamFlavor != JamFlavor.None) neededFlavors.Add(client.jamFlavor);
            }
        JamDecider.Instance.SetupLevelJams(neededFlavors);
    }

    public bool GetSimultaneousStatusForNextToast()
    {
        // Endless mode: read from our own flags list
        if (levelConfig == null)
        {
            if (currentIndex < simultaneousFlags.Count)
                return simultaneousFlags[currentIndex];
            return false;
        }

        // Normal mode: existing code unchanged
        if (currentWaveIndex >= levelConfig.waves.Count) return false;

        var wave = levelConfig.waves[currentWaveIndex];

        int dataIndex = Mathf.Clamp(currentIndex, 0, wave.clientsInWave.Count - 1);

        Debug.Log($"Checking simultaneous status for index {currentIndex} (data index {dataIndex}) in wave {currentWaveIndex}. Simultaneous: {wave.clientsInWave[dataIndex].simultaneousToast}");

        return wave.clientsInWave[dataIndex].simultaneousToast;
    }

    public bool IsLastClientOfWave()
    {
        if (levelConfig == null || currentWaveIndex >= levelConfig.waves.Count) return false;
        int progress = clientsFinishedInWave + activeClients.Values.Count(c => c.isSatisfied);
        return progress >= levelConfig.waves[currentWaveIndex].clientsInWave.Count;
    }

    IEnumerator SpawnWave(int index)
    {
        if (index >= levelConfig.waves.Count) {  yield break; }
        if (index > 0) yield return new WaitForSeconds(0.2f);

        clientsFinishedInWave = 0;
        var wave = levelConfig.waves[index];
        currentWord = "";
        currentIndex = 0;
        availableIndexes = new List<int>();
        slapWords = new List<string>();

        foreach (var clientData in wave.clientsInWave)
        {
            while (activeClients.Count >= seatingPositions.Count) yield return null;

            if(!isBossFight)
                SpawnClient(clientData, wave); // Pass wave data here
            else if (activeClientCount == 0)
            {
                SpawnClient(clientData, wave);
            }
                yield return new WaitForSeconds(0.1f);
        }
    }

    public string GetSlapWordForIndex(int index)
    {
        return index < slapWords.Count ? slapWords[index] : null;
    }

    public char GetCurrentLetter()
    {
        if (currentIndex < currentWord.Length) return char.ToUpper(currentWord[currentIndex]);

        return (char)('A' + Random.Range(0, 26));
    }

    public bool HasAvailableSeat()
    {
        return activeClients.Count < seatingPositions.Count;
    }

    public void ResetWordState()
    {
        currentWord = "";
        currentIndex = 0;
        availableIndexes.Clear();
        slapWords.Clear();
        simultaneousFlags.Clear(); // add this line
        clientsFinishedInWave = 0;
    }

    public int GetAvailableIndex()
    {
        // Ensure the list of active toasts is clean (no nulls)
        var activeToasts = Toaster.Instance.activeToasts.Where(t => t != null).ToList();

        for (int i = 0; i < availableIndexes.Count; i++)
        {
            int targetIndex = availableIndexes[i];

            // Check if ANY toast in the air is already "carrying" this index
            bool isIndexBusy = activeToasts.Any(toast => toast.myLetterIndex == targetIndex);

            // If NO toast is using this index, it's available!
            if (!isIndexBusy)
            {
                return targetIndex;
            }
        }

        // All currently available indexes are busy in the air
        return -1;
    }

    public void RemoveIndex(int i) => availableIndexes.Remove(i);

    public string GetCurrentWord()
        => currentWord;

    public void IncreaseLetterIndex()
    {

        if(levelConfig == null)
        {
            currentIndex++;
            return;
        }

        Debug.Log($"Increasing letter index. Current: {currentIndex}, Word Length: {currentWord.Length}");
        toastsNeededInCurrentLetter++;

        var wave = levelConfig.waves[currentWaveIndex];

        int dataIndex = Mathf.Clamp(currentIndex, 0, wave.clientsInWave.Count - 1);
        
        

        if(toastsNeededInCurrentLetter >= wave.clientsInWave[dataIndex].toastsNeeded)
        {
                       toastsNeededInCurrentLetter = 0;
            currentIndex++;
        }
        
    }


    public int GetSimultaneousBurstCount()
    {
        // Endless mode: look ahead in simultaneousFlags
        if (levelConfig == null)
        {
            int count = 1;
            int checkIndex = currentIndex;
            while (checkIndex < simultaneousFlags.Count && simultaneousFlags[checkIndex])
            {
                count++;
                checkIndex++;
            }
            return count;
        }

        // Normal mode: existing code unchanged
        if (currentWaveIndex >= levelConfig.waves.Count) return 1;
        var wave = levelConfig.waves[currentWaveIndex];
        int cnt = 1;
        int ci = currentIndex;
        while ((ci < wave.clientsInWave.Count || isBossFight) && wave.clientsInWave[ci].simultaneousToast)
        {
            cnt++;
            ci++;
        }
        return cnt;
    }

    public void SpawnEndlessClient(LevelConfiguration.ClientData data)
    {
        LevelConfiguration.Wave dummyWave = new LevelConfiguration.Wave(true);
        SpawnClient(data, dummyWave);
    }

    void SpawnClient(LevelConfiguration.ClientData data, LevelConfiguration.Wave wave)
    {
        List<Transform> availableSeats = new List<Transform>();
        for (int i = 0; i < seatingPositions.Count; i++)
        {
            if (activeClients.ContainsKey(seatingPositions[i])) continue;

            bool isBottom = i < 3;
            // Checking the row permissions from the specific wave passed in
            if (isBottom && wave.allowBottomRow) availableSeats.Add(seatingPositions[i]);
            else if (!isBottom && wave.allowTopRow) availableSeats.Add(seatingPositions[i]);
        }

        if (availableSeats.Count == 0) return;

        if(!isBossFight)
        {
            if (data.isSlappable)
            {
                slapWords.Add(data.slapString);
                currentWord += (slapWords.Count - 1).ToString();
            }
            else
            {
                currentWord += data.customLetter;
            }
            availableIndexes.Add(currentWord.Length - 1);
        }
        else
        {
            // In a boss fight we have a single client, so we have to parse every wave and every client to build the full word and slap words list for the boss client
            foreach(var w in levelConfig.waves)
            {
                foreach(var c in w.clientsInWave)
                {
                    int amountToAdd = 1;

                    if (c.toastsNeeded >= 1)
                        amountToAdd = c.toastsNeeded;

                    for(int i = 0; i < amountToAdd; i++)
                    {
                        if (c.isSlappable)
                        {
                            slapWords.Add(c.slapString);
                            currentWord += (slapWords.Count - 1).ToString();
                        }
                        else
                        {
                            currentWord += c.customLetter;
                        }
                        jamFlavorsInCurrentLevel.Add(c.jamFlavor);
                        availableIndexes.Add(currentWord.Length - 1);
                    }

                    
                }

            }
        }

        


        Transform chosenSeat = availableSeats[Random.Range(0, availableSeats.Count)];

        if(isBossFight)
        {
            chosenSeat = seatingPositions[0];
        }

        GameObject chosenPrefab = GetWeightedRandomPrefab();

        if(isBossFight)
        {
            chosenPrefab = levelConfig.bossPrefab != null ? levelConfig.bossPrefab : chosenPrefab;
        }

        GameObject newClientObj = Instantiate(chosenPrefab, chosenSeat.position, chosenSeat.rotation);

        Client clientScript = newClientObj.GetComponent<Client>();

        if (isBossFight)
        {
            bossClient = clientScript;
        }

        if (data.toastsNeeded > 1)
        {
            clientScript.toastsToSatisfy = data.toastsNeeded;
            if(clientScript.bossBar != null)
            {
                clientScript.bossBar.gameObject.SetActive(true);
            }
        }
        else if(isBossFight)
        {
            // Go through every wave and each client in wave and sum all toasts needed for boss clients, then set that as the toastsToSatisfy for the boss bar
            int totalToastsNeeded = 0;
            foreach(var w in levelConfig.waves)
            {
                foreach(var c in w.clientsInWave)
                {
                    Debug.Log($"Checking client with flavor {c.jamFlavor} and toasts needed {c.toastsNeeded}");
                    if (c.jamFlavor != JamFlavor.None)
                    {
                        if(c.toastsNeeded > 1)
                            totalToastsNeeded += c.toastsNeeded;
                        else
                            totalToastsNeeded++;
                    }
                }
            }
            clientScript.toastsToSatisfy = totalToastsNeeded;

            Debug.Log($"Boss client spawned with {clientScript.toastsToSatisfy} toasts needed. Boss bar should reflect this.");

            if (clientScript.bossBar != null)
            {
                clientScript.bossBar.gameObject.SetActive(true);
            }
        }


        Sprite chosenSprite;

        switch(data.jamFlavor)
        {
            case JamFlavor.Butter: chosenSprite = butterIcon; break;
            case JamFlavor.StrawberryJam: chosenSprite = strawberryIcon; break;
            case JamFlavor.GrapeJam: chosenSprite = grapeIcon; break;
            case JamFlavor.PeanutButter: chosenSprite = chocolateIcon; break;
            default: chosenSprite = null; break;
        }

        clientScript.Initialize(chosenSeat, chosenSprite);
        activeClients.Add(chosenSeat, clientScript);

        JamFlavor finalFlavor = data.jamFlavor;
        if (finalFlavor == JamFlavor.Random) finalFlavor = JamDecider.Instance.activeJams[Random.Range(0, JamDecider.Instance.activeJams.Count)].flavor;
        clientScript.SetOrder(finalFlavor.ToString(), JamDecider.Instance.GetColorFromFlavor(finalFlavor));
    }



    private GameObject GetWeightedRandomPrefab()
    {
        // Get the current flavor from your JamDecider
        JamFlavor currentActiveFlavor = JamDecider.Instance.allAvailableJams[JamDecider.Instance.currentJamIndex].flavor;

        float totalWeight = 0;
        List<float> adjustedWeights = new List<float>();

        // Calculate adjusted weights based on preference
        for (int i = 0; i < clientPrefabs.Count; i++)
        {
            float weight = clientPrefabs[i].spawnWeight;

            // If the current jam is one of their favorites, boost their weight!
            if (clientPrefabs[i].favoriteFlavors.Contains(currentActiveFlavor))
            {
                weight *= clientPrefabs[i].preferenceMultiplier;
            }

            adjustedWeights.Add(weight);
            totalWeight += weight;
        }

        // Standard Weighted Random Selection
        float pivot = Random.Range(0, totalWeight);
        float currentWeightSum = 0;

        for (int i = 0; i < clientPrefabs.Count; i++)
        {
            currentWeightSum += adjustedWeights[i];
            if (pivot <= currentWeightSum) return clientPrefabs[i].prefab;
        }

        return clientPrefabs[0].prefab;
    }

    public void AddClientAndUpdateUI()
    {
        totalClientsSatisfied++;
        UpdateUI();
    }

    public void OnClientFinished()
    {
        clientsFinishedInWave++;
        

        if(EndlessModeManager.Instance.isRunning)
            EndlessModeManager.Instance.AddASatisfiedClient();

        

        // Only do wave progression if we're in a normal level
        if ((levelConfig != null && currentWaveIndex < levelConfig.waves.Count &&
            clientsFinishedInWave >= levelConfig.waves[currentWaveIndex].clientsInWave.Count))
        {
            Debug.Log($"Wave {currentWaveIndex + 1} completed! Moving to next wave.");
            currentWaveIndex++;
            StartCoroutine(SpawnWave(currentWaveIndex));
        }
    }


    private IEnumerator FinishLevelRoutine()
    {
        levelFinished = true;
        yield return new WaitForSeconds(2f);
        float totalTime = Time.time - levelStartTime;
        int stars = CalculateStars(totalTime);

        AudioManager.Instance.PlaySound(levelCompleteSound, sfxMixer);

        levelCompleteUI.gameObject.SetActive(true);
        levelCompleteUI.Initialize(stars, totalTime);

        SaveProgress(stars, totalTime);

        clientCounterUI.SetActive(false);
        comboCounterUI.SetActive(false);
    }

    private void SaveProgress(int stars, float time)
    {

        // Matches the string format in your LevelButton Initialize method
        string starKey = $"Level_{levelConfig.levelNumber}_Stars";
        string timeKey = $"Level_{levelConfig.levelNumber}_Time";
        string comboKey = $"Level_{levelConfig.levelNumber}_Combo";

        // Use a high default value so any real time is "better" than nothing
        float previousBestTime = PlayerPrefs.GetFloat(timeKey, 999999f);

        int previousBestCombo = PlayerPrefs.GetInt(comboKey, 0);

        // Only save if this run is faster than the previous record
        if (time < previousBestTime)
        {
            PlayerPrefs.SetInt(starKey, stars);
            PlayerPrefs.SetFloat(timeKey, time);
            PlayerPrefs.Save();
           
        }

        if(previousBestCombo < Toaster.Instance.highestCombo)
        {
            PlayerPrefs.SetInt(comboKey, Toaster.Instance.highestCombo);
            PlayerPrefs.Save();
        }

        if(stars == 5)
        {
            // TP_FIRSTSTEPS
        }

        if (isBossFight)
        {
            // TP_BOSS
        }

        foreach(var jam in JamDecider.Instance.activeJams)
        {
           if(jam.flavor == JamFlavor.StrawberryJam)
            {
                // TP_STRAWBERRY
                Debug.Log("Strawberry Jam used in this level!");
            }
           if(jam.flavor == JamFlavor.GrapeJam)
            {
                // TP_GRAPE
                Debug.Log("Grape Jam used in this level!");
            }
           if(jam.flavor == JamFlavor.Butter)
            {
                // TP_CHOCOLATE
                Debug.Log("Butter used in this level!");
            }
        }

        LevelMenuManager.Instance.AreAllLevelsCompleted();
    }

    public void ResetLevelProgress(int levelNum)
    {
        // Cleans up the exact keys used by the LevelButtons
        PlayerPrefs.DeleteKey($"Level_{levelNum}_Stars");
        PlayerPrefs.DeleteKey($"Level_{levelNum}_Time");
        PlayerPrefs.Save();
    }

    private int CalculateStars(float time)
    {
        if (time <= levelConfig.fiveStarTime) return 5;
        if (time <= levelConfig.fourStarTime) return 4;
        if (time <= levelConfig.threeStarTime) return 3;
        if (time <= levelConfig.twoStarTime) return 2;
        return 1;
    }

    public Client GetClientInSeat(Transform seat)
    {
        if (activeClients.TryGetValue(seat, out Client client))
        {
            return client;
        }
        return null;
    }

    public void FullResetGame()
    {

        // Kills every active tween in the game UNLESS it has the ID "NPCPuppets"
        DOTween.KillAll(false, "NPCPuppets");

        Toaster.Instance.ResetCameraAngle();

        // 1. Stop all Spawning Coroutines
        StopAllCoroutines();

        // 2. Clear the Toaster
        Toaster.Instance.StopAllCoroutines();
        Toaster.Instance.ResetCombo();
        JamDecider.Instance.ResetJams();


        Toaster.Instance.activeToasts.Clear();

        // Inside FullResetGame(), alongside availableIndexes.Clear():
        simultaneousFlags.Clear();

        // 3. Destroy all leftover GameObjects in the scene
        // Find all Toasts
        ToastBehavior[] toasts = Object.FindObjectsByType<ToastBehavior>(FindObjectsSortMode.None);
        foreach (var t in toasts)
        {
            t.transform.DOKill(); // Stop any ongoing tweens to prevent errors
            Destroy(t.gameObject);
        }
        

        // Find all Clients
        Client[] clients = Object.FindObjectsByType<Client>(FindObjectsSortMode.None);
        foreach (var c in clients)
        {
            c.KillAllClientTweens();
            Destroy(c.gameObject);
        }
        

        // Find all Arms/Fists (they usually have a specific tag or we can find by name/component if you have one)
        TAG_Fist[] arms = GameObject.FindObjectsByType<TAG_Fist>(FindObjectsSortMode.None);

        for (int i = 0; i < arms.Length; i++)
        {
            arms[i].transform.DOKill(); // Stop any ongoing tweens to prevent errors
            Destroy(arms[i].gameObject);
        }


        // 4. Reset internal Manager logic
        activeClients.Clear();
        availableIndexes.Clear();
        currentWord = "";
        currentIndex = 0;
        levelFinished = false;

    }

    public Transform GetBestTarget(string currentJamInHand)
    {
        // --- BOSS FIGHT LOGIC ---
        if (isBossFight && levelConfig != null && bossClient != null)
        {
            var wave = levelConfig.waves[currentWaveIndex];
            int dataIndex = Mathf.Clamp(currentIndex, 0, wave.clientsInWave.Count - 1);

            // Determine the required jam for the specific toast within the current wave
            JamFlavor requiredFlavor = wave.clientsInWave[dataIndex].jamFlavor;
            string requiredJamString = requiredFlavor.ToString();

            // If it's a boss fight, we only care if the jam in hand matches the 
            // jam required by the current wave/index configuration
            if (currentJamInHand == requiredJamString)
            {
                bossClient.OpenMouth();
                return bossClient.transform;
            }
            else
            {
                // In a boss fight, if the jam is wrong, it's not a "best target"
                return seatingPositions[0]; // The boss always seats at this position
            }
        }

        // --- STANDARD LOGIC (ONLY RUNS IF NOT BOSS) ---
        // 1. Try to find a perfect match (Correct Jam + Not Satisfied)
        var matchingKvp = activeClients.FirstOrDefault(kvp =>
            kvp.Value.desiredCondiment == currentJamInHand && !kvp.Value.isSatisfied);

        if (matchingKvp.Value != null)
        {
            matchingKvp.Value.OpenMouth();
            return matchingKvp.Value.transform;
        }

        // 2. Default: Find ALL clients that are NOT satisfied yet
        var unsatisfiedClients = activeClients.Where(kvp => !kvp.Value.isSatisfied).ToList();

        if (unsatisfiedClients.Count > 0)
        {
            // Return a random client from the unsatisfied list
            return unsatisfiedClients[Random.Range(0, unsatisfiedClients.Count)].Key;
        }

        // 3. Absolute Fallback: If everyone is satisfied or no one is left, pick a random seat
        return seatingPositions[Random.Range(0, seatingPositions.Count)];
    }

    public void ClearSeat(Transform seat)
    {

        Debug.Log($"Clearing seat at position {seat.position}. Active clients before clear: {activeClients.Count}");

        if (isBossFight)
            currentWaveIndex++;

        // Debug the if logic to see whats happening when we clear a seat after the last wave
        Debug.Log($"Checking conditions for finishing level: levelConfig is null? {levelConfig == null}, currentWaveIndex: {currentWaveIndex}, levelConfig.waves.Count: {(levelConfig != null ? levelConfig.waves.Count : "N/A")}, activeClients.Count: {activeClients.Count} isBossFight: {(levelConfig != null ? levelConfig.isBossFight : "N / A")}, levelFinished: {levelFinished}");

        if (activeClients.Remove(seat)
            && levelConfig != null 
            && currentWaveIndex >= levelConfig.waves.Count
            && (activeClients.Count == 0 || isBossFight)
            && !levelFinished)
        {
            Debug.Log("All clients cleared after last wave! Finishing level...");
            StartCoroutine(FinishLevelRoutine());
        }
    }

    public JamFlavor GetCurrentBossRequiredJam()
    {
        if (levelConfig == null || !isBossFight)
        {
            return JamFlavor.None;
        }

        // Identify the current wave
        if (currentWaveIndex < levelConfig.waves.Count)
        {
            var wave = levelConfig.waves[currentWaveIndex];

            // Identify the current client data within that wave
            // We use currentIndex which is updated by IncreaseLetterIndex()
            int dataIndex = Mathf.Clamp(currentIndex, 0, wave.clientsInWave.Count - 1);

            return wave.clientsInWave[dataIndex].jamFlavor;
        }

        return JamFlavor.None;
    }

    public Sprite GetSpriteFromJam(JamFlavor jamFlavor)
    {
        switch (jamFlavor)
        {
            case JamFlavor.Butter: return butterIcon;
            case JamFlavor.StrawberryJam: return strawberryIcon;
            case JamFlavor.GrapeJam: return grapeIcon;
            case JamFlavor.PeanutButter: return chocolateIcon;
            default: return null;
        }
    }

    public bool IsLastToastOfLevel()
    {
        if (levelConfig == null) return false; // Endless mode doesn't have a "last" toast

        bool isLastWave = currentWaveIndex == levelConfig.waves.Count - 1;
        // Check if only one client remains in availableIndexes (the one currently in the air)
        bool isLastClient = availableIndexes.Count <= 0;

        Debug.Log($"Checking if last toast of level: isLastWave={isLastWave}, isLastClient={isLastClient}, currentWaveIndex={currentWaveIndex}, totalWaves={levelConfig.waves.Count}, availableIndexesCount={availableIndexes.Count}");

        return isLastWave && isLastClient;
    }
}