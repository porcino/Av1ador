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
    public class Video
    {
        private double starttime = 0, endtime = 0;
        private readonly string str_duration;
        public string File { get; }
        public double Duration { get; }
        public double EncodeDuration { get; set; }
        public double StartTime {
            get { return starttime; }
            set
            {
                if (value < EndTime)
                {
                    starttime = value;
                    EncodeDuration = (EndTime > 0 ? EndTime : Duration) - value;
                }
            }
        }
        public double EndTime
        {
            get { return endtime; }
            set
            {
                if (value > starttime && value > 0)
                {
                    endtime = value;
                    EncodeDuration = value - starttime;
                }
            }
        }
        public double CreditsTime { get; set; }
        public double CreditsEndTime { get; set; }

        public int Width { get; }
        public int Height { get; }
        public double Sar { get; }
        public double Dar { get; }
        public bool Hdr { get; }
        public string Color_matrix { get; }
        public bool Interlaced { get; }
        public int Channels { get; }
        public double Fps { get; set; }
        public double Kf_interval { get; set; }
        public bool Kf_fixed { get; set; }
        public double Timebase { get; set; }
        public double First_frame { get; set; }
        public bool Busy { get; set; }
        public bool Gs_thread { get; set; }
        public int Grain_level { get; set; }
        public double Predicted { get; set; }
        public List<string> Tracks { get; set; }


        public Video(string file, bool segundo = false, double len = 0)
        {
            File = file;
            First_frame = -1;
            Kf_fixed = true;
            Grain_level = -1;
            Tracks = new List<string> { };

            Process ffprobe = new Process();
            Func.Setinicial(ffprobe, 2);

            ffprobe.StartInfo.Arguments = " -show_entries stream=channels -of compact=p=0:nk=0 -v 0 \"" + file + "\"";
            ffprobe.Start();
            string output = ffprobe.StandardOutput.ReadToEnd();
            Regex res_regex = new Regex("channels=([0-9])");
            Match compare = res_regex.Match(output);
            Channels = compare.Success ? int.Parse(compare.Groups[1].Value) : 0;

            ffprobe.StartInfo.Arguments = " \"" + file + "\"";
            ffprobe.Start();
            string info = ffprobe.StandardError.ReadToEnd();
            res_regex = new Regex(" [0-9]{2,5}x[0-9]{2,5}");
            compare = res_regex.Match(info);
            if (compare.Success)
            {
                Width = int.Parse(compare.ToString().Split('x')[0].Replace(" ", ""));
                Height = int.Parse(compare.ToString().Split('x')[1]);
            }

            if (len == 0)
            {
                res_regex = new Regex(" ([0-9]*):([0-5][0-9]):([0-5][0-9].[0-9][0-9]), ");
                compare = res_regex.Match(info);
                if (compare.Success)
                {
                    str_duration = compare.Groups[0].ToString().Split('.')[0].Replace(" ", "");
                    Duration = Double.Parse(compare.Groups[1].ToString()) * 3600 + Double.Parse(compare.Groups[2].ToString()) * 60 + Double.Parse(compare.Groups[3].ToString());
                }
                else
                {
                    ffprobe.StartInfo.Arguments = " -v 0 -hide_banner -of compact=p=0:nk=1 -show_entries packet=pts_time -read_intervals 99999%+#1000" + " \"" + file + "\"";
                    ffprobe.Start();
                    string pts_time = ffprobe.StandardOutput.ReadToEnd();
                    res_regex = new Regex("([0-9]+).[0-9]+", RegexOptions.RightToLeft);
                    compare = res_regex.Match(pts_time);
                    Duration = compare.Success ? Double.Parse(compare.Groups[1].ToString()) : 0;
                    TimeSpan ts = TimeSpan.FromSeconds(Duration);
                    str_duration = ts.ToString().Split('.')[0];
                }
            }
            else
                Duration = len;
            endtime = Duration;

            res_regex = new Regex("(bt2020|smpte170m)");
            compare = res_regex.Match(info);
            if (compare.Success)
                Hdr = true;

            res_regex = new Regex("(bt2020|smpte170m|mpeg2video)");
            compare = res_regex.Match(info);
            if (compare.Success)
            {
                Color_matrix = compare.Groups[1].ToString();
                if (Color_matrix == "bt2020")
                    Hdr = true;
            }
            else
                Color_matrix = "";

            res_regex = new Regex("SAR ([0-9]+):([0-9]+)", RegexOptions.RightToLeft);
            compare = res_regex.Match(info);
            if (compare.Success)
                Sar = Double.Parse(compare.Groups[1].ToString()) / Double.Parse(compare.Groups[2].ToString());
            else
                Sar = 1;

            res_regex = new Regex("DAR ([0-9]+):([0-9]+)", RegexOptions.RightToLeft);
            compare = res_regex.Match(info);
            if (compare.Success && (Math.Abs(1.0 - Sar) > 0.01))
                Dar = Double.Parse(compare.Groups[1].ToString()) / Double.Parse(compare.Groups[2].ToString());
            else if (Height > 0)
            {
                Sar = 1;
                Dar = (double)Width / (double)Height;
            }

            res_regex = new Regex("(top first|bottom first)");
            compare = res_regex.Match(info);
            if (compare.Success)
                Interlaced = true;

            res_regex = new Regex("(23.976|23.98|24|25|30|29.97|60) fps");
            compare = res_regex.Match(info);
            if (compare.Success)
                Fps = Double.Parse(compare.Groups[1].ToString());
            else if (!segundo)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += (s, e) =>
                {
                    Process ffmpeg = new Process();
                    Func.Setinicial(ffmpeg, 3);
                    ffmpeg.StartInfo.Arguments = " -ss 0 -i \"" + file + "\" -t 180 -map 0:v:0 -c copy -f null -";
                    ffmpeg.Start();
                    ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
                    string output2 = ffmpeg.StandardError.ReadToEnd();
                    Regex regex = new Regex("frame= *([0-9]+)", RegexOptions.RightToLeft);
                    Match compara = regex.Match(output2);
                    if (compara.Success)
                        Fps = Math.Round(Double.Parse(compara.Groups[1].ToString()) / (Duration < 180 ? Duration : 180));
                    else
                        Fps = -1;
                };
                bw.RunWorkerAsync();
            }
            else
                Fps = -1;

            res_regex = new Regex(@"(\([\w]*?\):|:) Audio: ([\w\W]*?)\n(  [A-Z]|[A-Z]|$)");
            foreach (Match r in res_regex.Matches(info))
            {
                Regex regex = new Regex(@"[ ]+title[ ]*: ([\w\W]*?)[\r|\n]");
                Match compara = regex.Match(r.Groups[2].ToString());
                string lang = r.Groups[1].ToString();
                lang = lang.Remove(lang.Length - 1);
                if (compara.Success)
                    Tracks.Add(lang + compara.Groups[1].ToString());
                else
                    Tracks.Add(lang + r.Groups[2].ToString().Split(new string[] { "\n" }, StringSplitOptions.None)[0].Replace("\r",""));
            }
            

            Busy = true;
            BackgroundWorker bw2 = new BackgroundWorker();
            bw2.DoWork += (s, e) =>
            {
                Process ffmpeg = new Process();
                Func.Setinicial(ffmpeg, 3);
                ffmpeg.StartInfo.Arguments = " -copyts -start_at_zero -i \"" + file + "\" -t 120 -filter:v \"select='eq(pict_type\\,I)',showinfo\" -f null -";
                ffmpeg.Start();
                var kf = new List<double>();
                res_regex = new Regex("pts:[ ]*([0-9]{4}[0-9]*)");
                Regex t_regex;
                while (!ffmpeg.HasExited && kf.Count < 20 && Busy)
                {
                    output = ffmpeg.StandardError.ReadLine();
                    if (output != null)
                    {
                        if (Timebase == 0)
                        {
                            t_regex = new Regex("time_base:[ ]*([0-9]+)/([0-9]+)");
                            compare = t_regex.Match(output);
                            if (compare.Success)
                                Timebase = Double.Parse(compare.Groups[1].ToString()) / Double.Parse(compare.Groups[2].ToString());
                        }
                        if (First_frame == -1)
                        {
                            t_regex = new Regex("pts:[ ]*([0-9]+)");
                            compare = t_regex.Match(output);
                            if (compare.Success) { 
                                First_frame = Double.Parse(compare.Groups[1].ToString()) * Timebase;
                                if (segundo)
                                    Busy = false;
                            }
                        }
                        compare = res_regex.Match(output);
                        if (compare.Success)
                            kf.Add(Double.Parse(compare.Groups[1].ToString()) * Timebase);
                    }
                }
                try
                {
                    ffmpeg.Kill();
                }
                catch { }
                double last = 0, ldif = 0;
                int noteq = 0;
                foreach (double time in kf)
                {
                    var dif = time - last;
                    if (dif > 3 && Math.Abs(dif - ldif) > 0.5)
                        noteq++;
                    if (dif > Kf_interval)
                        Kf_interval = dif;
                    last = time;
                    ldif = dif;
                }
                if (Kf_interval == 0)
                    Kf_interval = 10;
                if (noteq > 2)
                    Kf_fixed = false;
            };
            bw2.RunWorkerCompleted += (s, e) => {
                Busy = false;
            };
            bw2.RunWorkerAsync();
        }

        internal Bitmap Bar(int barra_w, int barra_h, double playtime, [Optional] Encode encode)
        {
            Bitmap bar = new Bitmap(barra_w, barra_h);
            Graphics g = Graphics.FromImage(bar);
            int l = (int)(bar.Width * starttime / Duration);
            int r = (int)(bar.Width * endtime / Duration);
            g.Clear(Color.Firebrick);
            if (starttime > 0)
                g.FillRectangle(Brushes.RosyBrown, 0, 0, l, bar.Height);
            if (endtime < Duration)
                g.FillRectangle(Brushes.RosyBrown, r, 0, bar.Width - r, bar.Height);
            if (CreditsTime > 0 && CreditsTime < Duration)
            {
                int left = (int)(bar.Width * CreditsTime / Duration);
                int right = (int)(bar.Width * (CreditsEndTime > 0 ? CreditsEndTime : endtime) / Duration);
                g.FillRectangle(Brushes.Maroon, left, 0, bar.Width - left - (bar.Width - right), bar.Height);
            }
            int[] Left = new int[] { };
            int[] Ancho = new int[] { };
            if (encode != null)
            {
                if (encode.Chunks != null)
                {
                    Left = new int[encode.Chunks.Length];
                    Ancho = new int[encode.Chunks.Length];
                    for (int i = 0; i < encode.Chunks.Length; i++)
                    {
                        if (encode.Chunks[i] != null)
                        {
                            Left[i] = (int)(bar.Width * Double.Parse(encode.Splits[i]) / Duration);
                            Ancho[i] = (int)(bar.Width * Double.Parse(encode.Splits[i + 1]) / Duration) - Left[i];
                            if (encode.Chunks[i].Completed)
                            {
                                g.FillRectangle(Brushes.MediumSeaGreen, Left[i], 0, Ancho[i], bar.Height);
                            }
                            else if (encode.Chunks[i].Encoding)
                            {
                                g.FillRectangle(Brushes.Orange, Left[i], 0, Ancho[i], bar.Height);
                                Ancho[i] = (int)(bar.Width * (Double.Parse(encode.Splits[i]) + encode.Chunks[i].Progress) / Duration) - Left[i];
                                g.FillRectangle(Brushes.YellowGreen, Left[i], 0, Ancho[i], bar.Height);
                            }
                        }
                    }
                }
                if (encode.Splits != null)
                {
                    foreach (string line in encode.Splits.ToList())
                    {
                        int x = (int)(bar.Width * Double.Parse(line) / Duration);
                        g.DrawLine(new Pen(Color.FromArgb(128, 255, 200, 200), 1), x, 0, x, bar.Height);
                    }
                }
                if (encode.Merge > 0)
                {
                    g.FillRectangle(Brushes.MediumSeaGreen, l, 0, (int)(bar.Width * (int)(encode.Merge) / Duration) - l, bar.Height);
                }
                if (encode.Chunks != null)
                {
                    List<Point> points = new List<Point>();
                    for (int i = 0; i < encode.Chunks.Length; i++)
                    {
                        if (encode.Chunks[i] != null && encode.Chunks[i].Bitrate > 0 && encode.Abr > 0 && encode.Peak_br > 0)
                        {
                            double peak = encode.Abr;
                            if (encode.Peak_br > encode.Abr)
                                peak = encode.Peak_br;
                            double pow = 1.0 / (1.0 + peak / encode.Abr / 30.0);
                            double y = encode.Chunks[i].Bitrate > encode.Abr ? encode.Abr + Math.Pow(encode.Chunks[i].Bitrate - encode.Abr, pow) : encode.Chunks[i].Bitrate;
                            y = (double)bar.Height - (y * (double)bar.Height / (encode.Abr + Math.Pow(peak - encode.Abr, pow)));
                            points.Add(new Point(Left[i] + Ancho[i] / 2, (int)y));
                        }
                    }
                    if (points.Count > 0)
                    {
                        g.DrawLine(new Pen(Color.FromArgb(140, 0, 0, 0), 1), l, points[0].Y, points[0].X, points[0].Y);
                        g.DrawLine(new Pen(Color.FromArgb(140, 0, 0, 0), 1), points[points.Count - 1].X, points[points.Count - 1].Y, r, points[points.Count - 1].Y);
                    }
                    for (int i = 0; i < points.Count - 1; i++)
                        g.DrawLine(new Pen(Color.FromArgb(140, 0, 0, 0), 1), points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
                }
            }
            g.FillRectangle(Brushes.DodgerBlue, (int)(bar.Width * playtime / Duration), 0, 6, bar.Height);
            g.Flush();
            return bar;
        }

        internal string Mediainfo()
        {
            string str_ch = Channels.ToString();
            switch (str_ch)
            {
                case "1":
                    str_ch = "Mono";
                    break;
                case "2":
                    str_ch = "Stereo";
                    break;
                case "6":
                    str_ch = "Surround 5.1";
                    break;
                case "7":
                    str_ch = "Surround 6.1";
                    break;
                case "8":
                    str_ch = "Surround 7.1";
                    break;
                case "0":
                    str_ch = "No audio";
                    break;
            }
            double size = Math.Round(new FileInfo(File).Length / 1024.0 / 1024.0, 1);
            return str_duration + ", " + Width + "x" + Height + (Interlaced ? "i" : "p") + ", " + (Fps == 0 ? "..." : Fps.ToString() ) + " fps" + (Hdr ? ", HDR" : "") + " - " + str_ch + " - " + size + "MB";
        }

        internal void Grain_detect(NumericUpDown gsupdown, Label status, int maxgs, Label inf, List<string> vf)
        {
            Gs_thread = true;
            int loop = (int)((5 + Math.Sqrt(Duration / 5)) / 2);
            int frames = (int)(70 + (Duration / 30));
            string name = Path.GetFileNameWithoutExtension(File);
            BackgroundWorker bw = new BackgroundWorker();
            status.Text = "Measuring frame grain level...";
            string mediainfo = inf.Text;
            string crop = "";
            foreach (string filtro in vf)
            {
                if (filtro.Contains("crop="))
                    crop = filtro + ",";
            }
            bw.DoWork += (s, e) =>
            {
                List<int> gs = new List<int>();
                for (int i = 1; i <= loop; i++)
                {
                    if (gsupdown.Enabled || mediainfo != inf.Text || !status.Text.Contains("grain"))
                    {
                        Grain_level = -1;
                        break;
                    }
                    Process ffmpeg = new Process();
                    Func.Setinicial(ffmpeg, 3);
                    int ss = (int)(Duration * i / (loop + 2));
                    string cmd = " -loglevel panic -init_hw_device opencl=gpu -filter_hw_device gpu -ss " + ss + " -i \"" + File + "\"  -an -sn -frames:v 1 -vf \"" + crop + "thumbnail=n=" + frames + ",scale=w=1920:h=1080:force_original_aspect_ratio=decrease:force_divisible_by=2";
                    if (Hdr)
                        cmd += ",format=p010,hwupload,tonemap_opencl=tonemap=hable:r=tv:p=bt709:t=bt709:m=bt709:format=nv12,hwdownload,format=nv12";
                    cmd += "\" -lossless 1 -compression_level 1 -y \"" + name + "_th.webp\"";
                    ffmpeg.StartInfo.Arguments = cmd;
                    ffmpeg.Start();
                    ffmpeg.WaitForExit(-1);
                    ffmpeg.StartInfo.Arguments = " -loglevel panic -init_hw_device opencl=gpu -filter_hw_device gpu -i \"" + name + "_th.webp\" -frames:v 1 -vf pad=(iw+16):(ih+16):8:8,format=pix_fmts=yuv420p,hwupload,nlmeans_opencl=s=1.8:p=7:r=9,hwdownload,format=pix_fmts=yuv420p,crop=(iw-16):(ih-16):8:8 -lossless 1 -compression_level 1 -y \"" + name + "_th_dns.webp\"";
                    ffmpeg.Start();
                    ffmpeg.WaitForExit(-1);
                    ffmpeg.StartInfo.Arguments = " -loglevel panic -init_hw_device opencl=gpu -filter_hw_device gpu -i \"" + name + "_th.webp\" -frames:v 1 -vf pad=(iw+12):(ih+12):6:6,format=pix_fmts=yuv420p,hwupload,nlmeans_opencl=s=4:p=5:r=15,hwdownload,format=pix_fmts=yuv420p,crop=(iw-10):(ih-12):6:6 -lossless 1 -compression_level 1 -y \"" + name + "_th_dnb.webp\"";
                    ffmpeg.Start();
                    ffmpeg.WaitForExit(-1);
                    double s1 = new FileInfo(name + "_th.webp").Length;
                    double s2 = new FileInfo(name + "_th_dns.webp").Length;
                    double s3 = new FileInfo(name + "_th_dnb.webp").Length;
                    double g = s1 * 100.0 / s2;
                    g = ((s1 * 100.0 / s3 * 100.0 / g) - 105.0) * 8.0 / 10.0;
                    g = g > 100 ? 100 : g;
                    g = g < 0 ? 0 : g;
                    gs.Add((int)g);
                }
                if (System.IO.File.Exists(name + "_th.webp"))
                    System.IO.File.Delete(name + "_th.webp");
                if (System.IO.File.Exists(name + "_th_dns.webp"))
                    System.IO.File.Delete(name + "_th_dns.webp");
                if (System.IO.File.Exists(name + "_th_dnb.webp"))
                    System.IO.File.Delete(name + "_th_dnb.webp");
                if (!gsupdown.Enabled && mediainfo == inf.Text && status.Text.Contains("grain"))
                    Grain_level = (int)(Func.Median(gs.ToArray()) / (100.0/maxgs));
            };
            bw.RunWorkerCompleted += (s, e) => {
                if (!gsupdown.Enabled && Grain_level > -1)
                {
                    gsupdown.Value = Grain_level <= gsupdown.Maximum ? Grain_level : gsupdown.Maximum;
                    status.Text = "";
                }
                else if (gsupdown.Enabled)
                    status.Text = "";
                Gs_thread = false;
            };
            bw.RunWorkerAsync();
        }

        internal void Predict(Label status, Encoder encoder, ListBox vf)
        {
            Busy = true;
            double duration = EndTime - starttime;
            int fr = encoder.Out_fps > 0 ? encoder.Out_fps : (int)Fps;
            fr = (int)(fr * ((10000 / fr) + 600) / 1000);
            Encoder enc = (Encoder)encoder.Clone();
            enc.Set_video_codec("H264 (x264)");
            enc.Speed = "veryslow";
            enc.Param = "-x264opts ref=1:me=hex:subme=8:b-adapt=1";
            enc.V_kbps = enc.Out_w * enc.Out_h * fr / 100 * 3 / 1024;
            enc.Vf.RemoveAll(s => s.StartsWith("scale=w"));
            int t = 6;
            string str = enc.Build_vstr(true).Replace("\"!name!\"","-f null -").Replace("!start! ","").Replace("!file!",File).Replace("!duration!","-t " + t);
            str = str.Replace("-copyts -start_at_zero ", "");
            int loop = (int)((5 + Math.Sqrt(duration / 2)) / t);
            status.Text = "Analyzing...";
            Regex regex = new Regex("final ratefactor: ([0-9]*.[0-9]*)");
            double crf = 0;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                if (Predicted == 0)
                {
                    Process ffmpeg = new Process();
                    Func.Setinicial(ffmpeg, 3);
                    for (int i = 1; i <= loop; i++)
                    {
                        int ss = (int)(StartTime + (duration * i / (loop + 2)));
                        ffmpeg.StartInfo.Arguments = str.Replace("!seek!", "-ss " + ss).Replace("!bitrate!" , enc.V_kbps.ToString());
                        ffmpeg.Start();
                        ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
                        string output = "";
                        while (!ffmpeg.HasExited)
                        {
                            output += ffmpeg.StandardError.ReadLine() + "\n";
                            Thread.Sleep(50);
                        }
                        output += ffmpeg.StandardError.ReadToEnd();
                        Match match = regex.Match(output);
                        if (match.Success)
                            crf += Double.Parse(match.Groups[1].ToString());
                    }
                    crf /= loop;
                    Predicted = crf;
                }
                else
                    crf = Predicted;
                double ratio = Func.Crf2br(crf) * 100.0 / enc.V_kbps;
                double bitrate = Func.Crf2br(27) * 100.0 / ratio;
                double br_limit = bitrate * (60.0 + 100.0) / 160.0;
                int scale = (int)(5.0 * Math.Pow(encoder.V_kbps * (100.0 / encoder.Rate) / br_limit, 15.0 / 20.0));
                scale = scale > 100 ? 100 : scale;
                int w = 32 * (enc.Out_w * scale / 100 / 32);
                if (w == 0)
                    w = 16;
                int h = ((w * enc.Out_h / enc.Out_w) + 4) / 8;
                h *= 8;
                if (h % 2 != 0)
                    h -= 1;
                encoder.Vf_add("scale", w.ToString(), h.ToString());
            };
            bw.RunWorkerCompleted += (s, e) => {
                vf.Items.Clear();
                foreach (string f in encoder.Vf)
                    vf.Items.Add(f);
                Busy = false;
                status.Text = "";
                encoder.Predicted = true;
            };
            bw.RunWorkerAsync();
        }
    }
}

