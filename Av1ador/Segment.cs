using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Av1ador
{
    internal class Encode
    {
        private double progress;
        private double initial_progress;
        private double video_size;
        private long audio_size;
        private double speed;
        private TimeSpan remaining;
        private readonly Stopwatch watch;
        private int bitrate;
        private double track_delay;
        private int[] fps = new int[0];
        private int frames_last;
        private bool unattended;
        public string Tempdir { get; } = "temp\\";
        public string Dir { get; set; }
        public string File { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public int Split_min_time { get; set; }
        public bool Can_run { get; set; }
        public bool Failed { get; set; }
        public List<string> Splits { get; set; }
        public List<string> Complexity { get; set; }
        public int Workers { get; set; }
        public string Param { get; set; }
        public string A_Param { get; set; }
        public double Kf_interval { get; set; }
        public Segment[] Chunks { get; set; }
        public List<int> Order { get; set; }
        public string Job { get; set; }
        public string A_Job { get; set; }
        public bool Finished { get; set; }
        public double Merge { get; set; }
        public string Fps_filter { get; set; }
        public double Spd { get; set; }
        public List<string> Status { get; set; }
        public bool Running { get; set; }
        public double Abr { get; set; }
        public double Peak_br { get; set; }
        public int Counter { get; set; }
        public uint Clean { get; set; }
        public static BackgroundWorker[] Bw { get; set; }
        public int Progress
        {
            get
            {
                if (Counter > 5)
                {
                    Counter = 0;
                    if (Chunks != null && Splits != null)
                    {
                        double pct = 0;
                        foreach (var chunk in Chunks.ToList())
                            pct += chunk.Completed ? chunk.Length : chunk.Progress;
                        progress = pct * (double)100 / (Double.Parse(Splits[Splits.Count - 1]) - Double.Parse(Splits[0]));
                        if (initial_progress == 0 && progress > 0 && !Running)
                            initial_progress = progress;
                        return (int)Math.Ceiling(progress);
                    }
                    return 0;
                }
                else
                {
                    Counter++;
                    return (int)Math.Ceiling(progress);
                }
            }
        }
        public double Estimated
        {
            get
            {
                if (Counter > 5)
                {
                    if (Chunks != null && Splits != null && progress > 0)
                    {
                        long sum = 0;
                        int n = 0;
                        long bsum = 0;
                        foreach (var chunk in Chunks.ToList())
                        {
                            if (chunk.Completed || chunk.Encoding)
                            {
                                n++;
                                bsum += (long)chunk.Bitrate;
                            }
                            sum += chunk.Size;
                            if (chunk.Bitrate > Peak_br)
                                Peak_br = chunk.Bitrate;
                        }
                        double duration = Double.Parse(Splits[Splits.Count - 1]) - Double.Parse(Splits[0]);
                        double max = (Peak_br + (audio_size * 8.0 / 1024.0) / duration) * duration / 8.0 / 1024.0;
                        double size = ((bsum / (double)n) + (audio_size / 1024.0 * 8.0 / duration)) * duration / 8.0 / 1024.0;
                        sum += audio_size * (long)progress / (long)100;
                        double time = progress * duration / 100.0;
                        Abr = sum / 1024.0 * 8.0 / time;
                        double quadp = Math.Sqrt(progress / 100.0);
                        double quad2p = Math.Pow(progress / 100.0, 0.2);
                        video_size = Math.Round((size * quad2p + (max + size) / 2.0 * (1.0 - quad2p)) * (1.0 - quadp) + (sum / 1024.0 * 100.0 / 1024.0 / progress) * quadp, 1);
                        return video_size;
                    }
                    return 0;
                } else
                    return video_size;
            }
        }

        public double Speed
        {
            get
            {
                if (Counter > 5)
                {
                    if (Chunks != null && Splits != null)
                    {
                        int f = 0;
                        foreach (var chunk in Chunks.ToList())
                            f += chunk.Frames;
                        int frames_new = f - frames_last;
                        frames_last = f;
                        if (frames_new >= 0)
                        {
                            if (fps.Count() >= 40)
                                fps = fps.Skip(1).ToArray();
                            fps = fps.Concat(new int[] { frames_new }).ToArray();
                        }

                        speed = Math.Round((double)fps.Sum() / ((double)fps.Length * 1.5), 2);
                        return speed;
                    }
                    return 0;
                }
                else
                {
                    return speed;
                }
            }
        }

        public int Segments_left
        {
            get
            {
                if (Chunks != null && Splits != null)
                {
                    int f = 0;
                    foreach (var chunk in Chunks.ToList())
                    {
                        if (!chunk.Encoding && !chunk.Completed)
                            f++;
                    }
                    return f;
                }
                return 0;
            }
        }

        public TimeSpan Remaining
        {
            get
            {
                if (Counter > 5 && progress > 0 && progress != initial_progress)
                {
                    double total = (100.0 - initial_progress) * watch.ElapsedMilliseconds / (progress - initial_progress);
                    remaining = TimeSpan.FromMilliseconds(total - watch.ElapsedMilliseconds);
                }
                return remaining;
            }
        }

        public TimeSpan Elapsed
        {
            get
            {
                return TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds);
            }
        }

        public Encode()
        {
            Split_min_time = 6;
            Status = new List<string>();
            watch = new Stopwatch();
            remaining = new TimeSpan();
            bitrate = 0;
        }

        public void Start_encode(string dir, Video v, bool audio, double delay = 0, int br = 0, double spd = 1, bool unat = false)
        {
            track_delay = delay;
            Dir = dir == "" ? Path.GetDirectoryName(v.File) + "\\" : dir + "\\";
            File = v.File;
            Name = Tempdir + Path.GetFileNameWithoutExtension(v.File);
            unattended = unat;
            if (!Directory.Exists(Name))
                Directory.CreateDirectory(Name);
            else if (!unattended)
            {
                Form1.Dialogo = true;
                string[] files = Directory.GetFiles(Name);
                if (files.Length > 0 && MessageBox.Show("There are some files from a previous encoding, do you want to resume it?", "Resume", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                {
                    foreach (FileInfo f in new DirectoryInfo(Name).GetFiles())
                        f.Delete();
                }
                Form1.Dialogo = false;
            }
            Spd = spd;
            bitrate = br;
            bool vbr = br > 0;

            Kf_interval = v.Kf_interval;
            double to = v.EndTime != v.Duration ? v.EndTime : v.Duration + 1;
            double final = v.CreditsTime > 0 && !vbr ? v.CreditsTime - (double)Split_min_time : to;
            string audiofile = Name + "\\audio." + A_Job;

            if (audio && A_Param != "")
            {
                if (!System.IO.File.Exists(audiofile))
                {
                    Status.Add("Encoding audio...");
                    string ssa = v.StartTime > 0 ? " -ss " + v.StartTime.ToString() : "";
                    double toa = (to - v.StartTime) * Spd;
                    Process ffaudio = new Process();
                    Func.Setinicial(ffaudio, 3, " -y" + ssa + " -i \"" + v.File + "\" -t " + toa.ToString() + A_Param + " \"" + audiofile + "\"");
                    ffaudio.Start();
                    string aout = ffaudio.StartInfo.Arguments + Environment.NewLine;
                    BackgroundWorker abw = new BackgroundWorker();
                    abw.DoWork += (s, e) =>
                    {
                        while (!ffaudio.HasExited && Can_run)
                            aout += ffaudio.StandardError.ReadLine() + Environment.NewLine;
                        if (!Can_run)
                        {
                            while (System.IO.File.Exists(audiofile))
                            {
                                try { 
                                    ffaudio.Kill();
                                    Thread.Sleep(1000);
                                    System.IO.File.Delete(audiofile);
                                } catch { }
                            }
                        }
                    };
                    abw.RunWorkerCompleted += (s, e) =>
                    {
                        System.IO.File.WriteAllText(Name + "\\audio.log", aout);
                        Status.Remove("Encoding audio...");
                        if (System.IO.File.Exists(audiofile))
                            audio_size = new FileInfo(audiofile).Length;
                    };
                    abw.RunWorkerAsync();
                }
                else
                    audio_size = new FileInfo(audiofile).Length;
            }

            if (!System.IO.File.Exists(Name + "\\segments.txt") || (vbr && !System.IO.File.Exists(Name + "\\complexity.txt")))
            {
                Status.Add("Detecting scenes...");
                int workers = Math.Max(Math.Min((int)Math.Ceiling(v.Duration / (double)(180 / ((double)v.Width / 1000))), 3), 1);
                double tdist = (final - v.StartTime) / (double)workers;
                Bw = new BackgroundWorker[workers];
                List<string>[] trim_time = new List<string>[workers];
                List<string>[] scenes_complex = new List<string>[workers];
                Splits = trim_time[0];
                Regex t_regex = new Regex("pts:[ ]*([0-9]{2}[0-9]*)");
                Regex scene_regex = new Regex("scene:([0-9.]+) -> select:([0-1]+).[0]+");
                Regex tb_regex = new Regex("time_base:[ ]*([0-9]+)/([0-9]+)");

                for (int i = 0; i < workers; i++)
                {
                    trim_time[i] = new List<string> { v.StartTime.ToString() };
                    scenes_complex[i] = new List<string>();
                    Bw[i] = new BackgroundWorker();
                    Bw[i].DoWork += (s, e) =>
                    {
                        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                        object[] param = e.Argument as object[];
                        int j = (int)param[0];
                        double seek = v.StartTime + (double)j * tdist;
                        string ss1 = (seek - Kf_interval) > 0 ? " -ss " + (seek - Kf_interval).ToString() : "";
                        string ss2 = v.StartTime > 0 ? " -ss " + v.StartTime.ToString() : "";
                        double final2 = final - (double)(workers - (j + 1)) * tdist;
                        if (j > 0)
                            ss2 = " -ss " + seek.ToString();
                        Process ffmpeg = new Process();
                        Func.Setinicial(ffmpeg, 3);
                        if ((v.Width <= 1920 || v.Kf_fixed) || vbr || Fps_filter != "")
                            ffmpeg.StartInfo.Arguments = (vbr ? " -loglevel debug" : "") + " -copyts -start_at_zero" + ss1 + " -i \"" + v.File + "\"" + ss2 + " -to " + final2.ToString() + " -filter:v \"" + Fps_filter + "select='gt(scene,0.1)',select='isnan(prev_selected_t)+gte(t-prev_selected_t\\," + Split_min_time.ToString() + ")',showinfo\" -an -f null - ";
                        else
                            ffmpeg.StartInfo.Arguments = " -copyts -start_at_zero -skip_frame nokey" + ss1 + " -i \"" + v.File + "\"" + ss2 + " -to " + final2.ToString() + " -filter:v showinfo -an -f null - ";
                        ffmpeg.Start();
                        ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
                        string output;
                        double score = 0;
                        int frames = 0;
                        double new_timebase = 0;
                        while (!ffmpeg.HasExited && Can_run)
                        {
                            output = ffmpeg.StandardError.ReadLine();
                            if (output != null)
                            {
                                Match compare = t_regex.Match(output);
                                if (compare.Success)
                                {
                                    double time = Double.Parse(compare.Groups[1].ToString()) * (new_timebase > 0 ? new_timebase : v.Timebase);
                                    Match scene_compare = scene_regex.Match(output);
                                    if (!vbr || (scene_compare.Success && scene_compare.Groups[2].ToString() == "1"))
                                    {
                                        if (time > final)
                                        {
                                            time = final;
                                            break;
                                        }
                                        else if (time > seek)
                                        {
                                            if (trim_time[j].Count > 0 && (time - Double.Parse(trim_time[j][trim_time[j].Count - 1])) > Split_min_time)
                                            {
                                                trim_time[j].Add(time.ToString());
                                                Splits = Func.Concat(trim_time);

                                                if (frames > 0)
                                                {
                                                    scenes_complex[j].Add((score / (double)frames).ToString());
                                                    score = 0;
                                                    frames = 0;
                                                }
                                            }
                                        }
                                    }
                                    else if (scene_compare.Success)
                                    {
                                        score += Double.Parse(scene_compare.Groups[1].ToString());
                                        frames++;
                                    }
                                }
                                else
                                {
                                    if (Fps_filter != "" && new_timebase == 0)
                                    {
                                        compare = tb_regex.Match(output);
                                        if (compare.Success)
                                            new_timebase = Double.Parse(compare.Groups[1].ToString()) / Double.Parse(compare.Groups[2].ToString());
                                    }
                                }
                            }
                        }
                        if (!Can_run)
                        {
                            try { ffmpeg.Kill(); } catch { }
                        }
                        else
                        {
                            if (vbr && j + 1 == workers)
                            {
                                if (frames > 0)
                                    scenes_complex[j].Add((score / (double)frames).ToString());
                            }
                        }
                    };
                    Bw[i].RunWorkerCompleted += (s, e) =>
                    {
                        bool busy = false;
                        foreach (BackgroundWorker w in Bw)
                        {
                            busy = w.IsBusy;
                            if (busy)
                                break;
                        }
                        if (!busy)
                        {
                            Splits = Func.Concat(trim_time).Distinct().ToList();
                            if (v.CreditsTime > 0 && !vbr)
                            {
                                Splits.Add("000" + v.CreditsTime.ToString());
                                if (v.CreditsEndTime > 0)
                                    Splits.Add(v.CreditsEndTime.ToString());
                            }
                            Splits.Add(to.ToString());
                            System.IO.File.WriteAllLines(Name + "\\segments.txt", Splits.ToArray());
                            if (vbr)
                                System.IO.File.WriteAllLines(Name + "\\complexity.txt", Func.Concat(scenes_complex, false).ToArray());

                            Begin();
                        }
                    };
                }
                for (int i = 0; i < workers; i++)
                {
                    object[] parameters = new object[] { i };
                    Bw[i].RunWorkerAsync(parameters);
                }
            }
            else
                Begin();
        }

        public void Begin()
        {
            Status.Remove("Detecting scenes...");
            if (Can_run)
            {
                Status.Add("Encoding video...");
                watch.Start();
                Encoding();
            }
        }

        public void Encoding()
        {
            if (Running)
                return;
            Running = true;
            if (Splits == null)
                Splits = new List<string>(System.IO.File.ReadAllLines(Name + "\\segments.txt"));
            if (bitrate > 0 && Complexity == null)
                Complexity = new List<string>(System.IO.File.ReadAllLines(Name + "\\complexity.txt"));
            if (Chunks == null)
            {
                Chunks = new Segment[Splits.Count - 1];
                List<KeyValuePair<int, double>> Sorted = new List<KeyValuePair<int, double>>();
                Order = new List<int>();
                double avg_scene_score = 0;
                double duration = Double.Parse(Splits[Splits.Count - 1]);
                double min_scene_duration = duration;
                if (bitrate > 0 && Complexity != null)
                {
                    for (int i = 0; i < Splits.Count - 1; i++)
                    {
                        avg_scene_score += Double.Parse(Complexity[i]);
                        double scene_duration = Double.Parse(Splits[i + 1]) - Double.Parse(Splits[i]);
                        if (scene_duration < min_scene_duration)
                            min_scene_duration = scene_duration;
                    }
                    avg_scene_score /= Complexity.Count;
                }
                for (int i = 0; i < Splits.Count - 1; i++)
                {
                    double start = Double.Parse(Splits[i]);
                    double seek = start - Kf_interval;
                    string pathfile = Name + "\\" + i.ToString("00000") + "." + Job;
                    double scene_duration = Double.Parse(Splits[i + 1]) - start;
                    int scene_bitrate = bitrate;
                    if (avg_scene_score > 0)
                    {
                        double scene_score = Double.Parse(Complexity[i]);
                        scene_bitrate = (int)(((scene_score * 0.6 + avg_scene_score * 0.4) / avg_scene_score * (double)bitrate - (double)bitrate) / (scene_duration / min_scene_duration) + (double)bitrate);
                    }
                    Chunks[i] = new Segment
                    {
                        Arguments = Replace_times(Param, File, pathfile, Splits[i + 1], seek, start, scene_bitrate),
                        Pathfile = pathfile,
                        Length = scene_duration,
                        Credits = Splits[i].Length > 3 && Splits[i].Substring(0, 3) == "000",
                        Last = i + 1 == Splits.Count() - 1,
                        Compare = Job != "vvc"
                    };
                    Sorted.Add(new KeyValuePair<int, double>(i, Chunks[i].Length));
                }
                Sorted.Sort((x, y) => (y.Value.CompareTo(x.Value)));
                while(Sorted.Count > 0)
                {
                    if (Order.Count % 2 == 0)
                    {
                        Order.Add(Sorted[0].Key);
                        Sorted.RemoveAt(0);
                    }
                    else
                    {
                        Order.Add(Sorted[Sorted.Count - 1].Key);
                        Sorted.RemoveAt(Sorted.Count - 1);
                    }
                }
            }
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (ss, e) =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                int instances = 0;
                int done = 0;
                for (int i = 0; i < Order.Count; i++)
                {
                    Segment chunk = Chunks[Order[i]];
                    bool skip = true;
                    if (chunk.Encoding)
                        instances++;
                    else if (!chunk.Completed && chunk.Progress == 0 && Can_run)
                    {
                        bool present = System.IO.File.Exists(chunk.Pathfile);
                        if (present && new FileInfo(chunk.Pathfile).Length > 500 && !System.IO.File.Exists(chunk.Pathfile + ".txt")
                            && (!chunk.Compare || Math.Abs(Video.Get_duration(Video.Get_info(chunk.Pathfile), out string _, chunk.Pathfile) - chunk.Length) < 2 || chunk.Last))
                        {
                            skip = false;
                            chunk.Completed = true;
                            chunk.Progress = chunk.Progress == 0 ? 1 : chunk.Progress;
                            chunk.Size = new FileInfo(chunk.Pathfile).Length;
                            chunk.Bitrate = chunk.Size / (double)1024 * (double)8 / chunk.Length;
                            if (watch.ElapsedMilliseconds > 500)
                            {
                                for (int j = 0; j < Order.Count; j++)
                                    Chunks[Order[j]].Frames = 0;
                                if (Entry.elapsed_add == null && watch.ElapsedMilliseconds - Entry.Lastsave > 10000)
                                    Entry.elapsed_add = new object[] { watch.ElapsedMilliseconds - Entry.Lastsave, File };
                            }
                            fps = new int[0];
                            frames_last = 0;
                            Entry.Lastsave = 0;
                            watch.Reset();
                            watch.Start();
                        }
                        else
                        {
                            if (present)
                                try
                                {
                                    System.IO.File.Delete(chunk.Pathfile);
                                }
                                catch { }
                            instances++;
                            chunk.Encoding = true;
                            Thread thread = new Thread(() => Background(chunk));
                            thread.Start();
                            Thread.Sleep(100);
                        }
                    }
                    if (chunk.Completed)
                        done++;
                    if (instances >= Workers && skip)
                    {
                        if (Entry.elapsed_add == null && watch.ElapsedMilliseconds - Entry.Lastsave > 300000)
                            Entry.elapsed_add = new object[] { watch.ElapsedMilliseconds - Entry.Lastsave, File };
                        break;
                    }
                }
                if (done == Chunks.Length)
                {
                    watch.Reset();
                    Status.RemoveAll(s => s.StartsWith("Encoding video"));
                    Thread trd = new Thread(new ThreadStart(Concatenate))
                    {
                        IsBackground = true
                    };
                    trd.Start();
                }
                Running = false;
            };
            bw.RunWorkerAsync();
        }

        private void Concatenate()
        {
            while (Status.Count > 0)
            {
                Thread.Sleep(500);
            }
            Status.Add("Merging segments...");
            var files = new List<string>();
            for (int i = 0; i < Chunks.Length; i++)
                files.Add("file '" + Name.Replace(Tempdir, "").Replace("'","\'\\'\'") + "\\" + i.ToString("00000").ToString() + "." + Job + "'");
            System.IO.File.WriteAllLines(Tempdir + "concat.txt", files.ToArray());
            Process ffconcat = new Process();
            Func.Setinicial(ffconcat, 3);
            string b = A_Job == "m4a" ? "-bsf:a aac_adtstoasc " : "";
            b += Extension == "mp4" ? "-movflags faststart " : "";
            string f = Spd != 1 ? " -itsscale " + Spd : "";
            if (System.IO.File.Exists(Name + "\\audio." + A_Job))
                ffconcat.StartInfo.Arguments = " -y -f concat -safe 0" + f + " -i \"" + Tempdir + "concat.txt" + "\"" + (track_delay < 0 ? " -itsoffset " + track_delay + "ms" : "") + " -i \"" + Name + "\\audio." + A_Job + "\" -i \"" + File + "\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0? " + b + "\"" + Dir + Path.GetFileName(Name) + "_Av1ador." + Extension + "\"";
            else
                ffconcat.StartInfo.Arguments = " -y -f concat -safe 0" + f + "  -i \"" + Tempdir + "concat.txt" + "\" -c:v copy -an -map 0:v:0 -map_metadata -1 " + b + "\"" + Dir + Path.GetFileNameWithoutExtension(Name) + "_Av1ador." + Extension + "\"";
            ffconcat.Start();
            Regex regex = new Regex("time=([0-9]{2}):([0-9]{2}):([0-9]{2}.[0-9]{2})");
            Match compare;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                string output;
                while (!ffconcat.HasExited)
                {
                    output = ffconcat.StandardError.ReadLine();
                    if (output != null)
                    {
                        compare = regex.Match(output);
                        if (compare.Success)
                        {
                            double d = Double.Parse(compare.Groups[1].ToString()) * 3600 + Double.Parse(compare.Groups[2].ToString()) * 60 + Double.Parse(compare.Groups[3].ToString());
                            if (d > Merge)
                                Merge = d;
                        }
                    }
                }
            };
            bw.RunWorkerCompleted += (s, e) => {
                Status.Remove("Merging segments...");
                Cleanup();
                Finished = true;
            };
            bw.RunWorkerAsync();
        }

        private void Cleanup()
        {
            try
            {
                long size = new FileInfo(Dir + Path.GetFileName(Name) + "_Av1ador." + Extension).Length;
                if (size > 500 && Clean == 15)
                    Directory.Delete(Name, true);
                else
                {
                    if ((Clean & 2) != 0)
                        System.IO.File.Delete(Name + "\\segments.txt");
                    if ((Clean & 4) != 0)
                        foreach (string f in Directory.GetFiles(Name, "*." + Job).Where(i => i.EndsWith("." + Job)))
                            System.IO.File.Delete(f);
                    if ((Clean & 8) != 0)
                        System.IO.File.Delete(Name + "\\audio." + A_Job);
                }
            } catch { }
            try
            {
                System.IO.File.Delete(Tempdir + "concat.txt");
            } catch { }
        }

        public void Background(Segment chunk)
        {
            string log = chunk.Start();
            if (log == "Retry")
                log = chunk.Start();
            if (log.Trim() != "" && log != "Retry" && !Failed)
            {
                Failed = true;
                Set_state(true);
                if (unattended || MessageBox.Show(log, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                {
                    Status = new List<string>
                    {
                        "Failed"
                    };
                    if (!unattended)
                        Failed = false;
                    Chunks = null;
                }
            }
            if (!Running && !Failed)
                Encoding();
        }

        public string Replace_times(string str, string file, string name, string to, [Optional] double seek, [Optional] double ss, [Optional] int bitrate)
        {
            string ss1 = seek > 0 ? "-ss " + seek.ToString() : "";
            str = str.Replace("!file!", file).Replace("!duration!", "-to " + to);
            string ss2 = ss > 0 ? "-ss " + ss.ToString() : "";
            str = str.Replace("!seek!", ss1).Replace("!start!", ss2);
            if (bitrate > 0)
                str = str.Replace("!bitrate!", bitrate.ToString());
            str = str.Replace("!log!.log", name.Replace("\\", "\\\\") + ".log");
            str = str.Replace("!log!", name);
            return str.Replace("!name!", name).Replace("transforms.trf", name.Replace(@"\", @"\\") + ".trf");
        }

        public int Get_segment(int w, int x, double duration, double time)
        {
            time = time > 0 ? time : (double)x * duration / (double)w;
            for (int i = 0; i < Splits.Count - 1; i++)
            {
                if (time < Double.Parse(Splits[i + 1]) && time >= Double.Parse(Splits[i]))
                    return i;
            }
            return -1;
        }

        public void Set_fps_filter(List<string> vf)
        {
            string str = "";
            foreach(string f in vf)
            {
                if (f.Contains("fps=fps=") || f.Contains("framestep="))
                    str += f + ",";
            }
            Fps_filter = str;
        }

        public void Clear_splits(string file)
        {
            string name = Tempdir + Path.GetFileNameWithoutExtension(file);
            if (System.IO.File.Exists(name + "\\segments.txt"))
                System.IO.File.Delete(name + "\\segments.txt");
        }

        public void Set_state(bool stop = false)
        {
            if (stop)
                watch.Reset();
            if (Chunks != null)
                foreach (Segment chunk in Chunks)
                {
                    chunk.Stop = stop;
                    chunk.Frames = 0;
                }
        }
    }
    internal class Segment
    {
        public bool Completed { get; set; }
        public bool Encoding { get; set; }
        public long Size { get; set; }
        public double Bitrate { get; set; }
        public double Length { get; set; }
        public int Frames { get; set; }
        public double Progress { get; set; }
        public string Arguments { get; set; }
        public string Pathfile { get; set; }
        public bool Stop { get; set; }
        public bool Credits { get; set; }
        public int Retry { get; set; }
        public bool Last { get; set; }
        public bool Compare { get; set; }
        private bool retry;
        private string output;

        public string Start()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Encoding = true;
            Process ffmpeg = new Process();
            Func.Setinicial(ffmpeg, 3, Credits ? Func.Worsen_crf(Func.Replace_gs(Arguments, 0)) : Arguments);
            File.WriteAllText(Pathfile + ".txt", "ffmpeg" + ffmpeg.StartInfo.Arguments + "\r\n");
            if (Arguments.Contains("libaom-av1"))
            {
                retry = true;
                if (Retry > 0)
                {
                    output = "";
                    ffmpeg.StartInfo.Arguments = Func.Param_replace(ffmpeg.StartInfo.Arguments, "threads", 1.ToString());
                }
            }
            bool multi = ffmpeg.StartInfo.Arguments.Contains("&& ffmpeg");
            if (multi)
            {
                string pass = ffmpeg.StartInfo.Arguments;
                int pos1 = pass.IndexOf("&& ffmpeg");
                while (pos1 > -1 && !Stop)
                {
                    ffmpeg.StartInfo.Arguments = pass.Substring(0, pos1);
                    pass = pass.Substring(pos1 + 9);
                    pos1 = pass.IndexOf("&& ffmpeg");
                    ffmpeg.Start();
                    ffmpeg.StandardError.ReadToEnd();
                }
                ffmpeg.StartInfo.Arguments = pass;
            }
            Stopwatch watch = Stopwatch.StartNew();
            watch.Start();
            ffmpeg.Start();
            ffmpeg.PriorityClass = ProcessPriorityClass.BelowNormal;
            Regex regex = new Regex("time=([0-9]{2}):([0-9]{2}):([0-9]{2}.[0-9]{2})", RegexOptions.RightToLeft);
            Regex regex_frames = new Regex("frame=[ ]*([0-9]+) ", RegexOptions.RightToLeft);
            Match compare;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += (s, e) =>
            {
                bool jump = false;
                Stopwatch stopwatch = new Stopwatch();
                while (!Stop && !jump)
                {
                    Process crash = new Process();
                    try
                    {
                        crash = Process.GetProcessById(ffmpeg.Id);
                    }
                    catch
                    {
                        jump = true;
                    }
                    if (ffmpeg.HasExited || (crash != null && crash.HasExited))
                        jump = true;
                    stopwatch.Start();
                    output += jump ? ffmpeg.StandardError.ReadToEnd() : ffmpeg.StandardError.ReadLine() + "\r\n";
                    stopwatch.Stop();
                    Thread.Sleep((int)Math.Min(stopwatch.ElapsedMilliseconds / 4, 100));
                    stopwatch.Reset();
                }
            };
            bw.RunWorkerAsync();
            int frames_before = 0;
            while (!Stop)
            {
                bool jump = false;
                Process crash = new Process();
                try
                {
                    crash = Process.GetProcessById(ffmpeg.Id);
                }
                catch
                { 
                    jump = true;
                }
                if (jump || ffmpeg.HasExited || (crash != null && crash.HasExited))
                {
                    jump = true;
                    Thread.Sleep(1000);
                }

                if (output != null)
                {
                    if (retry && Retry < 2 && (output.Contains("error while decoding") || output.Contains("slice in a frame missing")))
                    {
                        Retry++;
                        if (Retry == 1)
                        {
                            Stop = true;
                            break;
                        }
                    }
                    if (output.Length > 1500)
                        output = output.Substring(output.Length - 1500);

                    compare = regex.Match(output);
                    if (compare.Success)
                    {
                        double d = Double.Parse(compare.Groups[1].ToString()) * 3600 + Double.Parse(compare.Groups[2].ToString()) * 60 + Double.Parse(compare.Groups[3].ToString());
                        if (d > Progress)
                        {
                            Progress = d;
                            compare = regex_frames.Match(output);
                            if (compare.Success)
                            {
                                int total_frames = int.Parse(compare.Groups[1].ToString());
                                Frames += total_frames - frames_before;
                                frames_before = total_frames;
                            }
                        }
                    }
                }
                if (File.Exists(Pathfile))
                {
                    Size = new FileInfo(Pathfile).Length;
                    Bitrate = Size / (double)1024 * (double)8 / Length;
                }
                if (jump)
                    break;
                Thread.Sleep(250);
            }
            Encoding = false;
            if (Stop)
            {
                while (File.Exists(Pathfile))
                {
                    try
                    {
                        File.Delete(Pathfile);
                        break;
                    }
                    catch
                    {
                        try
                        {
                            ffmpeg.Kill();
                        }
                        catch { }
                        Thread.Sleep(1000);
                    }
                }
                Progress = 0;
                Stop = false;
                if (Retry == 1)
                    return "Retry";
            }
            else
            {
                Completed = true;
                if (File.Exists(Pathfile))
                {
                    Size = new FileInfo(Pathfile).Length;
                    Bitrate = Size / (double)1024 * (double)8 / Length;
                }
                if (Progress == 0 && Size < 500)
                {
                    File.AppendAllText(Pathfile + ".txt", output);
                    return output;
                }
                if (Compare && Math.Abs(Video.Get_duration(Video.Get_info(Pathfile), out string _, Pathfile) - Length) > 2 && !Last)
                    return Pathfile + "\r\nThe segment has finished but there is a mismatch in the duration.\r\nUsually this means that ffmpeg crashed.";
            }
            File.Delete(Pathfile + ".txt");
            if (multi)
            {
                if (File.Exists(Pathfile + "-0.log"))
                    File.Delete(Pathfile + "-0.log");
                if (File.Exists(Pathfile + "-0.log.mbtree"))
                    File.Delete(Pathfile + "-0.log.mbtree");
                if (File.Exists(Pathfile + ".trf"))
                    File.Delete(Pathfile + ".trf");
                if (File.Exists(Pathfile + ".log.reuse"))
                    File.Delete(Pathfile + ".log.reuse");
                if (File.Exists(Pathfile + ".log.reuse.temp"))
                    File.Delete(Pathfile + ".log.reuse.temp");
            }
            return "";
        }
    }
}
