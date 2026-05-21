/*
 * File: loadmodel.cpp
 * Created Date: Wednesday August 16th 2023
 * Author: Steven Atkinson (steven@atkinson.mn)
 */

// Test loading a model
// FIXME let's get some proper testing going!

#include <stdlib.h>
#include "dsp/dsp.h"

class Dummy : public dsp::DSP
{
public:
  ~Dummy() { _DeallocateOutputPointers(); }
  DSP_SAMPLE** Process(DSP_SAMPLE** inputs, const size_t numChannels, const size_t numFrames) override
  {
    if (numChannels > _GetNumChannels())
    {
      throw std::runtime_error("Asked to process too many channels!\n");
    }
    if (numFrames > _GetNumFrames())
    {
      throw std::runtime_error("Asked to process too many samples!\n");
    }
    for (int c = 0; c < numChannels; c++)
    {
      for (int f = 0; f < numFrames; f++)
      {
        mOutputs[c][f] = inputs[c][f];
      }
    }
    return _GetPointers();
  };
  void PrepareForAudio(const int maxChannels, const int maxFrames) { _PrepareBuffers(maxChannels, maxFrames); };
};

int main(int argc, char* argv[])
{
  Dummy dummy;
  const int maxChannels = 2;
  const int maxSamples = 128;
  dummy.PrepareForAudio(maxChannels, maxSamples);
  DSP_SAMPLE** inputs = new DSP_SAMPLE*[maxChannels];
  for (int c = 0; c < maxChannels; c++)
  {
    inputs[c] = new DSP_SAMPLE[maxSamples];
  }

  const int numBuffers = 2;
  for (int b = 0; b < numBuffers; b++)
  {
    // Fill the input buffer
    for (int c = 0; c < maxChannels; c++)
    {
      for (int s = 0; s < maxSamples; s++)
      {
        inputs[c][s] = 0.01 * c + 0.02 * s / (DSP_SAMPLE)maxSamples;
      }
    }
    dummy.Process(inputs, maxChannels, maxSamples);
    // Yay
  }

  // Deallocate
  for (int c = 0; c < maxChannels; c++)
    delete[] inputs[c];
  delete[] inputs;

  return 0;
}
