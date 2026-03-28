using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Helpers;

public sealed class SystemIo : ISystemIo
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool FileExists(string path) => File.Exists(path);

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite) => File.Move(sourcePath, destinationPath, overwrite);

    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct) =>
        File.WriteAllBytesAsync(path, content, ct);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public string? GetPathRoot(string path) => Path.GetPathRoot(path);

    public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);

    public DriveMetrics GetDriveMetrics(string rootPath)
    {
        var di = new DriveInfo(rootPath);
        return new DriveMetrics(di.TotalSize, di.AvailableFreeSpace);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
}

