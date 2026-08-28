#if UNITY_ENTITIES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Entities;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// MCP tool for Unity DOTS ECS debugging, inspection, and performance monitoring.
    /// Actions: list_worlds, query_entities, get_entity, list_systems, get_system,
    ///          performance_snapshot, toggle_system, list_component_types,
    ///          create_entity, destroy_entity, set_component,
    ///          add_component, remove_component, query_count
    /// Requires com.unity.entities package.
    /// </summary>
    [McpForUnityTool("manage_dots", AutoRegister = true)]
    public static class ManageDots
    {
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);

            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess)
                return new ErrorResponse(actionResult.ErrorMessage);

            string action = actionResult.Value.ToLowerInvariant();

            try
            {
                return action switch
                {
                    "list_worlds"           => ListWorlds(p),
                    "query_entities"        => QueryEntities(p),
                    "get_entity"            => GetEntity(p),
                    "list_systems"          => ListSystems(p),
                    "get_system"            => GetSystem(p),
                    "performance_snapshot"  => PerformanceSnapshot(p),
                    "toggle_system"         => ToggleSystem(p),
                    "list_component_types"  => ListComponentTypes(p),
                    "create_entity"         => CreateEntity(p),
                    "destroy_entity"        => DestroyEntity(p),
                    "set_component"         => SetComponent(p),
                    "add_component"         => AddComponent(p),
                    "remove_component"      => RemoveComponent(p),
                    "query_count"           => QueryCount(p),
                    "inspect_bdp_tree"      => InspectBdpTree(p),
                    _ => new ErrorResponse(
                        $"Unknown action: '{action}'. Supported: list_worlds, query_entities, get_entity, " +
                        "list_systems, get_system, performance_snapshot, toggle_system, " +
                        "list_component_types, create_entity, destroy_entity, " +
                        "set_component, add_component, remove_component, query_count, inspect_bdp_tree")
                };
            }
            catch (Exception e)
            {
                McpLog.Error($"[ManageDots] Action '{action}' failed: {e}");
                return new ErrorResponse($"Internal error processing action '{action}': {e.Message}");
            }
        }

        #region World Operations

        private static object ListWorlds(ToolParams p)
        {
            var worlds = new List<object>();
            foreach (var world in World.All)
            {
                worlds.Add(new Dictionary<string, object>
                {
                    ["name"]         = world.Name,
                    ["is_created"]   = world.IsCreated,
                    ["system_count"] = world.Systems.Count,
                    ["entity_count"] = world.EntityManager.UniversalQuery.CalculateEntityCount(),
                    ["flags"]        = world.Flags.ToString()
                });
            }
            return new SuccessResponse($"Found {worlds.Count} world(s).", worlds);
        }

        #endregion

        #region Entity Operations

        private static object QueryEntities(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            string componentTypesStr = p.Get("component_types");
            if (string.IsNullOrEmpty(componentTypesStr))
                return new ErrorResponse("'component_types' parameter is required. Comma-separated component type names.");

            string[] typeNames = componentTypesStr.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();

            if (!TryBuildQuerySignatures(typeNames, out var signatures, out var ambiguity, out string resolveError))
                return new ErrorResponse(resolveError);

            var em = world.EntityManager;
            // Ad-hoc queries from outside a system have no tracked dependencies,
            // so CalculateEntityCount() can't sync jobs automatically — complete them first.
            em.CompleteAllTrackedJobs();

            int pageSize = p.GetInt("page_size") ?? 20;
            pageSize = Math.Clamp(pageSize, 1, 100);

            int totalCount = 0;
            var samples = new List<object>();

            // Every matching registration is queried; the entity sets are disjoint, so counts sum.
            foreach (var signature in signatures)
            {
                using var query = em.CreateEntityQuery(signature);
                totalCount += query.CalculateEntityCount();

                if (samples.Count >= pageSize)
                    continue;

                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length && samples.Count < pageSize; i++)
                {
                    samples.Add(SerializeEntityBrief(em, entities[i]));
                }
                entities.Dispose();
            }

            var payload = new Dictionary<string, object>
            {
                ["total_count"]     = totalCount,
                ["page_size"]       = pageSize,
                ["component_types"] = typeNames,
                ["entities"]        = samples
            };
            if (ambiguity != null)
            {
                payload["ambiguous_type_names"] = ambiguity;
                payload["queried_signature_count"] = signatures.Count;
            }

            string message = $"Found {totalCount} entities matching [{string.Join(", ", typeNames)}].";
            if (ambiguity != null)
                message += $" {ambiguity.Count} name(s) matched multiple registered type indices; all were queried — see ambiguous_type_names.";

            return new SuccessResponse(message, payload);
        }

        private static object GetEntity(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            int? entityIndex = p.GetInt("entity_index");
            int? entityVersion = p.GetInt("entity_version");
            if (entityIndex == null)
                return new ErrorResponse("'entity_index' parameter is required.");

            var entity = new Entity { Index = entityIndex.Value, Version = entityVersion ?? 1 };
            var em = world.EntityManager;

            if (!em.Exists(entity))
                return new ErrorResponse($"Entity (Index={entityIndex}, Version={entityVersion ?? 1}) does not exist.");

            // Optional component filter — only return specified components
            string componentFilter = p.Get("component_types");
            HashSet<string> filterSet = null;
            if (!string.IsNullOrEmpty(componentFilter))
            {
                filterSet = new HashSet<string>(
                    componentFilter.Split(',').Select(s => s.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            }

            return new SuccessResponse($"Entity {entity} details.", SerializeEntityFull(em, entity, filterSet));
        }

        #endregion

        #region System Operations

        private static object ListSystems(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            string groupFilter = p.Get("group");
            var systems = new List<object>();

            // Build ISystem type lookup for group name resolution on unmanaged systems
            var isystemTypes = BuildISystemTypeLookup();

            // Track managed system full names to distinguish managed vs unmanaged
            var managedTypesByName = new Dictionary<string, Type>();
            foreach (var sys in world.Systems)
                managedTypesByName[sys.GetType().FullName] = sys.GetType();

            // Enumerate ALL systems (managed + unmanaged) via WorldUnmanaged.GetAllSystems
            var allHandles = world.Unmanaged.GetAllSystems(Allocator.Temp);
            try
            {
                for (int i = 0; i < allHandles.Length; i++)
                {
                    try
                    {
                        ref var state = ref world.Unmanaged.ResolveSystemStateRef(allHandles[i]);
                        string debugName = state.DebugName.ToString();
                        if (string.IsNullOrEmpty(debugName)) continue;

                        bool isManaged = managedTypesByName.ContainsKey(debugName);
                        string shortName = debugName.Contains('.')
                            ? debugName.Substring(debugName.LastIndexOf('.') + 1)
                            : debugName;

                        // Resolve Type for [UpdateInGroup] attribute
                        Type sysType = null;
                        if (isManaged)
                            managedTypesByName.TryGetValue(debugName, out sysType);
                        else
                            isystemTypes.TryGetValue(shortName, out sysType);

                        string groupName = sysType != null ? GetSystemGroupName(sysType) : "Unknown";

                        if (!string.IsNullOrEmpty(groupFilter) &&
                            !groupName.Contains(groupFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        systems.Add(new Dictionary<string, object>
                        {
                            ["name"]    = shortName,
                            ["type"]    = debugName,
                            ["group"]   = groupName,
                            ["enabled"] = state.Enabled,
                            ["kind"]    = isManaged ? "managed" : "unmanaged"
                        });
                    }
                    catch { continue; }
                }
            }
            finally
            {
                allHandles.Dispose();
            }

            return new SuccessResponse($"Found {systems.Count} system(s) in '{world.Name}'.", systems);
        }

        private static object GetSystem(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            string systemName = p.Get("system_name");
            if (string.IsNullOrEmpty(systemName))
                return new ErrorResponse("'system_name' parameter is required.");

            // Try managed system first
            ComponentSystemBase managedSystem = FindSystem(world, systemName);
            if (managedSystem != null)
            {
                var sysType = managedSystem.GetType();
                var queries = new List<object>();
                foreach (var q in managedSystem.EntityQueries)
                {
                    queries.Add(new Dictionary<string, object>
                    {
                        ["entity_count"]    = q.CalculateEntityCount(),
                        ["component_types"] = GetQueryComponentTypeNames(q)
                    });
                }

                var updateBefore = sysType.GetCustomAttributes(typeof(UpdateBeforeAttribute), true)
                    .Cast<UpdateBeforeAttribute>()
                    .Select(a => a.SystemType.Name)
                    .ToList();
                var updateAfter = sysType.GetCustomAttributes(typeof(UpdateAfterAttribute), true)
                    .Cast<UpdateAfterAttribute>()
                    .Select(a => a.SystemType.Name)
                    .ToList();

                bool isGroup = typeof(ComponentSystemGroup).IsAssignableFrom(sysType);

                return new SuccessResponse($"System '{systemName}' details.", new Dictionary<string, object>
                {
                    ["name"]           = sysType.Name,
                    ["full_name"]      = sysType.FullName,
                    ["group"]          = GetSystemGroupName(sysType),
                    ["enabled"]        = managedSystem.Enabled,
                    ["is_group"]       = isGroup,
                    ["kind"]           = "managed",
                    ["update_before"]  = updateBefore,
                    ["update_after"]   = updateAfter,
                    ["query_count"]    = managedSystem.EntityQueries.Length,
                    ["queries"]        = queries
                });
            }

            // Try unmanaged ISystem struct
            var unmanagedResult = FindUnmanagedSystem(world, systemName);
            if (unmanagedResult.HasValue)
            {
                var (handle, resolvedType) = unmanagedResult.Value;
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
                string debugName = state.DebugName.ToString();

                var result = new Dictionary<string, object>
                {
                    ["name"]      = resolvedType?.Name ?? debugName,
                    ["full_name"] = resolvedType?.FullName ?? debugName,
                    ["group"]     = resolvedType != null ? GetSystemGroupName(resolvedType) : "Unknown",
                    ["enabled"]   = state.Enabled,
                    ["is_group"]  = false,
                    ["kind"]      = "unmanaged"
                };

                if (resolvedType != null)
                {
                    result["update_before"] = resolvedType
                        .GetCustomAttributes(typeof(UpdateBeforeAttribute), true)
                        .Cast<UpdateBeforeAttribute>()
                        .Select(a => a.SystemType.Name)
                        .ToList();
                    result["update_after"] = resolvedType
                        .GetCustomAttributes(typeof(UpdateAfterAttribute), true)
                        .Cast<UpdateAfterAttribute>()
                        .Select(a => a.SystemType.Name)
                        .ToList();
                }

                return new SuccessResponse($"System '{systemName}' details.", result);
            }

            return new ErrorResponse($"System '{systemName}' not found in world '{world.Name}'.");
        }

        private static object ToggleSystem(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            string systemName = p.Get("system_name");
            if (string.IsNullOrEmpty(systemName))
                return new ErrorResponse("'system_name' parameter is required.");

            if (!p.Has("enabled"))
                return new ErrorResponse("'enabled' parameter is required (true/false).");
            bool enabled = p.GetBool("enabled");

            // Try managed system first
            ComponentSystemBase managedSystem = FindSystem(world, systemName);
            if (managedSystem != null)
            {
                managedSystem.Enabled = enabled;
                return new SuccessResponse(
                    $"System '{systemName}' (managed) {(enabled ? "enabled" : "disabled")} in world '{world.Name}'.");
            }

            // Try unmanaged ISystem struct
            var unmanagedResult = FindUnmanagedSystem(world, systemName);
            if (unmanagedResult.HasValue)
            {
                ref var state = ref world.Unmanaged.ResolveSystemStateRef(unmanagedResult.Value.Handle);
                state.Enabled = enabled;
                return new SuccessResponse(
                    $"System '{systemName}' (unmanaged) {(enabled ? "enabled" : "disabled")} in world '{world.Name}'.");
            }

            return new ErrorResponse($"System '{systemName}' not found in world '{world.Name}'.");
        }

        #endregion

        #region Component Type Discovery

        private static object ListComponentTypes(ToolParams p)
        {
            string filter = p.Get("filter");
            string categoryFilter = p.Get("category"); // ComponentData, BufferData, SharedComponentData, etc.
            int pageSize = p.GetInt("page_size") ?? 50;
            pageSize = Math.Clamp(pageSize, 1, 200);

            int typeCount = TypeManager.GetTypeCount();
            var types = new List<object>();

            for (int i = 1; i < typeCount; i++)
            {
                var typeInfo = TypeManager.GetTypeInfo(i);
                string debugName = typeInfo.DebugTypeName.ToString();

                if (string.IsNullOrEmpty(debugName) || debugName == "null")
                    continue;

                // Apply name filter
                if (!string.IsNullOrEmpty(filter) &&
                    !debugName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Apply category filter
                string category = typeInfo.Category.ToString();
                if (!string.IsNullOrEmpty(categoryFilter) &&
                    !category.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                types.Add(new Dictionary<string, object>
                {
                    ["name"]          = debugName,
                    ["category"]      = category,
                    ["type_index"]    = typeInfo.TypeIndex.Value,
                    ["size_bytes"]    = typeInfo.SizeInChunk,
                    ["is_zero_sized"] = typeInfo.IsZeroSized,
                    ["is_buffer"]     = typeInfo.Category == TypeManager.TypeCategory.BufferData,
                    ["is_shared"]     = typeInfo.Category == TypeManager.TypeCategory.ISharedComponentData,
                    ["is_enableable"] = typeInfo.EnableableType
                });

                if (types.Count >= pageSize)
                    break;
            }

            return new SuccessResponse(
                $"Found {types.Count} component type(s) (of {typeCount - 1} total).", new Dictionary<string, object>
                {
                    ["total_registered"] = typeCount - 1,
                    ["returned"]         = types.Count,
                    ["types"]            = types
                });
        }

        #endregion

        #region Entity CRUD

        private static object CreateEntity(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            string componentTypesStr = p.Get("component_types");
            var em = world.EntityManager;
            Entity entity;

            if (!string.IsNullOrEmpty(componentTypesStr))
            {
                string[] typeNames = componentTypesStr.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();

                var componentTypes = new List<ComponentType>();
                foreach (string typeName in typeNames)
                {
                    var resolvedType = ResolveComponentType(typeName);
                    if (resolvedType == null)
                        return new ErrorResponse($"Component type '{typeName}' not found.");
                    componentTypes.Add(resolvedType.Value);
                }

                var archetype = em.CreateArchetype(componentTypes.ToArray());
                entity = em.CreateEntity(archetype);
            }
            else
            {
                entity = em.CreateEntity();
            }

            return new SuccessResponse(
                $"Created entity (Index={entity.Index}, Version={entity.Version}) in world '{world.Name}'.",
                SerializeEntityBrief(em, entity));
        }

        private static object DestroyEntity(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            int? entityIndex = p.GetInt("entity_index");
            int? entityVersion = p.GetInt("entity_version");
            if (entityIndex == null)
                return new ErrorResponse("'entity_index' parameter is required.");

            var entity = new Entity { Index = entityIndex.Value, Version = entityVersion ?? 1 };
            var em = world.EntityManager;

            if (!em.Exists(entity))
                return new ErrorResponse($"Entity (Index={entityIndex}, Version={entityVersion ?? 1}) does not exist.");

            em.DestroyEntity(entity);
            return new SuccessResponse($"Destroyed entity (Index={entityIndex}, Version={entityVersion ?? 1}) in world '{world.Name}'.");
        }

        private static object SetComponent(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found.");

            int? entityIndex = p.GetInt("entity_index");
            int? entityVersion = p.GetInt("entity_version");
            if (entityIndex == null)
                return new ErrorResponse("'entity_index' parameter is required.");

            string componentName = p.Get("component_name");
            if (string.IsNullOrEmpty(componentName))
                return new ErrorResponse("'component_name' parameter is required.");

            string fieldName = p.Get("field_name");
            if (string.IsNullOrEmpty(fieldName))
                return new ErrorResponse("'field_name' parameter is required.");

            string fieldValue = p.Get("field_value");
            if (fieldValue == null)
                return new ErrorResponse("'field_value' parameter is required.");

            var entity = new Entity { Index = entityIndex.Value, Version = entityVersion ?? 1 };
            var em = world.EntityManager;

            if (!em.Exists(entity))
                return new ErrorResponse($"Entity (Index={entityIndex}, Version={entityVersion ?? 1}) does not exist.");

            var ct = ResolveComponentType(componentName);
            if (ct == null)
                return new ErrorResponse($"Component type '{componentName}' not found.");

            if (!em.HasComponent(entity, ct.Value))
                return new ErrorResponse($"Entity does not have component '{componentName}'.");

            try
            {
                var type = ct.Value.GetManagedType();
                if (type == null)
                    return new ErrorResponse($"Cannot resolve managed type for '{componentName}'.");

                var obj = em.Debug.GetComponentBoxed(entity, ct.Value);
                if (obj == null)
                    return new ErrorResponse($"Cannot read component '{componentName}'.");

                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                    return new ErrorResponse($"Field '{fieldName}' not found on component '{componentName}'.");

                // Parse the value to the correct type
                object parsedValue = field.FieldType.IsEnum
                    ? ParseEnumFieldValue(field.FieldType, fieldValue)
                    : Convert.ChangeType(fieldValue, field.FieldType, System.Globalization.CultureInfo.InvariantCulture);
                field.SetValue(obj, parsedValue);

                if (!ct.Value.IsManagedComponent)
                {
                    // Unmanaged IComponentData (struct) — SetComponentObject below asserts
                    // componentType.IsManagedComponent internally (EntityDataAccess.SetComponentObject)
                    // and throws ArgumentException for anything else. Write back through the
                    // generic unmanaged EntityManager.SetComponentData<T> instead.
                    var setDataMethod = typeof(EntityManager)
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "SetComponentData"
                            && m.IsGenericMethodDefinition
                            && m.GetGenericArguments().Length == 1
                            && m.GetParameters().Length == 2
                            && m.GetParameters()[0].ParameterType == typeof(Entity));
                    if (setDataMethod == null)
                        return new ErrorResponse("SetComponentData is not available in this version of Unity Entities.");

                    setDataMethod.MakeGenericMethod(type).Invoke(em, new object[] { entity, obj });
                }
                else
                {
                    // Managed component (class-based IComponentData) — SetComponentBoxed is not
                    // available in the public API, so use SetComponentObject via reflection.
                    var setObjectMethod = typeof(EntityManager).GetMethod("SetComponentObject",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(Entity), typeof(ComponentType), typeof(object) }, null);
                    if (setObjectMethod != null)
                    {
                        setObjectMethod.Invoke(em, new object[] { entity, ct.Value, obj });
                    }
                    else
                    {
                        return new ErrorResponse("SetComponent is not supported in this version of Unity Entities.");
                    }
                }

                return new SuccessResponse(
                    $"Set {componentName}.{fieldName} = {fieldValue} on entity (Index={entityIndex}, Version={entityVersion ?? 1}).",
                    new Dictionary<string, object>
                    {
                        ["entity_index"] = entityIndex.Value,
                        ["component"]    = componentName,
                        ["field"]        = fieldName,
                        ["value"]        = fieldValue
                    });
            }
            catch (Exception e)
            {
                // Reflected MethodInfo.Invoke wraps the real failure in a TargetInvocationException;
                // surface the InnerException so the caller sees the actual cause, not the wrapper.
                var reported = e;
                if (e is TargetInvocationException tie && tie.InnerException != null)
                    reported = tie.InnerException;
                return new ErrorResponse($"Failed to set field: {reported.Message}");
            }
        }

        /// <summary>
        /// Parses a string value into an enum member of <paramref name="enumType"/>.
        /// Accepts a numeric literal (honoring the enum's underlying integral type) or a
        /// member name (case-insensitive, including comma-separated flag combinations).
        /// </summary>
        private static object ParseEnumFieldValue(Type enumType, string fieldValue)
        {
            var underlyingType = Enum.GetUnderlyingType(enumType);
            if (long.TryParse(fieldValue, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var numeric))
            {
                var converted = Convert.ChangeType(numeric, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
                return Enum.ToObject(enumType, converted);
            }

            try
            {
                return Enum.Parse(enumType, fieldValue, ignoreCase: true);
            }
            catch (ArgumentException)
            {
                var names = string.Join(", ", Enum.GetNames(enumType));
                throw new ArgumentException(
                    $"Value '{fieldValue}' is not a valid member of enum '{enumType.Name}'. Valid values: {names}");
            }
        }

        private static object AddComponent(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found.");

            int? entityIndex = p.GetInt("entity_index");
            int? entityVersion = p.GetInt("entity_version");
            if (entityIndex == null)
                return new ErrorResponse("'entity_index' parameter is required.");

            string componentName = p.Get("component_name");
            if (string.IsNullOrEmpty(componentName))
                return new ErrorResponse("'component_name' parameter is required.");

            var entity = new Entity { Index = entityIndex.Value, Version = entityVersion ?? 1 };
            var em = world.EntityManager;

            if (!em.Exists(entity))
                return new ErrorResponse($"Entity (Index={entityIndex}, Version={entityVersion ?? 1}) does not exist.");

            var ct = ResolveComponentType(componentName);
            if (ct == null)
                return new ErrorResponse($"Component type '{componentName}' not found.");

            if (em.HasComponent(entity, ct.Value))
                return new ErrorResponse($"Entity already has component '{componentName}'.");

            em.AddComponent(entity, ct.Value);
            return new SuccessResponse(
                $"Added '{componentName}' to entity (Index={entityIndex}, Version={entityVersion ?? 1}).",
                SerializeEntityBrief(em, entity));
        }

        private static object RemoveComponent(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found.");

            int? entityIndex = p.GetInt("entity_index");
            int? entityVersion = p.GetInt("entity_version");
            if (entityIndex == null)
                return new ErrorResponse("'entity_index' parameter is required.");

            string componentName = p.Get("component_name");
            if (string.IsNullOrEmpty(componentName))
                return new ErrorResponse("'component_name' parameter is required.");

            var entity = new Entity { Index = entityIndex.Value, Version = entityVersion ?? 1 };
            var em = world.EntityManager;

            if (!em.Exists(entity))
                return new ErrorResponse($"Entity (Index={entityIndex}, Version={entityVersion ?? 1}) does not exist.");

            var ct = ResolveComponentType(componentName);
            if (ct == null)
                return new ErrorResponse($"Component type '{componentName}' not found.");

            if (!em.HasComponent(entity, ct.Value))
                return new ErrorResponse($"Entity does not have component '{componentName}'.");

            em.RemoveComponent(entity, ct.Value);
            return new SuccessResponse(
                $"Removed '{componentName}' from entity (Index={entityIndex}, Version={entityVersion ?? 1}).",
                SerializeEntityBrief(em, entity));
        }

        private static object QueryCount(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found.");

            string componentTypesStr = p.Get("component_types");
            if (string.IsNullOrEmpty(componentTypesStr))
                return new ErrorResponse("'component_types' parameter is required.");

            string[] typeNames = componentTypesStr.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();

            if (!TryBuildQuerySignatures(typeNames, out var signatures, out var ambiguity, out string resolveError))
                return new ErrorResponse(resolveError);

            var em = world.EntityManager;
            em.CompleteAllTrackedJobs();

            int count = 0;
            foreach (var signature in signatures)
            {
                using var query = em.CreateEntityQuery(signature);
                count += query.CalculateEntityCount();
            }

            var payload = new Dictionary<string, object>
            {
                ["count"]           = count,
                ["component_types"] = typeNames,
                ["world"]           = world.Name
            };
            if (ambiguity != null)
            {
                payload["ambiguous_type_names"] = ambiguity;
                payload["queried_signature_count"] = signatures.Count;
            }

            string message = $"{count} entities match [{string.Join(", ", typeNames)}] in world '{world.Name}'.";
            if (ambiguity != null)
                message += $" {ambiguity.Count} name(s) matched multiple registered type indices; all were queried — see ambiguous_type_names.";

            return new SuccessResponse(message, payload);
        }

        #endregion

        #region Performance

        private static object PerformanceSnapshot(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            var em = world.EntityManager;

            // Archetype stats
            var archetypes = new NativeList<EntityArchetype>(Allocator.Temp);
            em.GetAllArchetypes(archetypes);
            int totalChunks = 0;
            int totalEntities = 0;
            int emptyChunks = 0;
            var archetypeStats = new List<object>();

            for (int i = 0; i < archetypes.Length; i++)
            {
                var archetype = archetypes[i];
                int chunkCount = archetype.ChunkCount;
                int chunkCapacity = archetype.ChunkCapacity;

                // Use a query to count entities for this archetype
                int entityCount = 0;
                if (chunkCount > 0 && chunkCapacity > 0)
                {
                    // Estimate: chunkCount * average fill. For exact count, use query.
                    // ChunkCapacity is per-chunk max; actual count needs CalculateEntityCount.
                    // For perf snapshot, we use the universal query total and archetype breakdown.
                    entityCount = chunkCount > 0 ? EstimateArchetypeEntityCount(archetype) : 0;
                }

                int capacity = chunkCapacity * chunkCount;
                float utilization = capacity > 0 ? (float)entityCount / capacity * 100f : 0f;

                totalChunks += chunkCount;
                totalEntities += entityCount;
                if (entityCount == 0 && chunkCount > 0) emptyChunks += chunkCount;

                if (chunkCount > 0)
                {
                    var componentNames = new List<string>();
                    var types = archetype.GetComponentTypes(Allocator.Temp);
                    for (int t = 0; t < types.Length; t++)
                    {
                        var info = TypeManager.GetTypeInfo(types[t].TypeIndex);
                        componentNames.Add(info.DebugTypeName.ToString());
                    }
                    types.Dispose();

                    archetypeStats.Add(new Dictionary<string, object>
                    {
                        ["components"]      = componentNames,
                        ["chunk_count"]     = chunkCount,
                        ["entity_count"]    = entityCount,
                        ["chunk_capacity"]  = chunkCapacity,
                        ["utilization_pct"] = Math.Round(utilization, 1)
                    });
                }
            }

            // Sort by entity count descending
            archetypeStats.Sort((a, b) =>
            {
                var aCount = (int)((Dictionary<string, object>)a)["entity_count"];
                var bCount = (int)((Dictionary<string, object>)b)["entity_count"];
                return bCount.CompareTo(aCount);
            });

            int limit = p.GetInt("limit") ?? 20;
            int totalArchetypes = archetypes.Length;
            archetypes.Dispose();

            if (archetypeStats.Count > limit)
                archetypeStats = archetypeStats.Take(limit).ToList();

            return new SuccessResponse($"Performance snapshot for world '{world.Name}'.", new Dictionary<string, object>
            {
                ["world"]               = world.Name,
                ["total_entities"]      = world.EntityManager.UniversalQuery.CalculateEntityCount(),
                ["total_archetypes"]    = totalArchetypes,
                ["total_chunks"]        = totalChunks,
                ["empty_chunks"]        = emptyChunks,
                ["system_count"]        = world.Systems.Count,
                ["top_archetypes"]      = archetypeStats
            });
        }

        /// <summary>
        /// Estimates entity count for an archetype using unsafe access.
        /// Falls back to chunk_count * chunk_capacity as upper bound.
        /// </summary>
        private static int EstimateArchetypeEntityCount(EntityArchetype archetype)
        {
            // EntityArchetype doesn't expose EntityCount directly in public API.
            // Use reflection to access internal Archetype->EntityCount if available,
            // otherwise return capacity as upper bound estimate.
            try
            {
                // Try the internal StableHash-based approach
                var archetypeField = typeof(EntityArchetype).GetField("Archetype",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (archetypeField != null)
                {
                    // Box the struct to access the field
                    object boxed = archetype;
                    var ptr = archetypeField.GetValue(boxed);
                    if (ptr != null)
                    {
                        // Archetype* has EntityCount property
                        var entityCountProp = ptr.GetType().GetProperty("EntityCount");
                        if (entityCountProp != null)
                            return (int)entityCountProp.GetValue(ptr);
                    }
                }
            }
            catch
            {
                // Reflection failed, fall back to estimate
            }

            // Upper bound: all chunks fully utilized
            return archetype.ChunkCapacity * archetype.ChunkCount;
        }

        #endregion

        #region Helpers

        private static World ResolveWorld(ToolParams p)
        {
            string worldName = p.Get("world");
            if (string.IsNullOrEmpty(worldName))
                return World.DefaultGameObjectInjectionWorld;

            foreach (var w in World.All)
            {
                if (string.Equals(w.Name, worldName, StringComparison.OrdinalIgnoreCase))
                    return w;
            }
            return null;
        }

        /// <summary>
        /// Resolves a component type name to ALL matching registered ComponentTypes.
        /// Supports short names ("LocalTransform") and full names ("Unity.Transforms.LocalTransform").
        ///
        /// A name can match more than one registration: TypeManager can hold several entries for
        /// what looks like one type (duplicate registrations across assemblies), and a short name
        /// can match several distinct namespaced types. Returning only the first match binds
        /// whichever registration TypeManager happens to enumerate first — which need not be the
        /// one the live World's chunks use — and a query against the wrong one silently reports
        /// 0 entities for a perfectly healthy world (issue #80).
        ///
        /// An exact full-name match, where one exists, wins over suffix matches, so
        /// "Unity.Transforms.LocalTransform" is never widened by an unrelated "…Foo.LocalTransform".
        /// </summary>
        private static List<ComponentType> ResolveComponentTypes(string typeName)
        {
            var exact = new List<ComponentType>();
            var suffix = new List<ComponentType>();
            var seen = new HashSet<int>();

            int typeCount = TypeManager.GetTypeCount();
            for (int i = 1; i < typeCount; i++) // Start at 1; index 0 is Entity itself
            {
                var typeInfo = TypeManager.GetTypeInfo(i);
                string debugName = typeInfo.DebugTypeName.ToString();

                bool isExact = string.Equals(debugName, typeName, StringComparison.OrdinalIgnoreCase);
                bool isSuffix = !isExact
                    && debugName.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase);

                if (!isExact && !isSuffix)
                    continue;

                // Must use typeInfo.TypeIndex (includes type flags) not the raw loop index
                var ct = ComponentType.FromTypeIndex(typeInfo.TypeIndex);
                if (!seen.Add(ct.TypeIndex.Value))
                    continue;

                (isExact ? exact : suffix).Add(ct);
            }

            return exact.Count > 0 ? exact : suffix;
        }

        /// <summary>
        /// Single-registration resolution, for call sites that act on one component (add, remove,
        /// set, create-archetype) where every matching registration denotes the same struct.
        /// Prefers an exact full-name match; see <see cref="ResolveComponentTypes"/>.
        /// </summary>
        private static ComponentType? ResolveComponentType(string typeName)
        {
            var matches = ResolveComponentTypes(typeName);
            return matches.Count > 0 ? matches[0] : (ComponentType?)null;
        }

        /// <summary>
        /// Resolves every requested component name, then expands the per-name candidate lists into
        /// the set of concrete query signatures that must be run.
        ///
        /// A component appears in an entity's archetype under exactly one type index, so the
        /// entity sets produced by two different signatures are disjoint — counts can be summed
        /// and entity arrays concatenated without deduplication.
        /// </summary>
        private static bool TryBuildQuerySignatures(
            string[] typeNames,
            out List<ComponentType[]> signatures,
            out Dictionary<string, object> ambiguity,
            out string error)
        {
            signatures = null;
            ambiguity = null;
            error = null;

            var perName = new List<List<ComponentType>>();
            foreach (string typeName in typeNames)
            {
                var matches = ResolveComponentTypes(typeName);
                if (matches.Count == 0)
                {
                    error = $"Component type '{typeName}' not found. Check spelling or ensure the assembly is loaded.";
                    return false;
                }

                if (matches.Count > 1)
                {
                    ambiguity ??= new Dictionary<string, object>();
                    ambiguity[typeName] = matches.Select(m => m.TypeIndex.Value).ToArray();
                }

                perName.Add(matches);
            }

            // Cartesian product across the per-name candidate lists. Bounded, so a pathological
            // registration set cannot turn one query into thousands.
            const int maxSignatures = 64;
            long projected = 1;
            foreach (var candidates in perName)
            {
                projected *= candidates.Count;
                if (projected > maxSignatures)
                {
                    error = $"Component names [{string.Join(", ", typeNames)}] resolve to more than "
                          + $"{maxSignatures} distinct type-index combinations. Disambiguate with "
                          + "fully-qualified type names, or inspect a known entity with get_entity.";
                    return false;
                }
            }

            signatures = new List<ComponentType[]> { new ComponentType[typeNames.Length] };
            for (int i = 0; i < perName.Count; i++)
            {
                var expanded = new List<ComponentType[]>(signatures.Count * perName[i].Count);
                foreach (var partial in signatures)
                {
                    foreach (var candidate in perName[i])
                    {
                        var next = (ComponentType[])partial.Clone();
                        next[i] = candidate;
                        expanded.Add(next);
                    }
                }
                signatures = expanded;
            }

            return true;
        }

        private static ComponentSystemBase FindSystem(World world, string systemName)
        {
            foreach (var sys in world.Systems)
            {
                var sysType = sys.GetType();
                if (sysType.Name == systemName || sysType.FullName == systemName)
                    return sys;
            }
            return null;
        }

        /// <summary>
        /// Finds an unmanaged ISystem struct in the world by name.
        /// Returns (SystemHandle, resolved Type) or null if not found.
        /// </summary>
        private static (SystemHandle Handle, Type SystemType)? FindUnmanagedSystem(
            World world, string systemName)
        {
            var isystemTypes = BuildISystemTypeLookup();
            var allHandles = world.Unmanaged.GetAllSystems(Allocator.Temp);
            try
            {
                for (int i = 0; i < allHandles.Length; i++)
                {
                    try
                    {
                        ref var state = ref world.Unmanaged.ResolveSystemStateRef(allHandles[i]);
                        string debugName = state.DebugName.ToString();
                        string shortName = debugName.Contains('.')
                            ? debugName.Substring(debugName.LastIndexOf('.') + 1)
                            : debugName;

                        if (shortName == systemName || debugName == systemName)
                        {
                            isystemTypes.TryGetValue(shortName, out var resolvedType);
                            return (allHandles[i], resolvedType);
                        }
                    }
                    catch { continue; }
                }
            }
            finally
            {
                allHandles.Dispose();
            }
            return null;
        }

        /// <summary>
        /// Scans loaded assemblies for ISystem struct types.
        /// Returns a dictionary of short type name → Type.
        /// </summary>
        private static Dictionary<string, Type> BuildISystemTypeLookup()
        {
            var lookup = new Dictionary<string, Type>();
            var isystemInterface = typeof(ISystem);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t.IsValueType && !t.IsAbstract && isystemInterface.IsAssignableFrom(t))
                        lookup[t.Name] = t;
                }
            }
            return lookup;
        }

        private static Dictionary<string, object> SerializeEntityBrief(EntityManager em, Entity entity)
        {
            var componentTypes = em.GetComponentTypes(entity, Allocator.Temp);
            var names = new List<string>();
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var info = TypeManager.GetTypeInfo(componentTypes[i].TypeIndex);
                names.Add(info.DebugTypeName.ToString());
            }
            componentTypes.Dispose();

            return new Dictionary<string, object>
            {
                ["index"]      = entity.Index,
                ["version"]    = entity.Version,
                ["components"] = names
            };
        }

        private static Dictionary<string, object> SerializeEntityFull(EntityManager em, Entity entity, HashSet<string> componentFilter = null)
        {
            var componentTypes = em.GetComponentTypes(entity, Allocator.Temp);
            var components = new List<object>();

            for (int i = 0; i < componentTypes.Length; i++)
            {
                var typeInfo = TypeManager.GetTypeInfo(componentTypes[i].TypeIndex);
                string typeName = typeInfo.DebugTypeName.ToString();

                // Apply component filter if specified
                if (componentFilter != null && componentFilter.Count > 0)
                {
                    string shortName = typeName.Contains('.')
                        ? typeName.Substring(typeName.LastIndexOf('.') + 1)
                        : typeName;
                    if (!componentFilter.Contains(shortName) && !componentFilter.Contains(typeName))
                        continue;
                }
                var componentData = new Dictionary<string, object>
                {
                    ["name"]          = typeName,
                    ["category"]      = typeInfo.Category.ToString(),
                    // SizeInChunk is structurally 0 for ISharedComponentData — shared components
                    // live outside the chunk — so reporting it as a byte size reads as "empty"
                    // for a perfectly healthy component. Say so explicitly instead (issue #80).
                    ["size_bytes"]    = typeInfo.Category == TypeManager.TypeCategory.ISharedComponentData
                                            ? (object)"n/a (shared component, stored outside the chunk)"
                                            : typeInfo.SizeInChunk,
                    ["is_zero_sized"] = typeInfo.IsZeroSized
                };

                // Check enableable component state
                if (typeInfo.EnableableType)
                {
                    try
                    {
                        componentData["is_enabled"] = em.IsComponentEnabled(entity, componentTypes[i]);
                    }
                    catch
                    {
                        componentData["is_enabled"] = "<unknown>";
                    }
                }

                // Read field values for IComponentData
                if (!typeInfo.IsZeroSized && typeInfo.Category == TypeManager.TypeCategory.ComponentData)
                {
                    ReadComponentFields(em, entity, componentTypes[i], componentData);
                }
                // Read shared component data
                else if (typeInfo.Category == TypeManager.TypeCategory.ISharedComponentData)
                {
                    ReadSharedComponentFields(em, entity, componentTypes[i], componentData);
                }
                // Read buffer element data
                else if (typeInfo.Category == TypeManager.TypeCategory.BufferData)
                {
                    ReadBufferElements(em, entity, componentTypes[i], typeInfo, componentData);
                }

                components.Add(componentData);
            }
            componentTypes.Dispose();

            return new Dictionary<string, object>
            {
                ["index"]           = entity.Index,
                ["version"]         = entity.Version,
                ["component_count"] = components.Count,
                ["components"]      = components
            };
        }

        private static void ReadComponentFields(EntityManager em, Entity entity, ComponentType ct, Dictionary<string, object> data)
        {
            ReadMembersInto(em, entity, ct, data, includeProperties: false);
        }

        private static void ReadSharedComponentFields(EntityManager em, Entity entity, ComponentType ct, Dictionary<string, object> data)
        {
            // Shared components routinely expose their contents as PROPERTIES over private
            // fields — RenderMeshArray's MaterialReferences / MeshReferences / Materials /
            // Meshes are the canonical case. A field-only enumeration yields nothing and
            // serializes as {}, which is indistinguishable from a genuinely empty component
            // (issue #80). Read properties as well.
            ReadMembersInto(em, entity, ct, data, includeProperties: true);
        }

        /// <summary>
        /// Boxes a component and reflects its public fields (and optionally its public
        /// properties) into data["fields"]. A failure is always reported explicitly — an empty
        /// or missing "fields" entry must never be the only signal that a read failed.
        /// </summary>
        private static void ReadMembersInto(
            EntityManager em, Entity entity, ComponentType ct,
            Dictionary<string, object> data, bool includeProperties)
        {
            try
            {
                var type = ct.GetManagedType();
                if (type == null)
                {
                    data["fields"] = "<unreadable: no managed type for this component>";
                    return;
                }

                var obj = em.Debug.GetComponentBoxed(entity, ct);
                if (obj == null)
                {
                    data["fields"] = "<unreadable: component boxed to null>";
                    return;
                }

                var members = new Dictionary<string, object>();

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        members[field.Name] = DescribeMemberValue(field.GetValue(obj));
                    }
                    catch (Exception ex)
                    {
                        members[field.Name] = $"<unreadable: {ex.Message}>";
                    }
                }

                if (includeProperties)
                {
                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        // Indexers need arguments; a write-only property has nothing to read.
                        if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                            continue;
                        if (members.ContainsKey(prop.Name))
                            continue;

                        try
                        {
                            members[prop.Name] = DescribeMemberValue(prop.GetValue(obj));
                        }
                        catch (Exception ex)
                        {
                            members[prop.Name] = $"<unreadable: {ex.Message}>";
                        }
                    }
                }

                data["fields"] = members;
            }
            catch (Exception ex)
            {
                // Previously swallowed silently, so a genuine read failure was reported as an
                // empty component. Always leave a marker instead (issue #80).
                data["fields"] = $"<unreadable: {ex.Message}>";
            }
        }

        /// <summary>
        /// Renders a reflected member value. Collections report their length and element
        /// identities rather than a bare type name, so an array-backed member (e.g.
        /// RenderMeshArray's material/mesh references) is legible instead of opaque.
        /// </summary>
        private static object DescribeMemberValue(object value)
        {
            const int maxListed = 8;

            if (value == null) return "null";
            if (value is string str) return str;

            if (value is System.Collections.IEnumerable enumerable)
            {
                var names = new List<string>();
                int count = 0;
                foreach (var item in enumerable)
                {
                    count++;
                    if (names.Count < maxListed)
                    {
                        var asObject = item as UnityEngine.Object;
                        names.Add(asObject != null ? asObject.name : (item?.ToString() ?? "null"));
                    }
                }

                string listed = string.Join(", ", names);
                if (count > names.Count)
                    listed += $", ... (+{count - names.Count} more)";
                return $"[{count}] {listed}";
            }

            return value.ToString();
        }

        private static void ReadBufferElements(EntityManager em, Entity entity, ComponentType ct, TypeManager.TypeInfo typeInfo, Dictionary<string, object> data)
        {
            try
            {
                var type = ct.GetManagedType();
                if (type == null) return;

                // Use reflection to call EntityManager.GetBuffer<T>(entity)
                var getBufferMethod = typeof(EntityManager).GetMethod("GetBuffer",
                    new[] { typeof(Entity), typeof(bool) });
                if (getBufferMethod == null) return;

                var genericMethod = getBufferMethod.MakeGenericMethod(type);
                var buffer = genericMethod.Invoke(em, new object[] { entity, true }); // readOnly=true
                if (buffer == null) return;

                // Get Length property
                var lengthProp = buffer.GetType().GetProperty("Length");
                int length = lengthProp != null ? (int)lengthProp.GetValue(buffer) : 0;
                data["buffer_length"] = length;

                // Read up to 10 elements
                int sampleCount = Math.Min(length, 10);
                var elements = new List<object>();
                var indexer = buffer.GetType().GetProperty("Item");
                if (indexer != null)
                {
                    for (int e = 0; e < sampleCount; e++)
                    {
                        try
                        {
                            var elem = indexer.GetValue(buffer, new object[] { e });
                            var fields = new Dictionary<string, object>();
                            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                            {
                                try
                                {
                                    fields[field.Name] = field.GetValue(elem)?.ToString() ?? "null";
                                }
                                catch
                                {
                                    fields[field.Name] = "<unreadable>";
                                }
                            }
                            elements.Add(fields);
                        }
                        catch { break; }
                    }
                }
                data["elements"] = elements;
            }
            catch
            {
                data["buffer_length"] = "<unreadable>";
            }
        }

        private static string GetSystemGroupName(Type systemType)
        {
            var attrs = systemType.GetCustomAttributes(typeof(UpdateInGroupAttribute), true);
            if (attrs.Length > 0)
            {
                var groupAttr = (UpdateInGroupAttribute)attrs[0];
                return groupAttr.GroupType.Name;
            }
            return "Default";
        }

        private static List<string> GetQueryComponentTypeNames(EntityQuery query)
        {
            var names = new List<string>();
            try
            {
                // GetQueryTypes is internal — use reflection
                var method = typeof(EntityQuery).GetMethod("GetQueryTypes",
                    BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    var types = (ComponentType[])method.Invoke(query, null);
                    foreach (var ct in types)
                    {
                        var info = TypeManager.GetTypeInfo(ct.TypeIndex);
                        names.Add(info.DebugTypeName.ToString());
                    }
                }
                else
                {
                    names.Add("<query types unavailable>");
                }
            }
            catch
            {
                names.Add("<unable to read query types>");
            }
            return names;
        }

        #endregion

        #region BDP Tree Inspection

        private static object InspectBdpTree(ToolParams p)
        {
            var world = ResolveWorld(p);
            if (world == null)
                return new ErrorResponse("World not found. Use list_worlds to see available worlds.");

            int? entityIndex = p.GetInt("entity_index");
            int? entityVersion = p.GetInt("entity_version");
            if (entityIndex == null)
                return new ErrorResponse("'entity_index' parameter is required.");

            var entity = new Entity { Index = entityIndex.Value, Version = entityVersion ?? 1 };
            var em = world.EntityManager;

            if (!em.Exists(entity))
                return new ErrorResponse($"Entity (Index={entityIndex}, Version={entityVersion ?? 1}) does not exist.");

            // Resolve BDP component types dynamically (avoids hard reference to BDP assembly)
            var taskComponentType = ResolveManagedType("TaskComponent");
            var branchComponentType = ResolveManagedType("BranchComponent");
            var evaluateFlagCt = ResolveComponentType("EvaluateFlag");

            if (taskComponentType == null)
                return new ErrorResponse("TaskComponent type not found. Is Behavior Designer Pro installed?");

            var componentTypes = em.GetComponentTypes(entity, Allocator.Temp);
            bool hasTaskBuffer = false;
            bool hasBranchBuffer = false;
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var ti = TypeManager.GetTypeInfo(componentTypes[i].TypeIndex);
                string name = ti.DebugTypeName.ToString();
                string shortName = name.Contains('.') ? name.Substring(name.LastIndexOf('.') + 1) : name;
                if (shortName == "TaskComponent") hasTaskBuffer = true;
                if (shortName == "BranchComponent") hasBranchBuffer = true;
            }
            componentTypes.Dispose();

            if (!hasTaskBuffer)
                return new ErrorResponse($"Entity {entity} does not have a TaskComponent buffer. Not a BDP entity.");

            var result = new Dictionary<string, object>
            {
                ["entity_index"] = entity.Index,
                ["entity_version"] = entity.Version
            };

            // Read EvaluateFlag state
            if (evaluateFlagCt.HasValue)
            {
                try
                {
                    if (em.HasComponent(entity, evaluateFlagCt.Value))
                        result["evaluate_flag_enabled"] = em.IsComponentEnabled(entity, evaluateFlagCt.Value);
                    else
                        result["evaluate_flag_enabled"] = "not_present";
                }
                catch { result["evaluate_flag_enabled"] = "<unreadable>"; }
            }
            else
            {
                result["evaluate_flag_enabled"] = "type_not_registered";
            }

            // Read TaskComponent buffer via reflection
            var tasks = ReadBdpBuffer(em, entity, taskComponentType);
            if (tasks != null)
            {
                result["task_count"] = tasks.Count;
                result["tasks"] = tasks;

                // Find active/running tasks
                var activeTasks = new List<object>();
                foreach (var taskObj in tasks)
                {
                    if (taskObj is not Dictionary<string, object> task) continue;
                    if (task.TryGetValue("fields", out var fieldsObj) && fieldsObj is Dictionary<string, object> fields)
                    {
                        string status = fields.TryGetValue("Status", out var s) ? s?.ToString() : null;
                        if (status != null && status != "Inactive")
                        {
                            var taskSummary = new Dictionary<string, object>
                            {
                                ["index"] = fields.TryGetValue("Index", out var idx) ? idx : "?",
                                ["status"] = status,
                                ["branch_index"] = fields.TryGetValue("BranchIndex", out var bi) ? bi : "?",
                                ["task_name"] = ResolveBdpTaskName(fields)
                            };
                            activeTasks.Add(taskSummary);
                        }
                    }
                }
                result["active_tasks"] = activeTasks;
            }

            // Read BranchComponent buffer via reflection
            if (hasBranchBuffer && branchComponentType != null)
            {
                var branches = ReadBdpBuffer(em, entity, branchComponentType);
                if (branches != null)
                {
                    result["branch_count"] = branches.Count;
                    result["branches"] = branches;
                }
            }

            return new SuccessResponse($"BDP tree state for Entity {entity}.", result);
        }

        private static List<object> ReadBdpBuffer(EntityManager em, Entity entity, Type bufferType)
        {
            try
            {
                // M-1 fix: Find generic GetBuffer<T> by definition to avoid overload fragility
                var getBufferMethod = typeof(EntityManager)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetBuffer"
                                      && m.IsGenericMethodDefinition
                                      && m.GetParameters().Length == 2
                                      && m.GetParameters()[1].ParameterType == typeof(bool));
                if (getBufferMethod == null) return null;

                var genericMethod = getBufferMethod.MakeGenericMethod(bufferType);
                var buffer = genericMethod.Invoke(em, new object[] { entity, true });
                if (buffer == null) return null;

                var lengthProp = buffer.GetType().GetProperty("Length");
                int length = lengthProp != null ? (int)lengthProp.GetValue(buffer) : 0;

                var elements = new List<object>();
                var indexer = buffer.GetType().GetProperty("Item");
                if (indexer == null) return null;

                // Include both public and serialized private fields
                var allFields = bufferType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                for (int e = 0; e < length; e++)
                {
                    try
                    {
                        var elem = indexer.GetValue(buffer, new object[] { e });
                        var fields = new Dictionary<string, object>();
                        foreach (var field in allFields)
                        {
                            // Include public fields and private fields with [SerializeField]
                            if (!field.IsPublic &&
                                !Attribute.IsDefined(field, typeof(UnityEngine.SerializeField)))
                                continue;

                            try
                            {
                                var val = field.GetValue(elem);
                                // Use clean name (strip m_ prefix for private backing fields)
                                string fieldName = field.Name;
                                if (fieldName.StartsWith("m_"))
                                    fieldName = fieldName.Substring(2);

                                if (field.FieldType == typeof(ComponentType))
                                {
                                    var ct = (ComponentType)val;
                                    var managedType = ct.GetManagedType();
                                    fields[fieldName] = managedType != null ? managedType.Name : ct.ToString();
                                }
                                else
                                {
                                    fields[fieldName] = val?.ToString() ?? "null";
                                }
                            }
                            catch { fields[field.Name] = "<unreadable>"; }
                        }
                        elements.Add(new Dictionary<string, object> { ["fields"] = fields });
                    }
                    catch { continue; }
                }
                return elements;
            }
            catch { return null; }
        }

        private static string ResolveBdpTaskName(Dictionary<string, object> taskFields)
        {
            // FlagComponentType contains the task's flag component (e.g., HasEnemyInRangeFlag)
            if (taskFields.TryGetValue("FlagComponentType", out var flagName) && flagName != null)
            {
                string name = flagName.ToString();
                // Clean up common suffixes to get task name
                if (name.EndsWith("Flag")) name = name.Substring(0, name.Length - 4);
                if (name.EndsWith("Tag")) name = name.Substring(0, name.Length - 3);
                if (name.EndsWith("Component")) name = name.Substring(0, name.Length - 9);
                return name;
            }
            return "Unknown";
        }

        /// <summary>
        /// Resolves a managed System.Type by name from all loaded assemblies.
        /// Matches IBufferElementData or IComponentData types. Results are cached.
        /// </summary>
        private static readonly Dictionary<string, Type> s_managedTypeCache = new();

        private static Type ResolveManagedType(string typeName)
        {
            if (s_managedTypeCache.TryGetValue(typeName, out var cached)) return cached;

            Type found = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == typeName &&
                            (typeof(IBufferElementData).IsAssignableFrom(type) ||
                             typeof(IComponentData).IsAssignableFrom(type)))
                        {
                            found = type;
                            break;
                        }
                    }
                }
                catch { continue; }
                if (found != null) break;
            }
            s_managedTypeCache[typeName] = found;
            return found;
        }

        #endregion
    }
}
#endif
