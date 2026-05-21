// File: ResamplingContainer.h
// Created Date: Saturday December 16th 2023
// Author: Steven Atkinson (steven@atkinson.mn)

// A container for real-time resampling using a Lanczos anti-aliasing filter

// This file originally came from the iPlug2 library and has been subsequently modified;
// the following license is copied as required from
// https://github.com/iPlug2/iPlug2/blob/40ebb560eba68f096221e99ef0ae826611fc2bda/LICENSE.txt
// -------------------------------------------------------------------------------------

/*
iPlug 2 C++ Plug-in Framework.

Copyright (C) the iPlug 2 Developers. Portions copyright other contributors, see each source file for more information.

Based on WDL-OL/iPlug by Oli Larkin (2011-2018), and the original iPlug v1 (2008) by John Schwartz / Cockos

LICENSE:

This software is provided 'as-is', without any express or implied warranty.  In no event will the authors be held liable
for any damages arising from the use of this software.

Permission is granted to anyone to use this software for any purpose, including commercial applications, and to alter it
and redistribute it freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If
you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not
required.
1. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original
software.
1. This notice may not be removed or altered from any source distribution.

iPlug 2 includes the following 3rd party libraries (see each license info):

* Cockos WDL https://www.cockos.com/wdl
* NanoVG https://github.com/memononen/nanovg
* NanoSVG https://github.com/memononen/nanosvg
* MetalNanoVG https://github.com/ollix/MetalNanoVG
* RTAudio https://www.music.mcgill.ca/~gary/rtaudio
* RTMidi https://www.music.mcgill.ca/~gary/rtmidi
*/
// -------------------------------------------------------------------------------------

#pragma once

#include <iostream>
#include <functional>
#include <cmath>

// #include "IPlugPlatform.h"

// #include "heapbuf.h"
#include "Dependencies/WDL/ptrlist.h"

#include "Dependencies/LanczosResampler.h"

namespace dsp
{

/** A multi-channel real-time resampling container that can be used to resample
 * audio processing to a specified sample rate for the situation where you have
 * some arbitary DSP code that requires a specific sample rate, then back to
 * the original external sample rate, encapsulating the arbitrary DSP code.

 * Three modes are supported:
 * - Linear interpolation: simple linear interpolation between samples
 * - Cubic interpolation: cubic interpolation between samples
 * - Lanczos: Lanczos resampling uses an approximation of the sinc function to
 *   interpolate between samples. This is the highest quality resampling mode.
 *
 * The Lanczos resampler has a configurable filter size (A) that affects the
 * latency of the resampler. It can also optionally use SIMD instructions to
 * when T==float.
 *
 *
 * @tparam T the sampletype
 * @tparam NCHANS the number of channels
 * @tparam A The Lanczos filter size for the LanczosResampler resampler mode
   A higher value makes the filter closer to an
   ideal stop-band that rejects high-frequency content (anti-aliasing),
   but at the expense of higher latency
 */
template <typename T = double, int NCHANS = 2, size_t A = 12>
class ResamplingContainer
{
public:
  using BlockProcessFunc = std::function<void(T**, T**, int)>;
  using LanczosResampler = LanczosResampler<T, NCHANS, A>;

  // :param renderingSampleRate: The sample rate required by the code to be encapsulated.
  ResamplingContainer(double renderingSampleRate)
  : mRenderingSampleRate(renderingSampleRate)
  {
  }

  ResamplingContainer(const ResamplingContainer&) = delete;
  ResamplingContainer& operator=(const ResamplingContainer&) = delete;

  // :param inputSampleRate: The external sample rate interacting with this object.
  // :param blockSize: The largest block size that will be given to this class to process until Reset()  is called
  //     again.
  void Reset(double inputSampleRate, int blockSize = DEFAULT_BLOCK_SIZE)
  {
    if (mInputSampleRate == inputSampleRate && mMaxBlockSize == blockSize)
    {
      ClearBuffers();
      return;
    }

    mInputSampleRate = inputSampleRate;
    mRatio1 = mInputSampleRate / mRenderingSampleRate;
    mRatio2 = mRenderingSampleRate / mInputSampleRate;
    // The buffers for the encapsulated code need to be long enough to hold the correesponding number of samples
    mMaxBlockSize = blockSize;
    mMaxEncapsulatedBlockSize = MaxEncapsulatedBlockSize(blockSize);

    mScratchExternalInputData.Resize(mMaxBlockSize * NCHANS); // This may contain junk right now.
    mEncapsulatedInputData.Resize(mMaxEncapsulatedBlockSize * NCHANS); // This may contain junk right now.
    mEncapsulatedOutputData.Resize(mMaxEncapsulatedBlockSize * NCHANS); // This may contain junk right now.
    mScratchExternalInputPointers.Empty();
    mEncapsulatedInputPointers.Empty();
    mEncapsulatedOutputPointers.Empty();

    for (auto chan = 0; chan < NCHANS; chan++)
    {
      mScratchExternalInputPointers.Add(mScratchExternalInputData.Get() + (chan * mMaxBlockSize));
      mEncapsulatedInputPointers.Add(mEncapsulatedInputData.Get() + (chan * mMaxEncapsulatedBlockSize));
      mEncapsulatedOutputPointers.Add(mEncapsulatedOutputData.Get() + (chan * mMaxEncapsulatedBlockSize));
    }

    {
      mResampler1 = std::make_unique<LanczosResampler>(mInputSampleRate, mRenderingSampleRate);
      mResampler2 = std::make_unique<LanczosResampler>(mRenderingSampleRate, mInputSampleRate);

      // Zeroes the scratch pointers so that we warm up with silence.
      ClearBuffers();

      // Warm up the resampling container with enough silence that the first real buffer can yield the required number
      // of output samples.
      const auto midSamples = mResampler2->GetNumSamplesRequiredFor(1);
      mLatency = int(mResampler1->GetNumSamplesRequiredFor(midSamples));
      // 1. Push some silence through the first resampler.
      //
      mResampler1->PushBlock(mScratchExternalInputPointers.GetList(), mLatency);
      const size_t populated = mResampler1->PopBlock(mEncapsulatedInputPointers.GetList(), midSamples);
      if (populated < midSamples)
      {
        throw std::runtime_error("Didn't get enough samples required for pre-population!");
      }
      // 2. "process" the warm-up in the encapsulated DSP.
      // Since this is an audio effect, we can assume that (1) it's causal and (2) that it's silent until
      // a non-silent input is given to it.
      // Therefore, we don't *acutally* need to use `func()`--we can assume that it would output silence!
      // func(mEncapsulatedInputPointers.GetList(), mEncapsulatedOutputPointers.GetList(), (int)populated);
      FallbackFunc(mEncapsulatedInputPointers.GetList(), mEncapsulatedOutputPointers.GetList(), (int)populated);
      mResampler2->PushBlock(mEncapsulatedOutputPointers.GetList(), populated);
      // Now we're ready for the first "real" buffer.
    }
  }

  /** Resample an input block with a per-block function (up sample input -> process with function -> down sample)
   * @param inputs Two-dimensional array containing the non-interleaved input buffers of audio samples for all channels
   * @param outputs Two-dimensional array for audio output (non-interleaved).
   * @param nFrames The block size for this block: number of samples per channel.
   * @param func The function that processes the audio sample at the higher sampling rate. NOTE: std::function can call
   * malloc if you pass in captures */
  void ProcessBlock(T** inputs, T** outputs, int nFrames, BlockProcessFunc func)
  {
    mResampler1->PushBlock(inputs, nFrames);
    // This is the most samples the encapsualted context might get. Sometimes it'll get fewer.
    const auto maxEncapsulatedLen = MaxEncapsulatedBlockSize(nFrames);

    // Process as much audio as you can with the encapsulated DSP, and push it into the second resampler.
    // This will give the second reasmpler enough for it to pop the required buffer size to complete this function
    // correctly.
    while (mResampler1->GetNumSamplesRequiredFor(1) == 0) // i.e. there's more to process
    {
      // Get a block no larger than the encapsulated DSP is expecting.
      const size_t populated1 = mResampler1->PopBlock(mEncapsulatedInputPointers.GetList(), maxEncapsulatedLen);
      if (populated1 > maxEncapsulatedLen)
      {
        throw std::runtime_error("Got more encapsulated samples than the encapsulated DSP is prepared to handle!");
      }
      func(mEncapsulatedInputPointers.GetList(), mEncapsulatedOutputPointers.GetList(), (int)populated1);
      // And push the results into the second resampler so that it has what the external context requires.
      mResampler2->PushBlock(mEncapsulatedOutputPointers.GetList(), populated1);
    }

    // Pop the required output from the second resampler for the external context.
    const auto populated2 = mResampler2->PopBlock(outputs, nFrames);
    if (populated2 < nFrames)
    {
      std::cerr << "Did not yield enough samples (" << populated2 << ") to provide the required output buffer (expected"
                << nFrames << ")! Filling with last sample..." << std::endl;
      for (int c = 0; c < NCHANS; c++)
      {
        const T lastSample = populated2 > 0 ? outputs[c][populated2 - 1] : 0.0;
        for (int i = populated2; i < nFrames; i++)
        {
          outputs[c][i] = lastSample;
        }
      }
    }
    // Get ready for the next block:
    mResampler1->RenormalizePhases();
    mResampler2->RenormalizePhases();
  }

  int GetLatency() const { return mLatency; }

private:
  static inline int LinearInterpolate(T** inputs, T** outputs, int inputLen, double ratio, int maxOutputLen)
  {
    // FIXME check through this!
    const auto outputLen = std::min(static_cast<int>(std::ceil(static_cast<double>(inputLen) / ratio)), maxOutputLen);

    for (auto writePos = 0; writePos < outputLen; writePos++)
    {
      const auto readPos = ratio * static_cast<double>(writePos);
      const auto readPostionTrunc = std::floor(readPos);
      const auto readPosInt = static_cast<int>(readPostionTrunc);

      if (readPosInt < inputLen)
      {
        const auto y = readPos - readPostionTrunc;

        for (auto chan = 0; chan < NCHANS; chan++)
        {
          const auto x0 = inputs[chan][readPosInt];
          const auto x1 = ((readPosInt + 1) < inputLen) ? inputs[chan][readPosInt + 1] : inputs[chan][readPosInt - 1];
          outputs[chan][writePos] = (1.0 - y) * x0 + y * x1;
        }
      }
    }

    return outputLen;
  }

  static inline int CubicInterpolate(T** inputs, T** outputs, int inputLen, double ratio, int maxOutputLen)
  {
    // FIXME check through this!
    const auto outputLen = std::min(static_cast<int>(std::ceil(static_cast<double>(inputLen) / ratio)), maxOutputLen);

    for (auto writePos = 0; writePos < outputLen; writePos++)
    {
      const auto readPos = ratio * static_cast<double>(writePos);
      const auto readPostionTrunc = std::floor(readPos);
      const auto readPosInt = static_cast<int>(readPostionTrunc);

      if (readPosInt < inputLen)
      {
        const auto y = readPos - readPostionTrunc;

        for (auto chan = 0; chan < NCHANS; chan++)
        {
          const auto xm1 = ((readPosInt - 1) > 0) ? inputs[chan][readPosInt - 1] : 0.0f;
          const auto x0 = ((readPosInt) < inputLen) ? inputs[chan][readPosInt] : inputs[chan][readPosInt - 1];
          const auto x1 = ((readPosInt + 1) < inputLen) ? inputs[chan][readPosInt + 1] : inputs[chan][readPosInt - 1];
          const auto x2 = ((readPosInt + 2) < inputLen) ? inputs[chan][readPosInt + 2] : inputs[chan][readPosInt - 1];

          const auto c = (x1 - xm1) * 0.5;
          const auto v = x0 - x1;
          const auto w = c + v;
          const auto a = w + v + (x2 - x0) * 0.5;
          const auto b = w + a;

          outputs[chan][writePos] = ((((a * y) - b) * y + c) * y + x0);
        }
      }
    }

    return outputLen;
  }

  void ClearBuffers()
  {
    memset(mScratchExternalInputData.Get(), 0.0f, DataSize(mMaxBlockSize));
    const auto encapsulatedDataSize = DataSize(mMaxEncapsulatedBlockSize);
    memset(mEncapsulatedInputData.Get(), 0.0f, encapsulatedDataSize);
    memset(mEncapsulatedOutputData.Get(), 0.0f, encapsulatedDataSize);

    if (mResampler1 != nullptr)
    {
      mResampler1->ClearBuffer();
    }
    if (mResampler2 != nullptr)
    {
      mResampler2->ClearBuffer();
    }
  }

  // How big could the corresponding encapsulated buffer be for a buffer at the external sample rate of a given size?
  int MaxEncapsulatedBlockSize(const int externalBlockSize) const
  {
    return static_cast<int>(std::ceil(static_cast<double>(externalBlockSize) / mRatio1));
  }

  // Size of the multi-channel data for a given block size
  size_t DataSize(const int blockSize) const { return blockSize * NCHANS * sizeof(T); };

  void FallbackFunc(T** inputs, T** outputs, int n)
  {
    for (int i = 0; i < NCHANS; i++)
    {
      memcpy(inputs[i], outputs[i], n * sizeof(T));
    }
  }

  // Buffers for scratch input data for Reset() to use
  WDL_TypedBuf<T> mScratchExternalInputData;
  WDL_PtrList<T> mScratchExternalInputPointers;
  // Buffers for the input & output to the encapsulated DSP
  WDL_TypedBuf<T> mEncapsulatedInputData;
  WDL_PtrList<T> mEncapsulatedInputPointers;
  WDL_TypedBuf<T> mEncapsulatedOutputData;
  WDL_PtrList<T> mEncapsulatedOutputPointers;
  // Sample rate ratio from external to encapsulated, from encapsulated to external.
  double mRatio1 = 0.0, mRatio2 = 0.0;
  // Sample rate of the external context.
  double mInputSampleRate = 0.0;
  // The size of the largest block the external context may provide. (It might provide something smaller.)
  int mMaxBlockSize = 0;
  // The size of the largest possible encapsulated block
  int mMaxEncapsulatedBlockSize = 0;
  // How much latency this object adds due to both of its resamplers. This does _not_ include the latency due to the
  // encapsulated `func()`.
  int mLatency = 0;
  // The sample rate required by the DSP that this object encapsulates
  const double mRenderingSampleRate;
  // Pair of resamplers for (1) external -> encapsulated, (2) encapsulated -> external
  std::unique_ptr<LanczosResampler> mResampler1, mResampler2;
};

}; // namespace dsp
