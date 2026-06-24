namespace P4G.SaveTool.WinUI;

internal static class SafeFilePersistence
{
    private const int BufferSize = 81920;

    internal static async Task ReplaceFileAsync(string targetPath, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("A save target path is required.", nameof(targetPath));
        }

        ArgumentNullException.ThrowIfNull(bytes);

        string fullTargetPath = Path.GetFullPath(targetPath);
        string? directoryPath = Path.GetDirectoryName(fullTargetPath);
        string fileName = Path.GetFileName(fullTargetPath);
        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(fileName))
        {
            throw new IOException("The save target path must include a file name.");
        }

        string? tempPath = Path.Combine(directoryPath, $".{fileName}.{Guid.NewGuid():N}.tmp");
        bool tempFileCreated = false;
        try
        {
            await using (FileStream tempStream = new(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                tempFileCreated = true;
                await tempStream.WriteAsync(bytes.AsMemory());
                tempStream.Flush(flushToDisk: true);
            }

            ReplaceOrMove(tempPath, fullTargetPath);
            tempPath = null;
        }
        catch
        {
            if (tempFileCreated)
            {
                TryDeleteTempFile(tempPath);
            }

            throw;
        }
    }

    private static void ReplaceOrMove(string sourcePath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(sourcePath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(sourcePath, targetPath);
    }

    private static void TryDeleteTempFile(string? tempPath)
    {
        if (string.IsNullOrWhiteSpace(tempPath))
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }
}
