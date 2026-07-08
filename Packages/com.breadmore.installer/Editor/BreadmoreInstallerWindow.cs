using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Breadmore.Installer.Editor
{
    public sealed class BreadmoreInstallerWindow : EditorWindow
    {
        private const string PrivateCoreUrl = "https://github.com/breadmore/BreadmoreCore.git?path=Packages/com.breadmore.core";

        private string _draft;
        private bool _reveal;
        private string _status;
        private AddRequest _request;

        [MenuItem("Breadmore/Installer", priority = -100)]
        [MenuItem("Window/Breadmore/GitHub Token...", priority = 100)]
        public static void Open()
        {
            var window = GetWindow<BreadmoreInstallerWindow>(true, "Breadmore Installer");
            window.minSize = new Vector2(480, 250);
            window._draft = GitHubTokenStore.Token;
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Breadmore Installer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Install the private Breadmore Core package. Save a GitHub PAT with read access to breadmore/BreadmoreCore first; the token is stored locally in EditorPrefs and registered with your OS git credential helper.",
                MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Token", GUILayout.Width(56));
                _draft = _reveal
                    ? EditorGUILayout.TextField(_draft ?? string.Empty)
                    : EditorGUILayout.PasswordField(_draft ?? string.Empty);
                _reveal = GUILayout.Toggle(_reveal, "Show", "Button", GUILayout.Width(60));
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_draft)))
                {
                    if (GUILayout.Button("Save Token"))
                    {
                        string token = _draft.Trim();
                        GitHubTokenStore.SetToken(token);
                        bool seeded = GitAuth.SeedCredential(token);
                        _status = seeded
                            ? "Token saved and git credential verified."
                            : "Token saved, but git credential lookup failed.";
                    }
                }

                using (new EditorGUI.DisabledScope(!GitHubTokenStore.HasToken))
                {
                    if (GUILayout.Button("Clear"))
                    {
                        _draft = string.Empty;
                        GitHubTokenStore.ClearToken();
                        GitAuth.ClearSeeded();
                        _status = "Token cleared.";
                    }
                }

                if (GUILayout.Button("Open PAT Page"))
                    Application.OpenURL("https://github.com/settings/personal-access-tokens/new");
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_request != null && !_request.IsCompleted))
            {
                if (GUILayout.Button("Install Breadmore Core", GUILayout.Height(32)))
                {
                    InstallCore();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.SelectableLabel(PrivateCoreUrl, EditorStyles.miniLabel, GUILayout.Height(18));

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void Update()
        {
            if (_request == null || !_request.IsCompleted) return;

            if (_request.Status == StatusCode.Success || IsCoreInstalledInManifest())
                _status = "Breadmore Core installed. You can now use the full Breadmore menu.";
            else
                _status = "Could not confirm Breadmore Core installation: " + GitAuth.Sanitize(_request.Error?.message);

            _request = null;
            Repaint();
        }

        private static bool IsCoreInstalledInManifest()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifestPath)) return false;

            string manifest = File.ReadAllText(manifestPath);
            return manifest.Contains("\"com.breadmore.core\"");
        }

        private void InstallCore()
        {
            if (!GitHubTokenStore.HasToken)
            {
                _status = "Save a GitHub PAT before installing private Breadmore Core.";
                return;
            }

            GitAuth.SeedCredential(GitHubTokenStore.Token);
            _request = Client.Add(PrivateCoreUrl);
            _status = "Installing Breadmore Core...";
        }
    }
}
