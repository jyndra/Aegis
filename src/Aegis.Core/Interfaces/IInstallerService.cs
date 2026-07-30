using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IInstallerService
{
    Task<InstallResult> InstallAsync(InstallOptions options, CancellationToken cancellationToken = default);
    Task<bool> RestorePoliciesAsync(string? overrideRootPath = null, CancellationToken cancellationToken = default);
}
