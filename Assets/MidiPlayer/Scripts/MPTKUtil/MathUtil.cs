using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{
    public class MovingAverage
    {
        private Queue<int> samples;
        private int windowSize = 50;
        private int sampleAccumulator;

        public int Count
        {
            get { return samples.Count; }
        }

        public int Average
        {
            get
            {
                try
                {
                    if (samples != null && samples.Count > 0)
                        return sampleAccumulator / samples.Count;
                    else
                        return 0;
                }
                catch (Exception ex)
                {
                    // Strange, samples null did not trigger an exception :-(
                    Debug.LogException(ex);
                }
                return 0;
            }
        }

        public MovingAverage()
        {
            sampleAccumulator = 0;
            samples = new Queue<int>();
        }

        public MovingAverage(int size)
        {
            sampleAccumulator = 0;
            samples = new Queue<int>();
            windowSize = size;
        }

        /// <summary>@brief
        /// Computes a new windowed average each time a new sample arrives
        /// </summary>
        /// <param name="newSample"></param>
        public void Add(int newSample)
        {
            // Add a new sample
            sampleAccumulator += newSample;
            samples.Enqueue(newSample);

            if (samples.Count > windowSize)
            {
                // Remove the older
                sampleAccumulator -= samples.Dequeue();
            }
        }
    }
}
