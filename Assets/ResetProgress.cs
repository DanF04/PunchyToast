using System.Linq;
using UnityEngine;

public class ResetProgress : MonoBehaviour
{
    public void ResetLevelProgress()
    {
        // Load all level configurations from the Resources/Levels folder
        LevelConfiguration[] allConfigs = Resources.LoadAll<LevelConfiguration>("Levels");

        foreach (var config in allConfigs)
        {
            int num = config.levelNumber;

            // Delete the Stars entry
            PlayerPrefs.DeleteKey($"Level_{num}_Stars");

            // Delete the Time entry
            PlayerPrefs.DeleteKey($"Level_{num}_Time");

            // Delete the Highest Combo entry
            PlayerPrefs.DeleteKey($"Level_{num}_Combo");
        }


        PlayerPrefs.DeleteKey("ToastsPunched");

        // Reset the "Last Played" tracking
        PlayerPrefs.DeleteKey("LastPlayedLevel");

        // Optional: If you have a global highest combo (not per level), delete it here:
        // PlayerPrefs.DeleteKey("GlobalHighestCombo");

        PlayerPrefs.Save();

        Debug.Log("All level progress, times, and combo records have been wiped.");
    }
}