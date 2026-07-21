using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

public class LevelMenuManager : MonoBehaviour
{
    // --- SINGLETON INSTANCE ---
    public static LevelMenuManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject pagePrefab; // A prefab with a Grid Layout Group (2x5)
    public GameObject levelButtonPrefab;
    public GameObject emptySlotPrefab;

    [Header("Navigation")]
    public Button btnLeft;
    public Button btnRight;
    public RectTransform pageAnchor; // The parent inside the Mask
    public float slideDuration = 0.5f;
    public float slideAmount = 1000f; // Define this in the inspector (e.g., 1080 or 1920)

    [Header("Level Completion Tracking")]
    [Tooltip("Live view of total completed levels.")]
    public int completedLevelsCount;

    [Tooltip("Live view of total 5-star levels.")]
    public int fiveStarLevelsCount; // NEW

    private List<LevelConfiguration> allLevels = new List<LevelConfiguration>();
    private int currentPage = 0;
    private int levelsPerPage = 10;
    private GameObject currentActivePage;
    private bool isTransitioning = false;

    private LevelButton lastPlayedLevel;
    private int lastPlayedLevelNumber = -1;

    private void Awake()
    {
        // Set up the Singleton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        LoadAllLevels();
        UpdateCompletedLevelsCount();
        UpdateFiveStarLevelsCount(); // Recount on start

        AreAllLevelsCompleted(); // Check if all levels are completed and log the result
        GetFiveStarLevelsCount(); // Check how many levels have 5 stars and log the result
        ShowPage(0, false);
    }

    private void OnEnable()
    {
        LoadAllLevels();
        UpdateCompletedLevelsCount();
        UpdateFiveStarLevelsCount(); // Recount on enable
        ShowPage(0, false);
    }

    void LoadAllLevels()
    {
        // Loads everything from Resources/Levels/
        LevelConfiguration[] loaded = Resources.LoadAll<LevelConfiguration>("Levels");
        allLevels = loaded.OrderBy(l => l.levelNumber).ToList();
    }

    public void NextPage() => ChangePage(1);
    public void PrevPage() => ChangePage(-1);

    private void ChangePage(int direction)
    {
        if (isTransitioning) return;

        int nextPageIndex = currentPage + direction;
        if (nextPageIndex < 0 || nextPageIndex >= GetTotalPages()) return;

        ShowPage(nextPageIndex, true, direction);
    }

    private int GetTotalPages()
    {
        return Mathf.CeilToInt((float)allLevels.Count / levelsPerPage);
    }

    private void ShowPage(int index, bool animate, int direction = 1)
    {
        isTransitioning = true;
        currentPage = index;

        // 1. Instantiate the new page
        GameObject newPage = Instantiate(pagePrefab, pageAnchor);
        FillPage(newPage, currentPage);

        // 2. Handle buttons state
        btnLeft.interactable = currentPage > 0;
        btnRight.interactable = currentPage < GetTotalPages() - 1;

        if (!animate)
        {
            if (currentActivePage != null) Destroy(currentActivePage);
            currentActivePage = newPage;
            newPage.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            isTransitioning = false;
        }
        else
        {
            // 3. Updated Animation Logic with slideAmount
            RectTransform newRect = newPage.GetComponent<RectTransform>();
            RectTransform oldRect = currentActivePage.GetComponent<RectTransform>();

            // Position new page exactly at the slideAmount offset
            newRect.anchoredPosition = new Vector2(slideAmount * direction, 0);

            // Slide both using InOutCubic for a smoother start/end transition
            oldRect.DOAnchorPos(new Vector2(-slideAmount * direction, 0), slideDuration)
                .SetEase(Ease.InOutCubic);

            newRect.DOAnchorPos(Vector2.zero, slideDuration)
                .SetEase(Ease.InOutCubic)
                .OnComplete(() =>
                {
                    if (oldRect != null) Destroy(oldRect.gameObject);
                    currentActivePage = newPage;
                    isTransitioning = false;
                });
        }
    }

    private void FillPage(GameObject pageObj, int pageIndex)
    {
        int startIdx = pageIndex * levelsPerPage;

        for (int i = 0; i < levelsPerPage; i++)
        {
            int dataIdx = startIdx + i;

            if (dataIdx < allLevels.Count)
            {
                // Add actual Level Button
                GameObject btn = Instantiate(levelButtonPrefab, pageObj.transform);

                LevelButton levelButton = btn.GetComponent<LevelButton>();

                levelButton.Initialize(allLevels[dataIdx], gameObject.transform.parent.gameObject, this);

                if (lastPlayedLevelNumber == allLevels[dataIdx].levelNumber)
                {
                    levelButton.SetLastPlayed(true);
                }
            }
            else
            {
                // Add Empty Slot prefab to keep the 2x5 grid intact
                Instantiate(emptySlotPrefab, pageObj.transform);
            }
        }
    }

    public void SetLastPlayedLevel(int levelNumber)
    {
        lastPlayedLevelNumber = levelNumber;
    }

    public void StartNextLevel()
    {
        int nextLevel = lastPlayedLevelNumber + 1;

        foreach (LevelConfiguration config in allLevels)
        {
            if (config.levelNumber == nextLevel)
            {
                ClientManager.Instance.StartLevel(config);

                if (MusicManager.Instance != null)
                {
                    MusicManager.Instance.FadeToLevel();
                }

                if (gameObject.transform.parent.gameObject != null)
                {
                    gameObject.transform.parent.gameObject.SetActive(false);
                }

                lastPlayedLevelNumber = nextLevel; // Update the last played level number

                Toaster.Instance.ResetCombo();
                Toaster.Instance.activeToasts.Clear();

                return;
            }
        }

        MusicManager.Instance.FadeToMenu();
    }

    public void RetryCurrentLevel()
    {
        int nextLevel = lastPlayedLevelNumber;

        foreach (LevelConfiguration config in allLevels)
        {
            if (config.levelNumber == nextLevel)
            {
                ClientManager.Instance.StartLevel(config);

                if (MusicManager.Instance != null)
                {
                    MusicManager.Instance.FadeToLevel();
                }

                if (gameObject.transform.parent.gameObject != null)
                {
                    gameObject.transform.parent.gameObject.SetActive(false);
                }

                lastPlayedLevelNumber = nextLevel; // Update the last played level number

                Toaster.Instance.ResetCombo();
                Toaster.Instance.activeToasts.Clear();

                return;
            }
        }
    }

    /// <summary>
    /// Calculates and returns the total number of completed levels.
    /// Updates the public completedLevelsCount for Inspector viewing.
    /// </summary>
    public int GetCompletedLevelsCount()
    {
        UpdateCompletedLevelsCount();
        return completedLevelsCount;
    }

    /// <summary>
    /// Helper method to recount completed levels.
    /// </summary>
    public void UpdateCompletedLevelsCount()
    {
        if (allLevels == null || allLevels.Count == 0)
        {
            completedLevelsCount = 0;
            return;
        }

        int count = 0;
        foreach (LevelConfiguration config in allLevels)
        {
            if (IsLevelCompleted(config.levelNumber))
            {
                count++;
            }
        }

        completedLevelsCount = count;
    }

    //// <summary>
    /// Checks whether every level loaded from resources has been completed.
    /// </summary>
    /// <returns>True if all levels are completed, false otherwise.</returns>
    public bool AreAllLevelsCompleted()
    {
        UpdateCompletedLevelsCount();

        int totalLevels = allLevels != null ? allLevels.Count : 0;
        Debug.Log($"[LevelMenuManager] Level Completion Status: {completedLevelsCount} / {totalLevels} completed.");

        return totalLevels > 0 && completedLevelsCount == totalLevels;
    }

    public void CheckForLevelAchievements()
    {
        if (AreAllLevelsCompleted())
        {
            // TP_FINISHED
        }
    }

    /// <summary>
    /// Helper method to check if a level is completed by checking if a valid best time exists.
    /// </summary>
    public bool IsLevelCompleted(int levelNumber)
    {
        // Checks if a best time exists and is greater than 0
        // (Supports float, int, or string formats if stored as time)
        if (PlayerPrefs.HasKey("Level_" + levelNumber + "_Time"))
        {
            return PlayerPrefs.GetFloat("Level_" + levelNumber + "_Time", 0f) > 0f;
        }

        if (PlayerPrefs.HasKey("LevelTime_" + levelNumber))
        {
            return PlayerPrefs.GetFloat("LevelTime_" + levelNumber, 0f) > 0f;
        }

        if (PlayerPrefs.HasKey("BestTime_Level_" + levelNumber))
        {
            return PlayerPrefs.GetFloat("BestTime_Level_" + levelNumber, 0f) > 0f;
        }

        return false;
    }

    /// <summary>
    /// Checks how many levels have achieved 5 stars and logs the count.
    /// </summary>
    /// <returns>The total number of levels completed with 5 stars.</returns>
    public int GetFiveStarLevelsCount()
    {
        UpdateFiveStarLevelsCount();

        int totalLevels = allLevels != null ? allLevels.Count : 0;
        Debug.Log($"[LevelMenuManager] 5-Star Status: {fiveStarLevelsCount} / {totalLevels} levels have 5 stars.");

        if(fiveStarLevelsCount == totalLevels)
        {
            // TP_COMPLETIONIST
        }

        return fiveStarLevelsCount;
    }

    /// <summary>
    /// Helper method to recount 5-star levels.
    /// </summary>
    private void UpdateFiveStarLevelsCount()
    {
        if (allLevels == null || allLevels.Count == 0)
        {
            fiveStarLevelsCount = 0;
            return;
        }

        int count = 0;
        foreach (LevelConfiguration config in allLevels)
        {
            if (GetLevelStars(config.levelNumber) >= 5)
            {
                count++;
            }
        }

        fiveStarLevelsCount = count;

        if(fiveStarLevelsCount == 5)
        {
            // TP_SPEEDRUNNER
        }

    }

    /// <summary>
    /// Helper method to retrieve the star rating for a specific level from PlayerPrefs.
    /// </summary>
    public int GetLevelStars(int levelNumber)
    {
        // Checks common PlayerPrefs key patterns for stars
        if (PlayerPrefs.HasKey("Level_" + levelNumber + "_Stars"))
        {
            return PlayerPrefs.GetInt("Level_" + levelNumber + "_Stars", 0);
        }

        if (PlayerPrefs.HasKey("LevelStars_" + levelNumber))
        {
            return PlayerPrefs.GetInt("LevelStars_" + levelNumber, 0);
        }

        if (PlayerPrefs.HasKey("Stars_Level_" + levelNumber))
        {
            return PlayerPrefs.GetInt("Stars_Level_" + levelNumber, 0);
        }

        return 0;
    }
}