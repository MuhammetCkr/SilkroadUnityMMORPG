using System;
using System.Collections.Generic;
using UnityEngine;

namespace SROClient.Network
{
    /// <summary>
    /// Gelen paketleri opcode'a göre ilgili işleyiciye yönlendiren dağıtıcı (dispatcher).
    /// Her opcode için bir Action&lt;PacketReader&gt; kaydedilir.
    /// </summary>
    public sealed class PacketHandler
    {
        // Opcode -> işleyici eşlemesi.
        private readonly Dictionary<ushort, Action<PacketReader>> _handlers =
            new Dictionary<ushort, Action<PacketReader>>();

        /// <summary>
        /// Bir opcode için işleyici kaydeder. Aynı opcode tekrar kaydedilirse üzerine yazılır.
        /// </summary>
        public void Register(ushort opcode, Action<PacketReader> handler)
        {
            _handlers[opcode] = handler;
        }

        /// <summary>Bir opcode için kayıtlı işleyiciyi kaldırır.</summary>
        public void Unregister(ushort opcode)
        {
            _handlers.Remove(opcode);
        }

        /// <summary>
        /// Ham paketi parse eder ve opcode'una uygun işleyiciye yönlendirir.
        /// Kayıtlı işleyici yoksa uyarı loglar.
        /// </summary>
        public void Dispatch(byte[] data)
        {
            PacketReader reader;
            try
            {
                reader = new PacketReader(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PacketHandler] Geçersiz paket: {ex.Message}");
                return;
            }

            if (_handlers.TryGetValue(reader.Opcode, out Action<PacketReader> handler))
            {
                try
                {
                    handler(reader);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PacketHandler] Opcode 0x{reader.Opcode:X4} işlenirken hata: {ex}");
                }
            }
            else
            {
                Debug.LogWarning($"[PacketHandler] Kayıtlı işleyici yok: 0x{reader.Opcode:X4}");
            }
        }
    }
}
