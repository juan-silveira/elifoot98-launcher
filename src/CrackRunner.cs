using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ElifootLauncher
{
    // Gera a Contra-Senha rodando o CRACK.EXE (DOS) no DOSBox escondido.
    // Entrada e saida via redirecionamento (CRACK.EXE < IN.TXT > OUT.TXT):
    // nao precisa simular teclado nem dar foco na janela do DOSBox.
    public static class CrackRunner
    {
        // 9 = "Registro para autor 2" (contra-senha comeca com 9)
        private const int TipoAutor2 = 9;

        public static string GerarContraSenha(GameLauncher launcher, string senha)
        {
            if (!File.Exists(launcher.CrackExe))
                throw new FileNotFoundException($"CRACK.EXE não encontrado: {launcher.CrackExe}");
            if (!File.Exists(launcher.DosBoxExe))
                throw new FileNotFoundException($"DOSBox não encontrado: {launcher.DosBoxExe}");

            var workDir = Path.Combine(Path.GetTempPath(), "elifoot_crack_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(workDir);
            Process? proc = null;
            try
            {
                File.Copy(launcher.CrackExe, Path.Combine(workDir, "CRACK.EXE"), overwrite: true);

                // O CRACK descarta o 1o caractere lido antes do menu, por isso o \r
                // inicial. Depois: tipo, senha + ENTER, e 'S' pra sair do menu.
                File.WriteAllText(Path.Combine(workDir, "IN.TXT"),
                    $"\r{TipoAutor2}{senha}\r\nS\r\n", Encoding.ASCII);

                var confPath = Path.Combine(workDir, "dosbox.conf");
                File.WriteAllText(confPath, string.Join(Environment.NewLine, new[]
                {
                    "[sdl]",
                    "fullscreen=false",
                    "windowresolution=320x200",
                    "[cpu]",
                    "cycles=max",
                    "[mixer]",
                    "nosound=true",
                    "[autoexec]",
                    $"mount C \"{workDir}\"",
                    "C:",
                    "CRACK.EXE < IN.TXT > OUT.TXT",
                    // Staging bloqueia 'exit' quando o programa roda rapido demais;
                    // o marcador avisa que o CRACK terminou e o DOSBox e encerrado daqui.
                    "echo fim > DONE.TXT",
                    "exit",
                }));

                proc = Process.Start(new ProcessStartInfo
                {
                    FileName = launcher.DosBoxExe,
                    Arguments = $"--noprimaryconf --nolocalconf -conf \"{confPath}\"",
                    WorkingDirectory = launcher.DosBoxDir,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                }) ?? throw new IOException("Não consegui iniciar o DOSBox.");

                // DOSBox-Staging sempre cria janela SDL (ignora SDL_VIDEODRIVER).
                // Esconde qualquer janela do processo enquanto ele roda.
                var pid = (uint)proc.Id;
                var donePath = Path.Combine(workDir, "DONE.TXT");
                while (!proc.WaitForExit(5) && !File.Exists(donePath))
                {
                    if (proc.StartTime < DateTime.Now.AddSeconds(-30))
                        throw new TimeoutException("O DOSBox demorou demais para gerar a contra-senha.");
                    EnumWindows((h, _) =>
                    {
                        GetWindowThreadProcessId(h, out uint p);
                        if (p == pid && IsWindowVisible(h)) ShowWindow(h, SW_HIDE);
                        return true;
                    }, IntPtr.Zero);
                }

                var outPath = Path.Combine(workDir, "OUT.TXT");
                if (!File.Exists(outPath))
                    throw new IOException("O DOSBox não gerou resposta. Verifique se o antivírus está bloqueando o DOSBox.");
                var m = Regex.Match(File.ReadAllText(outPath), @"Contra-senha:\s*(\d{3}-\d{3}-\d{3}-\d{3}-\d{3})");
                if (!m.Success)
                    throw new InvalidDataException("Não consegui gerar a contra-senha. Confira se a senha foi digitada corretamente.");
                return m.Groups[1].Value;
            }
            finally
            {
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
                proc?.Dispose();
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private const int SW_HIDE = 0;
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
