using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    public class SavePlayer
    {
        public string Nome { get; set; } = "";
        public string Posicao { get; set; } = "M";
        public byte Estatuto { get; set; } = 3;
        public byte Moral { get; set; } = 5;
        public byte Energia { get; set; } = 5;
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
    // Estrutura descoberta (2026-07-06):
    // - Save = header + 42 EFT team blobs consecutivos + calendario/footer.
    // - Cada EFT comeca com magic 'EFa\0'.
    // - Uma das 42 EFTs pertence ao time COACHED (usuario). Se identifica
    //   pelo marker uint32 500000 no offset +0xDC do EFT.
    // - Verba do clube = uint32 LE em +0xE0 dentro do EFT do time coached.
    //
    // TODO:
    //   - Cifra dos nomes de jogador (Caesar rolante — mesma do .EFT files
    //     standalone: plain[i] = (cipher[i] - delta) mod 256;
    //     delta = (delta + plain[i] - 0x20) mod 256, reset por EFT).
    //   - Offsets de moral/energia/estatuto dentro do EFT.
    //   - Adicionar/remover jogador (requer recomputar tamanhos e cifras).
    public static class SaveCodec
    {
        private static readonly byte[] EFT_MAGIC = new byte[] { (byte)'E', (byte)'F', (byte)'a', 0 };
        private const int OFFSET_MARKER = 0xDC;   // +offset dentro do EFT
        private const int OFFSET_VERBA  = 0xE0;   // +offset dentro do EFT
        private const uint MARKER_VALUE = 500000; // identifica o EFT do time coached

        public static SaveFile Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var sf = new SaveFile { RawBytes = bytes };
            int eftStart = FindCoachedEftStart(bytes);
            if (eftStart >= 0 && eftStart + OFFSET_VERBA + 4 <= bytes.Length)
            {
                sf.VerbaOffset = eftStart + OFFSET_VERBA;
                sf.Verba = (uint)BitConverter.ToInt32(bytes, sf.VerbaOffset);
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
            File.WriteAllBytes(path, bytes);
        }

        // Retorna offset do inicio (magic 'EFa\0') do EFT do time coached.
        // -1 se nao encontrado.
        private static int FindCoachedEftStart(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length - EFT_MAGIC.Length)
            {
                int p = IndexOf(bytes, EFT_MAGIC, i);
                if (p < 0) return -1;
                if (p + OFFSET_MARKER + 4 <= bytes.Length)
                {
                    uint marker = (uint)BitConverter.ToInt32(bytes, p + OFFSET_MARKER);
                    if (marker == MARKER_VALUE) return p;
                }
                i = p + 1;
            }
            return -1;
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            for (int i = start; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
