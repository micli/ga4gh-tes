using System;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace TesDeployer
{
    public static class AzureEnvironmentExtension
    {
        public static string GetBlobEndPointSuffix(this AzureEnvironment env)
        {
            return ".blob." + env.StorageEndpointSuffix;
        }

        public static bool IsEnvironmentNameAvailable(string envName)
        {
            foreach (var azEnv in AzureEnvironment.KnownEnvironments)
            {
                if (string.Equals(azEnv.Name, envName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

