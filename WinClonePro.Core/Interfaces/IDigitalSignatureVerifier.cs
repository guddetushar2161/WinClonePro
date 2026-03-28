using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Interfaces;

public interface IDigitalSignatureVerifier
{
    Task<bool> HasValidSignatureAsync(string filePath, CancellationToken ct);
}
