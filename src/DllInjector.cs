using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ElifootLauncher
{
    // Lança um processo suspenso, injeta uma DLL via CreateRemoteThread+LoadLibraryA
    // e resume. Usado pra carregar ScreenHook.dll dentro de otvdmw.exe antes do
    // Elifoot começar a rodar.
    public static class DllInjector
    {
        // Retorna Process no lugar de proc.MainWindowHandle pra o resto do
        // launcher usar como antes. Se DLL nao existe, faz launch normal.
        public static Process? LaunchWithInjectedDll(
            string exePath,
            string args,
            string workingDir,
            string dllPath,
            System.Collections.Generic.IDictionary<string, string>? extraEnv = null)
        {
            if (!File.Exists(dllPath))
            {
                // Fallback: launch normal sem hook
                return LaunchNormal(exePath, args, workingDir, extraEnv);
            }

            var cmdLine = $"\"{exePath}\" {args}";
            var si = new STARTUPINFO { cb = (uint)Marshal.SizeOf<STARTUPINFO>() };
            var pi = new PROCESS_INFORMATION();

            // Monta bloco de env se necessario
            IntPtr envBlock = IntPtr.Zero;
            string? envStr = null;
            if (extraEnv != null && extraEnv.Count > 0)
            {
                envStr = BuildEnvBlock(extraEnv);
                envBlock = Marshal.StringToHGlobalUni(envStr);
            }

            bool ok = CreateProcess(
                null,
                new StringBuilder(cmdLine),
                IntPtr.Zero, IntPtr.Zero,
                false,
                CREATE_SUSPENDED | CREATE_UNICODE_ENVIRONMENT,
                envBlock,
                workingDir,
                ref si, out pi);

            if (envBlock != IntPtr.Zero) Marshal.FreeHGlobal(envBlock);

            if (!ok)
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CreateProcess falhou: 0x{err:x}");
            }

            try
            {
                InjectDll(pi.hProcess, dllPath);
                ResumeThread(pi.hThread);
                return Process.GetProcessById((int)pi.dwProcessId);
            }
            catch
            {
                TerminateProcess(pi.hProcess, 1);
                throw;
            }
            finally
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
        }

        private static Process? LaunchNormal(string exePath, string args, string workingDir,
            System.Collections.Generic.IDictionary<string, string>? extraEnv)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
            };
            if (extraEnv != null)
            {
                foreach (var kv in extraEnv) psi.EnvironmentVariables[kv.Key] = kv.Value;
            }
            return Process.Start(psi);
        }

        private static void InjectDll(IntPtr hProc, string dllPath)
        {
            byte[] pathBytes = Encoding.ASCII.GetBytes(dllPath + "\0");
            IntPtr mem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length,
                MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (mem == IntPtr.Zero) throw new InvalidOperationException("VirtualAllocEx falhou");
            try
            {
                if (!WriteProcessMemory(hProc, mem, pathBytes, (uint)pathBytes.Length, out _))
                    throw new InvalidOperationException("WriteProcessMemory falhou");

                IntPtr k32 = GetModuleHandle("kernel32.dll");
                IntPtr loadLib = GetProcAddress(k32, "LoadLibraryA");
                if (loadLib == IntPtr.Zero)
                    throw new InvalidOperationException("Nao achei LoadLibraryA");

                IntPtr thread = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLib, mem, 0, out _);
                if (thread == IntPtr.Zero)
                    throw new InvalidOperationException("CreateRemoteThread falhou");

                WaitForSingleObject(thread, 5000);
                CloseHandle(thread);
            }
            finally
            {
                VirtualFreeEx(hProc, mem, 0, MEM_RELEASE);
            }
        }

        private static string BuildEnvBlock(System.Collections.Generic.IDictionary<string, string> extra)
        {
            // Começa com env atual + extras + duplo NUL
            var env = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables())
                env[(string)de.Key] = (string?)de.Value ?? "";
            foreach (var kv in extra) env[kv.Key] = kv.Value;

            var sb = new StringBuilder();
            foreach (var kv in env)
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
            sb.Append('\0');
            return sb.ToString();
        }

        // ---- P/Invoke ----
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint MEM_COMMIT = 0x00001000;
        private const uint MEM_RESERVE = 0x00002000;
        private const uint MEM_RELEASE = 0x00008000;
        private const uint PAGE_READWRITE = 0x04;

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public uint cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
            public uint dwFillAttribute, dwFlags;
            public ushort wShowWindow, cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public uint dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
            byte[] lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess,
            IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress,
            IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    }
}
