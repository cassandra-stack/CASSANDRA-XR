// Assets/Editor/ZipAlignPostBuild.cs
#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ZipAlignPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 999;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        var apkPath = report.summary.outputPath;
        if (!apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            UnityEngine.Debug.Log("[ZipAlign] Sortie non-APK, rien à faire.");
            return;
        }

        try
        {
            string sdkRoot = ResolveAndroidSdkRoot();
            string buildTools = ResolveLatestBuildToolsDir(sdkRoot);
            string zipalign = Path.Combine(buildTools, GetExe("zipalign"));
            string apksigner = Path.Combine(buildTools, GetExe("apksigner"));

            if (!File.Exists(zipalign))
                throw new FileNotFoundException("zipalign introuvable", zipalign);
            if (!File.Exists(apksigner))
                throw new FileNotFoundException("apksigner introuvable", apksigner);

            string alignedPath = Path.Combine(Path.GetDirectoryName(apkPath)!,
                Path.GetFileNameWithoutExtension(apkPath) + "_aligned.apk");

            // 1) zipalign 16KB
            Run(buildTools, $"{Quote(zipalign)} -p 16384 {Quote(apkPath)} {Quote(alignedPath)}");

            // 2) re-signer l'APK aligné (utilise les infos PlayerSettings Android)
            SignWithKeystore(apksigner, alignedPath);

            // 3) remplace l'APK original par l'aligné+signé
            File.Delete(apkPath);
            File.Move(alignedPath, apkPath);

            UnityEngine.Debug.Log($"[ZipAlign] APK aligné (16KB) et re-signé : {apkPath}");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ZipAlign] Échec facultatif : {ex.Message}\n{ex}");
        }
    }

    // -- Helpers -------------------------------------------------------------

    private static string ResolveAndroidSdkRoot()
    {
        // 1) Préférence Unity
        var sdk = EditorPrefs.GetString("AndroidSdkRoot");
        if (!string.IsNullOrEmpty(sdk) && Directory.Exists(sdk)) return sdk;

        // 2) Variables d’environnement standard
        foreach (var v in new[] { "ANDROID_SDK_ROOT", "ANDROID_HOME" })
        {
            var env = Environment.GetEnvironmentVariable(v);
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
        }

        throw new DirectoryNotFoundException(
            "Chemin du SDK Android introuvable. Vérifie Preferences → External Tools → Android SDK.");
    }

    private static string ResolveLatestBuildToolsDir(string sdkRoot)
    {
        var dir = Path.Combine(sdkRoot, "build-tools");
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Dossier build-tools introuvable dans : {dir}");

        // prend la version la plus récente
        var candidates = Directory.GetDirectories(dir)
            .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var c in candidates)
        {
            if (File.Exists(Path.Combine(c, GetExe("zipalign"))) &&
                File.Exists(Path.Combine(c, GetExe("apksigner"))))
                return c;
        }
        throw new FileNotFoundException("Aucun build-tools ne contient zipalign + apksigner.");
    }

    private static void SignWithKeystore(string apksigner, string apkPath)
    {
        // Récupère les infos de PlayerSettings
        var ksPath = PlayerSettings.Android.keystoreName;
        var ksPass = PlayerSettings.Android.keystorePass;
        var alias = PlayerSettings.Android.keyaliasName;
        var aliasPass = PlayerSettings.Android.keyaliasPass;

        // Si pas de keystore custom → Unity a signé en debug; on peut re-signer avec le même keystore debug.
        if (string.IsNullOrEmpty(ksPath) || !File.Exists(ksPath))
        {
            // Keystore debug par défaut d’Android (généralement dans ~/.android/debug.keystore)
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var debugKs = Path.Combine(user, ".android", "debug.keystore");
            if (!File.Exists(debugKs))
                throw new FileNotFoundException("Keystore introuvable (custom ou debug).", debugKs);

            ksPath = debugKs;
            ksPass = string.IsNullOrEmpty(ksPass) ? "android" : ksPass; // valeurs par défaut debug
            alias = string.IsNullOrEmpty(alias) ? "androiddebugkey" : alias;
            aliasPass = string.IsNullOrEmpty(aliasPass) ? "android" : aliasPass;
        }

        // apksigner sign --ks <file> --ks-pass pass:xxx --key-pass pass:yyy --ks-key-alias <alias> <apk>
        string args =
            $"{Quote(apksigner)} sign --ks {Quote(ksPath)} --ks-pass pass:{Escape(ksPass)} " +
            $"--key-pass pass:{Escape(aliasPass)} --ks-key-alias {Quote(alias)} {Quote(apkPath)}";

        Run(Path.GetDirectoryName(apksigner)!, args);
    }

    private static void Run(string workingDir, string cmd)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetShell(),
            Arguments = GetShellArgs(cmd),
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var p = Process.Start(psi);
        p.OutputDataReceived += (_, e) => { if (e.Data != null) UnityEngine.Debug.Log("[ZipAlign] " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) UnityEngine.Debug.LogWarning("[ZipAlign] " + e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new Exception($"Commande a échoué avec le code {p.ExitCode} : {cmd}");
    }

    private static string GetExe(string name)
    {
#if UNITY_EDITOR_WIN
        return name + ".bat"; // apksigner.bat / zipalign.exe (selon versions)
#else
        return name;          // macOS/Linux
#endif
    }

    private static string GetShell()
    {
#if UNITY_EDITOR_WIN
        return "cmd.exe";
#else
        return "/bin/bash";
#endif
    }

    private static string GetShellArgs(string cmd)
    {
#if UNITY_EDITOR_WIN
        return "/C " + cmd;
#else
        return "-lc " + Quote(cmd);
#endif
    }

    private static string Quote(string s) => $"\"{s}\"";
    private static string Escape(string s) => s.Replace("\"", "\\\"");
}
#endif
