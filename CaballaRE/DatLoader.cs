using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Data;
using Microsoft.VisualBasic.FileIO; // For CSV reader

namespace CaballaRE
{
    class DatLoader
    {
        // libcmgds: "#dyddydwnrrpTwl~"
        private uint[] key_libcmgds = new uint[] { 0x23647964, 0x64796477, 0x6e727270, 0x54776c7e };
        // libconfig: "#ShowMeTheMoney#"
        private uint[] key_libconfig = new uint[] { 0x2353686f, 0x774d6554, 0x68654d6f, 0x6e657923 };

        private byte[] currentfile = null;
        float progress = 0;
        string statusmessage = "";

        public Action<int> ProgressCallback = null;

        // Crypto function delegates
        XTEAFunction encFunc = null;
        XTEAFunction decFunc = null;

        public void Load(string src)
        {
            // Init reusable delegates
            encFunc = new XTEAFunction(this.Encrypt);
            decFunc = new XTEAFunction(this.Decrypt);

            this.progress = 0;
            this.currentfile = null;
            BinaryReader b = new BinaryReader(File.Open(src, FileMode.Open));
            
            // Setup key
            uint[] key;
            int header = b.PeekChar();
            if (header == 1)
            {
                b.ReadByte(); // Handle libconfig files
                key = key_libconfig;
            } else {
                key = key_libcmgds;
            }

            // Decrypt the file
            /*MemoryStream ms = new MemoryStream();
            long lastprogress = 0;
            long currentprogress = 0;
            long divisions = b.BaseStream.Length / 100;

            float filelen = (float)b.BaseStream.Length;
            while (b.BaseStream.Position < b.BaseStream.Length)
            {
                byte[] temp = this.DecryptBlock(b, key);
                ms.Write(temp, 0, temp.Length);

                currentprogress = b.BaseStream.Position / divisions;

                // Report progress
                if (currentprogress > lastprogress)
                {
                    lastprogress = currentprogress;
                    if (this.ProgressCallback != null)
                    {
                        this.progress = (float)b.BaseStream.Position / filelen;
                        this.ProgressCallback(this.GetProgress());
                    }
                }
            }

            this.progress = 1.0f;
            if (this.ProgressCallback != null)
            {
                this.ProgressCallback(100);
            }
            currentfile = ms.ToArray();
            */

            currentfile = this.DecryptFile(b, key);

            b.Close();
        }

        public int GetProgress()
        {
            return (int)(this.progress * 100.0);
        }

        public string GetStatus()
        {
            return this.statusmessage;
        }

        /** Encryption/Decryption functions **/

        // Parallelized XTEA file decryption
        byte[] DecryptFile(BinaryReader b, uint[] key)
        {
            MemoryStream ms = new MemoryStream();
            long totalblocks = b.BaseStream.Length / 8;
            // Separate workload into 100 sequential steps (to allow progress measurement)
            int intervals = 100;
            long intervalworkload = totalblocks / intervals;
            for (int i = 0; i < intervals-1; i++)
            {
                if (intervalworkload >= 1)
                {
                    int blocksize = (int)intervalworkload * 8;
                    byte[] result = this.ParallelDecrypt(b.ReadBytes(blocksize), key);
                    ms.Write(result, 0, result.Length);
                }
                if (this.ProgressCallback != null)
                {
                    this.ProgressCallback(i);
                }
            }
            // Last interval
            long remainderLen = b.BaseStream.Length - b.BaseStream.Position;
            byte[] result2 = this.ParallelDecrypt(b.ReadBytes((int)remainderLen), key);
            ms.Write(result2, 0, result2.Length);
            if (this.ProgressCallback != null)
            {
                this.ProgressCallback(100);
            }

            return ms.ToArray();
        }

        // Parallelized XTEA block decryption
        // Given this block (size multiple of 8), decrypt
        byte[] ParallelDecrypt(byte[] block, uint[] key)
        {
            byte[] resultarr = new byte[block.Length];
            int blocks = block.Length / 8;
            Parallel.For(0, blocks, i =>
            {
                int blockoffset = i * 8;

                uint v0 = (uint)(block[blockoffset + 0] << 24 
                    | block[blockoffset + 1] << 16
                    | block[blockoffset + 2] << 8
                    | block[blockoffset + 3]);
                uint v1 = (uint)(block[blockoffset + 4] << 24
                    | block[blockoffset + 5] << 16
                    | block[blockoffset + 6] << 8
                    | block[blockoffset + 7]);

                uint[] result = this.Decrypt(v0, v1, key);

                // Output is in big-endian
                
                resultarr[blockoffset + 0] = (byte)((result[0] >> 24));
                resultarr[blockoffset + 1] = (byte)((result[0] >> 16));
                resultarr[blockoffset + 2] = (byte)((result[0] >> 8));
                resultarr[blockoffset + 3] = (byte)((result[0]));
                resultarr[blockoffset + 4] = (byte)((result[1] >> 24));
                resultarr[blockoffset + 5] = (byte)((result[1] >> 16));
                resultarr[blockoffset + 6] = (byte)((result[1] >> 8));
                resultarr[blockoffset + 7] = (byte)((result[1]));
            });

            return resultarr;
        }

        // Return current file data (cached)
        public byte[] GetFile()
        {
            return this.currentfile;
        }

        public string GetString(bool actual = false, bool reconvertnewline = false)
        {
            if (this.currentfile == null)
            {
                return "";
            }

            if (actual)
            {
                // Strip null bytes
                MemoryStream ms = new MemoryStream();

                int divisions = this.currentfile.Length / 100;
                for (int i = 0; i < this.currentfile.Length; i++)
                {
                    if (this.currentfile[i] != 0)
                    {
                        if (reconvertnewline && this.currentfile[i] == 0x0A)
                        {
                            ms.WriteByte(0x0d); // Windows linebreaks
                        }
                        ms.WriteByte(this.currentfile[i]);
                    }

                    if (this.ProgressCallback != null && i % divisions == 0)
                    {
                        this.progress = (float)i / (float)this.currentfile.Length;
                        this.ProgressCallback(this.GetProgress());
                    }
                }

                byte[] result = ms.ToArray();
                return Encoding.UTF8.GetString(result, 0, result.Length);
            }
            else
            {
                return "Decryption done, click [Display] to view the XML file";
            }
        }

        // 4 bytes to uint big-endian representation
        uint ToUInt(byte[] input)
        {
            return (uint)(input[0] << 24 | input[1] << 16 | input[2] << 8 | input[3]);
        }

        // uint to 4 bytes big-endian representation
        byte[] ToBytes(uint input)
        {
            byte[] r1 = new byte[4];
            r1[0] = (byte)((input >> 24) & 0xFF);
            r1[1] = (byte)((input >> 16) & 0xFF);
            r1[2] = (byte)((input >> 8) & 0xFF);
            r1[3] = (byte)((input) & 0xFF);
            return r1;
        }

        delegate uint[] XTEAFunction(uint v0, uint v1, uint[] key);

        byte[] ProcessBlock(BinaryReader b, uint[] key, XTEAFunction xteaFunc)
        {
            // Data is in big-endian
            uint v0 = ToUInt(b.ReadBytes(4));
            uint v1 = ToUInt(b.ReadBytes(4));

            uint[] result = xteaFunc(v0, v1, key);

            // Output is in big-endian
            byte[] resultarr = new byte[8];
            resultarr[0] = (byte)((result[0] >> 24));
            resultarr[1] = (byte)((result[0] >> 16));
            resultarr[2] = (byte)((result[0] >> 8));
            resultarr[3] = (byte)((result[0]));
            resultarr[4] = (byte)((result[1] >> 24));
            resultarr[5] = (byte)((result[1] >> 16));
            resultarr[6] = (byte)((result[1] >> 8));
            resultarr[7] = (byte)((result[1]));

            return resultarr;
        }

        // Reads 8-byte block from file and encrypt it
        byte[] EncryptBlock(BinaryReader b, uint[] key)
        {
            //return ProcessBlock(b, key, new XTEAFunction(this.Encrypt));
            return ProcessBlock(b, key, this.encFunc);
        }

        // Reads 8-byte block from file and decrypt it
        byte[] DecryptBlock(BinaryReader b, uint[] key)
        {
            return ProcessBlock(b, key, this.decFunc);
            //return ProcessBlock(b, key, new XTEAFunction(this.Decrypt));
        }

        // Regular XTEA
        uint[] Encrypt(uint y, uint z, uint[] key)
        {
            uint delta = 0x9e3779b9;
            uint n = 32;
            uint sum = 0;

            while (n-- > 0)
            {
                y += (z << 4 ^ z >> 5) + z ^ sum + key[sum & 3];
                sum += delta;
                z += (y << 4 ^ y >> 5) + y ^ sum + key[sum >> 11 & 3];
            }

            return new uint[] { y, z };
        }

        // Regular XTEA
        uint[] Decrypt(uint y, uint z, uint[] key)
        {
            uint delta = 0x9e3779b9;
            uint n = 32;
            uint sum = delta << 5; // Equal to 0xc6ef3720

            while (n-- > 0)
            {
                z -= (y << 4 ^ y >> 5) + y ^ sum + key[sum >> 11 & 3];
                sum -= delta;
                y -= (z << 4 ^ z >> 5) + z ^ sum + key[sum & 3];
            }

            return new uint[] { y, z };
        }

        /** LibConfig viewer **/

        struct ColumnInfo
        {
            public string Name;
            public string IsKey;
            public string DataType;
            public int Index; // For ordering
        }

        class TableInfo
        {
            // Table metadata
            public string Name = "";
            public string RowCount = "";
            public string TableInfoID = "";
            public string FieldCnt = "";

            int colcount; // For building header
            List<string> header; // Ordered-list of column names
            Dictionary<string, ColumnInfo> headerMap; // Maps columns to type
            List<string[]> rows;
            string[] row; // For building current row

            public TableInfo()
            {
                header = new List<string>();
                rows = new List<string[]>();
                headerMap = new Dictionary<string, ColumnInfo>();
                colcount = 0;
            }

            public ColumnInfo GetColumn(int id)
            {
                return this.headerMap[this.header[id]];
            }

            public string GetName()
            {
                return this.Name;
            }

            // Add column field
            public void AddField(string name, string iskey, string datatype)
            {
                ColumnInfo ci = new ColumnInfo();
                ci.Name = name;
                ci.IsKey = iskey;
                ci.DataType = datatype;
                ci.Index = colcount++;

                header.Add(name);
                headerMap.Add(name, ci);
            }

            // Insert a value into current row
            public void SetValue(string name, string obj)
            {
                int targetfield = headerMap[name].Index;
                row[targetfield] = obj;
            }

            // Start row
            public void BeginRow()
            {
                row = new string[colcount];
            }

            // End row
            public void EndRow()
            {
                rows.Add(row);
            }

            private DataTable datatable = null;

            // Gets field value from specified row
            public string GetValue(int row, int col)
            {
                return this.rows[row][col];
            }

            // Explicitly set table data
            public void SetTableData(List<string[]> rows)
            {
                this.rows = rows;

                // Update datatable
                this.datatable = null;
                this.GetTable();
            }

            // Transfer data table contents back to table data
            public void UpdateTable()
            {
                if (datatable != null)
                {
                    this.rows.Clear();
                    // Rebuild rows (assume columns preserved)
                    for (int i = 0; i < datatable.Rows.Count; i++)
                    {
                        DataRow dr = datatable.Rows[i];
                        row = new string[this.colcount];
                        for (int j = 0; j < this.colcount; j++)
                        {
                            row[j] = (string)dr[j];
                        }
                        this.rows.Add(row);
                    }

                    // Update row count
                    this.RowCount = this.rows.Count.ToString();
                }
            }
            
            // Gets data table representing this table
            public DataTable GetTable()
            {
                if (datatable == null)
                {
                    // Create data table
                    datatable = new DataTable();
                    // Setup columns
                    for (int i = 0; i < this.header.Count; i++)
                    {
                        datatable.Columns.Add(this.header[i]);
                    }
                    // Setup rows
                    for (int i = 0; i < this.rows.Count; i++)
                    {
                        string[] currentrow = this.rows[i];
                        DataRow row = datatable.NewRow();
                        // Populate row
                        for (int j = 0; j < currentrow.Length; j++)
                        {
                            string fieldname = this.header[j];
                            string fieldvalue = currentrow[j];
                            row[fieldname] = fieldvalue;
                        }
                        datatable.Rows.Add(row);
                    }
                }
                return datatable;
            }
        }

        
        List<TableInfo> tables = new List<TableInfo>();
        HashSet<string> infotables = new HashSet<string>(); // for reference
        // Info from XML
        string tablecount = "";
        string tableinfocount = "";

        public List<string> GetTableList()
        {
            List<string> result = new List<string>();
            for (int i = 0; i < tables.Count; i++)
            {
                result.Add(tables[i].GetName());
            }
            return result;
        }

        public int GetTableCount()
        {
            return this.tables.Count;
        }

        public DataTable GetTable(int id)
        {
            return tables[id].GetTable();
        }

        public void LoadLibConfig(string file)
        {
            tables.Clear();
            infotables.Clear();
            XmlReader xmlr = XmlReader.Create(file);

            TableInfo currentTable = null;

            string currentTag = "";
            while (xmlr.Read())
            {
                if (xmlr.NodeType == XmlNodeType.Element)
                {
                    if (xmlr.IsStartElement())
                    {
                        currentTag = xmlr.Name;

                        switch (currentTag.ToLower())
                        {
                            case "ini":
                                tablecount = xmlr["TableCount"];
                                tableinfocount = xmlr["TableInfoCount"];
                                break;
                            case "table":
                                // Add table to table list
                                currentTable = new TableInfo();
                                currentTable.Name = xmlr["name"];
                                currentTable.RowCount = xmlr["RowCount"];
                                currentTable.TableInfoID = xmlr["TableInfoID"];
                                currentTable.FieldCnt = xmlr["FieldCnt"];

                                infotables.Add(xmlr["TableInfoID"]);
                                break;
                            case "fieldinfo":
                                currentTable.AddField(xmlr["Name"], xmlr["IsKey"], xmlr["DataType"]);
                                break;
                            case "row":
                                currentTable.BeginRow();
                                break;
                            default:
                                break;

                        }
                    }
                }
                if (xmlr.NodeType == XmlNodeType.EndElement) 
                {
                    switch (xmlr.Name.ToLower())
                    {
                        case "ini":
                            break;
                        case "table":
                            tables.Add(currentTable);
                            break;
                        case "fieldinfo":
                            break;
                        case "row":
                            currentTable.EndRow();
                            break;
                        default:
                            // Can only get value after tag concluded
                            //currentTable.SetValue(lasttag, xmlr.Value);
                            break;
                    }
                    
                }
                if (xmlr.NodeType == XmlNodeType.Text || xmlr.NodeType == XmlNodeType.CDATA)
                {
                    currentTable.SetValue(currentTag, xmlr.Value);
                }
            }
        }

        /** LibConfig export **/
        // Export to XML file
        public byte[] ExportXML()
        {
            this.statusmessage = "";
            libconfigidxtables.Clear();
            XmlWriterSettings xmls = new XmlWriterSettings();
            xmls.Encoding = new UTF8Encoding(false); // Don't include BOM
            xmls.NewLineChars = "\n"; // Force unix style
            xmls.Indent = true;
            xmls.IndentChars = "\t";

            MemoryStream ms = new MemoryStream();
            XmlWriter xmlw = XmlWriter.Create(ms, xmls);
            xmlw.WriteStartDocument();
            
            // INI
            xmlw.WriteStartElement("INI");
            xmlw.WriteAttributeString("TableCount", tablecount); // Can determine
            xmlw.WriteAttributeString("TableInfoCount", tableinfocount);

            for (int i = 0; i < tables.Count; i++)
            {
                // TABLE
                TableInfo ti = tables[i];
                xmlw.WriteStartElement("TABLE");
                xmlw.WriteAttributeString("name", ti.Name);
                xmlw.WriteAttributeString("RowCount", ti.RowCount); // Can determine
                xmlw.WriteAttributeString("TableInfoID", ti.TableInfoID);
                xmlw.WriteAttributeString("FieldCnt", ti.FieldCnt); // Can determine
                
                libconfigidxtables.Add(ti.Name); // For idx

                // FIELDINFO
                int fieldCount = int.Parse(ti.FieldCnt);
                ColumnInfo[] cilist = new ColumnInfo[fieldCount];
                for (int j = 0; j < fieldCount; j++)
                {
                    ColumnInfo ci = ti.GetColumn(j);
                    cilist[j] = ci;
                    xmlw.WriteStartElement("FIELDINFO");
                    xmlw.WriteAttributeString("Name", ci.Name);
                    xmlw.WriteAttributeString("IsKey", ci.IsKey);
                    xmlw.WriteAttributeString("DataType", ci.DataType);
                    xmlw.WriteFullEndElement();
                }

                // ROW
                int rowCount = int.Parse(ti.RowCount);
                for (int j = 0; j < rowCount; j++)
                {
                    xmlw.WriteStartElement("ROW");
                    // Follow format provided in cilist
                    for (int c = 0; c < cilist.Length; c++)
                    {
                        string value = "";
                        try
                        {
                            string tempvalue = ti.GetValue(j, c);
                            value = tempvalue;
                        }
                        catch (Exception ex)
                        {
                            this.statusmessage = "Error: Attempting to access non-existant entry (" + j + "," + c + ")";
                            return null;
                        }
                        
                        if (cilist[c].DataType.ToLower() == "string")
                        {
                            if (value == "")
                            {
                                // CDATA cannot be empty string or client will crash
                                value = " ";
                            }
                            xmlw.WriteStartElement(cilist[c].Name);
                            xmlw.WriteCData(value);
                            xmlw.WriteEndElement();
                        }
                        else
                        {
                            xmlw.WriteElementString(cilist[c].Name, value);
                        }
                    }
                    xmlw.WriteEndElement();
                }

                xmlw.WriteEndElement();
            }

            xmlw.WriteEndElement();
            xmlw.WriteEndDocument();
            xmlw.Flush();
            return ms.ToArray();
        }

        List<string> libconfigidxtables = new List<string>();
        List<uint> libconfigidxpointers = new List<uint>();

        // Export to padded XML file (each line on a block divisible by 8)
        public byte[] ExportDAT(byte[] xmldata, bool encrypt = false)
        {
            libconfigidxpointers.Clear();

            MemoryStream msout = new MemoryStream();
            BinaryWriter binw = new BinaryWriter(msout);

            MemoryStream ms = new MemoryStream();
            ms.Write(xmldata, 0, xmldata.Length);
            ms.Flush();
            ms.Position = 0;
            StreamReader txtr = new StreamReader(ms);

            uint pointer = 0; // Current file output pointer
            while (true)
            {
                string line = txtr.ReadLine();
                if (line == null)
                {
                    break;
                }

                // Check if it is the beginning of a table
                if (line.IndexOf("<TABLE ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Tables are ordered according to appearence
                    libconfigidxpointers.Add(pointer);
                }

                // Re-Add newline
                byte[] strbuffer = Encoding.UTF8.GetBytes(line + "\n");
                int strlen = strbuffer.Length;

                int blocks = (strlen) / 8;
                if (strlen % 8 != 0)
                {
                    blocks++;
                }
                if (blocks > 0)
                {
                    byte[] buffer = new byte[blocks * 8];
                    
                    // Copy
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        if (i < strbuffer.Length)
                        {
                            buffer[i] = strbuffer[i];
                        }
                        else
                        {
                            buffer[i] = 0; // Fill with null
                        }
                    }
                    binw.Write(buffer, 0, buffer.Length);
                    pointer += (uint)buffer.Length;
                }
            }
            binw.Flush();

            // Encryption (will also add 1-byte encryption flag)
            if (encrypt)
            {
                return this.EncryptFile(msout.ToArray());
            }
            else
            {
                return msout.ToArray();
            }
        }

        // Encrypt file (for export)
        public byte[] EncryptFile(byte[] data)
        {
            MemoryStream ms = new MemoryStream(); // Input
            ms.Write(data, 0, data.Length);
            ms.Position = 0;
            BinaryReader b = new BinaryReader(ms);

            MemoryStream ms2 = new MemoryStream(); // Output
            BinaryWriter bw = new BinaryWriter(ms2);
            bw.Write((byte)1); // Write encryption flag

            // Encrypt file using XTEA
            while (b.BaseStream.Position < b.BaseStream.Length)
            {
                byte[] temp = this.EncryptBlock(b, this.key_libconfig);
                bw.Write(temp);
            }
            bw.Flush();
            return ms2.ToArray();
        }

        // Given table name, find the the first occuring match
        public int GetTableID(string name)
        {
            for (int i = 0; i < this.tables.Count; i++)
            {
                if (this.tables[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        public void UpdateTable(int id)
        {
            if (this.tables != null && id >= 0 && id < this.tables.Count)
            {
                this.tables[id].UpdateTable();
            }
        }

        // Generates indices table. Should call this after exporting dat.
        public byte[] ExportIdx()
        {
            if (libconfigidxtables.Count == 0)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();
            BinaryWriter binw = new BinaryWriter(ms);
            for (int i = 0; i < libconfigidxtables.Count; i++)
            {
                binw.Write(Encoding.UTF8.GetBytes(libconfigidxtables[i]));
                binw.Write((byte)0); // String terminator
                binw.Write(libconfigidxpointers[i] + 1); // idx pointers appear to include 1 byte offset
            }
            binw.Flush();
            return ms.ToArray();
        }

        // Exports the given table to CSV format
        public byte[] ExportCSV(int tableid)
        {
            if (tableid > this.tables.Count || tableid < 0)
            {
                return null;
            }

            DataTable table = this.tables[tableid].GetTable();

            var result = new StringBuilder();

            // Build headers
            for (int i = 0; i < table.Columns.Count; i++)
            {
                string entry = table.Columns[i].ColumnName;
                // Escape strings
                entry = entry.Replace("\"", "\"\"");
                entry = "\"" + entry + "\"";

                result.Append(entry);
                result.Append(i == table.Columns.Count - 1 ? "\n" : ",");
            }

            // Build rows
            foreach (DataRow row in table.Rows)
            {
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    string entry = row[i].ToString();
                    // Escape strings
                    entry = entry.Replace("\"", "\"\"");
                    entry = "\"" + entry + "\"";

                    result.Append(entry);
                    result.Append(i == table.Columns.Count - 1 ? "\n" : ",");
                }
            }

            return Encoding.UTF8.GetBytes(result.ToString());
        }

        /** Import table **/

        // Prepares loading of source CSV file, and checks for match
        public int ImportTable(int tableid, string file)
        {
            if (tableid < 0)
            {
                return -1;
            }

            TextFieldParser parser = new TextFieldParser(file);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.TrimWhiteSpace = false; // Whitespaces essential
            string[] header = parser.ReadFields();

            // Do header check
            TableInfo ti = this.tables[tableid];
            int expectedColumns = int.Parse(ti.FieldCnt);
            if (header.Length != expectedColumns)
            {
                return 1;
            }
            for (int i = 0; i < expectedColumns; i++)
            {
                if (ti.GetColumn(i).Name != header[i])
                {
                    return 1;
                }
            }

            List<string[]> rows = new List<string[]>();
            while (!parser.EndOfData)
            {
                rows.Add(parser.ReadFields());
            }
            ti.SetTableData(rows);

            parser.Close();
            return 0;
        }
    }
}
