using LiteNetLib;
using Serilog;
using SROServer.Auth.Services;
using SROServer.Shared.Constants;
using SROServer.Shared.Models;
using SROServer.Shared.Packets;

namespace SROServer.Auth.Network;

/// <summary>
/// Gelen paketleri opcode'a göre işleyen sınıf.
/// AuthService ve TokenService ile birlikte çalışarak giriş, karakter listesi
/// ve karakter seçimi isteklerini yanıtlar.
/// </summary>
public sealed class PacketHandler
{
    private readonly AuthService _authService;
    private readonly TokenService _tokenService;

    public PacketHandler(AuthService authService, TokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Gelen ham paketi parse eder ve opcode'una göre ilgili işleyiciye yönlendirir.
    /// </summary>
    /// <param name="peer">İsteği gönderen istemci bağlantısı.</param>
    /// <param name="data">Ham paket byte dizisi.</param>
    public async Task HandlePacketAsync(NetPeer peer, byte[] data)
    {
        PacketReader reader;
        try
        {
            reader = new PacketReader(data);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Geçersiz paket alındı, atlanıyor.");
            return;
        }

        switch (reader.Opcode)
        {
            case PacketOpcodes.C_LOGIN_REQUEST:
                await HandleLoginRequestAsync(peer, reader);
                break;

            case PacketOpcodes.C_CHAR_LIST_REQ:
                await HandleCharListRequestAsync(peer, reader);
                break;

            case PacketOpcodes.C_SELECT_CHAR:
                HandleSelectChar(peer, reader);
                break;

            default:
                Log.Warning("Bilinmeyen opcode alındı: 0x{Opcode:X4}", reader.Opcode);
                break;
        }
    }

    /// <summary>
    /// Giriş isteğini işler. Kimlik doğrular, sonuca göre başarı/başarısızlık paketi gönderir.
    /// </summary>
    private async Task HandleLoginRequestAsync(NetPeer peer, PacketReader reader)
    {
        string username = reader.ReadString();
        string password = reader.ReadString();
        string clientVersion = reader.ReadString();

        Log.Information("Giriş isteği: kullanıcı={Username}, sürüm={Version}", username, clientVersion);

        AuthResult result = await _authService.LoginAsync(username, password);

        if (result.Status == AuthStatus.Success)
        {
            // Başarılı: token ve JID döner.
            byte[] response = new PacketBuilder(PacketOpcodes.S_LOGIN_SUCCESS)
                .WriteString(result.Token!)
                .WriteInt(result.Jid)
                .Build();
            Send(peer, response);
            Log.Information("Giriş başarılı: kullanıcı={Username}, JID={Jid}", username, result.Jid);
        }
        else
        {
            // Başarısız veya banlı: hata kodu (0=Failed, 1=Banned) ve mesaj döner.
            byte errorCode = result.Status == AuthStatus.Banned ? (byte)1 : (byte)0;
            byte[] response = new PacketBuilder(PacketOpcodes.S_LOGIN_FAILED)
                .WriteByte(errorCode)
                .WriteString(result.Message)
                .Build();
            Send(peer, response);
            Log.Information("Giriş reddedildi: kullanıcı={Username}, durum={Status}", username, result.Status);
        }
    }

    /// <summary>
    /// Karakter listesi isteğini işler. Token doğrulanır, geçerliyse karakterler gönderilir.
    /// </summary>
    private async Task HandleCharListRequestAsync(NetPeer peer, PacketReader reader)
    {
        string token = reader.ReadString();
        int? jid = _tokenService.ValidateToken(token);

        if (jid is null)
        {
            Log.Warning("Karakter listesi isteği geçersiz token ile reddedildi.");
            byte[] fail = new PacketBuilder(PacketOpcodes.S_LOGIN_FAILED)
                .WriteByte(0)
                .WriteString("Oturum geçersiz. Lütfen tekrar giriş yapın.")
                .Build();
            Send(peer, fail);
            return;
        }

        IEnumerable<Character> characters = await _authService.GetCharacterListAsync(jid.Value);
        List<Character> charList = characters.ToList();

        // Paket: [karakter sayısı (byte)] ardından her karakter için alanlar.
        var builder = new PacketBuilder(PacketOpcodes.S_CHAR_LIST)
            .WriteByte((byte)charList.Count);

        foreach (Character c in charList)
        {
            builder.WriteString(c.CharName16)
                   .WriteByte(c.CurLevel)
                   .WriteByte(c.MaxLevel)
                   .WriteInt(c.HP)
                   .WriteInt(c.MP)
                   .WriteInt(c.RemainStatPoint)
                   .WriteByte(c.PVPState);
        }

        Send(peer, builder.Build());
        Log.Information("Karakter listesi gönderildi: JID={Jid}, adet={Count}", jid.Value, charList.Count);
    }

    /// <summary>
    /// Karakter seçimini işler. Faz 1'de yalnızca seçim onaylanır;
    /// Faz 2'de seçim GameServer'a iletilecektir.
    /// </summary>
    private void HandleSelectChar(NetPeer peer, PacketReader reader)
    {
        string token = reader.ReadString();
        string charName = reader.ReadString();

        int? jid = _tokenService.ValidateToken(token);
        if (jid is null)
        {
            Log.Warning("Karakter seçimi geçersiz token ile reddedildi.");
            return;
        }

        // TODO (Faz 2): Seçilen karakteri GameServer'a devret ve dünyaya giriş başlat.
        byte[] response = new PacketBuilder(PacketOpcodes.S_SELECT_CHAR_OK)
            .WriteString(charName)
            .Build();
        Send(peer, response);
        Log.Information("Karakter seçimi onaylandı: JID={Jid}, karakter={Char}", jid.Value, charName);
    }

    /// <summary>
    /// Bir paketi güvenilir ve sıralı (ReliableOrdered) modda istemciye gönderir.
    /// </summary>
    private static void Send(NetPeer peer, byte[] data)
    {
        peer.Send(data, DeliveryMethod.ReliableOrdered);
    }
}
