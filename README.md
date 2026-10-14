# SilkroadUnityMMORPG

Silkroad Online'dan **ilham alan**, **Unity** (istemci) ve **.NET 8** (sunucu) ile geliştirilen bir MMORPG projesidir. Orijinal Silkroad veritabanı şemasından (`SRO_VT_ACCOUNT`, `SRO_VT_SHARD`, `SRO_VT_LOG`) türetilmiş modeller kullanır; ancak tamamen özgün bir istemci-sunucu mimarisi üzerine kurulmuştur.

> **Not:** Elimizdeki `.bak` dosyaları yalnızca veritabanı şemasını ve verisini içerir. Oyunun grafik motoru, haritaları, animasyonları ve ağ kodu sıfırdan yazılmaktadır. Bu depo o yeniden yazımın adım adım (fazlar halinde) ilerleyen halidir.

---

## Mimari Genel Bakış

```
İstemci (Unity / C#)  <--- TCP paketleri --->  Auth Server (.NET 8)  ---> SQL Server
                                                     │
                                                     └── (Faz 2) Game Server
```

- **İstemci ↔ Sunucu protokolü:** Özel binary paket formatı — `[Opcode (ushort)] [Length (ushort)] [Payload]`, little-endian.
- **Sunucu ağı:** LiteNetLib (UDP) — Auth Server. İstemci tarafı şu an TCP soket ile örneklenmiştir; protokol (opcode + length + payload) her iki tarafta birebir aynıdır.
- **ORM:** Dapper (hız için).
- **Veritabanı:** SQL Server (mevcut `.bak` yedekleriyle uyumlu).
- **Şifreleme:** BCrypt (parola hash doğrulama).

---

## Proje Yapısı

```
SilkroadUnityMMORPG/
├── Server/                      # .NET 8 sunucu çözümü
│   ├── SROServer.sln
│   ├── src/
│   │   ├── SROServer.Shared/    # Ortak: paket oluşturucu/okuyucu, opcode'lar, modeller (SROVector3, SectorPosition, ItemData, InventorySlot)
│   │   ├── SROServer.Auth/      # Kimlik doğrulama sunucusu (Faz 1)
│   │   └── SROServer.GameServer/# Oyun sunucusu (Faz 2-5) — Entities, World, Services, Repositories, Cache, Network
│   └── tests/
│       ├── SROServer.Auth.Tests/       # Auth birim testleri (xUnit + Moq)
│       └── SROServer.GameServer.Tests/ # Dünya/hareket/envanter/savaş/alkimi/skill birim testleri (xUnit + Moq)
│
└── Client/                      # Unity proje iskeleti (Unity Editor'da açılır)
    └── Assets/Scripts/
        ├── Network/             # ServerConnection (reconnect), PacketBuilder/Reader, PacketHandler
        ├── Gameplay/            # CharacterMovementController, CameraController, EntityManager, LocalPlayer
        ├── Inventory/           # InventoryManager (singleton), InventorySlotData (Faz 3)
        ├── Combat/              # CombatManager (singleton), SkillBar, SkillSlotUI, DamageNumber (Faz 4)
        ├── World/               # TerrainChunkLoader, ClientSectorManager
        ├── UI/                  # LoginUI, CharacterSelectUI, HUD/, Inventory/, Alchemy/ (AlchemyUI), Party/ (PartyUI), Guild/ (GuildUI)
        ├── Managers/            # GameManager, NetworkManager (singleton)
        └── Models/              # PlayerData, ItemData, EntityData
```

---

## Veritabanı Eşlemesi

| Model | Kaynak Tablo | Veritabanı |
|-------|--------------|------------|
| `UserEntity` | `TB_User` | `SRO_VT_ACCOUNT` |
| `Character` / `PlayerData` | `_User` | `SRO_VT_SHARD` |
| `ItemData` / `InventorySlot` (Faz 3) | `_Items` + `_Inventory` | `SRO_VT_SHARD` |
| `ItemReference` (Faz 3) | `_RefObjCommon` + `_RefObjItem` | `SRO_VT_SHARD` |
| `MagicOptReference` (Faz 3) | `_RefMagicOpt` | `SRO_VT_SHARD` |

---

## Kurulum

### Gereksinimler
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (mevcut `.bak` yedeklerini geri yükleyin: `SRO_VT_ACCOUNT`, `SRO_VT_SHARD`, `SRO_VT_LOG`)
- [Unity 2022.3 LTS veya üzeri](https://unity.com/) (istemci için)

### Sunucuyu Derleme ve Çalıştırma

```bash
cd Server
dotnet restore
dotnet build -c Release
dotnet test                       # Birim testlerini çalıştır
dotnet run --project src/SROServer.Auth
```

Bağlantı ayarları `Server/src/SROServer.Auth/appsettings.json` içinden yapılandırılır (portlar, veritabanı bağlantı dizeleri).

### İstemciyi Açma
1. Unity Hub üzerinden `Client/` klasörünü bir Unity projesi olarak açın.
2. `Assets/Scripts` altındaki scriptler hazırdır; `Login`, `CharacterSelect` ve `Game` sahnelerini oluşturup Build Settings'e ekleyin.
3. `NetworkManager` bileşenindeki `Host` / `Port` alanlarını sunucuya göre ayarlayın (varsayılan `127.0.0.1:15000`).

---

## Faz Planı

| Faz | Kapsam | Durum |
|-----|--------|-------|
| **Faz 1** | Auth Server + Unity Login/Karakter Seçim ekranı | ✅ Bu depoda |
| **Faz 2** | Dünya & Hareket (Game Server, varlık senkronizasyonu) | ✅ Bu depoda |
| **Faz 3** | Envanter & Item sistemi | ✅ Bu depoda |
| **Faz 4** | Savaş & Skill mekanikleri | ✅ Bu depoda |
| **Faz 5** | Alchemy, Party, Guild sistemleri | ✅ Bu depoda |

---

## Faz 1 — Tamamlananlar
- ✅ Özel binary paket protokolü (`PacketBuilder` / `PacketReader`) — sunucu ve istemcide birebir aynı.
- ✅ LiteNetLib tabanlı Auth ağ sunucusu.
- ✅ Dapper ile kullanıcı/karakter repository.
- ✅ BCrypt parola doğrulama + Guid tabanlı oturum tokenı (24 saat TTL).
- ✅ Giriş, karakter listesi ve karakter seçimi paket akışı.
- ✅ Unity istemcisi: `LoginUI`, `CharacterSelectUI`, `NetworkManager`, `GameManager`.
- ✅ xUnit + Moq birim testleri (5 test, tümü geçiyor).

---

## Faz 2 — Tamamlananlar
### Sunucu (SROServer.GameServer)
- ✅ LiteNetLib tabanlı oyun ağ sunucusu (`GameNetworkManager`, varsayılan port 15001) — bağlantı el sıkışması, oyuncu yaşam döngüsü.
- ✅ Bölge (sektör) tabanlı dünya yönetimi: `WorldManager` (singleton), `Region` (varlık giriş/çıkış olayları, broadcast), `SectorManager` (bölge geçişleri).
- ✅ Silkroad 192 birimlik sektör sistemi: `SROVector3` ve `SectorPosition` (dünya ↔ sektör dönüşümü) — `SROServer.Shared`.
- ✅ Varlık modeli: `EntityBase`, `PlayerEntity`, `MonsterEntity`.
- ✅ Otoriter hareket servisi (`MovementService`): mesafe doğrulaması (anti-cheat), bölge geçişi tespiti, throttle'lı (5sn) pozisyon kaydı.
- ✅ Mob üretim servisi (`SpawnService`) + veritabanı repository'leri (`CharacterRepository`, `SpawnRepository`, Dapper).
- ✅ Paket işleme (`GamePacketHandler`): move/stop/sector istekleri; spawn/despawn/move/init paket üretimi.
- ✅ Sabit adımlı oyun döngüsü (varsayılan 100ms tick) ve graceful shutdown (`Program.cs`).
- ✅ xUnit + Moq birim testleri (`SROServer.GameServer.Tests`, 12 test, tümü geçiyor).

### İstemci (Unity)
- ✅ `CharacterMovementController`: WASD + tıkla-git hareket, `CharacterController` ile fizik, sunucuya throttle'lı `C_MOVE_REQUEST`.
- ✅ `CameraController`: üçüncü şahıs takip kamerası (tekerlek zoom, sağ tık orbit, yumuşak takip).
- ✅ `EntityManager`: uzak varlık spawn/despawn/move interpolasyonu; `S_INIT_DATA` ve HP/MP güncellemeleri.
- ✅ `LocalPlayer`: yerel oyuncu durumu + HP/MP/seviye değişim olayları.
- ✅ HUD: `HUDManager` (HP/MP çubukları, ad/seviye), `MinimapController` (tepeden bakışlı mini harita).
- ✅ Dünya: `TerrainChunkLoader` (3x3 parça yükleme), `ClientSectorManager` (`C_SECTOR_CHANGE` bildirimi).
- ✅ `ServerConnection`: üstel geri çekilmeli yeniden bağlanma (3 deneme).

> Not: Unity istemci scriptleri Unity Editor içinde derlenir; sunucu çözümü `.NET 8 SDK` ile bu depoda 0 hata ile derlenir.

## Faz 3 — Tamamlananlar
### Sunucu (SROServer.GameServer)
- ✅ Item veri modelleri: `ItemData` (item örneği — RefItemID, OptLevel, büyü seçenekleri, dayanıklılık, adet) ve `InventorySlot` (slot ↔ item eşlemesi) — `SROServer.Shared`.
- ✅ `ItemReferenceCache` (singleton): `_RefObjCommon`, `_RefObjItem` ve `_RefMagicOpt` referans tablolarını bellekte önbelleğe alır (`ConcurrentDictionary`), item adı/fiyat/tip çözümlemesi sağlar.
- ✅ `InventoryRepository` (Dapper, transaction'lı): envanter yükleme, slot güncelleme/temizleme, item taşıma ve altın güncelleme — gerçek `_Inventory` / `_Items` tabloları üzerinden.
- ✅ `InventoryService`: item taşıma (slot swap/merge), kullanma (iksir ile HP yenileme), yere bırakma, toplam ağırlık hesaplama ve aşırı yük kontrolü; envanter/slot güncelleme paketleri üretir.
- ✅ `ShopService`: NPC dükkânından item satın alma (altın + slot doğrulaması) ve satma (satış fiyatı = fiyat / 2).
- ✅ Paket işleyicileri (`GamePacketHandler`): envanter taşıma, item kullanma, item bırakma, dükkân alma/satma istekleri + dünyaya katılırken envanter gönderimi.
- ✅ Paket altyapısı: `PacketBuilder.WriteLong` / `PacketReader.ReadLong` (64-bit item/altın değerleri) ve Faz 3 opcode'ları.
- ✅ xUnit + Moq birim testleri (`InventoryServiceTests`, `ItemReferenceCacheTests` — 9 yeni test; `SROServer.GameServer.Tests` toplam 21 test, tümü geçiyor).

### İstemci (Unity)
- ✅ `InventoryManager` (singleton): 112 slotluk envanter durumu, altın takibi, sunucu paket işleyicileri; `OnSlotUpdated` / `OnSlotCleared` / `OnGoldUpdated` olayları.
- ✅ `InventoryUI`: `I` tuşu ile envanter panelini aç/kapat, slot ızgarası yönetimi.
- ✅ `InventorySlotUI`: slot görselleştirme, sürükle-bırak ve tooltip arayüzleri (`IBeginDragHandler`, `IDropHandler`, `IPointerEnterHandler`).
- ✅ `DragDropHandler` (singleton): slotlar arası sürükle-bırak taşıma ve sunucuya taşıma isteği gönderimi.
- ✅ `ItemTooltip` (singleton): item üzerine gelince ad/açıklama/istatistik gösterimi.

> Not: Unity istemci scriptleri Unity Editor içinde derlenir; sunucu çözümü `.NET 8 SDK` ile bu depoda 0 hata ile derlenir.

---

## Faz 4 — Tamamlananlar
### Sunucu (SROServer.GameServer)
- ✅ Savaş veri modelleri: `SkillData` (yetenek referansı — kod/ad, ustalık, gereken seviye, MP, bekleme süresi, menzil, hasar, hedef türü) ve `CombatResult` (saldırı sonucu — hasar, kritik, ölüm) — `SROServer.Shared`.
- ✅ `SkillReferenceCache` (singleton): gerçek `_RefSkill` (+ `_RefSkillMastery`) tablolarını (`Basic_Code`, `Basic_Name`, `Consume_MP`, `Action_CoolTime`, `Param1` vb.) bellekte önbelleğe alır; yalnızca aktif (`Service=1`) yetenekleri yükler.
- ✅ `CombatService`: Silkroad'dan esinlenen hasar formülü (`(STR*1.5 + INT*0.5) * yetenekÇarpanı`), savunma azaltma (en fazla %90), kritik vuruş (%5 + Luck), saldırı işleme, canavar AI saldırısı ve XP/level-up yönetimi (seviye başına HP/MP artışı + kalıcılık).
- ✅ `SkillService`: yetenek kullanım doğrulaması (bilinen yetenek, seviye, bekleme süresi, MP), cooldown yönetimi ve `CombatService` ile hasar entegrasyonu.
- ✅ Paket işleyicileri (`GamePacketHandler`): temel saldırı, yetenek kullanma, dirilme istekleri; varlık ölümü bildirimi ve (canavarsa) 30 sn gecikmeli respawn zamanlama (`SpawnService.ScheduleRespawnAsync`).
- ✅ `PlayerEntity`/`MonsterEntity` savaş alanları (STR, INT, Luck, Defense, Exp, öğrenilen yetenekler/cooldown'lar; canavar için AttackPower, ExpReward, RespawnDelaySeconds) + `WorldManager.GetEntity` ve Faz 4 opcode'ları.
- ✅ xUnit + Moq birim testleri (`CombatServiceTests` — 4 test; sabit tohumlu RNG ile belirlenimci).

### İstemci (Unity)
- ✅ `CombatManager` (singleton): `S_ATTACK_RESULT` / `S_ENTITY_DEATH` / `S_SKILL_RESULT` / `S_SKILL_CAST` işleme; saldırı, yetenek ve dirilme isteği gönderimi; UI için olaylar.
- ✅ `SkillBar` + `SkillSlotUI`: 1-8 kısayol tuşlu yetenek çubuğu, bekleme süresi (cooldown) radial kaplaması ve seçili hedefe yetenek kullanımı.
- ✅ `DamageNumber`: hedef üzerinde yüzen, süzülerek saydamlaşan hasar sayısı (kritik vuruşlar vurgulu).
- ✅ `LocalPlayer.SelectedTargetId`: saldırı ve hedefli yetenekler için seçili hedef takibi.

---

## Faz 5 — Tamamlananlar
### Sunucu (SROServer.GameServer)
- ✅ `AlchemyService`: Silkroad'dan esinlenen eşya güçlendirme (plus) sistemi — seviyeye göre azalan başarı oranları (+0→+1 %80 ... +11→+12 %3), şanslı taş bonusu (+%10), +5 ve üzeri başarısızlıkta eşya yok olması, maksimum +12; güçlendirme taşı tüketimi ve kalıcılık.
- ✅ `PartyService`: en fazla 8 üyeli grup (normal/büyük grup türü) oluşturma, davet, çıkarma, dağıtma ve gruba yayın (`BroadcastToParty`).
- ✅ `GuildService`: bellek içi lonca yönetimi (oluşturma, üye ekleme, rütbe: Üye/Subay/Lider, seviye 1-6, GP) — `_Guild` tablosu olmadan geliştirme modunda çalışır.
- ✅ Paket işleyicileri (`GamePacketHandler`): alkimi güçlendirme, grup oluşturma/davet, lonca oluşturma istekleri + `S_PARTY_DATA` / `S_GUILD_DATA` / `S_ALCHEMY_RESULT` paket üretimi + Faz 5 opcode'ları.
- ✅ xUnit + Moq birim testleri (`AlchemyServiceTests`, `SkillServiceTests` — 6 test; `SROServer.GameServer.Tests` toplam 31 test, tümü geçiyor).

### İstemci (Unity)
- ✅ `AlchemyUI`: eşya + güçlendirme taşı seçimi, güçlendirme isteği ve `S_ALCHEMY_RESULT` sonucunun (başarı/başarısızlık/yok olma) gösterimi.
- ✅ `PartyUI`: grup oluşturma/davet, üye listesi (ad, seviye, HP/MP) ve `S_PARTY_DATA` / `S_PARTY_UPDATE` ile canlı tazeleme.
- ✅ `GuildUI`: lonca oluşturma, lonca adı/seviyesi ve üye listesi (ad + rütbe) gösterimi; `S_GUILD_DATA` işleme.

---

## Faz Özeti

| Faz | Sunucu | İstemci | Testler | Durum |
|-----|--------|---------|---------|-------|
| **Faz 1** — Auth & Giriş | Auth ağ sunucusu, repository, BCrypt + token | `LoginUI`, `CharacterSelectUI`, `NetworkManager` | 5 (Auth) | ✅ |
| **Faz 2** — Dünya & Hareket | `WorldManager`, `MovementService`, `SpawnService` | `CharacterMovementController`, `EntityManager`, HUD | 12 | ✅ |
| **Faz 3** — Envanter & Item | `InventoryService`, `ShopService`, `ItemReferenceCache` | `InventoryManager`, `InventoryUI`, sürükle-bırak | +9 (toplam 21) | ✅ |
| **Faz 4** — Savaş & Skill | `CombatService`, `SkillService`, `SkillReferenceCache` | `CombatManager`, `SkillBar`, `DamageNumber` | +4 | ✅ |
| **Faz 5** — Alkimi, Party, Guild | `AlchemyService`, `PartyService`, `GuildService` | `AlchemyUI`, `PartyUI`, `GuildUI` | +6 (toplam 31) | ✅ |

> Toplam: **36 birim testi** (31 GameServer + 5 Auth), tümü geçiyor. Sunucu çözümü `.NET 8 SDK` ile 0 hata / 0 uyarı ile derlenir.

### Oyun Sunucusunu Çalıştırma
```bash
cd Server
dotnet run --project src/SROServer.GameServer   # Varsayılan port 15001
```
Ayarlar `Server/src/SROServer.GameServer/appsettings.json` içindedir (`GamePort`, `TickRateMs`, veritabanı bağlantı dizesi, `PreloadRegions`).

---

## Katkı
Kod **İngilizce**, yorumlar ve dokümantasyon **Türkçe** yazılmıştır. Her faz ayrı bir `feature/` dalında geliştirilip PR ile `main`'e alınır.

## Lisans
Bkz. [LICENSE](LICENSE).
