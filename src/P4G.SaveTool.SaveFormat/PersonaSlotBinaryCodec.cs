using System.Buffers.Binary;
using P4G.SaveTool.Contracts;
using P4G.SaveTool.Domain;

namespace P4G.SaveTool.SaveFormat;

public static class PersonaSlotBinaryCodec
{
    public const int BinaryLength = 33;
    public const int SkillCount = PersonaSlot.SkillCount;

    public static SaveOpenResult<PersonaSlot> Open(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length < BinaryLength)
        {
            SaveDiagnostic diagnostic = new(
                DiagnosticSeverity.Error,
                "P4G001",
                "Persona slot data is incomplete or uses an unsupported format.",
                nameof(PersonaSlot));
            return new SaveOpenResult<PersonaSlot>(null, [diagnostic]);
        }

        return new SaveOpenResult<PersonaSlot>(Read(bytes.Span), []);
    }

    public static SaveWriteResult Write(PersonaSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        byte[] bytes = new byte[BinaryLength];
        Write(slot, bytes);
        return SaveWriteResult.Success(bytes);
    }

    internal static PersonaSlot Read(ReadOnlySpan<byte> source)
    {
        ushort[] skillIds = new ushort[SkillCount];
        for (int index = 0; index < SkillCount; index++)
        {
            skillIds[index] = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(12 + (index * sizeof(ushort)), sizeof(ushort)));
        }

        return new PersonaSlot(
            source[0],
            source[1],
            BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(2, sizeof(ushort))),
            source[4],
            source.Slice(5, 3).ToArray(),
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(8, sizeof(uint))),
            skillIds,
            source[28],
            source[29],
            source[30],
            source[31],
            source[32]);
    }

    private static void Write(PersonaSlot slot, Span<byte> destination)
    {
        destination.Clear();
        destination[0] = slot.ExistsRawByte;
        destination[1] = slot.Unknown0;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(2, sizeof(ushort)), slot.PersonaId);
        destination[4] = slot.Level;

        for (int index = 0; index < PersonaSlot.ReservedAfterLevelLength; index++)
        {
            destination[5 + index] = slot.ReservedAfterLevel[index];
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, sizeof(uint)), slot.TotalExperience);
        for (int index = 0; index < SkillCount; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                destination.Slice(12 + (index * sizeof(ushort)), sizeof(ushort)),
                slot.SkillIds[index]);
        }

        destination[28] = slot.Strength;
        destination[29] = slot.Magic;
        destination[30] = slot.Endurance;
        destination[31] = slot.Agility;
        destination[32] = slot.Luck;
    }
}
