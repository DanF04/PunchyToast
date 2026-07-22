using UnityEngine;

public class GeneralAchivementTracker : MonoBehaviour
{

    [SerializeField] private SteamManager steamManager;

    public void CustomizeArm()
    {
        // TP_CUSTOMIZEARM
        steamManager.UnlockAchievement("TP_CUSTOMIZEARM");
        Debug.Log("Achievement Unlocked: Customize Arm");
    }

    public void CustomizeToaster()
    {
        // TP_CUSTOMIZETOASTER
        steamManager.UnlockAchievement("TP_CUSTOMIZETOASTER");
        Debug.Log("Achievement Unlocked: Customize Toaster");   
    }
}
