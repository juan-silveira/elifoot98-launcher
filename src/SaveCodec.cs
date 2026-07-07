using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    public class SavePlayer
    {
        public string Nome { get; set; } = "";
        public string Posicao { get; set; } = "G";
        public bool Estrela { get; set; }
        public int Comportamento { get; set; }
        public int RecordStartInEft { get; set; }
        public int RecordSizeInEft { get; set; }
        public int ForcaOffsetInFile { get; set; }
        public int SalarioOffsetInFile { get; set; }
        public int PosicaoOffsetInFile { get; set; }
        public int EstrelaOffsetInFile { get; set; }
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
    // Estrutura DETERMINISTICA (v0.4.19, verificado em 200+ records e 9 saves):
    //
    // Cada EFT body decodificado com Caesar rolante:
    //   plain[i] = (cipher[i] - delta) mod 256
    //   delta = (delta + plain[i] - 0x20) mod 256
    //
    // Records dentro do EFT identificados por: raw[+0] == 0x03 (Pascal length
    // prefix da nacionalidade) + decoded[+1..+3] eh 3 letras minusculas.
    //
    // Player record:
    //   raw[+0] = 0x03 (marker Pascal)
    //   decoded[+1..+3] = nacionalidade (3 lowercase: bra, por, esp, etc.)
    //   raw[+4] = NL (name length in bytes)
    //   decoded[+5..+5+NL-1] = nome (1a letra unshifted lowercase, resto
    //                          shifted +0x20, ultima char shifted +0x40 as vezes)
    //   record_size = NL + 55
    //   raw[+sz-50] = posicao (0=G, 1=D, 2=M, 3=A)
    //   raw[+sz-49] = estrela (0/1)
    //   raw[+sz-48] = forca (0-99)
    //   raw[+sz-33] = comportamento (0-5)
    //   raw[+sz-25..sz-24] = salario uint16 LE
    //
    // Primeiro record em cada EFT eh TEAM HEADER (178 bytes fixo). Players
    // comecam em records[1] onwards.
    //
    // VERBA do time: uint32 LE em first_record_offset_in_file + 0x8E.
    //
    // Team name: byte em decoded[0x32] = 0x20 + name_length. Nome comeca em
    // decoded[0x33] e tem esse tamanho. Chars: lowercase, digits shifted
    // (0x50..0x59 = 0..9), acentos ISO-8859-1 diretos.
    public static class SaveCodec
    {
        private static readonly byte[] EFT_MAGIC = { (byte)'E', (byte)'F', (byte)'a', 0 };
        public const int FORCA_MIN = 1;
        public const int FORCA_MAX = 9999;
        public const int FORCA_WARN_ABOVE = 50;
        public const int SALARIO_MIN = 50;
        public const int SALARIO_MAX = 99999;

        public static readonly string[] ComportamentoLabels = {
            "Fair Play", "Cordeirinho", "Cavalheiro",
            "Caneleiro", "Violento", "Assassino"
        };

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

                int bodyStart = eftStart + 4;
                var decoded = DecodeCaesar(bytes, bodyStart, eftEnd);

                team.Nome = ExtractTeamName(decoded);

                // Detecta player records: raw byte 0x03 + decoded 3 lowercase
                var records = new List<(int Offset, int NL)>();
                int bodyLen = eftEnd - bodyStart;
                for (int i = 0; i < bodyLen - 5; i++)
                {
                    if (bytes[bodyStart + i] != 0x03) continue;
                    if (i + 4 >= decoded.Length) continue;
                    byte a = decoded[i + 1], b = decoded[i + 2], c = decoded[i + 3];
                    if (!(a >= 'a' && a <= 'z' && b >= 'a' && b <= 'z' && c >= 'a' && c <= 'z'))
                        continue;
                    int NL = bytes[bodyStart + i + 4];
                    if (NL <= 0 || NL >= 40) continue;
                    records.Add((i, NL));
                }

                if (records.Count == 0) { sf.Teams.Add(team); continue; }

                // Verba: uint32 LE em first_record + 0x8E do arquivo
                int firstRecFileOff = bodyStart + records[0].Offset;
                team.VerbaOffset = firstRecFileOff + 0x8E;
                if (team.VerbaOffset + 4 <= bytes.Length)
                    team.Verba = (uint)BitConverter.ToInt32(bytes, team.VerbaOffset);

                // Skip records[0] (team header). Players from records[1..]
                for (int idx = 1; idx < records.Count; idx++)
                {
                    var (recOff, NL) = records[idx];
                    int recSize = NL + 55;
                    if (recOff + recSize > decoded.Length) continue;

                    int posOff = bodyStart + recOff + recSize - 50;
                    int starOff = bodyStart + recOff + recSize - 49;
                    int forcaOff = bodyStart + recOff + recSize - 48;
                    int compOff = bodyStart + recOff + recSize - 33;
                    int salarioOff = bodyStart + recOff + recSize - 25;

                    if (salarioOff + 2 > bytes.Length) break;

                    // Forca eh uint16 LE em [sz-48..sz-47]. Normal 0-99 usa
                    // so o low byte; user pode setar ate 9999 (uint16 max).
                    int forcaVal = BitConverter.ToUInt16(bytes, forcaOff);
                    var p = new SavePlayer
                    {
                        Nome = ExtractPlayerName(decoded, recOff, NL),
                        RecordStartInEft = recOff,
                        RecordSizeInEft = recSize,
                        Posicao = PosicaoLabel(bytes[posOff]),
                        Estrela = bytes[starOff] != 0,
                        Forca = forcaVal,
                        Comportamento = bytes[compOff],
                        PosicaoOffsetInFile = posOff,
                        EstrelaOffsetInFile = starOff,
                        ForcaOffsetInFile = forcaOff,
                        SalarioOffsetInFile = salarioOff,
                        Salario = BitConverter.ToUInt16(bytes, salarioOff),
                    };
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
                    if (p.ForcaOffsetInFile > 0 && p.ForcaOffsetInFile + 2 <= bytes.Length)
                    {
                        int f = Math.Max(FORCA_MIN, Math.Min(FORCA_MAX, p.Forca));
                        var fb = BitConverter.GetBytes((ushort)f);
                        bytes[p.ForcaOffsetInFile] = fb[0];
                        bytes[p.ForcaOffsetInFile + 1] = fb[1];
                    }
                    if (p.SalarioOffsetInFile > 0 && p.SalarioOffsetInFile + 2 <= bytes.Length)
                    {
                        int s = Math.Max(SALARIO_MIN, Math.Min(SALARIO_MAX, p.Salario));
                        var sb = BitConverter.GetBytes((ushort)s);
                        bytes[p.SalarioOffsetInFile] = sb[0];
                        bytes[p.SalarioOffsetInFile + 1] = sb[1];
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

        // Team name: byte em decoded[0x2E] = 0x20 + name_len. Nome comeca em
        // 0x2F. Padding de 0x20 (space) precede o length byte.
        private static string ExtractTeamName(byte[] decoded)
        {
            // Length byte tipicamente em 0x2E mas pode variar. Escaneia
            // 0x28..0x32 procurando o primeiro byte >= 0x22 e <= 0x50 seguido
            // de chars validos.
            for (int lenPos = 0x28; lenPos <= 0x32 && lenPos + 1 < decoded.Length; lenPos++)
            {
                byte lenByte = decoded[lenPos];
                if (lenByte < 0x22 || lenByte > 0x50) continue;
                int nameLen = lenByte - 0x20;
                if (lenPos + 1 + nameLen > decoded.Length) continue;

                var sb = new StringBuilder(nameLen);
                int validChars = 0;
                for (int i = lenPos + 1; i < lenPos + 1 + nameLen; i++)
                {
                    byte b = decoded[i];
                    char? c = MapTeamChar(b);
                    if (c != null) { sb.Append(c.Value); validChars++; }
                    else sb.Append('?');
                }
                if (validChars >= nameLen * 3 / 4)
                    return sb.ToString().Trim().ToUpperInvariant();
            }
            return "?";
        }

        private static char? MapTeamChar(byte b)
        {
            if (b >= 0x61 && b <= 0x7A) return (char)b;
            if (b >= 0x81 && b <= 0x9A) return (char)(b - 0x20);
            if (b >= 0xA1 && b <= 0xBA) return (char)(b - 0x40); // final char shifted
            if (b >= 0x50 && b <= 0x59) return (char)(b - 0x20); // digitos shifted
            if (b >= 0x30 && b <= 0x39) return (char)b;          // digitos raw
            if (b == 0x40) return ' ';
            if (b == 0x4D || b == 0x4B || b == 0x2D) return '-';
            if (b == 0x2E) return '.';
            // Acentos ISO-8859-1 diretos
            if (b == 0xE1) return 'á';
            if (b == 0xE3) return 'ã';
            if (b == 0xE7) return 'ç';
            if (b == 0xE9) return 'é';
            if (b == 0xED) return 'í';
            if (b == 0xF3) return 'ó';
            if (b == 0xF4) return 'ô';
            if (b == 0xF5) return 'õ';
            if (b == 0xFA) return 'ú';
            if (b == 0xF1) return 'ñ';
            // Acentos shifted +0x20 (embedded ciphered)
            if (b == 0x01) return 'á';
            if (b == 0x03) return 'ã';
            if (b == 0x07) return 'ç';
            if (b == 0x09) return 'é';
            if (b == 0x0D) return 'í';
            if (b == 0x11) return 'ñ';
            if (b == 0x13) return 'ó';
            if (b == 0x14) return 'ô';
            if (b == 0x15) return 'õ';
            if (b == 0x1A) return 'ú';
            return null;
        }

        // Player name: decoded[recStart+5..+5+NL-1]
        // 1a char lowercase (0x61-0x7A) unshifted
        // Resto shifted (0x81-0x9A) → letters
        // Final char pode ser shifted +0x40 (0xA1-0xBA)
        // Espaco = 0x40, digitos = 0x30-0x39, acentos = valores baixos ou altos
        private static string ExtractPlayerName(byte[] decoded, int recStart, int NL)
        {
            if (decoded.Length < recStart + 5 + NL) return "?";
            var sb = new StringBuilder(NL);
            bool afterSpace = false;
            for (int i = 0; i < NL; i++)
            {
                byte b = decoded[recStart + 5 + i];
                char? c = MapTeamChar(b);
                if (c == null)
                {
                    // Alguns nomes tem final char em outros ranges — skip
                    continue;
                }
                if (i == 0 || afterSpace)
                {
                    sb.Append(char.ToUpperInvariant(c.Value));
                    afterSpace = false;
                }
                else
                {
                    sb.Append(c.Value);
                }
                if (c.Value == ' ') afterSpace = true;
            }
            return sb.ToString().Trim();
        }

        private static string PosicaoLabel(byte b) => b switch
        {
            0 => "G",
            1 => "D",
            2 => "M",
            3 => "A",
            _ => "?"
        };

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
