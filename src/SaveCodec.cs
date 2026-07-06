using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    public class SavePlayer
    {
        public string Nome { get; set; } = "";
        public int RecordStartInEft { get; set; }
        public int RecordSizeInEft { get; set; }
        public int ForcaOffsetInFile { get; set; }
        public int SalarioOffsetInFile { get; set; }
        public int Forca { get; set; }
        public int Salario { get; set; }
    }

    public class SaveTeam
    {
        public string Nome { get; set; } = "";
        public int EftStartOffset { get; set; }
        public long Verba { get; set; }
        public int VerbaOffset { get; set; } = -1;
        public List<SavePlayer> Players { get; } = new List<SavePlayer>();
    }

    public class SaveFile
    {
        public byte[] RawBytes { get; set; } = Array.Empty<byte>();
        public List<SaveTeam> Teams { get; } = new List<SaveTeam>();
    }

    // Codec de save .e98 do Elifoot 98.
    //
    // Estrutura:
    // - Save = header + 42 blobs EFT + calendario/footer.
    // - Cada EFT: magic 'EFa\0' + body cifrado Caesar rolante.
    // - Verba do time: uint32 LE. Offset padrao +0xE0, mas varia:
    //   +0xDD em alguns saves (multi-coach ou coach info embutido).
    //   Detectamos escaneando header em [+0xD8..+0xE4].
    // - Player records dentro do EFT decoded, marker: any + 'bra' +
    //   [A-Z pos_code] + initial + shifted name + stats.
    // - FORCA:   byte  em (record_size - 48) do record (RAW bytes).
    // - SALARIO: uint16 LE em (record_size - 25) do record (RAW bytes).
    public static class SaveCodec
    {
        private static readonly byte[] EFT_MAGIC = { (byte)'E', (byte)'F', (byte)'a', 0 };
        private const int FORCA_OFFSET_FROM_REC_END = 48;
        private const int SALARIO_OFFSET_FROM_REC_END = 25;
        public const int FORCA_MIN = 1;
        public const int FORCA_MAX = 9999;
        public const int FORCA_WARN_ABOVE = 50;
        public const int SALARIO_MIN = 50;
        public const int SALARIO_MAX = 99999;

        public static SaveFile Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var sf = new SaveFile { RawBytes = bytes };

            var eftPositions = FindAllEftStarts(bytes);
            for (int e = 0; e < eftPositions.Count; e++)
            {
                int eftStart = eftPositions[e];
                int eftEnd = e + 1 < eftPositions.Count ? eftPositions[e + 1] : bytes.Length;
                var team = new SaveTeam { EftStartOffset = eftStart };

                // Decode body pra achar nome + jogadores
                int bodyStart = eftStart + 4;
                int bodyEnd = eftEnd;
                var decoded = DecodeCaesar(bytes, bodyStart, bodyEnd);

                team.Nome = ExtractTeamName(decoded);

                // Verba: procura no header do EFT (offset +0xD8..+0xE4) por
                // uint32 razoavel (100K..1M reais eh normal pra time BR).
                team.VerbaOffset = FindVerbaOffset(bytes, eftStart);
                if (team.VerbaOffset > 0)
                    team.Verba = (uint)BitConverter.ToInt32(bytes, team.VerbaOffset);

                // Player records
                var starts = FindPlayerStarts(decoded);
                int lastRecCap = 130;
                for (int i = 1; i < starts.Count; i++)
                {
                    int recStart = starts[i];
                    int naiveEnd = i + 1 < starts.Count ? starts[i + 1] : decoded.Length;
                    int recSize = naiveEnd - recStart;
                    bool isLast = i + 1 >= starts.Count;
                    if (isLast && recSize > lastRecCap)
                        recSize = Math.Min(lastRecCap, naiveEnd - recStart);
                    else if (!isLast)
                        lastRecCap = Math.Max(lastRecCap, recSize + 20);

                    if (recSize < FORCA_OFFSET_FROM_REC_END + 1) continue;
                    int forcaLocal = recSize - FORCA_OFFSET_FROM_REC_END;
                    int salarioLocal = recSize - SALARIO_OFFSET_FROM_REC_END;
                    var p = new SavePlayer
                    {
                        Nome = ExtractPlayerName(decoded, recStart, forcaLocal),
                        RecordStartInEft = recStart,
                        RecordSizeInEft = recSize,
                        ForcaOffsetInFile = bodyStart + recStart + forcaLocal,
                        SalarioOffsetInFile = bodyStart + recStart + salarioLocal,
                    };
                    if (p.ForcaOffsetInFile < bytes.Length)
                        p.Forca = bytes[p.ForcaOffsetInFile];
                    if (p.SalarioOffsetInFile + 2 <= bytes.Length)
                        p.Salario = BitConverter.ToUInt16(bytes, p.SalarioOffsetInFile);
                    team.Players.Add(p);
                }

                sf.Teams.Add(team);
            }
            return sf;
        }

        public static void Write(string path, SaveFile sf)
        {
            var bytes = (byte[])sf.RawBytes.Clone();
            foreach (var team in sf.Teams)
            {
                if (team.VerbaOffset > 0 && team.VerbaOffset + 4 <= bytes.Length)
                {
                    var v = (uint)Math.Max(0, Math.Min(uint.MaxValue, (ulong)team.Verba));
                    var vb = BitConverter.GetBytes(v);
                    Array.Copy(vb, 0, bytes, team.VerbaOffset, 4);
                }
                foreach (var p in team.Players)
                {
                    if (p.ForcaOffsetInFile > 0 && p.ForcaOffsetInFile < bytes.Length)
                    {
                        var f = Math.Max(FORCA_MIN, Math.Min(FORCA_MAX, p.Forca));
                        bytes[p.ForcaOffsetInFile] = (byte)(f & 0xFF);
                        if (f > 255 && p.ForcaOffsetInFile + 1 < bytes.Length)
                            bytes[p.ForcaOffsetInFile + 1] = (byte)((f >> 8) & 0xFF);
                    }
                    if (p.SalarioOffsetInFile > 0 && p.SalarioOffsetInFile + 2 <= bytes.Length)
                    {
                        var s = (ushort)Math.Max(SALARIO_MIN, Math.Min(SALARIO_MAX, p.Salario));
                        var sb = BitConverter.GetBytes(s);
                        Array.Copy(sb, 0, bytes, p.SalarioOffsetInFile, 2);
                    }
                }
            }
            File.WriteAllBytes(path, bytes);
        }

        // ---- helpers ----

        private static List<int> FindAllEftStarts(byte[] bytes)
        {
            var list = new List<int>();
            int i = 0;
            while (i <= bytes.Length - EFT_MAGIC.Length)
            {
                int p = IndexOf(bytes, EFT_MAGIC, i);
                if (p < 0) break;
                list.Add(p);
                i = p + 1;
            }
            return list;
        }

        // Procura offset de verba no header do EFT (+0xD8..+0xE4). Verba
        // valida = uint32 no range 100k..1B (limite folgado). Retorna
        // primeiro que bater.
        private static int FindVerbaOffset(byte[] bytes, int eftStart)
        {
            // Ordem de preferencia: +0xE0 (comum em single-coach), +0xDD (multi),
            // depois varre o header.
            int[] preferred = { 0xE0, 0xDD, 0xE4, 0xDC, 0xD8 };
            foreach (var off in preferred)
            {
                int abs = eftStart + off;
                if (abs + 4 > bytes.Length) continue;
                uint v = (uint)BitConverter.ToInt32(bytes, abs);
                if (v >= 100_000 && v <= 1_000_000_000) return abs;
            }
            // Varredura geral
            for (int off = 0xD8; off <= 0xE8; off++)
            {
                int abs = eftStart + off;
                if (abs + 4 > bytes.Length) continue;
                uint v = (uint)BitConverter.ToInt32(bytes, abs);
                if (v >= 100_000 && v <= 1_000_000_000) return abs;
            }
            return -1;
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

        // Nome do time decoded: procura primeira sequencia de letras lowercase
        // depois de padding inicial (~0x2C). Nome curto (5..20 chars).
        private static string ExtractTeamName(byte[] decoded)
        {
            // Skip 0x2C bytes de padding tipico
            int start = 0x2C;
            if (start >= decoded.Length) return "?";
            var sb = new StringBuilder();
            // Find start of name (first letter)
            int i = start;
            while (i < decoded.Length && !IsTeamNameChar(decoded[i])) i++;
            if (i >= decoded.Length) return "?";
            // Read until non-letter
            for (; i < decoded.Length && sb.Length < 30; i++)
            {
                byte b = decoded[i];
                if (b >= 0x61 && b <= 0x7A)
                {
                    // primeira letra maiuscula, resto minuscula
                    if (sb.Length == 0) sb.Append(char.ToUpperInvariant((char)b));
                    else sb.Append((char)b);
                }
                else if (b == 0x40) sb.Append(' ');
                else if (b == 0x4D || b == 0x4B) sb.Append('-');
                else if (b < 0x30 || b > 0x7E)
                {
                    if (sb.Length >= 3) break;
                    sb.Clear();
                }
                else if (sb.Length >= 3) break;
            }
            var s = sb.ToString().Trim();
            return string.IsNullOrEmpty(s) ? "?" : s;
        }

        private static bool IsTeamNameChar(byte b) => b >= 0x61 && b <= 0x7A;

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

        // Formato do record de jogador:
        //   +4: pos_code (A-Z), +5: initial minuscula, +6..: shifted rest
        //   Nome termina 3 bytes antes da forca (empirico).
        //   Espaco (0x40) -> apos, prox byte = initial da 2a palavra unshifted.
        private static string ExtractPlayerName(byte[] decoded, int recStart, int forcaLocal)
        {
            if (decoded.Length < recStart + 6) return "?";
            var sb = new StringBuilder();
            char initial = (char)decoded[recStart + 5];
            sb.Append(char.ToUpperInvariant(initial));

            int nameEnd = recStart + forcaLocal - 3;
            if (nameEnd > decoded.Length) nameEnd = decoded.Length;

            bool afterSpace = false;
            for (int i = recStart + 6; i < nameEnd; i++)
            {
                byte b = decoded[i];
                if (afterSpace && b >= 0x61 && b <= 0x7A)
                {
                    sb.Append(char.ToUpperInvariant((char)b));
                    afterSpace = false;
                    continue;
                }
                afterSpace = false;
                char? c = MapNameByte(b);
                if (c == null) break;
                sb.Append(c.Value);
                if (c.Value == ' ') afterSpace = true;
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            return sb.ToString();
        }

        private static char? MapNameByte(byte b)
        {
            if (b >= 0x81 && b <= 0x9A) return (char)(b - 0x20);
            if (b == 0x40) return ' ';
            switch (b)
            {
                case 0x01: return 'á';
                case 0x02: return 'â';
                case 0x03: return 'ã';
                case 0x07: return 'ç';
                case 0x09: return 'é';
                case 0x0A: return 'ê';
                case 0x0D: return 'í';
                case 0x13: return 'ó';
                case 0x14: return 'ô';
                case 0x15: return 'õ';
                case 0x1A: return 'ú';
                case 0x1C: return 'ü';
            }
            return null;
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
    }
}
