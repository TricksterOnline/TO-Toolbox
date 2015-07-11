using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace CaballaRE
{
    public partial class CSVHelper : Form
    {
        public CSVHelper()
        {
            InitializeComponent();
        }

        CSVTableTransfer csvh = new CSVTableTransfer();

        private void button3_Click(object sender, EventArgs e)
        {
            csvh.SetCompareFields(this.textBox1.Text, 0);
            csvh.SetCompareFields(this.textBox2.Text, 1);
            
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV files (*.csv)|*.csv|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                // Insert UTF-8 BOM so that other languages can be read by CSV editors
                byte[] utf8bom = new byte[3] { 0xEF, 0xBB, 0xBF };
                bw.Write(utf8bom, 0, 3);
                csvh.GenerateMappings();
                byte[] data = csvh.PerformTransfer();
                bw.Write(data, 0, data.Length);
                bw.Flush();
                bw.Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV files (*.csv)|*.csv|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.label1.Text = "Source: " + ofd.FileName;
                csvh.SetFile(ofd.FileName, 0);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV files (*.csv)|*.csv|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.label2.Text = "Target: " + ofd.FileName;
                csvh.SetFile(ofd.FileName, 1);
            }
        }
    }
}
