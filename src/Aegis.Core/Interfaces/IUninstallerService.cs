using Aegis.Core.Models;

namespace Aegis.Core.Interfaces;

public interface IUninstallerService
{
    Task<(bool CanUninstall, string Reason)> CheckCanUninstallAsync(CancellationToken cancellationToken = default);
    Task<UninstallResult> UninstallAsync(string? overrideRootPath = null, bool forceConfirm = false, int confirmationStep = 1, CancellationToken cancellationToken = default);
}
