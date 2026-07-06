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
    // Descoberto empiricamente:
    // - Save = header + 42 blobs EFT + calendario/footer.
    // - Cada EFT comeca com magic 'EFa\0' + body cifrado Caesar rolante.
    // - Verba do clube: uint32 LE em +0xE0 dentro do EFT do coached team.
    // - EFT coached: contem 'juventus' no body decodificado (fallback:
    //   marker uint32 500000 em +0xDC).
    // - Player records: marker <any> + 'bra' + [A-Z pos_code] + initial +
    //   shifted_rest_of_name + fixed_stats.
    // - FORCA:   byte  em (record_size - 48) do record (RAW bytes, sem cifra).
    // - SALARIO: uint16 LE em (record_size - 25) do record (RAW bytes).
    public static class SaveCodec
    {
        private static readonly byte[] EFT_MAGIC = { (byte)'E', (byte)'F', (byte)'a', 0 };
        private const int EFT_MARKER_OFFSET = 0xDC;
        private const int EFT_VERBA_OFFSET = 0xE0;
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
            int eftStart = FindCoachedEftStart(bytes);
            if (eftStart < 0) return sf;
            int eftEnd = FindNextEftStart(bytes, eftStart);
            if (eftEnd < 0) eftEnd = bytes.Length;

            if (eftStart + EFT_VERBA_OFFSET + 4 <= bytes.Length)
            {
                sf.VerbaOffset = eftStart + EFT_VERBA_OFFSET;
                sf.Verba = (uint)BitConverter.ToInt32(bytes, sf.VerbaOffset);
            }

            int bodyStart = eftStart + 4;
            int bodyEnd = eftEnd;
            var decoded = DecodeCaesar(bytes, bodyStart, bodyEnd);
            var starts = FindPlayerStarts(decoded);

            // Tamanho MEDIANO do record calculado dos players anteriores.
            // Usado como upper bound pro ULTIMO player (o marker do proximo
            // player nao existe, entao naive end=decoded.Length gera record
            // enorme e forca/salario ficam em posicao errada).
            int lastRecCap = 130; // fallback razoavel

            for (int i = 1; i < starts.Count; i++)
            {
                int recStart = starts[i];
                int naiveEnd = i + 1 < starts.Count ? starts[i + 1] : decoded.Length;
                int recSize = naiveEnd - recStart;

                bool isLast = i + 1 >= starts.Count;
                if (isLast && recSize > lastRecCap)
                {
                    // Usa mediana dos records anteriores
                    recSize = Math.Min(lastRecCap, naiveEnd - recStart);
                }
                else if (!isLast)
                {
                    // Update running median estimate a cada player
                    lastRecCap = Math.Max(lastRecCap, recSize + 20);
                }

                if (recSize < FORCA_OFFSET_FROM_REC_END + 1) continue;

                int forcaLocal = recSize - FORCA_OFFSET_FROM_REC_END;
                int salarioLocal = recSize - SALARIO_OFFSET_FROM_REC_END;

                var p = new SavePlayer
                {
                    Nome = ExtractName(decoded, recStart, forcaLocal),
                    RecordStartInEft = recStart,
                    RecordSizeInEft = recSize,
                };

                p.ForcaOffsetInFile = bodyStart + recStart + forcaLocal;
                p.SalarioOffsetInFile = bodyStart + recStart + salarioLocal;
                if (p.ForcaOffsetInFile < bytes.Length)
                    p.Forca = bytes[p.ForcaOffsetInFile];
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
                if (p.ForcaOffsetInFile > 0 && p.ForcaOffsetInFile < bytes.Length)
                {
                    var f = Math.Max(FORCA_MIN, Math.Min(FORCA_MAX, p.Forca));
                    // Forca <= 255 cabe em byte. Se > 255, precisa de 2 bytes
                    // mas o campo eh 1 byte no formato original — game aceita
                    // valores > 255 truncando. Salvamos byte low.
                    bytes[p.ForcaOffsetInFile] = (byte)(f & 0xFF);
                    // Se forca > 255, sobrescreve tambem o byte anterior
                    // (complemento par sum) pra manter o par valido
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

            File.WriteAllBytes(path, bytes);
        }

        // ---- helpers ----

        private static int FindCoachedEftStart(byte[] bytes)
        {
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
            => IndexOf(bytes, EFT_MAGIC, after + 4);

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

        // Extrai o nome do jogador do record decodificado.
        // Formato observado:
        //   +4: pos_code (uppercase A-Z)
        //   +5: initial da primeira palavra (letra minuscula unshifted)
        //   +6..: resto shifted +0x20 (letras minusculas viram 0x81-0x9A;
        //         acentos ptbr viram valores baixos como 0x09='é')
        //   Se nome tem espaco:
        //     @ (0x40) = espaco shifted
        //     Byte seguinte ao espaco: initial da 2a palavra (unshifted 0x61-0x7A)
        //     Resto da 2a palavra: shifted como antes
        // O nome termina 3 bytes antes da forca (empiricamente).
        private static string ExtractName(byte[] decoded, int recStart, int forcaLocal)
        {
            if (decoded.Length < recStart + 6) return "?";
            var sb = new StringBuilder();
            char initial = (char)decoded[recStart + 5];
            sb.Append(char.ToUpperInvariant(initial));

            // Nome termina 3 bytes antes do byte de forca (empirico).
            int nameEnd = recStart + forcaLocal - 3;
            if (nameEnd > decoded.Length) nameEnd = decoded.Length;

            bool afterSpace = false;
            for (int i = recStart + 6; i < nameEnd; i++)
            {
                byte b = decoded[i];

                // Se acabou de vir espaco, proximo byte eh initial da 2a palavra
                // (letra minuscula UNSHIFTED)
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

        // Mapeia byte decoded pra caractere do nome. Retorna null se nao eh
        // parte de nome.
        private static char? MapNameByte(byte b)
        {
            // letras minusculas shifted (a-z -> 0x81-0x9A)
            if (b >= 0x81 && b <= 0x9A) return (char)(b - 0x20);
            // Space shifted (@)
            if (b == 0x40) return ' ';
            // Acentos ISO-8859-1 shifted +0x20 mod 256:
            // á(0xE1)->0x01, â(0xE2)->0x02, ã(0xE3)->0x03, ç(0xE7)->0x07,
            // é(0xE9)->0x09, ê(0xEA)->0x0A, í(0xED)->0x0D,
            // ó(0xF3)->0x13, ô(0xF4)->0x14, õ(0xF5)->0x15,
            // ú(0xFA)->0x1A, ü(0xFC)->0x1C
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
