using System.Collections.Concurrent;

namespace SROServer.Auth.Services;

/// <summary>
/// Guid tabanlı basit oturum (session) token yönetimi.
/// Token -> JID eşlemesini ve her tokenın oluşturulma zamanını bellek içinde tutar.
/// Token geçerlilik süresi (TTL): 24 saat.
/// Not: Üretim ortamında dağıtık bir depo (ör. Redis) tercih edilmelidir.
/// </summary>
public sealed class TokenService
{
    // Token geçerlilik süresi.
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(24);

    // Thread-safe eşleme: token -> (JID, oluşturulma zamanı).
    private readonly ConcurrentDictionary<string, (int Jid, DateTime CreatedAt)> _tokens = new();

    /// <summary>
    /// Belirtilen hesap için yeni bir oturum tokenı üretir ve saklar.
    /// </summary>
    /// <param name="jid">Hesap kimliği (JID).</param>
    /// <returns>Üretilen benzersiz token.</returns>
    public string CreateToken(int jid)
    {
        string token = Guid.NewGuid().ToString("N");
        _tokens[token] = (jid, DateTime.UtcNow);
        return token;
    }

    /// <summary>
    /// Tokenı doğrular. Geçerli ve süresi dolmamışsa ilgili JID'yi döner.
    /// Süresi dolmuşsa token temizlenir ve null döner.
    /// </summary>
    /// <param name="token">Doğrulanacak token.</param>
    /// <returns>Geçerliyse JID, aksi halde null.</returns>
    public int? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token) || !_tokens.TryGetValue(token, out var entry))
            return null;

        // TTL kontrolü — süresi dolan tokenı temizle.
        if (DateTime.UtcNow - entry.CreatedAt > TokenTtl)
        {
            _tokens.TryRemove(token, out _);
            return null;
        }

        return entry.Jid;
    }

    /// <summary>
    /// Bir tokenı geçersiz kılar (ör. çıkış yapıldığında).
    /// </summary>
    /// <param name="token">İptal edilecek token.</param>
    public void RevokeToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
            _tokens.TryRemove(token, out _);
    }
}
