using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    // Codec pra REFEREE.TXE (e outros .TXE do Elifoot 98).
    // Cifra descoberta por engenharia reversa (ver docs/referee-list.md):
    //   cipher[i] = ( plain[i] + (i+2)*L + Σ_{j<i}(i+1-j)*plain[j] ) mod 256
    // onde L é o tamanho do conteúdo do record (sem contar o length byte
    // que é plaintext).
    //
    // Formato de arquivo: sequência de Pascal short-strings [length_byte][content*length].
    // Record 0 de REFEREE.TXE = "Referee" (marca de tipo). Records 1..N = "CCC nome"
    // onde CCC é código do país (3 letras) + espaço + nome do árbitro.
    public static class RefereeCodec
    {
        public class Record
        {
            // Formato lógico: código país (3 letras) + " " + nome. Ex: "POR José Leirós"
            public string CountryCode = "";
            public string Name = "";

            public string FullText => CountryCode + " " + Name;

            public override string ToString() => FullText;
        }

        public class File
        {
            public string TypeMarker = "Referee"; // record 0
            public List<Record> Records = new List<Record>();
        }

        public static File Read(string path)
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            return Parse(bytes);
        }

        public static File Parse(byte[] bytes)
        {
            var file = new File();
            int pos = 0;
            bool first = true;
            while (pos < bytes.Length)
            {
                int len = bytes[pos];
                pos++;
                if (pos + len > bytes.Length) break;
                var content = DecodeContent(bytes, pos, len);
                pos += len;

                if (first)
                {
                    file.TypeMarker = Encoding.GetEncoding("iso-8859-1").GetString(content);
                    first = false;
                }
                else
                {
                    var text = Encoding.GetEncoding("iso-8859-1").GetString(content);
                    var rec = new Record();
                    if (text.Length >= 4 && text[3] == ' ')
                    {
                        rec.CountryCode = text.Substring(0, 3);
                        rec.Name = text.Substring(4);
                    }
                    else
                    {
                        rec.CountryCode = "???";
                        rec.Name = text;
                    }
                    file.Records.Add(rec);
                }
            }
            return file;
        }

        private static byte[] DecodeContent(byte[] cipher, int off, int len)
        {
            var plain = new byte[len];
            for (int i = 0; i < len; i++)
            {
                int K = (i + 2) * len;
                for (int j = 0; j < i; j++)
                    K += (i + 1 - j) * plain[j];
                plain[i] = (byte)((cipher[off + i] - K) & 0xff);
            }
            return plain;
        }

        private static byte[] EncodeContent(byte[] plain)
        {
            int len = plain.Length;
            var cipher = new byte[len];
            for (int i = 0; i < len; i++)
            {
                int K = (i + 2) * len;
                for (int j = 0; j < i; j++)
                    K += (i + 1 - j) * plain[j];
                cipher[i] = (byte)((plain[i] + K) & 0xff);
            }
            return cipher;
        }

        public static void Write(File file, string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                WriteRecord(fs, file.TypeMarker);
                foreach (var r in file.Records)
                {
                    WriteRecord(fs, r.FullText);
                }
            }
        }

        private static void WriteRecord(Stream s, string text)
        {
            var enc = Encoding.GetEncoding("iso-8859-1");
            var bytes = enc.GetBytes(text);
            if (bytes.Length > 255)
                throw new InvalidOperationException($"String demasiado longa (>255): '{text}'");
            s.WriteByte((byte)bytes.Length);
            var cipher = EncodeContent(bytes);
            s.Write(cipher, 0, cipher.Length);
        }
    }
}
