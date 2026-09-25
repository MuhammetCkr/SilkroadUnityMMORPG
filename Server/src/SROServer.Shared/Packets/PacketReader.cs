using System.Text;

namespace SROServer.Shared.Packets;

/// <summary>
/// Gelen byte[] paketini parse eden okuyucu sınıfı.
/// PacketBuilder ile üretilen paketleri okur: [Opcode][Length][Payload].
/// Okuma sırası, yazma sırasıyla birebir aynı olmalıdır.
/// </summary>
public sealed class PacketReader
{
    private readonly byte[] _buffer;
    private int _position;

    /// <summary>Paketin işlem kodu (başlıktan okunur).</summary>
    public ushort Opcode { get; }

    /// <summary>Payload uzunluğu (başlıktan okunur).</summary>
    public ushort Length { get; }

    /// <summary>
    /// Ham paket byte dizisini alır, başlığı (opcode + length) parse eder,
    /// okuma konumunu payload başına konumlandırır.
    /// </summary>
    /// <param name="data">Başlık dahil tam paket byte dizisi.</param>
    public PacketReader(byte[] data)
    {
        _buffer = data ?? throw new ArgumentNullException(nameof(data));
        if (_buffer.Length < 4)
            throw new ArgumentException("Paket başlığı için en az 4 byte gereklidir.", nameof(data));

        // Opcode (2 byte, little-endian)
        Opcode = (ushort)(_buffer[0] | (_buffer[1] << 8));
        // Length (2 byte, little-endian)
        Length = (ushort)(_buffer[2] | (_buffer[3] << 8));

        // Okuma payload başından başlar.
        _position = 4;
    }

    /// <summary>Payload'dan tek bir byte okur.</summary>
    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer[_position++];
    }

    /// <summary>Payload'dan 2 byte'lık işaretsiz tam sayı (ushort) okur.</summary>
    public ushort ReadShort()
    {
        EnsureAvailable(2);
        ushort value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
        _position += 2;
        return value;
    }

    /// <summary>Payload'dan 4 byte'lık işaretli tam sayı (int) okur.</summary>
    public int ReadInt()
    {
        EnsureAvailable(4);
        int value = _buffer[_position]
                    | (_buffer[_position + 1] << 8)
                    | (_buffer[_position + 2] << 16)
                    | (_buffer[_position + 3] << 24);
        _position += 4;
        return value;
    }

    /// <summary>Payload'dan 8 byte'lık işaretli uzun tam sayı (long) okur.</summary>
    public long ReadLong()
    {
        EnsureAvailable(8);
        long value = 0;
        for (int i = 0; i < 8; i++)
            value |= (long)_buffer[_position + i] << (8 * i);
        _position += 8;
        return value;
    }

    /// <summary>Payload'dan 4 byte'lık ondalık sayı (float) okur.</summary>
    public float ReadFloat()
    {
        EnsureAvailable(4);
        byte[] bytes = new byte[4];
        Array.Copy(_buffer, _position, bytes, 0, 4);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        _position += 4;
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Payload'dan UTF-8 string okur. Format: [Uzunluk (ushort)] [String byte'ları].
    /// </summary>
    public string ReadString()
    {
        ushort len = ReadShort();
        EnsureAvailable(len);
        string value = Encoding.UTF8.GetString(_buffer, _position, len);
        _position += len;
        return value;
    }

    /// <summary>
    /// İstenen sayıda byte'ın okunabilir olduğunu doğrular; yetersizse hata fırlatır.
    /// </summary>
    private void EnsureAvailable(int count)
    {
        if (_position + count > _buffer.Length)
            throw new InvalidOperationException(
                $"Paket sonuna ulaşıldı: {count} byte okunmak istendi ancak yeterli veri yok.");
    }
}
