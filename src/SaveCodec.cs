using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    public class SavePlayer
    {
        public string Nome { get; set; } = "";
        public string Posicao { get; set; } = "G"; // G/D/M/A
        public bool Estrela { get; set; }          // * no jogo
        public int Comportamento { get; set; }     // 0..5
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
    // Estrutura descoberta empiricamente (v0.4.12, 2026-07-06):
    //
    // Cada player record no EFT decoded body:
    //   +0     : marker (any byte < 0x30)
    //   +1..+3 : nacionalidade (3 lowercase, ex 'bra', 'por', 'esp')
    //   +4     : NL = comprimento do nome
    //   +5..+5+NL-1 : nome (1a letra unshifted, resto shifted +0x20)
    //   +5+NL  : POSICAO (0=G, 1=D, 2=M, 3=A)
    //   +5+NL+1: ESTRELA (0/1) — jogador com *
    //   +5+NL+2: FORCA (0..99)
    //   +sz-33 : COMPORTAMENTO (0..5)
    //   +sz-25..sz-24: SALARIO uint16 LE
    //   +sz-2..sz-1  : PLAYER_ID uint16 LE
    // record_size = NL + 55
    //
    // VERBA: uint32 LE armazenado 2 vezes consecutivos no EFT header
    // (offset varia por time). Detectamos scaneando raw[0x80..0x120] por
    // duas uint32 consecutivas iguais e > 0.
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
            "Caneleiro", "Caceteiro", "Sarrafeiro"
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
                team.VerbaOffset = FindVerbaOffset(bytes, eftStart, eftEnd);
                if (team.VerbaOffset > 0)
                    team.Verba = (uint)BitConverter.ToInt32(bytes, team.VerbaOffset);

                // Encontra markers + calcula recSize via boundary + estatistica
                // pro ultimo. NL = recSize - 55 (fixo).
                var starts = FindPlayerStarts(decoded);
                var prevSizes = new List<int>();
                for (int j = 0; j < starts.Count - 1; j++)
                    prevSizes.Add(starts[j + 1] - starts[j]);
                int medianSize = 60;
                if (prevSizes.Count > 0)
                {
                    prevSizes.Sort();
                    medianSize = prevSizes[prevSizes.Count / 2];
                }

                for (int idx = 0; idx < starts.Count; idx++)
                {
                    int recStart = starts[idx];
                    int naiveEnd = idx + 1 < starts.Count ? starts[idx + 1] : decoded.Length;
                    int recSize = naiveEnd - recStart;
                    bool isLast = idx + 1 >= starts.Count;
                    if (isLast)
                    {
                        // Estrategia especializada pro ultimo record: tenta
                        // varios recSizes (55 = NL+55 pra NL de 0..30) e
                        // escolhe o que produz posicao valida (0-3) e forca
                        // razoavel (1-100). Se nenhum bater, cai pra mediana.
                        int found = -1;
                        for (int tryNL = 3; tryNL <= 25; tryNL++)
                        {
                            int trySize = tryNL + 55;
                            if (recStart + trySize > decoded.Length) break;
                            int tryPosAbs = bodyStart + recStart + trySize - 50;
                            int tryForcaAbs = bodyStart + recStart + trySize - 48;
                            if (tryForcaAbs >= bytes.Length) break;
                            byte posB = bytes[tryPosAbs];
                            byte forcaB = bytes[tryForcaAbs];
                            if (posB <= 3 && forcaB >= 1 && forcaB <= 100)
                            {
                                found = trySize;
                                break;
                            }
                        }
                        recSize = found > 0 ? found : Math.Min(recSize, medianSize);
                    }
                    if (recSize < 55) continue;

                    // Offsets fixos do FIM do record:
                    int posOff = recStart + recSize - 50;
                    int starOff = recStart + recSize - 49;
                    int forcaOff = recStart + recSize - 48;
                    int compOff = recStart + recSize - 33;
                    int salarioOff = recStart + recSize - 25;
                    int NL = recSize - 55;

                    if (NL < 3 || NL > 30) continue;
                    // Campos numericos lidos do arquivo RAW (nao cifrados —
                    // por design do formato, esses bytes ficam intactos).
                    // Nome vem do DECODED (cifra aplicada em texto).
                    int posOffFile = bodyStart + posOff;
                    int starOffFile = bodyStart + starOff;
                    int forcaOffFile = bodyStart + forcaOff;
                    int compOffFile = bodyStart + compOff;
                    int salarioOffFile = bodyStart + salarioOff;

                    var p = new SavePlayer
                    {
                        Nome = ExtractPlayerName(decoded, recStart, NL),
                        RecordStartInEft = recStart,
                        RecordSizeInEft = recSize,
                        Posicao = posOffFile < bytes.Length ? PosicaoLabel(bytes[posOffFile]) : "?",
                        Estrela = starOffFile < bytes.Length && bytes[starOffFile] != 0,
                        Forca = forcaOffFile < bytes.Length ? bytes[forcaOffFile] : 0,
                        Comportamento = compOffFile < bytes.Length ? bytes[compOffFile] : 0,
                        PosicaoOffsetInFile = posOffFile,
                        EstrelaOffsetInFile = starOffFile,
                        ForcaOffsetInFile = forcaOffFile,
                        SalarioOffsetInFile = salarioOffFile,
                    };
                    if (salarioOffFile + 2 <= bytes.Length)
                        p.Salario = BitConverter.ToUInt16(bytes, salarioOffFile);
                    team.Players.Add(p);
                }

                sf.Teams.Add(team);
            }
            return sf;
        }

        public static void Write(string path, SaveFile sf)
        {
            // Re-encoding com cipher rolante eh complexo (mudar 1 byte muda
            // cipher de todos os subsequentes). Solucao: para cada EFT
            // modificado, decoda, aplica mudancas, re-codifica INTEIRO.
            var bytes = (byte[])sf.RawBytes.Clone();
            var eftPositions = FindAllEftStarts(bytes);

            foreach (var team in sf.Teams)
            {
                int eftIdx = eftPositions.IndexOf(team.EftStartOffset);
                if (eftIdx < 0) continue;
                int eftStart = eftPositions[eftIdx];
                int eftEnd = eftIdx + 1 < eftPositions.Count ? eftPositions[eftIdx + 1] : bytes.Length;
                int bodyStart = eftStart + 4;

                var decoded = DecodeCaesar(bytes, bodyStart, eftEnd);
                bool dirty = false;

                // Verba (nao passa pela cifra — offset fixo no header raw)
                if (team.VerbaOffset > 0 && team.VerbaOffset + 4 <= bytes.Length)
                {
                    var v = (uint)Math.Max(0, Math.Min(uint.MaxValue, (ulong)team.Verba));
                    var vb = BitConverter.GetBytes(v);
                    Array.Copy(vb, 0, bytes, team.VerbaOffset, 4);
                    // Verba armazenada duas vezes consecutivas
                    if (team.VerbaOffset + 8 <= bytes.Length)
                        Array.Copy(vb, 0, bytes, team.VerbaOffset + 4, 4);
                }

                // Campos numericos: escrever direto no RAW file (nao passam
                // pela cifra). Nao precisa re-encodar EFT.
                foreach (var p in team.Players)
                {
                    if (p.ForcaOffsetInFile > 0 && p.ForcaOffsetInFile < bytes.Length)
                    {
                        int f = Math.Max(FORCA_MIN, Math.Min(FORCA_MAX, p.Forca));
                        bytes[p.ForcaOffsetInFile] = (byte)(f & 0xFF);
                    }
                    if (p.SalarioOffsetInFile > 0 && p.SalarioOffsetInFile + 2 <= bytes.Length)
                    {
                        int s = Math.Max(SALARIO_MIN, Math.Min(SALARIO_MAX, p.Salario));
                        var sb = BitConverter.GetBytes((ushort)s);
                        bytes[p.SalarioOffsetInFile] = sb[0];
                        bytes[p.SalarioOffsetInFile + 1] = sb[1];
                    }
                }
                _ = dirty; // suprime warning; nao precisamos re-encoder
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

        // Verba do time = uint32 LE armazenado 2 vezes consecutivas dentro do
        // header do EFT. Escaneamos 0x80..0x120 procurando essa assinatura.
        //
        // Nuance: verbas com byte alto = 0 (V < 16M) criam FALSE-MATCH em
        // offset shifted (por 1 byte), pq a shifted-uint32 fica V*256 e o
        // padding 0x00 antes cria o twice-pattern. E ha tambem shift-fake +1
        // que da V/256. Para distinguir:
        // - Coleta TODAS as matches em [0x80..0x120]
        // - Filtra pra faixa razoavel [100k, 50M]
        // - Prefere o MAIOR (fake shift+1 divide por 256, ficando bem menor)
        private static int FindVerbaOffset(byte[] bytes, int eftStart, int eftEnd)
        {
            int max = Math.Min(eftEnd - 8, eftStart + 0x120);
            int bestOff = -1;
            uint bestVal = 0;
            int fallbackOff = -1;
            uint fallbackVal = 0;
            for (int off = eftStart + 0x80; off <= max; off++)
            {
                uint v1 = (uint)BitConverter.ToInt32(bytes, off);
                uint v2 = (uint)BitConverter.ToInt32(bytes, off + 4);
                if (v1 != v2) continue;
                if (v1 < 10_000 || v1 > 1_000_000_000) continue;
                if (fallbackOff < 0) { fallbackOff = off; fallbackVal = v1; }
                if (v1 >= 100_000 && v1 <= 50_000_000 && v1 > bestVal)
                {
                    bestOff = off;
                    bestVal = v1;
                }
            }
            if (bestOff >= 0) return bestOff;
            return fallbackOff;
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

        // Reverse: cipher[i] = (plain[i] + delta) mod 256;
        // delta = (delta + plain[i] - 0x20) mod 256
        private static byte[] EncodeCaesar(byte[] plain)
        {
            var cipher = new byte[plain.Length];
            int delta = 0;
            for (int i = 0; i < plain.Length; i++)
            {
                int c = (plain[i] + delta) & 0xFF;
                cipher[i] = (byte)c;
                delta = (delta + plain[i] - 0x20) & 0xFF;
            }
            return cipher;
        }

        private static string ExtractTeamName(byte[] decoded)
        {
            for (int i = 0x28; i < 0x40 && i + 1 < decoded.Length; i++)
            {
                byte lenByte = decoded[i];
                if (lenByte < 0x22 || lenByte > 0x40) continue;
                int nameLen = lenByte - 0x20;
                if (i + 1 + nameLen > decoded.Length) continue;
                var candidate = TeamNameFromBytes(decoded, i + 1, nameLen);
                if (candidate != null) return candidate;
            }
            return "?";
        }

        private static string? TeamNameFromBytes(byte[] decoded, int start, int len)
        {
            var sb = new StringBuilder(len);
            int letterCount = 0;
            int invalid = 0;
            for (int i = start; i < start + len && i < decoded.Length; i++)
            {
                byte b = decoded[i];
                char? c = null;
                if (b == 0x40) c = ' ';
                else if (b == 0x4D || b == 0x4B || b == 0x2D) c = '-';
                else if (b == 0x2E) c = '.';
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
                // Nomes de times armazenam digitos como P/Q/R/S/T/U/V/W/X/Y
                // (shifted +0x20 from '0'-'9'). Ex: 1993=QYYS, 1970=QYWP.
                // Nunca ha letras uppercase reais em team names — todas
                // sao lowercase e display faz uppercase.
                else if (b >= 0x50 && b <= 0x59) { c = (char)(b - 0x20); letterCount++; }
                else if (b >= 0x30 && b <= 0x39) c = (char)b;
                else { invalid++; continue; }
                if (c != null) sb.Append(c.Value);
            }
            if (letterCount < 3) return null;
            if (invalid > len / 3) return null;
            return sb.ToString().Trim().ToUpperInvariant();
        }

        private static List<int> FindPlayerStarts(byte[] decoded)
        {
            var starts = new List<int>();
            for (int i = 0; i < decoded.Length - 6; i++)
            {
                byte a = decoded[i + 1], b = decoded[i + 2], c = decoded[i + 3];
                byte pos = decoded[i + 4];
                byte m = decoded[i];
                byte initial = decoded[i + 5];
                bool nat = a >= 'a' && a <= 'z' && b >= 'a' && b <= 'z' && c >= 'a' && c <= 'z';
                bool posOk = pos >= 0x30 && pos <= 0x5A;
                bool markerOk = m < 0x30;
                bool initialOk = initial >= 'a' && initial <= 'z';
                if (nat && posOk && markerOk && initialOk)
                    starts.Add(i);
            }
            return starts;
        }

        // Extrai nome: raw[5]=inicial unshifted, raw[6..5+NL-1]=resto shifted
        private static string ExtractPlayerName(byte[] decoded, int recStart, int NL)
        {
            if (decoded.Length < recStart + 5 + NL) return "?";
            var sb = new StringBuilder(NL);
            char initial = (char)decoded[recStart + 5];
            sb.Append(char.ToUpperInvariant(initial));

            bool afterSpace = false;
            for (int i = recStart + 6; i < recStart + 5 + NL; i++)
            {
                byte b = decoded[i];
                if (afterSpace && b >= 0x61 && b <= 0x7A)
                {
                    sb.Append(char.ToUpperInvariant((char)b));
                    afterSpace = false;
                    continue;
                }
                afterSpace = false;
                char? cc = MapNameByte(b);
                if (cc == null) { sb.Append('?'); continue; }
                sb.Append(cc.Value);
                if (cc.Value == ' ') afterSpace = true;
            }
            return sb.ToString().TrimEnd();
        }

        private static char? MapNameByte(byte b)
        {
            if (b >= 0x81 && b <= 0x9A) return (char)(b - 0x20);
            if (b == 0x40) return ' ';
            if (b >= 0x30 && b <= 0x39) return (char)b; // digitos
            // Digitos shifted (P/Q/R/S/T/U/V/W/X/Y = 0-9)
            if (b >= 0x50 && b <= 0x59) return (char)(b - 0x20);
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
