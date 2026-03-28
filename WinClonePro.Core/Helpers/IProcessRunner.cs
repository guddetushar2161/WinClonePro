using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Helpers;

public interface IProcessRunner
{
    Task RunAsync(
        string exe,
        string args,
        IProgress<string> output,
        CancellationToken ct,
        int timeoutMinutes = 120);
}

