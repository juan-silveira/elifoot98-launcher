using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    public class SavePlayer
    {
        public string Nome { get; set; } = "";
        // Offset dentro do EFT decodificado onde o record deste player comeca.
        public int RecordStartInEft { get; set; }
        public int RecordSizeInEft { get; set; }
        // Offset ABSOLUTO no arquivo .e98 onde os bytes do salario ficam (uint16 LE).
        public int SalarioOffsetInFile { get; set; }
        public int Salario { get; set; }
    }

    public class SaveFile
    {
        public byte[] RawBytes { get; set; } = Array.Empty<byte>();
        public long Verba { get; set; }
        public int VerbaOffset { get; set; } = -1;
        public string ClubeNome { get; set; } = "";
        public List<SavePlayer> Players { get; } = new List<SavePlayer>();
    }

    // Codec de save .e98 do Elifoot 98.
    //
    // Descoberto empiricamente (2026-07-06):
    // - Save = header + 42 blobs EFT + calendario/footer.
    // - Cada EFT comeca com magic 'EFa\0'.
    // - EFT do time COACHED = o unico que contem "juventus" (nome do time
    //   do usuario) no decoded body. Marker uint32 500000 em +0xDC ajuda
    //   mas nao eh unico (outros EFTs tambem tem esse marker).
    // - Verba do clube = uint32 LE em +0xE0 dentro desse EFT.
    // - Cifra dos nomes: Caesar rolante. plain[i]=(cipher[i]-delta)%256;
    //   delta=(delta+plain[i]-0x20)%256. Delta reseta por EFT.
    // - Player records dentro do EFT decodificado, marker: qualquer byte
    //   + 'bra' + [A-Z pos_code].
    // - Cada record: 'X' + 'bra' + pos + initial + shifted_rest_of_name +
    //   fixed_stats.
    // - Salario = uint16 LE em (record_size - 25) do record. Confirmed em
    //   13/16 players do save de teste.
    public static class SaveCodec
    {
        private static readonly byte[] EFT_MAGIC = { (byte)'E', (byte)'F', (byte)'a', 0 };
        private const int EFT_MARKER_OFFSET = 0xDC;
        private const int EFT_VERBA_OFFSET = 0xE0;
        private const int SALARIO_OFFSET_FROM_REC_END = 25;
        private const int SALARIO_MIN = 50;
        private const int SALARIO_MAX = 9999;

        public static SaveFile Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var sf = new SaveFile { RawBytes = bytes };
            int eftStart = FindCoachedEftStart(bytes);
            if (eftStart < 0) return sf;
            int eftEnd = FindNextEftStart(bytes, eftStart);
            if (eftEnd < 0) eftEnd = bytes.Length;

            if (eftStart + EFT_VERBA_OFFSET + 4 <= bytes.Length)
            {
                sf.VerbaOffset = eftStart + EFT_VERBA_OFFSET;
                sf.Verba = (uint)BitConverter.ToInt32(bytes, sf.VerbaOffset);
            }

            // Decoded body pra achar players
            int bodyStart = eftStart + 4;
            int bodyEnd = eftEnd;
            var decoded = DecodeCaesar(bytes, bodyStart, bodyEnd);
            var starts = FindPlayerStarts(decoded);

            // Skip #0 (team header). Cada record = starts[i]..starts[i+1] (ou fim).
            for (int i = 1; i < starts.Count; i++)
            {
                int recStart = starts[i];
                int recEnd = i + 1 < starts.Count ? starts[i + 1] : decoded.Length;
                int recSize = recEnd - recStart;
                if (recSize < SALARIO_OFFSET_FROM_REC_END + 2) continue;

                var p = new SavePlayer
                {
                    Nome = ExtractName(decoded, recStart, recSize),
                    RecordStartInEft = recStart,
                    RecordSizeInEft = recSize,
                };

                // Salario: uint16 LE em record_size - 25 do record.
                // Endereço absoluto no arquivo:
                int salaryLocalOffset = recSize - SALARIO_OFFSET_FROM_REC_END;
                p.SalarioOffsetInFile = bodyStart + recStart + salaryLocalOffset;
                if (p.SalarioOffsetInFile + 2 <= bytes.Length)
                    p.Salario = BitConverter.ToUInt16(bytes, p.SalarioOffsetInFile);

                sf.Players.Add(p);
            }

            return sf;
        }

        public static void Write(string path, SaveFile sf)
        {
            var bytes = (byte[])sf.RawBytes.Clone();

            if (sf.VerbaOffset >= 0 && sf.VerbaOffset + 4 <= bytes.Length)
            {
                var v = (uint)Math.Max(0, Math.Min(uint.MaxValue, (ulong)sf.Verba));
                var vb = BitConverter.GetBytes(v);
                Array.Copy(vb, 0, bytes, sf.VerbaOffset, 4);
            }

            foreach (var p in sf.Players)
            {
                if (p.SalarioOffsetInFile <= 0) continue;
                if (p.SalarioOffsetInFile + 2 > bytes.Length) continue;
                var s = (ushort)Math.Max(SALARIO_MIN, Math.Min(SALARIO_MAX, p.Salario));
                var sb = BitConverter.GetBytes(s);
                Array.Copy(sb, 0, bytes, p.SalarioOffsetInFile, 2);
            }

            File.WriteAllBytes(path, bytes);
        }

        public static (int Min, int Max) SalarioLimits => (SALARIO_MIN, SALARIO_MAX);

        // ---- helpers ----

        private static int FindCoachedEftStart(byte[] bytes)
        {
            // Estrategia: procurar EFT que contenha 'juventus' no decoded
            // (o time do usuario). Fallback: primeiro EFT com marker 500000
            // em +0xDC.
            int fallback = -1;
            int i = 0;
            while (i < bytes.Length - EFT_MAGIC.Length)
            {
                int p = IndexOf(bytes, EFT_MAGIC, i);
                if (p < 0) break;
                int nextP = FindNextEftStart(bytes, p);
                if (nextP < 0) nextP = Math.Min(bytes.Length, p + 200);
                int scanEnd = Math.Min(p + 200, nextP);

                var decoded = DecodeCaesar(bytes, p + 4, scanEnd);
                if (ContainsIgnoreCase(decoded, Encoding.ASCII.GetBytes("juventus")))
                    return p;

                if (fallback < 0 && p + EFT_MARKER_OFFSET + 4 <= bytes.Length)
                {
                    uint marker = (uint)BitConverter.ToInt32(bytes, p + EFT_MARKER_OFFSET);
                    if (marker == 500000) fallback = p;
                }
                i = p + 1;
            }
            return fallback;
        }

        private static int FindNextEftStart(byte[] bytes, int after)
        {
            return IndexOf(bytes, EFT_MAGIC, after + 4);
        }

        private static byte[] DecodeCaesar(byte[] bytes, int start, int end)
        {
            end = Math.Min(end, bytes.Length);
            if (end <= start) return Array.Empty<byte>();
            var plain = new byte[end - start];
            int delta = 0;
            for (int i = 0; i < plain.Length; i++)
            {
                int p = (bytes[start + i] - delta) & 0xFF;
                plain[i] = (byte)p;
                delta = (delta + p - 0x20) & 0xFF;
            }
            return plain;
        }

        // Encontra offsets no decoded onde comecam player records.
        // Marker: <qualquer byte> + 'bra' + [A-Z pos_code].
        private static List<int> FindPlayerStarts(byte[] decoded)
        {
            var starts = new List<int>();
            for (int i = 0; i < decoded.Length - 6; i++)
            {
                if (decoded[i + 1] == (byte)'b' && decoded[i + 2] == (byte)'r' && decoded[i + 3] == (byte)'a')
                {
                    byte pos = decoded[i + 4];
                    if (pos >= 'A' && pos <= 'Z')
                        starts.Add(i);
                }
            }
            return starts;
        }

        private static string ExtractName(byte[] decoded, int recStart, int recSize)
        {
            // record: byte + 'bra' + pos + initial + shifted_rest
            if (recSize < 6) return "?";
            char initial = (char)decoded[recStart + 5];
            var sb = new StringBuilder();
            sb.Append(char.ToUpperInvariant(initial));
            for (int i = recStart + 6; i < recStart + recSize; i++)
            {
                byte b = decoded[i];
                // Rest of name = shifted por +0x20. Recuperamos subtraindo.
                // Nomes plainos costumam ter chars 0x60..0x7F apos shift-back de 0x80..0x9F.
                // Se b >= 0x80 e < 0xC0, provavelmente parte do nome shifted.
                // Tambem: accents like 'é' shifted = 0xE9+0x20 = 0x109 → 0x09.
                if (b >= 0x80 && b <= 0xBF)
                {
                    sb.Append((char)(b - 0x20));
                }
                else if (b == 0x09)
                {
                    sb.Append('é');
                }
                else
                {
                    break;
                }
            }
            return sb.ToString();
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match) return i;
            }
            return -1;
        }

        private static bool ContainsIgnoreCase(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    byte h = haystack[i + j];
                    byte n = needle[j];
                    if (h >= 'A' && h <= 'Z') h += 32;
                    if (n >= 'A' && n <= 'Z') n += 32;
                    if (h != n) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
