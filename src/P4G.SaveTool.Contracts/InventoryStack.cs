using System.Diagnostics.CodeAnalysis;

namespace P4G.SaveTool.Contracts;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain term")]
public readonly record struct InventoryStack(ushort ItemId, byte Quantity);
