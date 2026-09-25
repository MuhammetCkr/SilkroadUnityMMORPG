using System.Text;

namespace SROServer.Shared.Packets;

/// <summary>
/// Byte[] tabanlı özel paket oluşturucu.
/// Paket yapısı: [Opcode (ushort, 2 byte)] [Length (ushort, 2 byte)] [Payload (length byte)]
/// Length alanı yalnızca payload uzunluğunu ifade eder (başlık hariç).
/// Tüm sayısal değerler little-endian olarak yazılır (istemci ve sunucu aynı protokolü kullanır).
/// </summary>
public sealed class PacketBuilder
{
    // Opcode + Length başlığı için sabit boyut (2 + 2 = 4 byte)
    private const int HeaderSize = 4;

    private readonly ushort _opcode;
    private readonly List<byte> _payload = new();

    /// <summary>
    /// Belirtilen opcode ile yeni bir paket oluşturucu başlatır.
    /// </summary>
    /// <param name="opcode">Paketin işlem kodu (bkz. PacketOpcodes).</param>
    public PacketBuilder(ushort opcode)
    {
        _opcode = opcode;
    }

    /// <summary>Payload'a tek bir byte ekler.</summary>
    public PacketBuilder WriteByte(byte value)
    {
        _payload.Add(value);
        return this;
    }

    /// <summary>Payload'a 2 byte'lık işaretsiz tam sayı (ushort) ekler.</summary>
    public PacketBuilder WriteShort(ushort value)
    {
        _payload.Add((byte)(value & 0xFF));
        _payload.Add((byte)((value >> 8) & 0xFF));
        return this;
    }

    /// <summary>Payload'a 4 byte'lık işaretli tam sayı (int) ekler.</summary>
    public PacketBuilder WriteInt(int value)
    {
        _payload.Add((byte)(value & 0xFF));
        _payload.Add((byte)((value >> 8) & 0xFF));
        _payload.Add((byte)((value >> 16) & 0xFF));
        _payload.Add((byte)((value >> 24) & 0xFF));
        return this;
    }

    /// <summary>Payload'a 8 byte'lık işaretli uzun tam sayı (long) ekler.</summary>
    public PacketBuilder WriteLong(long value)
    {
        for (int i = 0; i < 8; i++)
            _payload.Add((byte)((value >> (8 * i)) & 0xFF));
        return this;
    }

    /// <summary>Payload'a 4 byte'lık ondalık sayı (float) ekler.</summary>
    public PacketBuilder WriteFloat(float value)
    {
        // Float, 4 byte'lık ham gösterimine dönüştürülür.
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        _payload.AddRange(bytes);
        return this;
    }

    /// <summary>
    /// Payload'a UTF-8 string ekler. Format: [Uzunluk (ushort)] [String byte'ları].
    /// </summary>
    public PacketBuilder WriteString(string value)
    {
        value ??= string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteShort((ushort)bytes.Length);
        _payload.AddRange(bytes);
        return this;
    }

    /// <summary>
    /// Tüm paketi başlık + payload olarak birleştirip byte dizisine dönüştürür.
    /// </summary>
    public byte[] Build()
    {
        ushort length = (ushort)_payload.Count;
        byte[] buffer = new byte[HeaderSize + _payload.Count];

        // Opcode (2 byte, little-endian)
        buffer[0] = (byte)(_opcode & 0xFF);
        buffer[1] = (byte)((_opcode >> 8) & 0xFF);

        // Length (2 byte, little-endian)
        buffer[2] = (byte)(length & 0xFF);
        buffer[3] = (byte)((length >> 8) & 0xFF);

        // Payload
        _payload.CopyTo(buffer, HeaderSize);

        return buffer;
    }
}
