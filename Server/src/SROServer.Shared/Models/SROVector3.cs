namespace SROServer.Shared.Models;

/// <summary>
/// Unity'den bağımsız, sunucu tarafı 3D konum modeli.
/// (Unity'nin UnityEngine.Vector3'ünü sunucuda kullanamayız; bu, onun karşılığıdır.)
/// </summary>
public struct SROVector3 : IEquatable<SROVector3>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public SROVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Sıfır vektörü (0,0,0).</summary>
    public static SROVector3 Zero => new SROVector3(0f, 0f, 0f);

    /// <summary>
    /// İki nokta arasındaki Öklid (3D) mesafesini hesaplar.
    /// </summary>
    public float DistanceTo(SROVector3 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// İki nokta arasındaki mesafenin karesini döner (karekök maliyetinden kaçınmak için —
    /// yalnızca eşik karşılaştırması yapılıyorsa tercih edilir).
    /// </summary>
    public float SqrDistanceTo(SROVector3 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public bool Equals(SROVector3 other) =>
        X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    public override bool Equals(object? obj) => obj is SROVector3 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
