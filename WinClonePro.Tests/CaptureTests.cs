using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.UI.ViewModels;
using Xunit;

namespace WinClonePro.Tests;

public class CaptureTests
{
    [Fact]
    public async Task SelectingDiskWithoutOS_MustFail()
    {
        var diskService = new Mock<IDiskService>();
        var dismService = new Mock<IDismService>();

        var vm = new CaptureViewModel(diskService.Object, dismService.Object);
        vm.AvailableDisks.Clear();

        vm.AvailableDisks.Add(new DiskInfo
        {
            Index = 0,
            Model = "Disk",
            SerialNumber = "ABC",
            SizeGB = 100,
            ContainsOS = false,
            SystemDriveLetter = ""
        });
        vm.SelectedSourceDisk = vm.AvailableDisks[0];
        vm.OutputPath = "C:\\temp";
        vm.ImageName = "test";

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.StartCaptureCommand.ExecuteAsync(null!));
    }
}

