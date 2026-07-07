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

                // Player records. Todos os markers detectados sao players
                // reais — team header nao aparece porque nao tem 'bra'
                // preciso e o filtro initial-lowercase rejeita ele.
                var starts = FindPlayerStarts(decoded);
                // Coleta tamanhos dos records nao-ultimos primeiro
                var prevSizes = new List<int>();
                for (int i = 0; i < starts.Count - 1; i++)
                    prevSizes.Add(starts[i + 1] - starts[i]);
                // Mediana pra estimar tamanho do ultimo record
                int medianSize = 60;
                if (prevSizes.Count > 0)
                {
                    prevSizes.Sort();
                    medianSize = prevSizes[prevSizes.Count / 2];
                }

                for (int i = 0; i < starts.Count; i++)
                {
                    int recStart = starts[i];
                    int naiveEnd = i + 1 < starts.Count ? starts[i + 1] : decoded.Length;
                    int recSize = naiveEnd - recStart;
                    bool isLast = i + 1 >= starts.Count;
                    if (isLast)
                        recSize = Math.Min(recSize, medianSize);

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

        // Verba do time = uint32 LE. Offset varia entre 2 layouts observados:
        //  A) Juventus-style (single-coach BR): +0xDC = marker 500000
        //     (bytes 20 A1 07 00), verba em +0xE0.
        //  B) Criciuma-style (multi-coach ou coach info embutido):
        //     byte +0xDC = 0x00 padding, verba em +0xDD.
        // Descobrimos observando byte +0xDC.
        private static int FindVerbaOffset(byte[] bytes, int eftStart)
        {
            int dc = eftStart + 0xDC;
            if (dc + 4 > bytes.Length) return -1;

            // Layout B: byte em +0xDC eh 0x00 (padding). Verba em +0xDD.
            if (bytes[dc] == 0x00 && dc + 5 <= bytes.Length)
            {
                uint vDd = (uint)BitConverter.ToInt32(bytes, dc + 1);
                if (IsPlausibleVerba(vDd)) return dc + 1;
            }

            // Layout A: verba em +0xE0
            if (dc + 8 <= bytes.Length)
            {
                uint vE0 = (uint)BitConverter.ToInt32(bytes, dc + 4);
                if (IsPlausibleVerba(vE0)) return dc + 4;
            }

            // Fallback: procura primeira uint32 plausivel no range 0xD8..0xE8
            for (int off = 0xD8; off <= 0xE8; off++)
            {
                int abs = eftStart + off;
                if (abs + 4 > bytes.Length) continue;
                uint v = (uint)BitConverter.ToInt32(bytes, abs);
                if (IsPlausibleVerba(v)) return abs;
            }
            return -1;
        }

        // Verba plausivel: um time BR novo comeca com 500k..5M. Podemos ir
        // ate 100M pra times ricos. Acima disso quase sempre eh outro campo.
        private static bool IsPlausibleVerba(uint v)
            => v >= 50_000 && v <= 100_000_000;

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

        // Nome do time decoded: byte em ~0x2C eh comprimento shifted (+0x20).
        // Exatamente N bytes de nome seguem. Depois vem short name idem.
        // Pra usuario, mostramos o nome longo em UPPERCASE tipo game screen.
        private static string ExtractTeamName(byte[] decoded)
        {
            // Encontra length byte: procura em 0x28..0x40 o primeiro byte
            // >= 0x20 seguido de letras minusculas.
            for (int i = 0x28; i < 0x40 && i + 1 < decoded.Length; i++)
            {
                byte lenByte = decoded[i];
                if (lenByte < 0x22 || lenByte > 0x40) continue; // len 2..32
                int nameLen = lenByte - 0x20;
                if (i + 1 + nameLen > decoded.Length) continue;
                // Verifica se os bytes seguintes formam nome plausivel
                var candidate = TeamNameFromBytes(decoded, i + 1, nameLen);
                if (candidate != null) return candidate;
            }
            return "?";
        }

        // Converte N bytes de nome do time em string. Retorna null se
        // bytes nao parecem nome (falha check).
        private static string? TeamNameFromBytes(byte[] decoded, int start, int len)
        {
            var sb = new StringBuilder(len);
            int letterCount = 0;
            int invalid = 0;
            for (int i = start; i < start + len && i < decoded.Length; i++)
            {
                byte b = decoded[i];
                char? c = null;
                // Ordem importa: acentos (bytes baixos) e separadores especiais
                // ANTES da faixa geral A-Z/a-z.
                if (b == 0x40) c = ' ';
                else if (b == 0x4D || b == 0x4B || b == 0x2D) c = '-';
                else if (b == 0x2E) c = '.';
                else if (b == 0x2F) c = '/';
                // Acentos ISO-8859-1 shifted +0x20 mod 256:
                else if (b == 0x01) { c = 'á'; letterCount++; }
                else if (b == 0x02) { c = 'â'; letterCount++; }
                else if (b == 0x03) { c = 'ã'; letterCount++; }
                else if (b == 0x07) { c = 'ç'; letterCount++; }
                else if (b == 0x09) { c = 'é'; letterCount++; }
                else if (b == 0x0A) { c = 'ê'; letterCount++; }
                else if (b == 0x0D) { c = 'í'; letterCount++; }
                else if (b == 0x11) { c = 'ñ'; letterCount++; }
                else if (b == 0x13) { c = 'ó'; letterCount++; }
                else if (b == 0x14) { c = 'ô'; letterCount++; }
                else if (b == 0x15) { c = 'õ'; letterCount++; }
                else if (b == 0x1A) { c = 'ú'; letterCount++; }
                else if (b == 0x1C) { c = 'ü'; letterCount++; }
                else if (b >= 0x61 && b <= 0x7A) { c = (char)b; letterCount++; }
                else if (b >= 0x41 && b <= 0x5A) { c = (char)b; letterCount++; }
                else if (b >= 0x30 && b <= 0x39) c = (char)b;
                else { invalid++; continue; }
                if (c != null) sb.Append(c.Value);
            }
            // Precisa ter maioria de letras validas
            if (letterCount < 3) return null;
            if (invalid > len / 3) return null;
            var s = sb.ToString().Trim();
            return s.ToUpperInvariant();
        }

        // Detecta inicios de player records. Marker: byte + 3 letras lowercase
        // (nacionalidade) + byte uppercase (pos_code). Nao so 'bra' — times
        // tem jogadores estrangeiros (por, ita, esp, arg, etc).
        private static List<int> FindPlayerStarts(byte[] decoded)
        {
            var starts = new List<int>();
            for (int i = 0; i < decoded.Length - 6; i++)
            {
                byte a = decoded[i + 1], b = decoded[i + 2], c = decoded[i + 3];
                byte pos = decoded[i + 4];
                bool nat = a >= 'a' && a <= 'z' && b >= 'a' && b <= 'z' && c >= 'a' && c <= 'z';
                bool posOk = pos >= 'A' && pos <= 'Z';
                // Marker byte: qualquer coisa < 0x30 (control chars, espaco,
                // !, ", #, $, etc.). Saves da comunidade (7.e98) usam markers
                // como 0x1F para alguns times. Confiamos nos outros checks
                // (nat lowercase, pos uppercase, initial lowercase) pra evitar
                // falsos positivos.
                byte m = decoded[i];
                bool markerOk = m < 0x30;
                byte initial = decoded[i + 5];
                bool initialOk = initial >= 'a' && initial <= 'z';
                if (nat && posOk && markerOk && initialOk)
                    starts.Add(i);
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

            // Nome termina 2 bytes antes da forca (empirico). Antes usava
            // -3 mas cortava o ultimo char (Jefferson -> Jefferso).
            int nameEnd = recStart + forcaLocal - 2;
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
