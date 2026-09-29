using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Services.Transport.Transports;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service locator for accessing MCP services without dependency injection
    /// </summary>
    public static class MCPServiceLocator
    {
        private static IBridgeControlService _bridgeService;
        private static IClientConfigurationService _clientService;
        private static IPathResolverService _pathService;
        private static ITestRunnerService _testRunnerService;
        private static IPackageUpdateService _packageUpdateService;
        private static IPlatformService _platformService;
        private static IToolDiscoveryService _toolDiscoveryService;
        private static IResourceDiscoveryService _resourceDiscoveryService;
        private static IServerManagementService _serverManagementService;
        private static TransportManager _transportManager;
        private static IPackageDeploymentService _packageDeploymentService;

        /// <summary>Bridge control service that starts and stops the MCP bridge.</summary>
        public static IBridgeControlService Bridge => _bridgeService ??= new BridgeControlService();

        /// <summary>Client configuration service that writes each client's MCP config file.</summary>
        public static IClientConfigurationService Client => _clientService ??= new ClientConfigurationService();

        /// <summary>Resolver for project and package paths used across the editor.</summary>
        public static IPathResolverService Paths => _pathService ??= new PathResolverService();

        /// <summary>Runs Unity Test Runner tests and reports their results.</summary>
        public static ITestRunnerService Tests => _testRunnerService ??= new TestRunnerService();

        /// <summary>Checks for and applies package updates.</summary>
        public static IPackageUpdateService Updates => _packageUpdateService ??= new PackageUpdateService();

        /// <summary>Reports the host platform and its capabilities.</summary>
        public static IPlatformService Platform => _platformService ??= new PlatformService();

        /// <summary>Discovers the MCP tools exposed by this package.</summary>
        public static IToolDiscoveryService ToolDiscovery => _toolDiscoveryService ??= new ToolDiscoveryService();

        /// <summary>Discovers the MCP resources exposed by this package.</summary>
        public static IResourceDiscoveryService ResourceDiscovery => _resourceDiscoveryService ??= new ResourceDiscoveryService();

        /// <summary>Manages the MCP server process (start, stop, status).</summary>
        public static IServerManagementService Server => _serverManagementService ??= new ServerManagementService();

        /// <summary>Coordinates the transport used to talk to the MCP server.</summary>
        public static TransportManager TransportManager => _transportManager ??= new TransportManager();

        /// <summary>Deploys the MCP package into a target project.</summary>
        public static IPackageDeploymentService Deployment => _packageDeploymentService ??= new PackageDeploymentService();

        /// <summary>
        /// Registers a custom implementation for a service (useful for testing)
        /// </summary>
        /// <typeparam name="T">The service interface type</typeparam>
        /// <param name="implementation">The implementation to register</param>
        public static void Register<T>(T implementation) where T : class
        {
            if (implementation is IBridgeControlService b)
                _bridgeService = b;
            else if (implementation is IClientConfigurationService c)
                _clientService = c;
            else if (implementation is IPathResolverService p)
                _pathService = p;
            else if (implementation is ITestRunnerService t)
                _testRunnerService = t;
            else if (implementation is IPackageUpdateService pu)
                _packageUpdateService = pu;
            else if (implementation is IPlatformService ps)
                _platformService = ps;
            else if (implementation is IToolDiscoveryService td)
                _toolDiscoveryService = td;
            else if (implementation is IResourceDiscoveryService rd)
                _resourceDiscoveryService = rd;
            else if (implementation is IServerManagementService sm)
                _serverManagementService = sm;
            else if (implementation is IPackageDeploymentService pd)
                _packageDeploymentService = pd;
            else if (implementation is TransportManager tm)
                _transportManager = tm;
        }

        /// <summary>
        /// Resets all services to their default implementations (useful for testing)
        /// </summary>
        public static void Reset()
        {
            (_bridgeService as IDisposable)?.Dispose();
            (_clientService as IDisposable)?.Dispose();
            (_pathService as IDisposable)?.Dispose();
            (_testRunnerService as IDisposable)?.Dispose();
            (_packageUpdateService as IDisposable)?.Dispose();
            (_platformService as IDisposable)?.Dispose();
            (_toolDiscoveryService as IDisposable)?.Dispose();
            (_resourceDiscoveryService as IDisposable)?.Dispose();
            (_serverManagementService as IDisposable)?.Dispose();
            (_transportManager as IDisposable)?.Dispose();
            (_packageDeploymentService as IDisposable)?.Dispose();

            _bridgeService = null;
            _clientService = null;
            _pathService = null;
            _testRunnerService = null;
            _packageUpdateService = null;
            _platformService = null;
            _toolDiscoveryService = null;
            _resourceDiscoveryService = null;
            _serverManagementService = null;
            _transportManager = null;
            _packageDeploymentService = null;
        }
    }
}
