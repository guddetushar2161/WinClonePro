using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace WinClonePro.Core.Helpers;

[SupportedOSPlatform("windows")]
public sealed class WmiQueryRunner : IWmiQueryRunner
{
    public async Task<List<Dictionary<string, object?>>> QueryAsync(string wql, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wql))
        {
            throw new ArgumentException("WQL cannot be null or whitespace.", nameof(wql));
        }

        // System.Management is sync; wrap in Task.Run to keep async API.
        return await Task.Run(
            () =>
            {
                var results = new List<Dictionary<string, object?>>();

                try
                {
                    using var searcher = new ManagementObjectSearcher(wql);
                    using var collection = searcher.Get();

                    foreach (ManagementObject obj in collection)
                    {
                        ct.ThrowIfCancellationRequested();

                        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (PropertyData prop in obj.Properties)
                        {
                            dict[prop.Name] = prop.Value;
                        }

                        results.Add(dict);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "WMI query failed. wql {Wql}", wql);
                    throw;
                }

                return results;
            },
            ct).ConfigureAwait(false);
    }
}

