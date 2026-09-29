using System;
using UnityEditor;
using UnityEditor.Build;

namespace MCPForUnity.Editor.Tools.Build
{
    /// <summary>
    /// Translates the MCP client's platform names (e.g. <c>windows64</c>, <c>macos</c>, <c>ios</c>)
    /// into Unity <see cref="BuildTarget"/> values, and resolves the matching build-target group,
    /// named target, default output path, and standalone subtarget for a build request.
    /// </summary>
    public static class BuildTargetMapping
    {
        private const string VisionOSName = "VisionOS";

        /// <summary>
        /// Resolves a client-supplied platform name to a <see cref="BuildTarget"/>. Falls back to
        /// parsing any build target the installed editor defines (so newly added platforms work
        /// without a code change).
        /// </summary>
        /// <param name="name">Platform name; null or empty selects the editor's active build target.</param>
/// <param name="target">Resolved build target, or <c>default</c> when resolution fails.</param>
        /// <returns>True when <paramref name="target"/> was resolved; false for an unknown name.</returns>
        public static bool TryResolveBuildTarget(string name, out BuildTarget target)
        {
            if (string.IsNullOrEmpty(name))
            {
                target = EditorUserBuildSettings.activeBuildTarget;
                return true;
            }

            switch (name.ToLowerInvariant())
            {
                case "windows64": target = BuildTarget.StandaloneWindows64; return true;
                case "windows": case "windows32": target = BuildTarget.StandaloneWindows; return true;
                case "osx": case "macos": target = BuildTarget.StandaloneOSX; return true;
                case "linux64": case "linux": target = BuildTarget.StandaloneLinux64; return true;
                case "android": target = BuildTarget.Android; return true;
                case "ios": target = BuildTarget.iOS; return true;
                case "webgl": target = BuildTarget.WebGL; return true;
                case "uwp": target = BuildTarget.WSAPlayer; return true;
                case "tvos": target = BuildTarget.tvOS; return true;
                default:
                    if (TryParseDefinedBuildTarget(name, out target))
                    {
                        return true;
                    }

                    target = default;
                    return false;
            }
        }

        /// <summary>
        /// Maps a build target to its <see cref="BuildTargetGroup"/>. VisionOS is resolved by name
        /// when the installed editor exposes the group; otherwise the result is
        /// <see cref="BuildTargetGroup.Unknown"/>.
        /// </summary>
        /// <param name="target">The build target to map.</param>
        /// <returns>The matching build target group, or <see cref="BuildTargetGroup.Unknown"/>.</returns>
        public static BuildTargetGroup GetTargetGroup(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    return BuildTargetGroup.Standalone;
                case BuildTarget.iOS: return BuildTargetGroup.iOS;
                case BuildTarget.Android: return BuildTargetGroup.Android;
                case BuildTarget.WebGL: return BuildTargetGroup.WebGL;
                case BuildTarget.WSAPlayer: return BuildTargetGroup.WSA;
                case BuildTarget.tvOS: return BuildTargetGroup.tvOS;
                default:
                    if (IsVisionOSTarget(target)
                        && Enum.TryParse(VisionOSName, true, out BuildTargetGroup visionOSGroup))
                    {
                        return visionOSGroup;
                    }

                    return BuildTargetGroup.Unknown;
            }
        }

        /// <summary>
        /// Returns the <see cref="NamedBuildTarget"/> used by the PlayerSettings APIs for the given
        /// build target.
        /// </summary>
        /// <param name="target">The build target to map.</param>
        /// <returns>The named build target derived from the target's build target group.</returns>
        public static NamedBuildTarget GetNamedBuildTarget(BuildTarget target)
        {
            return NamedBuildTarget.FromBuildTargetGroup(GetTargetGroup(target));
        }

        /// <summary>
        /// Resolves a client-supplied platform name all the way to a <see cref="NamedBuildTarget"/>,
        /// reporting why resolution failed when it does.
        /// </summary>
        /// <param name="name">Platform name as sent by the client.</param>
        /// <param name="namedTarget">Resolved named target, or <c>default</c> on failure.</param>
        /// <returns>Null on success; otherwise a human-readable explanation of the failure.</returns>
        public static string TryResolveNamedBuildTarget(string name, out NamedBuildTarget namedTarget)
        {
            if (!TryResolveBuildTarget(name, out var buildTarget))
            {
                namedTarget = default;
                return GetUnknownBuildTargetMessage(name);
            }

            var targetGroup = GetTargetGroup(buildTarget);
            if (targetGroup == BuildTargetGroup.Unknown)
            {
                namedTarget = default;
                return IsVisionOSTarget(buildTarget)
                    ? "VisionOS build target is available, but its BuildTargetGroup is not exposed by this Unity editor installation."
                    : $"Build target group could not be resolved for target '{buildTarget}'.";
            }

            namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            return null;
        }

        /// <summary>
        /// Builds the error text returned when a platform name cannot be resolved, listing the valid
        /// targets and calling out the missing visionOS support module as its own case.
        /// </summary>
        /// <param name="name">The unrecognized platform name.</param>
        /// <returns>A message naming the valid targets, or the visionOS-specific guidance.</returns>
        public static string GetUnknownBuildTargetMessage(string name)
        {
            if (string.Equals(name, "visionos", StringComparison.OrdinalIgnoreCase))
            {
                return "VisionOS build target is not available in this Unity editor installation. "
                    + "Install the visionOS build support module or use a Unity version/configuration that exposes BuildTarget.VisionOS.";
            }

            return $"Unknown build target: '{name}'. Valid targets: {GetValidTargetsList()}.";
        }

        private static string GetValidTargetsList()
        {
            string validTargets = "windows64, osx, linux64, android, ios, webgl, uwp, tvos";
            if (TryParseDefinedBuildTarget(VisionOSName, out _))
            {
                validTargets += ", visionos";
            }

            return validTargets;
        }

        private static bool IsVisionOSTarget(BuildTarget target)
        {
            return string.Equals(target.ToString(), VisionOSName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseDefinedBuildTarget(string name, out BuildTarget target)
        {
            target = default;
            if (int.TryParse(name, out _))
            {
                return false;
            }

            return Enum.TryParse(name, true, out target)
                && Enum.IsDefined(typeof(BuildTarget), target);
        }

        /// <summary>
        /// Produces the conventional output path for a build when the caller does not supply one:
        /// <c>Builds/&lt;target&gt;/&lt;productName&gt;</c> plus the per-platform file extension
        /// (<c>.exe</c>, <c>.app</c>, <c>.x86_64</c>, and <c>.apk</c>/<c>.aab</c> for Android).
        /// </summary>
        /// <param name="target">Platform being built.</param>
        /// <param name="productName">Player product name, used as the file stem.</param>
        /// <returns>The default output path for the target.</returns>
        public static string GetDefaultOutputPath(BuildTarget target, string productName)
        {
            string basePath = $"Builds/{target}";
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return $"{basePath}/{productName}.exe";
                case BuildTarget.StandaloneOSX:
                    return $"{basePath}/{productName}.app";
                case BuildTarget.StandaloneLinux64:
                    return $"{basePath}/{productName}.x86_64";
                case BuildTarget.Android:
                    return EditorUserBuildSettings.buildAppBundle
                        ? $"{basePath}/{productName}.aab"
                        : $"{basePath}/{productName}.apk";
                case BuildTarget.iOS:
                case BuildTarget.WebGL:
                    return $"{basePath}/{productName}";
                default:
                    return $"{basePath}/{productName}";
            }
        }

        /// <summary>
        /// Converts the client's subtarget string into a <see cref="StandaloneBuildSubtarget"/>
        /// ordinal; anything other than <c>server</c> resolves to a player build.
        /// </summary>
        /// <param name="subtarget">Client-supplied subtarget name, typically <c>player</c> or <c>server</c>.</param>
        /// <returns>The subtarget as an integer, ready for <c>BuildPlayerOptions.subtarget</c>.</returns>
        public static int ResolveSubtarget(string subtarget)
        {
            if (string.IsNullOrEmpty(subtarget))
                return (int)StandaloneBuildSubtarget.Player;
            string lower = subtarget.ToLowerInvariant();
            if (lower == "server")
                return (int)StandaloneBuildSubtarget.Server;
            return (int)StandaloneBuildSubtarget.Player;
        }
    }
}
