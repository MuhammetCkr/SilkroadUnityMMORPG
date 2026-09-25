using System.Collections.Generic;
using UnityEngine;

namespace SROClient.World
{
    /// <summary>
    /// Oyuncunun etrafında 3x3'lük bir arazi parçası (chunk) ızgarası yükleyip boşaltan
    /// bileşen. Silkroad'ın 192 birimlik sektör boyutuna uygun olarak, oyuncu bir parçadan
    /// diğerine geçtiğinde uzaktaki parçalar boşaltılır ve yeni komşu parçalar yüklenir.
    ///
    /// Bu Faz 2 uygulamasında parçalar prosedürel (düz zemin) yer tutucu olarak üretilir;
    /// gerçek arazi/harita verisi sonraki fazlarda entegre edilecektir.
    /// </summary>
    public sealed class TerrainChunkLoader : MonoBehaviour
    {
        [Header("Takip")]
        [Tooltip("Merkez alınacak hedef (yerel oyuncu)")]
        public Transform Target;

        [Header("Parça Ayarları")]
        [Tooltip("Bir arazi parçasının kenar uzunluğu (SRO sektör boyutu = 192)")]
        public float ChunkSize = 192f;

        [Tooltip("Yüklü tutulacak ızgara yarıçapı (1 = 3x3)")]
        public int ViewRadius = 1;

        // Yüklü parçalar: ızgara koordinatı -> GameObject.
        private readonly Dictionary<Vector2Int, GameObject> _loadedChunks =
            new Dictionary<Vector2Int, GameObject>();

        // Oyuncunun en son bulunduğu ızgara koordinatı.
        private Vector2Int _currentChunk = new Vector2Int(int.MinValue, int.MinValue);

        private void Update()
        {
            if (Target == null)
                return;

            Vector2Int chunk = WorldToChunk(Target.position);
            if (chunk != _currentChunk)
            {
                _currentChunk = chunk;
                RefreshChunks(chunk);
            }
        }

        /// <summary>Dünya konumunu ızgara (chunk) koordinatına çevirir.</summary>
        public Vector2Int WorldToChunk(Vector3 worldPos)
        {
            int cx = Mathf.FloorToInt(worldPos.x / ChunkSize);
            int cz = Mathf.FloorToInt(worldPos.z / ChunkSize);
            return new Vector2Int(cx, cz);
        }

        /// <summary>
        /// Merkez parçanın etrafındaki 3x3 ızgarayı yükler, menzil dışındakileri boşaltır.
        /// </summary>
        private void RefreshChunks(Vector2Int center)
        {
            // 1) Gerekli parçaları belirle.
            var needed = new HashSet<Vector2Int>();
            for (int dx = -ViewRadius; dx <= ViewRadius; dx++)
            {
                for (int dz = -ViewRadius; dz <= ViewRadius; dz++)
                {
                    var coord = new Vector2Int(center.x + dx, center.y + dz);
                    needed.Add(coord);
                    if (!_loadedChunks.ContainsKey(coord))
                        LoadChunk(coord);
                }
            }

            // 2) Artık gerekmeyen parçaları boşalt.
            var toUnload = new List<Vector2Int>();
            foreach (Vector2Int coord in _loadedChunks.Keys)
            {
                if (!needed.Contains(coord))
                    toUnload.Add(coord);
            }
            foreach (Vector2Int coord in toUnload)
                UnloadChunk(coord);
        }

        /// <summary>Bir ızgara koordinatı için prosedürel yer tutucu zemin üretir.</summary>
        private void LoadChunk(Vector2Int coord)
        {
            var chunk = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chunk.name = $"Chunk_{coord.x}_{coord.y}";

            // Unity Plane varsayılan 10x10 birimdir; sektör boyutuna ölçekle.
            float scale = ChunkSize / 10f;
            chunk.transform.localScale = new Vector3(scale, 1f, scale);

            // Parçanın merkezini ızgara hücresinin ortasına yerleştir.
            float worldX = coord.x * ChunkSize + ChunkSize * 0.5f;
            float worldZ = coord.y * ChunkSize + ChunkSize * 0.5f;
            chunk.transform.position = new Vector3(worldX, 0f, worldZ);
            chunk.transform.SetParent(transform, true);

            _loadedChunks[coord] = chunk;
        }

        /// <summary>Bir parçayı sahneden kaldırır.</summary>
        private void UnloadChunk(Vector2Int coord)
        {
            if (_loadedChunks.TryGetValue(coord, out GameObject chunk))
            {
                if (chunk != null) Destroy(chunk);
                _loadedChunks.Remove(coord);
            }
        }
    }
}
