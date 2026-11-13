using System;
using System.IO;
using Muhanok.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    // One place for the face server address used by the editor and by every Windows build.
    // The address is kept in Unity/identity-server.json (ignored by Git) and the build copies it next to Mongscape.exe,
    // so neither the editor nor the built game needs the MONGSCAPE_IDENTITY_URL environment variable.
    // Priority while building: -identityUrl <url> on the command line → MONGSCAPE_IDENTITY_URL → Unity/identity-server.json.
    public static class IdentityServerSetup
    {
        public const string FileName = "identity-server.json", CommandLineArgument = "-identityUrl";
        [Serializable] private sealed class Settings { public string url; }
        public static string ProjectConfigPath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", FileName));

        // The address a build will carry and where it came from; null when nothing is configured.
        // A configured but invalid address throws, so a build never ships a broken setting.
        public static string Resolve(out string source)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], CommandLineArgument, StringComparison.OrdinalIgnoreCase)) { source = "command line " + CommandLineArgument; return Check(args[i + 1], source); }
            string variable = Environment.GetEnvironmentVariable("MONGSCAPE_IDENTITY_URL");
            if (!string.IsNullOrWhiteSpace(variable)) { source = "MONGSCAPE_IDENTITY_URL"; return Check(variable, source); }
            source = ProjectConfigPath;
            string saved = ReadUrl(source);
            if (saved != null) return Check(saved, source);
            source = null; return null;
        }
        private static string Check(string url, string source)
        {
            try { return LocalProfileClient.IdentityServerConfig.Normalize(url); }
            catch (FormatException) { throw new Exception("Identity server address from " + source + " is invalid: use an HTTPS origin, or loopback HTTP for local Docker (" + url + ")"); }
        }
        public static string ReadUrl(string path)
        {
            if (!File.Exists(path)) return null;
            var settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
            if (settings == null || string.IsNullOrWhiteSpace(settings.url)) throw new Exception("Missing url in " + path);
            return settings.url.Trim();
        }
        public static void Write(string path, string url) => File.WriteAllText(path, JsonUtility.ToJson(new Settings { url = url }, true));

        // Called after a successful build: the game folder gets the resolved address, or keeps the one already there.
        public static void ApplyToBuild(string gameDirectory)
        {
            string target = Path.Combine(gameDirectory, FileName);
            string url = Resolve(out string source);
            if (url != null) { Write(target, url); Debug.Log("MONGSCAPE_IDENTITY_CONFIG: " + url + " from " + source + " → " + target); }
            else if (File.Exists(target)) Debug.Log("MONGSCAPE_IDENTITY_CONFIG: kept " + target + " (" + ReadUrl(target) + ")");
            else Debug.LogWarning("MONGSCAPE_IDENTITY_CONFIG: no face server address configured; the build uses the loopback default. Set it with Mongscape → Identity Server Address.");
        }
    }

    public sealed class IdentityServerWindow : EditorWindow
    {
        private string url = "", status = "";
        [MenuItem("Mongscape/Identity Server Address…")]
        public static void Open()
        {
            var window = GetWindow<IdentityServerWindow>(true, "Face server address");
            window.minSize = new Vector2(460, 210); window.Load();
        }
        private void Load()
        {
            try { url = IdentityServerSetup.ReadUrl(IdentityServerSetup.ProjectConfigPath) ?? ""; status = ""; }
            catch (Exception error) { status = error.Message; }
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Used in Play mode and copied next to Mongscape.exe on every build.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            url = EditorGUILayout.TextField("Server URL", url);
            EditorGUILayout.LabelField("예: https://faces.your-domain.com · 로컬 Docker는 http://127.0.0.1:8765", EditorStyles.miniLabel);
            string variable = Environment.GetEnvironmentVariable("MONGSCAPE_IDENTITY_URL");
            if (!string.IsNullOrWhiteSpace(variable))
                EditorGUILayout.HelpBox("MONGSCAPE_IDENTITY_URL (" + variable + ") is set for this editor and takes priority over this file.", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) Save();
            if (GUILayout.Button("Clear (loopback)")) Clear();
            EditorGUILayout.EndHorizontal();
            if (status.Length > 0) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.LabelField(IdentityServerSetup.ProjectConfigPath, EditorStyles.miniLabel);
        }
        private void Save()
        {
            try
            {
                string normalized = LocalProfileClient.IdentityServerConfig.Normalize(url);
                IdentityServerSetup.Write(IdentityServerSetup.ProjectConfigPath, normalized); url = normalized;
                // An existing build picks up the new address too, without rebuilding.
                string build = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../Builds/Mongscape/Windows"));
                bool copied = File.Exists(Path.Combine(build, "Mongscape.exe"));
                if (copied) IdentityServerSetup.Write(Path.Combine(build, IdentityServerSetup.FileName), normalized);
                status = "Saved " + normalized + (copied ? " (also the current build)" : "") + ". Restart Play mode to reconnect.";
            }
            catch (FormatException) { status = "Use an HTTPS origin, or loopback HTTP for local Docker."; }
        }
        private void Clear()
        {
            if (File.Exists(IdentityServerSetup.ProjectConfigPath)) File.Delete(IdentityServerSetup.ProjectConfigPath);
            url = ""; status = "Cleared: Play mode uses http://127.0.0.1:8765. Existing builds keep their own file.";
        }
    }
}
