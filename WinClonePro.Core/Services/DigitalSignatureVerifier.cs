using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class DigitalSignatureVerifier : IDigitalSignatureVerifier
{
    private readonly IProcessRunner _processRunner;

    public DigitalSignatureVerifier(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<bool> HasValidSignatureAsync(string filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var escapedFilePath = filePath.Replace("'", "''", StringComparison.Ordinal);
        var ps = "-NoProfile -ExecutionPolicy Bypass -Command " +
                 $"\"$sig = Get-AuthenticodeSignature -FilePath '{escapedFilePath}'; " +
                 "if ($sig -and $sig.Status -eq 'Valid') { exit 0 } else { exit 1 }\"";

        try
        {
            await _processRunner.RunAsync(
                "powershell.exe",
                ps,
                new Progress<string>(line => Log.Information("[sigcheck] {Line}", line)),
                ct).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Digital signature validation failed for {FilePath}", filePath);
            return false;
        }
    }
}
