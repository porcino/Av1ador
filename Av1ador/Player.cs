using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Av1ador
{
    internal class Player
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, ref int ProcessID);

        private readonly string mpv_args = " --priority=abovenormal --no-resume-playback --pause --cache=yes --hr-seek=always --hr-seek-demuxer-offset=5 -no-osc --osd-level=0 --no-border --mute --sid=no --no-window-dragging --video-unscaled=yes --no-input-builtin-bindings --input-ipc-server=\\\\.\\pipe\\mpvsocket --idle=yes --keep-open=yes --dither-depth=auto --background=0.78/0.78/0.78 --alpha=blend --osd-font-size=24 --osd-duration=5000 --osd-border-size=1.5 --osd-scale-by-window=no --tone-mapping=reinhard --tone-mapping-param=0.45 --tone-mapping-mode=rgb --gamut-mapping-mode=relative";
        private static readonly int processID = Process.GetCurrentProcess().Id;
        private System.IO.Pipes.NamedPipeClientStream mpv_tubo;
        private System.IO.Pipes.NamedPipeClientStream mpv2_tubo;
        private readonly Process mpv1p;
        private Process mpv2p;
        private StreamWriter mpv_cmd, mpv2_cmd;
        private StreamReader mpv_out;
        private readonly Panel rightpanel;
        private bool reading;
        private int mpvid = -1;
        private int mpv2id = -1;
        public bool Mpv_loaded { get; set; }
        public bool Mpv2_loaded { get; set; }
        public int Mpv_id { 
            get {
                if (mpvid != -1 || !Mpv_loaded)
                    return mpvid;
                GetWindowThreadProcessId(mpv1p.MainWindowHandle, ref mpvid);
                return mpvid;
            }
        }
        public int Mpv2_id
        {
            get
            {
                if (mpv2id != -1 || !Mpv2_loaded)
                    return mpv2id;
                GetWindowThreadProcessId(mpv2p.MainWindowHandle, ref mpv2id);
                return mpv2id;
            }
        }

        public Player(Control form, Panel leftpanel, Panel panel)
        {
            rightpanel = panel;
            Process[] processes = Process.GetProcessesByName("mpv");
            List<IntPtr> ignore = new List<IntPtr>();
            foreach (Process p in processes)
                ignore.Add(p.MainWindowHandle);
            Process.Start(Func.bindir + "mpv.exe", mpv_args.Replace("socket", "socket" + processID.ToString()) + " --vo=gpu-next,gpu");

            Process mp = mpv1p;
            
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
            
            mpv1p = mp;
            Wait_mpv(5);
            Thread.Sleep(16);
            form.Invoke(new Action(() => {
                SetParent(mpv1p.MainWindowHandle, leftpanel.Handle);
                MoveWindow(mpv1p.MainWindowHandle, 0, 0, Screen.FromControl(form).Bounds.Width, Screen.FromControl(form).Bounds.Height, true);
            }));

            Reconnect();
            Mpv_loaded = true;
        }

        public void Reconnect()
        {
            mpv_tubo = new System.IO.Pipes.NamedPipeClientStream("mpvsocket" + processID.ToString());
            mpv_cmd = new StreamWriter(mpv_tubo);
            mpv_tubo.Connect();
            mpv_cmd.AutoFlush = true;
            if (Mpv2_loaded)
            {
                mpv2_tubo = new System.IO.Pipes.NamedPipeClientStream("mpv2socket" + processID.ToString());
                mpv2_cmd = new StreamWriter(mpv2_tubo);
                mpv2_tubo.Connect();
                mpv2_cmd.AutoFlush = true;
            }
        }

        public bool Wait_mpv(int times = 1)
        {
            bool idle1 = false;
            bool idle2 = false;
            int limit = 0;
            while ((!idle1 || !idle2) && limit < 5000)
            {
                TimeSpan mpv1_b = mpv1p.TotalProcessorTime;
                TimeSpan mpv2_b = mpv1_b;
                if (Mpv2_loaded)
                    mpv2_b = mpv2p.TotalProcessorTime;
                Thread.Sleep(20 * times);
                limit += 20 * times;
                mpv1p.Refresh();
                TimeSpan mpv1_e = mpv1p.TotalProcessorTime;
                TimeSpan mpv2_e = mpv1_e;
                if (Mpv2_loaded)
                {
                    mpv2p.Refresh();
                    mpv2_e = mpv2p.TotalProcessorTime;
                }
                if (mpv1_e == mpv1_b)
                    idle1 = true;
                if (!Mpv2_loaded)
                    idle2 = true;
                else if (mpv2_e == mpv2_b)
                    idle2 = true;
            }
            return true;
        }

        public void Mpv2_load(Form1 form, string file, string args = "", double seek = 0)
        {
            if (!Mpv2_loaded)
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
                    form.Invoke(new Action(() => {
                        SetParent(mpv2p.MainWindowHandle, rightpanel.Handle);
                        MoveWindow(mpv2p.MainWindowHandle, 0, 0, Screen.FromControl(form).Bounds.Width, Screen.FromControl(form).Bounds.Height, true);
                    }));
                    Mpv2_loaded = true;
                    Reconnect();

                    form.Mpv_load_second(file, args, seek);
                };
                bw.RunWorkerAsync();
            }
            else
                form.Mpv_load_second(file, args, seek);
        }

        public string Time()
        {
            if (reading)
                return "";
            Cmd("{ \"command\": [\"get_property\", \"playback-time\"] }");
            mpv_out = new StreamReader(mpv_tubo);
            string in_str = "";
            Regex tiempo = new Regex("data\":([0-9\\.]+)");
            reading = true;
            while (mpv_out.Peek() > -1)
                in_str = mpv_out.ReadLine();
            reading = false;
            in_str = tiempo.Match(in_str).Groups[1].Value.ToString();
            Double.TryParse(in_str, out double d);
            return d >= 0 ? in_str : "";
        }

        public void Cmd(string cmd, int mpv = 1)
        {
            if (mpv == 0 || mpv == 1)
                mpv_cmd.WriteLine(cmd);
            if ((mpv == 0 || mpv == 2) && Mpv2_loaded)
                mpv2_cmd.WriteLine(cmd);
        }

        public void Sync(bool add)
        {
            if (add)
                Cmd("{ \"command\": [\"vf\", \"add\", \"curves=g=0/0.2 1/1\"] }");
            else
                Cmd("{ \"command\": [\"vf\", \"remove\", \"curves=g=0/0.2 1/1\"] }");
        }

        public void Scale(double x, double y, int mpv = 1)
        {
            Cmd("{ \"command\": [\"set_property\", \"video-scale-x\", " + x + "] }", mpv);
            Cmd("{ \"command\": [\"set_property\", \"video-scale-y\", " + y + "] }", mpv);
        }
    }
}
