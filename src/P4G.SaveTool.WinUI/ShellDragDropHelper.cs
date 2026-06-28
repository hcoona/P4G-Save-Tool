using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.DataTransfer;

namespace P4G.SaveTool.WinUI;

internal static class ShellDragDropHelper
{
    internal static DataPackageOperation GetAcceptedDragOperation(IEnumerable<string?>? paths) =>
        TryGetOpenablePath(paths, out _) ? DataPackageOperation.Copy : DataPackageOperation.None;

    internal static bool TryGetOpenablePath(IEnumerable<string?>? paths, out string openablePath)
    {
        if (paths is not null)
        {
            foreach (string? path in paths)
            {
                return TryGetOpenablePath(path, out openablePath);
            }
        }

        openablePath = string.Empty;
        return false;
    }

    internal static bool TryGetOpenablePath(string? path, out string openablePath)
    {
        if (!string.IsNullOrWhiteSpace(path) &&
            string.Equals(Path.GetExtension(path), ".bin", StringComparison.OrdinalIgnoreCase))
        {
            openablePath = path;
            return true;
        }

        openablePath = string.Empty;
        return false;
    }
}
