using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Av1ador
{
    internal class Encoder : ICloneable
    {
        public const string resdir = "resource\\\\";
        private readonly string gpu_name;
        private int ba;
        private readonly string[] v = new string[] { "AV1 (aom)", "AV1 (svt)", "AV1 (rav1e)", "AV1 (nvenc)", "VP9 (vpx)", "HEVC (x265)", "HEVC (nvenc)", "H264 (x264)", "H264 (nvenc)", "MPEG4 (xvid)" };
        private readonly string[] j = new string[] { "mkv", "ivf" };
        private readonly string[] a = new string[] { "aac", "opus", "vorbis", "mp3" };
        private readonly string[] c = new string[] { "2 (stereo)", "6 (surround 5.1)", "8 (surround 7.1)", "1 (mono)" };
        private string speed_str;
        public string[] Resos { get; set; }
        public string Param { get; set; }
        public int Max_crf { get; set; }
        public int Crf { get; set; }
        public int Cores { get; }
        public int Threads { get; set; }
        public string Cv { get; set; }
        public string Ca { get; set; }
        public string Ch { get; set; }
        public int V_kbps { get; set; }
        public int A_kbps { get; set; }
        public int A_min { get; set; }
        public int A_max { get; set; }
        public string Job { get; set; }
        public string A_Job { get; set; }
        public string Speed { get; set; }
        public bool Hdr { get; set; }
        public int Bits { get; set; }
        public string Params { get; set; }
        public string Color { get; set; }
        public int Gs { get; set; }
        public int Gs_level { get; set; }
        public string[] V_codecs { get; set; }
        public string[] A_codecs { get; set; }
        public string[] Channels { get; set; }
        public string[] Bit_depth { get; set; }
        public string[] Presets { get; set; }
        public List<string> Vf { get; set; }
        public List<string> Af { get; set; }
        public double Playtime { get; set; }
        public int Out_w { get; set; }
        public int Out_h { get; set; }
        public int Out_fps { get; set; }
        public double Out_spd { get; set; } = 1;
        public bool Predicted { get; set; }
        public bool Libfdk { get; set; }
        public bool Libplacebo { get; set; }
        public double Rate { get; set; }
        public string Multipass { get; set; }
        public string Vbr_str { get; set; }

        public int Ba
        {
            get
            { return ba; }
            set
            {
                if (int.Parse(Ch) > 0 && Ca == "libfdk_aac")
                {
                    if (value < 8)
                    {
                        if (!Af.Contains("aresample=8000"))
                            Af.Add("aresample=8000");
                        if (!Af.Contains("highpass=f=200"))
                            Af.Add("highpass=f=200");
                        if (!Af.Contains("adynamicsmooth"))
                            Af.Add("adynamicsmooth");
                    }
                    else
                    {
                        if (Af.Contains("aresample=8000"))
                            Af.RemoveAll(s => s.StartsWith("aresample="));
                        if (Af.Contains("highpass=f=200"))
                            Af.RemoveAll(s => s.StartsWith("highpass="));
                        if (Af.Contains("adynamicsmooth"))
                            Af.RemoveAll(s => s.StartsWith("adynamicsmooth"));
                    }
                }
                ba = value;
            }
        }

        public Encoder()
        {
            Resos = new string[] { "4320p", "2160p", "1080p", "900p", "720p", "576p", "480p", "360p", "240p", "160p" };
            Max_crf = 63;
            Crf = 36;
            Cores = Environment.ProcessorCount > 16 ? 16 : Environment.ProcessorCount;
            Threads = Cores / 2;
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                    gpu_name = obj["Name"].ToString();
            A_max = 320;
            Ch = "2";
            Gs_level = 0;
            Color = "";
            Vf = new List<string>();
            Af = new List<string>();
            Multipass = "";
        }
        private string[] CheckNvidia(string[] vtags)
        {
            if (gpu_name.Contains("NVIDIA"))
                return vtags;
            var nonv = new List<string>();
            foreach (var vtag in vtags)
                if (!vtag.Contains("nvenc"))
                    nonv.Add(vtag);
            return nonv.ToArray();

        }
        public void Format(string format)
        {
            switch (format)
            {
                case "mp4":
                    A_codecs = new string[] { a[0], a[1], a[3] };
                    V_codecs = new string[] { v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]  };
                    V_codecs = CheckNvidia(V_codecs);
                    break;
                case "mkv":
                    A_codecs = new string[] { a[0], a[1], a[2], a[3] };
                    V_codecs = new string[] { v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9] };
                    V_codecs = CheckNvidia(V_codecs);
                    break;
                case "webm":
                    A_codecs = new string[] { a[1], a[2] };
                    V_codecs = new string[] { v[0], v[1], v[2], v[3], v[4] };
                    break;
                case "avi":
                    A_codecs = new string[] { a[3], a[1] };
                    V_codecs = new string[] { v[9], v[4], v[5], v[6], v[7], v[8] };
                    V_codecs = CheckNvidia(V_codecs);
                    break;
            }
        }
        public void Set_video_codec(string codec)
        {
            Max_crf = 51;
            Gs = 0;
            Rate = 1.0;
            Multipass = "";
            if (codec == v[0])
            {
                Cv = "libaom-av1";
                Max_crf = 63;
                Crf = 36;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "2", "3", "*4", "5", "6", "7", "8 (fastest)" };
                speed_str = "-cpu-used ";
                Params = "-tune 1 -enable-restoration 0 -threads !threads! -tiles 2x1 -keyint_min !minkey! -g !maxkey! -aom-params sharpness=4:max-gf-interval=28:gf-max-pyr-height=4:disable-trellis-quant=2:denoise-noise-level=!gs!:enable-dnl-denoising=0:denoise-block-size=16:arnr-maxframes=2:arnr-strength=4:max-reference-frames=4:enable-rect-partitions=0:enable-filter-intra=0:enable-masked-comp=0:enable-qm=1:qm-min=1:enable-obmc=0 -strict -2";//:global-motion-method=0
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Gs = 100;
                Rate = 0.82;
                Vbr_str = "-undershoot-pct 60 -overshoot-pct 0 -minsection-pct 60 -maxsection-pct 96";
            }
            else if (codec == v[1])
            {
                Cv = "libsvtav1";
                Max_crf = 63;
                Crf = 36;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "2", "3", "4", "5", "*6", "7", "8", "9", "10", "11", "12 (fastest)" };
                speed_str = "-preset ";
                Params = "-svtav1-params tune=0:fast-decode=0:irefresh-type=2:keyint=!maxkey!:enable-overlays=1:enable-restoration=0:film-grain-denoise=0:film-grain=!gs!";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Gs = 50;
                Rate = 0.85;
                Vbr_str = "undershoot-pct=60:overshoot-pct=0:minsection-pct=60:maxsection-pct=97";
            }
            else if (codec == v[2])
            {
                Cv = "librav1e";
                Max_crf = 255;
                Crf = 120;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "2", "3", "4", "*5", "6", "7", "8", "9", "10 (fastest)" };
                speed_str = ":speed=";
                Params = ":threads=!threads!:tiles=2:min-keyint=!minkey!:keyint=!maxkey!";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Rate = 0.87;
            }
            else if (codec == v[3])
            {
                Cv = "av1_nvenc";
                Crf = 32;
                Bit_depth = new string[] { "8", "10" };
                Job = j[0];
                Presets = new string[] { "*slow", "p7", "p6", "p5", "p4", "p3", "p2", "p1", "fast" };
                speed_str = "-preset:v ";
                Params = "-tile-columns 1 -tile-rows 1";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else if (codec == v[4])
            {
                Cv = "libvpx-vp9";
                Max_crf = 63;
                Crf = 36;
                Bit_depth = new string[] { "10", "8" };
                Job = j[1];
                Presets = new string[] { "0 (slowest)", "1", "*2", "3", "4", "5 (fastest)" };
                speed_str = "-speed ";
                Params = "-tile-columns 2 -tile-rows 1 -frame-parallel 0 -row-mt 1 -auto-alt-ref 6 -lag-in-frames 25 -keyint_min !minkey! -g !maxkey! -max-intra-rate 0 -enable-tpl 1 -arnr-maxframes 4 -arnr-strength 2";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
                Rate = 0.95;
                Vbr_str = "-undershoot-pct 60 -overshoot-pct 0";
            }
            else if (codec == v[5])
            {
                Cv = "libx265";
                Crf = 28;
                Bit_depth = new string[] { "10", "8" };
                Job = j[0];
                Presets = new string[] { "placebo", "veryslow", "*slower", "slow", "medium", "fast", "faster", "veryfast", "superfast", "ultrafast" };
                speed_str = "-preset ";
                Params = "-x265-params min-keyint=!minkey!:keyint=!maxkey!:early-skip=1:psy-rd=0.5:rdoq-level=2:psy-rdoq=2:selective-sao=2:rskip-edge-threshold=1:aq-mode=4:rect=0:subme=2:limit-tu=1:amp=0:hme=1:hme-search=star,umh,hex:hme-range=16,32,100:strong-intra-smoothing=0";
                Color = ":colorprim=1:transfer=1:colormatrix=1";
                Rate = 0.9;
                Multipass = "-pass 1 -passlogfile \"!log!\"";
            }
            else if (codec == v[6])
            {
                Cv = "hevc_nvenc";
                Crf = 32;
                Bit_depth = new string[] { "8", "10" };
                Job = j[0];
                Presets = new string[] { "*slow", "p7", "p6", "p5", "p4", "p3", "p2", "p1", "fast" };
                speed_str = "-preset:v ";
                Params = "-bf:v 3 -spatial-aq 1 -temporal-aq 1 -aq-strength 7 -b_ref_mode 1";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else if (codec == v[7])
            {
                Cv = "libx264";
                Crf = 27;
                Bit_depth = new string[] { "8", "10" };
                Job = j[0];
                Presets = new string[] { "placebo", "*veryslow", "slower", "slow", "medium", "fast", "faster", "veryfast", "superfast", "ultrafast" };
                speed_str = "-preset ";
                Params = "-x264opts threads=!threads!:min-keyint=!minkey!:keyint=!maxkey!:stitchable=1";
                Color = ":colorprim=bt709:transfer=bt709:colormatrix=bt709";
                Multipass = "-pass 1 -passlogfile \"!log!\"";
            }
            else if (codec == v[8])
            {
                Cv = "h264_nvenc";
                Crf = 32;
                Bit_depth = new string[] { "8" };
                Job = j[0];
                Presets = new string[] { "*slow", "p7", "p6", "p5", "p4", "p3", "p2", "p1", "fast" };
                speed_str = "-preset ";
                Params = "-bf:v 4 -b_adapt 0 -spatial-aq 1 -temporal-aq 1 -aq-strength 7 -coder 1 -b_ref_mode 2";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
            else
            {
                Cv = "libxvid";
                Max_crf = 31;
                Crf = 16;
                Bit_depth = new string[] { "8" };
                Job = j[0];
                Presets = new string[] { "" };
                speed_str = "";
                Params = "-g !maxkey! -mbd rd -trellis 1 -flags +mv4+aic+cgop -variance_aq 1 -bf 3 -b_qoffset 10";
                Color = " -color_primaries 1 -color_trc 1 -colorspace 1";
            }
        }
        public void Set_audio_codec(string codec, int ch)
        {
            if (codec == a[0])
            {
                if (Libfdk)
                {
                    Ca = "libfdk_aac";
                    Channels = new string[] { c[0], c[1], c[2] };
                }
                else
                {
                    Ca = "aac";
                    Channels = new string[] { c[0], c[1], c[2], c[3] };
                }
                A_kbps = 112;
                A_min = 2;
                A_Job = "m4a";
            }
            else if (codec == a[1])
            {
                Ca = "libopus";
                A_kbps = 96;
                A_min = 5;
                A_Job = "ogg";
                Channels = new string[] { c[0], c[1], c[2], c[3] };
            }
            else if (codec == a[2])
            {
                Ca = "libvorbis";
                A_kbps = 128;
                A_min = 48;
                A_Job = "ogg";
                Channels = new string[] { c[0], c[1], c[2], c[3] };
            }
            else
            {
                Ca = "libmp3lame";
                A_kbps = 128;
                A_min = 32;
                A_Job = "mp3";
                Channels = new string[] { c[0], c[3] };
            }
            var channels = new List<string>();
            foreach (var str_ch in Channels)
                if (int.Parse(str_ch.Split(' ')[0]) <= ch)
                    channels.Add(str_ch);
            Channels = channels.ToArray();
            Ch = ch.ToString();
        }

        public string[] Calc_kbps(double total, double duracion, int fps, int kbps = 0)
        {
            if (kbps == 0)
                kbps = (int)Math.Floor(total * 8.0 * 1024.0 / duracion);
            //bitrate=!bitrate!-^(!hertz!/8000^)-^(!framerate!/10^)
            kbps -= fps / 10;
            kbps = kbps < 4 ? 4 : kbps;
            int reduccion = 16 - (((kbps * 4) - 4000) / (-253));
            int ba = A_kbps * (int.Parse(Ch) > 2 ? int.Parse(Ch) - 2 : int.Parse(Ch)) * reduccion / 64;
            ba = ba > A_kbps ? (ba - A_kbps) / 2 + A_kbps : ba;
            kbps -= ba;
            if (ba < 216 && int.Parse(Ch) > 2)
                Ch = "2";
            return new string[3] { kbps.ToString(), ba.ToString(), Ch };
        }

        public double Calc_total(int kbps, int ba, double duracion, int fps)
        {
            return Math.Round((double)(kbps + ba + fps / 10) * duracion / (double)8 / (double)1024, 1);
        }

        public string Params_replace(int fps)
        {
            int minkey = fps > 1 ? fps : 24;
            int maxkey = fps > 1 ? fps * 10 : 240;
            if (Cv == "libxvid")
                maxkey /= 2;
            Param = Params.Replace("!minkey!", minkey.ToString()).Replace("!maxkey!", maxkey.ToString()).Replace("!threads!", Threads.ToString()).Replace("!gs!", Gs_level.ToString());
            return Param;
        }

        private string Bit_Format([Optional] int bits)
        {
            if ((Bits == 10 && bits == 0) || bits == 10)
                return "format=yuv420p10le";
            else
                return "format=yuv420p";
        }

        private string Bit_OCL()
        {
            if (Bits == 10)
                return "format=p010";
            else
                return "format=nv12";
        }

        public string Filter_convert(string f)
        {
            if (f.IndexOf("nlmeans") > -1)
            {
                Regex regex = new Regex(@"_opencl=s=([0-9]+):p=([0-9]+):r=([0-9]+)");
                Match match = regex.Match(f);
                if (match.Success)
                    return "nlmeans=s=" + match.Groups[1].Value + ":p=" + match.Groups[2].Value + ":r=" + match.Groups[3].Value;
            }
            return f;
        }

        public void Vf_add(string f, [Optional] string v, [Optional] string a, [Optional] string b, [Optional] string c)
        {
            if (f == "fps")
            {
                Vf.RemoveAll(s => s.StartsWith("fps"));
                Vf.RemoveAll(s => s.StartsWith("framestep="));
                if (v != "Same")
                {
                    if (v.Contains("1/2"))
                        Vf.Insert(0, "framestep=2");
                    else if (v.Contains("1/3"))
                        Vf.Insert(0, "framestep=3");
                    else if (v.Contains("1/4"))
                        Vf.Insert(0, "framestep=4");
                    else
                        Vf.Insert(0, "fps=fps=" + v.Split(' ')[0]);
                }
            }
            else if (f == "scale")
            {
                Vf.RemoveAll(s => s.StartsWith("scale"));
                int ow = (int)Double.Parse(v);
                int oh = int.Parse(a);
                int iw = int.Parse(b);
                int ih = int.Parse(c);
                double cw = iw;
                double ch = ih;
                int index = -1;
                if (Vf.Count > 0)
                {
                    index = Math.Max(Vf.FindIndex(s => s.Contains("nnedi")), Vf.FindIndex(s => s.Contains("crop")));
                    index = Math.Max(index, Vf.FindIndex(s => s.Contains("glsl")));
                    if (index > -1)
                    {
                        string[] crop = Func.Find_w_h(Vf);
                        if (crop.Count() > 0)
                        {
                            cw = int.Parse(crop[0]);
                            ch = int.Parse(crop[1]);
                        }
                    }
                    else
                        index = Vf.FindIndex(s => s.StartsWith("fps"));
                }
                ow = (int)(Double.Parse(v) * (double)cw / (double)iw);
                oh = (int)(oh * (double)ch / (double)ih);
                ow = ow % 2 != 0 ? ow + 1 : ow;
                oh = oh % 2 != 0 ? oh - 1 : oh;
                Vf.Insert(index > -1 ? index + 1 : 0, "scale=w=" + ow + ":h=" + oh);
            }
            else if (f == "crop")
            {
                if (b == null)
                {
                    string[] dim = Func.Find_w_h(Vf);
                    if (dim.Count() > 0)
                    {
                        v = dim[0];
                        a = dim[1];
                    }
                    Vf.Add("crop=w=" + v + ":h=" + a + ":x=0:y=0");
                }
                else
                {
                    Vf.Remove("crop=w=Detecting:h=.:x=.:y=.");
                    Vf.Remove("crop=w=" + v + ":h=" + a + ":x=" + b + ":y=" + c);
                    Vf.Insert(0, "crop=w=" + v + ":h=" + a + ":x=" + b + ":y=" + c);
                }
            }
            else if (f == "Deband")
            {
                if (Libplacebo && Vf.FindIndex(s => s.Contains("opencl")) == -1)
                    Vf.Add(Bit_Format() + ",hwupload,libplacebo=deband=true:deband_iterations=1:deband_radius=8:deband_threshold=3:deband_grain=21,hwdownload," + Bit_Format());
                else
                    Vf.Add("gradfun=3:8");
            }
            else if (f == "delogo")
            {
                int w = int.Parse(v) * 2 / 10;
                int h = int.Parse(a) * 2 / 10;
                int x = int.Parse(v) * 7 / 10;
                int y = int.Parse(a) * 1 / 10;
                Vf.Add("delogo=x=" + x + ":y=" + y + ":w=" + w + ":h=" + h);
            }
            else if (f == "poi")
                Vf.Add("addroi=iw/8:ih/8:iw*3/4:ih*3/4:-1/40,addroi=0:0:iw/16:ih:1/27,addroi=iw*15/16:0:iw/16:ih:1/27,addroi=iw/16:0:iw*7/8:ih/16:1/40,addroi=iw/16:ih*15/16:iw*7/8:ih/16:1/40");
            else if (f == "deinterlace")
            {
                Vf.RemoveAll(s => s.StartsWith("nnedi"));
                if (v == "True")
                    Vf.Insert(0, "nnedi='weights=" + resdir + "nnedi3_weights.bin:field=a'");
            }
            else if (f == "autocolor")
            {
                Vf.RemoveAll(s => s.StartsWith("scale=in_color_matrix"));
                if (v != "" && v != "bt2020")
                    Vf.Add("scale=in_color_matrix=" + (v == "smpte170m" ? "bt601" : "auto") + ":out_color_matrix=" + (v == "smpte170m" ? "bt709" : "auto"));
            }
            else if (f == "Color adjustment")
                Vf.Add("eq=contrast=1.1:brightness=0.05:saturation=1.4:gamma=1.0");
            else if (f == "Sharpen")
                Vf.Add("smartblur=luma_radius=2:luma_strength=-1.0:luma_threshold=-3");
            else if (f == "Denoise")
                Vf.Add("format=pix_fmts=yuv420p,hwupload,nlmeans_opencl=s=3:p=15:r=7,hwdownload,format=pix_fmts=yuv420p");
            else if (f == "OpenCL")
                Vf.Add("\"curves=m=0/0 0.25/0.2 0.63/0.53 1/0.6:g=0.005/0 0.506/0.5 1/1,format=p010,hwupload,tonemap_opencl=tonemap=hable:desat=0:threshold=0:r=tv:p=bt709:t=bt709:m=bt709:" + Bit_OCL() + ",hwdownload," + Bit_OCL() + "\"");
            else if (f == "Vulkan")
                Vf.Add("\"curves=m=0/0 0.25/0.3 0.87/0.88 1/1," + Bit_Format(10) + ",hwupload,libplacebo=minimum_peak=4:gamut_mode=desaturate:tonemapping=hable:tonemapping_mode=rgb:tonemapping_crosstalk=0.04:range=tv:color_primaries=bt709:color_trc=bt709:colorspace=bt709:" + Bit_Format() + ",hwdownload," + Bit_Format() + "\"");
            else if (f == "anime4k")
                Vf.Add(Bit_Format() + ",hwupload,libplacebo='custom_shader_path=" + resdir + "Anime4K_Restore_CNN_" + (v == "1.5" ? "Soft_" : "") + "VL.glsl',libplacebo='w=iw*" + v + ":h=ih*" + v + ":custom_shader_path=" + resdir + "Anime4K_Upscale_Denoise_CNN_x2_VL.glsl',hwdownload," + Bit_Format());
            else if (f == "fsrcnnx")
                Vf.Add(Bit_Format() + ",hwupload,libplacebo='w=iw*2:h=ih*2:custom_shader_path=" + resdir + "FSRCNNX_x2_16-0-4-1.glsl',hwdownload," + Bit_Format());
            else if (f == "Stabilization")
                Vf.Insert(Vf.FindIndex(s => s.Contains("nnedi")) > -1 ? 1 : 0, "\"vidstabtransform=smoothing=6:crop=keep:zoom=0:optzoom=0:input='transforms.trf'\"");
            else if (f.Contains("90° clock"))
                Vf.Add("rotate=PI/2:ow=ih:oh=iw");
            else if (f.Contains("90° anti"))
                Vf.Add("rotate=-PI/2:ow=ih:oh=iw");
            else if (f == "180°")
                Vf.Add("rotate=PI");
            else if (f.Contains("Horizontal"))
                Vf.Add("hflip");
            else if (f.Contains("Vertical"))
                Vf.Add("vflip");
            else if (f == "interpolation")
            {
                Out_fps = (Out_fps > 0 ? Out_fps : (int)double.Parse(v)) * 2;
                Vf.Add("minterpolate=fps=" + Out_fps.ToString() + ":search_param=96");
            }
            else if (f.StartsWith("Speed up") || f.StartsWith("Slow down") || f == "Speed update")
            {
                double spd = Func.Get_speed(Vf) + (f.StartsWith("Speed up") ? -0.1 : (f == "Speed update" ? 0.0 : 0.1));
                spd = spd < 0.1 ? 0.1 : (spd > 100 ? 100 : spd);
                Vf.RemoveAll(s => s.StartsWith("setpts="));
                Af.RemoveAll(s => s.StartsWith("atempo="));
                if (spd == 1)
                    return;
                Vf.Add("setpts=" + spd +"*PTS");
                spd = 1.0 / spd;
                while (spd < 0.5)
                {
                    spd /= 0.5;
                    Af.Add("atempo=0.5");
                }
                Af.Add("atempo=" + spd);
            }
        }

        public void Vf_update(string f, [Optional] string v, [Optional] string a)
        {
            if (f == "tonemap")
            {
                if (v == "Yes" || a == "False")
                {
                    Vf.RemoveAll(s => s.Contains("tonemap"));
                    return;
                }
                bool d = false;
                for (int i = 0; i < Vf.Count; i++)
                {
                    if (Vf[i].Contains("tonemap"))
                        d = true;
                    if (Bits == 8 && (Vf[i].Contains("format=p010,hwdownload,format=p010") || Vf[i].Contains(Bit_Format(10) + ",hwdownload," + Bit_Format(10))))
                    {
                        Vf[i] = Vf[i].Replace("format=p010,hwdownload,format=p010", "format=nv12,hwdownload,format=nv12");
                        Vf[i] = Vf[i].Replace(Bit_Format(10) + ",hwdownload," + Bit_Format(10), Bit_Format(8) + ",hwdownload," + Bit_Format(8));
                        break;
                    }
                    else if (Bits > 8 && (Vf[i].Contains("format=nv12,hwdownload,format=nv12") || Vf[i].Contains(Bit_Format(8) + ",hwdownload," + Bit_Format(8))))
                    {
                        Vf[i] = Vf[i].Replace("format=nv12,hwdownload,format=nv12", "format=p010,hwdownload,format=p010");
                        Vf[i] = Vf[i].Replace(Bit_Format(8) + ",hwdownload," + Bit_Format(8), Bit_Format(10) + ",hwdownload," + Bit_Format(10));
                        break;
                    }
                }
                if (!d)
                {
                    if (Libplacebo)
                        Vf_add("Vulkan", "");
                    else
                        Vf_add("OpenCL", "");
                }
            }
            else if (f.Contains("setpts="))
                Vf_add("Speed update");
        }

        public void Af_add(string f, [Optional] string v)
        {
            if (f == "sofalizer")
            {
                Af.RemoveAll(s => s.StartsWith("sofalizer"));
                Af.RemoveAll(s => s.StartsWith("dynaudnorm"));
                if (int.Parse(Ch) < int.Parse(v) && int.Parse(Ch) < 3)
                {
                    if (int.Parse(Ch) == 2)
                        Af.Insert(0, "sofalizer='" + resdir + "HRIR_CIRC360_NF150.sofa':type=freq:lfegain=1:radius=5,\"firequalizer=gain_entry='entry(50,-2);entry(250,0);entry(1000,1);entry(4000,-0.5);entry(8000,3);entry(16000,4)'\"");
                    Af.Add("dynaudnorm=g=3:peak=0.99:maxgain=" + v + ":b=1:r=1");
                }
            }
            else if (f == "volume")
            {
                Af.RemoveAll(s => s.StartsWith("volume"));
                Af.Add("volume=1.3");
            }
            if (f == "normalize")
                Af.Add("dynaudnorm=g=3:peak=0.99:maxgain=" + v + ":b=1:r=1");
            if (f == "adelay")
            {
                Af.RemoveAll(s => s.StartsWith("adelay"));
                if (double.Parse(v) > 0)
                    Af.Add("adelay=" + v + ":all=true");
            }
            if (f == "noisereduction")
                Af.Add("arnndn=m='" + resdir + "std.rnnn':mix=0.65,afftdn=nr=3:nf=-20");
        }

        public string Build_vstr(bool predict = false)
        {
            string str = " -hide_banner -copyts -start_at_zero -y !seek! -i \"!file!\" !start! !duration!";
            str += " -c:v:0 " + Cv;
            List<string> vf = new List<string>(Vf);
            if (Vf.Count > 0 && Vf.FindIndex(s => s.StartsWith("setpts=")) > -1)
            {
                Out_spd = Func.Get_speed(Vf);
                vf.RemoveAll(s => s.StartsWith("setpts="));
            }
            if (vf.Count > 0)
            {
                if (Vf.FindIndex(s => s.Contains("vidstabtransform")) > -1)
                    str = " -copyts -start_at_zero -y !seek! -i \"!file!\" !start! !duration! -vf \"scale='min(640,iw)':-2,vidstabdetect=shakiness=10:accuracy=5:result='transforms.trf'\" -f null NUL && ffmpeg" + str;

                foreach (string s in Vf)
                {
                    if (s.Contains("libplacebo") && Libplacebo)
                    {
                        str += " -init_hw_device vulkan";
                        break;
                    }
                    if (s.Contains("opencl"))
                    {
                        str += " -init_hw_device opencl=gpu -filter_hw_device gpu";
                        break;
                    }
                }
                str += " -vf " + String.Join(",", vf.ToArray());
            }
            str += " -pix_fmt " + (Bits == 8 ? "yuv420p" : "yuv420p10le");
            str += " -fps_mode vfr";
            if (V_kbps > 0)
            {
                str += " -b:v !bitrate!k";
                if (Multipass != "" && !predict)
                    str += " " + Multipass;
                if (Cv == "librav1e")
                    str += " -rav1e-params bitrate=!bitrate!";
            }
            else
            {
                if (Cv == "libxvid")
                    str += " -qscale:v ";
                else if (Cv == "h264_nvenc" || Cv == "hevc_nvenc")
                    str += " -cq:v ";
                else if (Cv == "librav1e")
                    str += " -rav1e-params quantizer=";
                else
                    str += " -crf ";
                str += Crf.ToString();
            }
            str += (Cv != "librav1e" ? " " : "") + speed_str + Speed;
            str += (Cv != "librav1e" ? " " : "") + Func.Replace_gs(Param, Gs_level);
            if (V_kbps > 0 && Multipass != "" && !predict && Cv == "libx265")
                str += ":!reuse!";
            if (!Hdr)
                str += Color;
            str += " -an";
            if (V_kbps > 0 && Multipass != "" && !predict)
                str = Pass(str) + " -loglevel error -f null NUL && ffmpeg" + Pass(str, 2);
            str += " -map 0:v:0 -muxpreload 0 -muxdelay 0 -mpegts_copyts 1 -bsf:v dump_extra \"!name!\"";
            return str;
        }

        public string Build_astr(int track)
        {
            if (Ca == "libfdk_aac")
            {
                if (Ba < 8)
                    Ch = "1";
                else if (Ba < A_kbps)
                    Ch = "2";
            }
            string astr = " -vn -async 1 -c:a " + Ca;
            astr += " -ac " + Ch + " " ;
            string p2 = "-profile:a aac_he_v2", p1 = "-profile:a aac_he";
            if (Ca == "libfdk_aac")
            {
                if (Ba < 8)
                    astr += "-b:a " + Ba + "k ";
                else if (Ba < 24)
                    astr += "-b:a " + Ba + "k " + p2;
                else if (Ba < 41)
                    astr += "-b:a " + Ba + "k " + p1;
                else if (Ba < 45)
                    astr += "-vbr 1 " + p1;
                else if (Ba < 50)
                    astr += "-vbr 2 " + p1;
                else if (Ba < 57)
                    astr += "-vbr 1 " + p1;
                else if (Ba < 66)
                    astr += "-vbr 2 " + p1;
                else if (Ba < 81)
                    astr += "-vbr 3 " + p1;
                else if (Ba < 97)
                    astr += "-vbr 4 " + p1;
                else if (Ba < 110)
                    astr += "-vbr 1 -cutoff 15000";
                else if (Ba < 116)
                    astr += "-vbr 1 -cutoff 16000";
                else if (Ba < 121)
                    astr += "-vbr 1 -cutoff 17000";
                else if (Ba < 140)
                    astr += "-vbr 2 -cutoff 17000";
                else if (Ba < 160)
                    astr += "-vbr 3 -cutoff 17000";
                else if (Ba < 180)
                    astr += "-vbr 4 -cutoff 17000";
                else
                    astr += "-vbr 5 -cutoff 18000";

                if (Ba < 8)
                    astr += " -ar 8000";
                else if (Ba < 10)
                    astr += " -ar 16000";
                else if (Ba < 14)
                    astr += " -ar 22050";
                else if (Ba < 20)
                    astr += " -ar 24000";
                else if (Ba < 50)
                    astr += " -ar 32000";
            }
            else
                astr += "-b:a " + Ba + "k";
            if (Af.Count > 0)
                astr += " -af " + String.Join(",", Af.ToArray());
            astr += " -map 0:a:" + track;
            return astr;
        }

        private string Pass(string s, int p = 0)
        {
            if (p == 0)
                return Regex.Replace(s.Replace("!reuse!", "analysis-save=\"!log!.log.reuse\":analysis-save-reuse-level=10"), ":hme[^:]*", "");
            else
                return Regex.Replace(s.Replace("!reuse!", "analysis-load=\"!log!.log.reuse\":analysis-load-reuse-level=10:refine-intra=3").Replace("pass 1", "pass 2"), ":hme[-=][^:]*", "");
        }

        public string Params_vbr(string str, string vbr_str, bool remove = false)
        {
            if (vbr_str == null || vbr_str == "")
                return str;
            bool g = vbr_str[0] == '-';
            string[] param = g ? vbr_str.Substring(1).Split(new string[] { " -" }, StringSplitOptions.None) : vbr_str.Split(':');
            foreach (string p in param)
            {
                string[] arr = p.Replace('=', ' ').Split(' ');
                if (remove)
                    str = Func.Param_replace(str, arr[0], "");
                else
                {
                    if (!str.Contains(arr[0]))
                        str = g ? ("-" + arr[0] + " " + arr[1] + " " + str) : Regex.Replace(str, "params ([a-z])", m => "params " + arr[0] + "=" + arr[1] + ":" + m.Groups[1].Value);
                }
            }
            return str;
        }

        public void Save_settings(ToolStripComboBox format, ToolStripComboBox codec_video, ToolStripComboBox speed, ToolStripComboBox resolution, ToolStripComboBox hdr, ToolStripComboBox bit_depth, NumericUpDown crf, ToolStripComboBox codec_audio, ToolStripComboBox channels, TextBox ba, string output_folder, CheckBox gsauto)
        {
            if (Form.ActiveForm == null)
                return;
            Settings settings;
            string res_s = resolution.SelectedIndex > 0 || (resolution.Text != "" && int.Parse(resolution.Text.Replace("p", "")) > Screen.FromControl(Form.ActiveForm).Bounds.Height) ? resolution.Text : "Default";
            string hdr_s = hdr.Enabled ? hdr.Text : "Default";
            string bit_s = bit_depth.Items.Count > 1 ? bit_depth.Text : "Default";
            string ch_s = (channels.Text != c[0] || c.Length > 2) && channels.Items.Count > 1 ? channels.Text : "Default";
            if (System.IO.File.Exists("settings.xml"))
            {
                settings = Load_settings();
                settings.Format = format.Text;
                settings.Codec_video = codec_video.Text;
                settings.Speed = speed.Text;
                if (resolution.Text != "" && settings.Resolution != "Default" && int.Parse(resolution.Text.Replace("p", "")) >= int.Parse(settings.Resolution.Replace("p", "")))
                    settings.Resolution = resolution.Text;
                else
                    settings.Resolution = res_s == "Default" ? settings.Resolution : res_s;
                settings.Hdr = hdr_s == "Default" ? settings.Hdr : hdr_s;
                settings.Bit_depth = bit_s == "Default" ? settings.Bit_depth : bit_s;
                settings.Crf = crf.Value.ToString();
                settings.Codec_audio = codec_audio.Text;
                settings.Channels = ch_s == "Default" ? settings.Channels : ch_s[0] == '2' ? "Default" : ch_s;
                settings.Audio_br = ba.Text;
                settings.Output_folder = output_folder;
                settings.Auto_grain_level = gsauto.Checked;
            }
            else
            {
                settings = new Settings
                {
                    Format = format.Text,
                    Codec_video = codec_video.Text,
                    Speed = speed.Text,
                    Resolution = res_s,
                    Hdr = hdr_s,
                    Bit_depth = bit_s,
                    Crf = crf.Value.ToString(),
                    Codec_audio = codec_audio.Text,
                    Channels = ch_s,
                    Audio_br = ba.Text,
                    Output_folder = output_folder,
                    Auto_grain_level = gsauto.Checked
                };
            }
            var writer = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
            var wfile = new System.IO.StreamWriter(@"settings.xml");
            writer.Serialize(wfile, settings);
            wfile.Close();
        }

        public Settings Load_settings()
        {
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
            System.IO.StreamReader file = new System.IO.StreamReader("settings.xml");
            Settings settings = (Settings)reader.Deserialize(file);
            file.Close();
            return settings;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    public class Settings
    {
        public string Format;
        public string Codec_video;
        public string Speed;
        public string Resolution;
        public string Hdr;
        public string Bit_depth;
        public string Crf;
        public string Codec_audio;
        public string Channels;
        public string Audio_br;
        public string Output_folder;
        public bool Auto_grain_level;
    }
}
