using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MidiPlayerTK
{
    public class SFFile
    {
        public static string[] idlist =
            {
            "UNKN",
            "RIFF","LIST", "sfbk","INFO","sdta","pdta","ifil","isng","INAM","irom",
            "iver","ICRD","IENG","IPRD","ICOP","ICMT","ISFT","snam","smpl","phdr",
            "pbag","pmod","pgen","inst","ibag","imod","igen","shdr","sm24"
        };

        /* sfont file chunk sizes */
        public const int SFPHDRSIZE = 38;
        public const int SFBAGSIZE = 4;
        public const int SFMODSIZE = 10;
        public const int SFGENSIZE = 4;
        public const int SFIHDRSIZE = 22;
        public const int SFSHDRSIZE = 46;

        public const int SF_SAMPLETYPE_MONO = 1;   // Value used for mono samples
        public const int SF_SAMPLETYPE_RIGHT = 2;  // Value used for right samples of a stereo pair
        public const int SF_SAMPLETYPE_LEFT = 4;   // Value used for left samples of a stereo pair
        public const int SF_SAMPLETYPE_LINKED = 8; // Value used for linked sample, which is currently not supported
        public const int FLUID_SAMPLETYPE_OGG_VORBIS = 0x10; /**< Flag used for Ogg Vorbis compressed samples (non-standard compliant extension) as found in the program "sftools" developed by Werner Schweer from MuseScore @since 1.1.7 */
        public const int SF_SAMPLETYPE_ROM = 0x8000; // 32768 Flag that indicates ROM samples, causing the sample to be ignored

        public const int SF_SAMPMODES_LOOP = 1;
        public const int SF_SAMPMODES_UNROLL = 2;

        public const int SF_MIN_SAMPLERATE = 400;
        public const int SF_MAX_SAMPLERATE = 50000;

        public const int SF_MIN_SAMPLE_LENGTH = 32;

        //const int SFGen_ArraySize = sizeof(SFGenAmount) * SFGen_Count	/* gen array size */
        public const int zero_size = 0;	/* a 0 value used with WRITECHUNK macro */


        static public bool Verbose = false;

        public enum LogLevel
        {
            Panic,   /**< The synth can't function correctly any more */
            Error,     /**< Serious error occurred */
            Warn,    /**< Warning */
            Info,    /**< Verbose informational messages */
            Debug,     /**< Debugging messages */
        }

        public enum SfSource { SF2, MPTK, }

        public static string EscapeConvert(string name)
        {
            string conv = name;
            foreach (char i in Path.GetInvalidFileNameChars())
            {
                conv = conv.Replace(i, '_');
            }
            conv = conv.Replace('#', 'd');
            conv = conv.Replace('.', '-');
            conv = conv.Replace(' ', '-');
            return conv;
        }

        static public void Log(LogLevel level, string fmt, params object[] list)
        {
            //Console.WriteLine(string.Format(fmt, list));
            Debug.Log(string.Format(fmt, list));
        }

        public static void CheckSampleName(SFData sf)
        {
            foreach (HiSample s in sf.Samples)
                while (SearchSampleByName(sf, s))
                {
                    if (s.Name.Length > 15)
                        s.Name = s.Name.Remove(15); // sample name must not exceed 20 characters
                    s.Name += $"_{s.ItemId}";
                }

        }
        private static bool SearchSampleByName(SFData sf, HiSample hiSample)
        {
            foreach (HiSample s in sf.Samples)
                if (string.Compare(s.Name, hiSample.Name) == 0 && s.Start != hiSample.Start && s.End != hiSample.End)
                    // Same name but different sample
                    return true;
            return false;
        }


        public static void DumpSFToFile(SFData sf, string filename)
        {
            using (StreamWriter writer = File.CreateText(filename))
            {
                foreach (HiPreset p in sf.preset)
                {
                    if (p != null)
                    {
                        writer.WriteLine("Preset itemid:{0} bank:{1} prenum:{2} '{3}'", p.ItemId, p.Bank, p.Num, p.Name);
                        foreach (HiZone z in p.Zone)
                        {
                            if (z.Index < 0)
                                writer.WriteLine("   PZone itemid:{0} index:{1} Global zone", z.ItemId, z.Index);
                            else if (z.Index < sf.inst.Length)
                                writer.WriteLine("   PZone itemid:{0} index:{1} instrument:'{2}'", z.ItemId, z.Index, sf.inst[z.Index].Name);
                            else
                                writer.WriteLine("   PZone itemid:{0} index:{1} *** index not in instrument range ***", z.ItemId, z.Index);

                            if (z.gens != null)
                                foreach (HiGen g in z.gens)
                                {
                                    if (g.Amount != null)
                                        writer.WriteLine("      Gen Id:{0,-20} lo:{1:000} hi:{2:000} sword:{3}", g.type, g.Amount.Lo, g.Amount.Hi, g.Amount.Sword);
                                    else
                                        writer.WriteLine("      Gen Id:{0,-20} amount is null", g.type);
                                }
                            if (z.mods != null)
                                foreach (HiMod m in z.mods)
                                {
                                    writer.WriteLine("      Mod amount:{0} {1} {2} {3} {4} ", m.SfSrc, m.dest, m.SfAmtSrc, m.SfTrans, m.amount);
                                }
                        }
                    }
                }

                writer.WriteLine("------------- Instrument -------------");

                foreach (HiInstrument p in sf.inst)
                {
                    //if (p.name.Contains("Mellow"))
                    {
                        writer.WriteLine("Instru itemid:{0} '{1}'", p.ItemId, p.Name);

                        foreach (HiZone z in p.Zone)
                        {
                            if (z.Index < 0)
                                writer.WriteLine("   IZone itemid:{0} index:{1} Global zone", z.ItemId, z.Index);
                            else if (z.Index < sf.Samples.Length)
                                writer.WriteLine("   IZone itemid:{0} index:{1} sample:'{2}'", z.ItemId, z.Index, sf.Samples[z.Index].Name);
                            else
                                writer.WriteLine("   IZone itemid:{0} index:{1} *** index not in sample range ***", z.ItemId, z.Index);


                            if (z.gens != null)
                            {
                                if (z.gens != null)
                                    foreach (HiGen g in z.gens)
                                    {
                                        if (g.Amount != null)
                                            writer.WriteLine("      Gen Id:{0,-20} lo:{1:000} hi:{2:000} sword:{3}", g.type, g.Amount.Lo, g.Amount.Hi, g.Amount.Sword);
                                        else
                                            writer.WriteLine("      Gen Id:{0,-20} amount is null", g.type);
                                    }
                                if (z.mods != null)
                                    foreach (HiMod m in z.mods)
                                    {
                                        writer.WriteLine("      Mod amount:{0} {1} {2} {3} {4} ", m.SfSrc, m.dest, m.SfAmtSrc, m.SfTrans, m.amount);
                                    }
                            }
                        }
                    }
                }

                writer.WriteLine("------------- Sample -------------");
                foreach (HiSample s in sf.Samples)
                {
                    writer.WriteLine("itemid:{0} '{1,-20}' start:{2} end:{3} loopstart:{4} loopend:{5} origpitch:{6} pitchadj:{7} samplerate:{8}",
                        s.ItemId, s.Name, s.Start, s.End, s.LoopStart, s.LoopEnd, s.OrigPitch, s.PitchAdj, s.SampleRate);
                }

            }
        }

        static public void Sort(SFData sfdata)
        {
            Array.Sort(sfdata.preset, delegate (HiPreset sp1, HiPreset sp2) { return (sp1.Bank * 256 + sp1.Num).CompareTo(sp2.Bank * 256 + sp2.Num); });
            //Array.Sort(sfdata.preset, delegate (SFPreset sp1, SFPreset sp2) { return sp1.bank.CompareTo(sp2.bank); });
        }
    }


    /// <summary>@brief
    /// An encoding for use with file types that have one byte per character
    /// </summary>
    public class ByteEncoding : Encoding
    {
        private ByteEncoding()
        {
        }

        /// <summary>@brief
        /// The one and only instance of this class
        /// </summary>
        public static readonly ByteEncoding Instance = new ByteEncoding();

        /// <summary>@brief
        /// <see cref="Encoding.GetByteCount(char[],int,int)"/>
        /// </summary>
        public override int GetByteCount(char[] chars, int index, int count)
        {
            return count;
        }

        /// <summary>@brief
        /// <see cref="Encoding.GetBytes(char[],int,int,byte[],int)"/>
        /// </summary>
        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            for (int n = 0; n < charCount; n++)
            {
                bytes[byteIndex + n] = (byte)chars[charIndex + n];
            }
            return charCount;
        }

        /// <summary>@brief
        /// <see cref="Encoding.GetCharCount(byte[],int,int)"/>
        /// </summary>
        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            for (int n = 0; n < count; n++)
            {
                if (bytes[index + n] == 0)
                    return n;
            }
            return count;
        }

        /// <summary>@brief
        /// <see cref="Encoding.GetChars(byte[],int,int,char[],int)"/>
        /// </summary>
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            for (int n = 0; n < byteCount; n++)
            {
                var b = bytes[byteIndex + n];
                if (b == 0)
                {
                    return n;
                }
                chars[charIndex + n] = (char)b;
            }
            return byteCount;
        }

        /// <summary>@brief
        /// <see cref="Encoding.GetMaxCharCount"/>
        /// </summary>
        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }

        /// <summary>@brief
        /// <see cref="Encoding.GetMaxByteCount"/>
        /// </summary>
        public override int GetMaxByteCount(int charCount)
        {
            return charCount;
        }
    }

    /* sfont file data structures */
    public class SFChunk
    {               /* RIFF file chunk structure */
        public string id;         /* chunk id */
        public int size;           /* size of the following chunk */
    }

    /* Sound Font structure defines */

    public class SFVersion
    {               /* version structure */
        public ushort major;
        public ushort minor;
    }

    //public class SFMod
    //{
    //    /* Modulator structure */
    //    public ushort src;            /* source modulator */
    //    public ushort dest;           /* destination generator */
    //    public short amount;      /* signed, degree of modulation */
    //    public ushort amtsrc;     /* second source controls amnt of first */
    //    public ushort trans;       /* transform applied to source */
    //}
    //public class SFGenAmount

    //{               /* Generator amount structure */
    //    public ushort uword;      /* unsigned 16 bit value */

    //    /// <summary>@brief
    //    /// Generator amount as a signed short
    //    /// </summary>
    //    public short sword
    //    {
    //        get { return (short)uword; }
    //        set { uword = (ushort)value; }
    //    }

    //    /// <summary>@brief
    //    /// Low byte amount
    //    /// </summary>
    //    public byte lo
    //    {
    //        get { return (byte)(uword & 0x00FF); }
    //        set { uword &= 0xFF00; uword += value; }
    //    }

    //    /// <summary>@brief
    //    /// High byte amount
    //    /// </summary>
    //    public byte hi
    //    {
    //        get { return (byte)((uword & 0xFF00) >> 8); }
    //        set { uword &= 0x00FF; uword += (ushort)(value << 8); }
    //    }
    //}

    //public class SFGen
    //{
    //    /// <summary>@brief
    //    /// generator ID
    //    /// </summary>
    //    public fluid_gen_type id;
    //    /// <summary>@brief
    //    /// generator value
    //    /// </summary>
    //    public SFGenAmount amount;
    //}

    //public class SFZone
    //{
    //    /// <summary>@brief
    //    /// unique item id (see int note above)
    //    /// </summary>
    //    public int itemid;
    //    /// <summary>@brief
    //    /// index instrument/sample index for zone
    //    /// </summary>
    //    public short index;
    //    /// <summary>@brief
    //    /// list of generators
    //    /// </summary>
    //    public SFGen[] gens;
    //    /// <summary>@brief
    //    /// list of modulators
    //    /// </summary>
    //    public SFMod[] mods;
    //}

    //public class SFSample
    //{
    //    /// <summary>@brief
    //    /// unique item id (see int note above)
    //    /// </summary>
    //    public int itemid;
    //    /// <summary>@brief
    //    /// Name of sample
    //    /// </summary>
    //    public string name;
    //    /// <summary>@brief
    //    /// Offset in sample area to start of sample
    //    /// </summary>
    //    public uint start;
    //    /// <summary>@brief
    //    /// Offset from start to end of sample, this is the last point of the sample, the SF spec has this as the 1st point after, corrected on load/save
    //    /// </summary>
    //    public uint end;
    //    /// <summary>@brief
    //    /// Offset from start to start of loop
    //    /// </summary>
    //    public uint loopstart;
    //    /// <summary>@brief
    //    /// Offset from start to end of loop, marks the first point after loop, whose sample value is ideally equivalent to loopstart
    //    /// </summary>
    //    public uint loopend;
    //    /// <summary>@brief
    //    /// Sample rate recorded at
    //    /// </summary>
    //    public uint samplerate;
    //    /// <summary>@brief
    //    /// root midi key number
    //    /// </summary>
    //    public byte origpitch;
    //    /// <summary>@brief
    //    /// pitch correction in cents
    //    /// </summary>
    //    public byte pitchadj;
    //    /// <summary>@brief
    //    /// 1 mono,2 right,4 left,linked 8,0x8000=ROM
    //    /// </summary>
    //    public ushort sampletype;
    //}

    //public class SFInst
    //{
    //    /// <summary>@brief
    //    /// unique item id (see int note above)
    //    /// </summary>
    //    public int itemid;
    //    /// <summary>@brief
    //    /// Name of instrument
    //    /// </summary>
    //    public string name;
    //    /// <summary>@brief
    //    /// list of instrument zones
    //    /// </summary>
    //    public SFZone[] zone;
    //}


    public class SFData
    {
        public SFFile.SfSource Source;
        // V2.89.0 removed  public int itemid;
        // version to build The ifil sub-chunk identifying the SoundFont specification version level to which the file complies.It is always four bytes in length.
        public SFVersion version;
        // Loaded if exist but not saved
        public SFVersion romver;
        public uint samplepos;
        public string fname;
        public List<SFInfo> info;
        public HiPreset[] preset;
        public HiInstrument[] inst;
        public HiSample[] Samples;
        public byte[] SampleData; // all sample, loaded when a new SoundFont is added to the MPTK DB
    }

    public class SFInfo
    {
        public File_Chunk_ID id;
        public string Text;
    }

    /* sf file chunk IDs */
    public enum File_Chunk_ID
    {
        UNKN_ID, RIFF_ID, LIST_ID, SFBK_ID,
        INFO_ID, SDTA_ID, PDTA_ID,  /* info/sample/preset */

        IFIL_ID, ISNG_ID, INAM_ID, IROM_ID, /* info ids (1st byte of info strings) */
        IVER_ID, ICRD_ID, IENG_ID, IPRD_ID, /* more info ids */
        ICOP_ID, ICMT_ID, ISFT_ID,  /* and yet more info ids */

        SNAM_ID, SMPL_ID,       /* sample ids */
        PHDR_ID, PBAG_ID, PMOD_ID, PGEN_ID, /* preset ids */
        IHDR_ID, IBAG_ID, IMOD_ID, IGEN_ID, /* instrument ids */
        SHDR_ID,         /* sample info */
        SM24_ID
    };

    /* generator types */
    //public enum Gen_Type
    //{
    //    Gen_StartAddrOfs, Gen_EndAddrOfs, Gen_StartLoopAddrOfs,
    //    Gen_EndLoopAddrOfs, Gen_StartAddrCoarseOfs, Gen_ModLFO2Pitch,
    //    Gen_VibLFO2Pitch, Gen_ModEnv2Pitch, Gen_FilterFc, Gen_FilterQ,
    //    Gen_ModLFO2FilterFc, Gen_ModEnv2FilterFc, Gen_EndAddrCoarseOfs,
    //    Gen_ModLFO2Vol, Gen_Unused1, Gen_ChorusSend, Gen_ReverbSend, Gen_Pan,
    //    Gen_Unused2, Gen_Unused3, Gen_Unused4,
    //    Gen_ModLFODelay, Gen_ModLFOFreq, Gen_VibLFODelay, Gen_VibLFOFreq,
    //    Gen_ModEnvDelay, Gen_ModEnvAttack, Gen_ModEnvHold, Gen_ModEnvDecay,
    //    Gen_ModEnvSustain, Gen_ModEnvRelease, Gen_Key2ModEnvHold,
    //    Gen_Key2ModEnvDecay, Gen_VolEnvDelay, Gen_VolEnvAttack,
    //    Gen_VolEnvHold, Gen_VolEnvDecay, Gen_VolEnvSustain, Gen_VolEnvRelease,
    //    Gen_Key2VolEnvHold, Gen_Key2VolEnvDecay, Gen_Instrument,
    //    Gen_Reserved1, Gen_KeyRange, Gen_VelRange,
    //    Gen_StartLoopAddrCoarseOfs, Gen_Keynum, Gen_Velocity,
    //    Gen_Attenuation, Gen_Reserved2, Gen_EndLoopAddrCoarseOfs,
    //    Gen_CoarseTune, Gen_FineTune, Gen_SampleId, Gen_SampleModes,
    //    Gen_Reserved3, Gen_ScaleTune, Gen_ExclusiveClass, Gen_OverrideRootKey,
    //    Gen_Dummy
    //}
}
