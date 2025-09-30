using UnityEngine;
using UnityEngine.UI;

public class VoiceDetector : MonoBehaviour
{
    [Header("UI")]
    public Slider voiceSlider;   // Slider UI untuk menampilkan level suara

    [Header("Mic Settings")]
    public int sampleWindow = 64;   // Semakin tinggi, semakin halus
    public float screamThreshold = 30f; // Ambang batas scream

    private AudioClip micClip;
    private string micDevice;

    void Start()
    {
        // Pastikan ada microphone
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];

            // Start recording dari microphone → disimpan ke micClip
            micClip = Microphone.Start(micDevice, true, 10, AudioSettings.outputSampleRate);

            Debug.Log("Mic aktif: " + micDevice);
        }
        else
        {
            Debug.LogWarning("Tidak ada microphone terdeteksi!");
        }
    }

    void Update()
    {
        if (micClip != null)
        {
            float level = GetMicLoudness();
            voiceSlider.value = level;

            if (level > screamThreshold)
            {
                Debug.Log("⚡ Player Scream Detected!");
                // Tambahkan event lain di sini → misalnya Game Over / Jumpscare
            }
        }
    }

    float GetMicLoudness()
    {
        float[] data = new float[sampleWindow];
        int micPosition = Microphone.GetPosition(micDevice) - sampleWindow + 1;

        if (micPosition < 0) return 0;

        micClip.GetData(data, micPosition);

        float totalLoudness = 0;
        foreach (var s in data)
        {
            totalLoudness += Mathf.Abs(s);
        }

        return totalLoudness / sampleWindow * 100; // faktor kali biar lebih sensitif
    }
}
