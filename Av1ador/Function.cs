using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Av1ador
{
    public class Func
    {
        public static readonly string bindir = "bin\\";

        public static string[] Exes()
        {
            string msg = "";
            string[] output = new string[3];
            Process process = new Process();
            Setinicial(process, 3);
            try
            {
                process.Start();
                output[0] = process.StandardError.ReadToEnd();
            }
            catch
            {
                msg += "ffmpeg.exe\n";
            }
            Setinicial(process, 2);
            try
            {
                process.Start();
            }
            catch
            {
                msg += "ffprobe.exe\n";
            }
            Setinicial(process, 1);
            try
            {
                process.Start();
            }
            catch
            {
                msg += "mpv.exe\n";
            }
            if (msg != "")
                msg = "The following dependencies cannot be found:\n\n" + msg;
            if (msg != "")
                if (MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                    Environment.Exit(0);
            Setinicial(process, 3, " -hide_banner -init_hw_device list");
            process.Start();
            string types = process.StandardOutput.ReadToEnd();
            if (types.Contains("opencl"))
            {
                process.StartInfo.Arguments = " -hide_banner -v debug -init_hw_device opencl";
                process.Start();
                output[1] = new Regex(@"[0-9]+\.[0-9]+: ").Match(process.StandardError.ReadToEnd()).Value.Replace(":", "").Trim();
            }
            else
            {
                if (MessageBox.Show("No OpenCL device found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                    Environment.Exit(0);
            }
            if (types.Contains("vulkan"))
            {
                process.StartInfo.Arguments = " -hide_banner -v debug -init_hw_device vulkan";
                process.Start();
                output[2] = new Regex(@"[0-9\.]+:").Match(new Regex(@"GPU listing:[\n\r]*.*[0-9]+: ").Match(process.StandardError.ReadToEnd()).Value).Value.Replace(":", "").Trim();
            }
            return output;
        }

        public static void Setinicial(Process process, int id, [Optional] string args)
        {
            string exe = bindir;
            switch (id)
            {
                case 1:
                    exe += "mpv.exe";
                    break;
                case 2:
                    exe += "ffprobe.exe";
                    break;
                default:
                    exe += "ffmpeg.exe";
                    break;
            }
            process.StartInfo.FileName = exe;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.EnableRaisingEvents = true;
            process.StartInfo.Arguments = args;
        }

        public static void Update_combo(ToolStripComboBox combobox, string[] items, bool keep, string text = "")
        {
            string sel = combobox.Text;
            combobox.Items.Clear();
            string t = combobox.Text;
            foreach (string str in items)
            {
                combobox.Items.Add(str.Replace("*", ""));
                if (str.Contains("*"))
                    t = str.Replace("*", "");
                if (keep && str == sel)
                    t = sel;
                if (text != "")
                    t = text;
                combobox.Text = t;
            }
            if (combobox.Text == "")
            {
                if (combobox.Items.Count == 0)
                {
                    combobox.Items.Add("0");
                    combobox.Enabled = false;
                }
                else
                    combobox.Enabled = true;
                combobox.SelectedIndex = 0;
            }
        }

        public static string Param_replace(string str, string param, string replace)
        {
            str = Regex.Replace(str, "-(" + param + " )[0-9]+ ", m => replace == "" ? "" : "-" + m.Groups[1].Value + replace + " ");
            str = Regex.Replace(str, "([\\s:]+)(" + param + "=)[0-9]+", m => replace == "" ? "" : m.Groups[1].Value + m.Groups[2].Value + replace);
            return str.Replace("::", ":").Replace("params :", "params ").Replace(": ", " ");
        }

        public static string Replace_gs(string str, int gs_level)
        {
            str = Param_replace(str, "denoise-noise-level", gs_level.ToString());
            return Param_replace(str, "film-grain", gs_level.ToString());
        }

        public static string Worsen_crf(string param)
        {
            if (param.Contains("-crf"))
            {
                Regex regex = new Regex("-crf ([0-9]+)");
                Match compare = regex.Match(param);
                int crf = int.Parse(compare.Groups[1].Value.ToString());
                if (crf < 51)
                {
                    crf = crf * 4 / 3;
                    if (crf > 51)
                        crf = 51;
                }
                param = Regex.Replace(param, "(-crf )[0-9]+", m => m.Groups[1].Value + crf.ToString());
            }
            return param;
        }

        public static int Chbox2int(ToolStripComboBox cb)
        {
            if (cb == null)
                return 0;
            else if (cb.Text == null || cb.Text == "")
                return 0;
            else
                return int.Parse(cb.Text.Split(' ')[0]);
        }

        public static void DrawItems(ListBox list, DrawItemEventArgs e)
        {
            e.DrawBackground();

            bool isItemSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            if (e.Index >= 0 && e.Index < list.Items.Count)
            {
                e.Graphics.FillRectangle(isItemSelected ? Brushes.LightSteelBlue : (e.Index % 2 == 0 ? Brushes.OldLace : Brushes.White), e.Bounds);
                e.Graphics.DrawString(list.Items[e.Index].ToString(), e.Font, Brushes.Black, e.Bounds);
            }
        }

        public static string[] Find_w_h(List<string> list)
        {
            Regex[] regex = new Regex[]
            {
                new Regex("crop=([0-9]+):([0-9]+)"),
                new Regex("w=([0-9]+):h=([0-9]+)")
            };
            for (int j = 0; j < regex.Length; j++)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Match compare = regex[j].Match(list[i]);
                    if (compare.Success)
                        return new string[] { compare.Groups[1].Value, compare.Groups[2].Value };
                }
            }
            return new string[] { };
        }

        public static double Get_speed(List<string> list)
        {
            Regex regex = new Regex(@"setpts=[pts\*]*([0-9\./]+)", RegexOptions.IgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                Match compare = regex.Match(list[i]);
                try
                {
                    if (compare.Success)
                        return Convert.ToDouble(new DataTable().Compute(compare.Groups[1].Value, null));
                }
                catch { }
            }
            return 1.0;
        }

        public static double[] Upscale_ratio(List<string> list)
        {
            Regex regex = new Regex(@"iw\*([0-9\.]+).*ih\*([0-9\.]+)");
            for (int i = 0; i < list.Count; i++)
            {
                Match compare = regex.Match(list[i]);
                if (compare.Success)
                    return new double[] { double.Parse(compare.Groups[1].Value), double.Parse(compare.Groups[2].Value) };
            }
            return new double[] { 1, 1 };
        }

        public static double Scale(Video v1, Video v2, double scale, double w = 0)
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            int rw = v1.Width;
            int rh = v1.Height;
            if (Math.Abs(v1.Rotation) == 90)
            {
                rw = v1.Height;
                rh = v1.Width;
            }
            return v1.Dar <= v2.Dar || rw == v2.Width * scale || w == v2.Width * scale
                ? ((double)v2.Width * scale * v2.Sar) / ((double)rw * (v1.Sar > 1 ? v1.Sar : 1.0)) : (double)v2.Height * scale / (double)rh;
        }

        public static double Median(int[] a)
        {
            Array.Sort(a);
            int n = a.Count();
            if (n % 2 != 0)
                return (double)a[n / 2];

            return (double)(a[(n - 1) / 2] + a[n / 2]) / 2.0;
        }

        public static double Crf2br(double crf)
        {
            crf *= 100.0;
            return Math.Pow(5000000 / (crf + 1600) + crf / 29 - 800, 2) / 100 + 50;
        }

        public static bool Preview(string vf)
        {
            string[] filters = new string[] { "crop", "eq¡", "delogo", "smartblur", "gradfun", "framestep", "fps¡", "nlmeans", "nlmeans_opencl", "hqdn3d", "rotate", "hflip", "vflip", "setpts" };
            foreach (string filter in filters)
            {
                if (vf.IndexOf(filter.Replace('¡', '=')) > -1)
                    return true;
            }
            return false;
        }

        public static Color Heat(int usage)
        {
            int r, gb;
            r = 255 - (100 - usage) * 35 / 100;
            gb = 220 - usage * 22 / 10;
            return Color.FromArgb(r, gb, gb);
        }

        public static int Rule(int min, int max, int x, int l)
        {
            int r = max - min;
            if (x > max)
                return l;
            else if (x <= min)
                return 0;
            return (x - min) * 100 / r * l / 100;
        }

        public static string Size_unit(double size)
        {
            string unit = "MB";
            if (size > 1024)
            {
                size = Math.Round(size / 1024.0, 1);
                unit = "GB";
            }
            return size.ToString() + unit;
        }

        public static List<string> Concat(List<string>[] list, bool sort = true)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var concatenated = list[0];
            for (int i = 1; i < list.Length; i++)
            {
                if (i > 0)
                    concatenated = concatenated.Concat(list[i].Skip(1).Take(list[i].Count - 1)).ToList();
                else
                    concatenated = concatenated.Concat(list[i]).ToList();
            }
            if (!sort)
                return concatenated;
            try
            {
                List<double> result = concatenated.Select(x => double.Parse(x)).ToList();
                result.Sort();
                return result.Select(i => i.ToString()).ToList();
            } catch
            {
                return concatenated;
            }
        }
    }
}
