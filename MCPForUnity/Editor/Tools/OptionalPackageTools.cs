using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Tools whose Unity-side handler is compiled out unless an optional package is installed.
    /// Each entry mirrors a <c>#if</c> gate on a tool file and the matching versionDefine in
    /// MCPForUnity.Editor.asmdef. The Python server advertises these tools unconditionally, so
    /// without a placeholder the dispatcher answers a bare "Unknown or unsupported command type",
    /// which tells the caller nothing about which package to install.
    /// </summary>
    internal static class OptionalPackageTools
    {
        /// <summary>Command name → the package whose presence defines its compile gate.</summary>
        internal static readonly IReadOnlyDictionary<string, string> CommandToPackage =
            new Dictionary<string, string>
            {
                ["manage_addressables"] = "com.unity.addressables",
                ["manage_behavior"] = "com.unity.behavior",
                ["manage_cinemachine"] = "com.unity.cinemachine",
                ["manage_dots"] = "com.unity.entities",
                ["manage_dots_graphics"] = "com.unity.entities.graphics",
                ["manage_dots_physics"] = "com.unity.physics",
                ["manage_dots_subscene"] = "com.unity.entities",
                ["manage_input_system"] = "com.unity.inputsystem",
                ["manage_localization"] = "com.unity.localization",
                ["manage_navigation"] = "com.unity.ai.navigation",
                ["manage_netcode"] = "com.unity.netcode.gameobjects",
                ["manage_splines"] = "com.unity.splines",
                ["manage_tilemap"] = "com.unity.2d.tilemap",
                ["manage_timeline"] = "com.unity.timeline",
                ["manage_vfx"] = "com.unity.visualeffectgraph",
                ["validation_snapshot"] = "com.unity.entities",
            };

        internal static object MissingPackageResponse(string commandName, string packageName)
        {
            return new ErrorResponse(
                $"'{commandName}' requires the {packageName} package, which is not installed in this project. "
                + $"Install it from Window > Package Manager (or add \"{packageName}\" to Packages/manifest.json), "
                + "wait for Unity to recompile, then retry.",
                new { command = commandName, required_package = packageName });
        }
    }
}
