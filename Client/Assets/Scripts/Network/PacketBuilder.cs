using System.Collections.Generic;
using System.Text;

namespace SROClient.Network
{
    /// <summary>
    /// İstemci tarafı paket oluşturucu. Sunucudaki SROServer.Shared.Packets.PacketBuilder
    /// ile BİREBİR aynı protokolü kullanır: [Opcode (ushort)] [Length (ushort)] [Payload].
    /// Tüm sayısal değerler little-endian yazılır.
    /// </summary>
    public sealed class PacketBuilder
    {
        private const int HeaderSize = 4;

        private readonly ushort _opcode;
        private readonly List<byte> _payload = new List<byte>();

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

        /// <summary>Payload'a 2 byte'lık ushort ekler.</summary>
        public PacketBuilder WriteShort(ushort value)
        {
            _payload.Add((byte)(value & 0xFF));
            _payload.Add((byte)((value >> 8) & 0xFF));
            return this;
        }

        /// <summary>Payload'a 4 byte'lık int ekler.</summary>
        public PacketBuilder WriteInt(int value)
        {
            _payload.Add((byte)(value & 0xFF));
            _payload.Add((byte)((value >> 8) & 0xFF));
            _payload.Add((byte)((value >> 16) & 0xFF));
            _payload.Add((byte)((value >> 24) & 0xFF));
            return this;
        }

        /// <summary>Payload'a 8 byte'lık işaretli long ekler (little-endian).</summary>
        public PacketBuilder WriteLong(long value)
        {
            for (int i = 0; i < 8; i++)
                _payload.Add((byte)((value >> (8 * i)) & 0xFF));
            return this;
        }

        /// <summary>Payload'a 4 byte'lık float ekler.</summary>
        public PacketBuilder WriteFloat(float value)
        {
            byte[] bytes = System.BitConverter.GetBytes(value);
            if (!System.BitConverter.IsLittleEndian)
                System.Array.Reverse(bytes);
            _payload.AddRange(bytes);
            return this;
        }

        /// <summary>Payload'a UTF-8 string ekler: [Uzunluk (ushort)] [String byte'ları].</summary>
        public PacketBuilder WriteString(string value)
        {
            if (value == null) value = string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteShort((ushort)bytes.Length);
            _payload.AddRange(bytes);
            return this;
        }

        /// <summary>Başlık + payload'ı birleştirerek gönderilecek byte dizisini üretir.</summary>
        public byte[] Build()
        {
            ushort length = (ushort)_payload.Count;
            byte[] buffer = new byte[HeaderSize + _payload.Count];

            buffer[0] = (byte)(_opcode & 0xFF);
            buffer[1] = (byte)((_opcode >> 8) & 0xFF);
            buffer[2] = (byte)(length & 0xFF);
            buffer[3] = (byte)((length >> 8) & 0xFF);

            _payload.CopyTo(buffer, HeaderSize);
            return buffer;
        }
    }
}
