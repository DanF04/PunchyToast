using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DelayedAudioPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Delay in seconds before the audio starts playing.")]
    [SerializeField] private float delayInSeconds = 2f;

    [Tooltip("If true, the audio delay will start automatically when this object becomes active.")]
    [SerializeField] private bool playOnStart = true;

    private AudioSource audioSource;
    private Coroutine playCoroutine;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // Ensure "Play On Awake" is turned off on the AudioSource component so it doesn't play immediately
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayWithDelay();
        }
    }

    /// <summary>
    /// Starts the delay countdown and plays the audio source once finished.
    /// </summary>
    public void PlayWithDelay()
    {
        // Cancel any existing delay before starting a new one
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        playCoroutine = StartCoroutine(PlayAudioRoutine(delayInSeconds));
    }

    /// <summary>
    /// Overload to allow playing with a dynamic delay from other scripts.
    /// </summary>
    public void PlayWithDelay(float customDelay)
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        playCoroutine = StartCoroutine(PlayAudioRoutine(customDelay));
    }

    private IEnumerator PlayAudioRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        audioSource.Play();
        playCoroutine = null;
    }

    private void OnDisable()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }
}