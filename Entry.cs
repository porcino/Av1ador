using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Av1ador
{
    public class Entry
    {
        private static bool shouldsave;
        public string File { get; set; }
        public string Vf { get; set; }
        public string Af { get; set; }
        public string Gs { get; set; }
        public double Credits { get; set; }
        public double CreditsEnd { get; set; }
        public int Cv { get; set; }
        public string Bits { get; set; }
        public string Param { get; set; }
        public int Crf { get; set; }
        public int Ba { get; set; }
        public string Bv { get; set; }
        public int Track { get; set; }
        public string Resolution { get; set; }

        
        public static int Index(string file, ListBox list)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                Entry entry = list.Items[i] as Entry;
                if (entry.File == file)
                    return i;
            }
            return -1;
        }

        public static void Draw(ListBox list, DrawItemEventArgs e)
        {
            e.DrawBackground();

            bool isItemSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            if (e.Index >= 0 && e.Index < list.Items.Count)
            {
                e.Graphics.FillRectangle(isItemSelected ? Brushes.LightSteelBlue : (e.Index % 2 == 0 ? Brushes.OldLace : Brushes.White), e.Bounds);
                Entry entry = (Entry)list.Items[e.Index];
                e.Graphics.DrawString(entry.File, e.Font, Brushes.Black, e.Bounds);
            }
        }

        public static void Save(ListBox list, bool save = false)
        {
            if (shouldsave || save)
            {
                shouldsave = false;
                Save_entries(list);
            }
        }

        public static void Load(ListBox list)
        {
            if (System.IO.File.Exists("queue.xml"))
            {
                System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(Entry[]));
                StreamReader file = new StreamReader("queue.xml");
                Entry[] entries = (Entry[])reader.Deserialize(file);
                file.Close();
                foreach(Entry entry in entries)
                    list.Items.Add(entry);
            }
        }

        public static void Update(int col, string file, ListBox list, ListBox vf, ListBox af, string gs, double credits, double creditsend, int cv, string bits, string param, int crf, int ba, string bv, int track, string res)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                Entry entry = list.Items[i] as Entry;
                if (file != "" && entry.File == file)
                {
                    switch (col)
                    {
                        case 1:
                            string j = String.Join("¡", vf.Items.OfType<string>().ToArray());
                            string k = String.Join("¡", af.Items.OfType<string>().ToArray());
                            shouldsave = j != entry.Vf || k != entry.Af;
                            if (shouldsave)
                            {
                                entry.Vf = j;
                                entry.Af = k;
                            }
                            break;
                        case 2:
                            shouldsave = gs != entry.Gs;
                            if (shouldsave)
                                entry.Gs = gs;
                            break;
                        case 3:
                            shouldsave = credits != entry.Credits || creditsend != entry.CreditsEnd;
                            if (shouldsave)
                            {
                                entry.Credits = credits;
                                entry.CreditsEnd = creditsend;
                            }
                            break;
                        case 4:
                            shouldsave = cv != entry.Cv;
                            if (shouldsave)
                            {
                                entry.Cv = cv;
                                entry.Param = "";
                            }
                            break;
                        case 5:
                            shouldsave = bits != entry.Bits;
                            if (shouldsave)
                                entry.Bits = bits;
                            break;
                        case 6:
                            shouldsave = param != entry.Param;
                            if (shouldsave)
                                entry.Param = param;
                            break;
                        case 7:
                            shouldsave = crf != entry.Crf;
                            if (shouldsave)
                                entry.Crf = crf;
                            break;
                        case 8:
                            shouldsave = ba != entry.Ba;
                            if (shouldsave)
                                entry.Ba = ba;
                            break;
                        case 9:
                            shouldsave = bv != entry.Bv;
                            if (shouldsave)
                                entry.Bv = bv;
                            break;
                        case 10:
                            shouldsave = track != entry.Track;
                            if (shouldsave)
                                entry.Track = track;
                            break;
                        case 11:
                            shouldsave = res != entry.Resolution;
                            if (shouldsave)
                                entry.Resolution = res;
                            break;
                    }
                    list.Items[i] = entry;
                }
            }
        }

        private static void Save_entries(ListBox list)
        {
            var writer = new System.Xml.Serialization.XmlSerializer(typeof(Entry[]));
            var wfile = new StreamWriter(@"queue.xml");
            Entry[] entries = new Entry[list.Items.Count];
            for (int i = 0; i < list.Items.Count; i++)
                entries[i] = list.Items[i] as Entry;
            writer.Serialize(wfile, entries);
            wfile.Close();
        }

        public static List<string> Filter2List(string f)
        {
            return f.Split(new char[] { '¡' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static bool Queued(ListBox list, string file)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                if ((list.Items[i] as Entry).File == file)
                    return true;
            }
            return false;
        }
    }
}
