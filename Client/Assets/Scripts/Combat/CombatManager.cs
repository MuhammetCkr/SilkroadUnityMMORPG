using System;
using UnityEngine;
using SROClient.Managers;
using SROClient.Network;

namespace SROClient.Combat
{
    /// <summary>
    /// İstemci savaş durumunu yöneten singleton (Faz 4). Sunucudan gelen
    /// S_ATTACK_RESULT / S_ENTITY_DEATH / S_SKILL_RESULT / S_SKILL_CAST paketlerini işler;
    /// saldırı, yetenek kullanma ve dirilme isteklerini sunucuya gönderir.
    /// UI katmanı (hasar sayıları, HUD) olaylara abone olarak bilgi alır.
    /// </summary>
    public sealed class CombatManager : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static CombatManager Instance { get; private set; }

        // --- Olaylar (UI/efekt katmanı abone olur) ---

        /// <summary>Saldırı sonucu geldi: (attackerId, targetId, damage, isCritical).</summary>
        public event Action<uint, uint, int, bool> OnAttackResult;

        /// <summary>Bir varlık öldü: (entityId, entityType).</summary>
        public event Action<uint, byte> OnEntityDeath;

        /// <summary>Yetenek kullanım sonucu: (skillId, result, currentMp).</summary>
        public event Action<int, byte, int> OnSkillResult;

        /// <summary>Yetenek yayını (görsel efekt için): (casterId, skillId, targetId).</summary>
        public event Action<uint, int, uint> OnSkillCast;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            PacketHandler ph = NetworkManager.Instance.PacketHandler;
            ph.Register(PacketOpcodes.S_ATTACK_RESULT, HandleAttackResult);
            ph.Register(PacketOpcodes.S_ENTITY_DEATH, HandleEntityDeath);
            ph.Register(PacketOpcodes.S_SKILL_RESULT, HandleSkillResult);
            ph.Register(PacketOpcodes.S_SKILL_CAST, HandleSkillCast);
        }

        // ==================== Gelen paket işleyicileri ====================

        /// <summary>
        /// S_ATTACK_RESULT: [attackerId(int)] [targetId(int)] [damage(int)]
        /// [isCritical(byte)] [isTargetDead(byte)].
        /// </summary>
        private void HandleAttackResult(PacketReader reader)
        {
            uint attackerId = (uint)reader.ReadInt();
            uint targetId = (uint)reader.ReadInt();
            int damage = reader.ReadInt();
            bool isCritical = reader.ReadByte() == 1;
            bool isTargetDead = reader.ReadByte() == 1;

            OnAttackResult?.Invoke(attackerId, targetId, damage, isCritical);

            // Hedefin üzerinde yüzen hasar sayısı göster.
            DamageNumber.Spawn(targetId, damage, isCritical);

            if (isTargetDead)
                Debug.Log($"[Combat] Hedef öldü: UID={targetId}");
        }

        /// <summary>S_ENTITY_DEATH: [entityId(int)] [entityType(byte)].</summary>
        private void HandleEntityDeath(PacketReader reader)
        {
            uint entityId = (uint)reader.ReadInt();
            byte entityType = reader.ReadByte();
            OnEntityDeath?.Invoke(entityId, entityType);
            Debug.Log($"[Combat] Varlık öldü: UID={entityId} tip={entityType}");
        }

        /// <summary>S_SKILL_RESULT: [skillId(int)] [result(byte)] [currentMp(int)].</summary>
        private void HandleSkillResult(PacketReader reader)
        {
            int skillId = reader.ReadInt();
            byte result = reader.ReadByte();
            int currentMp = reader.ReadInt();
            OnSkillResult?.Invoke(skillId, result, currentMp);
            Debug.Log($"[Combat] Yetenek sonucu skill={skillId} result={result} MP={currentMp}");
        }

        /// <summary>S_SKILL_CAST: [casterId(int)] [skillId(int)] [targetId(int)].</summary>
        private void HandleSkillCast(PacketReader reader)
        {
            uint casterId = (uint)reader.ReadInt();
            int skillId = reader.ReadInt();
            uint targetId = (uint)reader.ReadInt();
            OnSkillCast?.Invoke(casterId, skillId, targetId);
        }

        // ==================== Sunucuya istek gönderme ====================

        /// <summary>C_ATTACK_REQUEST: hedefe temel saldırı isteği.</summary>
        public async void RequestAttack(uint targetId)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_ATTACK_REQUEST)
                .WriteInt((int)targetId)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>C_SKILL_USE: bir yeteneği hedefe karşı kullanma isteği.</summary>
        public async void RequestUseSkill(int skillId, uint targetId)
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_SKILL_USE)
                .WriteInt(skillId)
                .WriteInt((int)targetId)
                .Build();
            await NetworkManager.Instance.SendAsync(packet);
        }

        /// <summary>C_RESURRECTION: ölümden sonra dirilme isteği.</summary>
        public async void RequestResurrection()
        {
            byte[] packet = new PacketBuilder(PacketOpcodes.C_RESURRECTION).Build();
            await NetworkManager.Instance.SendAsync(packet);
        }
    }
}
