using System;

namespace MCPForUnity.Editor.Services
{
    /// <summary>Deploys the MCP for Unity package into a target Unity project and rolls it back on failure.</summary>
    public interface IPackageDeploymentService
    {
        string GetStoredSourcePath();
        void SetStoredSourcePath(string path);
        void ClearStoredSourcePath();

        string GetTargetPath();
        string GetTargetDisplayPath();

        string GetLastBackupPath();
        bool HasBackup();

        PackageDeploymentResult DeployFromStoredSource();
        PackageDeploymentResult RestoreLastBackup();
    }

    /// <summary>Outcome of a package deployment attempt, including the paths involved.</summary>
    public class PackageDeploymentResult
    {
        /// <summary>True when the deployment completed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable summary or failure reason.</summary>
        public string Message { get; set; }

        /// <summary>Path the package was deployed from.</summary>
        public string SourcePath { get; set; }

        /// <summary>Path the package was deployed into.</summary>
        public string TargetPath { get; set; }

        /// <summary>Path of the backup taken before overwriting, when one was made.</summary>
        public string BackupPath { get; set; }
    }
}
