﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Av1ador
{
    public class Func
    {
        public static string Exes()
        {
            string msg = "";
            string output = "";
            Process process = new Process();
            Setinicial(process, 3);
            try
            {
                process.Start();
                output = process.StandardError.ReadToEnd();
            }
            catch
            {
                msg += "ffmpeg.exe\n";
            }
            process.StartInfo.FileName = "ffprobe.exe";
            try
            {
                process.Start();
            }
            catch
            {
                msg += "ffprobe.exe\n";
            }
            process.StartInfo.FileName = "mpv.exe";
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
            return output;
        }
        public static void Setinicial(Process process, int id, [Optional] string args)
        {
            string exe;
            switch (id)
            {
                case 1:
                    exe = "mpv.exe";
                    break;
                case 2:
                    exe = "ffprobe.exe";
                    break;
                default:
                    exe = "ffmpeg.exe";
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

        public static string Replace_gs(string param, int gs_level)
        {
            param = Regex.Replace(param, "(denoise-noise-level=)[0-9]+", m => m.Groups[1].Value + gs_level.ToString());
            return Regex.Replace(param, "(film-grain=)[0-9]+", m => m.Groups[1].Value + gs_level.ToString());
        }

        public static string Replace_threads(string param, int threads)
        {
            param = Regex.Replace(param, "(threads=)[0-9]+", m => m.Groups[1].Value + threads.ToString());
            return Regex.Replace(param, "(-threads )[0-9]+", m => m.Groups[1].Value + threads.ToString());
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

        public static void DrawItems(ListBox list, DrawItemEventArgs e, bool entry = false)
        {
            e.DrawBackground();

            bool isItemSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            if (e.Index >= 0 && e.Index < list.Items.Count)
            {
                e.Graphics.FillRectangle(isItemSelected ? Brushes.LightSteelBlue : (e.Index % 2 == 0 ? Brushes.OldLace : Brushes.White), e.Bounds);
                if (entry)
                {
                    Entry en = (Entry)list.Items[e.Index];
                    e.Graphics.DrawString(en.File, e.Font, Brushes.Black, e.Bounds);
                }
                else
                    e.Graphics.DrawString(list.Items[e.Index].ToString(), e.Font, Brushes.Black, e.Bounds);
            }
        }

        public static string[] Find_w_h(List<string> list)
        {
            Regex regex = new Regex("w=([0-9]+):h=([0-9]+)");
            for (int i = 0; i < list.Count; i++)
            {
                Match compare = regex.Match(list[i]);
                if (compare.Success)
                    return new string[] { compare.Groups[1].Value, compare.Groups[2].Value };
            }
            return new string[] { };
        }

        public static double Scale(Video v1, Video v2, double scale, double w = 0)
        {
            return v1.Dar <= v2.Dar || v1.Width == v2.Width * scale || w == v2.Width * scale
                ? ((double)v2.Width * scale * v2.Sar) / ((double)v1.Width * (v1.Sar > 1 ? v1.Sar : 1.0)) : (double)v2.Height * scale / (double)v1.Height;
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
            string[] filters = new string[] { "crop", "eq", "delogo", "smartblur" };
            foreach (string filter in filters)
            {
                if (vf.IndexOf(filter + "=") > -1)
                    return true;
            }
            return false;
        }
    }
}