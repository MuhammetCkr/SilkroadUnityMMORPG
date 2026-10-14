using System.Collections.Concurrent;
using LiteNetLib;
using Serilog;
using SROServer.GameServer.Entities;

namespace SROServer.GameServer.Services;

/// <summary>
/// Bir lonca (guild) üyesini temsil eder.
/// </summary>
public sealed class GuildMember
{
    /// <summary>Üyenin hesap/karakter kimliği.</summary>
    public int JID { get; set; }

    /// <summary>Karakter adı.</summary>
    public string CharName { get; set; } = string.Empty;

    /// <summary>Rütbe: 0=Üye, 1=Subay, 2=Lider.</summary>
    public byte Rank { get; set; }

    /// <summary>Üyenin anlık benzersiz kimliği (çevrimiçiyse; yayın için).</summary>
    public uint UniqueId { get; set; }
}

/// <summary>
/// Bir loncayı temsil eder. Üyeler, seviye ve lonca puanı (GP) bilgisini taşır.
/// </summary>
public sealed class Guild
{
    /// <summary>Lonca kimliği.</summary>
    public int Id { get; set; }

    /// <summary>Lonca adı.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lonca üyeleri.</summary>
    public List<GuildMember> Members { get; set; } = new();

    /// <summary>Lonca seviyesi (1-6).</summary>
    public int Level { get; set; } = 1;

    /// <summary>Lonca puanı (Guild Points).</summary>
    public long GP { get; set; }
}

/// <summary>
/// Lonca (guild) iş mantığı: oluşturma, üye ekleme ve loncaya yayın.
/// Veritabanı _Guild tablosu şu an kullanılmadığından loncalar bellek içinde tutulur
/// (ileride kalıcı hale getirilebilir). Asenkron imza, kalıcılık eklendiğinde korunur.
/// </summary>
public sealed class GuildService
{
    private readonly ConcurrentDictionary<int, Guild> _guilds = new();

    // Oyuncu UniqueId -> lonca kimliği (çevrimiçi üyelere yayın için).
    private readonly ConcurrentDictionary<uint, int> _playerToGuild = new();

    private int _nextGuildId;

    /// <summary>Bir loncanın ulaşabileceği maksimum seviye.</summary>
    public const int MaxLevel = 6;

    /// <summary>Aktif lonca sayısı.</summary>
    public int GuildCount => _guilds.Count;

    /// <summary>Kimliğe göre loncayı döner (yoksa null).</summary>
    public Task<Guild?> GetGuildAsync(int guildId)
    {
        _guilds.TryGetValue(guildId, out Guild? guild);
        return Task.FromResult(guild);
    }

    /// <summary>
    /// Kurucuyu lider yaparak yeni bir lonca oluşturur.
    /// </summary>
    public Task<Guild> CreateGuildAsync(PlayerEntity founder, string guildName)
    {
        int id = Interlocked.Increment(ref _nextGuildId);
        var guild = new Guild
        {
            Id = id,
            Name = guildName,
            Level = 1
        };
        guild.Members.Add(new GuildMember
        {
            JID = founder.JID,
            CharName = founder.CharName,
            Rank = 2, // Lider
            UniqueId = founder.UniqueId
        });

        _guilds[id] = guild;
        _playerToGuild[founder.UniqueId] = id;

        Log.Debug("Lonca oluşturuldu: Id={Id}, Ad={Name}, Kurucu={Founder}",
            id, guildName, founder.CharName);
        return Task.FromResult(guild);
    }

    /// <summary>
    /// Bir oyuncuyu loncaya üye (rütbe 0) olarak ekler. Lonca yoksa false döner.
    /// </summary>
    public Task<bool> AddMemberAsync(int guildId, PlayerEntity player)
    {
        if (!_guilds.TryGetValue(guildId, out Guild? guild))
            return Task.FromResult(false);

        // Zaten üyeyse tekrar ekleme.
        if (guild.Members.Any(m => m.JID == player.JID))
            return Task.FromResult(false);

        guild.Members.Add(new GuildMember
        {
            JID = player.JID,
            CharName = player.CharName,
            Rank = 0,
            UniqueId = player.UniqueId
        });
        _playerToGuild[player.UniqueId] = guildId;

        Log.Debug("Loncaya katıldı: {Player} -> Lonca {Id}", player.CharName, guildId);
        return Task.FromResult(true);
    }

    /// <summary>Loncanın çevrimiçi üyelerine paket yayınlar.</summary>
    public Task BroadcastToGuildAsync(int guildId, byte[] packet)
    {
        if (!_guilds.TryGetValue(guildId, out Guild? guild))
            return Task.CompletedTask;

        foreach (GuildMember member in guild.Members)
        {
            if (!_playerToGuild.ContainsKey(member.UniqueId))
                continue;
            // Not: Çevrimiçi NetPeer referansı WorldManager üzerinden çözülür; bu servis
            // yalnızca üye kimliklerini bildiğinden yayın, çağıran katman tarafından
            // (GamePacketHandler) yapılabilir. Burada yer tutucu olarak bırakılmıştır.
        }

        return Task.CompletedTask;
    }

    /// <summary>Oyuncunun ait olduğu loncayı döner (yoksa null).</summary>
    public Guild? GetPlayerGuild(uint playerUniqueId)
    {
        if (_playerToGuild.TryGetValue(playerUniqueId, out int guildId) &&
            _guilds.TryGetValue(guildId, out Guild? guild))
            return guild;
        return null;
    }
}
