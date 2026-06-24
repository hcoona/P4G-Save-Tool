using System.Buffers.Binary;
using System.Text;

namespace P4G.SaveTool.SaveFormat;

public static class SaveStringCodec
{
    public const int EncodedNameCharacterLength = 9;
    public const int EncodedNameByteLength = EncodedNameCharacterLength * sizeof(ushort);

    public static string DecodeJString(ReadOnlyMemory<byte> source)
    {
        if (source.Length < EncodedNameByteLength)
        {
            throw new ArgumentException("Encoded JString input is too short.", nameof(source));
        }

        StringBuilder builder = new(EncodedNameCharacterLength);
        ReadOnlySpan<byte> span = source.Span;
        for (int index = 0; index < EncodedNameCharacterLength; index++)
        {
            ushort encoded = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index * sizeof(ushort), sizeof(ushort)));
            byte raw = (byte)(((encoded & 0xff00) >> 8) - 0x60);
            char character = (char)raw;
            if (character is not (' ' or '\u00a0' or '\0'))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static string DecodePString(ReadOnlyMemory<byte> source)
    {
        if (source.Length < EncodedNameByteLength)
        {
            throw new ArgumentException("Encoded PString input is too short.", nameof(source));
        }

        StringBuilder builder = new(EncodedNameCharacterLength);
        ReadOnlySpan<byte> span = source.Span;
        for (int index = 0; index < EncodedNameCharacterLength; index++)
        {
            ushort encoded = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(index * sizeof(ushort), sizeof(ushort)));
            if (encoded == 0)
            {
                continue;
            }

            if (encoded is > 33358 and < 33402)
            {
                builder.Append((char)(encoded - 33311));
            }
            else if (encoded is > 33408 and < 33435)
            {
                builder.Append((char)(encoded - 33312));
            }
            else if (encoded > 33079)
            {
                builder.Append((char)(encoded - 33047));
            }
        }

        return builder.ToString();
    }

    public static void EncodeJString(string value, Memory<byte> destination)
    {
        if (destination.Length < EncodedNameByteLength)
        {
            throw new ArgumentException("Encoded JString destination is too short.", nameof(destination));
        }

        Span<byte> span = destination.Span;
        span[..EncodedNameByteLength].Clear();
        for (int index = 0; index < EncodedNameCharacterLength && index < value.Length; index++)
        {
            char character = value[index];
            ushort encoded = character == '\u00a0'
                ? (ushort)0
                : (ushort)(((byte)character + 0x60) << 8 | 0x80);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(index * sizeof(ushort), sizeof(ushort)), encoded);
        }
    }

    public static void EncodePString(string value, Memory<byte> destination)
    {
        if (destination.Length < EncodedNameByteLength)
        {
            throw new ArgumentException("Encoded PString destination is too short.", nameof(destination));
        }

        Span<byte> span = destination.Span;
        span[..EncodedNameByteLength].Clear();
        for (int index = 0; index < EncodedNameCharacterLength && index < value.Length; index++)
        {
            char character = value[index];
            ushort encoded = character switch
            {
                >= '0' and <= 'Z' => (ushort)(33311 + character),
                >= 'a' and <= 'z' => (ushort)(33312 + character),
                >= '!' => (ushort)(33047 + character),
                _ => 0,
            };
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(index * sizeof(ushort), sizeof(ushort)), encoded);
        }
    }
}
