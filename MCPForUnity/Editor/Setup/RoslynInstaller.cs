using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MCPForUnity.Editor.Setup
{
    /// <summary>
    /// Downloads the Roslyn (Microsoft.CodeAnalysis) assemblies and their transitive NuGet
    /// dependencies into <c>Assets/Plugins/Roslyn/</c>, which is what makes the
    /// <c>runtime_compilation</c> MCP tool available. No package dependency: the .nupkg files are
    /// fetched straight from nuget.org and the target DLL is unzipped out of each one.
    /// </summary>
    public static class RoslynInstaller
    {
        private const string PluginsRelPath = "Plugins/Roslyn";

        private static readonly (string packageId, string version, string dllPath, string dllName)[] NuGetEntries =
        {
            ("microsoft.codeanalysis.common",         "4.12.0", "lib/netstandard2.0/Microsoft.CodeAnalysis.dll",                   "Microsoft.CodeAnalysis.dll"),
            ("microsoft.codeanalysis.csharp",         "4.12.0", "lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll",            "Microsoft.CodeAnalysis.CSharp.dll"),
            ("system.collections.immutable",          "8.0.0",  "lib/netstandard2.0/System.Collections.Immutable.dll",             "System.Collections.Immutable.dll"),
            ("system.reflection.metadata",            "8.0.0",  "lib/netstandard2.0/System.Reflection.Metadata.dll",               "System.Reflection.Metadata.dll"),
            // Transitive dep of Microsoft.CodeAnalysis.* on netstandard2.0. Without it, Roslyn's StringTable
            // static cctor throws FileNotFoundException for v6.0.0.0 and every Roslyn entry point fails to
            // initialize. Unity ships a v4.x of this assembly which does NOT satisfy the v6 reference.
            ("system.runtime.compilerservices.unsafe","6.0.0",  "lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll",   "System.Runtime.CompilerServices.Unsafe.dll"),
        };

        /// <summary>
        /// Reports whether every entry in <see cref="NuGetEntries"/> is present in
        /// <c>Assets/Plugins/Roslyn/</c> at an assembly version new enough for the Roslyn build
        /// that references it.
        /// </summary>
        /// <returns>
        /// <c>true</c> only when all DLLs exist and each on-disk assembly version is at least its
        /// declared NuGet version. A stale DLL that still satisfies file-existence but shadows the
        /// required version (Unity's own <c>System.Runtime.CompilerServices.Unsafe</c> v4.x against
        /// the v6 reference) reports <c>false</c>, so <see cref="Install"/> rewrites it.
        /// </returns>
        public static bool IsInstalled()
        {
            string folder = Path.Combine(Application.dataPath, PluginsRelPath);
            foreach (var entry in NuGetEntries)
            {
                string path = Path.Combine(folder, entry.dllName);
                if (!File.Exists(path))
                    return false;

                // Defense-in-depth: a stale DLL whose assembly version is older than what
                // Roslyn references (e.g. a v4.x System.Runtime.CompilerServices.Unsafe
                // shadowing the v6 we actually need) would still satisfy file-existence but
                // leave Roslyn unable to load. Compare the on-disk assembly version against
                // each entry's declared NuGet version, treating "older or unreadable" as not
                // installed so Install() can rewrite it.
                if (Version.TryParse(entry.version, out var requiredVersion))
                {
                    try
                    {
                        var actual = AssemblyName.GetAssemblyName(path).Version;
                        if (actual == null || actual < requiredVersion)
                            return false;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Fetches and writes every Roslyn DLL, then refreshes the asset database so the
        /// assemblies are imported before the caller asks for <c>runtime_compilation</c>.
        /// </summary>
        /// <param name="interactive">
        /// <c>true</c> for the menu-driven path: prompts before overwriting an existing install,
        /// shows a progress bar and a result dialog. <c>false</c> for silent/CI installs, where the
        /// log line is the only feedback.
        /// </param>
        /// <remarks>
        /// Failures are logged and surfaced in a dialog rather than thrown — a partial install is
        /// recoverable by re-running, and the caller (a menu item) has nowhere to propagate to.
        /// </remarks>
        public static void Install(bool interactive = true)
        {
            if (IsInstalled() && interactive)
            {
                if (!EditorUtility.DisplayDialog(
                        "Roslyn Already Installed",
                        $"Roslyn DLLs are already present in Assets/{PluginsRelPath}.\nReinstall?",
                        "Reinstall", "Cancel"))
                    return;
            }

            string destFolder = Path.Combine(Application.dataPath, PluginsRelPath);

            try
            {
                Directory.CreateDirectory(destFolder);

                for (int i = 0; i < NuGetEntries.Length; i++)
                {
                    var (packageId, pkgVersion, dllPathInZip, dllName) = NuGetEntries[i];

                    if (interactive)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Installing Roslyn",
                            $"Downloading {packageId} v{pkgVersion}...",
                            (float)i / NuGetEntries.Length);
                    }

                    string url =
                        $"https://api.nuget.org/v3-flatcontainer/{packageId}/{pkgVersion}/{packageId}.{pkgVersion}.nupkg";

                    using (var request = UnityWebRequest.Get(url))
                    {
                        request.timeout = 30;
                        request.SendWebRequest();
                        while (!request.isDone)
                            System.Threading.Thread.Sleep(50);

                        if (request.result != UnityWebRequest.Result.Success)
                            throw new Exception($"Failed to download {packageId}: {request.error}");

                        byte[] nupkgBytes = request.downloadHandler.data;
                        byte[] dllBytes = ExtractFileFromZip(nupkgBytes, dllPathInZip);

                        if (dllBytes == null)
                        {
                            Debug.LogError($"[MCP] Could not find {dllPathInZip} in {packageId}.{pkgVersion}.nupkg");
                            continue;
                        }

                        string destPath = Path.Combine(destFolder, dllName);
                        File.WriteAllBytes(destPath, dllBytes);
                        Debug.Log($"[MCP] Extracted {dllName} ({dllBytes.Length / 1024}KB) → Assets/{PluginsRelPath}/{dllName}");
                    }
                }

                if (interactive)
                    EditorUtility.DisplayProgressBar("Installing Roslyn", "Refreshing assets...", 0.95f);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (interactive)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog(
                        "Roslyn Installed",
                        $"Roslyn DLLs and dependencies installed to Assets/{PluginsRelPath}/.\n\n" +
                        "The runtime_compilation tool is now available via MCP.",
                        "OK");
                }

                Debug.Log($"[MCP] Roslyn installation complete ({NuGetEntries.Length} DLLs). runtime_compilation is now available.");
            }
            catch (Exception e)
            {
                if (interactive) EditorUtility.ClearProgressBar();
                Debug.LogError($"[MCP] Failed to install Roslyn: {e}");

                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "Installation Failed",
                        $"Could not download Roslyn DLLs:\n{e.Message}\n\n" +
                        "You can manually download Microsoft.CodeAnalysis.CSharp from NuGet " +
                        "and place the DLLs in Assets/Plugins/Roslyn/.",
                        "OK");
                }
            }
        }

        private static byte[] ExtractFileFromZip(byte[] zipBytes, string entryPath)
        {
            entryPath = entryPath.Replace('\\', '/');

            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Replace('\\', '/').Equals(entryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        using (var entryStream = entry.Open())
                        using (var output = new MemoryStream())
                        {
                            entryStream.CopyTo(output);
                            return output.ToArray();
                        }
                    }
                }
            }

            return null;
        }
    }
}
