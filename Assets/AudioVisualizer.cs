using UnityEngine;

public class AudioVisualizer : MonoBehaviour
{
    [SerializeField] private AudioSource musicAudio;

    public int numberOfSamples = 32;
    public float[] samples;
    public int numberOfFrequencies = 8;
    public float[] frequencyBands;

    private float[] bandAverages;
    private float smoothingFactor = 0.1f; // adjust for responsiveness

    public int barOffset = 4;

    private float[] bandBuffer;           // Smoothed visual output
    public float[] BandBuffer => bandBuffer;

    private float[] bandBufferDecrease;   // Helps track falloff speed
    private float[] bandMaxHistory;       // Tracks historical max per band

    [SerializeField] private float visualScaleMultiplier = 10f;
    [SerializeField] private float bufferDecreaseSpeed = 0.005f;
    [SerializeField] private float maxHistoryDecay = 0.99f; // Slowly forget max

    private float globalMax = 0.01f; // start small to avoid divide-by-zero
    [SerializeField] private float globalMaxDecay = 0.999f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        samples = new float[numberOfSamples];
        frequencyBands = new float[numberOfFrequencies];
        bandAverages = new float[numberOfFrequencies];
        bandBuffer = new float[numberOfFrequencies];
        bandBufferDecrease = new float[numberOfFrequencies];
        bandMaxHistory = new float[numberOfFrequencies];
    }

    // Update is called once per frame
    void Update()
    {
        GetSpectrumAudioSource();
        MakeFrequencyBands();
        UpdateBandBuffer();
    }

    void GetSpectrumAudioSource()
    {
        musicAudio.GetSpectrumData(samples, 0, FFTWindow.Blackman);
    }

    void MakeFrequencyBands()
    {
        int count = 0;

        for (int i = 0; i < numberOfFrequencies; i++)
        {
            int sampleCount = (int)Mathf.Pow(2, i) * 2;
            float average = 0;

            if (i == numberOfFrequencies - 1)
            {
                sampleCount += 2;
            }

            for (int j = 0; j < sampleCount && count < samples.Length; j++)
            {
                average += samples[count] * (count + 1);
                count++;
            }

            average /= count;
            frequencyBands[i] = average * 10;

            // Update global max to maintain contrast between bands
            if (frequencyBands[i] > globalMax)
            {
                globalMax = frequencyBands[i];
            }
        }

        // Slowly decay the global max so it adapts
        globalMax *= globalMaxDecay;

        // Optional: avoid over-normalizing during quiet parts
        if (globalMax < 0.01f)
            globalMax = 0.01f;
    }


    void UpdateBandBuffer()
    {
        for (int i = 0; i < numberOfFrequencies; i++)
        {
            float normalized = frequencyBands[i] / globalMax;

            if (normalized > bandBuffer[i])
            {
                bandBuffer[i] = normalized;
            }
            else
            {
                bandBuffer[i] = Mathf.Lerp(bandBuffer[i], normalized, 0.05f); // smooth falloff
            }
        }
    }
}