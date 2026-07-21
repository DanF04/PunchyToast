using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Plays a random clip from an array. 
    /// Pass a Vector3 for 3D spatial audio, or null for 2D (UI/Global) audio.
    /// </summary>
    public void PlaySound(AudioClip[] clips, AudioMixer mixer, Vector3? position = null, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip randomClip = clips[Random.Range(0, clips.Length)];
        PlaySound(randomClip, mixer, position, volume);
    }

    /// <summary>
    /// Plays a single clip.
    /// </summary>
    public void PlaySound(AudioClip clip, AudioMixer mixer, Vector3? position = null, float volume = 1f)
    {
        if (clip == null) return;

        // 1. Create a temporary GameObject for the sound
        GameObject go = new GameObject("TempAudio_" + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();

        source.outputAudioMixerGroup = mixer.FindMatchingGroups("Master")[0];

        // 2. Configure Position (3D vs 2D)
        if (position.HasValue)
        {
            go.transform.position = position.Value;
            source.spatialBlend = 1f; // Full 3D
            source.minDistance = 2f;
            source.maxDistance = 20f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
        else
        {
            source.spatialBlend = 0f; // Full 2D (UI)
        }

        // 3. Randomize Pitch and Set Volume
        source.clip = clip;
        source.volume = volume;
        source.pitch = Random.Range(0.95f, 1.05f);

        // 4. Play and Destroy
        source.Play();
        Destroy(go, clip.length / source.pitch);
    }

    /// <summary>
    /// Plays a sound with a specific fixed pitch.
    /// </summary>
    public void PlaySoundFixedPitch(AudioClip clip, float pitch, AudioMixer mixer, Vector3? position = null, float volume = 1f)
    {
        if (clip == null) return;

        GameObject go = new GameObject("TempAudio_Fixed_" + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();

        if (position.HasValue)
        {
            go.transform.position = position.Value;
            source.spatialBlend = 1f;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch; // Uses the passed value directly
        source.outputAudioMixerGroup = mixer.FindMatchingGroups("Master")[0];

        source.Play();
        Destroy(go, clip.length / Mathf.Max(0.01f, pitch));
    }
}