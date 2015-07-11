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
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private NRILoader nril = new NRILoader();
        private DatLoader dl = new DatLoader();

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "NRI files (*.nri;*.bac)|*.nri;*.bac|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                nril.Load(ofd.FileName);
                // Populate list
                this.listBox1.Items.Clear();
                int fileCount = nril.GetFileCount();
                for (int i = 0; i < fileCount; i++)
                {
                    this.listBox1.Items.Add("File" + (i+1));
                }
            }
        }

        private Stream lastimagestream = null;
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (nril.GetFileCount() > 0)
            {
                int fileid = this.listBox1.SelectedIndex;
                if (fileid < nril.GetFileCount())
                {
                    if (lastimagestream != null)
                    {
                        lastimagestream.Close();
                    }
                    try
                    {
                        Stream bmpstream = nril.GetFile(fileid);
                        this.pictureBox1.Image = Image.FromStream(bmpstream);
                        lastimagestream = bmpstream;
                    }
                    catch
                    {

                    }
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (nril.GetFileCount() > 0)
            {
                int fileid = this.listBox1.SelectedIndex;
                if (fileid < nril.GetFileCount() && fileid >= 0)
                {
                    if (lastimagestream != null)
                    {
                        lastimagestream.Close();
                    }

                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = "BMP file|*.bmp|All files|*.*";
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        MemoryStream bmpstream = nril.GetFile(fileid);
                        BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                        bmpstream.Flush();
                        bw.Write(bmpstream.ToArray());
                        bw.Flush();
                        bw.Close();
                        bmpstream.Close();
                    }
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "DAT files (*.dat)|*.dat|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dl.Load(ofd.FileName);
                this.textBox1.Text = dl.GetString();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (dl.GetFile() == null)
            {
                return;
            }
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML file|*.xml|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                bw.Write(dl.GetFile());
                bw.Flush();
                bw.Close();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            this.textBox1.Text = dl.GetString(true, true);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML file|*.xml|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                byte[] str = Encoding.UTF8.GetBytes(dl.GetString(true));
                bw.Write(str);
                bw.Flush();
                bw.Close();
            }
        }

        List<string> tables = new List<string>();
        int currenttable = -1; // Current active table

        private void button7_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "XML files (*.xml)|*.xml|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dl.LoadLibConfig(ofd.FileName);
                tables = dl.GetTableList();
                UpdateListbox();
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selindex = this.listBox2.SelectedIndex;
            
            if (selindex >= 0 && selindex < dl.GetTableCount())
            {
                string seltext = (string)this.listBox2.SelectedItem;
                string[] parts = seltext.Split(new char[] { '.' }, 2);
                int tableid = int.Parse(parts[0]);
                currenttable = tableid;
                if (tableid >= 0)
                {
                    this.dataGridView1.DataSource = this.dl.GetTable(tableid);
                }
            }
            else
            {
                currenttable = -1;
            }
        }

        private void UpdateListbox()
        {
            this.listBox2.BeginUpdate();
            this.listBox2.Items.Clear();
            for (int i = 0; i < tables.Count; i++)
            {
                string entry = "" + i + ". " + tables[i];
                if (entry.IndexOf(this.textBox2.Text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    this.listBox2.Items.Add(entry);
                }
            }

            this.listBox2.EndUpdate();
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            UpdateListbox();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            this.dl.UpdateTable(this.currenttable);
        }

        private void iDXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // IDX export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "IDX file|*.idx|All files|*.*";
            byte[] data = dl.ExportIdx();
            if (data == null)
            {
                MessageBox.Show("Export to DAT file first to build IDX");
                return;
            }
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                
                bw.Write(data);
                bw.Flush();
                bw.Close();
            }
        }

        private void dATToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // DAT export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "DAT file|*.dat|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                bw.Write(dl.ExportDAT(dl.ExportXML(), true));
                bw.Flush();
                bw.Close();
            }
        }

        private void dATUnencryptedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Unencrypted DAT export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML file|*.xml|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                bw.Write(dl.ExportDAT(dl.ExportXML()));
                bw.Flush();
                bw.Close();
            }
        }

        private void xMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // XML export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML file|*.xml|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                bw.Write(dl.ExportXML());
                bw.Flush();
                bw.Close();
            }
        }

        private void xLSExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // CSV export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV file|*.csv|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                // Insert UTF-8 BOM so that other languages can be read by CSV editors
                byte[] utf8bom = new byte[3] {0xEF, 0xBB, 0xBF};
                bw.Write(utf8bom, 0, 3);
                bw.Write(dl.ExportCSV(currenttable));
                bw.Flush();
                bw.Close();
            }
        }

        // Export Dropdown button
        private void button9_Paint(object sender, PaintEventArgs e)
        {
            base.OnPaint(e);

            int arrowX = button9.ClientRectangle.Width - 14;
            int arrowY = button9.ClientRectangle.Height / 2 - 1;

            Brush brush = Enabled ? SystemBrushes.ControlText : SystemBrushes.ButtonShadow;
            Point[] arrows = new Point[] { new Point(arrowX, arrowY), new Point(arrowX + 7, arrowY), new Point(arrowX + 3, arrowY + 4) };
            e.Graphics.FillPolygon(brush, arrows);
        }

        // Export Dropdown button
        private void button9_MouseDown(object sender, MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                contextMenuStrip1.Show(this.button9, 0, this.button9.Height);
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            // Import CSV
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV files (*.csv)|*.csv|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                int result = dl.ImportTable(currenttable, ofd.FileName);
                switch (result)
                {
                    case -1:
                        MessageBox.Show("No table selected to override");
                        break;
                    case 1:
                        MessageBox.Show("Table structure do not match imported format");
                        break;
                }
                this.dataGridView1.DataSource = this.dl.GetTable(currenttable);
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            CSVHelper csvh = new CSVHelper();
            csvh.Show();
        }
    }
}
