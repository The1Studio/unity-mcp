using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Clients
{
    /// <summary>
    /// Central registry that auto-discovers configurators via TypeCache.
    /// </summary>
    public static class McpClientRegistry
    {
        private static List<IMcpClientConfigurator> cached;

        /// <summary>All discovered configurators, built once from the <c>TypeCache</c> scan and cached for the session.</summary>
        /// <value>Read-only list of one configurator instance per discovered client type.</value>
        public static IReadOnlyList<IMcpClientConfigurator> All
        {
            get
            {
                if (cached == null)
                {
                    cached = BuildRegistry();
                }
                return cached;
            }
        }

        private static List<IMcpClientConfigurator> BuildRegistry()
        {
            var configurators = new List<IMcpClientConfigurator>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<IMcpClientConfigurator>())
            {
                if (type.IsAbstract || !type.IsClass || !type.IsPublic)
                    continue;

                // Require a public parameterless constructor
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                try
                {
                    if (Activator.CreateInstance(type) is IMcpClientConfigurator instance)
                    {
                        configurators.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"UnityMCP: Failed to instantiate configurator {type.Name}: {ex.Message}");
                }
            }

            // Alphabetical order by display name
            configurators = configurators.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            return configurators;
        }
    }
}
