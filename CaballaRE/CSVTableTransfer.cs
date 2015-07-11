using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.VisualBasic.FileIO;

namespace CaballaRE
{
    // Performs transferring of data between CSV files based on matching conditions
    class CSVTableTransfer
    {
        string[] comparefields = null;
        string[] overridefields = null;

        List<string[]> srccsv = new List<string[]>();
        List<string[]> destcsv = new List<string[]>();
        List<string[]> outputcsv = new List<string[]>();

        string fieldseparator = "|"; // Special character to separate field values

        public void SetFile(string file, int type)
        {
            TextFieldParser parser = new TextFieldParser(file);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.TrimWhiteSpace = false; // Whitespaces essential

            List<string[]> result = new List<string[]>();

            while (!parser.EndOfData)
            {
                result.Add(parser.ReadFields());
            }

            switch (type)
            {
                case 0:
                    srccsv = result;
                    break;
                case 1:
                    destcsv = result;
                    break;
            }
        }

        // Fields is delimitted string
        public void SetCompareFields(string fields, int type)
        {
            string[] parts = fields.Split(new char[] { ',' });
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            switch (type)
            {
                case 0:
                    this.comparefields = parts;
                    break;
                case 1:
                    this.overridefields = parts;
                    break;
            }
        }

        Hashtable ht = new Hashtable();
        List<int> overridesourcefields = new List<int>();

        // Source file and comparison fields must be loaded
        // Generate field mappings to allow finding row in source that matches row in column
        public void GenerateMappings()
        {
            ht.Clear();
            overridesourcefields.Clear();

            // Get the set of fields to include in mapping
            HashSet<int> includedfields = new HashSet<int>();
            string[] header = srccsv[0];
            // Find the header ids that are of concern to us
            for (int j = 0; j < this.comparefields.Length; j++)
            {
                int match = Array.IndexOf(header, this.comparefields[j]);
                if (match >= 0)
                {
                    includedfields.Add(match);
                }
            }
            for (int j = 0; j < this.overridefields.Length; j++)
            {
                int match = Array.IndexOf(header, this.overridefields[j]);
                if (match >= 0)
                {
                    overridesourcefields.Add(match);
                }
            }

            for (int i = 1; i < srccsv.Count; i++)
            {
                string[] row = srccsv[i];
                // Compute row hash
                string hashvalue = "";
                for (int j = 0; j < row.Length; j++)
                {
                    if (includedfields.Contains(j))
                    {
                        hashvalue += row[j];
                        hashvalue += fieldseparator;
                    }
                }

                // Do not add rows in which all comparison values are empty
                if (hashvalue.Length > this.comparefields.Length)
                {
                    if (!ht.Contains(hashvalue))
                    {
                        ht.Add(hashvalue, i);
                    }
                    
                }
            }
        }

        // Replace destination data
        public byte[] PerformTransfer()
        {
            HashSet<int> includedfields = new HashSet<int>();
            List<int> overridingfields = new List<int>();
            for (int i = 0; i < this.destcsv.Count; i++)
            {
                if (i == 0)
                {
                    // Copy header
                    string[] header = destcsv[0];
                    this.outputcsv.Add(header);

                    // Find the header ids that are of concern to us
                    for (int j = 0; j < this.comparefields.Length; j++)
                    {
                        int match = Array.IndexOf(header, this.comparefields[j]);
                        if (match >= 0)
                        {
                            includedfields.Add(match);
                        }
                    }
                    for (int j = 0; j < this.overridefields.Length; j++)
                    {
                        int match = Array.IndexOf(header, this.overridefields[j]);
                        if (match >= 0)
                        {
                            overridingfields.Add(match);
                        }
                    }
                }
                else
                {
                    string[] row = destcsv[i];
                    // Compute row hash
                    string hashvalue = "";
                    for (int j = 0; j < row.Length; j++)
                    {
                        if (includedfields.Contains(j))
                        {
                            hashvalue += row[j];
                            hashvalue += fieldseparator;
                        }
                    }

                    int replacementrow = -1;
                    if (ht.Contains(hashvalue))
                    {
                        replacementrow = (int)ht[hashvalue];
                    }

                    if (replacementrow >= 0)
                    {
                        // Override target values
                        string[] srcrow = srccsv[replacementrow];
                        string[] newrow = new string[row.Length];
                        // Copy
                        for (int j = 0; j < row.Length; j++)
                        {
                            newrow[j] = row[j];
                        }
                        // Replace
                        for (int j = 0; j < overridefields.Length; j++)
                        {
                            newrow[overridingfields[j]] = srcrow[overridesourcefields[j]];
                        }
                        this.outputcsv.Add(newrow);
                    }
                    else
                    {
                        // Row is unchanged
                        this.outputcsv.Add(row);
                    }
                }
            }

            return ConvertToCSV(this.outputcsv);
        }

        public byte[] ConvertToCSV(List<string[]> csvdata)
        {
            var result = new StringBuilder();

            // Build rows
            foreach (string[] row in csvdata)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    string entry = row[i].ToString();
                    // Escape strings
                    entry = entry.Replace("\"", "\"\"");
                    entry = "\"" + entry + "\"";

                    result.Append(entry);
                    result.Append(i == row.Length - 1 ? "\n" : ",");
                }
            }

            return Encoding.UTF8.GetBytes(result.ToString());
        }
    }
}
