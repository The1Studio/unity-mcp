using System;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Editor.Tools.InputSimulation
{
    /// <summary>
    /// Resolves all 4 target modes (coordinates, gameobject, ui_element, anchor) to screen coordinates.
    /// Also handles discover and get_element_bounds actions.
    /// </summary>
    internal static class InputTargetResolver
    {
        // --- Target Resolution ---

        /// <summary>
        /// Resolve a target JSON object to screen coordinates (pixel position).
        /// Returns (screenPos, null) on success or (null, errorMessage) on failure.
        /// </summary>
        internal static (Vector2? screenPos, string error) ResolveTarget(JObject targetObj)
        {
            if (targetObj == null)
                return (null, "Target parameter is required.");

            string mode = targetObj["mode"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(mode))
                return (null, "Target 'mode' is required. Valid: coordinates, gameobject, ui_element, anchor.");

            return mode switch
            {
                "coordinates" => ResolveCoordinates(targetObj),
                "gameobject"  => ResolveGameObject(targetObj),
                "ui_element"  => ResolveUIElement(targetObj),
                "anchor"      => ResolveAnchor(targetObj),
                _ => (null, $"Unknown target mode: '{mode}'. Valid: coordinates, gameobject, ui_element, anchor.")
            };
        }

        /// <summary>
        /// Resolve a target to its full screen Rect (for bounds queries).
        /// Returns (rect, null) on success or (null, errorMessage) on failure.
        /// </summary>
        internal static (Rect? bounds, string error) ResolveTargetBounds(JObject targetObj)
        {
            if (targetObj == null)
                return (null, "Target parameter is required.");

            string mode = targetObj["mode"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(mode))
                return (null, "Target 'mode' is required.");

            return mode switch
            {
                "coordinates" => ResolveCoordinatesBounds(targetObj),
                "gameobject"  => ResolveGameObjectBounds(targetObj),
                "ui_element"  => ResolveUIElementBounds(targetObj),
                "anchor"      => ResolveAnchorBounds(targetObj),
                _ => (null, $"Unknown target mode: '{mode}'.")
            };
        }

        // --- Coordinates Mode ---

        static (Vector2?, string) ResolveCoordinates(JObject target)
        {
            float? x = (float?)target["x"];
            float? y = (float?)target["y"];
            if (!x.HasValue || !y.HasValue)
                return (null, "Coordinates mode requires 'x' and 'y' fields.");
            return (new Vector2(x.Value, y.Value), null);
        }

        static (Rect?, string) ResolveCoordinatesBounds(JObject target)
        {
            var (pos, err) = ResolveCoordinates(target);
            if (err != null) return (null, err);
            // Point rect (no size info for raw coordinates)
            return (new Rect(pos.Value.x, pos.Value.y, 0, 0), null);
        }

        // --- GameObject Mode ---

        static Camera FindCamera(JObject target)
        {
            string cameraName = target["camera_name"]?.ToString();
            string cameraTag = target["camera_tag"]?.ToString();

            if (!string.IsNullOrEmpty(cameraName))
            {
                var go = GameObject.Find(cameraName);
                if (go != null) return go.GetComponent<Camera>();
            }

            if (!string.IsNullOrEmpty(cameraTag))
            {
                try { return Camera.main != null && Camera.main.CompareTag(cameraTag) ? Camera.main : GameObject.FindWithTag(cameraTag)?.GetComponent<Camera>(); }
                catch { /* tag not defined */ }
            }

            return Camera.main;
        }

        static (GameObject, string) FindTargetGameObject(JObject target)
        {
            string path = target["path"]?.ToString();
            string name = target["name"]?.ToString();
            string instanceId = target["instance_id"]?.ToString();

            GameObject go = null;

            if (!string.IsNullOrEmpty(path))
                go = GameObject.Find(path);
            else if (!string.IsNullOrEmpty(instanceId) && int.TryParse(instanceId, out int id))
                go = UnityEditor.EditorUtility.InstanceIDToObject(id) as GameObject;
            else if (!string.IsNullOrEmpty(name))
                go = GameObject.Find(name);

            if (go == null)
                return (null, $"GameObject not found. Searched: path='{path}', name='{name}', instance_id='{instanceId}'.");

            return (go, null);
        }

        static (Vector2?, string) ResolveGameObject(JObject target)
        {
            var (go, err) = FindTargetGameObject(target);
            if (err != null) return (null, err);

            var cam = FindCamera(target);
            if (cam == null)
                return (null, "No camera found. Set Camera.main or provide 'camera_name'/'camera_tag' in target.");

            var renderer = go.GetComponent<Renderer>();
            Vector3 worldPos = renderer != null ? renderer.bounds.center : go.transform.position;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0)
                return (null, $"GameObject '{go.name}' is behind the camera.");

            return (new Vector2(screenPos.x, screenPos.y), null);
        }

        static (Rect?, string) ResolveGameObjectBounds(JObject target)
        {
            var (go, err) = FindTargetGameObject(target);
            if (err != null) return (null, err);

            var cam = FindCamera(target);
            if (cam == null)
                return (null, "No camera found.");

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var bounds = renderer.bounds;
                Vector3 min = cam.WorldToScreenPoint(bounds.min);
                Vector3 max = cam.WorldToScreenPoint(bounds.max);
                if (min.z < 0 && max.z < 0) return (null, $"GameObject '{go.name}' is behind the camera.");
                float x = Mathf.Min(min.x, max.x);
                float y = Mathf.Min(min.y, max.y);
                float w = Mathf.Abs(max.x - min.x);
                float h = Mathf.Abs(max.y - min.y);
                return (new Rect(x, y, w, h), null);
            }

            // Fallback: point at transform position
            Vector3 sp = cam.WorldToScreenPoint(go.transform.position);
            return (new Rect(sp.x, sp.y, 0, 0), null);
        }

        // --- UI Element Mode ---

        static (Vector2?, string) ResolveUIElement(JObject target)
        {
            var (rect, err) = ResolveUIElementBounds(target);
            if (err != null) return (null, err);
            return (rect.Value.center, null);
        }

        static (Rect?, string) ResolveUIElementBounds(JObject target)
        {
            string query = target["name"]?.ToString() ?? target["query"]?.ToString();
            if (string.IsNullOrEmpty(query))
                return (null, "ui_element mode requires 'name' or 'query' field.");

            // Search UGUI Selectables
            foreach (var selectable in Selectable.allSelectablesArray)
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy) continue;
                if (!selectable.name.Equals(query, StringComparison.OrdinalIgnoreCase) &&
                    !selectable.name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

                var rt = selectable.GetComponent<RectTransform>();
                if (rt == null) continue;

                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                var cam = rt.GetComponentInParent<Canvas>()?.worldCamera;
                // Screen space overlay: corners are already in screen space
                var canvas = rt.GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
                    float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                    float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
                    float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                    return (new Rect(minX, minY, maxX - minX, maxY - minY), null);
                }
                else if (cam != null)
                {
                    // World space / camera space canvas
                    Vector2 min = cam.WorldToScreenPoint(corners[0]);
                    Vector2 max = cam.WorldToScreenPoint(corners[2]);
                    return (new Rect(min.x, min.y, max.x - min.x, max.y - min.y), null);
                }
            }

            return (null, $"UI element not found matching '{query}'. Ensure it is active and has a Selectable component.");
        }

        // --- Anchor Mode ---

        static (Vector2?, string) ResolveAnchor(JObject target)
        {
            string anchor = target["anchor"]?.ToString()?.ToLowerInvariant() ?? target["position"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(anchor))
                return (null, "Anchor mode requires 'anchor' or 'position' field.");

            float offsetX = (float?)target["offset_x"] ?? 0f;
            float offsetY = (float?)target["offset_y"] ?? 0f;
            float w = Screen.width;
            float h = Screen.height;

            Vector2 pos = anchor switch
            {
                "center"        => new Vector2(w * 0.5f, h * 0.5f),
                "top_left"      => new Vector2(0, h),
                "top_right"     => new Vector2(w, h),
                "top_center"    => new Vector2(w * 0.5f, h),
                "bottom_left"   => new Vector2(0, 0),
                "bottom_right"  => new Vector2(w, 0),
                "bottom_center" => new Vector2(w * 0.5f, 0),
                "center_left"   => new Vector2(0, h * 0.5f),
                "center_right"  => new Vector2(w, h * 0.5f),
                _ => Vector2.negativeInfinity
            };

            if (float.IsNegativeInfinity(pos.x))
                return (null, $"Unknown anchor: '{anchor}'. Valid: center, top_left, top_right, top_center, bottom_left, bottom_right, bottom_center, center_left, center_right.");

            return (pos + new Vector2(offsetX, offsetY), null);
        }

        static (Rect?, string) ResolveAnchorBounds(JObject target)
        {
            var (pos, err) = ResolveAnchor(target);
            if (err != null) return (null, err);
            return (new Rect(pos.Value.x, pos.Value.y, 0, 0), null);
        }

        // Discovery and GetElementBounds are in InputDiscovery.cs
    }
}
