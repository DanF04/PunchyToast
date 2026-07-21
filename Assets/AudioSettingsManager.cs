using NaughtyAttributes;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioSettingsManager : MonoBehaviour
{
    [System.Serializable]
    public struct VolumeSetting
    {
        public string name;                  // Optional label for clarity in Inspector (e.g., "Master", "SFX", "BGM")
        public AudioMixer mixer;             // Target AudioMixer
        public string volumeParameter;       // Exposed parameter name (e.g., "MasterVolume")
        public string playerPrefsKey;        // Key saved in PlayerPrefs (e.g., "MasterVolumeValue")
        [Range(0.0001f, 1f)]
        public float defaultValue;           // Default volume if no PlayerPrefs key exists
    }

    [Header("Mixer Settings")]
    [SerializeField] private VolumeSetting[] volumeSettings;

    private void Start()
    {
        // Yielding one frame guarantees Unity's AudioMixers are initialized
        ApplyAllSavedVolumes();
    }


    public void ApplyAllSavedVolumes()
    {
        foreach (var setting in volumeSettings)
        {
            if (setting.mixer == null || string.IsNullOrEmpty(setting.volumeParameter))
                continue;

            float savedValue = PlayerPrefs.GetFloat(setting.playerPrefsKey, setting.defaultValue);
            SetMixerVolume(setting.mixer, setting.volumeParameter, savedValue);
        }
    }

    public static void SetMixerVolume(AudioMixer mixer, string parameter, float normalizedValue)
    {
        // Protect against log(0)
        if (normalizedValue <= 0.0001f) normalizedValue = 0.0001f;

        float dB = Mathf.Log10(normalizedValue) * 20f;
        mixer.SetFloat(parameter, dB);
    }

    [Button("Clear Saved Audio Prefs")]
    public void ClearAudioPreferences()
    {
        foreach (var setting in volumeSettings)
        {
            if (!string.IsNullOrEmpty(setting.playerPrefsKey))
            {
                PlayerPrefs.DeleteKey(setting.playerPrefsKey);
            }
        }

        PlayerPrefs.Save();
        Debug.Log("Audio volume PlayerPrefs have been cleared.");

        // Reset the active mixers back to default values immediately
        ApplyAllSavedVolumes();
    }

}