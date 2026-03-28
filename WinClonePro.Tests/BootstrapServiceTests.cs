using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.Core.Services;
using Xunit;

namespace WinClonePro.Tests;

public sealed class BootstrapServiceTests
{
    [Fact]
    public async Task PrepareSystemAsync_InstallsMissingAdk_ThenSucceeds()
    {
        var embeddedToolService = new Mock<IEmbeddedToolService>();
        var systemCheckService = new Mock<ISystemCheckService>();
        var dependencyInstallerService = new Mock<IDependencyInstallerService>();

        embeddedToolService.Setup(x => x.ExtractToolsAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddedToolExtractionResult());

        systemCheckService.SetupSequence(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemCheckResult
            {
                IsAdministrator = true,
                DismTool = AvailableTool("dism.exe"),
                DiskPartTool = AvailableTool("diskpart.exe"),
                BcdBootTool = AvailableTool("bcdboot.exe"),
                AdkInstalled = false
            })
            .ReturnsAsync(new SystemCheckResult
            {
                IsAdministrator = true,
                DismTool = AvailableTool("dism.exe"),
                DiskPartTool = AvailableTool("diskpart.exe"),
                BcdBootTool = AvailableTool("bcdboot.exe"),
                AdkInstalled = true
            });

        dependencyInstallerService.Setup(x => x.EnsureAdkDeploymentToolsInstalledAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var bootstrapService = new BootstrapService(
            embeddedToolService.Object,
            systemCheckService.Object,
            dependencyInstallerService.Object);

        var states = new List<BootstrapProgressState>();
        var progress = new Progress<BootstrapProgressState>(state => states.Add(state));

        var result = await bootstrapService.PrepareSystemAsync(progress, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(states, state => state.Stage == BootstrapStage.InstallingComponents);
        Assert.Contains(states, state => state.Stage == BootstrapStage.Ready);
        dependencyInstallerService.Verify(x => x.EnsureAdkDeploymentToolsInstalledAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrepareSystemAsync_FailsWhenCoreToolsRemainMissing()
    {
        var embeddedToolService = new Mock<IEmbeddedToolService>();
        var systemCheckService = new Mock<ISystemCheckService>();
        var dependencyInstallerService = new Mock<IDependencyInstallerService>();

        embeddedToolService.Setup(x => x.ExtractToolsAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddedToolExtractionResult());

        systemCheckService.Setup(x => x.RunAllChecksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemCheckResult
            {
                IsAdministrator = true,
                DismTool = ToolResolution.Missing("dism.exe"),
                DiskPartTool = AvailableTool("diskpart.exe"),
                BcdBootTool = AvailableTool("bcdboot.exe"),
                AdkInstalled = false,
                Errors = new List<string> { "DISM (dism.exe) is missing." }
            });

        var bootstrapService = new BootstrapService(
            embeddedToolService.Object,
            systemCheckService.Object,
            dependencyInstallerService.Object);

        var result = await bootstrapService.PrepareSystemAsync(new Progress<BootstrapProgressState>(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Required Windows deployment tools are unavailable.", result.FailureMessage, StringComparison.Ordinal);
        dependencyInstallerService.Verify(x => x.EnsureAdkDeploymentToolsInstalledAsync(It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ToolResolution AvailableTool(string toolName)
    {
        return new ToolResolution
        {
            ToolName = toolName,
            ResolvedPath = $@"C:\Windows\System32\{toolName}",
            Source = DependencySource.System
        };
    }
}
