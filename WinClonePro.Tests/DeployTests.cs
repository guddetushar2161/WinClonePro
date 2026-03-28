using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.UI.Dialogs;
using WinClonePro.UI.ViewModels;
using Xunit;

namespace WinClonePro.Tests;

public class DeployTests
{
    [Fact]
    public async Task StartDeployCommand_BootDiskSelected_ThrowsSafetyViolationException()
    {
        var diskService = new Mock<IDiskService>();
        var dismService = new Mock<IDismService>();
        var confirmService = new Mock<IConfirmWipeDialogService>();
        var dialogMessageService = new Mock<IDialogMessageService>();

        var vm = new DeployViewModel(diskService.Object, dismService.Object, confirmService.Object, dialogMessageService.Object);

        vm.AvailableDisks.Add(new DiskInfo
        {
            Index = 0,
            Model = "Disk",
            SerialNumber = "ABC",
            SizeGB = 100,
            MediaType = "HDD",
            Status = "OK",
            IsBootDisk = true,
            PartitionCount = 2
        });
        vm.SelectedTargetDisk = vm.AvailableDisks[0];

        var img = new ImageInfo
        {
            Index = 1,
            Name = "Windows",
            Description = "desc",
            SizeBytes = 1,
            CapturedAt = DateTime.UtcNow
        };
        vm.AvailableImages.Add(img);
        vm.SelectedImage = img;

        var tempWim = CreateTempWimPath();
        vm.WimPath = tempWim;

        await Assert.ThrowsAsync<SafetyViolationException>(() => vm.StartDeployCommand.ExecuteAsync(null!));
    }

    [Fact]
    public async Task StartDeployCommand_InvalidWimPath_ThrowsArgumentException()
    {
        var diskService = new Mock<IDiskService>();
        var dismService = new Mock<IDismService>();
        var confirmService = new Mock<IConfirmWipeDialogService>();
        var dialogMessageService = new Mock<IDialogMessageService>();

        var vm = new DeployViewModel(diskService.Object, dismService.Object, confirmService.Object, dialogMessageService.Object);

        vm.AvailableDisks.Add(new DiskInfo { Index = 1, Model = "Disk", SerialNumber = "ABC", SizeGB = 100, IsBootDisk = false });
        vm.SelectedTargetDisk = vm.AvailableDisks[0];

        vm.AvailableImages.Add(new ImageInfo { Index = 1, Name = "img", Description = "d", SizeBytes = 1, CapturedAt = DateTime.UtcNow });
        vm.SelectedImage = vm.AvailableImages[0];

        vm.WimPath = @"C:\this-file-should-not-exist\missing.wim";

        await Assert.ThrowsAsync<ArgumentException>(() => vm.StartDeployCommand.ExecuteAsync(null!));
    }

    [Fact]
    public async Task StartDeployCommand_ConfirmationRequiresWipe_DoesNotProceedWhenNotConfirmed()
    {
        var diskService = new Mock<IDiskService>();
        var dismService = new Mock<IDismService>();
        var confirmService = new Mock<IConfirmWipeDialogService>();
        var dialogMessageService = new Mock<IDialogMessageService>();

        var vm = new DeployViewModel(diskService.Object, dismService.Object, confirmService.Object, dialogMessageService.Object);

        vm.AvailableDisks.Add(new DiskInfo { Index = 2, Model = "Disk", SerialNumber = "ABC", SizeGB = 100, IsBootDisk = false });
        vm.SelectedTargetDisk = vm.AvailableDisks[0];

        vm.AvailableImages.Add(new ImageInfo { Index = 1, Name = "img", Description = "d", SizeBytes = 1, CapturedAt = DateTime.UtcNow });
        vm.SelectedImage = vm.AvailableImages[0];

        vm.WimPath = CreateTempWimPath();
        vm.InjectDrivers = false;

        confirmService
            .Setup(x => x.ConfirmWipeAsync(
                It.IsAny<ConfirmWipeDialogViewModel>(),
                It.IsAny<DiskInfo>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Callback<ConfirmWipeDialogViewModel, DiskInfo, CancellationToken>((vm, _, __) =>
            {
                Assert.False(vm.CanConfirm);
            });

        await vm.StartDeployCommand.ExecuteAsync(null!);

        dismService.Verify(x => x.ApplyImageAsync(It.IsAny<ApplyRequest>(), It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartDeployCommand_ProgressUpdatesFrom0To100()
    {
        var diskService = new Mock<IDiskService>();
        var dismService = new Mock<IDismService>();
        var confirmService = new Mock<IConfirmWipeDialogService>();
        var dialogMessageService = new Mock<IDialogMessageService>();
        dialogMessageService.Setup(x => x.ShowSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dialogMessageService.Setup(x => x.ShowErrorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = new DeployViewModel(diskService.Object, dismService.Object, confirmService.Object, dialogMessageService.Object);

        vm.AvailableDisks.Add(new DiskInfo { Index = 3, Model = "Disk", SerialNumber = "ABC", SizeGB = 100, IsBootDisk = false });
        vm.SelectedTargetDisk = vm.AvailableDisks[0];

        vm.AvailableImages.Add(new ImageInfo { Index = 1, Name = "img", Description = "d", SizeBytes = 1, CapturedAt = DateTime.UtcNow });
        vm.SelectedImage = vm.AvailableImages[0];

        vm.WimPath = CreateTempWimPath();

        confirmService
            .Setup(x => x.ConfirmWipeAsync(It.IsAny<ConfirmWipeDialogViewModel>(), It.IsAny<DiskInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        dismService
            .Setup(x => x.ApplyImageAsync(It.IsAny<ApplyRequest>(), It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .Returns<ApplyRequest, IProgress<int>, CancellationToken>(async (_, progress, ct) =>
            {
                progress.Report(0);
                await Task.Delay(10, ct);
                progress.Report(50);
                await Task.Delay(10, ct);
                progress.Report(100);
                return true;
            });

        await vm.StartDeployCommand.ExecuteAsync(null!);

        Assert.Equal(100, vm.DeployProgress);
    }

    [Fact]
    public async Task StartDeployCommand_DriverInjectionSkippedWhenDisabled()
    {
        var diskService = new Mock<IDiskService>();
        var dismService = new Mock<IDismService>();
        var confirmService = new Mock<IConfirmWipeDialogService>();
        var dialogMessageService = new Mock<IDialogMessageService>();
        dialogMessageService.Setup(x => x.ShowSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        dialogMessageService.Setup(x => x.ShowErrorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var vm = new DeployViewModel(diskService.Object, dismService.Object, confirmService.Object, dialogMessageService.Object);

        vm.AvailableDisks.Add(new DiskInfo { Index = 4, Model = "Disk", SerialNumber = "ABC", SizeGB = 100, IsBootDisk = false });
        vm.SelectedTargetDisk = vm.AvailableDisks[0];

        vm.AvailableImages.Add(new ImageInfo { Index = 2, Name = "img", Description = "d", SizeBytes = 1, CapturedAt = DateTime.UtcNow });
        vm.SelectedImage = vm.AvailableImages[0];

        vm.WimPath = CreateTempWimPath();
        vm.InjectDrivers = false;
        vm.DriverFolderPath = @"C:\does-not-matter";

        confirmService
            .Setup(x => x.ConfirmWipeAsync(It.IsAny<ConfirmWipeDialogViewModel>(), It.IsAny<DiskInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ApplyRequest? capturedRequest = null;
        dismService
            .Setup(x => x.ApplyImageAsync(It.IsAny<ApplyRequest>(), It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .Callback<ApplyRequest, IProgress<int>, CancellationToken>((req, _, __) => capturedRequest = req)
            .ReturnsAsync(true);

        await vm.StartDeployCommand.ExecuteAsync(null!);

        Assert.NotNull(capturedRequest);
        Assert.False(capturedRequest!.InjectDrivers);
    }

    private static string CreateTempWimPath()
    {
        var file = Path.Combine(Path.GetTempPath(), $"winclonepro_{Guid.NewGuid():N}.wim");
        File.WriteAllBytes(file, new byte[] { 0 });
        return file;
    }
}

