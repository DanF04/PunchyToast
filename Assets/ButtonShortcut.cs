using UnityEngine;
using UnityEngine.UI;

public class ButtonShortcut : MonoBehaviour
{
    [Header("Assign the Button and Key")]
    [SerializeField] private Button targetButton;
    [SerializeField] private KeyCode shortcutKey = KeyCode.Return;

    void Update()
    {
        // Triggers the exact same onClick events hooked up in the inspector
        if (Input.GetKeyDown(shortcutKey))
        {
            if (targetButton != null && targetButton.gameObject.activeInHierarchy)
            {
                targetButton.onClick.Invoke();
            }
        }
    }
}