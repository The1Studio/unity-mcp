using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Resources.MenuItems
{
    /// <summary>
    /// Provides a simple read-only resource that returns Unity menu items.
    /// </summary>
    [McpForUnityResource("get_menu_items")]
    public static class GetMenuItems
    {
        private static List<string> _cached;

        [InitializeOnLoadMethod]
        private static void BuildCache() => Refresh();

        /// <summary>
        /// Handles the <c>get_menu_items</c> command, returning the project's registered menu item
        /// paths. Accepts an optional <c>search</c> substring and a <c>refresh</c> flag that
        /// re-scans instead of using the cache built at load.
        /// </summary>
        /// <param name="params">Optional <c>refresh</c> and <c>search</c> arguments.</param>
        /// <returns>A success envelope with the (possibly filtered) menu item paths.</returns>
        public static object HandleCommand(JObject @params)
        {
            bool forceRefresh = @params?["refresh"]?.ToObject<bool>() ?? false;
            string search = @params?["search"]?.ToString();

            var items = GetMenuItemsInternal(forceRefresh);

            if (!string.IsNullOrEmpty(search))
            {
                items = items
                    .Where(item => item.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            string message = $"Retrieved {items.Count} menu items";
            return new SuccessResponse(message, items);
        }

        internal static List<string> GetMenuItemsInternal(bool forceRefresh)
        {
            if (forceRefresh || _cached == null)
            {
                Refresh();
            }

            return (_cached ?? new List<string>()).ToList();
        }

        private static void Refresh()
        {
            try
            {
                var methods = TypeCache.GetMethodsWithAttribute<MenuItem>();
                _cached = methods
                    .SelectMany(m => m
                        .GetCustomAttributes(typeof(MenuItem), false)
                        .OfType<MenuItem>()
                        .Select(attr => attr.menuItem))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception ex)
            {
                McpLog.Error($"[GetMenuItems] Failed to scan menu items: {ex}");
                _cached ??= new List<string>();
            }
        }
    }
}
