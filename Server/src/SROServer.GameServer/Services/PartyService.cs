using System.Collections.Concurrent;
using LiteNetLib;
using Serilog;
using SROServer.GameServer.Entities;

namespace SROServer.GameServer.Services;

/// <summary>Grup (party) türü.</summary>
public enum PartyType
{
    /// <summary>Normal grup (en fazla 8 üye).</summary>
    Normal,

    /// <summary>Büyük grup (farklı sunucularda daha fazla üye desteği için yer tutucu).</summary>
    BigParty
}

/// <summary>
/// Bir oyuncu grubunu temsil eder. Üyeler, lider ve grup türü bilgisini taşır.
/// </summary>
public sealed class Party
{
    /// <summary>Grup kimliği (PartyService tarafından atanır).</summary>
    public int Id { get; set; }

    /// <summary>Grup üyeleri (lider dahil, en fazla 8).</summary>
    public List<PlayerEntity> Members { get; set; } = new();

    /// <summary>Grup lideri.</summary>
    public PlayerEntity Leader { get; set; } = null!;

    /// <summary>Grup türü.</summary>
    public PartyType Type { get; set; } = PartyType.Normal;
}

/// <summary>
/// Grup (party) iş mantığı: grup oluşturma, davet, üye çıkarma ve gruba yayın.
/// Gruplar bellek içinde tutulur (oturum ömürlüdür).
/// </summary>
public sealed class PartyService
{
    private readonly ConcurrentDictionary<int, Party> _parties = new();

    // Oyuncu UniqueId -> ait olduğu grup kimliği (hızlı arama için).
    private readonly ConcurrentDictionary<uint, int> _playerToParty = new();

    private int _nextPartyId;

    /// <summary>Bir grup için en fazla üye sayısı.</summary>
    public const int MaxMembers = 8;

    /// <summary>Aktif grup sayısı.</summary>
    public int PartyCount => _parties.Count;

    /// <summary>
    /// Verilen oyuncuyu lider yaparak yeni bir grup oluşturur.
    /// </summary>
    public Party CreateParty(PlayerEntity leader)
    {
        // Oyuncu zaten bir gruptaysa önce o gruptan çıkar.
        Party? existing = GetPlayerParty(leader.UniqueId);
        if (existing != null)
            RemovePlayer(existing, leader.UniqueId);

        int id = Interlocked.Increment(ref _nextPartyId);
        var party = new Party
        {
            Id = id,
            Leader = leader,
            Type = PartyType.Normal
        };
        party.Members.Add(leader);

        _parties[id] = party;
        _playerToParty[leader.UniqueId] = id;

        Log.Debug("Grup oluşturuldu: Id={Id}, Lider={Leader}", id, leader.CharName);
        return party;
    }

    /// <summary>
    /// Bir oyuncuyu gruba ekler. Grup doluysa veya oyuncu zaten bir gruptaysa false döner.
    /// </summary>
    public bool InvitePlayer(Party party, PlayerEntity target)
    {
        if (party.Members.Count >= MaxMembers)
            return false;

        if (GetPlayerParty(target.UniqueId) != null)
            return false; // Zaten bir grupta.

        party.Members.Add(target);
        _playerToParty[target.UniqueId] = party.Id;

        Log.Debug("Gruba katıldı: {Target} -> Grup {Id}", target.CharName, party.Id);
        return true;
    }

    /// <summary>
    /// Bir oyuncuyu gruptan çıkarır. Lider çıkarsa liderlik devredilir; grup tek kişiye
    /// düşerse veya boşalırsa dağıtılır.
    /// </summary>
    public bool RemovePlayer(Party party, uint playerUniqueId)
    {
        PlayerEntity? member = party.Members.FirstOrDefault(m => m.UniqueId == playerUniqueId);
        if (member == null)
            return false;

        party.Members.Remove(member);
        _playerToParty.TryRemove(playerUniqueId, out _);

        // Lider ayrıldıysa liderliği ilk üyeye devret.
        if (party.Leader.UniqueId == playerUniqueId && party.Members.Count > 0)
            party.Leader = party.Members[0];

        // Grup tek kişiye düştü veya boşaldı: dağıt.
        if (party.Members.Count <= 1)
            Disband(party);

        return true;
    }

    /// <summary>Grubu tamamen dağıtır (tüm üye eşlemelerini temizler).</summary>
    public void Disband(Party party)
    {
        foreach (PlayerEntity member in party.Members)
            _playerToParty.TryRemove(member.UniqueId, out _);

        party.Members.Clear();
        _parties.TryRemove(party.Id, out _);

        Log.Debug("Grup dağıtıldı: Id={Id}", party.Id);
    }

    /// <summary>Bir grubun tüm bağlı üyelerine paket yayınlar.</summary>
    public void BroadcastToParty(Party party, byte[] packet)
    {
        foreach (PlayerEntity member in party.Members)
        {
            NetPeer? peer = member.Connection;
            if (peer != null && peer.ConnectionState == ConnectionState.Connected)
                peer.Send(packet, DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>Oyuncunun ait olduğu grubu döner (yoksa null).</summary>
    public Party? GetPlayerParty(uint playerUniqueId)
    {
        if (_playerToParty.TryGetValue(playerUniqueId, out int partyId) &&
            _parties.TryGetValue(partyId, out Party? party))
            return party;
        return null;
    }

    /// <summary>Kimliğe göre grup döner (yoksa null).</summary>
    public Party? GetParty(int partyId)
        => _parties.TryGetValue(partyId, out Party? party) ? party : null;
}
