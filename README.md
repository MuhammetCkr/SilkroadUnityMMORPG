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
│   │   ├── SROServer.Shared/    # Ortak: paket oluşturucu/okuyucu, opcode'lar, modeller
│   │   ├── SROServer.Auth/      # Kimlik doğrulama sunucusu (Faz 1)
│   │   └── SROServer.GameServer/# Oyun sunucusu (Faz 2 placeholder)
│   └── tests/
│       └── SROServer.Auth.Tests/# xUnit + Moq birim testleri
│
└── Client/                      # Unity proje iskeleti (Unity Editor'da açılır)
    └── Assets/Scripts/
        ├── Network/             # ServerConnection, PacketBuilder/Reader, PacketHandler
        ├── UI/                  # LoginUI, CharacterSelectUI
        ├── Managers/            # GameManager, NetworkManager (singleton)
        └── Models/              # PlayerData, ItemData
```

---

## Veritabanı Eşlemesi

| Model | Kaynak Tablo | Veritabanı |
|-------|--------------|------------|
| `UserEntity` | `TB_User` | `SRO_VT_ACCOUNT` |
| `Character` / `PlayerData` | `_User` | `SRO_VT_SHARD` |
| `ItemData` (Faz 3) | `_Items` | `SRO_VT_SHARD` |

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
| **Faz 2** | Dünya & Hareket (Game Server, varlık senkronizasyonu) | ⏳ Planlandı |
| **Faz 3** | Envanter & Item sistemi | ⏳ Planlandı |
| **Faz 4** | Savaş & Skill mekanikleri | ⏳ Planlandı |
| **Faz 5** | Alchemy, Party, Guild sistemleri | ⏳ Planlandı |

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

## Katkı
Kod **İngilizce**, yorumlar ve dokümantasyon **Türkçe** yazılmıştır. Her faz ayrı bir `feature/` dalında geliştirilip PR ile `main`'e alınır.

## Lisans
Bkz. [LICENSE](LICENSE).
