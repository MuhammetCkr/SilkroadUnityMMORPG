using System;
using System.Text;

namespace SROClient.Network
{
    /// <summary>
    /// İstemci tarafı paket okuyucu. Sunucudan gelen paketleri parse eder.
    /// SROServer.Shared.Packets.PacketReader ile birebir aynı protokolü kullanır.
    /// </summary>
    public sealed class PacketReader
    {
        private readonly byte[] _buffer;
        private int _position;

        /// <summary>Paketin işlem kodu.</summary>
        public ushort Opcode { get; }

        /// <summary>Payload uzunluğu.</summary>
        public ushort Length { get; }

        public PacketReader(byte[] data)
        {
            _buffer = data ?? throw new ArgumentNullException(nameof(data));
            if (_buffer.Length < 4)
                throw new ArgumentException("Paket başlığı için en az 4 byte gereklidir.", nameof(data));

            Opcode = (ushort)(_buffer[0] | (_buffer[1] << 8));
            Length = (ushort)(_buffer[2] | (_buffer[3] << 8));
            _position = 4;
        }

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _buffer[_position++];
        }

        public ushort ReadShort()
        {
            EnsureAvailable(2);
            ushort value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
            _position += 2;
            return value;
        }

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

        public long ReadLong()
        {
            EnsureAvailable(8);
            long value = 0;
            for (int i = 0; i < 8; i++)
                value |= (long)_buffer[_position + i] << (8 * i);
            _position += 8;
            return value;
        }

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

        public string ReadString()
        {
            ushort len = ReadShort();
            EnsureAvailable(len);
            string value = Encoding.UTF8.GetString(_buffer, _position, len);
            _position += len;
            return value;
        }

        private void EnsureAvailable(int count)
        {
            if (_position + count > _buffer.Length)
                throw new InvalidOperationException("Paket sonuna ulaşıldı: yeterli veri yok.");
        }
    }
}
