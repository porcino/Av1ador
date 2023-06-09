﻿using Av1ador.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Av1ador
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        static extern int GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, ref int ProcessID);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int VirtualKeyPressed);
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point point);

        private readonly string title = "Av1ador 1.0.9";
        private readonly Regex formatos = new Regex(".+(mkv|mp4|avi|webm|ivf|m2ts|wmv|mpg|mov|3gp|ts|mpeg|y4m|vob|m4v)$", RegexOptions.IgnoreCase);
        private readonly string mpv_args = " --pause --hr-seek=always -no-osc --osd-level=0 --no-border --mute --sid=no --no-window-dragging --video-unscaled=yes --no-input-builtin-bindings --input-ipc-server=\\\\.\\pipe\\mpvsocket --idle=yes --keep-open=yes --dither-depth=auto --background=0.78/0.78/0.78 --alpha=blend --osd-font-size=24 --osd-duration=5000 --osd-border-size=1.5 --osd-scale-by-window=no";
        private static readonly int processID = Process.GetCurrentProcess().Id;
        private Video primer_video, segundo_video;
        public Process mpv2 = new Process();
        private System.IO.Pipes.NamedPipeClientStream mpv_tubo = new System.IO.Pipes.NamedPipeClientStream("mpvsocket" + processID.ToString());
        private System.IO.Pipes.NamedPipeClientStream mpv2_tubo = new System.IO.Pipes.NamedPipeClientStream("mpv2socket" + processID.ToString());
        private StreamWriter mpv_cmd, mpv2_cmd;
        private StreamReader mpv_out;
        private Process mpv1p, mpv2p;
        private bool mpv2_loaded = false;
        private double panx, pany, panx_ratio, pany_ratio;
        private bool click_in, mouse1, moviendo_divisor, reading, can_sync;
        private Point click_pos, mouse_pos, mouse_pos_antes;
        private int focus_id, mpv_left, me_x, underload;
        private Encoder encoder;
        private Encode encode;
        private double scale = 1.0;
        private int usage, prevheight;
        private PerformanceCounter cpu;
        private PerformanceCounter disk;
        private PerformanceCounter ram;
        private string[] disks;
        Settings settings;
        private FormWindowState winstate;
        private Size winsize;
        private Point winpos;
        private readonly BackgroundWorker aset = new BackgroundWorker();
        private Color heat = Color.FromArgb(220, 220, 220);
        private string[] clipboard = new string[] { "", "" };
        private int hover_before = -1;

        public static bool Dialogo { get; set; }

        public Form1()
        {
            InitializeComponent();
            aset.DoWork += (s, ee) =>
            {
                int aid = (int)ee.Argument;
                while (primer_video != null && primer_video.Busy)
                    Thread.Sleep(30);
                /*if (primer_video != null && encoder != null)
                    encoder.Af_add("adelay", primer_video.Tracks_delay[aid].ToString());*/
            };
            aset.RunWorkerCompleted += (s, ee) =>
            {
                infoTimer.Enabled = true;
                checkedListBox1.Enabled = true;
                Filter_items_update();
            };
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Text = title;
            string exes = Func.Exes();

            encoder = new Encoder
            {
                Libfdk = exes.Contains("enable-libfdk-aac"),
                Libplacebo = exes.Contains("enable-libplacebo")
            };
            workersUpDown.Maximum = encoder.Cores;
            workersgroupBox.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(workersgroupBox, true, null);

            playtimeLabel.Text = "";
            mediainfoLabel.Text = "";
            cvComboBox.SelectedIndex = 0;
            fpsComboBox.SelectedIndex = 0;
            bitsComboBox.SelectedIndex = 0;
            hdrComboBox.SelectedIndex = 0;
            caComboBox.SelectedIndex = 0;
            chComboBox.SelectedIndex = 0;
            formatComboBox.SelectedIndex = 0;

            leftPanel.Width = mpvsPanel.Width;
            rightPanel.Width = Screen.FromControl(this).Bounds.Width;
            Show_filter(true);

            Process[] processes = Process.GetProcessesByName("mpv");
            List<IntPtr> ignore = new List<IntPtr>();
            foreach (Process p in processes)
                ignore.Add(p.MainWindowHandle);
            Process.Start(Func.bindir + "mpv.exe", mpv_args.Replace("socket", "socket" + processID.ToString()));
            BackgroundWorker bw = new BackgroundWorker();
            BackgroundWorker bw2 = new BackgroundWorker();
            Process mp = mpv1p;
            bw.DoWork += (s, ee) =>
            {
                bool found = false;
                int limit = 0;
                while (!found && limit < 8000)
                {
                    limit += 16;
                    Thread.Sleep(16);
                    processes = Process.GetProcessesByName("mpv");
                    foreach (Process p in processes)
                    {
                        if (!ignore.Contains(p.MainWindowHandle))
                        {
                            mp = p;
                            found = true;
                        }
                    }
                }
                if (limit >= 8000)
                {
                    if (MessageBox.Show("The video player (mpv) failed to start or is unavailable.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                        Environment.Exit(0);
                }
            };
            bw.RunWorkerCompleted += (s, ee) =>
            {
                mpv1p = mp;
                Wait_mpv(5);
                Thread.Sleep(16);
                SetParent(mpv1p.MainWindowHandle, leftPanel.Handle);
                MoveWindow(mpv1p.MainWindowHandle, 0, 0, Screen.FromControl(this).Bounds.Width, Screen.FromControl(this).Bounds.Height, true);
                mpv_cmd = new StreamWriter(mpv_tubo);
                mpv_tubo.Connect();
                mpv_cmd.AutoFlush = true;

                Entry.Load(listBox1);

                underload = -2;
                Program.Log = true;
                Restore_settings(true);
                Mpv_load_first();
                infoTimer.Enabled = true;
            };
            bw.RunWorkerAsync();

            bw2.DoWork += (s, ee) =>
            {
                cpu = new PerformanceCounter
                {
                    CategoryName = "Processor",
                    CounterName = "% Processor Time",
                    InstanceName = "_Total"
                };

                PerformanceCounterCategory disks_cat = new PerformanceCounterCategory("PhysicalDisk");
                disks = disks_cat.GetInstanceNames();
                ram = new PerformanceCounter("Memory", "Available MBytes");
            };
            bw2.RunWorkerAsync();
        }

        private void Mpv2_load(string file, string args = "", double seek = 0)
        {
            if (!IsLoaded_mpv2())
            {
                Process[] processes = Process.GetProcessesByName("mpv");
                List<IntPtr> ignore = new List<IntPtr>();
                foreach (Process p in processes)
                    ignore.Add(p.MainWindowHandle);
                Process.Start(Func.bindir + "mpv.exe", mpv_args.Replace("socket", "2socket" + processID.ToString()));
                BackgroundWorker bw = new BackgroundWorker();
                Process mp = mpv2p;
                bw.DoWork += (s, ee) =>
                {
                    bool found = false;
                    while (!found)
                    {
                        Thread.Sleep(16);
                        processes = Process.GetProcessesByName("mpv");
                        foreach (Process p in processes)
                        {
                            if (!ignore.Contains(p.MainWindowHandle))
                            {
                                mp = p;
                                found = true;
                            }
                        }
                    }
                };
                bw.RunWorkerCompleted += (s, ee) =>
                {
                    mpv2p = mp;
                    Wait_mpv(6);
                    Thread.Sleep(16);
                    SetParent(mpv2p.MainWindowHandle, rightPanel.Handle);
                    MoveWindow(mpv2p.MainWindowHandle, 0, 0, Screen.FromControl(this).Bounds.Width, Screen.FromControl(this).Bounds.Height, true);
                    mpv2_loaded = true;
                    mpv2_cmd = new StreamWriter(mpv2_tubo);
                    mpv2_tubo.Connect();
                    mpv2_cmd.AutoFlush = true;

                    Mpv_load_second(file, args, seek);
                };
                bw.RunWorkerAsync();
            }
            else
                Mpv_load_second(file, args, seek);
        }

        private void Mpv_load_first()
        {
            if (listBox1.Items.Count > 0)
            {
                Entry entry = listBox1.SelectedIndex > -1 ? (Entry)listBox1.SelectedItems[0] : (Entry)listBox1.Items[0];
                if (!File.Exists(entry.File))
                {
                    listBox1.Items.Remove(entry);
                    return;
                }
                if (primer_video == null || (primer_video != null && entry.File != primer_video.File))
                {
                    encoder.Vf.Clear();
                    while (primer_video != null && primer_video.Busy)
                    {
                        primer_video.Busy = false;
                        Thread.Sleep(10);
                    }
                    checkedListBox1.Enabled = false;

                    syncButton.Checked = false;
                    removeButton.Enabled = true;
                    picBoxBarra.Cursor = Cursors.Hand;
                    primer_video = new Video(entry.File);
                    mpv_cmd.WriteLine("loadfile \"" + primer_video.File.Replace(@"\", @"\\").Replace(@"'", @"\'") + "\";set pause yes;set fullscreen yes");
                    mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-x\", 1] }");
                    mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-y\", 1] }");
                    zoomButton.Checked = false;
                    encoder.Playtime = 0;
                    mediainfoLabel.Text = primer_video.Mediainfo();
                    checkedListBox1.Items.Clear();
                    checkedListBox1.Items.AddRange(primer_video.Tracks.ToArray());
                    hdrComboBox.Enabled = primer_video.Hdr;
                    encoder.Set_audio_codec(caComboBox.Text.Split(' ')[0], primer_video.Channels);
                    Func.Update_combo(chComboBox, encoder.Channels, true);
                    caComboBox.Enabled = chComboBox.Enabled;
                    groupBox2.Enabled = chComboBox.Enabled;
                    audiounmuteButton.Enabled = chComboBox.Enabled;
                    encoder.Predicted = false;
                    if (entry.Vf != "")
                        encoder.Vf = Entry.Filter2List(entry.Vf);
                    if (entry.Af != null && entry.Af != "")
                        encoder.Af = Entry.Filter2List(entry.Af);
                    Get_res();
                    resComboBox.Text = entry.Resolution ?? resComboBox.Text;
                    primer_video.CreditsTime = entry.Credits;
                    primer_video.CreditsEndTime = entry.CreditsEnd;
                    cvComboBox.SelectedIndex = entry.Cv;
                    paramsBox.Text = entry.Param == "" ? encoder.Params_replace((int)Math.Round(primer_video.Fps)) : entry.Param;
                    bitsComboBox.Text = entry.Bits;
                    numericUpDown1.Value = entry.Crf;
                    if (entry.Bv.Length > 0)
                        bitrateBox.Text = entry.Bv;
                    else
                        bitrateBox.Text = totalBox.Text = "";
                    abitrateBox.Text = entry.Ba.ToString();
                    creditsendButton.Enabled = primer_video.CreditsTime > 0;
                    if (entry.Gs != "" && int.Parse(entry.Gs) <= gsUpDown.Maximum)
                        gsUpDown.Value = int.Parse(entry.Gs);
                    for (int i = 0; i < checkedListBox1.Items.Count; i++)
                        checkedListBox1.SetItemCheckState(i, i == entry.Track ? CheckState.Checked : CheckState.Unchecked);
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += (s, e) => {
                        while (disks == null)
                            Thread.Sleep(100);
                    };
                    bw.RunWorkerCompleted += (s, e) => {
                        string disk_letter = "_Total";
                        foreach (string d in disks)
                            disk_letter = d.Contains(entry.File.Substring(0, 2)) ? d : disk_letter;
                        disk = new PerformanceCounter
                        {
                            CategoryName = "PhysicalDisk",
                            CounterName = "% Disk Time",
                            InstanceName = disk_letter
                        };
                    };
                    bw.RunWorkerAsync();
                    infoTimer.Interval = 221;
                }
            }
            else
            {
                picBoxBarra.Cursor = Cursors.Default;
                mpv_cmd.WriteLine("playlist-play-index none");
                primer_video = null;
            }
            if (IsLoaded_mpv2())
            {
                mpv2_cmd.WriteLine("playlist-play-index none");
                segundo_video = null;
                leftPanel.Width = mpvsPanel.Width;
            }
            UpdateLayout();
            Restore_settings();
        }

        private void Mpv_load_second(string file, string cmds, double seek, [Optional] double len)
        {
            if (primer_video == null || !File.Exists(file))
                return;
            if (segundo_video == null || (segundo_video != null && segundo_video.File != file))
            {
                if (leftPanel.Width > mpvsPanel.Width - mpvsPanel.Width / 15)
                    leftPanel.Width = mpvsPanel.Width / 2;
                UpdateLayout();

                Video video = new Video(file, true, len);
                BackgroundWorker backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += (o, e) =>
                {
                    while (video.First_frame == -1)
                    {
                        Thread.Sleep(40);
                    }
                    if (video.Width > 0)
                    {
                        cmds = cmds == null ? "" : ";" + cmds;
                        scale = zoomButton.Checked ? 2.0 : 1.0;
                        double zoom = Func.Scale(primer_video, video, scale, primer_video.Sar != video.Sar && encoder.Vf.Count > 0 && Func.Find_w_h(encoder.Vf).Count() > 0 ? Double.Parse(Func.Find_w_h(encoder.Vf)[0]) : 0);
                        mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-x\", " + zoom + "] }");
                        mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-y\", " + zoom + "] }");
                        panx_ratio = ((Double)primer_video.Width * (zoom / scale) * (primer_video.Sar > 1 ? primer_video.Sar : 1.0)) / ((Double)video.Width * video.Sar);
                        pany_ratio = ((Double)primer_video.Height * (zoom / scale) / (primer_video.Sar < 1 ? primer_video.Sar : 1.0)) / ((Double)video.Height / video.Sar);
                        mpv2_cmd.WriteLine("loadfile \"" + file.Replace(@"\", @"\\").Replace(@"'", @"\'") + "\";set pause yes;set fullscreen yes;set video-pan-x " + (panx * panx_ratio) + ";set video-pan-y " + (pany * pany_ratio) + cmds);
                        mpv2_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-x\", " + scale + "] }");
                        mpv2_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-y\", " + scale + "] }");
                        Wait_mpv();
                        if (seek != 0)
                            mpv2_cmd.WriteLine("seek " + (seek + video.First_frame).ToString() + " absolute+exact");
                    }
                };
                backgroundWorker.RunWorkerCompleted += (s, e) =>
                {
                    segundo_video = video;
                };
                backgroundWorker.RunWorkerAsync();
            }
            else if (seek != 0)
                mpv2_cmd.WriteLine("set pause yes;seek " + (seek + segundo_video.First_frame).ToString() + " absolute+exact");
        }

        private void Get_res()
        {
            if (primer_video == null)
                return;
            resComboBox.Items.Clear();
            double[] ur = Func.Upscale_ratio(encoder.Vf);
            int uh = (int)(primer_video.Height * ur[0]);
            for (int i = 0; i < encoder.Resos.Length; i++)
            {
                int alto = int.Parse(encoder.Resos[i].Replace("p", ""));
                if (alto <= uh || alto <= uh / primer_video.Sar || alto <= primer_video.Width * ur[1] * 9 / 15)
                {
                    resComboBox.Items.Add(encoder.Resos[i]);
                    if (resComboBox.SelectedIndex < 0 && (alto <= Screen.FromControl(this).Bounds.Height || alto <= Screen.FromControl(this).Bounds.Height))
                        resComboBox.Text = encoder.Resos[i];
                }
            }
            if (uh / primer_video.Sar > int.Parse(resComboBox.Items[0].ToString().Replace("p", "")))
            {
                resComboBox.Items.Insert(0, uh / primer_video.Sar + "p");
                resComboBox.Text = resComboBox.Items[0].ToString();
            }
        }

        private bool IsLoaded_mpv2()
        {
            return mpv2_loaded;
        }

        private void Detener()
        {
            mpvTimer.Enabled = false;
            playButton.Visible = true;
            pauseButton.Visible = false;
        }

        private bool Wait_mpv(int times = 1)
        {
            bool idle1 = false;
            bool idle2 = false;
            int limit = 0;
            while ((!idle1 || !idle2) && limit < 5000)
            {
                TimeSpan mpv1_b = mpv1p.TotalProcessorTime;
                TimeSpan mpv2_b = mpv1_b;
                if (IsLoaded_mpv2())
                    mpv2_b = mpv2p.TotalProcessorTime;
                Thread.Sleep(20 * times);
                limit += 20 * times;
                mpv1p.Refresh();
                TimeSpan mpv1_e = mpv1p.TotalProcessorTime;
                TimeSpan mpv2_e = mpv1_e;
                if (IsLoaded_mpv2())
                {
                    mpv2p.Refresh();
                    mpv2_e = mpv2p.TotalProcessorTime;
                }
                if (mpv1_e == mpv1_b)
                    idle1 = true;
                if (!IsLoaded_mpv2())
                    idle2 = true;
                else if (mpv2_e == mpv2_b)
                    idle2 = true;
            }
            return true;
        }

        private void UpdateLayout(bool hide = false)
        {
            int bordes = (tableLayoutPanel1.PointToScreen(Point.Empty).X - Left) * 2;
            int form_w = Width - bordes;
            mpv_left = leftPanel.PointToScreen(Point.Empty).X;

            leftPanel.Width = (int)Math.Ceiling(Double.Parse(leftPanel.Width.ToString()) * Double.Parse((form_w - tableLayoutPanel3.Width).ToString()) / Double.Parse((me_x > 0 ? me_x : Width).ToString()));

            if ((hide && segundo_video == null) || (mpv_left + leftPanel.Width - PointToScreen(Point.Empty).X) > form_w)
                leftPanel.Width = mpvsPanel.Width;

            rightPanel.Width = Screen.FromControl(this).Bounds.Width - (mpv_left - PointToScreen(Point.Empty).X) - (Screen.FromControl(this).Bounds.Width - Width + bordes);
            
            if (primer_video != null)
            {
                if (encode == null || encode.Splits == null || encode.File != primer_video.File)
                    picBoxBarra.Image = primer_video.Bar(picBoxBarra.Width, picBoxBarra.Height, encoder.Playtime);
            }
            
            me_x = form_w - tableLayoutPanel3.Width;
        }

        private void Restore_settings(bool before = false)
        {
            if (File.Exists("settings.xml"))
            {
                settings = encoder.Load_settings();
                formatComboBox.Text = settings.Format != "Default" ? settings.Format : formatComboBox.Text;
                if (before)
                    cvComboBox.Text = settings.Codec_video != "Default" ? settings.Codec_video : cvComboBox.Text;
                speedComboBox.Text = settings.Speed != "Default" ? settings.Speed : speedComboBox.Text;
                if (before)
                    resComboBox.Text = settings.Resolution != "Default" ? settings.Resolution : resComboBox.Text;
                hdrComboBox.Text = settings.Hdr != "Default" ? settings.Hdr : hdrComboBox.Text;
                if (before)
                    bitsComboBox.Text = settings.Bit_depth != "Default" ? settings.Bit_depth : bitsComboBox.Text;
                int crf = 99;
                try { crf = int.Parse(settings.Crf); }
                catch { }
                if (before && crf < 99 && crf > numericUpDown1.Minimum && crf < numericUpDown1.Maximum)
                    numericUpDown1.Value = settings.Crf != "Default" ? crf : numericUpDown1.Value;
                caComboBox.Text = settings.Codec_audio != "Default" ? settings.Codec_audio : caComboBox.Text;
                chComboBox.Text = settings.Channels != "Default" ? settings.Channels : chComboBox.Text;
                if (before)
                    abitrateBox.Text = settings.Audio_br;
                folderBrowserDialog1.SelectedPath = settings.Output_folder;
                outfolderButton.Checked = settings.Output_folder.Length > 0;
                origfolderButton.Checked = settings.Output_folder.Length == 0;
                gscheckBox.Checked = settings.Auto_grain_level;
                // && setting != null
            }
        }

        private bool Sobrelinea()
        {
            int division_x = leftPanel.PointToScreen(Point.Empty).X + leftPanel.Width;
            if (moviendo_divisor || (mouse_pos.X > (division_x - 10) && mouse_pos.X < (division_x + 10) && mouse_pos.Y > leftPanel.PointToScreen(Point.Empty).Y && mouse_pos.Y < (leftPanel.PointToScreen(Point.Empty).Y + splitterPanel.Height)))
            {
                if (!panTimer.Enabled)
                {
                    if (mouse1)
                        mouseTimer.Interval = 8;
                    return true;
                }
            }
            mouseTimer.Interval = 16;
            return false;
         }

        private void ListBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            Entry.Draw(listBox1, e);
        }

        private void VfListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            Func.DrawItems(vfListBox, e);
        }
        private void AfListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            Func.DrawItems(afListBox, e);
        }

        private void Update_Video_txt(bool save = true)
        {
            Entry.Save(listBox1, save);
            removeButton.Enabled = listBox1.Items.Count > 0;
        }

        private void ListBox1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                foreach (string file in (string[])(e.Data.GetData(DataFormats.FileDrop)))
                {
                    if (formatos.Match(file).Success && !Entry.Queued(listBox1, file) && File.Exists(file))
                        Add_entry(file);
                }
                
            }
            Update_Video_txt();
            if (primer_video == null)
                Mpv_load_first();
        }

        private void ListBox1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count != 0)
            {
                while (listBox1.SelectedIndex != -1)
                    listBox1.Items.RemoveAt(listBox1.SelectedIndex);
                Update_Video_txt();
            }
            if (listBox1.Items.Count == 0)
            {
                Detener();
                mpv_cmd.WriteLine("playlist-play-index none");
                primer_video = null;
                if (IsLoaded_mpv2())
                {
                    mpv2_cmd.WriteLine("playlist-play-index none");
                    segundo_video = null;
                }
            }
        }

        private void AddfilesButton_Click(object sender, EventArgs e)
        {
            Dialogo = true;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                foreach (string file in openFileDialog1.FileNames)
                {
                    Add_entry(file);
                }
                Update_Video_txt();
                if (primer_video == null)
                    Mpv_load_first();
            }
            Dialogo = false;
        }

        private void Add_entry(string file)
        {
            Entry entry = new Entry
            {
                File = file,
                Vf = "",
                Af = "",
                Gs = gsUpDown.Value.ToString(),
                Cv = cvComboBox.SelectedIndex,
                Bits = bitsComboBox.Text,
                Param = paramsBox.Text,
                Crf = (int)numericUpDown1.Value,
                Ba = int.Parse(abitrateBox.Text),
                Bv = bitrateBox.Text,
                Resolution = resComboBox.Text
            };
            listBox1.Items.Add(entry);
        }

        private void EncodefirstButton_Click(object sender, EventArgs e)
        {
            int sel = listBox1.SelectedItems.Count;
            for (int i = 0; i < sel; i++)
            {
                Entry entry = (Entry)listBox1.SelectedItems[0];
                listBox1.Items.Remove(entry);
                listBox1.Items.Insert(i, entry);
            }
            Update_Video_txt();
        }

        private void ListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Reconnect();
            encodefirstButton.Enabled = listBox1.SelectedIndex > 0;
            Update_Video_txt(false);
            if (listBox1.SelectedIndex > -1)
            {
                Entry entry = (Entry)listBox1.Items[listBox1.SelectedIndex];
                if (!File.Exists(entry.File))
                    listBox1.Items.RemoveAt(listBox1.SelectedIndex);
            }
            Filter_remove();
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (primer_video == null)
                return;
            can_sync = false;
            playButton.Visible = false;
            pauseButton.Visible = true;
            mpvTimer.Enabled = true;

            mpv_cmd.WriteLine("set pause no");
            if (IsLoaded_mpv2())
                mpv2_cmd.WriteLine("set pause no");
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            mpvTimer.Enabled = false;
            playButton.Visible = true;
            pauseButton.Visible = false;

            mpv_cmd.WriteLine("set pause yes");
            if (IsLoaded_mpv2())
                mpv2_cmd.WriteLine("set pause yes");
        }
        private void PrevframeButton_Click(object sender, EventArgs e)
        {
            if (prevframeButton.Enabled)
            {
                if (pauseButton.Visible)
                    Detener();
                nextframeButton.Enabled = false;
                prevframeButton.Enabled = false;
                mpv_cmd.WriteLine("frame-back-step");
                if (!syncButton.Checked)
                {
                    if (IsLoaded_mpv2())
                        mpv2_cmd.WriteLine("frame-back-step");
                }
                else
                    primer_video.First_frame -= 1.0 / primer_video.Fps;
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, ee) => {
                    Wait_mpv();
                };
                bw.RunWorkerCompleted += (s, ee) => {
                    nextframeButton.Enabled = true;
                    prevframeButton.Enabled = true;
                    mpvTimer.Enabled = true;
                };
                bw.RunWorkerAsync();
            }
        }        
        private void NextframeButton_Click(object sender, EventArgs e)
        {
            if (nextframeButton.Enabled)
            {
                if (pauseButton.Visible)
                    Detener();
                nextframeButton.Enabled = false;
                prevframeButton.Enabled = false;
                mpv_cmd.WriteLine("frame-step");
                if (!syncButton.Checked)
                {
                    if (IsLoaded_mpv2())
                        mpv2_cmd.WriteLine("frame-step");
                } else
                    primer_video.First_frame += 1.0 / primer_video.Fps;
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, ee) => {
                    Wait_mpv();
                };
                bw.RunWorkerCompleted += (s, ee) => {
                    nextframeButton.Enabled = true;
                    prevframeButton.Enabled = true;
                    mpvTimer.Enabled = true;
                };
                bw.RunWorkerAsync();
            }
        }
        private void ToolStrip2_MouseEnter(object sender, EventArgs e)
        {
            toolStrip2.Focus();
        }

        private void ToolStrip3_MouseEnter(object sender, EventArgs e)
        {
            toolStrip3.Focus();
        }

        private void ToolStrip1_MouseEnter(object sender, EventArgs e)
        {
            toolStrip1.Focus();
        }

        private void ExpandButton_Click(object sender, EventArgs e)
        {
            winstate = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            winsize = Size;
            winpos = Location;
            Location = new Point(Screen.FromControl(this).WorkingArea.Left, Screen.FromControl(this).WorkingArea.Top);
            Size = new Size(Screen.FromControl(this).Bounds.Width, Screen.FromControl(this).Bounds.Height);
            tableLayoutPanel1.RowStyles[2].Height = 16;
            tableLayoutPanel1.RowStyles[3].Height = 0;
            tableLayoutPanel4.RowStyles[0].Height = 24;
            syncButton.DisplayStyle = prevframeButton.DisplayStyle = playButton.DisplayStyle = pauseButton.DisplayStyle = nextframeButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            audiomuteButton.DisplayStyle = audiounmuteButton.DisplayStyle = expandButton.DisplayStyle = restoreButton.DisplayStyle = zoomButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            restoreButton.Visible = true;
            expandButton.Visible = false;
            splitContainer2.Panel1Collapsed = true;
            panel1.Height = 12;
            UpdateLayout(true);
        }

        private void RestoreButton_Click(object sender, EventArgs e)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = winstate;
            Size = winsize;
            Location = winpos;
            tableLayoutPanel1.RowStyles[2].Height = 24;
            tableLayoutPanel1.RowStyles[3].Height = 24;
            tableLayoutPanel4.RowStyles[0].Height = 38;
            syncButton.DisplayStyle = prevframeButton.DisplayStyle = playButton.DisplayStyle = pauseButton.DisplayStyle = nextframeButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            audiomuteButton.DisplayStyle = audiounmuteButton.DisplayStyle = expandButton.DisplayStyle = restoreButton.DisplayStyle = zoomButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            restoreButton.Visible = false;
            expandButton.Visible = true;
            splitContainer2.Panel1Collapsed = false;
            UpdateLayout(true);
        }

        private void AudiounmuteButton_Click(object sender, EventArgs e)
        {
            audiounmuteButton.Visible = false;
            audiomuteButton.Visible = true;
            mpv_cmd.WriteLine("cycle mute");
        }

        private void AudiomuteButton2_Click(object sender, EventArgs e)
        {
            audiounmuteButton.Visible = true;
            audiomuteButton.Visible = false;
            mpv_cmd.WriteLine("cycle mute");
        }

        private void MpvTimer_Tick(object sender, EventArgs e)
        {
            if (reading)
                return;
            mpv_cmd.WriteLine("{ \"command\": [\"get_property\", \"playback-time\"] }");
            mpv_out = new StreamReader(mpv_tubo);
            string recibo = "";
            Regex tiempo = new Regex("data\":([0-9\\.]+)");
            reading = true;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, ee) => {
                while (mpv_out.Peek() > -1)
                    recibo = mpv_out.ReadLine();
            };
            bw.RunWorkerCompleted += (s, ee) => {
                reading = false;
                try
                {
                    Update_current_time(Double.Parse(tiempo.Match(recibo).Groups[1].Value.ToString()));
                }
                catch { }
            };
            bw.RunWorkerAsync();   
        }

        private void Update_current_time(double time)
        {
            mpvTimer.Enabled = true;
            encoder.Playtime = time;
            var timespan = TimeSpan.FromSeconds(time);
            string current_time = timespan.ToString().Replace("0000", "");
            if (!syncButton.Checked)
            {
                playButton.Visible = current_time == playtimeLabel.Text;
                pauseButton.Visible = current_time != playtimeLabel.Text;
            }
            playtimeLabel.Text = current_time;
            encoder.Playtime = timespan.TotalSeconds;
        }

        private void PicBoxBarra_MouseClick(object sender, MouseEventArgs e)
        {
            if (primer_video == null)
                return;
            syncButton.Checked = false;
            can_sync = true;
            Reconnect();
            double pos = primer_video.Duration * e.Location.X / picBoxBarra.Width;
            Update_current_time(pos);
            if (encodestopButton.Enabled && encode != null && encode.Splits != null && encode.Chunks != null && encode.File == primer_video.File)
            {
                int segment = encode.Get_segment(picBoxBarra.Width, e.Location.X, primer_video.Duration);
                string name = encode.Name + "\\" + segment.ToString("00000") + "." + encode.Job;
                if (segment > -1 && (encode.Chunks[segment].Completed || (encode.Chunks[segment].Encoding && encode.Chunks[segment].Progress > 0)) && File.Exists(name))
                {
                    Detener();
                    Mpv2_load(name, "set pause yes", pos - Double.Parse(encode.Splits[segment]) - primer_video.First_frame);
                    mpv_cmd.WriteLine("set pause yes;seek " + pos.ToString() + " absolute+exact");
                }
                else
                {
                    leftPanel.Width = mpvsPanel.Width;
                    mpv_cmd.WriteLine("seek " + pos.ToString() + " absolute+exact");
                }
            }
            else
            {
                mpv_cmd.WriteLine("seek " + pos.ToString() + " absolute+exact");
                if (IsLoaded_mpv2() && segundo_video != null)
                    mpv2_cmd.WriteLine("seek " + (pos + segundo_video.First_frame - primer_video.First_frame - primer_video.StartTime).ToString() + " absolute+exact");
            }
        }

        private void Reconnect()
        {
            mpv_tubo = new System.IO.Pipes.NamedPipeClientStream("mpvsocket" + processID.ToString());
            mpv_cmd = new StreamWriter(mpv_tubo);
            mpv_tubo.Connect();
            mpv_cmd.AutoFlush = true;
            if (IsLoaded_mpv2())
            {
                mpv2_tubo = new System.IO.Pipes.NamedPipeClientStream("mpv2socket" + processID.ToString());
                mpv2_cmd = new StreamWriter(mpv2_tubo);
                mpv2_tubo.Connect();
                mpv2_cmd.AutoFlush = true;
            }
        }

        private void ToolStripButton2_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 1)
            {
                tabControl1.SelectedIndex = 0;
                tableLayoutPanel5.RowStyles[2].Height = 290;
            }
            else
                tableLayoutPanel5.RowStyles[2].Height = tableLayoutPanel5.RowStyles[2].Height == 0 ? 290 : 0;
            toolStripButton2.Checked = tableLayoutPanel5.RowStyles[2].Height != 0;

        }

        private void TrackBar1_Scroll(object sender, EventArgs e)
        {
            numericUpDown1.Value = trackBar1.Value;
        }

        private void NumericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            trackBar1.Value = (int)numericUpDown1.Value;
            encoder.Crf = (int)numericUpDown1.Value;
            Entry_update(7);
            Entry.Save(listBox1);
        }

        private void TotalBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && (e.KeyChar != (char)'.' || totalBox.Text.Contains(".")))
                e.Handled = true;
        }

        private void TotalBox_TextChanged(object sender, EventArgs e)
        {
            if (primer_video == null || totalBox.Text == ".")
                return;
            double total = totalBox.Text.Length > 0 ? Double.Parse(totalBox.Text) : 0;
            int bitrate = bitrateBox.Text.Length > 0 ? int.Parse(bitrateBox.Text) : 0;
            if (total > 0 || bitrate > 0)
            {
                if (totalBox.Text != total.ToString() && total > 0)
                {
                    totalBox.SelectionStart = totalBox.Text.Length;
                }
                if (bitrateBox.Text != bitrate.ToString() && bitrate > 0)
                {
                    bitrateBox.Text = bitrate.ToString();
                    bitrateBox.SelectionStart = bitrateBox.Text.Length;
                }
                trackBar1.Enabled = false;
                numericUpDown1.Enabled = false;
                constantLabel.Enabled = false;
                betterLabel.Enabled = false;
                worseLabel.Enabled = false;
            } 
            else
            {
                trackBar1.Enabled = true;
                numericUpDown1.Enabled = true;
                constantLabel.Enabled = true;
                betterLabel.Enabled = true;
                worseLabel.Enabled = true;
                if (primer_video.Predicted > 0)
                {
                    if (encoder.Out_w < (double)primer_video.Width * primer_video.Sar - 1 || primer_video.Sar != 1)
                        encoder.Vf_add("scale", encoder.Out_w.ToString(), encoder.Out_h.ToString(), primer_video.Width.ToString(), primer_video.Height.ToString());
                    else
                        encoder.Vf.RemoveAll(s => s.StartsWith("scale"));
                    Filter_items_update();
                }
            }
            if (totalBox.Focused)
            {
                if (totalBox.Text != "")
                {
                    encoder.Set_audio_codec(caComboBox.Text, Func.Chbox2int(chComboBox));
                    string[] kbps_ba_ch = encoder.Calc_kbps(Double.Parse(totalBox.Text), primer_video.EndTime - primer_video.StartTime, (int)primer_video.Fps);
                    bitrateBox.Text = kbps_ba_ch[0];
                    abitrateBox.Text = kbps_ba_ch[1];
                    if (Func.Chbox2int(chComboBox) > int.Parse(kbps_ba_ch[2]))
                        chComboBox.Text = "2 (mono)";
                }
                else
                    bitrateBox.Text = "";
            }
            
            if (bitrateBox.Text != "")
            {
                totalBox.Text = (encoder.Calc_total(int.Parse(bitrateBox.Text), int.Parse(abitrateBox.Text), primer_video.EndTime - primer_video.StartTime, (int)primer_video.Fps)).ToString();
                if (bitrateBox.Focused)
                    abitrateBox.Text = encoder.Calc_kbps(Double.Parse(totalBox.Text), primer_video.EndTime - primer_video.StartTime, (int)primer_video.Fps)[1];
            }
            else
                totalBox.Text = "";

            if (bitrateBox.Text != "")
            {
                encoder.V_kbps = int.Parse(bitrateBox.Text);
                encoder.Predicted = false;
            }
            else
                encoder.V_kbps = 0;
            Entry_update(9);
        }

        private void SsButton_Click(object sender, EventArgs e)
        {
            if (primer_video != null && encode != null)
            {
                encode.Clear_splits(primer_video.File);
                primer_video.StartTime = encoder.Playtime;
                UpdateBar();
            }
        }

        private void ToStripButton_Click(object sender, EventArgs e)
        {
            if (primer_video != null && encode != null)
            {
                encode.Clear_splits(primer_video.File);
                primer_video.EndTime = encoder.Playtime;
                if (primer_video.CreditsTime > primer_video.EndTime)
                    primer_video.CreditsTime = 0;
                UpdateBar();
            }
        }

        private void CreditsstartButton_Click(object sender, EventArgs e)
        {
            if (primer_video != null && encode != null)
            {
                if (encoder.Playtime < primer_video.EndTime)
                {
                    encode.Clear_splits(primer_video.File);
                    primer_video.CreditsTime = encoder.Playtime;
                    if (primer_video.CreditsEndTime > 0 && primer_video.CreditsEndTime < primer_video.CreditsTime)
                        primer_video.CreditsEndTime = 0;
                    Entry_update(3);
                }
                UpdateBar();
                creditsendButton.Enabled = primer_video.CreditsTime > 0;
            }
        }

        private void CreditsendButton_Click(object sender, EventArgs e)
        {
            if (primer_video != null && encode != null)
            {
                if (encoder.Playtime < primer_video.EndTime && encoder.Playtime > primer_video.CreditsTime)
                {
                    encode.Clear_splits(primer_video.File);
                    primer_video.CreditsEndTime = encoder.Playtime;
                    Entry_update(3);
                }
                UpdateBar();
            }
        }

        private void CvComboBox_DropDownClosed(object sender, EventArgs e)
        {
            encoder.Set_video_codec(cvComboBox.Text);
            if (listBox1.Items.Count > 0)
            {
                Entry entry = listBox1.SelectedIndex > -1 ? (Entry)listBox1.SelectedItems[0] : (Entry)listBox1.Items[0];
                if (entry.Param == "")
                {
                    Func.Update_combo(bitsComboBox, encoder.Bit_depth, !encoder.Cv.Contains("264"));
                    if (primer_video != null)
                        paramsBox.Text = encoder.Params_replace((int)primer_video.Fps);
                }
                else
                {
                    Func.Update_combo(bitsComboBox, encoder.Bit_depth, !encoder.Cv.Contains("264"), entry.Bits);
                    paramsBox.Text = entry.Param;
                }
            }
            else
            {
                Func.Update_combo(bitsComboBox, encoder.Bit_depth, !encoder.Cv.Contains("264"));
                paramsBox.Text = "";
            }
            trackBar1.Maximum = encoder.Max_crf;
            numericUpDown1.Value = encoder.Crf;
            Func.Update_combo(speedComboBox, encoder.Presets, false);
            Dialogo = false;
            gsgroupBox.Enabled = encoder.Gs > 0;
            gsUpDown.Maximum = encoder.Gs;
            pOIaddroiToolStripMenuItem.Enabled = encoder.Cv.StartsWith("libx");
            workersUpDown.Maximum = encoder.Cv.Contains("nvenc") ? 2 : encoder.Cores;
            workersUpDown.Value = workersUpDown.Maximum > 2 ? (workersBox.Checked ? (workersUpDown.Value <= workersUpDown.Maximum ? workersUpDown.Value : workersUpDown.Maximum) : 2) : 1;
            encoder.Predicted = false;
            Entry_update(4);
        }

        private void FormatComboBox_DropDownClosed(object sender, EventArgs e)
        {
            encoder.Format(formatComboBox.Text);
            Func.Update_combo(cvComboBox, encoder.V_codecs, true);
            Func.Update_combo(caComboBox, encoder.A_codecs, true);
            caComboBox.Enabled = chComboBox.Enabled;
            Dialogo = false;
        }

        private void CaComboBox_DropDownClosed(object sender, EventArgs e)
        {
            encoder.Set_audio_codec(caComboBox.Text, primer_video == null ? 8 : primer_video.Channels);
            Func.Update_combo(chComboBox, encoder.Channels, true);
            trackBar2.Minimum = encoder.A_min;
            Abitrate_update(caComboBox.Focused || abitrateBox.Text.Length == 0);
            Dialogo = false;
        }

        private void TrackBar2_Scroll(object sender, EventArgs e)
        {
            if (trackBar2.Focused)
            {
                abitrateBox.Text = trackBar2.Value.ToString();
                encoder.Ba = trackBar2.Value;
            }
        }

        private void AbitrateBox_TextChanged(object sender, EventArgs e)
        {
            if (abitrateBox.Text != "")
            {
                int value = int.Parse(abitrateBox.Text);
                if (value > trackBar2.Maximum)
                {
                    value = trackBar2.Maximum;
                    abitrateBox.Text = value.ToString();
                }
                if (value < trackBar2.Minimum)
                    value = trackBar2.Minimum;
                trackBar2.Value = value;

                if ((abitrateBox.Focused || trackBar2.Focused) && !trackBar1.Enabled)
                    totalBox.Text = (encoder.Calc_total(int.Parse(bitrateBox.Text), int.Parse(abitrateBox.Text), primer_video.EndTime - primer_video.StartTime, (int)primer_video.Fps)).ToString();
                encoder.Ba = trackBar2.Value;
                Entry_update(8);
                Entry.Save(listBox1);
            }
        }

        private void AbitrateBox_Leave(object sender, EventArgs e)
        {
            abitrateBox.Text = trackBar2.Value.ToString();
            if (bitrateBox.Text != "")
                totalBox.Text = (encoder.Calc_total(int.Parse(bitrateBox.Text), int.Parse(abitrateBox.Text), primer_video.EndTime - primer_video.StartTime, (int)primer_video.Fps)).ToString();
        }

        private void ChComboBox_DropDownClosed(object sender, EventArgs e)
        {
            Abitrate_update(chComboBox.Focused || abitrateBox.Text.Length == 0);
            if (primer_video != null)
            {
                encoder.Af_add("sofalizer", primer_video.Channels.ToString());
                downmixToolStripMenuItem.Enabled = primer_video.Channels > 2;
            }
            Filter_items_update();
            Dialogo = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (primer_video != null)
                primer_video.Clear_tmp();
            if (encodestopButton.Enabled && encode.Chunks != null)
            {
                e.Cancel = true;
                Dialogo = true;
                if (MessageBox.Show("An encode is currently running.\n\nDo you want to stop it and exit?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    encodestopButton.Enabled = false;
                    encodestartButton.Enabled = true;
                    encode.Set_state(true);
                    Thread thread = new Thread(() => Exit());
                    thread.Start();
                }
                Dialogo = false;
            }
            else if (mpv_cmd != null)
            {
                encoder.Save_settings(formatComboBox, cvComboBox, speedComboBox, resComboBox, hdrComboBox, bitsComboBox, numericUpDown1, caComboBox, chComboBox, abitrateBox, folderBrowserDialog1.SelectedPath, gscheckBox);
            }
        }

        private void Exit()
        {
            int running = encode.Chunks.Length;
            while (running > 0)
            {
                running = encode.Chunks.Length;
                foreach (Segment chunk in encode.Chunks)
                {
                    if (!chunk.Encoding)
                        running--;
                }
                Thread.Sleep(1000);
            }
            Environment.Exit(0);
        }

        private void FormatComboBox_DropDown(object sender, EventArgs e)
        {
            Dialogo = true;
        }

        private void SpeedComboBox_DropDownClosed(object sender, EventArgs e)
        {
            Dialogo = false;
            if (encoder != null)
            {
                encoder.Speed = speedComboBox.Text.Split(' ')[0];

                if (hdrComboBox.Text == "Yes" && hdrComboBox.Enabled)
                    bitsComboBox.Text = int.Parse(bitsComboBox.Text) < 10 ? "10" : bitsComboBox.Text;
                encoder.Hdr = hdrComboBox.Text == "Yes" && hdrComboBox.Enabled;
                if (primer_video != null)
                    encoder.Vf_update("tonemap", hdrComboBox.Text, hdrComboBox.Enabled.ToString());
            }
            Filter_items_update();
        }

        private void BitsComboBox_DropDownClosed(object sender, EventArgs e)
        {
            Dialogo = false;
            if (encoder != null)
            {
                if (int.Parse(bitsComboBox.Text) < 10 && hdrComboBox.Enabled)
                    hdrComboBox.Text = "No";
                encoder.Bits = int.Parse(bitsComboBox.Text);
                if (primer_video != null)
                    encoder.Vf_update("tonemap", hdrComboBox.Text, hdrComboBox.Enabled.ToString());
            }
            Filter_items_update();
            Entry_update(5);
        }

        private void EncodestartButton_Click(object sender, EventArgs e)
        {
            if (primer_video == null)
                return;
            Update_Video_txt();
            encoder.Save_settings(formatComboBox, cvComboBox, speedComboBox, resComboBox, hdrComboBox, bitsComboBox, numericUpDown1, caComboBox, chComboBox, abitrateBox, folderBrowserDialog1.SelectedPath, gscheckBox);
            encodestopButton.Enabled = true;
            encodestartButton.Enabled = false;
            if (encode != null)
                encode.Set_state();
            Filter_remove();
        }

        private void EncodestopButton_Click(object sender, EventArgs e)
        {
            encodestopButton.Enabled = false;
            encodestartButton.Enabled = true;
            if (encode != null)
                encode.Set_state(true);
            heat = Func.Heat(0);
            workersgroupBox.Refresh();
        }

        private void Abitrate_update(bool calc)
        {
            encoder.Ch = Func.Chbox2int(chComboBox).ToString();
            int chs = int.Parse(encoder.Ch);
            chs = chs > 2 ? chs - 2 : chs;
            trackBar2.Maximum = chs * encoder.A_max / 2;
            if (!calc)
                return;
            if (totalBox.Text.Length > 0 && primer_video != null)
                abitrateBox.Text = encoder.Calc_kbps(Double.Parse(totalBox.Text), primer_video.EndTime - primer_video.StartTime, (int)primer_video.Fps)[1];
            else if (totalBox.Text == "")
            {
                string bas = (chs * encoder.A_kbps / 2).ToString();
                if (settings != null && settings.Audio_br != null)
                {
                    string sch = settings.Channels.Split(' ')[0];
                    sch = sch == "Default" ? 2.ToString() : sch;
                    abitrateBox.Text = chs == int.Parse(sch) ? settings.Audio_br : bas;
                }
                else
                    abitrateBox.Text = bas;
            }
        }

        private void InfoTimer_Tick(object sender, EventArgs e)
        {
            if (encode == null)
                encode = new Encode();
            if (listBox1.Items.Count == 0 || primer_video == null)
                return;
            Entry entry;
            if (listBox1.SelectedItems.Count > 0)
                entry = (Entry)listBox1.SelectedItems[0];
            else
            {
                int index = Entry.Index(primer_video.File, listBox1);
                entry = index > -1 ? (Entry)listBox1.Items[index] : (Entry)listBox1.Items[0];
            }

            if (entry.File != primer_video.File)
            {
                Mpv_load_first();
                return;
            }
            if (infoTimer.Interval == 221)
            {
                if (mediainfoLabel.Text.Contains("..."))
                {
                    if (primer_video.Fps > 0)
                        mediainfoLabel.Text = mediainfoLabel.Text.Replace("...", "~" + primer_video.Fps.ToString());
                    else if (primer_video.Fps < 0)
                        mediainfoLabel.Text = mediainfoLabel.Text.Replace(", ... fps", "");
                }
                else if (primer_video != null)
                {
                    fpsComboBox.Items.Clear();
                    fpsComboBox.Items.Add("Same");
                    fpsComboBox.Items.Add(Math.Round(primer_video.Fps / 2, 1) + " (1/2)");
                    fpsComboBox.Items.Add(Math.Round(primer_video.Fps / 3, 1) + " (1/3)");
                    fpsComboBox.Items.Add(Math.Round(primer_video.Fps / 4, 1) + " (1/4)");
                    infoTimer.Interval = 250;
                }
            }
            if (infoTimer.Interval == 250 && !encodestopButton.Enabled && scaleBox.Checked && bitrateBox.Text != "" && !primer_video.Gs_thread && !encoder.Predicted)
            {
                if (primer_video.Busy || totalBox.Focused || bitrateBox.Focused)
                    return;
                primer_video.Predict(statusLabel, encoder, vfListBox);
            }
            else if (infoTimer.Interval == 250 && !encode.Can_run && !gscheckBox.Checked && gscheckBox.Enabled && primer_video.Grain_level == -1 && !primer_video.Busy && !primer_video.Gs_thread)
            {
                primer_video.Grain_detect(gsUpDown, statusLabel, encoder.Gs, mediainfoLabel, encoder.Vf);
                return;
            }
            else if (encodestopButton.Enabled && infoTimer.Interval == 250 && !primer_video.Busy && !encode.Can_run && !encode.Failed && !primer_video.Gs_thread && entry.Param != "" && (encoder.Vf.Count == 0 || !encoder.Vf[0].Contains("crop=D")))
            {
                encoder.Params = paramsBox.Text;
                paramsBox.Text = encoder.Params_replace((int)primer_video.Fps);
                encode = new Encode
                {
                    Split_min_time = 4 + (int)((primer_video.EndTime - primer_video.StartTime) / 1400.0),
                    Can_run = true,
                    Job = encoder.Job,
                    Extension = formatComboBox.Text,
                    Workers = (int)workersUpDown.Value,
                    Param = encoder.Build_vstr()
                };
                encode.Set_fps_filter(encoder.Vf);
                double delay = 0;
                if (primer_video.Channels > 0 && checkedListBox1.CheckedItems.Count > 0)
                {
                    encode.A_Param = encoder.Build_astr(checkedListBox1.CheckedIndices[0]);
                    encode.A_Job = encoder.A_Job;
                    delay = primer_video.Tracks_delay[checkedListBox1.CheckedIndices[0]];
                }
                double to = primer_video.EndTime != primer_video.Duration ? primer_video.EndTime : primer_video.Duration + 1;
                encode.Start_encode(folderBrowserDialog1.SelectedPath, primer_video.File, primer_video.StartTime, to, primer_video.CreditsTime, primer_video.CreditsEndTime, primer_video.Timebase, primer_video.Kf_interval, (primer_video.Width <= 1920 || primer_video.Kf_fixed), primer_video.Channels > 0, delay, encoder.V_kbps, encoder.Out_spd);
                listBox1.Refresh();
            }
            else if (encodestopButton.Enabled && encode.Finished)
            {
                if (encodelistButton.Checked && Entry.Index(primer_video.File, listBox1) < listBox1.Items.Count - 1)
                {
                    listBox1.SelectedIndex = -1;
                    listBox1.SelectedIndex = Entry.Index(primer_video.File, listBox1) + 1;
                    encode = new Encode();
                    Mpv_load_first();
                }
                else
                {
                    encodestopButton.Enabled = false;
                    encodestartButton.Enabled = true;
                    heat = Func.Heat(0);
                    workersgroupBox.Refresh();
                    Detener();
                    Mpv2_load(encode.Dir + Path.GetFileNameWithoutExtension(encode.Name) + "_Av1ador." + encode.Extension, "set pause yes");
                    mpv_cmd.WriteLine("set pause yes;seek " + primer_video.StartTime.ToString() + " absolute+exact");
                    Update_current_time(primer_video.StartTime);
                }
                if (!workersUpDown.Enabled && workersUpDown.Value > 1)
                    workersUpDown.Value = 2;
            }
            else if (!encodestopButton.Enabled)
            {
                encode.Can_run = false;
                if (statusLabel.Text.Contains("Encoding"))
                    listBox1.Refresh();
                if (!statusLabel.Text.Contains("grain"))
                    statusLabel.Text = "";
                estimatedLabel.Text = "";
            }
            else if (encode.Failed)
            {
                encodestopButton.Enabled = false;
                encodestartButton.Enabled = true;
            }
            if (encodestopButton.Enabled && !statusLabel.Text.Contains("grain"))
            {
                usage = (int)cpu.NextValue();
                if (WindowState != FormWindowState.Minimized)
                {
                    heat = Func.Heat((int)usage);
                    workersgroupBox.Refresh();
                }
                if (!workersBox.Checked && workersUpDown.Maximum > 1 && encode.Counter == 0)
                {
                    if (disk != null && usage < 90 && disk.NextValue() < 70 && ram.NextValue() > primer_video.Height)
                        underload++;
                    else
                        underload = 0;
                    if (underload > 8)
                    {
                        underload = -1;
                        if (workersUpDown.Value + 1 <= workersUpDown.Maximum && encode.Segments_left > 0 && workersUpDown.Value < Environment.ProcessorCount * 2 / 3)
                            workersUpDown.Value++;
                    }
                }
                double progress = encode.Progress;
                statusLabel.Text = string.Join(", ", encode.Status.ToArray()).Replace("Encoding video...", "Encoding video... " + (progress - 1 < 0 ? 0 : progress - 1) + "%");
                statusLabel.Text = statusLabel.Text.Replace("...,", ",");
                if (statusLabel.Text.Contains("Encoding video"))
                    statusLabel.Text += ", Fps: " + encode.Speed;
                double size = encode.Estimated;
                string t = encode.Remaining.ToString().Split('.')[0];
                if (t.Length < 4)
                    t += " day" + (t == "1" ? "" : "s");
                estimatedLabel.Text = size > 0 ? "Estimated size: " + Func.Size_unit(size) + ", Remaining time: " + t : "";
            }
            Text = statusLabel.Text.Contains("%") ? statusLabel.Text.Split('%')[0].Split(' ').Last() + "%" + " - " + title : title;
            UpdateBar();
        }

        private void GscheckBox_CheckedChanged(object sender, EventArgs e)
        {
            gsUpDown.Enabled = gscheckBox.Checked;
            gsgroupBox.Text = gscheckBox.Checked ? "Grain synthesis" : "Grain synthesis (auto)";
            encoder.Save_settings(formatComboBox, cvComboBox, speedComboBox, resComboBox, hdrComboBox, bitsComboBox, numericUpDown1, caComboBox, chComboBox, abitrateBox, folderBrowserDialog1.SelectedPath, gscheckBox);
        }

        private void TogglefButton_Click(object sender, EventArgs e)
        {
            if (togglefButton.Text == "Video")
            {
                if (vfListBox.SelectedIndex > -1 || encoder.Vf.Contains(clTextBox.Text))
                    clTextBox.Text = "";
                else if (vfListBox.SelectedIndex > -1 && clTextBox.Text != vfListBox.SelectedItem.ToString())
                    vfListBox.SelectedIndex = -1;
            }
            else
            {
                if (afListBox.SelectedIndex > -1)
                {
                    if (afListBox.SelectedIndex > -1 || encoder.Af.Contains(clTextBox.Text))
                        clTextBox.Text = "";
                    else if (afListBox.SelectedIndex > -1 && clTextBox.Text != afListBox.SelectedItem.ToString())
                        afListBox.SelectedIndex = -1;
                }
            }
            Show_filter(togglefButton.Text != "Video");
        }

        private void Show_filter(bool left)
        {
            if (!left)
            {
                togglefButton.Text = "Audio";
                togglefButton.Image = Resources.Sound;
                filteraddDropDownButton.Visible = false;
                filteraddaDropDownButton.Visible = true;
                vfListBox.Visible = false;
                afListBox.Visible = true;
                tableLayoutPanel18.ColumnStyles[0].Width = 0;
                tableLayoutPanel18.ColumnStyles[1].Width = 100;
            }
            else
            {
                togglefButton.Text = "Video";
                togglefButton.Image = Resources.Image;
                filteraddaDropDownButton.Visible = false;
                filteraddDropDownButton.Visible = true;
                afListBox.Visible = false;
                vfListBox.Visible = true;
                tableLayoutPanel18.ColumnStyles[0].Width = 100;
                tableLayoutPanel18.ColumnStyles[1].Width = 0;
            }
        }

        private void TabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (tabControl1.SelectedTab.Text == "Filters")
                Filter_items_update();
        }

        private void Filter_items_update(bool g = false)
        {
            vfListBox.Items.Clear();
            afListBox.Items.Clear();
            foreach (string vf in encoder.Vf)
                vfListBox.Items.Add(vf);
            foreach (string af in encoder.Af)
                afListBox.Items.Add(af);
            if (primer_video != null)
            {
                if (g)
                {
                    if (statusLabel.Text.Contains("grain"))
                        statusLabel.Text = "";
                    primer_video.Grain_level = -1;
                }
                if (tabControl1.SelectedTab.Text == "Filters")
                {
                    Entry_update(1);
                    Entry.Save(listBox1);
                }
            }
        }

        private void FilterremoveButton_Click(object sender, EventArgs e)
        {
            if (togglefButton.Text == "Video")
            {
                for (int i = vfListBox.SelectedIndices.Count - 1; i >= 0; i--)
                {
                    string s = vfListBox.SelectedItems[i].ToString();
                    if (Func.Preview(clTextBox.Text))
                        Filter_remove();
                    if (clTextBox.Text == s)
                        clTextBox.Text = "";
                    encoder.Vf.RemoveAt(vfListBox.SelectedIndices[i]);
                    if (s.Contains("Anime4K") || s.Contains("FSRCNNX"))
                        Get_res();
                    if (s.StartsWith("setpts"))
                        encoder.Af.RemoveAll(ss => ss.StartsWith("atempo="));
                }
            }
            else
            {
                for (int i = afListBox.SelectedIndices.Count - 1; i >= 0; i--)
                {
                    if (clTextBox.Text == afListBox.SelectedItems[i].ToString())
                        clTextBox.Text = "";
                    encoder.Af.RemoveAt(afListBox.SelectedIndices[i]);
                }
            }
            Filter_items_update();
        }

        private void AfListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (togglefButton.Text == "Video")
            {
                if (vfListBox.SelectedIndex > -1)
                    clTextBox.Text = vfListBox.SelectedItem.ToString();
                Filter_preview(clTextBox.Text);
            }
            else
            {
                if (afListBox.SelectedIndex > -1)
                    clTextBox.Text = afListBox.SelectedItem.ToString();
            }
        }

        private void ClTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar == ((char)Keys.Enter))
            {
                e.Handled = true;
                if (togglefButton.Text == "Video")
                {
                    if (vfListBox.SelectedIndex > -1)
                        encoder.Vf[vfListBox.SelectedIndex] = clTextBox.Text;
                    else if (clTextBox.Text != "")
                        encoder.Vf.Add(clTextBox.Text);
                    encoder.Vf_update(clTextBox.Text);
                    Filter_items_update(clTextBox.Text.Contains("crop"));
                    Filter_preview(clTextBox.Text);
                    clTextBox.Text = "";
                }
                else
                {
                    if (afListBox.SelectedIndex > -1)
                        encoder.Af[afListBox.SelectedIndex] = clTextBox.Text;
                    else if (clTextBox.Text != "")
                        encoder.Af.Add(clTextBox.Text);
                    Filter_items_update();
                    clTextBox.Text = "";
                }
            }
        }

        private void Filter_preview(string vf)
        {
            vf = encoder.Filter_convert(vf);
            if (Func.Preview(vf))
            {
                string osd = "Filter preview: " + vf.Split('=')[0];
                mpv_cmd.WriteLine("{ \"command\": [\"vf\", \"set\", \"\"] }");
                mpv_cmd.WriteLine("{ \"command\": [\"vf\", \"add\", \"" + vf.Replace("delogo=","delogo=show=1:") + "\"] }");
                mpv_cmd.WriteLine("{ \"command\": [\"show-text\", \"" + osd + "\"] }");
            }
            else
                Filter_remove();
        }

        private void Filter_remove()
        {
            mpv_cmd.WriteLine("{ \"command\": [\"vf\", \"set\", \"\"] }");
            mpv_cmd.WriteLine("{ \"command\": [\"show-text\", \"\"] }");
        }

        private void FilternewButton_Click(object sender, EventArgs e)
        {
            if (togglefButton.Text == "Video")
            {
                if (vfListBox.SelectedIndex > -1)
                    vfListBox.SelectedIndex = -1;
            }
            else
            {
                if (afListBox.SelectedIndex > -1)
                    afListBox.SelectedIndex = -1;
            }
            clTextBox.Text = "";
            clTextBox.Focus();
        }

        private void FpsComboBox_DropDownClosed(object sender, EventArgs e)
        {
            Dialogo = false;
            encoder.Vf_add("fps", fpsComboBox.Text);
            int.TryParse(fpsComboBox.Text.Split(' ')[0], out int ofps);
            encoder.Out_fps = 0;
            if (ofps > 0)
                encoder.Out_fps = int.Parse(fpsComboBox.Text.Split(' ')[0]);
            encoder.Predicted = false;
            Filter_items_update();
        }

        private void ResComboBox_Enter(object sender, EventArgs e)
        {
            prevheight = int.Parse(resComboBox.Text.Replace("p", ""));
        }

        private void ResComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (prevheight + "p" != resComboBox.Text)
                encoder.Vf.RemoveAll(s => s.StartsWith("scale"));
            ResComboBox_SelectedIndexChanged(sender, e);
            Entry_update(11);
            ActiveControl = null;
        }

        private void ResComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Dialogo = false;
            if (primer_video == null || encoder.Vf.FindIndex(s => s.StartsWith("scale")) != -1)
                return;
            double dn = (double)16 / (double)9;
            double h = Double.Parse(resComboBox.Text.Replace("p", "")) * (primer_video.Sar < 1 ? primer_video.Sar : 1.0);
            scale = (double)primer_video.Width * primer_video.Sar / (double)primer_video.Height > dn ? h * dn / (double)primer_video.Width : h / (double)primer_video.Height;
            double ow = ((double)primer_video.Width * (primer_video.Sar > 1 ? primer_video.Sar : 1.0) * scale);
            encoder.Out_w = (int)ow;
            encoder.Out_h = (int)(Math.Floor(((ow * (double)primer_video.Height / (double)primer_video.Width / primer_video.Sar) + (double)1) / (double)2) * 2);

            if (ow < (double)primer_video.Width * primer_video.Sar - 1 || primer_video.Sar != 1)
                encoder.Vf_add("scale", ow.ToString(), encoder.Out_h.ToString(), primer_video.Width.ToString(), primer_video.Height.ToString());
            
            if (segundo_video == null) {
                mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-x\", " + scale + "] }");
                mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-y\", " + scale + "] }");
            }
            if (primer_video != null)
            {
                encoder.Vf_update("tonemap", hdrComboBox.Text, hdrComboBox.Enabled.ToString());
                encoder.Vf_add("deinterlace", primer_video.Interlaced.ToString());
                encoder.Vf_add("autocolor", primer_video.Color_matrix);
            }
            Filter_items_update();
        }

        private void VolumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            encoder.Af_add("volume", "");
            Filter_items_update();
        }

        private void CropToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (primer_video != null)
                encoder.Vf_add("crop", primer_video.Width.ToString(), primer_video.Height.ToString());
            Filter_items_update(true);
        }

        private void DelogoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (primer_video != null)
                encoder.Vf_add("delogo", primer_video.Width.ToString(), primer_video.Height.ToString());
            Filter_items_update();
        }

        private void POIaddroiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            encoder.Vf_add("poi", "");
            Filter_items_update();
        }

        private void FilterupButton_Click(object sender, EventArgs e)
        {
            if (togglefButton.Text == "Video")
            {
                int index = vfListBox.SelectedIndex;
                if (index - 1 > -1)
                {
                    var item = vfListBox.SelectedItem;
                    encoder.Vf.RemoveAt(index);
                    encoder.Vf.Insert(index - 1, item.ToString());
                    Filter_items_update();
                    vfListBox.SelectedIndex = index - 1;
                }
            } 
            else
            {
                int index = afListBox.SelectedIndex;
                if (index - 1 > -1)
                {
                    var item = afListBox.SelectedItem;
                    encoder.Af.RemoveAt(index);
                    encoder.Af.Insert(index - 1, item.ToString());
                    Filter_items_update();
                    afListBox.SelectedIndex = index - 1;
                }
            }
        }

        private void WorkersUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (encode != null)
            {
                encode.Workers = (int)workersUpDown.Value;
                if (encode.Chunks != null)
                {
                    foreach (Segment segment in encode.Chunks)
                    {
                        if (encode.Running)
                            break;
                        if (segment.Encoding && encodestopButton.Enabled)
                        {
                            encode.Encoding();
                            break;
                        }
                    }
                }
            }
            encoder.Threads = (int)Math.Ceiling((double)encoder.Cores / (double)workersUpDown.Value);
        }

        private void DeinterlaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            encoder.Vf_add("deinterlace", "True");
            Filter_items_update();
        }

        private void NormalizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (primer_video != null)
                encoder.Af_add("normalize", primer_video.Channels.ToString());
            Filter_items_update();
        }

        private void DownmixToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (primer_video != null)
                encoder.Af_add("sofalizer", primer_video.Channels.ToString());
            Filter_items_update();
        }

        private void NoiseReductionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            encoder.Af_add("noisereduction");
            Filter_items_update();
        }

        private void ZoomButton_CheckStateChanged(object sender, EventArgs e)
        {
            if (primer_video == null)
                return;
            double zoom;
            if (zoomButton.Checked)
                scale *= 2.0;
            else
                scale /= 2.0;
            if (segundo_video != null)
            {
                zoom = Func.Scale(primer_video, segundo_video, scale, encoder.Vf.Count > 0 && Func.Find_w_h(encoder.Vf).Count() > 0 ? Double.Parse(Func.Find_w_h(encoder.Vf)[0]) : 0);
                mpv2_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-x\", " + scale + "] }");
                mpv2_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-y\", " + scale + "] }");
            }
            else
                zoom = scale;
            mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-x\", " + zoom + "] }");
            mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"video-scale-y\", " + zoom + "] }");
        }

        private void FilteraddDropDownButton_DropDownOpening(object sender, EventArgs e)
        {
            bool vulkan = false;
            bool opencl = !encoder.Libplacebo;
            bool tonemap = false;
            if (encoder.Vf.Count > 0)
            {
                foreach (var f in encoder.Vf)
                {
                    if (f.Contains("libplacebo"))
                        vulkan = true;
                    if (f.Contains("tonemap"))
                        tonemap = true;
                    if (f.Contains("opencl"))
                        opencl = true;
                }
            }
            denoiseToolStripMenuItem.Enabled = !vulkan;
            openclToolStripMenuItem.Enabled = !vulkan && !tonemap;
            vulkanToolStripMenuItem.Enabled = !opencl && !tonemap;
            upscaleToolStripMenuItem.Enabled = !vulkan;
        }

        private void SyncButton_CheckStateChanged(object sender, EventArgs e)
        {
            playButton.Enabled = !syncButton.Checked;
            nextframeButton.BackColor = syncButton.Checked ? Color.LightGreen : Color.Transparent;
            prevframeButton.BackColor = syncButton.Checked ? Color.LightGreen : Color.Transparent;
            if (syncButton.Checked)
                mpv_cmd.WriteLine("{ \"command\": [\"vf\", \"add\", \"curves=g=0/0.2 1/1\"] }");
            else
                mpv_cmd.WriteLine("{ \"command\": [\"vf\", \"remove\", \"curves=g=0/0.2 1/1\"] }");
        }

        private void PlayButton_VisibleChanged(object sender, EventArgs e)
        {
            syncButton.Enabled = playButton.Visible && segundo_video != null && can_sync;
            syncButton.Checked = syncButton.Checked && playButton.Visible && can_sync;
        }

        private void ParamsBox_TextChanged(object sender, EventArgs e)
        {
            paramsBox.Text = paramsBox.Text.Replace(Environment.NewLine, "");
            if (paramsBox.Text != "")
            {
                Entry_update(6);
                Entry.Save(listBox1);
            }
        }

        private void CheckedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (i != e.Index)
                    checkedListBox1.SetItemChecked(i, false);
                else if (e.NewValue == CheckState.Checked && !aset.IsBusy)
                    aset.RunWorkerAsync(e.Index);
            }
            mpv_cmd.WriteLine("{ \"command\": [\"set_property\", \"aid\", " + (e.Index + 1) + "] }");
            Entry_update(10, e.NewValue == CheckState.Checked ? e.Index : -1);
        }

        private void FilterdocButton_Click(object sender, EventArgs e)
        {
            Process.Start("https://ffmpeg.org/ffmpeg-filters.html");
        }

        private void RemoveBlackBarsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (primer_video != null && encoder != null)
            {
                encoder.Vf_add("crop", "Detecting", ".", ".", ".");
                Filter_items_update();
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, ee) =>
                {
                    primer_video.Blackbars(encoder);
                    while (primer_video.Letterbox.Width == 0)
                        Thread.Sleep(30);
                };
                bw.RunWorkerCompleted += (ss, ee) =>
                {
                    encoder.Vf.RemoveAll(s => s.StartsWith("scale"));
                    ResComboBox_SelectedIndexChanged(sender, e);
                    Filter_items_update();
                    Filter_preview(encoder.Vf[0]);
                };
                bw.RunWorkerAsync();
            }
        }

        private void MouseTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                GetCursorPos(ref mouse_pos);
                if (!Dialogo && click_in && mouse1 && Sobrelinea())
                {
                    moviendo_divisor = true;
                    int distancia = mouse_pos.X - leftPanel.PointToScreen(Point.Empty).X;
                    leftPanel.Width = distancia > 0 ? distancia : 0;
                    UpdateLayout();
                }
            }
            catch { }

            try
            {
                if (GetAsyncKeyState(0x01) == 0)
                {
                    mouse1 = false;
                    moviendo_divisor = false;
                    panTimer.Enabled = false;
                    mouse_pos_antes.X = 0;
                    mouse_pos_antes.Y = 0;
                }
                else
                {
                    int mpv_id = 0, mpv2_id = 0, new_id = 0;
                    GetWindowThreadProcessId(mpv1p.MainWindowHandle, ref mpv_id);
                    if (IsLoaded_mpv2())
                        GetWindowThreadProcessId(mpv2p.MainWindowHandle, ref mpv2_id);
                    GetWindowThreadProcessId((IntPtr)GetForegroundWindow(), ref new_id);
                    focus_id = new_id != 0 ? new_id : focus_id;
                    if (focus_id > 0 || new_id == 0)
                    {
                        click_in = mpv_id == focus_id || mpv2_id == focus_id || Process.GetProcessById(focus_id).ProcessName.ToString() == GetType().Assembly.GetName().Name;
                        if (!mouse1 && (click_in || new_id == 0))
                        {
                            click_pos = mouse_pos;
                            int izq = tableLayoutPanel1.PointToScreen(Point.Empty).X;
                            int bordes = (izq - Left) * 2;
                            if (!Dialogo && !Sobrelinea() && click_pos.X > leftPanel.PointToScreen(Point.Empty).X && click_pos.X < (izq + Width - bordes) && click_pos.Y > leftPanel.PointToScreen(Point.Empty).Y && click_pos.Y < (leftPanel.PointToScreen(Point.Empty).Y + splitterPanel.Height))
                                panTimer.Enabled = true;
                        }
                    }
                    mouse1 = true;
                }
            }
            catch { }

            int hover = listBox1.IndexFromPoint(mouse_pos.X - listBox1.PointToScreen(Point.Empty).X, mouse_pos.Y - listBox1.PointToScreen(Point.Empty).Y);
            if (hover_before != hover)
            {
                hover_before = hover;
                if (hover_before > -1)
                {
                    toolTip1.Active = false;
                    toolTip1.SetToolTip(listBox1, ((Entry)listBox1.Items[hover]).File);
                    toolTip1.Active = true;
                }
                else
                    toolTip1.Active = false;
            }
        }

        private void WorkersgroupBox_Paint(object sender, PaintEventArgs e)
        {
            var gfx = e.Graphics;
            Pen pen = new Pen(heat, 2);
            int[] pt = new int[4] { 4, 25, 72, 73 };
            int ext = 30;
            if (workersBox.Checked || e.ClipRectangle.Width < 91)
            {
                ext = 0;
                pt = new int[4] { 3, 21, 62, 80 };
            }
            gfx.DrawLine(pen, 8 - Func.Rule(0, pt[0], usage, 8), 6, 8, 6);
            gfx.DrawLine(pen, 1, 6, 1, Func.Rule(pt[0], pt[1], usage, e.ClipRectangle.Height - 8) + 6);
            gfx.DrawLine(pen, Func.Rule(pt[1], pt[2], usage, e.ClipRectangle.Width), e.ClipRectangle.Height - 2, 0, e.ClipRectangle.Height - 2);
            gfx.DrawLine(pen, e.ClipRectangle.Width - 1, e.ClipRectangle.Height - Func.Rule(pt[2], pt[3], usage, e.ClipRectangle.Height - 8) - 2, e.ClipRectangle.Width - 1, e.ClipRectangle.Height - 2);
            int line = 50 + ext;
            gfx.DrawLine(pen, e.ClipRectangle.Width - Func.Rule(pt[3], 100, usage, e.ClipRectangle.Width - line), 6, e.ClipRectangle.Width, 6);
        }

        private void VfListBox_DragOver(object sender, DragEventArgs e)
        {
            tabControl1.SelectedIndex = 0;
        }

        private void XToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int uh = int.Parse(resComboBox.Items[0].ToString().Replace("p", ""));
            resComboBox.Items.Clear();
            if ((sender as ToolStripMenuItem).Text.Contains("x2"))
            {
                uh *= 2;
                encoder.Vf_add((sender as ToolStripMenuItem).Text.Contains("FSRCNNX") ? "fsrcnnx" : "anime4k", "2", (bitsComboBox.Text == "10").ToString());
            }
            else
            {
                uh = uh * 3 / 2;
                encoder.Vf_add("anime4k", "1.5", (bitsComboBox.Text == "10").ToString());
            }
            for (int i = 0; i < encoder.Resos.Length; i++)
            {
                if (int.Parse(encoder.Resos[i].Replace("p", "")) <= uh)
                    resComboBox.Items.Add(encoder.Resos[i]);
            }
            if (uh > int.Parse(resComboBox.Items[0].ToString().Replace("p", "")))
                resComboBox.Items.Insert(0, uh + "p");
            resComboBox.SelectedIndex = 0;
            Filter_items_update();
        }

        private void CopytoolStripButton_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count > 0 && (encoder.Vf.Count > 0 || encoder.Af.Count > 0))
                clipboard = new string[] { (listBox1.SelectedItems[0] as Entry).Vf, (listBox1.SelectedItems[0] as Entry).Af };
            pastetoolStripButton.Enabled = true;
        }

        private void PastetoolStripButton_Click(object sender, EventArgs e)
        {
            if ((clipboard[0] != "" || clipboard[1] != "") && listBox1.SelectedItems.Count > 0)
            {
                foreach (Entry entry in listBox1.SelectedItems)
                {
                    entry.Vf = clipboard[0];
                    entry.Af = clipboard[1];
                }
                encoder.Vf = Entry.Filter2List((listBox1.SelectedItems[0] as Entry).Vf);
                encoder.Af = Entry.Filter2List((listBox1.SelectedItems[0] as Entry).Af);
                Entry.Save(listBox1, true);
            }
        }

        private void FrameInterpolationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (primer_video == null)
                return;
            encoder.Vf_add("interpolation", primer_video.Fps.ToString());
            Filter_items_update();
        }

        private void MultiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            encoder.Vf_add((sender as ToolStripMenuItem).Text);
            Filter_items_update();
        }

        private void ScaleBox_CheckedChanged(object sender, EventArgs e)
        {
            if (bitrateBox.Text.Length > 0)
                resComboBox.Enabled = !scaleBox.Checked;
        }

        private void WorkersBox_CheckedChanged(object sender, EventArgs e)
        {
            workersgroupBox.Text = workersBox.Checked ? "Workers" : "Workers (auto)";
            workersUpDown.Enabled = workersBox.Checked;
        }

        private void FilterdownButton_Click(object sender, EventArgs e)
        {
            if (togglefButton.Text == "Video")
            {
                int index = vfListBox.SelectedIndex;
                if (index > -1 && index + 1 < vfListBox.Items.Count)
                {
                    var item = vfListBox.SelectedItem;
                    encoder.Vf.RemoveAt(index);
                    encoder.Vf.Insert(index + 1, item.ToString());
                    Filter_items_update();
                    vfListBox.SelectedIndex = index + 1;
                }
            }
            else
            {
                int index = afListBox.SelectedIndex;
                if (index > -1 && index + 1 < afListBox.Items.Count)
                {
                    var item = afListBox.SelectedItem;
                    encoder.Af.RemoveAt(index);
                    encoder.Af.Insert(index + 1, item.ToString());
                    Filter_items_update();
                    afListBox.SelectedIndex = index + 1;
                }
            }
        }

        private void GsUpDown_ValueChanged(object sender, EventArgs e)
        {
            encoder.Gs_level = (int)gsUpDown.Value;
            paramsBox.Text = Func.Replace_gs(paramsBox.Text, (int)gsUpDown.Value);
            Entry_update(2);
        }

        private void UpdateBar()
        {
            if (WindowState == FormWindowState.Minimized)
                return;
            if (encode.Splits != null && encode.File == primer_video.File)
                picBoxBarra.Image = primer_video.Bar(picBoxBarra.Width, picBoxBarra.Height, encoder.Playtime, encode);
            else
                picBoxBarra.Image = primer_video.Bar(picBoxBarra.Width, picBoxBarra.Height, encoder.Playtime);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized)
                UpdateLayout();
        }

        private void SplitterPanel_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void SplitterPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = (string[])(e.Data.GetData(DataFormats.FileDrop));
                if (formatos.Match(file[0]).Success && File.Exists(file[0]))
                    Mpv2_load(file[0], "", encoder != null ? encoder.Playtime : 0);
            }
        }

        private void LeftPanel_SizeChanged(object sender, EventArgs e)
        {
            splitterPanel.Width = leftPanel.Width + 5;
        }

        private void OutfolderButton_Click(object sender, EventArgs e)
        {
            Dialogo = true;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                origfolderButton.Checked = false;
                outfolderButton.Checked = true;
                if (encode != null)
                    encode.Dir = folderBrowserDialog1.SelectedPath + "\\";
            }
            else
            {
                outfolderButton.Checked = false;
            }
            Dialogo = false;
        }

        private void OrigfolderButton_Click(object sender, EventArgs e)
        {
            outfolderButton.Checked = false;
            origfolderButton.Checked = true;
            folderBrowserDialog1.SelectedPath = "";
        }

        private void SplitContainer2_SplitterMoved(object sender, SplitterEventArgs e)
        {
            UpdateLayout(false);
        }

        private void PanTimer_Tick(object sender, EventArgs e)
        {
            if (moviendo_divisor || Dialogo || (encode != null && encode.Failed))
                panTimer.Enabled = false;
            else
            {
                if (mouse_pos_antes.X > 0 && mouse_pos_antes.Y > 0)
                {
                    panx += ((Double)mouse_pos.X - (Double)mouse_pos_antes.X) / (Double)Screen.FromControl(this).Bounds.Width * 1.5;
                    pany += ((Double)mouse_pos.Y - (Double)mouse_pos_antes.Y) / (Double)Screen.FromControl(this).Bounds.Height * 1.5;
                    if (panx != 0 || pany != 0)
                    {
                        mpv_cmd.WriteLine("set video-pan-x " + panx + ";set video-pan-y " + pany);
                        if (IsLoaded_mpv2())
                            mpv2_cmd.WriteLine("set video-pan-x " + (panx * panx_ratio) + ";set video-pan-y " + (pany * pany_ratio));
                    }
                }
                mouse_pos_antes = mouse_pos;
            }
        }

        private void Entry_update(int field, int track = -1)
        {
            if (primer_video != null)
                Entry.Update(field, primer_video.File, listBox1, vfListBox, afListBox, gsUpDown.Value.ToString(), primer_video.CreditsTime, primer_video.CreditsEndTime, cvComboBox.SelectedIndex, bitsComboBox.Text, paramsBox.Text, (int)numericUpDown1.Value, int.Parse(abitrateBox.Text), bitrateBox.Text, track, resComboBox.Text);
        }
    }
}
