//
//  wav.cpp
//  NeuralAmpModeler-macOS
//
//  Created by Steven Atkinson on 12/31/22.
//

#include <cstdint>
#include <cstring> // strncmp
#include <cmath> // pow
#include <fstream>
#include <iostream>
#include <string>
#include <sstream>
#include <unordered_set>
#include <vector>

#include "wav.h"

struct WaveFileData
{
  // TODO use types like uint32_t, etc
  struct RiffChunk
  {
    bool valid = false; // Have we gotten this info yet?
    int size; // NB: Of the rest of the file
    char format[4];
  } riffChunk;

  struct FmtChunk
  {
    bool valid = false;
    int size;
    // PCM: 1
    // IEEE: 3
    // A-law: 6
    // mu-law: 7
    // Extensible: 65534
    unsigned short audioFormat;
    short numChannels;
    int sampleRate;
    int byteRate;
    short blockAlign;
    short bitsPerSample;
    struct Extensible
    {
      std::uint16_t validBitsPerSample;
      std::uint16_t channelMask;
      std::uint32_t subFormat; // PCM, IEEE
    } extensible;
  } fmtChunk;

  struct FactChunk
  {
    bool valid = false;
    int size;
    int numSamples;
  } factChunk;

  struct DataChunk
  {
    bool valid = false;
    char id[4];
    int size;
  } dataChunk;
};

const unsigned short AUDIO_FORMAT_PCM = 1;
const unsigned short AUDIO_FORMAT_IEEE = 3;
const unsigned short AUDIO_FORMAT_ALAW = 6;
const unsigned short AUDIO_FORMAT_MULAW = 7;
const unsigned short AUDIO_FORMAT_EXTENSIBLE = 65534;

bool idIsNotJunk(char* id)
{
  return strncmp(id, "RIFF", 4) == 0 || strncmp(id, "WAVE", 4) == 0 || strncmp(id, "fmt ", 4) == 0
         || strncmp(id, "data", 4) == 0;
}

int ReadInt(std::ifstream& file)
{
  int value;
  file.read(reinterpret_cast<char*>(&value), 4);
  return value;
}

short ReadShort(std::ifstream& file)
{
  short value;
  file.read(reinterpret_cast<char*>(&value), 2);
  return value;
}

unsigned short ReadUnsignedShort(std::ifstream& file)
{
  unsigned short value;
  file.read(reinterpret_cast<char*>(&value), 2);
  return value;
}

dsp::wav::LoadReturnCode ReadJunk(std::ifstream& file)
{
  int chunkSize = ReadInt(file);
  file.ignore(chunkSize + (chunkSize % 2)); // Pad to 2 bytes at a time
  return file.good() ? dsp::wav::LoadReturnCode::SUCCESS : dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
}

std::string dsp::wav::GetMsgForLoadReturnCode(LoadReturnCode retCode)
{
  std::stringstream message;

  switch (retCode)
  {
    case (LoadReturnCode::ERROR_OPENING):
      message << "Failed to open file (is it being used by another "
                 "program?)";
      break;
    case (LoadReturnCode::ERROR_NOT_RIFF): message << "File is not a WAV file."; break;
    case (LoadReturnCode::ERROR_NOT_WAVE): message << "File is not a WAV file."; break;
    case (LoadReturnCode::ERROR_MISSING_FMT): message << "File is missing expected format chunk."; break;
    case (LoadReturnCode::ERROR_INVALID_FILE): message << "WAV file contents are invalid."; break;
    case (LoadReturnCode::ERROR_UNSUPPORTED_FORMAT_ALAW): message << "Unsupported file format \"A-law\""; break;
    case (LoadReturnCode::ERROR_UNSUPPORTED_FORMAT_MULAW): message << "Unsupported file format \"mu-law\""; break;
    case (LoadReturnCode::ERROR_UNSUPPORTED_FORMAT_OTHER): message << "Unsupported file format."; break;
    case (LoadReturnCode::ERROR_NOT_MONO): message << "File is not mono."; break;
    case (LoadReturnCode::ERROR_UNSUPPORTED_BITS_PER_SAMPLE): message << "Unsupported bits per sample"; break;
    case (dsp::wav::LoadReturnCode::ERROR_OTHER): message << "???"; break;
    default: message << "???"; break;
  }

  return message.str();
}

dsp::wav::LoadReturnCode ReadRiffChunk(std::ifstream& wavFile, WaveFileData::RiffChunk& chunk)
{
  if (chunk.valid)
  {
    std::cerr << "Error: RIFF chunk already read." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  chunk.size = ReadInt(wavFile);
  wavFile.read(chunk.format, 4);
  if (strncmp(chunk.format, "WAVE", 4) != 0)
  {
    std::cerr << "Error: File format is not expected 'WAVE'. Got '" << chunk.format << "' instead." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_NOT_WAVE;
  }
  chunk.valid = true;
  return dsp::wav::LoadReturnCode::SUCCESS;
}

dsp::wav::LoadReturnCode ReadFmtChunk(std::ifstream& wavFile, WaveFileData& wfd, double& sampleRate)
{
  if (wfd.fmtChunk.valid)
  {
    std::cerr << "Error: Format chunk already read." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  if (!wfd.riffChunk.valid)
  {
    std::cerr << "Error: Missing RIFF chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }

  wfd.fmtChunk.size = ReadInt(wavFile);
  if (wfd.fmtChunk.size < 16)
  {
    std::cerr << "WAV chunk 1 size is " << wfd.fmtChunk.size
              << ", which is smaller than the requried 16 to fit the expected "
                 "information."
              << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }

  wfd.fmtChunk.audioFormat = ReadUnsignedShort(wavFile);
  std::unordered_set<unsigned short> supportedFormats{AUDIO_FORMAT_PCM, AUDIO_FORMAT_IEEE, AUDIO_FORMAT_EXTENSIBLE};
  if (supportedFormats.find(wfd.fmtChunk.audioFormat) == supportedFormats.end())
  {
    std::cerr << "Error: Unsupported WAV format detected. ";
    switch (wfd.fmtChunk.audioFormat)
    {
      case AUDIO_FORMAT_ALAW:
        std::cerr << "(Got: A-law)" << std::endl;
        return dsp::wav::LoadReturnCode::ERROR_UNSUPPORTED_FORMAT_ALAW;
      case AUDIO_FORMAT_MULAW:
        std::cerr << "(Got: mu-law)" << std::endl;
        return dsp::wav::LoadReturnCode::ERROR_UNSUPPORTED_FORMAT_MULAW;
      default:
        std::cerr << "(Got unknown format " << wfd.fmtChunk.audioFormat << ")" << std::endl;
        return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
    }
  }

  wfd.fmtChunk.numChannels = ReadShort(wavFile);
  // HACK
  // Note for future: for multi-channel files, samples are laid out with channel in the inner loop.
  if (wfd.fmtChunk.numChannels != 1)
  {
    std::cerr << "Require mono (using for IR loading)" << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_NOT_MONO;
  }

  wfd.fmtChunk.sampleRate = ReadInt(wavFile);
  wfd.fmtChunk.byteRate = ReadInt(wavFile);
  wfd.fmtChunk.blockAlign = ReadShort(wavFile);
  wfd.fmtChunk.bitsPerSample = ReadShort(wavFile);
  int bytesRead = 16;

  if (wfd.fmtChunk.audioFormat == AUDIO_FORMAT_EXTENSIBLE)
  {
    unsigned short cbSize = ReadUnsignedShort(wavFile);
    // Do we need to assert or modify the data loading below if this doesn't match bitsPerSample?
    wfd.fmtChunk.extensible.validBitsPerSample = ReadUnsignedShort(wavFile);
    auto read_u32 = [&]() -> std::uint32_t {
      std::uint8_t b[4];
      wavFile.read((char*)b, 4);
      return b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
    };
    wfd.fmtChunk.extensible.channelMask = read_u32();
    std::uint8_t guid[16];
    wavFile.read((char*)guid, 16);
    wfd.fmtChunk.extensible.subFormat = guid[1] << 8 | guid[0];
    bytesRead += cbSize + 2; // Don't forget the 2 for the cbSize itself!
  }

  // Skip any extra bytes in the fmt chunk
  // This should probably be a remainder of a dword so that we're mod-4
  if (wfd.fmtChunk.size > bytesRead)
  {
    const int extraBytes = wfd.fmtChunk.size - bytesRead;
    if (extraBytes >= 4)
    {
      std::cerr << "More than 4 extra bytes in fmt chunk." << std::endl;
      return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
    }
    wavFile.ignore(extraBytes);
  }

  // Store SR for final return
  sampleRate = (double)wfd.fmtChunk.sampleRate;

  wfd.fmtChunk.valid = true;
  return dsp::wav::LoadReturnCode::SUCCESS;
}

dsp::wav::LoadReturnCode ReadFactChunk(std::ifstream& wavFile, WaveFileData& wfd)
{
  if (wfd.factChunk.valid)
  {
    std::cerr << "Error: Duplicate fact chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  if (!wfd.riffChunk.valid)
  {
    std::cerr << "Error: Missing RIFF chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  // We could assert that the fmt chunk was also read first but I'm not sure that's necessary for the file to be valid.

  wfd.factChunk.size = ReadInt(wavFile);
  if (wfd.factChunk.size != 4)
  {
    std::cerr << "Error: Invalid fact chunk size. Only 4 is supported; got " << wfd.factChunk.size << " instead."
              << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  wfd.factChunk.numSamples = ReadInt(wavFile);

  wfd.factChunk.valid = true;
  return dsp::wav::LoadReturnCode::SUCCESS;
}

int GetAudioFormat(WaveFileData& wfd)
{
  return wfd.fmtChunk.audioFormat == AUDIO_FORMAT_EXTENSIBLE ? wfd.fmtChunk.extensible.subFormat
                                                             : wfd.fmtChunk.audioFormat;
}

dsp::wav::LoadReturnCode ReadDataChunk(std::ifstream& wavFile, WaveFileData& wfd, std::vector<float>& audio)
{
  if (wfd.dataChunk.valid)
  {
    std::cerr << "Error: Already read data chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  if (!wfd.riffChunk.valid)
  {
    std::cerr << "Error: Missing RIFF chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  if (!wfd.fmtChunk.valid) // fmt chunk must come before data chunk
  {
    std::cerr << "Error: Tried to read data chunk before fmt chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  if (wfd.fmtChunk.audioFormat == AUDIO_FORMAT_EXTENSIBLE
      && !wfd.factChunk.valid) // fact chunk must come before data chunk
  {
    std::cerr << "Error: Tried to read data chunk before fact chunk for extensible format WAVE file." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }

  // Size of the data chunk, in bits.
  wfd.dataChunk.size = ReadInt(wavFile);

  const int audioFormat = GetAudioFormat(wfd);
  if (audioFormat == AUDIO_FORMAT_IEEE)
  {
    if (wfd.fmtChunk.bitsPerSample == 32)
      dsp::wav::_LoadSamples32FloatingPoint(wavFile, wfd.dataChunk.size, audio);
    else
    {
      std::cerr << "Error: Unsupported bits per sample for IEEE files: " << wfd.fmtChunk.bitsPerSample << std::endl;
      return dsp::wav::LoadReturnCode::ERROR_UNSUPPORTED_BITS_PER_SAMPLE;
    }
  }
  else if (audioFormat == AUDIO_FORMAT_PCM)
  {
    if (wfd.fmtChunk.bitsPerSample == 16)
      dsp::wav::_LoadSamples16(wavFile, wfd.dataChunk.size, audio);
    else if (wfd.fmtChunk.bitsPerSample == 24)
      dsp::wav::_LoadSamples24(wavFile, wfd.dataChunk.size, audio);
    else if (wfd.fmtChunk.bitsPerSample == 32)
      dsp::wav::_LoadSamples32FixedPoint(wavFile, wfd.dataChunk.size, audio);
    else
    {
      std::cerr << "Error: Unsupported bits per sample for PCM files: " << wfd.fmtChunk.bitsPerSample << std::endl;
      return dsp::wav::LoadReturnCode::ERROR_UNSUPPORTED_BITS_PER_SAMPLE;
    }
  }
  else
  {
    std::cerr << "Error: Unsupported audio format: " << audioFormat << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_UNSUPPORTED_FORMAT_OTHER;
  }
  wfd.dataChunk.valid = true;
  return dsp::wav::LoadReturnCode::SUCCESS;
}

dsp::wav::LoadReturnCode dsp::wav::Load(const char* fileName, std::vector<float>& audio, double& sampleRate)
{
  // FYI: https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html
  // Open the WAV file for reading
  std::ifstream wavFile(fileName, std::ios::binary);

  // Check if the file was opened successfully
  if (!wavFile.is_open())
  {
    std::cerr << "Error opening WAV file" << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_OPENING;
  }

  char chunkId[4];
  auto ReadChunkID = [&]() { wavFile.read(chunkId, 4); };

  WaveFileData wfd;
  dsp::wav::LoadReturnCode returnCode;
  while (!wfd.dataChunk.valid && !wavFile.eof())
  {
    ReadChunkID();
    if (!wfd.riffChunk.valid && strncmp(chunkId, "RIFF", 4) != 0)
    {
      {
        std::cerr << "Error: File does not start with expected RIFF chunk. Got" << chunkId << " instead." << std::endl;
        wavFile.close();
        return dsp::wav::LoadReturnCode::ERROR_NOT_RIFF;
      }
    }
    // Read the various chunks
    if (strncmp(chunkId, "RIFF", 4) == 0)
    {
      returnCode = ReadRiffChunk(wavFile, wfd.riffChunk);
    }
    else if (strncmp(chunkId, "fmt ", 4) == 0)
    {
      returnCode = ReadFmtChunk(wavFile, wfd, sampleRate);
    }
    else if (strncmp(chunkId, "fact", 4) == 0)
    {
      returnCode = ReadFactChunk(wavFile, wfd);
    }
    else if (strncmp(chunkId, "data", 4) == 0)
    {
      returnCode = ReadDataChunk(wavFile, wfd, audio);
    }
    else
    { // There might be junk chunks; just ignore them.
      returnCode = ReadJunk(wavFile);
    }
    if (returnCode != dsp::wav::LoadReturnCode::SUCCESS)
    {
      wavFile.close();
      return returnCode;
    }
  }
  wavFile.close();
  if (!wfd.dataChunk.valid)
  { // This implicitly asserts that the fmt chunk was read and gave us the sample rate
    std::cerr << "Error: File does not contain expected data chunk." << std::endl;
    return dsp::wav::LoadReturnCode::ERROR_INVALID_FILE;
  }
  return dsp::wav::LoadReturnCode::SUCCESS;
}

void dsp::wav::_LoadSamples16(std::ifstream& wavFile, const int chunkSize, std::vector<float>& samples)
{
  // Allocate an array to hold the samples
  std::vector<short> tmp(chunkSize / 2); // 16 bits (2 bytes) per sample

  // Read the samples from the file into the array
  wavFile.read(reinterpret_cast<char*>(tmp.data()), chunkSize);

  // Copy into the return array
  const float scale = 1.0 / ((double)(1 << 15));
  samples.resize(tmp.size());
  for (auto i = 0; i < samples.size(); i++)
    samples[i] = scale * ((float)tmp[i]); // 2^16
}

void dsp::wav::_LoadSamples24(std::ifstream& wavFile, const int chunkSize, std::vector<float>& samples)
{
  // Allocate an array to hold the samples
  std::vector<int> tmp(chunkSize / 3); // 24 bits (3 bytes) per sample
  // Read in and convert the samples
  for (int& x : tmp)
  {
    x = dsp::wav::_ReadSigned24BitInt(wavFile);
  }

  // Copy into the return array
  const float scale = 1.0 / ((double)(1 << 23));
  samples.resize(tmp.size());
  for (auto i = 0; i < samples.size(); i++)
    samples[i] = scale * ((float)tmp[i]);
}

int dsp::wav::_ReadSigned24BitInt(std::ifstream& stream)
{
  // Read the three bytes of the 24-bit integer.
  std::uint8_t bytes[3];
  stream.read(reinterpret_cast<char*>(bytes), 3);

  // Combine the three bytes into a single integer using bit shifting and
  // masking. This works by isolating each byte using a bit mask (0xff) and then
  // shifting the byte to the correct position in the final integer.
  int value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

  // The value is stored in two's complement format, so if the most significant
  // bit (the 24th bit) is set, then the value is negative. In this case, we
  // need to extend the sign bit to get the correct negative value.
  if (value & (1 << 23))
  {
    value |= ~((1 << 24) - 1);
  }

  return value;
}

void dsp::wav::_LoadSamples32FloatingPoint(std::ifstream& wavFile, const int chunkSize, std::vector<float>& samples)
{
  // NOTE: 32-bit is float.
  samples.resize(chunkSize / 4); // 32 bits (4 bytes) per sample
  // Read the samples from the file into the array
  wavFile.read(reinterpret_cast<char*>(samples.data()), chunkSize);
}

void dsp::wav::_LoadSamples32FixedPoint(std::ifstream& wavFile, const int chunkSize, std::vector<float>& samples)
{
  // Allocate an array to hold the samples
  std::vector<int> tmp(chunkSize / 4); // 32 bits (4 bytes) per sample

  // Read the samples from the file into the array
  wavFile.read(reinterpret_cast<char*>(tmp.data()), chunkSize);

  // Copy into the return array
  const float scale = 1.0 / ((double)(1 << 31)); // 2^31 for 32-bit fixed point
  samples.resize(tmp.size());
  for (auto i = 0; i < samples.size(); i++)
    samples[i] = scale * ((float)tmp[i]);
}
