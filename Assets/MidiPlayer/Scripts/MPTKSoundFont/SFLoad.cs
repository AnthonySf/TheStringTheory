//#define DEBUGLOAD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MidiPlayerTK
{
    public class SFLoad
    {
        /* EMU8k/10k hardware applies this factor to initial attenuation generator values set at preset and
         * instrument level in a soundfont. We apply this factor when loading the generator values to stay
         * compatible as most existing soundfonts expect exactly this (strange, non-standard) behaviour. */
        public float EMU_ATTENUATION_FACTOR = 0.4f;

        public SFData SfData;
        private BinaryReader fd;
        /// <summary>@brief
        /// Source=0 from SF2
        /// Source=1 from MPTK SF
        /// </summary>


        public SFLoad(string filename, SFFile.SfSource psource)
        {
            // Load a SF from Maestro Unity resource in editor mode (for creating a MPTK SF) or from LiveSF (see SfSource)
            long size = new FileInfo(filename).Length;
            using (Stream sfFile = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                SfData = new SFData();
                SfData.Source = psource;
                using (fd = new BinaryReader(sfFile))
                    LoadBody(size);
            }
        }

        public SFLoad(byte[] bytes, SFFile.SfSource psource)
        {
            // Load a SF from Maestro Unity resource in editor mode (for creating a MPTK SF) or from LiveSF with LoadLiveSF (see SfSource)
            // Call from
            //      MidiPlayerGlobal.LoadCurrentSF() --> ImSoundFont.LoadMPTKSoundFont()
            //      ImSoundFontPro.LoadLiveSF
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                SfData = new SFData();
                SfData.Source = psource;
                using (fd = new BinaryReader(ms))
                    LoadBody(bytes.Length);
            }
        }

        //public SFLoad(Stream sfFile)
        //{
        //    fd = new BinaryReader(sfFile);
        //}

        /// <summary>@brief
        /// Read 4 bytes for the ID and 4 bytes for the length
        /// </summary>
        /// <returns></returns>
        private SFChunk ReadChunk()
        {
            byte[] cid = fd.ReadBytes(4);
            if (cid.Length != 4)
            {
                throw new Exception("Couldn't read Chunk ID");
            }

            SFChunk chunk = new SFChunk();
            chunk.id = ByteEncoding.Instance.GetString(cid, 0, cid.Length);
            chunk.size = (int)fd.ReadUInt32();
            return chunk;
        }

        /// <summary>@brief
        /// Read 4 bytes for the ID
        /// </summary>
        /// <returns></returns>
        private string ReadId()
        {
            byte[] cid = fd.ReadBytes(4);
            if (cid.Length != 4)
            {
                throw new Exception("Couldn't read Chunk ID");
            }
            return ByteEncoding.Instance.GetString(cid, 0, cid.Length);
        }

        private string ReadStr()
        {
            byte[] cid = fd.ReadBytes(20);
            if (cid.Length != 20)
                throw new Exception("Couldn't read 20 string");
            return ByteEncoding.Instance.GetString(cid, 0, cid.Length);
        }


        private File_Chunk_ID ChunkId(string id)
        {
            for (int i = 0; i < SFFile.idlist.Length; i++)
                if (SFFile.idlist[i] == id)
                    return (File_Chunk_ID)(i);

            return (File_Chunk_ID.UNKN_ID);
        }

        /// <summary>@brief
        /// sound font file load functions
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private SFData LoadBody(long size)
        {
            SFChunk chunk;


            chunk = ReadChunk();  /* load RIFF chunk */
            if (ChunkId(chunk.id) != File_Chunk_ID.RIFF_ID)   /* error if not RIFF */
                throw new Exception("Not a RIFF file");

            string sid = ReadId();  /* load file ID */
            if (ChunkId(sid) != File_Chunk_ID.SFBK_ID)   /* error if not SFBK_ID */
                throw new Exception("Not a sound font file");

            if (chunk.size != size - 8)
                throw new Exception("Sound font file size mismatch");
            //SFFile.Log(SFFile.LogLevel.Error, "Sound font file size mismatch {0} {1} {2}", chunk.size, size, chunk.size - size);

            /* Process INFO block */
            chunk = ReadListchunk();
            if (ChunkId(chunk.id) != File_Chunk_ID.INFO_ID)
                throw new Exception("Invalid ID found when expecting INFO chunk");
            if (SFFile.Verbose) SFFile.Log(SFFile.LogLevel.Info, "Load Info. Chunk Size:{0}", chunk.size);
            ProcessInfo(chunk.size);

            /* Process sample chunk */
            chunk = ReadListchunk();
            if (ChunkId(chunk.id) != File_Chunk_ID.SDTA_ID)
                throw new Exception("Invalid ID found when expecting SAMPLE chunk");
            if (SFFile.Verbose) SFFile.Log(SFFile.LogLevel.Info, "Load sdta. Chunk Size:{0}", chunk.size);
            ProcessSDta(chunk.size);

            /* process HYDRA chunk */
            chunk = ReadListchunk();
            if (ChunkId(chunk.id) != File_Chunk_ID.PDTA_ID)
                throw new Exception("Invalid ID found when expecting HYDRA chunk");
            if (SFFile.Verbose) SFFile.Log(SFFile.LogLevel.Info, "Load pdta. Chunk Size:{0}", chunk.size);
            ProcessPDta(chunk.size);

            //      if (SfData.Source == SFFile.SfSource.MPTK)
            {
                FixupPreset(SfData);
                FixupInstrument(SfData);
            }

            FixupSample(SfData);

            /* sort preset list by bank, preset # */
            //sf.preset = g_slist_sort(sf.preset,
            //  (GCompareFunc)sfont_preset_compare_func);

            return SfData;
        }

        private SFChunk ReadListchunk()
        {
            SFChunk chunk;
            chunk = ReadChunk();   /* read list chunk */
            if (ChunkId(chunk.id) != File_Chunk_ID.LIST_ID)  /* error if ! list chunk */
                throw new Exception("Invalid chunk id in level 0 parse");
            chunk.id = ReadId(); /* read id string */
            chunk.size -= 4;
            return chunk;
        }

        private void ProcessInfo(int size)
        {
            SFChunk chunk;
            File_Chunk_ID id;

            while (size > 0)
            {
                chunk = ReadChunk();
                size -= 8;

                id = ChunkId(chunk.id);

                if (id == File_Chunk_ID.IFIL_ID)
                {
                    /* sound font version chunk? */
                    if (chunk.size != 4)
                        throw new Exception("Sound font version info chunk has invalid size");

                    SfData.version = new SFVersion();
                    SfData.version.major = fd.ReadUInt16();
                    SfData.version.minor = fd.ReadUInt16();

                    if (SfData.version.major < 2)
                        throw new Exception(string.Format("Sound font version is {0}.{1} which is not supported, convert to version 2.0x", SfData.version.major, SfData.version.minor));

                    if (SfData.version.major > 2)
                        SFFile.Log(SFFile.LogLevel.Error, "Sound font version is {0}.{1} which is newer than what this version of Smurf was designed for (v2.0x)", SfData.version.major, SfData.version.minor);
                }
                else if (id == File_Chunk_ID.IVER_ID)
                {
                    /* ROM version chunk? */
                    if (chunk.size != 4)
                        throw new Exception("ROM version info chunk has invalid size");
                    SfData.romver = new SFVersion();
                    SfData.romver.major = fd.ReadUInt16();
                    SfData.romver.minor = fd.ReadUInt16();
                }
                else if (id != File_Chunk_ID.UNKN_ID)
                {
                    if ((id != File_Chunk_ID.ICMT_ID && chunk.size > 256) || (chunk.size > 65536) || (chunk.size % 2) != 0)
                        throw new Exception(string.Format("INFO sub chunk {0} has invalid chunk size of {1} bytes", chunk.id, chunk.size));

                    /* attach to INFO list, sfont_close will cleanup if FAIL occurs */

                    byte[] cid = fd.ReadBytes(chunk.size);
                    if (cid.Length != chunk.size)
                    {
                        throw new Exception(string.Format("INFO Couldn't read Chunk ID {0} ", id));
                    }
                    if (SfData.info == null)
                        SfData.info = new List<SFInfo>();
                    SFInfo info = new SFInfo();
                    info.id = id;
                    info.Text = ByteEncoding.Instance.GetString(cid, 0, cid.Length);
                    //info.Text = Encoding.UTF8.GetString(cid);// (cid, 0, cid.Length);
                    SfData.info.Add(info);

                    if (SFFile.Verbose) SFFile.Log(SFFile.LogLevel.Info, "   INFO {0} '{1}' Conv. bytes to string: {2} --> {3}", info.id, info.Text, cid.Length, info.Text.Length);

                }
                else
                    throw new Exception("Invalid chunk id in INFO chunk");
                size -= chunk.size;
            }

            if (size < 0)
                throw new Exception("INFO chunk size mismatch");
        }

        private void ProcessSDta(int size)
        {
            SFChunk chunk;

            if (size == 0)
            {
                return;        /* no sample data? */
            }

            /* read sub chunk */
            chunk = ReadChunk();
            size -= 8;

            if (ChunkId(chunk.id) != File_Chunk_ID.SMPL_ID)
                throw new Exception("Expected SMPL chunk found invalid id instead");

            if ((size - chunk.size) < 0)
                throw new Exception("SDTA chunk size mismatch");

            /* sample data follows */
            SfData.samplepos = (uint)fd.BaseStream.Position;
            if (SFFile.Verbose) SFFile.Log(SFFile.LogLevel.Info, "Samplepos:{0} size:{1}", SfData.samplepos, chunk.size);
            // if (SfData.Source == SFFile.SfSource.SF2)
            SfData.SampleData = fd.ReadBytes(chunk.size);
            //else
            //    // no need of the data samples for MPTK
            //    fd.BaseStream.Seek(chunk.size, SeekOrigin.Current);

            /* check for sm24 sub-chunk */
            if ((size - chunk.size) > 0)
            {
                size -= chunk.size;
                chunk = ReadChunk();
                size -= 8;
                if (ChunkId(chunk.id) != File_Chunk_ID.SM24_ID)
                    throw new Exception("Expected SM24 chunk found invalid id instead");
                if ((size - chunk.size) != 0)
                    throw new Exception("SDTA chunk size mismatch");
                fd.BaseStream.Seek(chunk.size, SeekOrigin.Current);
            }
        }

        private int HelperPDta(File_Chunk_ID expid, uint reclen, out SFChunk chunk, int size)
        {
            File_Chunk_ID id;
            int newsize;

            string expstr = SFFile.idlist[(int)expid];

            chunk = ReadChunk();
            newsize = size - 8;
            id = ChunkId(chunk.id);
            if (id != expid)
                throw new Exception(string.Format("Expected PDTA sub-chunk {0} found invalid id instead", expstr));

            if ((chunk.size % reclen) != 0)   /* valid chunk size? */
                throw new Exception(string.Format("{0} chunk size is not a multiple of {1} bytes", expstr, reclen));

            newsize -= chunk.size;
            if (newsize < 0)
                throw new Exception(string.Format("{0} chunk size exceeds remaining PDTA chunk size", expstr));

            return newsize;
        }

        private void ProcessPDta(int size)
        {
            SFChunk chunk;

            size = HelperPDta(File_Chunk_ID.PHDR_ID, SFFile.SFPHDRSIZE, out chunk, size);
            LoadPHdr(chunk.size);

            size = HelperPDta(File_Chunk_ID.PBAG_ID, SFFile.SFBAGSIZE, out chunk, size);
            LoadPBag(chunk.size);

            size = HelperPDta(File_Chunk_ID.PMOD_ID, SFFile.SFMODSIZE, out chunk, size);
            LoadPMod(chunk.size);

            size = HelperPDta(File_Chunk_ID.PGEN_ID, SFFile.SFGENSIZE, out chunk, size);
            LoadPGen(chunk.size);

            size = HelperPDta(File_Chunk_ID.IHDR_ID, SFFile.SFIHDRSIZE, out chunk, size);
            LoadIHdr(chunk.size);

            size = HelperPDta(File_Chunk_ID.IBAG_ID, SFFile.SFBAGSIZE, out chunk, size);
            LoadIBag(chunk.size);

            size = HelperPDta(File_Chunk_ID.IMOD_ID, SFFile.SFMODSIZE, out chunk, size);
            LoadIMod(chunk.size);

            size = HelperPDta(File_Chunk_ID.IGEN_ID, SFFile.SFGENSIZE, out chunk, size);
            LoadIGen(chunk.size);

            size = HelperPDta(File_Chunk_ID.SHDR_ID, SFFile.SFSHDRSIZE, out chunk, size);
            LoadSHdr(chunk.size);
        }

        /* preset header loader */
        private void LoadPHdr(int size)
        {
            int count, i2;
            HiPreset p, pr = null;    /* ptr to current & previous preset */
            ushort zndx, pzndx = 0;

            if ((size % SFFile.SFPHDRSIZE) != 0 || size == 0)
                throw new Exception("Preset header chunk size is invalid");

            count = size / SFFile.SFPHDRSIZE - 1;
            if (count == 0)
            {               /* at least one preset + term record */
                SFFile.Log(SFFile.LogLevel.Info, "File contains no presets");
                fd.BaseStream.Seek(SFFile.SFPHDRSIZE, SeekOrigin.Current);

                return;
            }

            SfData.preset = new HiPreset[count];
            for (int ip = 0; ip < count; ip++)
            {
                /* load all preset headers */
                p = new HiPreset();
                SfData.preset[ip] = p;

                p.Name = ReadStr();
                p.ItemId = ip; // V2.89.0 was ItemId=count, replaced by ItemId=ip, position of the preset in the list
                p.Num = fd.ReadUInt16();
                p.Bank = fd.ReadUInt16();
                zndx = fd.ReadUInt16();
                p.Libr = fd.ReadUInt32();
                p.Genre = fd.ReadUInt32();
                p.Morph = fd.ReadUInt32();

                if (pr != null)
                {
                    /* not first preset? */
                    if (zndx < pzndx)
                        throw new Exception("Preset header indices not monotonic");
                    i2 = zndx - pzndx;
                    if (i2 > 0)
                        pr.Zone = new HiZone[i2];
                }
                else if (zndx > 0)
                    /* 1st preset, warn if ofs >0 */
                    SFFile.Log(SFFile.LogLevel.Warn, "{0} preset zones not referenced, discarding", zndx);
                /* update preset ptr */
                pr = p;
                pzndx = zndx;
            }
            fd.BaseStream.Seek(24, SeekOrigin.Current);

            /* Read terminal generator index */
            zndx = fd.ReadUInt16();

            fd.BaseStream.Seek(12, SeekOrigin.Current);

            if (zndx < pzndx)
                throw new Exception("Preset header indices not monotonic");
            i2 = zndx - pzndx;
            if (i2 > 0)
                pr.Zone = new HiZone[i2];
        }

        /* preset bag loader */
        private void LoadPBag(int size)
        {
            HiZone pz = null;
            ushort genndx, modndx;
            ushort pgenndx = 0, pmodndx = 0;
            int i;

            if ((size % SFFile.SFBAGSIZE) != 0 || size == 0)  /* size is multiple of SFBAGSIZE? */
                throw new Exception("Preset bag chunk size is invalid");
            foreach (HiPreset p in SfData.preset)
            {
                /* traverse through presets */
                for (int iz = 0; iz < p.Zone.Length; iz++)
                {
                    /* traverse preset's zones */
                    if ((size -= SFFile.SFBAGSIZE) < 0)
                        throw new Exception("Preset bag chunk size mismatch");
                    p.Zone[iz] = new HiZone();
                    p.Zone[iz].ItemId = iz;
                    p.Zone[iz].gens = null;  // Init gen and mod before possible failure, 
                    p.Zone[iz].mods = null;  // to ensure proper cleanup (sfont_close) 
                    genndx = fd.ReadUInt16();  // possible read failure ^ 
                    modndx = fd.ReadUInt16();
                    p.Zone[iz].Index = -1;

                    if (pz != null)
                    {
                        // if not first zone
                        if (genndx < pgenndx) throw new Exception("Preset bag generator indices not monotonic");
                        if (modndx < pmodndx) throw new Exception("Preset bag modulator indices not monotonic");
                        i = genndx - pgenndx;
                        if (i > 0) pz.gens = new HiGen[i];
                        i = modndx - pmodndx;
                        if (i > 0) pz.mods = new HiMod[i];
                    }
                    pz = p.Zone[iz];     // update previous zone ptr 
                    pgenndx = genndx;   // update previous zone gen index 
                    pmodndx = modndx;   // update previous zone mod index 
                }
            }

            size -= SFFile.SFBAGSIZE;
            if (size != 0)
                throw new Exception("Preset bag chunk size mismatch");

            genndx = fd.ReadUInt16();
            modndx = fd.ReadUInt16();

            if (pz == null)
            {
                if (genndx > 0) throw new Exception("No preset generators and terminal index not 0");
                if (modndx > 0) throw new Exception("No preset modulators and terminal index not 0");
                return;
            }

            if (genndx < pgenndx) throw new Exception("Preset bag generator indices not monotonic");
            if (modndx < pmodndx) throw new Exception("Preset bag modulator indices not monotonic");

            i = genndx - pgenndx;
            if (i > 0) pz.gens = new HiGen[i];

            i = modndx - pmodndx;
            if (i > 0) pz.mods = new HiMod[i];
        }

        private void LoadPMod(int size)
        {
            foreach (HiPreset p in SfData.preset)
            {
                for (int iz = 0; iz < p.Zone.Length; iz++)
                {
                    if (p.Zone[iz].mods != null)
                        for (int im = 0; im < p.Zone[iz].mods.Length; im++)
                        {
                            if ((size -= SFFile.SFMODSIZE) < 0)
                                throw new Exception("Preset modulator chunk size mismatch");
                            HiMod m = new HiMod();
                            p.Zone[iz].mods[im] = m;

                            m.SfSrc = fd.ReadUInt16();
                            m.dest = (byte)fd.ReadUInt16();
                            m.amount = fd.ReadInt16();
                            m.SfAmtSrc = fd.ReadUInt16();
                            m.SfTrans = fd.ReadUInt16();
                            ProcessModulator(m);
                        }
                }
            }


            // If there isn't even a terminal record/ Hmmm, the specs say there should be one, but..
            if (size == 0)
                return;

            size -= SFFile.SFMODSIZE;
            if (size != 0) throw new Exception("Preset modulator chunk size mismatch");
            fd.BaseStream.Seek(SFFile.SFMODSIZE, SeekOrigin.Current);
        }

        private void ProcessModulator(HiMod m)
        {
            m.src1 = (byte)(m.SfSrc & 127); // index of source 1, seven-bit value, SF2.01 section 8.2, page 50
            m.flags1 = 0;

            /* Bit 7: CC flag SF 2.01 section 8.2.1 page 50*/
            if ((m.SfSrc & (1 << 7)) != 0)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_CC;
            }
            else
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_GC;
            }

            // Bit 8: D flag SF 2.01 section 8.2.2 page 51
            if ((m.SfSrc & (1 << 8)) != 0)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE;
            }
            else
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_POSITIVE;
            }

            // Bit 9: P flag SF 2.01 section 8.2.3 page 51
            if ((m.SfSrc & (1 << 9)) != 0)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR;
            }
            else
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR;
            }
            int type;
            // modulator source types: SF2.01 section 8.2.1 page 52 
            type = (m.SfSrc) >> 10;
            type &= 63; // type is a 6-bit value 
            if (type == 0)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_LINEAR;
            }
            else if (type == 1)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_CONCAVE;
            }
            else if (type == 2)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_CONVEX;
            }
            else if (type == 3)
            {
                m.flags1 |= (byte)fluid_mod_flags.FLUID_MOD_SWITCH;
            }
            else
            {
                // This shouldn't happen - unknown type! Deactivate the modulator by setting the amount to 0. 
                m.amount = 0;
            }

            // *** Amount source *** 
            m.src2 = (byte)(m.SfAmtSrc & 127); // index of source 2, seven-bit value, SF2.01 section 8.2, p.50 
            m.flags2 = 0;

            // Bit 7: CC flag SF 2.01 section 8.2.1 page 50
            if ((m.SfAmtSrc & (1 << 7)) != 0)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_CC;
            }
            else
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_GC;
            }

            // Bit 8: D flag SF 2.01 section 8.2.2 page 51
            if ((m.SfAmtSrc & (1 << 8)) != 0)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_NEGATIVE;
            }
            else
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_POSITIVE;
            }

            // Bit 9: P flag SF 2.01 section 8.2.3 page 51
            if ((m.SfAmtSrc & (1 << 9)) != 0)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR;
            }
            else
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_UNIPOLAR;
            }

            // modulator source types: SF2.01 section 8.2.1 page 52
            type = (m.SfAmtSrc) >> 10;
            type &= 63; /* type is a 6-bit value */
            if (type == 0)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_LINEAR;
            }
            else if (type == 1)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_CONCAVE;
            }
            else if (type == 2)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_CONVEX;
            }
            else if (type == 3)
            {
                m.flags2 |= (byte)fluid_mod_flags.FLUID_MOD_SWITCH;
            }
            else
            {
                // This shouldn't happen - unknown type!  Deactivate the modulator by setting the amount to 0.
                m.amount = 0;
            }

            // SF2.01 only uses the 'linear' transform (0). Deactivate the modulator by setting the amount to 0 in any other case.
            if (m.SfTrans != 0)
            {
                m.amount = 0;
            }

            // Some soundfonts come with a huge number of non-standard controllers, because they have been designed for one particular sound card.
            if (((m.flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0)
                && ((m.src1 != 0)          /* SF2.01 section 8.2.1: Constant value */
                    && (m.src1 != 2)       /* Note-on velocity */
                    && (m.src1 != 3)       /* Note-on key number */
                    && (m.src1 != 10)      /* Poly pressure */
                    && (m.src1 != 13)      /* Channel pressure */
                    && (m.src1 != 14)      /* Pitch wheel */
                    && (m.src1 != 16)))    /* Pitch wheel sensitivity */
            {
                if (SFFile.Verbose) Debug.LogFormat("Ignoring invalid controller, using non-CC source {0}.", m.src1);
            }
        }

        /* -------------------------------------------------------------------
         * preset generator loader
         * generator (per preset) loading rules:
         * Zones with no generators or modulators shall be annihilated
         * Global zone must be 1st zone, discard additional ones (instrumentless zones)
         *
         * generator (per zone) loading rules (in order of decreasing precedence):
         * KeyRange is 1st in list (if exists), else discard
         * if a VelRange exists only preceded by a KeyRange, else discard
         * if a generator follows an instrument discard it
         * if a duplicate generator exists replace previous one
         * ------------------------------------------------------------------- */
        private void LoadPGen(int size)
        {
            //Console.WriteLine(fd.BaseStream.Position);

            foreach (HiPreset p in SfData.preset)
            {
                //Debug.Log($"{p.Bank} {p.Num} {p.Name}");
                //if (p.bank==0 && p.prenum==0)
                //    Console.WriteLine(fd.BaseStream.Position);
                //Console.WriteLine(string.Format("ftell:{0} {1} {2} {3}",fd.BaseStream.Position,p.bank,p.prenum,p.name));

                /* traverse through presets */
                for (int iz = 0; iz < p.Zone.Length; iz++)
                {
                    if (p.Zone[iz].gens != null)
                    {
                        for (int ig = 0; ig < p.Zone[iz].gens.Length; ig++)
                        {
                            /* load zone's generators */
                            if ((size -= SFFile.SFGENSIZE) < 0)
                                throw new Exception("Preset generator chunk size mismatch");

                            HiGen g = new HiGen();
                            p.Zone[iz].gens[ig] = g;

                            fluid_gen_type id = (fluid_gen_type)fd.ReadUInt16();
                            g.type = id;
                            g.Amount = new HiGenAmount();
                            g.Amount.Sword = fd.ReadInt16();

                            //if (g.type == fluid_gen_type.GEN_MODLFOFREQ)
                            //    Debug.Log($"LoadPGen fluid_gen_type.GEN_MODLFOFREQ {p.Name} {g.Amount.Sword}");
                            //if (g.type == fluid_gen_type.GEN_VIBLFOFREQ)
                            //    Debug.Log($"LoadPGen fluid_gen_type.GEN_VIBLFOFREQ {p.Name} {g.Amount.Sword}");
                            //if (g.type == fluid_gen_type.GEN_ATTENUATION)
                            //    Debug.Log($"LoadPGen fluid_gen_type.GEN_ATTENUATION {p.Name} {g.Amount.Sword}");
                        }
                    }

                }
            }

            /* in case there isn't a terminal record */
            if (size == 0)
                return;

            size -= SFFile.SFGENSIZE;
            if (size != 0)
                throw new Exception("Preset generator chunk size mismatch");
            fd.BaseStream.Seek(SFFile.SFGENSIZE, SeekOrigin.Current);

        }


        /* instrument header loader */
        private void LoadIHdr(int size)
        {
            int ii, i2;
            HiInstrument inst = null, pr = null;  /* ptr to current & previous instrument */
            ushort zndx, pzndx = 0;

            if ((size % SFFile.SFIHDRSIZE) != 0 || size == 0) /* chunk size is valid? */
                throw new Exception("Instrument header has invalid size");

            size = size / SFFile.SFIHDRSIZE - 1;
            if (size == 0)
            {               /* at least one preset + term record */
                SFFile.Log(SFFile.LogLevel.Warn, "File contains no instruments");
                fd.BaseStream.Seek(SFFile.SFIHDRSIZE, SeekOrigin.Current);
                return;
            }
            SfData.inst = new HiInstrument[size];
            for (ii = 0; ii < size; ii++)
            {               /* load all instrument headers */
                inst = new HiInstrument();
                SfData.inst[ii] = inst;
                inst.Name = ReadStr();
                inst.ItemId = ii;
                zndx = fd.ReadUInt16();

                if (pr != null)
                {
                    /* not first instrument? */
                    if (zndx < pzndx) throw new Exception("Instrument header indices not monotonic");
                    i2 = zndx - pzndx;
                    if (i2 > 0) pr.Zone = new HiZone[i2];
                }
                else if (zndx > 0)  /* 1st inst, warn if ofs >0 */
                    SFFile.Log(SFFile.LogLevel.Error, "{0} instrument zones not referenced, discarding", zndx);
                pzndx = zndx;
                pr = inst;         /* update instrument ptr */
            }

            fd.BaseStream.Seek(20, SeekOrigin.Current);
            zndx = fd.ReadUInt16();

            if (zndx < pzndx) throw new Exception("Instrument header indices not monotonic");
            i2 = zndx - pzndx;
            if (i2 > 0) pr.Zone = new HiZone[i2];
        }

        /* instrument bag loader */
        private void LoadIBag(int size)
        {
            HiZone z, pz = null;
            ushort genndx, modndx, pgenndx = 0, pmodndx = 0;
            int i;

            if ((size % SFFile.SFBAGSIZE) != 0 || size == 0)  /* size is multiple of SFBAGSIZE? */
                throw new Exception("Instrument bag chunk size is invalid");

            for (int ip = 0; ip < SfData.inst.Length; ip++)
            {
                /* traverse through inst */
                for (int iz = 0; iz < SfData.inst[ip].Zone.Length; iz++)
                {
                    /* load this inst's zones */
                    if ((size -= SFFile.SFBAGSIZE) < 0)
                        throw new Exception("Instrument bag chunk size mismatch");
                    z = new HiZone();
                    SfData.inst[ip].Zone[iz] = z;
                    z.ItemId = iz;
                    z.gens = null;  /* In case of failure, */
                    z.mods = null;  /* sfont_close can clean up */
                    genndx = fd.ReadUInt16();
                    modndx = fd.ReadUInt16();
                    z.Index = -1;

                    if (pz != null)
                    {           /* if not first zone */
                        if (genndx < pgenndx) throw new Exception("Instrument generator indices not monotonic");
                        if (modndx < pmodndx) throw new Exception("Instrument modulator indices not monotonic");
                        i = genndx - pgenndx;
                        if (i > 0) pz.gens = new HiGen[i];
                        i = modndx - pmodndx;
                        if (i > 0) pz.mods = new HiMod[i];
                    }
                    pz = z;     /* update previous zone ptr */
                    pgenndx = genndx;
                    pmodndx = modndx;
                }
            }

            size -= SFFile.SFBAGSIZE;
            if (size != 0)
                throw new Exception("Instrument chunk size mismatch");

            genndx = fd.ReadUInt16();
            modndx = fd.ReadUInt16();

            if (pz == null)
            {               /* in case that all are no zoners */
                if (genndx > 0) throw new Exception("No instrument generators and terminal index not 0");
                if (modndx > 0) throw new Exception("No instrument modulators and terminal index not 0");
                return;
            }

            if (genndx < pgenndx) throw new Exception("Instrument generator indices not monotonic");
            if (modndx < pmodndx) throw new Exception("Instrument modulator indices not monotonic");
            i = genndx - pgenndx;
            if (i > 0) pz.gens = new HiGen[i];
            i = modndx - pmodndx;
            if (i > 0) pz.mods = new HiMod[i];
        }

        /* instrument modulator loader */
        private void LoadIMod(int size)
        {
            HiInstrument p;
            HiZone z;
            HiMod m;

            for (int ip = 0; ip < SfData.inst.Length; ip++)
            {
                /* traverse through inst */
                p = SfData.inst[ip];
                for (int iz = 0; iz < p.Zone.Length; iz++)
                {
                    z = p.Zone[iz];
                    if (z != null)
                    {
                        /* traverse this inst's zones */
                        if (z.mods != null)
                        {
                            for (int im = 0; im < z.mods.Length; im++)
                            {
                                if ((size -= SFFile.SFMODSIZE) < 0) throw new Exception("Instrument modulator chunk size mismatch");
                                m = new HiMod();
                                z.mods[im] = m;
                                m.SfSrc = fd.ReadUInt16();
                                m.dest = (byte)fd.ReadUInt16();
                                m.amount = fd.ReadInt16();
                                m.SfAmtSrc = fd.ReadUInt16();
                                m.SfTrans = fd.ReadUInt16();
                            }
                        }
                    }
                }
            }

            /*
               If there isn't even a terminal record
               Hmmm, the specs say there should be one, but..
             */
            if (size == 0)
                return;

            size -= SFFile.SFMODSIZE;
            if (size != 0) throw new Exception("Instrument modulator chunk size mismatch");
            fd.BaseStream.Seek(SFFile.SFMODSIZE, SeekOrigin.Current);
        }

        /* load instrument generators (see load_pgen for loading rules) */
        private void LoadIGen(int size)
        {
            HiInstrument p;
            HiZone z;
            HiGen g;

            for (int ip = 0; ip < SfData.inst.Length; ip++)
            {
                /* traverse through inst */
                p = SfData.inst[ip];
                for (int iz = 0; iz < p.Zone.Length; iz++)
                {
                    z = p.Zone[iz];
                    if (z != null)
                    {
                        /* traverse this inst's zones */
                        if (z.gens != null)
                        {
                            for (int ig = 0; ig < z.gens.Length; ig++)
                            {
                                /* load zone's generators */
                                if ((size -= SFFile.SFGENSIZE) < 0)
                                    throw new Exception("IGEN chunk size mismatch");
                                g = new HiGen();
                                z.gens[ig] = g;

                                g.type = (fluid_gen_type)fd.ReadUInt16();
                                g.Amount = new HiGenAmount();
                                //if (g.type== fluid_gen_type.GEN_ATTENUATION)
                                g.Amount.Uword = fd.ReadUInt16();
                                if (g.type == fluid_gen_type.GEN_SAMPLEID)
                                    z.Index = (short)g.Amount.Uword;

                                //if (g.type == fluid_gen_type.GEN_EXCLUSIVECLASS && g.Amount.Sword != 0)
                                //    Debug.Log($"   {g.type} {p.Name} {g.Amount.Sword}");
                                //if (g.type == fluid_gen_type.GEN_ATTENUATION)
                                //    Debug.Log($"LoadIGen fluid_gen_type.GEN_ATTENUATION {p.Name} {g.Amount.Sword}");

                            }
                        }
                    }
                }

            }

            /* for those non-terminal record cases, grr! */
            if (size == 0)
                return;

            size -= SFFile.SFGENSIZE;
            if (size != 0)
                throw new Exception("IGEN chunk size mismatch");
            fd.BaseStream.Seek(SFFile.SFGENSIZE, SeekOrigin.Current);
        }

        /* sample header loader */
        private void LoadSHdr(int size)
        {
            HiSample s;

            if ((size % SFFile.SFSHDRSIZE) != 0 || size == 0) /* size is multiple of SHDR size? */
                throw new Exception("Sample header has invalid size");

            size = size / SFFile.SFSHDRSIZE - 1;
            if (size == 0)
            {               /* at least one sample + term record? */
                SFFile.Log(SFFile.LogLevel.Error, "File contains no samples");
                fd.BaseStream.Seek(SFFile.SFSHDRSIZE, SeekOrigin.Current);
                return;
            }

            SfData.Samples = new HiSample[size];

            /* load all sample headers */
            for (int ismpl = 0; ismpl < size; ismpl++)
            {
                s = new HiSample();
                SfData.Samples[ismpl] = s;
                if (SfData.Source == SFFile.SfSource.MPTK)
                    // Name will be used to build files, so we need to replace forbidden caractere
                    s.Name = SFFile.EscapeConvert(ReadStr());
                else
                    s.Name = ReadStr();
                s.ItemId = ismpl;
                s.Start = fd.ReadUInt32();
                s.End = fd.ReadUInt32();  /* - end, loopstart and loopend */
                s.LoopStart = fd.ReadUInt32();    /* - will be checked and turned into */
                s.LoopEnd = fd.ReadUInt32();  /* - offsets in fixup_sample() */
                s.SampleRate = fd.ReadUInt32();
                s.OrigPitch = fd.ReadByte();
                s.PitchAdj = fd.ReadSByte(); // was ReadByte before 2.05
                fd.BaseStream.Seek(2, SeekOrigin.Current);
                /* skip sample link */
                s.SampleType = fd.ReadUInt16();
                //Debug.Log($"ItemId:{s.ItemId} Name:{s.Name} Start:{s.Start } End:{s.End} LoopStart:{s.LoopStart} LoopEnd:{ s.LoopEnd} SampleRate:{ s.SampleRate} OrigPitch:{s.OrigPitch} PitchAdj:{ s.PitchAdj} ");
            }

            /* skip terminal shdr */
            fd.BaseStream.Seek(SFFile.SFSHDRSIZE, SeekOrigin.Current);
        }

        /// <summary>@brief
        /// "fixup" (inst # . inst ptr) instrument references in preset list
        /// when loading a SF, 
        ///     for a preset zone, instsamp contains an index to the inst[] 
        ///     for a instru zone, instsamp contains an index to the sample[] 
        /// fixup set the corrent pointer directly to the obkect
        /// change: with c# it's not possible in a clean way to use an integer as an adresse of an object 
        /// to access to the object: 
        ///     for instru: use sf.inst[z.instsamp - 1]
        ///     for sample: sf.sample[z.instsamp - 1];
        /// </summary>
        /// <param name="sf"></param>
        private void FixupPreset(SFData sf)
        {
            foreach (HiPreset p in sf.preset)
            {
                foreach (HiZone z in p.Zone)
                {
                    bool hasInstrument = false;

                    if (z.gens != null)
                    {
                        foreach (HiGen g in z.gens)
                        {
                            if (g != null)
                            {
                                switch (g.type)
                                {
                                    case fluid_gen_type.GEN_KEYRANGE:
                                        z.KeyLo = g.Amount.Lo;
                                        z.KeyHi = g.Amount.Hi;
                                        break;
                                    case fluid_gen_type.GEN_VELRANGE:
                                        z.VelLo = g.Amount.Lo;
                                        z.VelHi = g.Amount.Hi;
                                        break;
                                    case fluid_gen_type.GEN_ATTENUATION:
                                        // MPTK 2.11.3 update related to FS 2.3
                                        // EMU8k/10k hardware applies a scale factor to initial attenuation generator values set at preset and instrument level
                                        g.Val = g.Amount.Sword * EMU_ATTENUATION_FACTOR;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        //Debug.Log($"FixupPreset GEN_ATTENUATION {p.Name} sword:{g.Amount.Sword} Val:{g.Val}");
                                        break;
                                    case fluid_gen_type.GEN_INSTRUMENT:
                                        z.Index = (short)(g.Amount.Sword);
                                        hasInstrument = true; // not a global zone
                                        if (z.Index >= 0)
                                        {
                                            if (z.Index >= sf.inst.Length || sf.inst[z.Index] == null)
                                                throw new Exception(string.Format("Invalid instrument reference - Bank:{0} Preset:{1} ZoneId:{2} Index:{3}", p.Bank, p.Num, z.ItemId, z.Index));
                                        }
                                        break;
                                    case fluid_gen_type.GEN_STARTADDROFS:
                                    case fluid_gen_type.GEN_ENDADDROFS:
                                    case fluid_gen_type.GEN_STARTLOOPADDROFS:
                                    case fluid_gen_type.GEN_ENDLOOPADDROFS:
                                    case fluid_gen_type.GEN_STARTADDRCOARSEOFS:
                                    case fluid_gen_type.GEN_ENDADDRCOARSEOFS:
                                    case fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS:
                                    case fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS:
                                    case fluid_gen_type.GEN_KEYNUM:
                                    case fluid_gen_type.GEN_VELOCITY:
                                    case fluid_gen_type.GEN_SAMPLEMODE:
                                    case fluid_gen_type.GEN_EXCLUSIVECLASS:
                                    case fluid_gen_type.GEN_OVERRIDEROOTKEY:
                                    case fluid_gen_type.GEN_SAMPLEID:
                                        SFFile.Log(SFFile.LogLevel.Warn, "Generator found at preset level not valid {0} {1} {2}", p.Name, z.ItemId, g.type);
                                        break;

                                    // TBN juste pour tester les valeurs lues. Call depuis fluid_defpreset_import_sfont -> fluid_preset_zone_import_sfont
                                    //case fluid_gen_type.GEN_MODLFOFREQ:
                                    //case fluid_gen_type.GEN_VIBLFOFREQ:
                                    //    g.Val = g.Amount.Sword;
                                    //    g.flags = fluid_gen_flags.GEN_SET;
                                    //    Debug.Log($"just for testing {(int)g.type} {g.type} {g.Amount.Sword}");
                                    //    break;

                                    default:
                                        // FIXME: some generators have an unsigned word amount value but i don't know which ones */
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;
                                }
                            }
                        }
                        if (!hasInstrument)
                        {
                            if (p.GlobalZone != null)
                                SFFile.Log(SFFile.LogLevel.Warn, "Preset already have a global zone - Preset:{0} ZoneId:{1} Index:{2}", p.Name, z.ItemId, z.Index);
                            p.GlobalZone = z;
                        }
                    }

                    if (z.mods != null)
                        foreach (HiMod m in z.mods)
                            if (m != null)
                                ProcessModulator(m);
                }
            }
        }

        /// <summary>@brief
        /// "fixup" (sample # . sample ptr) sample references in instrument list 
        /// when loading a SF, 
        ///     for a preset zone, instsamp contains an index to the inst[] 
        ///     for a instru zone, instsamp contains an index to the sample[] 
        /// fixup set the corrent pointer directly to the obkect
        /// change: with c# it's not possible in a clean way to use an integer as an adresse of an object 
        /// to access to the object: 
        ///     for instru: use sf.inst[z.instsamp - 1]
        ///     for sample: sf.sample[z.instsamp - 1];
        /// </summary>
        /// <param name="sf"></param>
        private void FixupInstrument(SFData sf)
        {
            foreach (HiInstrument i in sf.inst)
            {
                foreach (HiZone z in i.Zone)
                {
                    bool hasSample = false;
                    if (z.gens != null)
                    {
                        foreach (HiGen g in z.gens)
                        {
                            if (g != null)
                            {
                                switch (g.type)
                                {
                                    case fluid_gen_type.GEN_KEYRANGE:
                                        z.KeyLo = g.Amount.Lo;
                                        z.KeyHi = g.Amount.Hi;
                                        break;

                                    case fluid_gen_type.GEN_VELRANGE:
                                        z.VelLo = g.Amount.Lo;
                                        z.VelHi = g.Amount.Hi;
                                        break;
                                    case fluid_gen_type.GEN_ATTENUATION:
                                        // MPTK 2.11.3 update related to FS 2.3
                                        // EMU8k/10k hardware applies a scale factor to initial attenuation generator values set at preset and instrument level
                                        g.Val = g.Amount.Sword * EMU_ATTENUATION_FACTOR;
                                        //Debug.Log($"FixupInstrument GEN_ATTENUATION {i.Name} sword:{g.Amount.Sword} Val:{g.Val}");
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;
                                    case fluid_gen_type.GEN_SAMPLEID:
                                        z.Index = (short)(g.Amount.Sword);
                                        hasSample = true; // not a global zone
                                        if (z.Index < 0 || z.Index >= sf.Samples.Length || sf.Samples[z.Index] == null)
                                        {
                                            if (SFFile.Verbose) Debug.LogFormat("Invalid sample reference - Instrument:{0} ZoneId:{1} Index:{2} {3}", i.Name, z.ItemId, z.Index, g.type);
                                        }
                                        break;

                                    case fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS:
                                        if (g.Amount.Sword != 0)
                                        {
                                            if (z.Index < 0 || z.Index >= sf.Samples.Length || sf.Samples[z.Index] == null)
                                            {
                                                if (SFFile.Verbose) Debug.LogFormat("Instrument:{0} zone:{1} {2} {3} *** errror, wave not defined", i.Name, z.Index, g.type, g.Amount.Sword);
                                            }
                                            else
                                            {
#if DEBUGLOAD
                                                if (sf.Samples[z.Index].Correctedcoarseloopstart != 0 && sf.Samples[z.Index].Correctedcoarseloopstart != g.Amount.Sword) Debug.LogFormat("Conflict Instrument:{0} zone:{1} {2} {3} {4} != {5}", i.Name, z.Index, g.type, sf.Samples[z.Index].Name, g.Amount.Sword, sf.Samples[z.Index].Correctedcoarseloopstart);
#endif
                                                sf.Samples[z.Index].Correctedcoarseloopstart = g.Amount.Sword;
                                            }
                                        }
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;

                                    case fluid_gen_type.GEN_STARTLOOPADDROFS:
                                        if (g.Amount.Sword != 0)
                                        {
                                            try
                                            {
                                                if (z.Index < 0 || z.Index >= sf.Samples.Length || sf.Samples[z.Index] == null)
                                                {
                                                    if (SFFile.Verbose) Debug.LogFormat("Instrument:{0} zone:{1} {2} {3} *** errror, wave not defined", i.Name, z.Index, g.type, g.Amount.Sword);
                                                }
                                                else
                                                {
#if DEBUGLOAD
                                                    if (sf.Samples[z.Index].Correctedloopstart != 0 && sf.Samples[z.Index].Correctedloopstart != g.Amount.Sword) Debug.LogFormat("Conflict Instrument:{0} zone:{1} {2} {3} {4} != {5}", i.Name, z.Index, g.type, sf.Samples[z.Index].Name, g.Amount.Sword, sf.Samples[z.Index].Correctedloopstart);
#endif
                                                    sf.Samples[z.Index].Correctedloopstart = g.Amount.Sword;
                                                }
                                            }
                                            catch (Exception)
                                            {

                                                throw;
                                            }
                                        }
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;

                                    case fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS:
                                        if (g.Amount.Sword != 0)
                                        {
                                            if (z.Index < 0 || z.Index >= sf.Samples.Length || sf.Samples[z.Index] == null)
                                            {
                                                if (SFFile.Verbose) Debug.LogFormat("Instrument:{0} zone:{1} {2} {3} *** errror, wave not defined", i.Name, z.Index, g.type, g.Amount.Sword);
                                            }
                                            else
                                            {
#if DEBUGLOAD
                                                if (sf.Samples[z.Index].Correctedcoarseloopend != 0 && sf.Samples[z.Index].Correctedcoarseloopend != g.Amount.Sword) Debug.LogFormat("Conflict Instrument:{0} zone:{1} {2} {3} {4} != {5}", i.Name, z.Index, g.type, sf.Samples[z.Index].Name, g.Amount.Sword, sf.Samples[z.Index].Correctedcoarseloopend);
#endif
                                                sf.Samples[z.Index].Correctedcoarseloopend = g.Amount.Sword;
                                            }
                                        }
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;

                                    case fluid_gen_type.GEN_ENDLOOPADDROFS:
                                        if (g.Amount.Sword != 0)
                                        {
                                            if (z.Index < 0 || z.Index >= sf.Samples.Length || sf.Samples[z.Index] == null)
                                            {
                                                if (SFFile.Verbose) Debug.LogFormat("Instrument:{0} zone:{1} {2} {3} *** errror, wave not defined", i.Name, z.Index, g.type, g.Amount.Sword);
                                            }
                                            else
                                            {
#if DEBUGLOAD
                                                if (sf.Samples[z.Index].Correctedloopend != 0 && sf.Samples[z.Index].Correctedloopend != g.Amount.Sword) Debug.LogFormat("Conflict Instrument:{0} zone:{1} {2} {3} {4} != {5}", i.Name, z.Index, g.type, sf.Samples[z.Index].Name, g.Amount.Sword, sf.Samples[z.Index].Correctedloopend);
#endif
                                                sf.Samples[z.Index].Correctedloopend = g.Amount.Sword;
                                            }
                                        }
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;

                                    case fluid_gen_type.GEN_STARTADDROFS:
                                    case fluid_gen_type.GEN_ENDADDROFS:
                                    case fluid_gen_type.GEN_STARTADDRCOARSEOFS:
                                    case fluid_gen_type.GEN_ENDADDRCOARSEOFS:
                                        if (g.Amount.Sword != 0)
                                        {
                                            if (z.Index < 0 || z.Index >= sf.Samples.Length || sf.Samples[z.Index] == null)
                                                if (SFFile.Verbose) Debug.LogFormat("Invalid sample reference - Instrument:{0} ZoneId:{1} Index:{2} {3}", i.Name, z.ItemId, z.Index, g.type);
#if DEBUGLOAD
                                            Debug.LogFormat("Generator not processed - Instrument:{0} zone:{1} {2} {3} {4}", i.Name, z.Index, g.type, sf.Samples[z.Index].Name, g.Amount.Sword);
#endif
                                        }
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;
                                    default:
                                        // FIXME: some generators have an unsigned word amount value but i don't know which ones */
                                        g.Val = g.Amount.Sword;
                                        g.flags = fluid_gen_flags.GEN_SET;
                                        break;
                                }
                            }
                        }
                        if (!hasSample)
                        {
#if DEBUGLOAD
                            if (i.GlobalZone != null)
                                SFFile.Log(SFFile.LogLevel.Warn, "Instrument already have a global zone - Instrument:{0} ZoneId:{1} Index:{2}", i.Name, z.ItemId, z.Index);
#endif
                            i.GlobalZone = z;
                        }
                    }

                    if (z.mods != null)
                        foreach (HiMod m in z.mods)
                            if (m != null)
                                ProcessModulator(m);
                }
            }
        }

        /* convert sample end, loopstart and loopend to offsets and check if valid  */
        private void FixupSample(SFData sf)
        {
            foreach (HiSample sam in sf.Samples)
            {
                //if (sam.Name.Contains("Grand-Piano-C4")) Debug.Break();
                // if sample is not a ROM sample and end is over the sample data chunk or sam start is greater than 4 less than the end (at least 4 samples) 
                //MPTK No check over the sample, SF don't contains sample  
                if ((sam.SampleType & SFFile.SF_SAMPLETYPE_ROM) != 0)
                {
#if DEBUGLOAD
                    SFFile.Log(SFFile.LogLevel.Warn, "Sample '{0}' ROM sample ignored, disabling and will not be saved", sam.Name);
#endif
                    /* disable sample by setting all sample markers to 0 */
                    sam.Start = sam.End = sam.LoopStart = sam.LoopEnd = 0;
                    continue; //   v2.11 return;
                }
                else if (sam.LoopEnd > sam.End || sam.LoopStart >= sam.LoopEnd || sam.LoopStart <= sam.Start)
                {
                    //SFFile.Log(SFFile.LogLevel.Warn, "Sample '{0}' loop is fowled? (cluck cluck :) can pad loop by 8 samples and ensure at least 4 for loop (2*8+4) ", sam.Name);
                    // loop is fowled?? (cluck cluck :) can pad loop by 8 samples and ensure at least 4 for loop (2*8+4) 
                    if ((sam.End - sam.Start) >= 20)
                    {
                        sam.LoopStart = sam.Start + 8;
                        sam.LoopEnd = sam.End - 8;
                    }
                    else
                    {
                        /* loop is fowled, sample is tiny (can't pad 8 samples) */
                        sam.LoopStart = sam.Start + 1;
                        sam.LoopEnd = sam.End - 1;
                    }
                }

                /* convert sample end, loopstart, loopend to offsets from sam.start */
                // disabled here, uncomment for fluidsynth compatibility:  end and look position are an offset from start position
                //sam.End -= sam.Start + 1;   /* marks last sample, contrary to SF spec. (wish I hadn't done this, will change again later) */
                //sam.LoopStart -= sam.Start;
                //sam.LoopEnd -= sam.Start;
            }
        }
    }
}
