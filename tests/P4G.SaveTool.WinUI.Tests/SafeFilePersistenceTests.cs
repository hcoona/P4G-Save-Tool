using P4G.SaveTool.WinUI;
using Xunit;

namespace P4G.SaveTool.WinUI.Tests;

public sealed class SafeFilePersistenceTests : IDisposable
{
    private readonly string testDirectoryPath;

    public SafeFilePersistenceTests()
    {
        testDirectoryPath = Path.Combine(
            AppContext.BaseDirectory,
            nameof(SafeFilePersistenceTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectoryPath);
    }

    [Fact]
    public async Task ReplaceFileAsyncCreatesNewFileWhenTargetDoesNotExist()
    {
        string targetPath = GetTargetPath("new-save.bin");
        byte[] bytes = [0x10, 0x20, 0x30, 0x40];

        await SafeFilePersistence.ReplaceFileAsync(targetPath, bytes);

        Assert.True(File.Exists(targetPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(targetPath));
        AssertNoTempFilesRemain();
    }

    [Fact]
    public async Task ReplaceFileAsyncReplacesExistingFileWithNewBytes()
    {
        string targetPath = GetTargetPath("existing-save.bin");
        byte[] originalBytes = [0x01, 0x02, 0x03];
        byte[] replacementBytes = [0xa0, 0xb0, 0xc0, 0xd0];
        await File.WriteAllBytesAsync(targetPath, originalBytes);

        await SafeFilePersistence.ReplaceFileAsync(targetPath, replacementBytes);

        Assert.Equal(replacementBytes, await File.ReadAllBytesAsync(targetPath));
        AssertNoTempFilesRemain();
    }

    [Fact]
    public async Task ReplaceFileAsyncPreservesExistingFileAndCleansTempWhenReplaceFails()
    {
        string targetPath = GetTargetPath("locked-save.bin");
        byte[] originalBytes = [0x11, 0x22, 0x33];
        byte[] replacementBytes = [0xaa, 0xbb, 0xcc];
        await File.WriteAllBytesAsync(targetPath, originalBytes);

        await using (new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(
                () => SafeFilePersistence.ReplaceFileAsync(targetPath, replacementBytes));
        }

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(targetPath));
        AssertNoTempFilesRemain();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReplaceFileAsyncRejectsBlankTargetPath(string targetPath)
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SafeFilePersistence.ReplaceFileAsync(targetPath, [0x01]));

        Assert.Equal("targetPath", exception.ParamName);
    }

    [Fact]
    public async Task ReplaceFileAsyncRejectsNullBytes()
    {
        string targetPath = GetTargetPath("null-bytes.bin");

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => SafeFilePersistence.ReplaceFileAsync(targetPath, null!));

        Assert.Equal("bytes", exception.ParamName);
        Assert.False(File.Exists(targetPath));
        AssertNoTempFilesRemain();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(testDirectoryPath, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
        }
    }

    private string GetTargetPath(string fileName) => Path.Combine(testDirectoryPath, fileName);

    private void AssertNoTempFilesRemain()
    {
        Assert.Empty(Directory.EnumerateFiles(testDirectoryPath, "*.tmp", SearchOption.TopDirectoryOnly));
    }
}
