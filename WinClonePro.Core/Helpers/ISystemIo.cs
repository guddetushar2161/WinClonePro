using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Helpers;

public interface ISystemIo
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool FileExists(string path);
    void MoveFile(string sourcePath, string destinationPath, bool overwrite);
    void WriteAllText(string path, string content);
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct);
    long GetFileLength(string path);
    string? GetPathRoot(string path);
    string? GetDirectoryName(string path);
    DriveMetrics GetDriveMetrics(string rootPath);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive);
}

