namespace Lighthouse.Tests.Services.Registry;

/// <summary>
/// A <see cref="FactAttribute"/> that only runs when the environment variable
/// <c>RUN_REGISTRY_INTEGRATION=1</c> is set. These tests hit real container registries over the
/// network, so they are skipped by default (including in CI) and run on demand:
/// <code>RUN_REGISTRY_INTEGRATION=1 dotnet test --filter Category=RegistryIntegration</code>
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_REGISTRY_INTEGRATION") != "1")
        {
            Skip = "Set RUN_REGISTRY_INTEGRATION=1 to run (hits real container registries over the network).";
        }
    }
}
