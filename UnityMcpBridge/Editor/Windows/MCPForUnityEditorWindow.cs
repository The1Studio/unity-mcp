using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Data;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Windows
{
    public class MCPForUnityEditorWindow : EditorWindow
    {
        private bool isUnityBridgeRunning = false;
        private Vector2 scrollPosition;
        private string pythonServerInstallationStatus = "Not Installed";
        private Color pythonServerInstallationStatusColor = Color.red;
        private const int mcpPort = 6500; // MCP port (still hardcoded for MCP server)
        private readonly McpClients mcpClients = new();
        private bool autoRegisterEnabled;
        private bool lastClientRegisteredOk;
        private bool lastBridgeVerifiedOk;
        private string pythonDirOverride = null;
        private bool debugLogsEnabled;
        private double lastRepaintTime = 0;
        private int manualPortInput = 0;
        private bool isEditingPort = false;

        // Script validation settings
        private int validationLevelIndex = 1; // Default to Standard
        private readonly string[] validationLevelOptions = new string[]
        {
            "Basic - Only syntax checks",
            "Standard - Syntax + Unity practices",
            "Comprehensive - All checks + semantic analysis",
            "Strict - Full semantic validation (requires Roslyn)"
        };

        // UI state
        private int selectedClientIndex = 0;

        [MenuItem("Window/MCP for Unity")]
        public static void ShowWindow()
        {
            GetWindow<MCPForUnityEditorWindow>("MCP for Unity");
        }

        private void OnEnable()
        {
            UpdatePythonServerInstallationStatus();

            // Refresh bridge status
            isUnityBridgeRunning = MCPForUnityBridge.IsRunning;
            autoRegisterEnabled = EditorPrefs.GetBool("MCPForUnity.AutoRegisterEnabled", true);
            debugLogsEnabled = EditorPrefs.GetBool("MCPForUnity.DebugLogs", false);
            if (debugLogsEnabled)
            {
                LogDebugPrefsState();
            }
            foreach (var mcpClient in mcpClients.clients)
            {
                CheckMcpConfiguration(mcpClient);
            }

            // Load validation level setting
            LoadValidationLevelSetting();

            // First-run auto-setup only if Claude CLI is available
            if (autoRegisterEnabled && !string.IsNullOrEmpty(ExecPath.ResolveClaude()))
            {
                AutoFirstRunSetup();
            }
        }

        private void OnFocus()
        {
            // Refresh bridge running state on focus in case initialization completed after domain reload
            isUnityBridgeRunning = MCPForUnityBridge.IsRunning;
            if (mcpClients.clients.Count > 0 && selectedClientIndex < mcpClients.clients.Count)
            {
                var selectedClient = mcpClients.clients[selectedClientIndex];
                CheckMcpConfiguration(selectedClient);
            }
            Repaint();
        }

        private Color GetStatusColor(McpStatus status)
        {
            // Return appropriate color based on the status enum
            return status switch
            {
                McpStatus.Configured => Color.green,
                McpStatus.Running => Color.green,
                McpStatus.Connected => Color.green,
                McpStatus.IncorrectPath => Color.yellow,
                McpStatus.CommunicationError => Color.yellow,
                McpStatus.NoResponse => Color.yellow,
                _ => Color.red, // Default to red for error states or not configured
            };
        }

        private void UpdatePythonServerInstallationStatus()
        {
            try
            {
                var installedPath = ServerInstaller.GetServerPath();
                var installedOk = !string.IsNullOrEmpty(installedPath) && File.Exists(Path.Combine(installedPath, "server.py"));
                if (installedOk)
                {
                    pythonServerInstallationStatus = "Installed";
                    pythonServerInstallationStatusColor = Color.green;
                    return;
                }

                // Fall back to embedded/dev source via our existing resolution logic
                var embeddedPath = FindPackagePythonDirectory();
                var embeddedOk = !string.IsNullOrEmpty(embeddedPath) && File.Exists(Path.Combine(embeddedPath, "server.py"));
                if (embeddedOk)
                {
                    pythonServerInstallationStatus = "Installed (Embedded)";
                    pythonServerInstallationStatusColor = Color.green;
                }
                else
                {
                    pythonServerInstallationStatus = "Not Installed";
                    pythonServerInstallationStatusColor = Color.red;
                }
            }
            catch
            {
                pythonServerInstallationStatus = "Not Installed";
                pythonServerInstallationStatusColor = Color.red;
            }
        }


        private void DrawStatusDot(Rect statusRect, Color statusColor, float size = 12)
        {
            var offsetX = (statusRect.width - size) / 2;
            var offsetY = (statusRect.height - size) / 2;
            Rect dotRect = new(statusRect.x + offsetX, statusRect.y + offsetY, size, size);
            Vector3 center = new(
                dotRect.x + (dotRect.width / 2),
                dotRect.y + (dotRect.height / 2),
                0
            );
            var radius = size / 2;

            // Draw the main dot
            Handles.color = statusColor;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);

            // Draw the border
            Color borderColor = new(
                statusColor.r * 0.7f,
                statusColor.g * 0.7f,
                statusColor.b * 0.7f
            );
            Handles.color = borderColor;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Header
            DrawHeader();

            // Periodically refresh to catch port changes
            if (Event.current.type == EventType.Layout)
            {
                // Schedule a repaint every second to catch port updates
                var currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - lastRepaintTime > 1.0)
                {
                    lastRepaintTime = currentTime;
                    Repaint();
                }
            }

            // Compute equal column widths for uniform layout
            var horizontalSpacing = 2f;
            var outerPadding = 20f; // approximate padding
            // Make columns a bit less wide for a tighter layout
            var computed = (position.width - outerPadding - horizontalSpacing) / 2f;
            var colWidth = Mathf.Clamp(computed, 220f, 340f);
            // Use fixed heights per row so paired panels match exactly
            var topPanelHeight = 190f;
            var bottomPanelHeight = 230f;

            // Top row: Server Status (left) and Unity Bridge (right)
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(colWidth), GUILayout.Height(topPanelHeight));
                DrawServerStatusSection();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(horizontalSpacing);

                EditorGUILayout.BeginVertical(GUILayout.Width(colWidth), GUILayout.Height(topPanelHeight));
                DrawBridgeSection();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Second row: MCP Client Configuration (left) and Script Validation (right)
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(colWidth), GUILayout.Height(bottomPanelHeight));
                DrawUnifiedClientConfiguration();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(horizontalSpacing);

                EditorGUILayout.BeginVertical(GUILayout.Width(colWidth), GUILayout.Height(bottomPanelHeight));
                DrawValidationSection();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            // Minimal bottom padding
            EditorGUILayout.Space(2);

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(15);
            var titleRect = EditorGUILayout.GetControlRect(false, 40);
            EditorGUI.DrawRect(titleRect, new Color(0.2f, 0.2f, 0.2f, 0.1f));

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            GUI.Label(
                new Rect(titleRect.x + 15, titleRect.y + 8, titleRect.width - 30, titleRect.height),
                "MCP for Unity Editor",
                titleStyle
            );

            // Place the Show Debug Logs toggle on the same header row, right-aligned
            var toggleWidth = 160f;
            var toggleRect = new Rect(titleRect.xMax - toggleWidth - 12f, titleRect.y + 10f, toggleWidth, 20f);
            var newDebug = GUI.Toggle(toggleRect, debugLogsEnabled, "Show Debug Logs");
            if (newDebug != debugLogsEnabled)
            {
                debugLogsEnabled = newDebug;
                EditorPrefs.SetBool("MCPForUnity.DebugLogs", debugLogsEnabled);
                if (debugLogsEnabled)
                {
                    LogDebugPrefsState();
                }
            }
            EditorGUILayout.Space(15);
        }

        private void LogDebugPrefsState()
        {
            try
            {
                var pythonDirOverridePref = SafeGetPrefString("MCPForUnity.PythonDirOverride");
                var uvPathPref = SafeGetPrefString("MCPForUnity.UvPath");
                var serverSrcPref = SafeGetPrefString("MCPForUnity.ServerSrc");
                var useEmbedded = SafeGetPrefBool("MCPForUnity.UseEmbeddedServer");

                // Version-scoped detection key
                var embeddedVer = ReadEmbeddedVersionOrFallback();
                var detectKey = $"MCPForUnity.LegacyDetectLogged:{embeddedVer}";
                var detectLogged = SafeGetPrefBool(detectKey);

                // Project-scoped auto-register key
                var projectPath = Application.dataPath ?? string.Empty;
                var autoKey = $"MCPForUnity.AutoRegistered.{ComputeSha1(projectPath)}";
                var autoRegistered = SafeGetPrefBool(autoKey);

                MCPForUnity.Editor.Helpers.McpLog.Info(
                    "MCP Debug Prefs:\n" +
                    $"  DebugLogs: {debugLogsEnabled}\n" +
                    $"  PythonDirOverride: '{pythonDirOverridePref}'\n" +
                    $"  UvPath: '{uvPathPref}'\n" +
                    $"  ServerSrc: '{serverSrcPref}'\n" +
                    $"  UseEmbeddedServer: {useEmbedded}\n" +
                    $"  DetectOnceKey: '{detectKey}' => {detectLogged}\n" +
                    $"  AutoRegisteredKey: '{autoKey}' => {autoRegistered}",
                    always: false
                );
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"MCP Debug Prefs logging failed: {ex.Message}");
            }
        }

        private static string SafeGetPrefString(string key)
        {
            try { return EditorPrefs.GetString(key, string.Empty) ?? string.Empty; } catch { return string.Empty; }
        }

        private static bool SafeGetPrefBool(string key)
        {
            try { return EditorPrefs.GetBool(key, false); } catch { return false; }
        }

        private static string ReadEmbeddedVersionOrFallback()
        {
            try
            {
                if (ServerPathResolver.TryFindEmbeddedServerSource(out var embeddedSrc))
                {
                    var p = Path.Combine(embeddedSrc, "server_version.txt");
                    if (File.Exists(p))
                    {
                        var s = File.ReadAllText(p)?.Trim();
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }
            }
            catch { }
            return "unknown";
        }

        private void DrawServerStatusSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
            EditorGUILayout.LabelField("Server Status", sectionTitleStyle);
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            var statusRect = GUILayoutUtility.GetRect(0, 28, GUILayout.Width(24));
            DrawStatusDot(statusRect, pythonServerInstallationStatusColor, 16);

            var statusStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(pythonServerInstallationStatus, statusStyle, GUILayout.Height(28));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            var isAutoMode = MCPForUnityBridge.IsAutoConnectMode();
            var modeStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
            EditorGUILayout.LabelField($"Mode: {(isAutoMode ? "Auto" : "Standard")}", modeStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var currentUnityPort = MCPForUnityBridge.GetCurrentPort();

            // Port display with edit capability
            EditorGUILayout.BeginHorizontal();
            var portStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11
            };

            if (!isEditingPort)
            {
                EditorGUILayout.LabelField($"Unity Port: {currentUnityPort}", portStyle, GUILayout.Width(100));
                if (GUILayout.Button("Change", GUILayout.Width(60), GUILayout.Height(18)))
                {
                    isEditingPort = true;
                    manualPortInput = currentUnityPort;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Unity Port:", portStyle, GUILayout.Width(70));
                manualPortInput = EditorGUILayout.IntField(manualPortInput, GUILayout.Width(60));

                if (GUILayout.Button("Set", GUILayout.Width(40), GUILayout.Height(18)))
                {
                    SetManualPort(manualPortInput);
                    isEditingPort = false;
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(50), GUILayout.Height(18)))
                {
                    isEditingPort = false;
                }
            }
            EditorGUILayout.LabelField($"MCP: {mcpPort}", portStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            /// Auto-Setup button below ports
            var setupButtonText = (lastClientRegisteredOk && lastBridgeVerifiedOk) ? "Connected ✓" : "Auto-Setup";
            if (GUILayout.Button(setupButtonText, GUILayout.Height(24)))
            {
                RunSetupNow();
            }
            EditorGUILayout.Space(4);

            // Repair Python Env button with tooltip tag
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                var repairLabel = new GUIContent(
                    "Repair Python Env",
                    "Deletes the server's .venv and runs 'uv sync' to rebuild a clean environment. Use this if modules are missing or Python upgraded."
                );
                if (GUILayout.Button(repairLabel, GUILayout.Width(160), GUILayout.Height(22)))
                {
                    var ok = global::MCPForUnity.Editor.Helpers.ServerInstaller.RepairPythonEnvironment();
                    if (ok)
                    {
                        EditorUtility.DisplayDialog("MCP for Unity", "Python environment repaired.", "OK");
                        UpdatePythonServerInstallationStatus();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("MCP for Unity", "Repair failed. Please check Console for details.", "OK");
                    }
                }
            }
            // (Removed descriptive tool tag under the Repair button)

            // (Show Debug Logs toggle moved to header)
            EditorGUILayout.Space(2);

            // Python detection warning with link
            if (!IsPythonDetected())
            {
                var warnStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
                EditorGUILayout.LabelField("<color=#cc3333><b>Warning:</b></color> No Python installation found.", warnStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Install Instructions", GUILayout.Width(200)))
                    {
                        Application.OpenURL("https://www.python.org/downloads/");
                    }
                }
                EditorGUILayout.Space(4);
            }

            // Troubleshooting helpers
            if (pythonServerInstallationStatusColor != Color.green)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select server folder…", GUILayout.Width(160)))
                    {
                        var picked = EditorUtility.OpenFolderPanel("Select UnityMcpServer/src", Application.dataPath, "");
                        if (!string.IsNullOrEmpty(picked) && File.Exists(Path.Combine(picked, "server.py")))
                        {
                            pythonDirOverride = picked;
                            EditorPrefs.SetString("MCPForUnity.PythonDirOverride", pythonDirOverride);
                            UpdatePythonServerInstallationStatus();
                        }
                        else if (!string.IsNullOrEmpty(picked))
                        {
                            EditorUtility.DisplayDialog("Invalid Selection", "The selected folder does not contain server.py", "OK");
                        }
                    }
                    if (GUILayout.Button("Verify again", GUILayout.Width(120)))
                    {
                        UpdatePythonServerInstallationStatus();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawBridgeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Always reflect the live state each repaint to avoid stale UI after recompiles
            isUnityBridgeRunning = MCPForUnityBridge.IsRunning;

            // Force repaint if bridge status changed to update port display
            if (isUnityBridgeRunning != MCPForUnityBridge.IsRunning)
            {
                Repaint();
            }

            var sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
            EditorGUILayout.LabelField("Unity Bridge", sectionTitleStyle);
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            var bridgeColor = isUnityBridgeRunning ? Color.green : Color.red;
            var bridgeStatusRect = GUILayoutUtility.GetRect(0, 28, GUILayout.Width(24));
            DrawStatusDot(bridgeStatusRect, bridgeColor, 16);

            var bridgeStatusStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(isUnityBridgeRunning ? "Running" : "Stopped", bridgeStatusStyle, GUILayout.Height(28));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (GUILayout.Button(isUnityBridgeRunning ? "Stop Bridge" : "Start Bridge", GUILayout.Height(32)))
            {
                ToggleUnityBridge();
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
            EditorGUILayout.LabelField("Script Validation", sectionTitleStyle);
            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            validationLevelIndex = EditorGUILayout.Popup("Validation Level", validationLevelIndex, validationLevelOptions, GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck())
            {
                SaveValidationLevelSetting();
            }

            EditorGUILayout.Space(8);
            var description = GetValidationLevelDescription(validationLevelIndex);
            EditorGUILayout.HelpBox(description, MessageType.Info);
            EditorGUILayout.Space(4);
            // (Show Debug Logs toggle moved to header)
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        private void DrawUnifiedClientConfiguration()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };
            EditorGUILayout.LabelField("MCP Client Configuration", sectionTitleStyle);
            EditorGUILayout.Space(10);

			// (Auto-connect toggle removed per design)

            // Client selector
            var clientNames = mcpClients.clients.Select(c => c.name).ToArray();
            EditorGUI.BeginChangeCheck();
            selectedClientIndex = EditorGUILayout.Popup("Select Client", selectedClientIndex, clientNames, GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck())
            {
                selectedClientIndex = Mathf.Clamp(selectedClientIndex, 0, mcpClients.clients.Count - 1);
            }

            EditorGUILayout.Space(10);

            if (mcpClients.clients.Count > 0 && selectedClientIndex < mcpClients.clients.Count)
            {
                var selectedClient = mcpClients.clients[selectedClientIndex];
                DrawClientConfigurationCompact(selectedClient);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void AutoFirstRunSetup()
        {
            try
            {
                // Project-scoped one-time flag
                var projectPath = Application.dataPath ?? string.Empty;
                var key = $"MCPForUnity.AutoRegistered.{ComputeSha1(projectPath)}";
                if (EditorPrefs.GetBool(key, false))
                {
                    return;
                }

                // Attempt client registration using discovered Python server dir
                pythonDirOverride ??= EditorPrefs.GetString("MCPForUnity.PythonDirOverride", null);
                var pythonDir = !string.IsNullOrEmpty(pythonDirOverride) ? pythonDirOverride : FindPackagePythonDirectory();
                if (!string.IsNullOrEmpty(pythonDir) && File.Exists(Path.Combine(pythonDir, "server.py")))
                {
                    var anyRegistered = false;
                    foreach (var client in mcpClients.clients)
                    {
                        try
                        {
                            if (client.mcpType == McpTypes.ClaudeCode)
                            {
                                // Only attempt if Claude CLI is present
                                if (!IsClaudeConfigured() && !string.IsNullOrEmpty(ExecPath.ResolveClaude()))
                                {
                                    RegisterWithClaudeCode(pythonDir);
                                    anyRegistered = true;
                                }
                            }
                            else
                            {
                                // For Cursor/others, skip if already configured
                                if (!IsCursorConfigured(pythonDir))
                                {
                                    ConfigureMcpClient(client);
                                    anyRegistered = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MCPForUnity.Editor.Helpers.McpLog.Warn($"Auto-setup client '{client.name}' failed: {ex.Message}");
                        }
                    }
                    lastClientRegisteredOk = anyRegistered || IsCursorConfigured(pythonDir) || IsClaudeConfigured();
                }

                // Ensure the bridge is listening and has a fresh saved port
                if (!MCPForUnityBridge.IsRunning)
                {
                    try
                    {
                        MCPForUnityBridge.StartAutoConnect();
                        isUnityBridgeRunning = MCPForUnityBridge.IsRunning;
                        Repaint();
                    }
                    catch (Exception ex)
                    {
                        MCPForUnity.Editor.Helpers.McpLog.Warn($"Auto-setup StartAutoConnect failed: {ex.Message}");
                    }
                }

                // Verify bridge with a quick ping
                lastBridgeVerifiedOk = VerifyBridgePing(MCPForUnityBridge.GetCurrentPort());

                EditorPrefs.SetBool(key, true);
            }
            catch (Exception e)
            {
                MCPForUnity.Editor.Helpers.McpLog.Warn($"MCP for Unity auto-setup skipped: {e.Message}");
            }
        }

        private static string ComputeSha1(string input)
        {
            try
            {
                using var sha1 = SHA1.Create();
                var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
                var hash = sha1.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        private void SetManualPort(int newPort)
        {
            if (newPort < 1024 || newPort > 65535)
            {
                EditorUtility.DisplayDialog("Invalid Port", "Port must be between 1024 and 65535", "OK");
                return;
            }

            // Save the new port for this project
            var projectHash = ComputeSha1(Application.dataPath).Substring(0, 8);
            var portFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-mcp",
                $"unity-mcp-port-{projectHash}.json"
            );

            try
            {
                // Create directory if needed
                var dir = Path.GetDirectoryName(portFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Write new port config
                var config = new
                {
                    unity_port = newPort,
                    created_date = DateTime.UtcNow.ToString("o"),
                    project_path = Application.dataPath
                };
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(portFile, json);

                // If bridge is running, restart it with new port
                if (MCPForUnityBridge.IsRunning)
                {
                    MCPForUnityBridge.Stop();
                    EditorApplication.delayCall += () =>
                    {
                        MCPForUnityBridge.Start();
                        Repaint();
                    };
                }

                EditorUtility.DisplayDialog("Port Changed", $"Unity Bridge port set to {newPort}. Bridge will restart if running.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to set port: {ex.Message}", "OK");
            }
        }

        private void RunSetupNow()
        {
            // Force a one-shot setup regardless of first-run flag
            try
            {
                pythonDirOverride ??= EditorPrefs.GetString("MCPForUnity.PythonDirOverride", null);
                var pythonDir = !string.IsNullOrEmpty(pythonDirOverride) ? pythonDirOverride : FindPackagePythonDirectory();
                if (string.IsNullOrEmpty(pythonDir) || !File.Exists(Path.Combine(pythonDir, "server.py")))
                {
                    EditorUtility.DisplayDialog("Setup", "Python server not found. Please select UnityMcpServer/src.", "OK");
                    return;
                }

                var anyRegistered = false;
                foreach (var client in mcpClients.clients)
                {
                    try
                    {
                        if (client.mcpType == McpTypes.ClaudeCode)
                        {
                            if (!IsClaudeConfigured())
                            {
                                RegisterWithClaudeCode(pythonDir);
                                anyRegistered = true;
                            }
                        }
                        else
                        {
                            if (!IsCursorConfigured(pythonDir))
                            {
                                ConfigureMcpClient(client);
                                anyRegistered = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Setup client '{client.name}' failed: {ex.Message}");
                    }
                }
                lastClientRegisteredOk = anyRegistered || IsCursorConfigured(pythonDir) || IsClaudeConfigured();

                // Restart/ensure bridge
                MCPForUnityBridge.StartAutoConnect();
                isUnityBridgeRunning = MCPForUnityBridge.IsRunning;

                // Verify
                lastBridgeVerifiedOk = VerifyBridgePing(MCPForUnityBridge.GetCurrentPort());
                Repaint();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Setup Failed", e.Message, "OK");
            }
        }

        private static bool IsCursorConfigured(string pythonDir)
        {
            try
            {
                var configPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cursor", "mcp.json")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".cursor", "mcp.json");
                if (!File.Exists(configPath)) return false;
                var json = File.ReadAllText(configPath);
                dynamic cfg = JsonConvert.DeserializeObject(json);
                var servers = cfg?.mcpServers;
                if (servers == null) return false;
                var unity = servers.unityMCP ?? servers.UnityMCP;
                if (unity == null) return false;
                var args = unity.args;
                if (args == null) return false;
                // Prefer exact extraction of the --directory value and compare normalized paths
                var strArgs = ((System.Collections.Generic.IEnumerable<object>)args)
                    .Select(x => x?.ToString() ?? string.Empty)
                    .ToArray();
                var dir = ExtractDirectoryArg(strArgs);
                if (string.IsNullOrEmpty(dir)) return false;
                return PathsEqual(dir, pythonDir);
            }
            catch { return false; }
        }

        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try
            {
                var na = System.IO.Path.GetFullPath(a.Trim());
                var nb = System.IO.Path.GetFullPath(b.Trim());
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
                // Default to ordinal on Unix; optionally detect FS case-sensitivity at runtime if needed
                return string.Equals(na, nb, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private static bool IsClaudeConfigured()
        {
            try
            {
                var claudePath = ExecPath.ResolveClaude();
                if (string.IsNullOrEmpty(claudePath)) return false;

                // Only prepend PATH on Unix
                string pathPrepend = null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    pathPrepend = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin"
                        : "/usr/local/bin:/usr/bin:/bin";
                }

                if (!ExecPath.TryRun(claudePath, "mcp list", workingDir: null, out var stdout, out var stderr, 5000, pathPrepend))
                {
                    return false;
                }
                return (stdout ?? string.Empty).IndexOf("UnityMCP", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static bool VerifyBridgePing(int port)
        {
            // Use strict framed protocol to match bridge (FRAMING=1)
            const int ConnectTimeoutMs = 1000;
            const int FrameTimeoutMs = 30000; // match bridge frame I/O timeout

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
                if (!connectTask.Wait(ConnectTimeoutMs)) return false;

                using var stream = client.GetStream();
                try { client.NoDelay = true; } catch { }

                // 1) Read handshake line (ASCII, newline-terminated)
                var handshake = ReadLineAscii(stream, 2000);
                if (string.IsNullOrEmpty(handshake) || handshake.IndexOf("FRAMING=1", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    UnityEngine.Debug.LogWarning("MCP for Unity: Bridge handshake missing FRAMING=1");
                    return false;
                }

                // 2) Send framed "ping"
                var payload = Encoding.UTF8.GetBytes("ping");
                WriteFrame(stream, payload, FrameTimeoutMs);

                // 3) Read framed response and check for pong
                var response = ReadFrameUtf8(stream, FrameTimeoutMs);
                var ok = !string.IsNullOrEmpty(response) && response.IndexOf("pong", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!ok)
                {
                    UnityEngine.Debug.LogWarning($"MCP for Unity: Framed ping failed; response='{response}'");
                }
                return ok;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"MCP for Unity: VerifyBridgePing error: {ex.Message}");
                return false;
            }
        }

        // Minimal framing helpers (8-byte big-endian length prefix), blocking with timeouts
        private static void WriteFrame(NetworkStream stream, byte[] payload, int timeoutMs)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.LongLength < 1) throw new IOException("Zero-length frames are not allowed");
            var header = new byte[8];
            var len = (ulong)payload.LongLength;
            header[0] = (byte)(len >> 56);
            header[1] = (byte)(len >> 48);
            header[2] = (byte)(len >> 40);
            header[3] = (byte)(len >> 32);
            header[4] = (byte)(len >> 24);
            header[5] = (byte)(len >> 16);
            header[6] = (byte)(len >> 8);
            header[7] = (byte)(len);

            stream.WriteTimeout = timeoutMs;
            stream.Write(header, 0, header.Length);
            stream.Write(payload, 0, payload.Length);
        }

        private static string ReadFrameUtf8(NetworkStream stream, int timeoutMs)
        {
            var header = ReadExact(stream, 8, timeoutMs);
            var len = ((ulong)header[0] << 56)
                      | ((ulong)header[1] << 48)
                      | ((ulong)header[2] << 40)
                      | ((ulong)header[3] << 32)
                      | ((ulong)header[4] << 24)
                      | ((ulong)header[5] << 16)
                      | ((ulong)header[6] << 8)
                      | header[7];
            if (len == 0UL) throw new IOException("Zero-length frames are not allowed");
            if (len > int.MaxValue) throw new IOException("Frame too large");
            var payload = ReadExact(stream, (int)len, timeoutMs);
            return Encoding.UTF8.GetString(payload);
        }

        private static byte[] ReadExact(NetworkStream stream, int count, int timeoutMs)
        {
            var buffer = new byte[count];
            var offset = 0;
            stream.ReadTimeout = timeoutMs;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new IOException("Connection closed before reading expected bytes");
                offset += read;
            }
            return buffer;
        }

        private static string ReadLineAscii(NetworkStream stream, int timeoutMs, int maxLen = 512)
        {
            stream.ReadTimeout = timeoutMs;
            using var ms = new MemoryStream();
            var one = new byte[1];
            while (ms.Length < maxLen)
            {
                var n = stream.Read(one, 0, 1);
                if (n <= 0) break;
                if (one[0] == (byte)'\n') break;
                ms.WriteByte(one[0]);
            }
            return Encoding.ASCII.GetString(ms.ToArray());
        }

        private void DrawClientConfigurationCompact(McpClient mcpClient)
        {
			// Special pre-check for Claude Code: if CLI missing, reflect in status UI
			if (mcpClient.mcpType == McpTypes.ClaudeCode)
			{
				var claudeCheck = ExecPath.ResolveClaude();
				if (string.IsNullOrEmpty(claudeCheck))
				{
					mcpClient.configStatus = "Claude Not Found";
					mcpClient.status = McpStatus.NotConfigured;
				}
			}

			// Pre-check for clients that require uv (all except Claude Code)
			var uvRequired = mcpClient.mcpType != McpTypes.ClaudeCode;
			var uvMissingEarly = false;
			if (uvRequired)
			{
				var uvPathEarly = FindUvPath();
				if (string.IsNullOrEmpty(uvPathEarly))
				{
					uvMissingEarly = true;
					mcpClient.configStatus = "uv Not Found";
					mcpClient.status = McpStatus.NotConfigured;
				}
			}

            // Status display
            EditorGUILayout.BeginHorizontal();
            var statusRect = GUILayoutUtility.GetRect(0, 28, GUILayout.Width(24));
            var statusColor = GetStatusColor(mcpClient.status);
            DrawStatusDot(statusRect, statusColor, 16);

            var clientStatusStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(mcpClient.configStatus, clientStatusStyle, GUILayout.Height(28));
            EditorGUILayout.EndHorizontal();
			// When Claude CLI is missing, show a clear install hint directly below status
			if (mcpClient.mcpType == McpTypes.ClaudeCode && string.IsNullOrEmpty(ExecPath.ResolveClaude()))
			{
				var installHintStyle = new GUIStyle(clientStatusStyle);
				installHintStyle.normal.textColor = new Color(1f, 0.5f, 0f); // orange
				EditorGUILayout.BeginHorizontal();
				var installText = new GUIContent("Make sure Claude Code is installed!");
				var textSize = installHintStyle.CalcSize(installText);
				EditorGUILayout.LabelField(installText, installHintStyle, GUILayout.Height(22), GUILayout.Width(textSize.x + 2), GUILayout.ExpandWidth(false));
				var helpLinkStyle = new GUIStyle(EditorStyles.linkLabel) { fontStyle = FontStyle.Bold };
				GUILayout.Space(6);
				if (GUILayout.Button("[HELP]", helpLinkStyle, GUILayout.Height(22), GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL("https://github.com/CoplayDev/unity-mcp/wiki/Troubleshooting-Unity-MCP-and-Claude-Code");
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.Space(10);

			// If uv is missing for required clients, show hint and picker then exit early to avoid showing other controls
			if (uvRequired && uvMissingEarly)
			{
				var installHintStyle2 = new GUIStyle(EditorStyles.label)
				{
					fontSize = 12,
					fontStyle = FontStyle.Bold,
					wordWrap = false
				};
				installHintStyle2.normal.textColor = new Color(1f, 0.5f, 0f);
				EditorGUILayout.BeginHorizontal();
				var installText2 = new GUIContent("Make sure uv is installed!");
				var sz = installHintStyle2.CalcSize(installText2);
				EditorGUILayout.LabelField(installText2, installHintStyle2, GUILayout.Height(22), GUILayout.Width(sz.x + 2), GUILayout.ExpandWidth(false));
				var helpLinkStyle2 = new GUIStyle(EditorStyles.linkLabel) { fontStyle = FontStyle.Bold };
				GUILayout.Space(6);
				if (GUILayout.Button("[HELP]", helpLinkStyle2, GUILayout.Height(22), GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL("https://github.com/CoplayDev/unity-mcp/wiki/Troubleshooting-Unity-MCP-and-Cursor,-VSCode-&-Windsurf");
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space(8);
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Choose uv Install Location", GUILayout.Width(260), GUILayout.Height(22)))
				{
					var suggested = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/opt/homebrew/bin" : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
					var picked = EditorUtility.OpenFilePanel("Select 'uv' binary", suggested, "");
					if (!string.IsNullOrEmpty(picked))
					{
						EditorPrefs.SetString("MCPForUnity.UvPath", picked);
						ConfigureMcpClient(mcpClient);
						Repaint();
					}
				}
				EditorGUILayout.EndHorizontal();
				return;
			}

            // Action buttons in horizontal layout
            EditorGUILayout.BeginHorizontal();

            if (mcpClient.mcpType == McpTypes.VSCode)
            {
                if (GUILayout.Button("Auto Configure", GUILayout.Height(32)))
                {
                    ConfigureMcpClient(mcpClient);
                }
            }
			else if (mcpClient.mcpType == McpTypes.ClaudeCode)
			{
				var claudeAvailable = !string.IsNullOrEmpty(ExecPath.ResolveClaude());
				if (claudeAvailable)
				{
					var isConfigured = mcpClient.status == McpStatus.Configured;
					var buttonText = isConfigured ? "Unregister MCP for Unity with Claude Code" : "Register with Claude Code";
					if (GUILayout.Button(buttonText, GUILayout.Height(32)))
					{
						if (isConfigured)
						{
							UnregisterWithClaudeCode();
						}
						else
						{
							var pythonDir = FindPackagePythonDirectory();
							RegisterWithClaudeCode(pythonDir);
						}
					}
					// Hide the picker once a valid binary is available
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					var pathLabelStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
					var resolvedClaude = ExecPath.ResolveClaude();
					EditorGUILayout.LabelField($"Claude CLI: {resolvedClaude}", pathLabelStyle);
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
				}
				// CLI picker row (only when not found)
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				if (!claudeAvailable)
				{
					// Only show the picker button in not-found state (no redundant "not found" label)
					if (GUILayout.Button("Choose Claude Install Location", GUILayout.Width(260), GUILayout.Height(22)))
					{
						var suggested = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/opt/homebrew/bin" : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
						var picked = EditorUtility.OpenFilePanel("Select 'claude' CLI", suggested, "");
						if (!string.IsNullOrEmpty(picked))
						{
							ExecPath.SetClaudeCliPath(picked);
							// Auto-register after setting a valid path
							var pythonDir = FindPackagePythonDirectory();
							RegisterWithClaudeCode(pythonDir);
							Repaint();
						}
					}
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
			}
            else
            {
                if (GUILayout.Button($"Auto Configure", GUILayout.Height(32)))
                {
                    ConfigureMcpClient(mcpClient);
                }
            }

            if (mcpClient.mcpType != McpTypes.ClaudeCode)
            {
                if (GUILayout.Button("Manual Setup", GUILayout.Height(32)))
                {
                    var configPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? mcpClient.windowsConfigPath
                        : mcpClient.linuxConfigPath;

                    if (mcpClient.mcpType == McpTypes.VSCode)
                    {
                        var pythonDir = FindPackagePythonDirectory();
                        var uvPath = FindUvPath();
                        if (uvPath == null)
                        {
                            UnityEngine.Debug.LogError("UV package manager not found. Cannot configure VSCode.");
                            return;
                        }
                        // VSCode now reads from mcp.json with a top-level "servers" block
                        var vscodeConfig = new
                        {
                            servers = new
                            {
                                unityMCP = new
                                {
                                    command = uvPath,
                                    args = new[] { "run", "--directory", pythonDir, "server.py" }
                                }
                            }
                        };
                        JsonSerializerSettings jsonSettings = new() { Formatting = Formatting.Indented };
                        var manualConfigJson = JsonConvert.SerializeObject(vscodeConfig, jsonSettings);
                        VSCodeManualSetupWindow.ShowWindow(configPath, manualConfigJson);
                    }
                    else
                    {
                        ShowManualInstructionsWindow(configPath, mcpClient);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(8);
			// Quick info (hide when Claude is not found to avoid confusion)
			var hideConfigInfo =
				(mcpClient.mcpType == McpTypes.ClaudeCode && string.IsNullOrEmpty(ExecPath.ResolveClaude()))
				|| ((mcpClient.mcpType != McpTypes.ClaudeCode) && string.IsNullOrEmpty(FindUvPath()));
			if (!hideConfigInfo)
			{
				var configInfoStyle = new GUIStyle(EditorStyles.miniLabel)
				{
					fontSize = 10
				};
				EditorGUILayout.LabelField($"Config: {Path.GetFileName(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? mcpClient.windowsConfigPath : mcpClient.linuxConfigPath)}", configInfoStyle);
			}
        }

        private void ToggleUnityBridge()
        {
            if (isUnityBridgeRunning)
            {
                MCPForUnityBridge.Stop();
            }
            else
            {
                MCPForUnityBridge.Start();
            }
            // Reflect the actual state post-operation (avoid optimistic toggle)
            isUnityBridgeRunning = MCPForUnityBridge.IsRunning;
            Repaint();
        }

		private static bool IsValidUv(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& System.IO.Path.IsPathRooted(path)
				&& System.IO.File.Exists(path);
		}

		private static bool ValidateUvBinarySafe(string path)
		{
			try
			{
				if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return false;
				var psi = new System.Diagnostics.ProcessStartInfo
				{
					FileName = path,
					Arguments = "--version",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};
				using var p = System.Diagnostics.Process.Start(psi);
				if (p == null) return false;
				if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } return false; }
				if (p.ExitCode != 0) return false;
				var output = p.StandardOutput.ReadToEnd().Trim();
				return output.StartsWith("uv ");
			}
			catch { return false; }
		}

		private static string ExtractDirectoryArg(string[] args)
		{
			if (args == null) return null;
			for (var i = 0; i < args.Length - 1; i++)
			{
				if (string.Equals(args[i], "--directory", StringComparison.OrdinalIgnoreCase))
				{
					return args[i + 1];
				}
			}
			return null;
		}

		private static bool ArgsEqual(string[] a, string[] b)
		{
			if (a == null || b == null) return a == b;
			if (a.Length != b.Length) return false;
			for (var i = 0; i < a.Length; i++)
			{
				if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
			}
			return true;
		}

        private string WriteToConfig(string pythonDir, string configPath, McpClient mcpClient = null)
        {
			// 0) Respect explicit lock (hidden pref or UI toggle)
			try { if (UnityEditor.EditorPrefs.GetBool("MCPForUnity.LockCursorConfig", false)) return "Skipped (locked)"; } catch { }

            JsonSerializerSettings jsonSettings = new() { Formatting = Formatting.Indented };

            // Read existing config if it exists
            var existingJson = "{}";
            if (File.Exists(configPath))
            {
                try
                {
                    existingJson = File.ReadAllText(configPath);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"Error reading existing config: {e.Message}.");
                }
            }

            // Parse the existing JSON while preserving all properties
            dynamic existingConfig;
            try
            {
                if (string.IsNullOrWhiteSpace(existingJson))
                {
                    existingConfig = new Newtonsoft.Json.Linq.JObject();
                }
                else
                {
                    existingConfig = JsonConvert.DeserializeObject(existingJson) ?? new Newtonsoft.Json.Linq.JObject();
                }
            }
            catch
            {
                // If user has partial/invalid JSON (e.g., mid-edit), start from a fresh object
                if (!string.IsNullOrWhiteSpace(existingJson))
                {
                    UnityEngine.Debug.LogWarning("UnityMCP: VSCode mcp.json could not be parsed; rewriting servers block.");
                }
                existingConfig = new Newtonsoft.Json.Linq.JObject();
            }

			// Determine existing entry references (command/args)
			string existingCommand = null;
			string[] existingArgs = null;
			var isVSCode = (mcpClient?.mcpType == McpTypes.VSCode);
			try
			{
				if (isVSCode)
				{
					existingCommand = existingConfig?.servers?.unityMCP?.command?.ToString();
					existingArgs = existingConfig?.servers?.unityMCP?.args?.ToObject<string[]>();
				}
				else
				{
					existingCommand = existingConfig?.mcpServers?.unityMCP?.command?.ToString();
					existingArgs = existingConfig?.mcpServers?.unityMCP?.args?.ToObject<string[]>();
				}
			}
			catch { }

			// 1) Start from existing, only fill gaps (prefer trusted resolver)
			var uvPath = ServerInstaller.FindUvPath();
			// Optionally trust existingCommand if it looks like uv/uv.exe
			try
			{
				var name = System.IO.Path.GetFileName((existingCommand ?? string.Empty).Trim()).ToLowerInvariant();
				if ((name == "uv" || name == "uv.exe") && ValidateUvBinarySafe(existingCommand))
				{
					uvPath = existingCommand;
				}
			}
			catch { }
			if (uvPath == null) return "UV package manager not found. Please install UV first.";
			var serverSrc = ExtractDirectoryArg(existingArgs);
			var serverValid = !string.IsNullOrEmpty(serverSrc)
				&& System.IO.File.Exists(System.IO.Path.Combine(serverSrc, "server.py"));
			if (!serverValid)
			{
				// Prefer the provided pythonDir if valid; fall back to resolver
				if (!string.IsNullOrEmpty(pythonDir) && System.IO.File.Exists(System.IO.Path.Combine(pythonDir, "server.py")))
				{
					serverSrc = pythonDir;
				}
				else
				{
					serverSrc = ResolveServerSrc();
				}
			}

			// macOS normalization: map XDG-style ~/.local/share to canonical Application Support
			try
			{
				if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
					&& !string.IsNullOrEmpty(serverSrc))
				{
					var norm = serverSrc.Replace('\\', '/');
					var idx = norm.IndexOf("/.local/share/UnityMCP/", StringComparison.Ordinal);
					if (idx >= 0)
					{
						var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal) ?? string.Empty;
						var suffix = norm.Substring(idx + "/.local/share/".Length); // UnityMCP/...
						serverSrc = System.IO.Path.Combine(home, "Library", "Application Support", suffix);
					}
				}
			}
			catch { }

			// Hard-block PackageCache on Windows unless dev override is set
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				&& !string.IsNullOrEmpty(serverSrc)
				&& serverSrc.IndexOf(@"\Library\PackageCache\", StringComparison.OrdinalIgnoreCase) >= 0
				&& !UnityEditor.EditorPrefs.GetBool("MCPForUnity.UseEmbeddedServer", false))
			{
				serverSrc = ServerInstaller.GetServerPath();
			}

			// 2) Canonical args order
			var newArgs = new[] { "run", "--directory", serverSrc, "server.py" };

			// 3) Only write if changed
			var changed = !string.Equals(existingCommand, uvPath, StringComparison.Ordinal)
				|| !ArgsEqual(existingArgs, newArgs);
			if (!changed)
			{
				return "Configured successfully"; // nothing to do
			}

			// 4) Ensure containers exist and write back minimal changes
            JObject existingRoot;
            if (existingConfig is JObject eo)
                existingRoot = eo;
            else
                existingRoot = JObject.FromObject(existingConfig);

            existingRoot = ConfigJsonBuilder.ApplyUnityServerToExistingConfig(existingRoot, uvPath, serverSrc, mcpClient);

			var mergedJson = JsonConvert.SerializeObject(existingRoot, jsonSettings);

			// Robust atomic write without redundant backup or race on existence
			var tmp = configPath + ".tmp";
			var backup = configPath + ".backup";
			var writeDone = false;
			try
			{
				// Write to temp file first (in same directory for atomicity)
				System.IO.File.WriteAllText(tmp, mergedJson, new System.Text.UTF8Encoding(false));

				try
				{
					// Try atomic replace; creates 'backup' only on success (platform-dependent)
					System.IO.File.Replace(tmp, configPath, backup);
					writeDone = true;
				}
				catch (System.IO.FileNotFoundException)
				{
					// Destination didn't exist; fall back to move
					System.IO.File.Move(tmp, configPath);
                    writeDone = true;
				}
				catch (System.PlatformNotSupportedException)
				{
					// Fallback: rename existing to backup, then move tmp into place
					if (System.IO.File.Exists(configPath))
					{
						try { if (System.IO.File.Exists(backup)) System.IO.File.Delete(backup); } catch { }
						System.IO.File.Move(configPath, backup);
					}
					System.IO.File.Move(tmp, configPath);
					writeDone = true;
				}
			}
			catch (Exception ex)
			{

				// If write did not complete, attempt restore from backup without deleting current file first
				try
				{
					if (!writeDone && System.IO.File.Exists(backup))
					{
						try { System.IO.File.Copy(backup, configPath, true); } catch { }
					}
				}
				catch { }
				throw new Exception($"Failed to write config file '{configPath}': {ex.Message}", ex);
			}
			finally
			{
				// Best-effort cleanup of temp
				try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { }
				// Only remove backup after a confirmed successful write
				try { if (writeDone && System.IO.File.Exists(backup)) System.IO.File.Delete(backup); } catch { }
			}

			try
			{
				if (IsValidUv(uvPath)) UnityEditor.EditorPrefs.SetString("MCPForUnity.UvPath", uvPath);
				UnityEditor.EditorPrefs.SetString("MCPForUnity.ServerSrc", serverSrc);
			}
			catch { }

			return "Configured successfully";
        }

        private void ShowManualConfigurationInstructions(
            string configPath,
            McpClient mcpClient
        )
        {
            mcpClient.SetStatus(McpStatus.Error, "Manual configuration required");

            ShowManualInstructionsWindow(configPath, mcpClient);
        }

        // New method to show manual instructions without changing status
        private void ShowManualInstructionsWindow(string configPath, McpClient mcpClient)
        {
            // Get the Python directory path using Package Manager API
            var pythonDir = FindPackagePythonDirectory();
            // Build manual JSON centrally using the shared builder
            var uvPathForManual = FindUvPath();
            if (uvPathForManual == null)
            {
                UnityEngine.Debug.LogError("UV package manager not found. Cannot generate manual configuration.");
                return;
            }

            var manualConfigJson = ConfigJsonBuilder.BuildManualConfigJson(uvPathForManual, pythonDir, mcpClient);
            ManualConfigEditorWindow.ShowWindow(configPath, manualConfigJson, mcpClient);
        }

		private static string ResolveServerSrc()
		{
			try
			{
				var remembered = UnityEditor.EditorPrefs.GetString("MCPForUnity.ServerSrc", string.Empty);
				if (!string.IsNullOrEmpty(remembered) && File.Exists(Path.Combine(remembered, "server.py")))
				{
					return remembered;
				}

				ServerInstaller.EnsureServerInstalled();
				var installed = ServerInstaller.GetServerPath();
				if (File.Exists(Path.Combine(installed, "server.py")))
				{
					return installed;
				}

				var useEmbedded = UnityEditor.EditorPrefs.GetBool("MCPForUnity.UseEmbeddedServer", false);
				if (useEmbedded && ServerPathResolver.TryFindEmbeddedServerSource(out var embedded)
					&& File.Exists(Path.Combine(embedded, "server.py")))
				{
					return embedded;
				}

				return installed;
			}
			catch { return ServerInstaller.GetServerPath(); }
		}

		private string FindPackagePythonDirectory()
        {
			var pythonDir = ResolveServerSrc();

            try
            {
                // Only check dev paths if we're using a file-based package (development mode)
                var isDevelopmentMode = IsDevelopmentMode();
                if (isDevelopmentMode)
                {
                    var currentPackagePath = Path.GetDirectoryName(Application.dataPath);
                    string[] devPaths = {
                        Path.Combine(currentPackagePath, "unity-mcp", "UnityMcpServer", "src"),
                        Path.Combine(Path.GetDirectoryName(currentPackagePath), "unity-mcp", "UnityMcpServer", "src"),
                    };

                    foreach (var devPath in devPaths)
                    {
                        if (Directory.Exists(devPath) && File.Exists(Path.Combine(devPath, "server.py")))
                        {
                            if (debugLogsEnabled)
                            {
                                UnityEngine.Debug.Log($"Currently in development mode. Package: {devPath}");
                            }
                            return devPath;
                        }
                    }
                }

				// Resolve via shared helper (handles local registry and older fallback) only if dev override on
				if (UnityEditor.EditorPrefs.GetBool("MCPForUnity.UseEmbeddedServer", false))
				{
					if (ServerPathResolver.TryFindEmbeddedServerSource(out var embedded))
					{
						return embedded;
					}
				}

				// Log only if the resolved path does not actually contain server.py
				if (debugLogsEnabled)
				{
					var hasServer = false;
					try { hasServer = File.Exists(Path.Combine(pythonDir, "server.py")); } catch { }
					if (!hasServer)
					{
						UnityEngine.Debug.LogWarning("Could not find Python directory with server.py; falling back to installed path");
					}
				}
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Error finding package path: {e.Message}");
            }

            return pythonDir;
        }

        private bool IsDevelopmentMode()
        {
            try
            {
                // Only treat as development if manifest explicitly references a local file path for the package
                var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (!File.Exists(manifestPath)) return false;

                var manifestContent = File.ReadAllText(manifestPath);
                // Look specifically for our package dependency set to a file: URL
                // This avoids auto-enabling dev mode just because a repo exists elsewhere on disk
                if (manifestContent.IndexOf("\"com.justinpbarnett.unity-mcp\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var idx = manifestContent.IndexOf("com.justinpbarnett.unity-mcp", StringComparison.OrdinalIgnoreCase);
                    // Crude but effective: check for "file:" in the same line/value
                    if (manifestContent.IndexOf("file:", idx, StringComparison.OrdinalIgnoreCase) >= 0
                        && manifestContent.IndexOf("\n", idx, StringComparison.OrdinalIgnoreCase) > manifestContent.IndexOf("file:", idx, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string ConfigureMcpClient(McpClient mcpClient)
        {
            try
            {
                // Determine the config file path based on OS
                string configPath;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    configPath = mcpClient.windowsConfigPath;
                }
                else if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                )
                {
                    configPath = string.IsNullOrEmpty(mcpClient.macConfigPath)
                        ? mcpClient.linuxConfigPath
                        : mcpClient.macConfigPath;
                }
                else if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                )
                {
                    configPath = mcpClient.linuxConfigPath;
                }
                else
                {
                    return "Unsupported OS";
                }

                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));

                // Find the server.py file location using the same logic as FindPackagePythonDirectory
                var pythonDir = FindPackagePythonDirectory();

                if (pythonDir == null || !File.Exists(Path.Combine(pythonDir, "server.py")))
                {
                    ShowManualInstructionsWindow(configPath, mcpClient);
                    return "Manual Configuration Required";
                }

                var result = WriteToConfig(pythonDir, configPath, mcpClient);

                // Update the client status after successful configuration
                if (result == "Configured successfully")
                {
                    mcpClient.SetStatus(McpStatus.Configured);
                }

                return result;
            }
            catch (Exception e)
            {
                // Determine the config file path based on OS for error message
                var configPath = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    configPath = mcpClient.windowsConfigPath;
                }
                else if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                )
                {
                    configPath = string.IsNullOrEmpty(mcpClient.macConfigPath)
                        ? mcpClient.linuxConfigPath
                        : mcpClient.macConfigPath;
                }
                else if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                )
                {
                    configPath = mcpClient.linuxConfigPath;
                }

                ShowManualInstructionsWindow(configPath, mcpClient);
                UnityEngine.Debug.LogError(
                    $"Failed to configure {mcpClient.name}: {e.Message}\n{e.StackTrace}"
                );
                return $"Failed to configure {mcpClient.name}";
            }
        }

        private void ShowCursorManualConfigurationInstructions(
            string configPath,
            McpClient mcpClient
        )
        {
            mcpClient.SetStatus(McpStatus.Error, "Manual configuration required");

            // Get the Python directory path using Package Manager API
            var pythonDir = FindPackagePythonDirectory();

            // Create the manual configuration message
            var uvPath = FindUvPath();
            if (uvPath == null)
            {
                UnityEngine.Debug.LogError("UV package manager not found. Cannot configure manual setup.");
                return;
            }

            McpConfig jsonConfig = new()
            {
                mcpServers = new McpConfigServers
                {
                    unityMCP = new McpConfigServer
                    {
                        command = uvPath,
                        args = new[] { "run", "--directory", pythonDir, "server.py" },
                    },
                },
            };

            JsonSerializerSettings jsonSettings = new() { Formatting = Formatting.Indented };
            var manualConfigJson = JsonConvert.SerializeObject(jsonConfig, jsonSettings);

            ManualConfigEditorWindow.ShowWindow(configPath, manualConfigJson, mcpClient);
        }

        private void LoadValidationLevelSetting()
        {
            var savedLevel = EditorPrefs.GetString("MCPForUnity_ScriptValidationLevel", "standard");
            validationLevelIndex = savedLevel.ToLower() switch
            {
                "basic" => 0,
                "standard" => 1,
                "comprehensive" => 2,
                "strict" => 3,
                _ => 1 // Default to Standard
            };
        }

        private void SaveValidationLevelSetting()
        {
            var levelString = validationLevelIndex switch
            {
                0 => "basic",
                1 => "standard",
                2 => "comprehensive",
                3 => "strict",
                _ => "standard"
            };
            EditorPrefs.SetString("MCPForUnity_ScriptValidationLevel", levelString);
        }

        private string GetValidationLevelDescription(int index)
        {
            return index switch
            {
                0 => "Only basic syntax checks (braces, quotes, comments)",
                1 => "Syntax checks + Unity best practices and warnings",
                2 => "All checks + semantic analysis and performance warnings",
                3 => "Full semantic validation with namespace/type resolution (requires Roslyn)",
                _ => "Standard validation"
            };
        }

        public static string GetCurrentValidationLevel()
        {
            var savedLevel = EditorPrefs.GetString("MCPForUnity_ScriptValidationLevel", "standard");
            return savedLevel;
        }

        private void CheckMcpConfiguration(McpClient mcpClient)
        {
            try
            {
                // Special handling for Claude Code
                if (mcpClient.mcpType == McpTypes.ClaudeCode)
                {
                    CheckClaudeCodeConfiguration(mcpClient);
                    return;
                }

                string configPath;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    configPath = mcpClient.windowsConfigPath;
                }
                else if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                )
                {
                    configPath = string.IsNullOrEmpty(mcpClient.macConfigPath)
                        ? mcpClient.linuxConfigPath
                        : mcpClient.macConfigPath;
                }
                else if (
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                )
                {
                    configPath = mcpClient.linuxConfigPath;
                }
                else
                {
                    mcpClient.SetStatus(McpStatus.UnsupportedOS);
                    return;
                }

                if (!File.Exists(configPath))
                {
                    mcpClient.SetStatus(McpStatus.NotConfigured);
                    return;
                }

                var configJson = File.ReadAllText(configPath);
                // Use the same path resolution as configuration to avoid false "Incorrect Path" in dev mode
                var pythonDir = FindPackagePythonDirectory();

                // Use switch statement to handle different client types, extracting common logic
                string[] args = null;
                var configExists = false;

                switch (mcpClient.mcpType)
                {
                    case McpTypes.VSCode:
                        dynamic config = JsonConvert.DeserializeObject(configJson);

                        // New schema: top-level servers
                        if (config?.servers?.unityMCP != null)
                        {
                            args = config.servers.unityMCP.args.ToObject<string[]>();
                            configExists = true;
                        }
                        // Back-compat: legacy mcp.servers
                        else if (config?.mcp?.servers?.unityMCP != null)
                        {
                            args = config.mcp.servers.unityMCP.args.ToObject<string[]>();
                            configExists = true;
                        }
                        break;

                    default:
                        // Standard MCP configuration check for Claude Desktop, Cursor, etc.
                        var standardConfig = JsonConvert.DeserializeObject<McpConfig>(configJson);

                        if (standardConfig?.mcpServers?.unityMCP != null)
                        {
                            args = standardConfig.mcpServers.unityMCP.args;
                            configExists = true;
                        }
                        break;
                }

                // Common logic for checking configuration status
                if (configExists)
                {
                    var configuredDir = ExtractDirectoryArg(args);
                    var matches = !string.IsNullOrEmpty(configuredDir) && PathsEqual(configuredDir, pythonDir);
                    if (matches)
                    {
                        mcpClient.SetStatus(McpStatus.Configured);
                    }
                    else
                    {
                        // Attempt auto-rewrite once if the package path changed
                        try
                        {
                            var rewriteResult = WriteToConfig(pythonDir, configPath, mcpClient);
                            if (rewriteResult == "Configured successfully")
                            {
                                if (debugLogsEnabled)
                                {
                                    MCPForUnity.Editor.Helpers.McpLog.Info($"Auto-updated MCP config for '{mcpClient.name}' to new path: {pythonDir}", always: false);
                                }
                                mcpClient.SetStatus(McpStatus.Configured);
                            }
                            else
                            {
                                mcpClient.SetStatus(McpStatus.IncorrectPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            mcpClient.SetStatus(McpStatus.IncorrectPath);
                            if (debugLogsEnabled)
                            {
                                UnityEngine.Debug.LogWarning($"MCP for Unity: Auto-config rewrite failed for '{mcpClient.name}': {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    mcpClient.SetStatus(McpStatus.MissingConfig);
                }
            }
            catch (Exception e)
            {
                mcpClient.SetStatus(McpStatus.Error, e.Message);
            }
        }

        private void RegisterWithClaudeCode(string pythonDir)
        {
            // Resolve claude and uv; then run register command
            var claudePath = ExecPath.ResolveClaude();
            if (string.IsNullOrEmpty(claudePath))
            {
                UnityEngine.Debug.LogError("MCP for Unity: Claude CLI not found. Set a path in this window or install the CLI, then try again.");
                return;
            }
            var uvPath = ExecPath.ResolveUv() ?? "uv";

            // Prefer embedded/dev path when available
            var srcDir = !string.IsNullOrEmpty(pythonDirOverride) ? pythonDirOverride : FindPackagePythonDirectory();
            if (string.IsNullOrEmpty(srcDir)) srcDir = pythonDir;

            var args = $"mcp add UnityMCP -- \"{uvPath}\" run --directory \"{srcDir}\" server.py";

            var projectDir = Path.GetDirectoryName(Application.dataPath);
            // Ensure PATH includes common locations on Unix; on Windows leave PATH as-is
            string pathPrepend = null;
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxEditor)
            {
                pathPrepend = Application.platform == RuntimePlatform.OSXEditor
                    ? "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin"
                    : "/usr/local/bin:/usr/bin:/bin";
            }
            if (!ExecPath.TryRun(claudePath, args, projectDir, out var stdout, out var stderr, 15000, pathPrepend))
            {
                var combined = ($"{stdout}\n{stderr}") ?? string.Empty;
                if (combined.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Treat as success if Claude reports existing registration
                    var existingClient = mcpClients.clients.FirstOrDefault(c => c.mcpType == McpTypes.ClaudeCode);
                    if (existingClient != null) CheckClaudeCodeConfiguration(existingClient);
                    Repaint();
                    UnityEngine.Debug.Log("<b><color=#2EA3FF>MCP-FOR-UNITY</color></b>: MCP for Unity already registered with Claude Code.");
                }
                else
                {
                    UnityEngine.Debug.LogError($"MCP for Unity: Failed to start Claude CLI.\n{stderr}\n{stdout}");
                }
                return;
            }

            // Update status
            var claudeClient = mcpClients.clients.FirstOrDefault(c => c.mcpType == McpTypes.ClaudeCode);
            if (claudeClient != null) CheckClaudeCodeConfiguration(claudeClient);
            Repaint();
            UnityEngine.Debug.Log("<b><color=#2EA3FF>MCP-FOR-UNITY</color></b>: Registered with Claude Code.");
        }

        private void UnregisterWithClaudeCode()
        {
            var claudePath = ExecPath.ResolveClaude();
            if (string.IsNullOrEmpty(claudePath))
            {
                UnityEngine.Debug.LogError("MCP for Unity: Claude CLI not found. Set a path in this window or install the CLI, then try again.");
                return;
            }

            var projectDir = Path.GetDirectoryName(Application.dataPath);
            var pathPrepend = Application.platform == RuntimePlatform.OSXEditor
                ? "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin"
                : null; // On Windows, don't modify PATH - use system PATH as-is

			// Determine if Claude has a "UnityMCP" server registered by using exit codes from `claude mcp get <name>`
			string[] candidateNamesForGet = { "UnityMCP", "unityMCP", "unity-mcp", "UnityMcpServer" };
			var existingNames = new List<string>();
			foreach (var candidate in candidateNamesForGet)
			{
				if (ExecPath.TryRun(claudePath, $"mcp get {candidate}", projectDir, out var getStdout, out var getStderr, 7000, pathPrepend))
				{
					// Success exit code indicates the server exists
					existingNames.Add(candidate);
				}
			}

			if (existingNames.Count == 0)
			{
				// Nothing to unregister – set status and bail early
				var claudeClient = mcpClients.clients.FirstOrDefault(c => c.mcpType == McpTypes.ClaudeCode);
				if (claudeClient != null)
				{
					claudeClient.SetStatus(McpStatus.NotConfigured);
					UnityEngine.Debug.Log("Claude CLI reports no MCP for Unity server via 'mcp get' - setting status to NotConfigured and aborting unregister.");
					Repaint();
				}
				return;
			}

            // Try different possible server names
            string[] possibleNames = { "UnityMCP", "unityMCP", "unity-mcp", "UnityMcpServer" };
            var success = false;

            foreach (var serverName in possibleNames)
            {
                if (ExecPath.TryRun(claudePath, $"mcp remove {serverName}", projectDir, out var stdout, out var stderr, 10000, pathPrepend))
                {
                    success = true;
                    UnityEngine.Debug.Log($"MCP for Unity: Successfully removed MCP server: {serverName}");
                    break;
                }
                else if (!string.IsNullOrEmpty(stderr) &&
                         !stderr.Contains("No MCP server found", StringComparison.OrdinalIgnoreCase))
                {
                    // If it's not a "not found" error, log it and stop trying
                    UnityEngine.Debug.LogWarning($"Error removing {serverName}: {stderr}");
                    break;
                }
            }

            if (success)
            {
                var claudeClient = mcpClients.clients.FirstOrDefault(c => c.mcpType == McpTypes.ClaudeCode);
                if (claudeClient != null)
                {
                    // Optimistically flip to NotConfigured; then verify
                    claudeClient.SetStatus(McpStatus.NotConfigured);
                    CheckClaudeCodeConfiguration(claudeClient);
                }
                Repaint();
                UnityEngine.Debug.Log("MCP for Unity: MCP server successfully unregistered from Claude Code.");
            }
            else
            {
                // If no servers were found to remove, they're already unregistered
                // Force status to NotConfigured and update the UI
                UnityEngine.Debug.Log("No MCP servers found to unregister - already unregistered.");
                var claudeClient = mcpClients.clients.FirstOrDefault(c => c.mcpType == McpTypes.ClaudeCode);
                if (claudeClient != null)
                {
                    claudeClient.SetStatus(McpStatus.NotConfigured);
                    CheckClaudeCodeConfiguration(claudeClient);
                }
                Repaint();
            }
        }

        // Removed unused ParseTextOutput

        private string FindUvPath()
        {
            try { return MCPForUnity.Editor.Helpers.ServerInstaller.FindUvPath(); } catch { return null; }
        }

        // Validation and platform-specific scanning are handled by ServerInstaller.FindUvPath()

        // Windows-specific discovery removed; use ServerInstaller.FindUvPath() instead

        // Removed unused FindClaudeCommand

        private void CheckClaudeCodeConfiguration(McpClient mcpClient)
        {
            try
            {
                // Get the Unity project directory to check project-specific config
                var unityProjectDir = Application.dataPath;
                var projectDir = Path.GetDirectoryName(unityProjectDir);

                // Read the global Claude config file (honor macConfigPath on macOS)
                string configPath;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    configPath = mcpClient.windowsConfigPath;
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    configPath = string.IsNullOrEmpty(mcpClient.macConfigPath) ? mcpClient.linuxConfigPath : mcpClient.macConfigPath;
                else
                    configPath = mcpClient.linuxConfigPath;

                if (debugLogsEnabled)
                {
                    MCPForUnity.Editor.Helpers.McpLog.Info($"Checking Claude config at: {configPath}", always: false);
                }

                if (!File.Exists(configPath))
                {
                    UnityEngine.Debug.LogWarning($"Claude config file not found at: {configPath}");
                    mcpClient.SetStatus(McpStatus.NotConfigured);
                    return;
                }

                var configJson = File.ReadAllText(configPath);
                dynamic claudeConfig = JsonConvert.DeserializeObject(configJson);

                // Check for "UnityMCP" server in the mcpServers section (current format)
                if (claudeConfig?.mcpServers != null)
                {
                    var servers = claudeConfig.mcpServers;
                    if (servers.UnityMCP != null || servers.unityMCP != null)
                    {
                        // Found MCP for Unity configured
                        mcpClient.SetStatus(McpStatus.Configured);
                        return;
                    }
                }

                // Also check if there's a project-specific configuration for this Unity project (legacy format)
                if (claudeConfig?.projects != null)
                {
                    // Look for the project path in the config
                    foreach (var project in claudeConfig.projects)
                    {
                        string projectPath = project.Name;

                        // Normalize paths for comparison (handle forward/back slash differences)
                        var normalizedProjectPath = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var normalizedProjectDir = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        if (string.Equals(normalizedProjectPath, normalizedProjectDir, StringComparison.OrdinalIgnoreCase) && project.Value?.mcpServers != null)
                        {
                            // Check for "UnityMCP" (case variations)
                            var servers = project.Value.mcpServers;
                            if (servers.UnityMCP != null || servers.unityMCP != null)
                            {
                                // Found MCP for Unity configured for this project
                                mcpClient.SetStatus(McpStatus.Configured);
                                return;
                            }
                        }
                    }
                }

                // No configuration found for this project
                mcpClient.SetStatus(McpStatus.NotConfigured);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Error checking Claude Code config: {e.Message}");
                mcpClient.SetStatus(McpStatus.Error, e.Message);
            }
        }

        private bool IsPythonDetected()
        {
            try
            {
                // Windows-specific Python detection
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // Common Windows Python installation paths
                    string[] windowsCandidates =
                    {
                        @"C:\Python313\python.exe",
                        @"C:\Python312\python.exe",
                        @"C:\Python311\python.exe",
                        @"C:\Python310\python.exe",
                        @"C:\Python39\python.exe",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python313\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python39\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python313\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python312\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python311\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python310\python.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Python39\python.exe"),
                    };

                    foreach (var c in windowsCandidates)
                    {
                        if (File.Exists(c)) return true;
                    }

                    // Try 'where python' command (Windows equivalent of 'which')
                    var psi = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "python",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    var outp = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(2000);
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(outp))
                    {
                        var lines = outp.Split('\n');
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (File.Exists(trimmed)) return true;
                        }
                    }
                }
                else
                {
                    // macOS/Linux detection (existing code)
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
                    string[] candidates =
                    {
                        "/opt/homebrew/bin/python3",
                        "/usr/local/bin/python3",
                        "/usr/bin/python3",
                        "/opt/local/bin/python3",
                        Path.Combine(home, ".local", "bin", "python3"),
                        "/Library/Frameworks/Python.framework/Versions/3.13/bin/python3",
                        "/Library/Frameworks/Python.framework/Versions/3.12/bin/python3",
                    };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c)) return true;
                    }

                    // Try 'which python3'
                    var psi = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/which",
                        Arguments = "python3",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    var outp = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(2000);
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(outp) && File.Exists(outp)) return true;
                }
            }
            catch { }
            return false;
        }
    }
}