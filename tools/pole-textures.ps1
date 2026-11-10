# Makes the drawn poles' textures (FlagPole.shader, PoleTextures.cs): downloads each source's 1k
# maps -- Poly Haven's (every Poly Haven asset is CC0) and ambientCG's (every ambientCG asset is
# CC0) -- and writes two 512-pixel PNGs per texture into RealFlag\Resources\Poles\, which the DLL
# embeds as RealFlag.Poles.<file>. Credited in THIRD-PARTY-NOTICES.md, LICENSE and README.md.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\pole-textures.ps1 [-RawDir <dir>]
#
# Per texture (the list below is the one place they are named):
#   <key>_c.png  RGB = the colour map (sRGB), scaled in linear light to the mean luminance below
#                (what the shader expects of it: wood and rust as they are; the rope GREY at 0.5,
#                the shader multiplying it by twice the rope's colour); A = the roughness map.
#   <key>_n.png  RGB = the OpenGL-convention normal map (green up), as is.
# Both 512 pixels wide (each 2 x 2 of the 1k source averaged; the colour in linear light). The
# wood's grain runs across its scan: it is TURNED (transposed, the normal's x and y swapped and
# negated with it) so the grain runs along the pole, the texture's v.
# The raw downloads (checked against the API's md5 where it gives one) stay in -RawDir.

param(
    [string]$RawDir = (Join-Path $env:TEMP "RealFlag-pole-raw"),
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $OutDir) { $OutDir = Join-Path $root "RealFlag\Resources\Poles" }

# Source, id, our key, mean linear luminance of the colour (0: as scanned), grey, turned. The rust
# was 0.075 at first, "a bit too bright" in the pictures (the author, 2026-11-09): 0.045.
# The wood (Poly Haven's european_walnut_veneer_04, Luma 0.180, turned) went with the materials
# on 2026-11-09; Turn stays for a scan whose grain runs across.
$textures = @(
    @{ Site = "polyhaven"; Id = "rust_coarse_01";            Key = "rust"; Luma = 0.045; Grey = $false; Turn = $false },
    @{ Site = "ambientcg"; Id = "Rope001";                   Key = "rope"; Luma = 0.500; Grey = $true;  Turn = $false }
)
$src = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>A decoded PNG: samples scaled to 0..65535, row-major, Channels per pixel.</summary>
public sealed class PngImage
{
    public int Width, Height, Channels, BitDepth, ColorType;
    public ushort[] Data;
}

/// <summary>
/// A minimal PNG reader (8 / 16 bits, grey, grey + alpha, RGB, RGBA, palette; not interlaced)
/// and writer (8-bit RGB / RGBA, adaptive filters, only IHDR / IDAT / IEND, so nothing like a
/// gAMA chunk can change how Unity reads the colours).
/// </summary>
public static class PngCodec
{
    static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    static uint[] crcTable;

    static uint BE32(byte[] b, int o)
    {
        return (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
    }

    public static PngImage Read(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        if (file.Length < 8) throw new InvalidDataException(path + ": not a PNG");
        for (int i = 0; i < 8; i++)
            if (file[i] != Signature[i]) throw new InvalidDataException(path + ": not a PNG");

        int w = 0, h = 0, depth = 0, type = -1, interlace = 0;
        byte[] palette = null;
        MemoryStream idat = new MemoryStream();
        int pos = 8;
        while (pos + 8 <= file.Length)
        {
            int len = (int)BE32(file, pos);
            string kind = Encoding.ASCII.GetString(file, pos + 4, 4);
            int data = pos + 8;
            if (len < 0 || data + len + 4 > file.Length) throw new InvalidDataException(path + ": truncated " + kind + " chunk");
            if (kind == "IHDR")
            {
                w = (int)BE32(file, data);
                h = (int)BE32(file, data + 4);
                depth = file[data + 8];
                type = file[data + 9];
                if (file[data + 10] != 0 || file[data + 11] != 0) throw new NotSupportedException(path + ": unknown compression / filter method");
                interlace = file[data + 12];
            }
            else if (kind == "PLTE")
            {
                palette = new byte[len];
                Buffer.BlockCopy(file, data, palette, 0, len);
            }
            else if (kind == "IDAT")
            {
                idat.Write(file, data, len);
            }
            else if (kind == "IEND")
            {
                break;
            }
            pos = data + len + 4;
        }

        if (w <= 0 || h <= 0) throw new InvalidDataException(path + ": no IHDR");
        if (interlace != 0) throw new NotSupportedException(path + ": interlaced PNG");
        int channels;
        switch (type)
        {
            case 0: channels = 1; break;
            case 2: channels = 3; break;
            case 3: channels = 1; break;
            case 4: channels = 2; break;
            case 6: channels = 4; break;
            default: throw new NotSupportedException(path + ": colour type " + type);
        }
        if (depth != 8 && depth != 16) throw new NotSupportedException(path + ": bit depth " + depth);
        if (type == 3 && (depth != 8 || palette == null)) throw new NotSupportedException(path + ": palette PNG that is not 8-bit");

        int bpp = channels * depth / 8;
        int stride = w * bpp;
        byte[] raw = new byte[(stride + 1) * h];
        byte[] z = idat.ToArray();
        using (DeflateStream ds = new DeflateStream(new MemoryStream(z, 2, z.Length - 2), CompressionMode.Decompress))
        {
            int got = 0;
            while (got < raw.Length)
            {
                int n = ds.Read(raw, got, raw.Length - got);
                if (n <= 0) break;
                got += n;
            }
            if (got != raw.Length) throw new InvalidDataException(path + ": image data too short (" + got + " of " + raw.Length + " bytes)");
        }

        int outChannels = type == 3 ? 3 : channels;
        ushort[] outData = new ushort[w * h * outChannels];
        byte[] cur = new byte[stride], prev = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int at = y * (stride + 1);
            Buffer.BlockCopy(raw, at + 1, cur, 0, stride);
            Unfilter(raw[at], cur, prev, bpp, path);
            int o = y * w * outChannels;
            if (type == 3)
            {
                for (int x = 0; x < w; x++)
                    for (int c = 0; c < 3; c++)
                        outData[o + x * 3 + c] = (ushort)(palette[cur[x] * 3 + c] * 257);
            }
            else if (depth == 8)
            {
                for (int i = 0; i < stride; i++)
                    outData[o + i] = (ushort)(cur[i] * 257);
            }
            else
            {
                for (int i = 0; i < w * channels; i++)
                    outData[o + i] = (ushort)(cur[2 * i] << 8 | cur[2 * i + 1]);
            }
            byte[] t = prev; prev = cur; cur = t;
        }

        PngImage img = new PngImage();
        img.Width = w; img.Height = h; img.Channels = outChannels; img.BitDepth = depth; img.ColorType = type; img.Data = outData;
        return img;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    static void Unfilter(int f, byte[] cur, byte[] prev, int bpp, string path)
    {
        int n = cur.Length;
        switch (f)
        {
            case 0:
                return;
            case 1:
                for (int i = bpp; i < n; i++) cur[i] = (byte)(cur[i] + cur[i - bpp]);
                return;
            case 2:
                for (int i = 0; i < n; i++) cur[i] = (byte)(cur[i] + prev[i]);
                return;
            case 3:
                for (int i = 0; i < n; i++)
                {
                    int a = i >= bpp ? cur[i - bpp] : 0;
                    cur[i] = (byte)(cur[i] + ((a + prev[i]) >> 1));
                }
                return;
            case 4:
                for (int i = 0; i < n; i++)
                {
                    int a = i >= bpp ? cur[i - bpp] : 0;
                    int c = i >= bpp ? prev[i - bpp] : 0;
                    cur[i] = (byte)(cur[i] + Paeth(a, prev[i], c));
                }
                return;
            default:
                throw new InvalidDataException(path + ": unknown row filter " + f);
        }
    }

    /// <summary>Writes 8-bit RGB (channels 3) or straight-alpha RGBA (channels 4).</summary>
    public static void Write(string path, int w, int h, int channels, byte[] pixels)
    {
        if (channels != 3 && channels != 4) throw new ArgumentException("channels");
        int stride = w * channels;
        byte[] filtered = new byte[(stride + 1) * h];
        byte[] prev = new byte[stride];
        byte[] cur = new byte[stride];
        byte[][] trial = new byte[5][];
        for (int f = 0; f < 5; f++) trial[f] = new byte[stride];

        for (int y = 0; y < h; y++)
        {
            Buffer.BlockCopy(pixels, y * stride, cur, 0, stride);
            int best = 0;
            long bestSum = long.MaxValue;
            for (int f = 0; f < 5; f++)
            {
                byte[] t = trial[f];
                long sum = 0;
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= channels ? cur[i - channels] : 0;
                    int b = prev[i];
                    int c = i >= channels ? prev[i - channels] : 0;
                    int pred = f == 0 ? 0 : f == 1 ? a : f == 2 ? b : f == 3 ? (a + b) >> 1 : Paeth(a, b, c);
                    byte v = (byte)(cur[i] - pred);
                    t[i] = v;
                    sum += v < 128 ? v : 256 - v;
                }
                if (sum < bestSum) { bestSum = sum; best = f; }
            }
            filtered[y * (stride + 1)] = (byte)best;
            Buffer.BlockCopy(trial[best], 0, filtered, y * (stride + 1) + 1, stride);
            byte[] swap = prev; prev = cur; cur = swap;
        }

        MemoryStream z = new MemoryStream();
        z.WriteByte(0x78);
        z.WriteByte(0xDA);
        using (DeflateStream ds = new DeflateStream(z, CompressionLevel.Optimal, true))
            ds.Write(filtered, 0, filtered.Length);
        uint adler = Adler32(filtered);
        z.WriteByte((byte)(adler >> 24)); z.WriteByte((byte)(adler >> 16)); z.WriteByte((byte)(adler >> 8)); z.WriteByte((byte)adler);

        byte[] ihdr = new byte[13];
        PutBE32(ihdr, 0, (uint)w);
        PutBE32(ihdr, 4, (uint)h);
        ihdr[8] = 8;
        ihdr[9] = (byte)(channels == 4 ? 6 : 2);

        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            fs.Write(Signature, 0, 8);
            Chunk(fs, "IHDR", ihdr);
            Chunk(fs, "IDAT", z.ToArray());
            Chunk(fs, "IEND", new byte[0]);
        }
    }

    static void PutBE32(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    static void Chunk(Stream s, string kind, byte[] data)
    {
        byte[] head = new byte[8];
        PutBE32(head, 0, (uint)data.Length);
        Encoding.ASCII.GetBytes(kind, 0, 4, head, 4);
        s.Write(head, 0, 8);
        s.Write(data, 0, data.Length);
        uint crc = Crc(head, 4, 4, 0xFFFFFFFFu);
        crc = Crc(data, 0, data.Length, crc) ^ 0xFFFFFFFFu;
        byte[] tail = new byte[4];
        PutBE32(tail, 0, crc);
        s.Write(tail, 0, 4);
    }

    static uint Crc(byte[] b, int o, int n, uint crc)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                crcTable[i] = c;
            }
        }
        for (int i = 0; i < n; i++) crc = crcTable[(crc ^ b[o + i]) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static uint Adler32(byte[] b)
    {
        uint a = 1, s = 0;
        for (int i = 0; i < b.Length; i++)
        {
            a = (a + b[i]) % 65521;
            s = (s + a) % 65521;
        }
        return (s << 16) | a;
    }
}

public static class PoleMaps
{
    static double ToLinear(double s)
    {
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    static double ToSrgb(double l)
    {
        l = Math.Max(0.0, Math.Min(1.0, l));
        return l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1.0 / 2.4) - 0.055;
    }

    /// <summary>
    /// Writes key_c.png (RGB colour calibrated, A roughness) and key_n.png (normal) at half the
    /// source's size; returns a line of what it did.
    /// </summary>
    public static string Compose(string colourPath, string roughPath, string normalPath, string cOut, string nOut, double luma, bool grey, bool turn)
    {
        PngImage c = PngCodec.Read(colourPath);
        PngImage r = PngCodec.Read(roughPath);
        PngImage n = PngCodec.Read(normalPath);
        int w = c.Width, h = c.Height;
        if (r.Width != w || r.Height != h || n.Width != w || n.Height != h)
            throw new InvalidDataException("the maps differ in size");
        if (c.Channels < 3 || n.Channels < 3) throw new InvalidDataException("the colour and the normal map must be colour");
        if ((w & 1) != 0 || (h & 1) != 0) throw new InvalidDataException("odd sizes");

        int ow = w / 2, oh = h / 2;
        // Linear colour, roughness, normal x / y / z at half size.
        double[] lin = new double[ow * oh * 3];
        double[] rough = new double[ow * oh];
        double[] nor = new double[ow * oh * 3];
        double sumLuma = 0;
        for (int y = 0; y < oh; y++)
        {
            for (int x = 0; x < ow; x++)
            {
                int o = y * ow + x;
                for (int dy = 0; dy < 2; dy++)
                {
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int i = (2 * y + dy) * w + 2 * x + dx;
                        for (int k = 0; k < 3; k++)
                        {
                            lin[o * 3 + k] += 0.25 * ToLinear(c.Data[i * c.Channels + k] / 65535.0);
                            nor[o * 3 + k] += 0.25 * (n.Data[i * n.Channels + k] / 65535.0 * 2.0 - 1.0);
                        }
                        rough[o] += 0.25 * r.Data[i * r.Channels] / 65535.0;
                    }
                }
                sumLuma += 0.2126 * lin[o * 3] + 0.7152 * lin[o * 3 + 1] + 0.0722 * lin[o * 3 + 2];
            }
        }

        double mean = sumLuma / (ow * oh);
        double gain = luma > 0 ? luma / mean : 1.0;

        // Turned: transposed, so a row becomes a column. A normal's (col, row) parts (nx, -ny) go
        // to (-ny, nx) in the new (col, row): nx' = -ny, ny' = -nx.
        int tw = turn ? oh : ow, th = turn ? ow : oh;
        byte[] cb = new byte[tw * th * 4];
        byte[] nb = new byte[tw * th * 3];
        int clipped = 0;
        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                int o = turn ? x * ow + y : y * ow + x;
                int d = y * tw + x;
                double l = 0.2126 * lin[o * 3] + 0.7152 * lin[o * 3 + 1] + 0.0722 * lin[o * 3 + 2];
                for (int k = 0; k < 3; k++)
                {
                    double v = (grey ? l : lin[o * 3 + k]) * gain;
                    if (v > 1.0) clipped++;
                    cb[d * 4 + k] = (byte)Math.Round(255.0 * ToSrgb(v));
                }
                cb[d * 4 + 3] = (byte)Math.Round(255.0 * Math.Max(0.0, Math.Min(1.0, rough[o])));

                double nx = nor[o * 3], ny = nor[o * 3 + 1], nz = nor[o * 3 + 2];
                if (turn) { double t = nx; nx = -ny; ny = -t; }
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-6) { nx = 0; ny = 0; nz = 1; len = 1; }
                nb[d * 3] = (byte)Math.Round(255.0 * (0.5 + 0.5 * nx / len));
                nb[d * 3 + 1] = (byte)Math.Round(255.0 * (0.5 + 0.5 * ny / len));
                nb[d * 3 + 2] = (byte)Math.Round(255.0 * (0.5 + 0.5 * nz / len));
            }
        }

        PngCodec.Write(cOut, tw, th, 4, cb);
        PngCodec.Write(nOut, tw, th, 3, nb);
        return string.Format("{0}x{1} -> {2}x{3}{4}; mean luminance {5:0.000} -> {6:0.000} (x {7:0.00}, {8} values clipped){9}",
                             w, h, tw, th, turn ? " turned" : "", mean, mean * gain, gain, clipped, grey ? ", grey" : "");
    }
}
'@

Add-Type -TypeDefinition $src -Language CSharp -ReferencedAssemblies System.Drawing

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$web = New-Object Net.WebClient
$web.Headers.Add("User-Agent", "RealFlag-pole-textures/1.0")
$web.Encoding = [Text.Encoding]::UTF8

New-Item -ItemType Directory -Force $RawDir, $OutDir | Out-Null

function Fetch([string]$url, [string]$file, [string]$md5) {
    $ok = (Test-Path $file) -and (-not $md5 -or (Get-FileHash $file -Algorithm MD5).Hash -eq $md5.ToUpperInvariant())
    if (-not $ok) {
        Write-Host ("  downloading {0}" -f $url)
        $web.DownloadFile($url, $file)
        if ($md5) {
            $hash = (Get-FileHash $file -Algorithm MD5).Hash
            if ($hash -ne $md5.ToUpperInvariant()) { throw ("{0}: md5 {1}, the API says {2}" -f $file, $hash, $md5) }
        }
    }
}

$credits = @()
foreach ($t in $textures) {
    $dir = Join-Path $RawDir $t.Id
    New-Item -ItemType Directory -Force $dir | Out-Null
    $local = @{}
    if ($t.Site -eq "polyhaven") {
        $info = $web.DownloadString("https://api.polyhaven.com/info/" + $t.Id) | ConvertFrom-Json
        $files = $web.DownloadString("https://api.polyhaven.com/files/" + $t.Id) | ConvertFrom-Json
        foreach ($m in @(@{ Name = "colour"; Api = "Diffuse" }, @{ Name = "rough"; Api = "Rough" }, @{ Name = "normal"; Api = "nor_gl" })) {
            $png = $files.($m.Api).'1k'.png
            if ($null -eq $png) { throw ("{0}: no 1k PNG of '{1}'" -f $t.Id, $m.Api) }
            $file = Join-Path $dir ([IO.Path]::GetFileName(([Uri]$png.url).AbsolutePath))
            Fetch $png.url $file $png.md5
            $local[$m.Name] = $file
        }
        $authors = ($info.authors.PSObject.Properties | ForEach-Object { $_.Name }) -join ", "
        $credits += ("{0}: Poly Haven '{1}' (https://polyhaven.com/a/{2}) by {3}, CC0" -f $t.Key, $info.name, $t.Id, $authors)
    }
    else {
        $zip = Join-Path $dir ($t.Id + "_1K-PNG.zip")
        Fetch ("https://ambientcg.com/get?file=" + $t.Id + "_1K-PNG.zip") $zip ""
        $unzipped = Join-Path $dir "maps"
        if (-not (Test-Path $unzipped)) { Expand-Archive -Path $zip -DestinationPath $unzipped -Force }
        $local["colour"] = Join-Path $unzipped ($t.Id + "_1K-PNG_Color.png")
        $local["rough"] = Join-Path $unzipped ($t.Id + "_1K-PNG_Roughness.png")
        $local["normal"] = Join-Path $unzipped ($t.Id + "_1K-PNG_NormalGL.png")
        foreach ($f in $local.Values) { if (-not (Test-Path $f)) { throw "missing $f" } }
        $credits += ("{0}: ambientCG '{1}' (https://ambientcg.com/a/{1}), CC0" -f $t.Key, $t.Id)
    }

    $line = [PoleMaps]::Compose($local["colour"], $local["rough"], $local["normal"],
        (Join-Path $OutDir ($t.Key + "_c.png")), (Join-Path $OutDir ($t.Key + "_n.png")), [double]$t.Luma, [bool]$t.Grey, [bool]$t.Turn)
    Write-Host ("{0,-5} {1}" -f $t.Key, $line) -ForegroundColor Green
}

Write-Host ""
$credits | ForEach-Object { Write-Host $_ }
Write-Host "POLE TEXTURES OK" -ForegroundColor Green
