using UnityEngine;

namespace MidiPlayerTK
{
    public class AudioVisualizer : MonoBehaviour
    {
        public int sampleSize = 1024;
        public float[] samples;
        public LineRenderer lineRenderer;

        private float[] timeData;
        private int currentIndex = 0;
        private float timeInterval;

        void Start()
        {
            samples = new float[sampleSize];
            timeData = new float[sampleSize];
            lineRenderer.positionCount = sampleSize;
            timeInterval = 1.0f / AudioSettings.outputSampleRate;

            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.useWorldSpace = false;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            Debug.Log($"{currentIndex} {data.Length} {channels}");
            if (samples == null || timeData == null) return;

            for (int i = 0; i < data.Length; i += channels)
            {
                float average = 0f;
                // Compute the average of all channels
                for (int j = 0; j < channels; j++)
                {
                    average += data[i + j];
                }
                average /= channels;
                samples[currentIndex] = average;
                timeData[currentIndex] = currentIndex * timeInterval;
                currentIndex = (currentIndex + 1) % sampleSize;
            }

        }

        void Update()
        {
            // lineRenderer.positionCount = currentIndex;
            for (int i = 0; i < sampleSize; i++)
            {
                float x = timeData[i] * 100;
                float y = samples[i] * 100;
                lineRenderer.SetPosition(i, new Vector3(x, y, 0));
            }
        }
    }
}
