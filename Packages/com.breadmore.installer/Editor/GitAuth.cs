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
        private const string CoreCredentialPath = "breadmore/BreadmoreCore.git";

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

                bool hostOk = ApproveCredential(token, credentialPath: null)
                              && CanFillCredential(token, credentialPath: null);
                bool corePathOk = ApproveCredential(token, CoreCredentialPath)
                                  && CanFillCredential(token, CoreCredentialPath);
                bool ok = hostOk && corePathOk;
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
            // osxkeychain may keep an older GitHub Desktop or PAT credential under a different
            // username. Ask git what it would currently use, then reject that exact account.
            ForgetCredential(credentialPath: null);
            ForgetCredential(CoreCredentialPath);
        }

        private static void ForgetCredential(string credentialPath)
        {
            string existing = FillCredentialOutput(credentialPath);
            string existingUsername = ReadCredentialValue(existing, "username");
            if (!string.IsNullOrEmpty(existingUsername))
                RejectCredential(existingUsername, credentialPath);

            RejectCredential("x-access-token", credentialPath);
            RejectCredential(null, credentialPath);
        }

        private static bool ApproveCredential(string token, string credentialPath)
        {
            using var proc = StartGit("credential approve");
            if (proc == null) return false;

            WriteCredential(proc, includePassword: true, token, credentialPath: credentialPath);
            proc.WaitForExit(5000);
            return proc.HasExited && proc.ExitCode == 0;
        }

        private static void RejectCredential(string username, string credentialPath)
        {
            using var proc = StartGit("credential reject");
            if (proc == null) return;

            WriteCredential(proc, includePassword: false, token: null, username: username, credentialPath: credentialPath);
            proc.WaitForExit(5000);
        }

        private static bool CanFillCredential(string expectedToken, string credentialPath)
        {
            string output = FillCredentialOutput(credentialPath);
            string username = ReadCredentialValue(output, "username");
            string password = ReadCredentialValue(output, "password");

            return username == "x-access-token" && password == expectedToken;
        }

        private static string FillCredentialOutput(string credentialPath)
        {
            using var proc = StartGit("credential fill");
            if (proc == null) return string.Empty;

            WriteCredential(proc, includePassword: false, token: null, credentialPath: credentialPath);
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return proc.HasExited && proc.ExitCode == 0 ? output : string.Empty;
        }

        private static string ReadCredentialValue(string credentialOutput, string key)
        {
            if (string.IsNullOrEmpty(credentialOutput) || string.IsNullOrEmpty(key)) return null;

            string prefix = key + "=";
            string[] lines = credentialOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length);
            }
            return null;
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

        private static void WriteCredential(Process proc, bool includePassword, string token, string username = null, string credentialPath = null)
        {
            proc.StandardInput.NewLine = "\n";
            proc.StandardInput.WriteLine("protocol=https");
            proc.StandardInput.WriteLine("host=github.com");
            if (!string.IsNullOrEmpty(credentialPath))
                proc.StandardInput.WriteLine("path=" + credentialPath);
            if (!string.IsNullOrEmpty(username))
                proc.StandardInput.WriteLine("username=" + username);
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
