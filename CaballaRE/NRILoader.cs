using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CaballaRE
{
    class NRILoader
    {
        private List<byte[]> fileData;
        private int pixelsize = 0;
        private byte[] colortable;
        private int nritype = 0;
        private int compresstype = 0;

        public void Load(string file)
        {
            BinaryReader b = new BinaryReader(File.Open(file, FileMode.Open));

            int headerSize = 0;
            colortable = null;

            // Read NORI header
            int header = b.ReadInt32();
            if (header != 0x49524f4e) { // NORI
                // Warn that not nori file
                Console.WriteLine("Warning: Not NORI file");
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
            }
            b.ReadInt32();
            this.pixelsize = b.ReadInt32();
            this.compresstype = b.ReadInt32();
            b.ReadBytes(20); // Skip

            // Get offsets
            int numFiles = b.ReadInt32();
            b.ReadInt32(); // Skip
            headerSize += 44;

            // Read other headers (certain files only)
            while (true)
            {
                int nextHeader = b.ReadInt32();
                if (nextHeader == 0x5f4c4150) // PAL_ (color table)
                {
                    b.ReadChars(24); // Skip
                    int PALlen = b.ReadInt32();
                    int colortablesize = PALlen - 32 - 8;
                    byte[] nricolortable = b.ReadBytes(colortablesize);
                    this.colortable = this.ProcessColorTable(nricolortable);
                    b.ReadInt32();
                    b.ReadInt32();
                    headerSize += PALlen;
                }
                if (nextHeader == 0)
                {
                    break;
                }
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

            b.Close();
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
                        for (int j = 0; j < pixelbytes; j++)
                        {
                            block[pointer++] = 0;
                        }
                    }
                    // Data copy
                    byte[] data = br.ReadBytes(datalen * pixelbytes);
                    for (int i = 0; i < datalen * pixelbytes; i++)
                    {
                        block[pointer++] = data[i];
                    }

                }
                // Back padding (if any)
                /*for (int i = pointer; i < blocksize; i++)
                {
                    block[i] = 0;
                }*/

                /*if (pointer < blocksize)
                {
                    int backpadlen = b.ReadInt16();
                    b.ReadInt16();
                    for (int i = 0; i < backpadlen; i++)
                    {
                        for (int j = 0; j < pixelbytes; j++)
                        {
                            block[pointer++] = 0;
                        }
                    }
                }*/
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
            bw.Write(width);
            bw.Write(height);
            bw.Write((short)1);
            bw.Write((short)this.pixelsize);
            bw.Write((int)0);
            bw.Write(bmpdatasize);
            bw.Write((int)0);
            bw.Write((int)0);
            bw.Write((int)0);
            bw.Write((int)0);

            // BMP color table
            if (this.colortable != null)
            {
                bw.Write(this.colortable);
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
    }
}
