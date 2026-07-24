using System;
using NUnit.Framework;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;

namespace MCPForUnityTests.Editor.Tools
{
    public class CommandRegistryTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Ensure CommandRegistry is initialized before tests run
            CommandRegistry.Initialize();
        }

        [Test]
        public void GetHandler_ThrowsException_ForUnknownCommand()
        {
            var unknown = "nonexistent_command_that_should_not_exist";

            Assert.Throws<InvalidOperationException>(() =>
            {
                CommandRegistry.GetHandler(unknown);
            }, "Should throw InvalidOperationException for unknown handler");
        }

        [Test]
        public void OptionalPackageTools_AlwaysResolveToAHandler()
        {
            // Whether or not the optional package is installed, the command must dispatch.
            // An absent package yields a placeholder that names the package to install,
            // never "Unknown or unsupported command type".
            foreach (var entry in OptionalPackageTools.CommandToPackage)
            {
                var handler = CommandRegistry.GetHandler(entry.Key);
                Assert.IsNotNull(handler, $"Handler for '{entry.Key}' should not be null");

                var result = handler(new Newtonsoft.Json.Linq.JObject());
                Assert.IsNotNull(result, $"Handler for '{entry.Key}' should return a result for empty params");

                if (result is ErrorResponse error && error.Error.Contains("not installed in this project"))
                {
                    StringAssert.Contains(entry.Value, error.Error,
                        $"Placeholder for '{entry.Key}' should name the required package");
                }
            }
        }

        [Test]
        public void MissingPackageResponse_NamesTheCommandAndPackage()
        {
            var response = OptionalPackageTools.MissingPackageResponse("manage_splines", "com.unity.splines")
                as ErrorResponse;

            Assert.IsNotNull(response, "Missing-package response should be an ErrorResponse");
            Assert.IsFalse(response.Success, "Missing-package response should not report success");
            StringAssert.Contains("manage_splines", response.Error);
            StringAssert.Contains("com.unity.splines", response.Error);
        }

        [Test]
        public void AutoDiscovery_RegistersAllBuiltInTools()
        {
            // Verify that all expected built-in tools are registered by trying to get their handlers
            var expectedTools = new[]
            {
                "manage_asset",
                "manage_editor",
                "manage_gameobject",
                "manage_scene",
                "manage_script",
                "manage_shader",
                "read_console",
                "execute_menu_item",
                "manage_prefabs"
            };

            foreach (var toolName in expectedTools)
            {
                var handler = CommandRegistry.GetHandler(toolName);
                Assert.IsNotNull(handler, $"Handler for '{toolName}' should not be null");

                // Verify the handler is actually callable (returns a result, not throws)
                var emptyParams = new Newtonsoft.Json.Linq.JObject();
                var result = handler(emptyParams);
                Assert.IsNotNull(result, $"Handler for '{toolName}' should return a result even for empty params");
            }
        }
    }
}
