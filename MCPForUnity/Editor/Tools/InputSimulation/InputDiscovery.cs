using System;
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    /// <summary>
    /// Discovery and bounds actions for input simulation.
    /// Finds interactable elements: UGUI Selectables, 3D Colliders, 2D Colliders.
    /// </summary>
    internal static class InputDiscovery
    {
        internal static object Discover(ToolParams p)
        {
            string filter = p.Get("filter");
            int pageSize = p.GetInt("page_size") ?? 50;
            int cursor = p.GetInt("cursor") ?? 0;
            var items = new List<Dictionary<string, object>>();

            CollectSelectables(items, filter);
            CollectColliders3D(items, filter);
            CollectColliders2D(items, filter);

            int total = items.Count;
            var page = items.GetRange(
                Math.Min(cursor, total),
                Math.Min(pageSize, Math.Max(0, total - cursor))
            );
            bool hasMore = cursor + pageSize < total;

            return new SuccessResponse($"Found {total} interactable elements.", new
            {
                items = page,
                cursor,
                page_size = pageSize,
                total_count = total,
                has_more = hasMore,
                next_cursor = hasMore ? (int?)(cursor + pageSize) : null
            });
        }

        internal static object GetElementBounds(ToolParams p)
        {
            var targetToken = p.GetRaw("target");
            if (targetToken == null || targetToken.Type != JTokenType.Object)
                return new ErrorResponse("'target' parameter is required as JSON object.");

            var (bounds, err) = InputTargetResolver.ResolveTargetBounds((JObject)targetToken);
            if (err != null) return new ErrorResponse(err);

            return new SuccessResponse("Element bounds resolved.", new
            {
                x = bounds.Value.x,
                y = bounds.Value.y,
                width = bounds.Value.width,
                height = bounds.Value.height,
                center_x = bounds.Value.center.x,
                center_y = bounds.Value.center.y
            });
        }

        // --- Collectors ---

        static void CollectSelectables(List<Dictionary<string, object>> items, string filter)
        {
            foreach (var selectable in Selectable.allSelectablesArray)
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(filter) && !selectable.name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                var rt = selectable.GetComponent<RectTransform>();
                Rect? screenRect = GetSelectableScreenRect(rt);

                items.Add(new Dictionary<string, object>
                {
                    ["name"] = selectable.name,
                    ["path"] = GetGameObjectPath(selectable.gameObject),
                    ["type"] = "ugui",
                    ["component"] = selectable.GetType().Name,
                    ["interactable"] = selectable.interactable,
                    ["screen_rect"] = RectToDict(screenRect)
                });
            }
        }

        static void CollectColliders3D(List<Dictionary<string, object>> items, string filter)
        {
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var col in colliders)
            {
                if (col == null || !col.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(filter) && !col.name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                Rect? screenRect = ProjectBoundsToScreen(col.bounds);
                items.Add(new Dictionary<string, object>
                {
                    ["name"] = col.name,
                    ["path"] = GetGameObjectPath(col.gameObject),
                    ["type"] = "gameobject",
                    ["component"] = col.GetType().Name,
                    ["interactable"] = col.enabled,
                    ["screen_rect"] = RectToDict(screenRect)
                });
            }
        }

        static void CollectColliders2D(List<Dictionary<string, object>> items, string filter)
        {
            var colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            foreach (var col in colliders)
            {
                if (col == null || !col.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(filter) && !col.name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                var cam = Camera.main;
                Rect? screenRect = null;
                if (cam != null)
                {
                    var bounds = col.bounds;
                    Vector3 sp = cam.WorldToScreenPoint(bounds.center);
                    if (sp.z > 0)
                    {
                        Vector3 min = cam.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
                        Vector3 max = cam.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                        screenRect = new Rect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                            Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
                    }
                }

                items.Add(new Dictionary<string, object>
                {
                    ["name"] = col.name,
                    ["path"] = GetGameObjectPath(col.gameObject),
                    ["type"] = "gameobject_2d",
                    ["component"] = col.GetType().Name,
                    ["interactable"] = col.enabled,
                    ["screen_rect"] = RectToDict(screenRect)
                });
            }
        }

        // --- Shared Helpers ---

        static Rect? ProjectBoundsToScreen(Bounds bounds)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            Vector3 sp = cam.WorldToScreenPoint(bounds.center);
            if (sp.z <= 0) return null;
            Vector3 min = cam.WorldToScreenPoint(bounds.min);
            Vector3 max = cam.WorldToScreenPoint(bounds.max);
            float x = Mathf.Min(min.x, max.x);
            float y = Mathf.Min(min.y, max.y);
            return new Rect(x, y, Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        }

        internal static Rect? GetSelectableScreenRect(RectTransform rt)
        {
            if (rt == null) return null;

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>()?.rootCanvas;

            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
                float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
                float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }

            var cam = canvas?.worldCamera ?? Camera.main;
            if (cam != null)
            {
                Vector2 min = cam.WorldToScreenPoint(corners[0]);
                Vector2 max = cam.WorldToScreenPoint(corners[2]);
                return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            }

            return null;
        }

        internal static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return "/" + path;
        }

        static object RectToDict(Rect? rect)
        {
            if (!rect.HasValue) return null;
            return new { x = rect.Value.x, y = rect.Value.y, width = rect.Value.width, height = rect.Value.height };
        }
    }
}
