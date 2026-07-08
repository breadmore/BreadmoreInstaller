using UnityEditor;

namespace Breadmore.Installer.Editor
{
    internal static class GitHubTokenStore
    {
        private const string TokenKey = "Breadmore.Installer.GitHubToken";
        private const string CoreTokenKey = "Breadmore.Core.GitHubToken";

        public static bool HasToken => !string.IsNullOrEmpty(Token);

        public static string Token
        {
            get => EditorPrefs.GetString(TokenKey, string.Empty);
            private set => EditorPrefs.SetString(TokenKey, value ?? string.Empty);
        }

        public static void SetToken(string token)
        {
            Token = token?.Trim();
            EditorPrefs.SetString(CoreTokenKey, Token);
        }

        public static void ClearToken()
        {
            EditorPrefs.DeleteKey(TokenKey);
            EditorPrefs.DeleteKey(CoreTokenKey);
        }
    }
}
