using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MuChilaAdmin.Core;

/// <summary>
/// Data\Lang.mpr do cliente: os textos do jogo por idioma (por\Text(por).txt, por\MUQuest(por).txt, eng\..., issue #41).
/// É um ZIP comum com duas camadas: cada byte do arquivo em XOR com 20 13 77 (pela posição, de 3 em 3) e as entradas
/// cifradas com ZipCrypto. A senha é a que o main.exe usa (texto em 0x14A6174 do main.exe decifrado na memória, passado à
/// rotina do ZIP em 0xD0DDAF). Nomes das entradas como no original ("\por\Text(por).txt"); os textos são Windows-1252.
/// Depois do ZIP vêm 4 bytes de conferência, que o jogo exige (sem eles, ou errados, não carrega texto nenhum): a soma do MU
/// (GenerateCheckSum2, a mesma dos .bmd) com a chave 0x12DC sobre o arquivo já em XOR, sem os 4 bytes; achada testando as
/// 65.536 chaves contra o Lang.mpr original.
/// </summary>
public static class LangPack
{
    const string Password = "Tw(b!zn&Mu1@#^Ge&sch%enk!";
    static readonly byte[] Xor = { 0x20, 0x13, 0x77 };
    const uint ChecksumKey = 0x12DC;

    /// <summary>GenerateCheckSum2 do cliente: res = chave &lt;&lt; 9; por DWORD: (p/4 + chave) par → XOR, senão soma; a cada 16 bytes, mistura.</summary>
    public static uint Checksum(ReadOnlySpan<byte> data, uint key = ChecksumKey)
    {
        uint res = key << 9;
        for (int p = 0; p + 4 <= data.Length; p += 4)
        {
            uint dw = BitConverter.ToUInt32(data.Slice(p, 4));
            if ((p / 4 + key) % 2 == 0) res ^= dw; else res += dw;
            if (p % 16 == 0) res ^= (key + res) >> (p / 4 % 8 + 1);
        }
        return res;
    }

    /// <summary>Uma entrada do pacote: nome como no ZIP e o conteúdo já decifrado e descompactado.</summary>
    public sealed record Entry(string Name, byte[] Data, ushort Time, ushort Date, uint ExternalAttributes);

    public static List<Entry> Read(string path) => Read(File.ReadAllBytes(path));

    public static List<Entry> Read(byte[] mpr)
    {
        if (mpr.Length < 26 || Checksum(mpr.AsSpan(0, mpr.Length - 4)) != BitConverter.ToUInt32(mpr, mpr.Length - 4))
            throw new InvalidDataException("Lang.mpr: a soma de conferência do fim do arquivo não confere (arquivo estragado ou de outra versão)");
        var z = mpr.AsSpan(0, mpr.Length - 4).ToArray();
        for (int i = 0; i < z.Length; i++) z[i] ^= Xor[i % 3];
        int eocd = -1;
        for (int i = z.Length - 22; i >= 0 && i >= z.Length - 22 - 0xFFFF; i--)
            if (BitConverter.ToUInt32(z, i) == 0x06054B50) { eocd = i; break; }
        if (eocd < 0) throw new InvalidDataException("Lang.mpr: fim do ZIP não encontrado (arquivo de outra versão?)");
        int count = BitConverter.ToUInt16(z, eocd + 10), cd = BitConverter.ToInt32(z, eocd + 16);
        var list = new List<Entry>();
        for (int k = 0, p = cd; k < count; k++)
        {
            if (BitConverter.ToUInt32(z, p) != 0x02014B50) throw new InvalidDataException("Lang.mpr: diretório do ZIP inválido");
            ushort flags = BitConverter.ToUInt16(z, p + 8), method = BitConverter.ToUInt16(z, p + 10);
            ushort time = BitConverter.ToUInt16(z, p + 12), date = BitConverter.ToUInt16(z, p + 14);
            uint crc = BitConverter.ToUInt32(z, p + 16);
            int csize = BitConverter.ToInt32(z, p + 20), usize = BitConverter.ToInt32(z, p + 24);
            int nlen = BitConverter.ToUInt16(z, p + 28), xlen = BitConverter.ToUInt16(z, p + 30), clen = BitConverter.ToUInt16(z, p + 32);
            uint ext = BitConverter.ToUInt32(z, p + 38);
            int local = BitConverter.ToInt32(z, p + 42);
            string name = Encoding.Latin1.GetString(z, p + 46, nlen);
            int data = local + 30 + BitConverter.ToUInt16(z, local + 26) + BitConverter.ToUInt16(z, local + 28);
            var buf = z.AsSpan(data, csize).ToArray();
            if ((flags & 1) != 0)
            {
                var keys = new Keys();
                for (int i = 0; i < buf.Length; i++) { buf[i] ^= keys.Stream(); keys.Update(buf[i]); }
                buf = buf[12..];
            }
            var raw = method switch { 8 => Inflate(buf), 0 => buf, _ => throw new InvalidDataException($"{name}: compressão {method} não suportada") };
            if (raw.Length != usize || Crc32(raw) != crc) throw new InvalidDataException($"{name}: conteúdo não confere (senha errada?)");
            list.Add(new Entry(name, raw, time, date, ext));
            p += 46 + nlen + xlen + clen;
        }
        return list;
    }

    /// <summary>Monta o Lang.mpr (deflate + ZipCrypto + XOR) na ordem dada, como o original: sem data descriptor.</summary>
    public static byte[] Write(IEnumerable<Entry> entries)
    {
        using var ms = new MemoryStream();
        var central = new MemoryStream();
        int n = 0;
        foreach (var e in entries)
        {
            uint crc = Crc32(e.Data);
            var comp = Deflate(e.Data);
            var head = RandomNumberGenerator.GetBytes(12);
            head[11] = (byte)(crc >> 24);   // byte de conferência da senha (sem data descriptor: o CRC)
            var enc = new byte[12 + comp.Length];
            var keys = new Keys();
            for (int i = 0; i < enc.Length; i++)
            {
                byte plain = i < 12 ? head[i] : comp[i - 12];
                enc[i] = (byte)(plain ^ keys.Stream());
                keys.Update(plain);
            }
            var name = Encoding.Latin1.GetBytes(e.Name);
            int offset = (int)ms.Position;
            var w = new BinaryWriter(ms);
            w.Write(0x04034B50u); w.Write((ushort)20); w.Write((ushort)1); w.Write((ushort)8); w.Write(e.Time); w.Write(e.Date);
            w.Write(crc); w.Write(enc.Length); w.Write(e.Data.Length); w.Write((ushort)name.Length); w.Write((ushort)0); w.Write(name); w.Write(enc);

            var c = new BinaryWriter(central);
            c.Write(0x02014B50u); c.Write((ushort)20); c.Write((ushort)20); c.Write((ushort)1); c.Write((ushort)8); c.Write(e.Time); c.Write(e.Date);
            c.Write(crc); c.Write(enc.Length); c.Write(e.Data.Length); c.Write((ushort)name.Length); c.Write((ushort)0); c.Write((ushort)0);
            c.Write((ushort)0); c.Write((ushort)0); c.Write(e.ExternalAttributes); c.Write(offset); c.Write(name);
            n++;
        }
        int cdOffset = (int)ms.Position;
        central.Position = 0; central.CopyTo(ms);
        var end = new BinaryWriter(ms);
        end.Write(0x06054B50u); end.Write((ushort)0); end.Write((ushort)0); end.Write((ushort)n); end.Write((ushort)n);
        end.Write((int)central.Length); end.Write(cdOffset); end.Write((ushort)0);
        var z = ms.ToArray();
        for (int i = 0; i < z.Length; i++) z[i] ^= Xor[i % 3];
        return z.Concat(BitConverter.GetBytes(Checksum(z))).ToArray();
    }

    /// <summary>Grava cada entrada numa pasta (subpastas por idioma: por\, eng\...).</summary>
    public static int Extract(string mprPath, string folder)
    {
        int n = 0;
        foreach (var e in Read(mprPath))
        {
            var dst = Path.Combine(folder, e.Name.TrimStart('\\', '/'));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.WriteAllBytes(dst, e.Data);
            n++;
        }
        return n;
    }

    /// <summary>
    /// Refaz o pacote a partir do original: cada entrada que existir na pasta (mesmo caminho) entra com o conteúdo da pasta;
    /// as outras ficam como estavam. Devolve as entradas trocadas.
    /// </summary>
    public static List<string> Pack(string originalMpr, string folder, string outputMpr)
    {
        var changed = new List<string>();
        var entries = Read(originalMpr).Select(e =>
        {
            var f = Path.Combine(folder, e.Name.TrimStart('\\', '/'));
            if (!File.Exists(f)) return e;
            var data = File.ReadAllBytes(f);
            if (!data.AsSpan().SequenceEqual(e.Data)) changed.Add(e.Name);
            return e with { Data = data };
        }).ToList();
        var bytes = Write(entries);
        // confere: o pacote novo abre e devolve exatamente o que foi gravado
        var back = Read(bytes);
        if (back.Count != entries.Count || back.Zip(entries).Any(x => x.First.Name != x.Second.Name || !x.First.Data.AsSpan().SequenceEqual(x.Second.Data)))
            throw new InvalidOperationException("O Lang.mpr montado não conferiu; nada foi gravado.");
        File.WriteAllBytes(outputMpr, bytes);
        return changed;
    }

    sealed class Keys
    {
        uint k0 = 0x12345678, k1 = 0x23456789, k2 = 0x34567890;
        public Keys() { foreach (var b in Encoding.ASCII.GetBytes(Password)) Update(b); }
        public void Update(byte b) { k0 = Crc(k0, b); k1 = (k1 + (k0 & 0xFF)) * 134775813 + 1; k2 = Crc(k2, (byte)(k1 >> 24)); }
        public byte Stream() { ushort t = (ushort)(k2 | 2); return (byte)((t * (t ^ 1)) >> 8); }
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(uint c, byte b) => CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);

    static uint Crc32(byte[] data)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in data) c = Crc(c, b);
        return c ^ 0xFFFFFFFF;
    }

    static byte[] Inflate(byte[] b)
    {
        using var o = new MemoryStream();
        using (var d = new DeflateStream(new MemoryStream(b), CompressionMode.Decompress)) d.CopyTo(o);
        return o.ToArray();
    }

    static byte[] Deflate(byte[] b)
    {
        using var o = new MemoryStream();
        using (var d = new DeflateStream(o, CompressionLevel.SmallestSize, leaveOpen: true)) d.Write(b);
        return o.ToArray();
    }
}
