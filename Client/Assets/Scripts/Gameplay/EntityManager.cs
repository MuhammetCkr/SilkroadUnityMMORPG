using System.Collections.Generic;
using UnityEngine;
using SROClient.Managers;
using SROClient.Models;
using SROClient.Network;

namespace SROClient.Gameplay
{
    /// <summary>
    /// Dünyadaki varlıkların (diğer oyuncular, canavarlar) sahnedeki temsillerini yöneten
    /// singleton. Sunucudan gelen spawn/despawn/move paketlerini işler; GameObject'leri
    /// oluşturur, konumlarını hedeflerine doğru interpolasyonla günceller ve yok eder.
    /// Ayrıca S_INIT_DATA ile yerel oyuncuyu ilklendirir.
    /// </summary>
    public sealed class EntityManager : MonoBehaviour
    {
        /// <summary>Global singleton örneği.</summary>
        public static EntityManager Instance { get; private set; }

        [Header("Prefab'lar")]
        [Tooltip("Diğer oyuncular için prefab")]
        [SerializeField] private GameObject _playerPrefab;

        [Tooltip("Canavarlar için prefab")]
        [SerializeField] private GameObject _monsterPrefab;

        [Header("Interpolasyon")]
        [Tooltip("Uzak varlıkların hedefe yumuşak ilerleme hızı")]
        [SerializeField] private float _lerpSpeed = 10f;

        // UniqueId -> sahne nesnesi + veri.
        private readonly Dictionary<uint, GameObject> _entityObjects = new Dictionary<uint, GameObject>();
        private readonly Dictionary<uint, EntityData> _entityData = new Dictionary<uint, EntityData>();

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
            // Ağ paket işleyicilerini kaydet.
            PacketHandler ph = NetworkManager.Instance.PacketHandler;
            ph.Register(PacketOpcodes.S_INIT_DATA, HandleInitData);
            ph.Register(PacketOpcodes.S_ENTITY_SPAWN, HandleEntitySpawn);
            ph.Register(PacketOpcodes.S_ENTITY_DESPAWN, HandleEntityDespawn);
            ph.Register(PacketOpcodes.S_ENTITY_MOVE, HandleEntityMove);
            ph.Register(PacketOpcodes.S_ENTITY_STOP, HandleEntityStop);
            ph.Register(PacketOpcodes.S_HP_UPDATE, HandleHpUpdate);
            ph.Register(PacketOpcodes.S_MP_UPDATE, HandleMpUpdate);
        }

        private void Update()
        {
            // Uzak varlıkları hedef konumlarına doğru yumuşakça ilerlet.
            foreach (KeyValuePair<uint, EntityData> kv in _entityData)
            {
                if (_entityObjects.TryGetValue(kv.Key, out GameObject go) && go != null)
                {
                    EntityData data = kv.Value;
                    go.transform.position = Vector3.Lerp(
                        go.transform.position, data.TargetPosition, _lerpSpeed * Time.deltaTime);
                }
            }
        }

        // ==================== Paket işleyicileri ====================

        /// <summary>
        /// S_INIT_DATA: yerel oyuncunun başlangıç verisi. LocalPlayer'ı ilklendirir.
        /// </summary>
        private void HandleInitData(PacketReader reader)
        {
            uint uniqueId = (uint)reader.ReadInt();
            string charName = reader.ReadString();
            byte level = reader.ReadByte();
            int hp = reader.ReadInt();
            int maxHp = reader.ReadInt();
            int mp = reader.ReadInt();
            int maxMp = reader.ReadInt();
            float x = reader.ReadFloat();
            float y = reader.ReadFloat();
            float z = reader.ReadFloat();
            ushort regionId = reader.ReadShort();

            if (LocalPlayer.Instance != null)
            {
                LocalPlayer.Instance.Initialize(uniqueId, charName, level, hp, maxHp, mp, maxMp);
                if (LocalPlayer.Instance.MovementController != null)
                {
                    LocalPlayer.Instance.MovementController.transform.position = new Vector3(x, y, z);
                }
            }

            Debug.Log($"[EntityManager] Dünyaya giriş: {charName} (UID={uniqueId}, Region={regionId})");
        }

        /// <summary>
        /// S_ENTITY_SPAWN: yeni bir varlığı sahnede oluşturur.
        /// </summary>
        private void HandleEntitySpawn(PacketReader reader)
        {
            var data = new EntityData();
            data.EntityType = (ClientEntityType)reader.ReadByte();
            data.UniqueId = (uint)reader.ReadInt();
            float x = reader.ReadFloat();
            float y = reader.ReadFloat();
            float z = reader.ReadFloat();
            data.Angle = reader.ReadFloat();
            data.Name = reader.ReadString();
            data.Level = reader.ReadByte();
            data.HP = reader.ReadInt();
            data.MaxHP = reader.ReadInt();

            data.Position = new Vector3(x, y, z);
            data.TargetPosition = data.Position;

            // Yerel oyuncunun kendisi ise tekrar oluşturma.
            if (LocalPlayer.Instance != null && data.UniqueId == LocalPlayer.Instance.UniqueId)
                return;

            SpawnEntityObject(data);
        }

        /// <summary>Verilen veriye göre uygun prefab'tan bir GameObject oluşturur.</summary>
        private void SpawnEntityObject(EntityData data)
        {
            if (_entityObjects.ContainsKey(data.UniqueId))
                return; // Zaten var.

            GameObject prefab = data.EntityType == ClientEntityType.Monster
                ? _monsterPrefab
                : _playerPrefab;

            GameObject go = prefab != null
                ? Instantiate(prefab, data.Position, Quaternion.Euler(0f, data.Angle, 0f))
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            if (prefab == null)
                go.transform.position = data.Position;

            go.name = $"{data.EntityType}_{data.UniqueId}_{data.Name}";

            _entityObjects[data.UniqueId] = go;
            _entityData[data.UniqueId] = data;
        }

        /// <summary>
        /// S_ENTITY_DESPAWN: bir varlığı sahneden kaldırır.
        /// </summary>
        private void HandleEntityDespawn(PacketReader reader)
        {
            uint uniqueId = (uint)reader.ReadInt();

            if (_entityObjects.TryGetValue(uniqueId, out GameObject go))
            {
                if (go != null) Destroy(go);
                _entityObjects.Remove(uniqueId);
            }
            _entityData.Remove(uniqueId);
        }

        /// <summary>
        /// S_ENTITY_MOVE: bir varlığın yeni hedef konumunu ayarlar (Update'te interpolasyon).
        /// </summary>
        private void HandleEntityMove(PacketReader reader)
        {
            uint uniqueId = (uint)reader.ReadInt();
            float x = reader.ReadFloat();
            float y = reader.ReadFloat();
            float z = reader.ReadFloat();
            float angle = reader.ReadFloat();

            if (_entityData.TryGetValue(uniqueId, out EntityData data))
            {
                data.TargetPosition = new Vector3(x, y, z);
                data.Angle = angle;

                if (_entityObjects.TryGetValue(uniqueId, out GameObject go) && go != null)
                    go.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        /// <summary>
        /// S_ENTITY_STOP: bir varlığı verilen konumda durdurur.
        /// </summary>
        private void HandleEntityStop(PacketReader reader)
        {
            uint uniqueId = (uint)reader.ReadInt();
            float x = reader.ReadFloat();
            float y = reader.ReadFloat();
            float z = reader.ReadFloat();

            if (_entityData.TryGetValue(uniqueId, out EntityData data))
            {
                data.TargetPosition = new Vector3(x, y, z);
            }
        }

        /// <summary>
        /// S_HP_UPDATE: bir varlığın HP değeri güncellenir. Yerel oyuncuya aitse
        /// LocalPlayer üzerinden HUD'a yansıtılır.
        /// </summary>
        private void HandleHpUpdate(PacketReader reader)
        {
            uint uniqueId = (uint)reader.ReadInt();
            int hp = reader.ReadInt();
            int maxHp = reader.ReadInt();

            if (LocalPlayer.Instance != null && uniqueId == LocalPlayer.Instance.UniqueId)
            {
                LocalPlayer.Instance.SetHP(hp, maxHp);
            }
            else if (_entityData.TryGetValue(uniqueId, out EntityData data))
            {
                data.HP = hp;
                data.MaxHP = maxHp;
            }
        }

        /// <summary>
        /// S_MP_UPDATE: yerel oyuncunun MP değeri güncellenir.
        /// </summary>
        private void HandleMpUpdate(PacketReader reader)
        {
            uint uniqueId = (uint)reader.ReadInt();
            int mp = reader.ReadInt();
            int maxMp = reader.ReadInt();

            if (LocalPlayer.Instance != null && uniqueId == LocalPlayer.Instance.UniqueId)
            {
                LocalPlayer.Instance.SetMP(mp, maxMp);
            }
        }
    }
}
