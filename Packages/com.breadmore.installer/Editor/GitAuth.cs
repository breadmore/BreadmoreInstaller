using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Breadmore.Installer.Editor
{
    internal static class GitAuth
    {
        private const string SeededKey = "Breadmore.Installer.GitCredentialSeeded";

        public static bool CredentialSeeded
        {
            get => EditorPrefs.GetBool(SeededKey, false);
            private set => EditorPrefs.SetBool(SeededKey, value);
        }

        public static bool SeedCredential(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                CredentialSeeded = false;
                return false;
            }

            try
            {
                EnsureCredentialHelperConfigured();
                ForgetCredential();

                using var proc = StartGit("credential approve");
                if (proc == null)
                {
                    CredentialSeeded = false;
                    return false;
                }

                WriteCredential(proc, includePassword: true, token);
                proc.WaitForExit(5000);

                bool ok = proc.HasExited && proc.ExitCode == 0 && CanFillCredential();
                CredentialSeeded = ok;
                if (!ok)
                    Debug.LogWarning("[Breadmore Installer] Token was saved, but git credential lookup failed. Configure a git credential helper, then save again.");
                return ok;
            }
            catch (Exception ex)
            {
                CredentialSeeded = false;
                Debug.LogWarning($"[Breadmore Installer] Could not seed git credential ({Sanitize(ex.Message)}).");
                return false;
            }
        }

        private static void EnsureCredentialHelperConfigured()
        {
            if (HasCredentialHelper()) return;
            if (Application.platform == RuntimePlatform.OSXEditor)
                RunGit("config --global credential.helper osxkeychain", 5000, out _, out _);
        }

        private static bool HasCredentialHelper()
        {
            int exit = RunGit("config --global --get credential.helper", 5000, out string stdout, out _);
            return exit == 0 && !string.IsNullOrWhiteSpace(stdout);
        }

        private static void ForgetCredential()
        {
            using var proc = StartGit("credential reject");
            if (proc == null) return;

            WriteCredential(proc, includePassword: false, token: null);
            proc.WaitForExit(5000);
        }

        private static bool CanFillCredential()
        {
            using var proc = StartGit("credential fill");
            if (proc == null) return false;

            WriteCredential(proc, includePassword: false, token: null);
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            return proc.HasExited && proc.ExitCode == 0 && output.Contains("username=") && output.Contains("password=");
        }

        private static Process StartGit(string arguments)
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            return Process.Start(psi);
        }

        private static int RunGit(string arguments, int timeoutMs, out string stdout, out string stderr)
        {
            stdout = string.Empty;
            stderr = string.Empty;

            using var proc = StartGit(arguments);
            if (proc == null) return -1;

            proc.StandardInput.Close();
            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(timeoutMs);
            return proc.HasExited ? proc.ExitCode : -1;
        }

        private static void WriteCredential(Process proc, bool includePassword, string token)
        {
            proc.StandardInput.NewLine = "\n";
            proc.StandardInput.WriteLine("protocol=https");
            proc.StandardInput.WriteLine("host=github.com");
            if (includePassword)
            {
                proc.StandardInput.WriteLine("username=x-access-token");
                proc.StandardInput.WriteLine("password=" + token);
            }
            proc.StandardInput.WriteLine();
            proc.StandardInput.Close();
        }

        public static void ClearSeeded() => EditorPrefs.DeleteKey(SeededKey);

        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return GitHubTokenStore.HasToken ? text.Replace(GitHubTokenStore.Token, "***") : text;
        }
    }
}
