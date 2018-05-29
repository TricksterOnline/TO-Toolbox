using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace CaballaRE
{
    class NRILoader
    {
        private List<byte[]> fileData;
        private int pixelsize = 0;
        private byte[] colortable;
        private int nritype = 0;
        private int compresstype = 0;
        public string status = ""; // Additional status during loading

        public bool Load(string file)
        {
            BinaryReader b = new BinaryReader(File.Open(file, FileMode.Open));

            int headerSize = 0;
            colortable = null;

            // Read NORI header
            int header = b.ReadInt32();
            if (header != 0x49524f4e) { // NORI
                // Warn that not nori file
                Console.WriteLine("Warning: Not NORI file");
                return false;
            }
            nritype = b.ReadInt32();
            b.ReadBytes(28); // Skip
            int filelen = b.ReadInt32();
            headerSize += 40;

            // Read GAWI header
            int gawiheader = b.ReadInt32();
            if (gawiheader != 0x49574147) // GAWI
            {
                // Warn that not image header
                Console.WriteLine("Warning: Not image section");
                return false;
            }
            b.ReadInt32();
            this.pixelsize = b.ReadInt32();
            this.compresstype = b.ReadInt32();
            int hasPalette = b.ReadInt32();
            b.ReadBytes(16); // Skip

            // Get offsets
            int numFiles = b.ReadInt32();
            b.ReadInt32(); // Skip
            headerSize += 44;

            // Read PAL header
            if (hasPalette == 1)
            {
                int palHeader = b.ReadInt32();
                if (palHeader == 0x5f4c4150) // PAL_ (color table)
                {
                    b.ReadChars(24); // Skip
                    int PALlen = b.ReadInt32();
                    int colortablesize = PALlen - 32 - 8;
                    int remainder = 0;

                    byte[] nricolortable = b.ReadBytes(colortablesize);
                    if (remainder > 0)
                    {
                        b.ReadBytes(remainder);
                    }
                    this.colortable = this.ProcessColorTable(nricolortable);
                    b.ReadInt32();
                    b.ReadInt32();
                    headerSize += PALlen;
                }
            }

            if (numFiles == 0)
            {
                this.fileData = new List<byte[]>();
                this.status = "This NRI has no images";
                return true;
            }

            int indicesHeader = b.ReadInt32();
            if (indicesHeader != 0)
            {
                Console.WriteLine("Warning: No indices section");
                return false;
            }

            List<int> files = new List<int>();
            headerSize += numFiles * 4;
            if (numFiles > 0) {
                files.Add(headerSize);
            }
            int extraoffset = 0; // Only for some NRI
            for (int i = 1; i < numFiles; i++)
            {
                if (this.isBacFile()) // Map background files
                {
                    extraoffset += 28;
                }

                files.Add(b.ReadInt32() + headerSize + extraoffset);
            }
            files.Add(filelen);

            // Convert to bitmaps
            // Separate loop because binary reader position can be changed
            this.fileData = new List<byte[]>();
            for (int i = 0; i < numFiles; i++)
            {
                this.fileData.Add(this.ProcessBMP(b, files[i], files[i+1]));
            }

            this.status = "";
            if (!this.ProcessAnimations(b))
            {
                this.status = "Error loading animations";
            }

            b.Close();

            return true;
        }

        // Decompress the file
        public byte[] DecompressFile(string file)
        {
            BinaryReader b = new BinaryReader(File.Open(file, FileMode.Open));
            b.ReadBytes(8);
            int datasize = b.ReadInt32();
            byte zlibflag = b.ReadByte();
            if (zlibflag != 0x78)
            {
                return null;
            }

            b.ReadByte(); // Zlib compression level (not used)

            byte[] data = b.ReadBytes(datasize-2);
            b.Close();

            // DEFLATE
            MemoryStream ms = new MemoryStream(data);
            DeflateStream decompressor = new DeflateStream(ms, CompressionMode.Decompress);
            MemoryStream output = new MemoryStream();
            decompressor.CopyTo(output);

            return output.ToArray();
        }

        public int GetFileCount()
        {
            if (this.fileData != null)
            {
                return this.fileData.Count;
            }
            else
            {
                return 0;
            }
        }

        public MemoryStream GetFile(int id)
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(this.fileData[id], 0, this.fileData[id].Length);
            return ms;
        }

        public int GetAnimationsCount()
        {
            return this.animations.Count;
        }

        public Animation GetAnimations(int id)
        {
            return this.animations[id];
        }

        // Create BMP color table from palette segment
        private byte[] ProcessColorTable(byte[] rawtable)
        {
            int entries = rawtable.Length / 3;
            byte[] result = new byte[entries * 4];
            for (int i = 0; i < entries; i++)
            {
                result[i * 4 + 0] = rawtable[i * 3 + 2];
                result[i * 4 + 1] = rawtable[i * 3 + 1];
                result[i * 4 + 2] = rawtable[i * 3 + 0];
                result[i * 4 + 3] = 0;
            }
            return result;
        }

        private bool isBacFile()
        {
            //return this.nritype == 0x12D || this.nritype == 0x12E || this.nritype == 0x12F;
            return this.compresstype == 1;
        }

        // Process BMP block
        private byte[] processBlock(BinaryReader b, int blocksize)
        {
            if (!this.isBacFile())
            {
                // Raw
                return b.ReadBytes(blocksize);
            } else {
                // Compressed
                long rollbackpos = b.BaseStream.Position;
                short blocklen = b.ReadInt16();

                if (blocklen == 1 || blocklen == 0)
                {
                    // Read straddled into another image zone
                    b.BaseStream.Position = rollbackpos;
                    return null;
                }
                
                byte[] blockdata = b.ReadBytes(blocklen - 2);
                MemoryStream ms = new MemoryStream();
                ms.Write(blockdata, 0, blockdata.Length);
                ms.Flush();
                ms.Position = 0;
                BinaryReader br = new BinaryReader(ms);

                byte[] block = new byte[blocksize];
                int pointer = 0;
                while (pointer < blocksize)
                {
                    short frontpadlen = br.ReadInt16();
                    short datalen = br.ReadInt16();

                    if (datalen == 0)
                    {
                        break;
                    }

                    int pixelbytes = this.pixelsize / 8;

                    // Front padding
                    for (int i = 0; i < frontpadlen; i++)
                    {
                        // Fills with nulls (equivalent to black)
                        for (int j = 0; j < pixelbytes; j++)
                        {
                            block[pointer++] = 0;
                        }
                        // Attempt to fill with magenta (chroma-key)
                        /*if (pixelbytes == 1) // 8-bit
                        {
                            block[pointer++] = 0;
                        }
                        if (pixelbytes == 2) // 16-bit
                        {
                            block[pointer++] = 0x1F;
                            block[pointer++] = 0x7C;
                            
                        }
                        if (pixelbytes == 3) // 24-bit
                        {
                            block[pointer++] = 0xFF;
                            block[pointer++] = 0x00;
                            block[pointer++] = 0xFF;
                        }*/
                    }

                    // Data copy
                    byte[] data = br.ReadBytes(datalen * pixelbytes);
                    for (int i = 0; i < datalen * pixelbytes; i++)
                    {
                        block[pointer++] = data[i];
                    }
                }

                return block;
            }
        }

        // Create BMP file from image segment
        private byte[] ProcessBMP(BinaryReader b, long offset, long nextfile)
        {
            // Read the image header
            b.BaseStream.Position = offset;
            //int headersize = 8 + 8 + 12;
            int check = b.ReadInt32();
            if (check != 1)
            {
                Console.WriteLine("Warning: Mismatch of image header");
            }
            int datasize = b.ReadInt32();
            int width = b.ReadInt32();
            int height = b.ReadInt32();
            b.ReadChars(12); // Skip

            int bmpdatasize = 0;
            
            // Read the image data
            List<byte[]> imgblocks = new List<byte[]>();
            int blocksize = pixelsize * width / 8;
            int padsize = 0;
            int blocks = 0;
            if (blocksize > 0)
            {
                if (!this.isBacFile())
                {
                    blocks = (datasize) / blocksize;
                    padsize = (4 - blocksize % 4) % 4;

                    for (int i = 0; i < blocks; i++)
                    {
                        //imgblocks.Add(b.ReadBytes(blocksize));
                        imgblocks.Add(this.processBlock(b, blocksize));
                        bmpdatasize += blocksize + padsize; // BMP data array computed size (+2 due to padding)
                    }
                } else {
                    padsize = (4 - blocksize % 4) % 4;
                    while (b.BaseStream.Position < b.BaseStream.Length && b.BaseStream.Position < nextfile)
                    {
                        byte[] d = this.processBlock(b, blocksize);
                        if (d != null)
                        {
                            imgblocks.Add(d);
                            bmpdatasize += blocksize + padsize; // BMP data array computed size (+2 due to padding)
                            blocks++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            int bmpcolortablesize = 0;
            if (this.colortable != null)
            {
                bmpcolortablesize = this.colortable.Length;
            }
            
            int bmpheadersize = 2 + 4 + 4 + 4 + 4 + 4 + 4 + 2 + 2 + 4 + 4 + 16;
            int bmpfilesize = bmpheadersize + bmpdatasize + bmpcolortablesize;


            // Generate the file
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            // BMP header
            bw.Write('B');
            bw.Write('M');
            bw.Write(bmpfilesize);
            bw.Write((int)0);
            bw.Write(bmpheadersize); // Location of pixel data
            int dibheadersize = bmpheadersize - 14;
            bw.Write(dibheadersize); // DIB header size
            bw.Write(width); // Image width
            bw.Write(height); // Image height
            bw.Write((short)1); // Planes
            bw.Write((short)this.pixelsize); // Bits per pixel
            bw.Write((int)0); // Compression (none)
            bw.Write(bmpdatasize);
            bw.Write((int)0);
            bw.Write((int)0);
            bw.Write((int)0);
            bw.Write((int)0);

            // BMP color table
            // For BMPv3, BMP with pixel size 16 will not have a color palette
            if (this.colortable != null && this.pixelsize < 16)
            {
                bw.Write(this.colortable);
                // Pad color table
                int colorTablePadSize = 1;
                while (colorTablePadSize < bmpcolortablesize)
                {
                    colorTablePadSize *= 2;
                    if (colorTablePadSize >= bmpcolortablesize)
                    {
                        break;
                    }
                }
                int padAmount = colorTablePadSize - bmpcolortablesize;
                if (padAmount > 0)
                {
                    byte[] padBuffer = new byte[padAmount];
                    bw.Write(padBuffer);
                }
            }

            // BMP image data (write in reverse order)
            for (int i = 0; i < blocks; i++)
            {
                bw.Write(imgblocks[blocks - i - 1]);
                
                // Add padding
                if (padsize % 2 == 1)
                {
                    bw.Write((char)0);
                }
                if (padsize >= 2)
                {
                    bw.Write((short)0);
                }
            }

            bw.Flush();
            byte[] result = ms.ToArray();
            bw.Close();
            return result;
        }

        // Process animations
        public class Animation
        {
            public string name;
            public List<Frame> frames;
            public long offset; // This is for debugging purposes (not part of file data)
        }

        public struct Frame
        {
            public int delay;
            public List<FramePlane> planes;
            public List<FrameEffect> effects;
        }

        public struct FramePlane
        {
            public int bitmapid;
            public int x;
            public int y;
            public int transparency;
            public int reverseflag;
            public int param5;
            public int param6;
        }

        public struct FrameEffect // Temporary name for unknown data block
        {
            public int param1;
            public int x;
            public int y;
            public int param4;
            public int param5;
            public int param6;
        }

        List<Animation> animations = new List<Animation>();
        public bool ProcessAnimations(BinaryReader b)
        {
            try
            {
                animations.Clear();

                b.BaseStream.Position = 0;
                // Get location of animation data
                b.ReadBytes(28);
                int animationCount = b.ReadInt32();
                int animationMemAllocSize = b.ReadInt32();
                int filesize = b.ReadInt32();

                // Seek to animation data
                int noriheadersize = 40;
                int animationLocation = filesize - (animationMemAllocSize - noriheadersize);
                b.BaseStream.Position = animationLocation;

                // Read animation offsets
                List<int> offsets = new List<int>();
                if (animationCount >= 1) // Address list only present if more than 1 animation
                {
                    int offsetsSize = animationCount * 4;
                    for (int i = 0; i < animationCount; i++)
                    {
                        offsets.Add(b.ReadInt32() + animationLocation + offsetsSize);
                    }
                }
                else
                {
                    offsets.Add(animationLocation);
                }

                for (int i = 0; i < animationCount; i++)
                {
                    b.BaseStream.Position = offsets[i];
                    Animation anim = new Animation();
                    anim.frames = new List<Frame>();
                    anim.offset = b.BaseStream.Position;

                    byte[] name_raw = b.ReadBytes(32);
                    string name = Encoding.GetEncoding(51949).GetString(name_raw); // EUC-KR
                    // Convert to null-terminated version
                    string[] parts = name.Split(new char[] { '\0' });
                    name = parts[0];
                    
                    int frameCount = b.ReadInt32();
                    anim.name = name;

                    // Get frame offsets
                    int refPosition = (int)b.BaseStream.Position;
                    List<int> frameOffsets = new List<int>();
                    int frameOffsetsSize = frameCount * 4;
                    for (int j = 0; j < frameCount; j++)
                    {
                        frameOffsets.Add(b.ReadInt32() + refPosition + frameOffsetsSize);
                    }

                    // Process frame data
                    for (int j = 0; j < frameCount; j++)
                    {
                        b.BaseStream.Position = frameOffsets[j];
                        Frame frame = new Frame();
                        frame.delay = b.ReadInt32();
                        frame.planes = new List<FramePlane>();
                        frame.effects = new List<FrameEffect>();

                        // Read plane data
                        int planeCount = b.ReadInt32();
                        for (int c = 0; c < planeCount; c++)
                        {
                            FramePlane plane = new FramePlane();
                            plane.bitmapid = b.ReadInt32();
                            plane.x = b.ReadInt32();
                            plane.y = b.ReadInt32();
                            plane.transparency = b.ReadInt32();
                            plane.reverseflag = b.ReadInt32();
                            plane.param5 = b.ReadInt32();
                            plane.param6 = b.ReadInt32();
                            frame.planes.Add(plane);
                        }

                        // Read frame additional data
                        switch (this.nritype)
                        {
                            case 0x12C:
                                b.ReadBytes(0x90); // CD block
                                b.ReadBytes(0x50); // Null block
                                break;
                            case 0x12D:
                                b.ReadInt32(); // int32 value
                                b.ReadBytes(0x90); // CD block
                                b.ReadBytes(0x50); // Null block
                                break;
                            case 0x12E:
                                b.ReadInt32(); // int32 value
                                b.ReadBytes(0x60); // CD block
                                for (int z = 0; z < 6; z++)
                                {
                                    FrameEffect effect = new FrameEffect();
                                    b.ReadInt32(); // 0xCDCDCDCD
                                    effect.param1 = b.ReadInt32();
                                    effect.x = b.ReadInt32();
                                    effect.y = b.ReadInt32();
                                    effect.param4 = b.ReadInt32();
                                    effect.param5 = b.ReadInt32();
                                    effect.param6 = b.ReadInt32();
                                    frame.effects.Add(effect);
                                }
                                b.ReadBytes(0x50); // Null block
                                break;
                            case 0x12F:
                                b.ReadInt32(); // int32 value
                                b.ReadBytes(0x60); // CD block
                                for (int z = 0; z < 6; z++)
                                {
                                    FrameEffect effect = new FrameEffect();
                                    b.ReadInt32(); // 0xCDCDCDCD
                                    effect.param1 = b.ReadInt32();
                                    effect.x = b.ReadInt32();
                                    effect.y = b.ReadInt32();
                                    effect.param4 = b.ReadInt32();
                                    effect.param5 = b.ReadInt32();
                                    effect.param6 = b.ReadInt32();
                                    frame.effects.Add(effect);
                                }
                                b.ReadBytes(0x54); // Null block
                                break;
                        }

                        anim.frames.Add(frame);
                    }

                    animations.Add(anim);
                }
                return true;
            }
            catch
            {
            }
            return false;
        } 
    }
}
