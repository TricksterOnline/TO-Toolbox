using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Imaging;

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
		public string nriName = "";

        // Open NRI
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "NRI files (*.nri;*.bac)|*.nri;*.bac|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
				// Can use GetFileNameWithoutExtension, but .bac/.nri would be indifferentiable then
				nriName = Path.GetFileName(ofd.FileName);

                if (nril.Load(ofd.FileName))
                {
                    if (nril.status != "")
                    {
                        MessageBox.Show(nril.status);
                    }

                    // Populate file list
                    this.listBox1.Items.Clear();
                    int fileCount = nril.GetFileCount();
                    for (int i = 0; i < fileCount; i++)
                    {
                        this.listBox1.Items.Add("Image" + (i + 1));
                    }

                    // Popuate animation list
                    this.listBox3.Items.Clear();
                    int animCount = nril.GetAnimationsCount();
                    for (int i = 0; i < animCount; i++)
                    {
                        NRILoader.Animation anim = nril.GetAnimations(i);
                        this.listBox3.Items.Add("" + (i+1) + ". " + anim.name);
                    }
                }
                else
                {
                    MessageBox.Show("Unable to load NRI file");
                }
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (nril.GetFileCount() > 0)
            {
                int fileid = this.listBox1.SelectedIndex;
                if (fileid < nril.GetFileCount())
                {
                    try
                    {
                        Stream bmpstream = nril.GetFile(fileid);
                        this.pictureBox1.Image = Image.FromStream(bmpstream);
                        bmpstream.Close();
                    }
                    catch
                    {

                    }
                }
            }
        }

        // Extract image
        private void button2_Click(object sender, EventArgs e)
        {
            if (nril.GetFileCount() > 0)
            {
                int fileid = this.listBox1.SelectedIndex;
                if (fileid < nril.GetFileCount() && fileid >= 0)
                {
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

        // Open DAT
        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "DAT files (*.dat)|*.dat|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.BeginOperation(new DoWorkEventHandler(
                    delegate(object s, DoWorkEventArgs ev)
                    {
                        dl.Load(ofd.FileName);
                    }
                ), new RunWorkerCompletedEventHandler(
                    delegate(object s, RunWorkerCompletedEventArgs ev)
                    {
                        this.textBox1.Text = dl.GetString();
                    }
                ));
            }
        }

        // Export unencrypted DAT
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

        // Display processed XML
        private void button5_Click(object sender, EventArgs e)
        {
            string result = "";
            this.BeginOperation(new DoWorkEventHandler(
                delegate(object s, DoWorkEventArgs ev)
                {
                    result = dl.GetString(true, true);
                }
            ), new RunWorkerCompletedEventHandler(
                delegate(object s, RunWorkerCompletedEventArgs ev)
                {
                    this.textBox1.Text = result;// dl.GetString(true, true);
                }
            ));
            //this.textBox1.Text = dl.GetString(true, true);
        }

        // Export processed XML
        private void button6_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML file|*.xml|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                byte[] str = null;
                this.BeginOperation(new DoWorkEventHandler(
                    delegate(object s, DoWorkEventArgs ev)
                    {
                        str = Encoding.UTF8.GetBytes(dl.GetString(true));
                    }
                ), new RunWorkerCompletedEventHandler(
                    delegate(object s, RunWorkerCompletedEventArgs ev)
                    {
                        BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                        bw.Write(str);
                        bw.Flush();
                        bw.Close();
                    }
                ));
            }
        }

        List<string> tables = new List<string>();
        int currenttable = -1; // Current active table

        // Load Libconfig.xml
        private void button7_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "XML files (*.xml)|*.xml|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dl.LoadLibConfig(ofd.FileName);
                tables = dl.GetTableList();
                UpdateListbox();
                if (dl.GetStatus() != "")
                {
                    MessageBox.Show(dl.GetStatus());
                }
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
                this.label1.Text = "Table: " + seltext;
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

        // Update table
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
                MessageBox.Show("Export to DAT first before building IDX");
                return;
            }
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                bw.Write(data);
                bw.Flush();
                bw.Close();
                MessageBox.Show("Export completed");
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
                byte[] data = dl.ExportXML();
                if (data == null)
                {
                    MessageBox.Show("Failed to export. " + dl.GetStatus());
                    return;
                }
                data = dl.ExportDAT(data);
                GC.Collect();
                data = dl.EncryptFile(data);
                bw.Write(data);
                bw.Flush();
                bw.Close();
                MessageBox.Show("Export completed");
            }
            GC.Collect();
        }

        private void dATUnencryptedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Unencrypted DAT export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML file|*.xml|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                byte[] data = dl.ExportXML();
                if (data == null)
                {
                    MessageBox.Show("Failed to export. " + dl.GetStatus());
                    return;
                }
                bw.Write(dl.ExportDAT(data));
                bw.Flush();
                bw.Close();
                if (data != null)
                {
                    MessageBox.Show("Export completed");
                }
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
                byte[] data = dl.ExportXML();
                if (data == null)
                {
                    MessageBox.Show("Failed to export. " + dl.GetStatus());
                    return;
                }
                bw.Write(data);
                bw.Flush();
                bw.Close();
                if (data != null)
                {
                    MessageBox.Show("Export completed");
                }
                
            }
        }

        private void xLSExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currenttable < 0)
            {
                MessageBox.Show("Please select a table to export");
                return;
            }

            // CSV export
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV file|*.csv|All files|*.*";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                // Insert UTF-8 BOM so that other languages can be read by CSV editors
                byte[] utf8bom = new byte[3] {0xEF, 0xBB, 0xBF};
                bw.Write(utf8bom, 0, 3);
                byte[] data = dl.ExportCSV(currenttable);
                if (data != null) {
                    bw.Write(data);
                } else {
                    MessageBox.Show("Invalid table selected");
                }
                bw.Flush();
                bw.Close();
                if (data != null)
                {
                    MessageBox.Show("Export completed");
                }
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

        // Import table
        private void button8_Click(object sender, EventArgs e)
        {
            if (currenttable < 0)
            {
                MessageBox.Show("No table selected to override");
                return;
            }

            // Import CSV
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV files (*.csv)|*.csv|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try {
                    int result = dl.ImportTable(currenttable, ofd.FileName);
                    switch (result)
                    {
                        case -1:
                            MessageBox.Show("No table selected to override");
                            break;
                        case 1:
                            MessageBox.Show("Table structure do not match imported format");
                            break;
                        case 0:
                            this.dataGridView1.DataSource = this.dl.GetTable(currenttable);
                            MessageBox.Show("Selected table has been overwritten");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to override table, table format do not match");
                }
            }
        }

        // Localization helper
        private void button10_Click(object sender, EventArgs e)
        {
            CSVHelper csvh = new CSVHelper();
            csvh.Show();
        }


        /** Background worker **/
        private BackgroundWorker bw = null;
        // Performs the task in a background worker
        private void BeginOperation(DoWorkEventHandler taskFunc, RunWorkerCompletedEventHandler completeFunc)
        {
            this.bw = new BackgroundWorker();
            this.bw.DoWork += taskFunc;
            this.bw.RunWorkerCompleted += completeFunc;

            // Progress bar handling
            this.bw.WorkerReportsProgress = true;
            this.dl.ProgressCallback = this.bw.ReportProgress;
            this.bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(this.ThreadedOperationDone);
            this.bw.ProgressChanged += new ProgressChangedEventHandler(this.ThreadedOperationProgress);

            this.ThreadedOperationStart();
        }
        
        // Event
        private void ThreadedOperationStart()
        {
            this.toolStripProgressBar1.Visible = true;
            bw.RunWorkerAsync();
        }
        // Event
        private void ThreadedOperationProgress(object sender, ProgressChangedEventArgs e)
        {
            //this.toolStripProgressBar1.Value = this.dl.GetProgress();
            this.toolStripProgressBar1.Value = e.ProgressPercentage;
        }
        // Event
        private void ThreadedOperationDone(object sender, EventArgs e) // GUI thread
        {
            //this.textBox1.Text = dl.GetString();
            this.toolStripProgressBar1.Visible = false;
            this.toolStripStatusLabel1.Text = this.dl.GetStatus();
        }

        // Decompress file
        private void button11_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "NRI files (*.nri;*.bac)|*.nri;*.bac|All files|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                byte[] data = nril.DecompressFile(ofd.FileName);
                if (data == null)
                {
                    MessageBox.Show("Input data not compressed");
                    return;
                }
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "NRI files (*.nri;*.bac)|*.nri;*.bac|All files|*.*";
                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    BinaryWriter bw = new BinaryWriter(File.Create(sfd.FileName));
                    bw.Write(data, 0, data.Length);
                    bw.Flush();
                    bw.Close();
                }
            }
        }

        // Extract all images
        private void button13_Click(object sender, EventArgs e)
        {
            if (nril.GetFileCount() > 0)
            {
                FolderBrowserDialog sfd = new FolderBrowserDialog();
                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
					string targetDir = sfd.SelectedPath + Path.DirectorySeparatorChar;
                    int files = nril.GetFileCount();
					int numLength = files.ToString ().Length;
                    for (int i = 0; i < files; i++)
                    {
						i++;
						string fileNum = i.ToString().PadLeft(numLength, '0');
						string genFileName = targetDir + nriName + "_img" + fileNum + ".bmp";
                        MemoryStream bmpstream = nril.GetFile(i);
						BinaryWriter bw = new BinaryWriter(File.Create(genFileName));
                        bmpstream.Flush();
                        bw.Write(bmpstream.ToArray());
                        bw.Flush();
                        bw.Close();
                        bmpstream.Close();
                    }
                }
            }
            else
            {
                MessageBox.Show("Nothing to extract");
            }
        }

        // Animation time bar
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            DisplayAnimation(listBox3.SelectedIndex, trackBar1.Value);
        }

        // Animation selector
        private void listBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            int id = listBox3.SelectedIndex;
            if (id >= 0 && id < nril.GetAnimationsCount())
            {
                trackBar1.Minimum = 0;

                NRILoader.Animation anim = nril.GetAnimations(id);
                trackBar1.Maximum = anim.frames.Count;
                
                DisplayAnimation(id, 0);
            }
        }

        // Displays an animation
        private void DisplayAnimation(int id, int frameid)
        {
            if (id >= 0 && id < nril.GetAnimationsCount())
            {
                NRILoader.Animation anim = nril.GetAnimations(id);

                this.label2.Text = anim.name + " (" + anim.frames.Count + " frames)";
                if (frameid >= 0 && frameid < anim.frames.Count)
                {
                    // Render animation
                    NRILoader.Frame frame = anim.frames[frameid];
                    // TODO: Adjusted to fit entire animation
                    int canvaswidth = 500;
                    int canvasheight = 500;

                    // Setup chroma-key maps
                    ColorMap cmap1 = new ColorMap();
                    cmap1.OldColor = Color.FromArgb(0, 255, 0);
                    cmap1.NewColor = Color.Transparent;
                    ColorMap cmap2 = new ColorMap();
                    cmap2.OldColor = Color.FromArgb(255, 0, 255);
                    cmap2.NewColor = Color.Transparent;
                    ImageAttributes imageAttributes = new ImageAttributes();
                    imageAttributes.SetRemapTable(new ColorMap[] { cmap1, cmap2 }, ColorAdjustType.Bitmap);

                    Bitmap canvas = new Bitmap(canvaswidth, canvasheight); 
                    Graphics g = Graphics.FromImage(canvas);
                    int baseoffsetx = canvaswidth/2;
                    int baseoffsety = canvasheight/2;

                    // Draw guide lines (for debugging)
                    Pen gridPen = new Pen(Color.Black);
                    gridPen.Width = 1;
                    // Grid center is at (baseoffsetx, baseoffsety)
                    g.DrawLine(gridPen, 0, baseoffsety, baseoffsetx * 2, baseoffsety);
                    g.DrawLine(gridPen, baseoffsetx, 0, baseoffsetx, baseoffsety * 2);

                    for (int i = 0; i < frame.planes.Count; i++)
                    {
                        // Draw the requested image
                        NRILoader.FramePlane plane = frame.planes[i];
                        int bitmapid = plane.bitmapid;

                        // Some animations appear to have negative ids specfied (need more investigation)
                        if (bitmapid >= 0 && bitmapid < nril.GetFileCount())
                        {
                            Stream bmpstream = nril.GetFile(bitmapid);
                            Bitmap bmp = new Bitmap(bmpstream);

                            // Perform image flips
                            switch (plane.reverseflag) {
                                case 1:
                                    bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                                    break;
                                case 2:
                                    bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                                    break;
                                case 3:
                                    bmp.RotateFlip(RotateFlipType.RotateNoneFlipXY);
                                    break;
                            }

                            int x = plane.x + baseoffsetx;
                            int y = plane.y + baseoffsety;
                            g.DrawImage(bmp,
                                new Rectangle(x, y, bmp.Width, bmp.Height),
                                0, 0, bmp.Width, bmp.Height
                                ,GraphicsUnit.Pixel, imageAttributes);
                            bmpstream.Close();
                        }
                        else
                        {
                            MessageBox.Show("Unable to load " + bitmapid);
                        }
                    }

                    // Display to picture box
                    this.pictureBox2.Image = canvas;
                }
            }
        }
    }
}
