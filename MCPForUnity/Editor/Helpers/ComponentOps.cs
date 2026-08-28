using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using MCPForUnity.Runtime.Helpers;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Low-level component operations extracted from ManageGameObject and ManageComponents.
    /// Provides pure C# operations without JSON parsing or response formatting.
    /// </summary>
    public static class ComponentOps
    {
        /// <summary>
        /// Adds a component to a GameObject with Undo support.
        /// </summary>
        /// <param name="target">The target GameObject</param>
        /// <param name="componentType">The type of component to add</param>
        /// <param name="error">Error message if operation fails</param>
        /// <returns>The added component, or null if failed</returns>
        public static Component AddComponent(GameObject target, Type componentType, out string error)
        {
            error = null;

            if (target == null)
            {
                error = "Target GameObject is null.";
                return null;
            }

            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                error = $"Type '{componentType?.Name ?? "null"}' is not a valid Component type.";
                return null;
            }

            // Prevent adding duplicate Transform
            if (componentType == typeof(Transform))
            {
                error = "Cannot add another Transform component.";
                return null;
            }

            // Check for 2D/3D physics conflicts
            string conflictError = CheckPhysicsConflict(target, componentType);
            if (conflictError != null)
            {
                error = conflictError;
                return null;
            }

            // Produce a clearer error when this component already exists and cannot be duplicated.
            Component existingComponent = target.GetComponent(componentType);
            if (existingComponent != null && !AllowsMultiple(target, componentType))
            {
                error = $"Component '{componentType.Name}' already exists on '{target.name}' and this type does not allow multiple instances.";
                return null;
            }

            try
            {
                Component newComponent = Undo.AddComponent(target, componentType);
                if (newComponent == null)
                {
                    if (target.GetComponent(componentType) != null && !AllowsMultiple(target, componentType))
                    {
                        error = $"Component '{componentType.Name}' already exists on '{target.name}' and this type does not allow multiple instances.";
                    }
                    else
                    {
                        error = $"Failed to add component '{componentType.Name}' to '{target.name}'. Unity may restrict this component on the current target.";
                    }
                    return null;
                }

                // Apply default values for specific component types
                ApplyDefaultValues(newComponent);

                return newComponent;
            }
            catch (Exception ex)
            {
                error = $"Error adding component '{componentType.Name}': {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Removes a component from a GameObject with Undo support.
        /// </summary>
        /// <param name="target">The target GameObject</param>
        /// <param name="componentType">The type of component to remove</param>
        /// <param name="error">Error message if operation fails</param>
        /// <returns>True if component was removed successfully</returns>
        public static bool RemoveComponent(GameObject target, Type componentType, out string error)
        {
            error = null;

            if (target == null)
            {
                error = "Target GameObject is null.";
                return false;
            }

            if (componentType == null)
            {
                error = "Component type is null.";
                return false;
            }

            // Prevent removing Transform
            if (componentType == typeof(Transform))
            {
                error = "Cannot remove Transform component.";
                return false;
            }

            Component component = target.GetComponent(componentType);
            if (component == null)
            {
                error = $"Component '{componentType.Name}' not found on '{target.name}'.";
                return false;
            }

            try
            {
                Undo.DestroyObjectImmediate(component);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Error removing component '{componentType.Name}': {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Sets a property value on a component using reflection.
        /// </summary>
        /// <param name="component">The target component</param>
        /// <param name="propertyName">The property or field name</param>
        /// <param name="value">The value to set (JToken)</param>
        /// <param name="error">Error message if operation fails</param>
        /// <returns>True if property was set successfully</returns>
        public static bool SetProperty(Component component, string propertyName, JToken value, out string error)
        {
            return SetProperty(component, propertyName, value, null, out error);
        }

        /// <summary>
        /// Sets a property on a component, optionally resolving in-prefab object references
        /// against <paramref name="prefabRoot"/> instead of the active scene.
        /// When <paramref name="prefabRoot"/> is non-null (e.g. headless prefab editing via
        /// PrefabUtility.LoadPrefabContents), name/path/instanceID object-reference payloads are
        /// resolved exclusively within the loaded prefab hierarchy and never fall back to the scene,
        /// preventing cross-contamination of references between the prefab and any open scene.
        /// </summary>
        public static bool SetProperty(Component component, string propertyName, JToken value, GameObject prefabRoot, out string error)
        {
            error = null;

            if (component == null)
            {
                error = "Component is null.";
                return false;
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                error = "Property name is null or empty.";
                return false;
            }

            Type type = component.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            string normalizedName = ParamCoercion.NormalizePropertyName(propertyName);

            // UnityEventBase-derived types must be set via SerializedProperty, not reflection.
            // Reflection creates a disconnected object that Unity's serialization layer doesn't track,
            // causing m_PersistentCalls to be empty when the scene is saved.
            Type memberType = ResolveMemberType(type, propertyName, normalizedName);
            if (memberType != null && typeof(UnityEventBase).IsAssignableFrom(memberType))
            {
                return SetViaSerializedProperty(component, propertyName, normalizedName, value, prefabRoot, out error);
            }

            // When resolving inside a loaded prefab, object references must be resolved against
            // the prefab hierarchy via SerializedProperty — skip the reflection path so the
            // prefabRoot-aware resolver is always used for object-reference payloads.
            bool deferToSerializedForPrefab = prefabRoot != null
                && value != null
                && value.Type == JTokenType.Object
                && memberType != null
                && typeof(UnityEngine.Object).IsAssignableFrom(memberType);

            // Try reflection first (property, field, then non-public serialized field)
            if (!deferToSerializedForPrefab
                && TrySetViaReflection(component, type, propertyName, normalizedName, flags, value, out error))
                return true;

            // Reflection failed — fall back to SerializedProperty which handles arrays,
            // custom serialization (e.g. UdonSharp), and types reflection can't convert.
            string reflectionError = error;
            if (SetViaSerializedProperty(component, propertyName, normalizedName, value, prefabRoot, out error))
                return true;

            // Both paths failed. If reflection found the member but couldn't convert,
            // report that (more useful than the SerializedProperty error).
            // If reflection didn't find it at all, report the SerializedProperty error.
            if (reflectionError != null && !reflectionError.Contains("not found"))
                error = reflectionError;

            return false;
        }

        private static bool TrySetViaReflection(object component, Type type, string propertyName, string normalizedName, BindingFlags flags, JToken value, out string error)
        {
            error = null;

            // Skip reflection for UnityEngine.Object types with JObject values
            // so SerializedProperty can resolve guid/spriteName/fileID forms.
            bool isJObjectValue = value != null && value.Type == JTokenType.Object;

            // Try property first
            PropertyInfo propInfo = type.GetProperty(propertyName, flags)
                                 ?? type.GetProperty(normalizedName, flags);
            if (propInfo != null && propInfo.CanWrite)
            {
                if (isJObjectValue && typeof(UnityEngine.Object).IsAssignableFrom(propInfo.PropertyType))
                {
                    // Let SerializedProperty path handle complex object references.
                    return false;
                }

                try
                {
                    object convertedValue = PropertyConversion.ConvertToType(value, propInfo.PropertyType);
                    if (convertedValue == null && value.Type != JTokenType.Null)
                    {
                        error = $"Failed to convert value for property '{propertyName}' to type '{propInfo.PropertyType.Name}'.";
                        return false;
                    }
                    propInfo.SetValue(component, convertedValue);
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"Failed to set property '{propertyName}': {ex.Message}";
                    return false;
                }
            }

            // Try field
            FieldInfo fieldInfo = type.GetField(propertyName, flags)
                               ?? type.GetField(normalizedName, flags);
            if (fieldInfo != null && !fieldInfo.IsInitOnly)
            {
                if (isJObjectValue && typeof(UnityEngine.Object).IsAssignableFrom(fieldInfo.FieldType))
                {
                    // Let SerializedProperty path handle complex object references.
                    return false;
                }

                try
                {
                    object convertedValue = PropertyConversion.ConvertToType(value, fieldInfo.FieldType);
                    if (convertedValue == null && value.Type != JTokenType.Null)
                    {
                        error = $"Failed to convert value for field '{propertyName}' to type '{fieldInfo.FieldType.Name}'.";
                        return false;
                    }
                    fieldInfo.SetValue(component, convertedValue);
                    if (!ReadbackMatches(fieldInfo.GetValue(component), convertedValue))
                    {
                        error = $"Field '{propertyName}' was set but did not persist the requested value (readback mismatch).";
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"Failed to set field '{propertyName}': {ex.Message}";
                    return false;
                }
            }

            // Try non-public serialized fields — traverse inheritance hierarchy
            fieldInfo = FindSerializedFieldInHierarchy(type, propertyName)
                     ?? FindSerializedFieldInHierarchy(type, normalizedName);
            if (fieldInfo != null)
            {
                if (isJObjectValue && typeof(UnityEngine.Object).IsAssignableFrom(fieldInfo.FieldType))
                {
                    // Let SerializedProperty path handle complex object references.
                    return false;
                }

                try
                {
                    object convertedValue = PropertyConversion.ConvertToType(value, fieldInfo.FieldType);
                    if (convertedValue == null && value.Type != JTokenType.Null)
                    {
                        error = $"Failed to convert value for serialized field '{propertyName}' to type '{fieldInfo.FieldType.Name}'.";
                        return false;
                    }
                    fieldInfo.SetValue(component, convertedValue);
                    if (!ReadbackMatches(fieldInfo.GetValue(component), convertedValue))
                    {
                        error = $"Serialized field '{propertyName}' was set but did not persist the requested value (readback mismatch).";
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"Failed to set serialized field '{propertyName}': {ex.Message}";
                    return false;
                }
            }

            error = $"Property or field '{propertyName}' not found on component '{type.Name}'.";
            return false;
        }

        /// <summary>
        /// Confirms a raw field write actually stuck by comparing the post-write value
        /// against what was requested. A CLR field set on a live reference-type instance
        /// (a Component) cannot normally "silently fail" — but this catches cases where the
        /// converted value itself was already wrong (e.g. a struct converter that ignored an
        /// unrecognized key and produced a zeroed default) instead of trusting SetValue blindly.
        /// Per issue #42's cross-cutting requirement: a property that could not be applied
        /// must not be reported as success.
        /// </summary>
        private static bool ReadbackMatches(object actual, object expected)
        {
            if (expected == null)
                return actual == null;
            return expected.Equals(actual);
        }

        /// <summary>
        /// Gets all public properties and fields from a component type.
        /// </summary>
        public static List<string> GetAccessibleMembers(Type componentType)
        {
            var members = new List<string>();
            if (componentType == null) return members;

            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var prop in componentType.GetProperties(flags))
            {
                if (prop.CanWrite && prop.GetSetMethod() != null)
                {
                    members.Add(prop.Name);
                }
            }

            foreach (var field in componentType.GetFields(flags))
            {
                if (!field.IsInitOnly)
                {
                    members.Add(field.Name);
                }
            }

            // Include private [SerializeField] fields - traverse inheritance hierarchy
            // Type.GetFields with NonPublic only returns fields declared directly on that type,
            // so we need to walk up the chain to find inherited private serialized fields
            var seenFieldNames = new HashSet<string>(members); // Avoid duplicates with public fields
            Type currentType = componentType;
            while (currentType != null && currentType != typeof(object))
            {
                foreach (var field in currentType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (field.GetCustomAttribute<SerializeField>() != null && !seenFieldNames.Contains(field.Name))
                    {
                        members.Add(field.Name);
                        seenFieldNames.Add(field.Name);
                    }
                }
                currentType = currentType.BaseType;
            }

            members.Sort();
            return members;
        }

        // --- Private Helpers ---

        /// <summary>
        /// Searches for a non-public [SerializeField] field through the entire inheritance hierarchy.
        /// Type.GetField() with NonPublic only returns fields declared directly on that type,
        /// so this method walks up the chain to find inherited private serialized fields.
        /// </summary>
        internal static FieldInfo FindSerializedFieldInHierarchy(Type type, string fieldName)
        {
            if (type == null || string.IsNullOrEmpty(fieldName))
                return null;

            BindingFlags privateFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            Type currentType = type;

            // Walk up the inheritance chain
            while (currentType != null && currentType != typeof(object))
            {
                // Search for the field on this specific type (case-insensitive)
                foreach (var field in currentType.GetFields(privateFlags))
                {
                    if (string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase) &&
                        field.GetCustomAttribute<SerializeField>() != null)
                    {
                        return field;
                    }
                }
                currentType = currentType.BaseType;
            }

            return null;
        }

        private static string CheckPhysicsConflict(GameObject target, Type componentType)
        {
            bool isAdding2DPhysics =
                typeof(Rigidbody2D).IsAssignableFrom(componentType) ||
                typeof(Collider2D).IsAssignableFrom(componentType);

            bool isAdding3DPhysics =
                typeof(Rigidbody).IsAssignableFrom(componentType) ||
                typeof(Collider).IsAssignableFrom(componentType);

            if (isAdding2DPhysics)
            {
                if (target.GetComponent<Rigidbody>() != null || target.GetComponent<Collider>() != null)
                {
                    return $"Cannot add 2D physics component '{componentType.Name}' because the GameObject '{target.name}' already has a 3D Rigidbody or Collider.";
                }
            }
            else if (isAdding3DPhysics)
            {
                if (target.GetComponent<Rigidbody2D>() != null || target.GetComponent<Collider2D>() != null)
                {
                    return $"Cannot add 3D physics component '{componentType.Name}' because the GameObject '{target.name}' already has a 2D Rigidbody or Collider.";
                }
            }

            return null;
        }

        private static void ApplyDefaultValues(Component component)
        {
            // Default newly added Lights to Directional
            if (component is Light light)
            {
                light.type = LightType.Directional;
            }
        }

        private static bool AllowsMultiple(GameObject target, Type componentType)
        {
            if (target == null || componentType == null)
            {
                return false;
            }

            if (Attribute.IsDefined(componentType, typeof(DisallowMultipleComponent), inherit: true))
            {
                return false;
            }

            return true;
        }

        // --- UnityEvent SerializedProperty support ---

        private static Type ResolveMemberType(Type componentType, string propertyName, string normalizedName)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            PropertyInfo propInfo = componentType.GetProperty(propertyName, flags)
                                 ?? componentType.GetProperty(normalizedName, flags);
            if (propInfo != null)
                return propInfo.PropertyType;

            FieldInfo fieldInfo = componentType.GetField(propertyName, flags)
                               ?? componentType.GetField(normalizedName, flags);
            if (fieldInfo != null)
                return fieldInfo.FieldType;

            fieldInfo = FindSerializedFieldInHierarchy(componentType, propertyName)
                     ?? FindSerializedFieldInHierarchy(componentType, normalizedName);
            if (fieldInfo != null)
                return fieldInfo.FieldType;

            return null;
        }

        private static bool SetViaSerializedProperty(Component component, string propertyName, string normalizedName, JToken value, out string error)
        {
            return SetViaSerializedProperty(component, propertyName, normalizedName, value, null, out error);
        }

        private static bool SetViaSerializedProperty(Component component, string propertyName, string normalizedName, JToken value, GameObject prefabRoot, out string error)
        {
            error = null;
            using var so = new SerializedObject(component);

            SerializedProperty prop = so.FindProperty(propertyName)
                                   ?? so.FindProperty(normalizedName);
            if (prop == null)
            {
                error = $"SerializedProperty '{propertyName}' not found on component '{component.GetType().Name}'.";
                return false;
            }

            if (!SetSerializedPropertyRecursive(prop, value, prefabRoot, out error, 0))
                return false;

            so.ApplyModifiedProperties();

            // Readback verification for ObjectReference — these can silently fail
            if (prop.propertyType == SerializedPropertyType.ObjectReference
                && value != null
                && !(value is JValue jv && jv.Type == JTokenType.Null))
            {
                so.Update();
                var verifyProp = so.FindProperty(propertyName)
                              ?? so.FindProperty(normalizedName);
                if (verifyProp != null
                    && verifyProp.propertyType == SerializedPropertyType.ObjectReference
                    && verifyProp.objectReferenceValue == null)
                {
                    error = $"Property '{propertyName}' was set but the object reference did not persist. " +
                            "Check that the referenced object exists and is the correct type.";
                    return false;
                }
            }

            return true;
        }

        private static bool SetSerializedPropertyRecursive(SerializedProperty prop, JToken value, out string error, int depth)
        {
            return SetSerializedPropertyRecursive(prop, value, null, out error, depth);
        }

        private static bool SetSerializedPropertyRecursive(SerializedProperty prop, JToken value, GameObject prefabRoot, out string error, int depth)
        {
            error = null;
            const int MaxDepth = 20;
            if (depth > MaxDepth)
            {
                error = $"Maximum recursion depth ({MaxDepth}) exceeded.";
                return false;
            }

            try
            {
                // Array + JArray
                if (prop.isArray && prop.propertyType != SerializedPropertyType.String && value is JArray jArray)
                {
                    prop.arraySize = jArray.Count;
                    prop.serializedObject.ApplyModifiedProperties();
                    prop.serializedObject.Update();

                    for (int i = 0; i < jArray.Count; i++)
                    {
                        var element = prop.GetArrayElementAtIndex(i);
                        if (!SetSerializedPropertyRecursive(element, jArray[i], prefabRoot, out error, depth + 1))
                            return false;
                    }
                    return true;
                }

                // Generic (struct/class) + JObject
                if (prop.propertyType == SerializedPropertyType.Generic && !prop.isArray && value is JObject jObj)
                {
                    foreach (var kvp in jObj)
                    {
                        var child = FindPropertyRelativeFuzzy(prop, kvp.Key);
                        if (child == null)
                        {
                            error = $"Sub-property '{kvp.Key}' not found under '{prop.propertyPath}'.";
                            return false;
                        }
                        if (!SetSerializedPropertyRecursive(child, kvp.Value, prefabRoot, out error, depth + 1))
                            return false;
                    }
                    return true;
                }

                // ObjectReference
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    return SetObjectReference(prop, value, prefabRoot, out error);

                // Leaf types
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        // Use the checked coercers — never silently narrow an out-of-range
                        // or unparsable value to a default (e.g. a LayerMask bit-pattern
                        // above int.MaxValue must error, not save as 0). See issue #42.
                        if (prop.type == "long")
                        {
                            if (!ParamCoercion.TryCoerceLong(value, out long longVal, out string longError))
                            {
                                error = longError ?? "Expected integer value.";
                                return false;
                            }
                            prop.longValue = longVal;
                        }
                        else
                        {
                            if (!ParamCoercion.TryCoerceInt(value, out int intVal, out string intError))
                            {
                                error = intError ?? "Expected integer value.";
                                return false;
                            }
                            prop.intValue = intVal;
                        }
                        return true;

                    case SerializedPropertyType.Boolean:
                        if (value == null || value.Type == JTokenType.Null)
                        {
                            error = "Expected boolean value.";
                            return false;
                        }
                        prop.boolValue = ParamCoercion.CoerceBool(value, false);
                        return true;

                    case SerializedPropertyType.Float:
                        float floatVal = ParamCoercion.CoerceFloat(value, float.NaN);
                        if (float.IsNaN(floatVal))
                        {
                            error = "Expected float value.";
                            return false;
                        }
                        prop.floatValue = floatVal;
                        return true;

                    case SerializedPropertyType.String:
                        prop.stringValue = value == null || value.Type == JTokenType.Null ? string.Empty : value.ToString();
                        return true;

                    case SerializedPropertyType.Enum:
                        return SetEnum(prop, value, out error);

                    // Vector/struct leaves. Mirrors the switch in ManageScriptableObject
                    // (TrySetSerializedProperty) so the two write paths accept the same value
                    // forms — both the array [x, y] and the object {"x": 0, "y": 0} shape that
                    // VectorParsing handles. Without these, a RectTransform's m_AnchorMin /
                    // m_AnchorMax / m_SizeDelta / m_Pivot (all SerializedPropertyType.Vector2)
                    // fell through to the default and were rejected outright. See issue #79.
                    case SerializedPropertyType.Vector2:
                        var v2 = VectorParsing.ParseVector2(value);
                        if (v2 == null)
                        {
                            error = "Expected Vector2 (array or object).";
                            return false;
                        }
                        prop.vector2Value = v2.Value;
                        return true;

                    case SerializedPropertyType.Vector3:
                        var v3 = VectorParsing.ParseVector3(value);
                        if (v3 == null)
                        {
                            error = "Expected Vector3 (array or object).";
                            return false;
                        }
                        prop.vector3Value = v3.Value;
                        return true;

                    case SerializedPropertyType.Vector4:
                        var v4 = VectorParsing.ParseVector4(value);
                        if (v4 == null)
                        {
                            error = "Expected Vector4 (array or object).";
                            return false;
                        }
                        prop.vector4Value = v4.Value;
                        return true;

                    case SerializedPropertyType.Quaternion:
                        var quat = VectorParsing.ParseQuaternion(value);
                        if (quat == null)
                        {
                            error = "Expected Quaternion (array [x,y,z] euler, [x,y,z,w], or object).";
                            return false;
                        }
                        prop.quaternionValue = quat.Value;
                        return true;

                    case SerializedPropertyType.Color:
                        var col = VectorParsing.ParseColor(value);
                        if (col == null)
                        {
                            error = "Expected Color (array or object).";
                            return false;
                        }
                        prop.colorValue = col.Value;
                        return true;

                    case SerializedPropertyType.Rect:
                        var rect = VectorParsing.ParseRect(value);
                        if (rect == null)
                        {
                            error = "Expected Rect (array [x,y,width,height] or object).";
                            return false;
                        }
                        prop.rectValue = rect.Value;
                        return true;

                    case SerializedPropertyType.Bounds:
                        var bounds = VectorParsing.ParseBounds(value);
                        if (bounds == null)
                        {
                            error = "Expected Bounds (object with center and size).";
                            return false;
                        }
                        prop.boundsValue = bounds.Value;
                        return true;

                    case SerializedPropertyType.AnimationCurve:
                        var curve = VectorParsing.ParseAnimationCurve(value);
                        if (curve == null)
                        {
                            error = "Expected AnimationCurve (object with 'keys' or array of keyframes).";
                            return false;
                        }
                        prop.animationCurveValue = curve;
                        return true;

                    default:
                        error = $"Unsupported SerializedPropertyType: {prop.propertyType} at '{prop.propertyPath}'.";
                        return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Error setting '{prop.propertyPath}': {ex.Message}";
                return false;
            }
        }

        internal static bool SetObjectReference(SerializedProperty prop, JToken value, out string error)
        {
            return SetObjectReference(prop, value, null, out error);
        }

        /// <summary>
        /// Resolves and assigns an object reference. When <paramref name="prefabRoot"/> is non-null,
        /// name/path/instanceID payloads are resolved against the loaded prefab hierarchy
        /// (<see cref="GameObject.GetComponentsInChildren{T}(bool)"/> on the prefab root) with
        /// prefab-only precedence — the active scene is never consulted, so references cannot
        /// cross-contaminate between the prefab and any open scene. Asset payloads
        /// (guid/path-to-asset) are unaffected and resolve via the AssetDatabase as before.
        /// </summary>
        internal static bool SetObjectReference(SerializedProperty prop, JToken value, GameObject prefabRoot, out string error)
        {
            error = null;

            if (value == null || value.Type == JTokenType.Null)
            {
                prop.objectReferenceValue = null;
                return true;
            }

            if (value.Type == JTokenType.Integer)
            {
                int id = value.Value<int>();
                if (prefabRoot != null)
                {
                    var prefabObj = ResolveInPrefabByInstanceId(prefabRoot, id);
                    if (prefabObj == null)
                    {
                        error = $"No object found with instanceID {id} in loaded prefab.";
                        return false;
                    }
                    return AssignObjectReference(prop, prefabObj, null, out error);
                }
                var resolved = GameObjectLookup.ResolveInstanceID(id);
                if (resolved == null)
                {
                    error = $"No object found with instanceID {id}.";
                    return false;
                }
                return AssignObjectReference(prop, resolved, null, out error);
            }

            if (value is JObject jObj)
            {
                // Optional component type filter — e.g. {"instanceID": 123, "component": "Button"}
                string componentFilter = jObj["component"]?.ToString();

                var idToken = jObj["instanceID"];
                if (idToken != null)
                {
                    int id = ParamCoercion.CoerceInt(idToken, 0);
                    if (prefabRoot != null)
                    {
                        var prefabObj = ResolveInPrefabByInstanceId(prefabRoot, id);
                        if (prefabObj == null)
                        {
                            error = $"No object found with instanceID {id} in loaded prefab.";
                            return false;
                        }
                        return AssignObjectReference(prop, prefabObj, componentFilter, out error);
                    }
                    var resolved = GameObjectLookup.ResolveInstanceID(id);
                    if (resolved == null)
                    {
                        error = $"No object found with instanceID {id}.";
                        return false;
                    }
                    return AssignObjectReference(prop, resolved, componentFilter, out error);
                }

                var guidToken = jObj["guid"];
                if (guidToken != null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guidToken.ToString());
                    if (string.IsNullOrEmpty(path))
                    {
                        error = $"No asset found for GUID '{guidToken}'.";
                        return false;
                    }

                    var spriteNameToken = jObj["spriteName"];
                    if (spriteNameToken != null)
                    {
                        string spriteName = spriteNameToken.ToString();
                        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                        var originalRef = prop.objectReferenceValue;
                        foreach (var asset in allAssets)
                        {
                            if (asset is Sprite sprite && sprite.name == spriteName)
                            {
                                prop.objectReferenceValue = sprite;
                                if (prop.objectReferenceValue != null)
                                    return true;
                                // Unity rejected the type — restore and report
                                prop.objectReferenceValue = originalRef;
                                error = $"Sprite '{spriteName}' found but is not compatible with the property type.";
                                return false;
                            }
                        }

                        error = $"Sprite '{spriteName}' not found in atlas '{path}'.";
                        return false;
                    }

                    var fileIdToken = jObj["fileID"];
                    if (fileIdToken != null)
                    {
                        long targetFileId = fileIdToken.Value<long>();
                        if (targetFileId != 0)
                        {
                            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                            var originalRef = prop.objectReferenceValue;
                            foreach (var asset in allAssets)
                            {
                                if (asset is Sprite sprite)
                                {
                                    long spriteFileId = GetSpriteFileId(sprite);
                                    if (spriteFileId == targetFileId)
                                    {
                                        prop.objectReferenceValue = sprite;
                                        if (prop.objectReferenceValue != null)
                                            return true;
                                        prop.objectReferenceValue = originalRef;
                                        error = $"Sprite with fileID '{targetFileId}' found but is not compatible with the property type.";
                                        return false;
                                    }
                                }
                            }
                        }

                        error = $"Sprite with fileID '{targetFileId}' not found in atlas '{path}'.";
                        return false;
                    }

                    var loaded = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    return AssignObjectReference(prop, loaded, componentFilter, out error);
                }

                var pathToken = jObj["path"];
                if (pathToken != null)
                {
                    string pathStr = pathToken.ToString();

                    // When editing a loaded prefab, a {"path": ...} payload refers to an in-prefab
                    // hierarchy path (e.g. "HomeScreenView/Container/BtnProfile"), NOT an asset path.
                    // Resolve against the prefab hierarchy only; do not fall back to the scene/AssetDatabase.
                    if (prefabRoot != null)
                    {
                        var prefabObj = ResolveInPrefabByPath(prefabRoot, pathStr);
                        if (prefabObj == null)
                        {
                            error = $"No GameObject found at path '{pathStr}' within loaded prefab '{prefabRoot.name}'.";
                            return false;
                        }
                        return AssignObjectReference(prop, prefabObj, componentFilter, out error);
                    }

                    string sanitized = AssetPathUtility.SanitizeAssetPath(pathStr);
                    var resolved = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sanitized);
                    if (resolved == null)
                    {
                        error = $"No asset found at path '{pathToken}'.";
                        return false;
                    }
                    return AssignObjectReference(prop, resolved, componentFilter, out error);
                }

                var nameToken = jObj["name"];
                if (nameToken != null)
                {
                    if (prefabRoot != null)
                    {
                        var prefabObj = ResolveInPrefabByName(prefabRoot, nameToken.ToString());
                        if (prefabObj == null)
                        {
                            error = $"No GameObject named '{nameToken}' found in loaded prefab '{prefabRoot.name}'.";
                            return false;
                        }
                        return AssignObjectReference(prop, prefabObj, componentFilter, out error);
                    }
                    return ResolveSceneObjectByName(prop, nameToken.ToString(), componentFilter, out error);
                }

                error = "Object reference must contain 'instanceID', 'guid', 'path', or 'name'.";
                return false;
            }

            if (value.Type == JTokenType.String)
            {
                string strVal = value.ToString();

                // Prefab-scoped resolution: a bare string identifies an in-prefab child by
                // instanceID, hierarchy path, or name. Genuine asset references (Assets/-paths
                // and 32-char GUIDs) still resolve via the AssetDatabase, since a prefab may
                // legitimately reference project assets.
                if (prefabRoot != null)
                {
                    if (int.TryParse(strVal, out int prefabParsedId))
                    {
                        var prefabObj = ResolveInPrefabByInstanceId(prefabRoot, prefabParsedId);
                        if (prefabObj != null)
                            return AssignObjectReference(prop, prefabObj, null, out error);
                        // Fall through to path/name resolution within the prefab.
                    }

                    if (strVal.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        string sanitizedAsset = AssetPathUtility.SanitizeAssetPath(strVal);
                        var assetRef = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sanitizedAsset);
                        if (assetRef == null)
                        {
                            error = $"No asset found at path '{strVal}'.";
                            return false;
                        }
                        return AssignObjectReference(prop, assetRef, null, out error);
                    }

                    if (strVal.Length == 32 && IsHexString(strVal))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(strVal);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            var assetRef = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                            if (assetRef != null)
                                return AssignObjectReference(prop, assetRef, null, out error);
                        }
                    }

                    var prefabByPath = strVal.Contains("/")
                        ? ResolveInPrefabByPath(prefabRoot, strVal)
                        : ResolveInPrefabByName(prefabRoot, strVal);
                    if (prefabByPath == null)
                    {
                        error = $"No GameObject matching '{strVal}' found in loaded prefab '{prefabRoot.name}'.";
                        return false;
                    }
                    return AssignObjectReference(prop, prefabByPath, null, out error);
                }

                // Try as instanceID if the string is purely numeric
                if (int.TryParse(strVal, out int parsedId))
                {
                    var resolved = GameObjectLookup.ResolveInstanceID(parsedId);
                    if (resolved != null)
                        return AssignObjectReference(prop, resolved, null, out error);
                    // Not a valid instanceID — fall through to path/name resolution
                }

                if (strVal.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || strVal.Contains("/"))
                {
                    string sanitized = AssetPathUtility.SanitizeAssetPath(strVal);
                    var resolved = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sanitized);
                    if (resolved == null)
                    {
                        error = $"No asset found at path '{strVal}'.";
                        return false;
                    }
                    return AssignObjectReference(prop, resolved, null, out error);
                }

                // Try as asset GUID (32-char hex string)
                if (strVal.Length == 32 && IsHexString(strVal))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(strVal);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var resolved = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (resolved != null)
                            return AssignObjectReference(prop, resolved, null, out error);
                    }
                }

                // Fall back to scene hierarchy lookup by name.
                return ResolveSceneObjectByName(prop, strVal, null, out error);
            }

            error = $"Unsupported object reference format: {value.Type}.";
            return false;
        }

        /// <summary>
        /// Assigns a resolved object to a SerializedProperty, with automatic component fallback.
        /// If the resolved object is a GameObject but the property expects a Component type,
        /// searches the GameObject's components for a compatible one.
        /// Optionally filters by component type name (e.g. "Button", "Rigidbody").
        /// </summary>
        private static bool AssignObjectReference(SerializedProperty prop, UnityEngine.Object resolved, string componentFilter, out string error)
        {
            error = null;
            if (resolved == null)
            {
                error = "Resolved object is null.";
                return false;
            }

            // If a component filter is specified and the resolved object is a GameObject,
            // find the specific component by type name.
            if (!string.IsNullOrEmpty(componentFilter) && resolved is GameObject filterGo)
            {
                var components = filterGo.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    if (string.Equals(comp.GetType().Name, componentFilter, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(comp.GetType().FullName, componentFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        prop.objectReferenceValue = comp;
                        if (prop.objectReferenceValue != null)
                            return true;
                    }
                }
                error = $"Component '{componentFilter}' not found on GameObject '{filterGo.name}'.";
                return false;
            }

            // Try direct assignment first
            prop.objectReferenceValue = resolved;
            if (prop.objectReferenceValue != null)
                return true;

            // Sub-asset fallback: e.g., Texture2D → Sprite
            string subAssetPath = AssetDatabase.GetAssetPath(resolved);
            if (!string.IsNullOrEmpty(subAssetPath))
            {
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(subAssetPath);
                UnityEngine.Object match = null;
                int matchCount = 0;
                foreach (var sub in subAssets)
                {
                    if (sub == null || sub == resolved) continue;
                    prop.objectReferenceValue = sub;
                    if (prop.objectReferenceValue != null)
                    {
                        match = sub;
                        matchCount++;
                        if (matchCount > 1) break;
                    }
                }

                if (matchCount == 1)
                {
                    prop.objectReferenceValue = match;
                    return true;
                }

                // Clean up: probing may have left the property dirty
                prop.objectReferenceValue = null;

                if (matchCount > 1)
                {
                    error = $"Multiple compatible sub-assets found in '{subAssetPath}'. " +
                            "Use {\"guid\": \"...\", \"spriteName\": \"<name>\"} or " +
                            "{\"guid\": \"...\", \"fileID\": <id>} for precise selection.";
                    return false;
                }
            }

            // If the resolved object is a GameObject but the property expects a Component,
            // try each component on the GameObject until one is accepted.
            if (resolved is GameObject go)
            {
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    prop.objectReferenceValue = comp;
                    if (prop.objectReferenceValue != null)
                        return true;
                }
                error = $"GameObject '{go.name}' found but no compatible component for the property type.";
                return false;
            }

            error = $"Object '{resolved.name}' (type: {resolved.GetType().Name}) is not compatible with the property type.";
            return false;
        }

        /// <summary>
        /// Resolves a scene GameObject by name and assigns it (or a component on it)
        /// to a SerializedProperty. Uses GameObjectLookup for robust search
        /// including inactive objects and prefab stage support.
        /// </summary>
        private static bool ResolveSceneObjectByName(SerializedProperty prop, string name, string componentFilter, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Cannot resolve object reference from empty name.";
                return false;
            }

            var ids = GameObjectLookup.SearchGameObjects(
                GameObjectLookup.SearchMethod.ByName, name, includeInactive: true, maxResults: 1);

            if (ids.Count == 0)
            {
                error = $"No GameObject named '{name}' found in scene.";
                return false;
            }

            var go = GameObjectLookup.FindById(ids[0]);
            if (go == null)
            {
                error = $"GameObject '{name}' found but could not be resolved.";
                return false;
            }

            return AssignObjectReference(prop, go, componentFilter, out error);
        }

        /// <summary>
        /// Resolves a GameObject within a loaded prefab hierarchy by name.
        /// Searches all transforms (including inactive) under the prefab root and the root itself.
        /// Prefab-only — never consults the active scene.
        /// </summary>
        private static GameObject ResolveInPrefabByName(GameObject prefabRoot, string name)
        {
            if (prefabRoot == null || string.IsNullOrWhiteSpace(name))
                return null;

            // Match the root first (callers commonly target the root by name).
            if (prefabRoot.name == name)
                return prefabRoot;

            foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject.name == name)
                    return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Resolves a GameObject within a loaded prefab hierarchy by hierarchy path
        /// (e.g. "Root/Container/BtnProfile"). Tolerates a leading root-name segment.
        /// Prefab-only — never consults the active scene or the AssetDatabase.
        /// </summary>
        private static GameObject ResolveInPrefabByPath(GameObject prefabRoot, string path)
        {
            if (prefabRoot == null || string.IsNullOrWhiteSpace(path))
                return null;

            string trimmed = path.Trim('/');
            if (trimmed.Length == 0)
                return prefabRoot;

            // Try the path verbatim relative to the root.
            Transform found = prefabRoot.transform.Find(trimmed);
            if (found != null)
                return found.gameObject;

            // Tolerate a leading root-name segment ("Root/Child/..." → "Child/...").
            string rootPrefix = prefabRoot.name + "/";
            if (trimmed.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                string relative = trimmed.Substring(rootPrefix.Length);
                found = prefabRoot.transform.Find(relative);
                if (found != null)
                    return found.gameObject;
            }

            // Fall back to matching the final segment by name anywhere in the hierarchy.
            int lastSlash = trimmed.LastIndexOf('/');
            string leaf = lastSlash >= 0 ? trimmed.Substring(lastSlash + 1) : trimmed;
            return ResolveInPrefabByName(prefabRoot, leaf);
        }

        /// <summary>
        /// Resolves a GameObject or Component within a loaded prefab hierarchy by instanceID.
        /// Prefab-only — never consults the active scene. Returns the matching GameObject,
        /// Component, or null if the instanceID does not belong to the loaded prefab.
        /// </summary>
        private static UnityEngine.Object ResolveInPrefabByInstanceId(GameObject prefabRoot, int instanceId)
        {
            if (prefabRoot == null || instanceId == 0)
                return null;

            foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;

                var go = t.gameObject;
                if (go.GetInstanceIDCompat() == instanceId)
                    return go;

                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && comp.GetInstanceIDCompat() == instanceId)
                        return comp;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a child SerializedProperty by name, falling back to underscore-insensitive matching.
        /// The batch_execute transport can strip underscores from JSON keys
        /// (e.g. m_PersistentCalls → mPersistentCalls), so we iterate immediate children
        /// and compare with underscores removed.
        /// </summary>
        private static SerializedProperty FindPropertyRelativeFuzzy(SerializedProperty parent, string key)
        {
            var child = parent.FindPropertyRelative(key);
            if (child != null) return child;

            string normalizedKey = key.Replace("_", "").ToLowerInvariant();

            var end = parent.GetEndProperty();
            var iter = parent.Copy();
            if (!iter.Next(true)) return null;

            while (!SerializedProperty.EqualContents(iter, end))
            {
                if (iter.depth == parent.depth + 1)
                {
                    string normalizedName = iter.name.Replace("_", "").ToLowerInvariant();
                    if (normalizedName == normalizedKey)
                        return parent.FindPropertyRelative(iter.name);
                }
                if (!iter.Next(false))
                    break;
            }

            return null;
        }

        private static bool SetEnum(SerializedProperty prop, JToken value, out string error)
        {
            error = null;
            var names = prop.enumNames;
            if (names == null || names.Length == 0)
            {
                error = "Enum has no names.";
                return false;
            }

            if (value.Type == JTokenType.Integer)
            {
                int idx = value.Value<int>();
                if (idx < 0 || idx >= names.Length)
                {
                    error = $"Enum index out of range: {idx}.";
                    return false;
                }
                prop.enumValueIndex = idx;
                return true;
            }

            string s = value.ToString();
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], s, StringComparison.OrdinalIgnoreCase))
                {
                    prop.enumValueIndex = i;
                    return true;
                }
            }
            error = $"Unknown enum name '{s}'.";
            return false;
        }

        private static long GetSpriteFileId(Sprite sprite)
        {
            if (sprite == null)
                return 0;

            try
            {
                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(sprite);
                return unchecked((long)globalId.targetObjectId);
            }
            catch (Exception ex)
            {
                McpLog.Warn($"Failed to get fileID for sprite '{sprite.name}' (instanceID={sprite.GetInstanceIDCompat()}): {ex.Message}");
                return 0;
            }
        }

        private static bool IsHexString(string str)
        {
            foreach (char c in str)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }
    }
}

