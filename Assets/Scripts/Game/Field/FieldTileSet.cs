using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 존 바닥 타일 구성 데이터 (사용자 지시: 타일 프리팹 리스트 + 노이즈 기반 배치).
    /// 어떤 타일이 어디에 깔릴지는 월드 좌표 기반 노이즈로 정해지므로,
    /// 같은 시드 + 같은 타일셋이면 항상 같은 바닥이 나온다 (재현성).
    ///
    /// 배리에이션은 두 축으로 만든다.
    /// - 타일 종류: 노이즈 값으로 가중치 목록에서 고른다 (뭉쳐서 패치가 생긴다)
    /// - 미세 기울기: 타일마다 1도 미만의 roll/pitch를 줘서 반듯한 격자감을 깬다
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Field Tile Set", fileName = "FieldTileSet")]
    public sealed class FieldTileSet : ScriptableObject
    {
        [System.Serializable]
        public sealed class TileEntry
        {
            public GameObject prefab;

            [Header("선택 가중치 (클수록 넓게 깔린다)")]
            [Min(0f)] public float weight = 1f;
        }

        [Header("타일 프리팹 목록 (1블록 크기 기준)")]
        public List<TileEntry> tiles = new List<TileEntry>();

        [Header("노이즈 스케일. 작을수록 같은 타일이 넓게 뭉친다")]
        public float noiseScale = 0.18f;

        [Header("미세 기울기 상한 (도). 1도 미만 권장 - 격자감만 깨는 용도")]
        [Range(0f, 3f)] public float maxTiltDegrees = 0.8f;

        [Header("타일 원점 -> 보행면 보정 y. 큐브 중심 피벗이면 -두께/2")]
        public float tileSurfaceOffsetY = -0.1f;

        [Header("타일 두께 (행 콜라이더 높이). 타일 콜라이더는 제거하고 행에 하나만 둔다")]
        public float tileThickness = 0.2f;

        /// <summary>사용 가능한 타일이 있는가.</summary>
        public bool HasTiles
        {
            get
            {
                foreach (TileEntry entry in tiles)
                {
                    if (entry != null && entry.prefab != null && entry.weight > 0f)
                        return true;
                }

                return false;
            }
        }

        void OnValidate()
        {
            noiseScale = Mathf.Max(0.001f, noiseScale);
        }

        /// <summary>
        /// 타일 좌표의 노이즈로 타일을 고른다. noiseOrigin은 런 시드에서 나온
        /// 오프셋이라 시드가 같으면 같은 배치가 재현된다.
        /// </summary>
        public GameObject PickTile(float worldX, float worldZ, Vector2 noiseOrigin)
        {
            if (!HasTiles)
                return null;

            float noise = Mathf.PerlinNoise(
                (worldX + noiseOrigin.x) * noiseScale,
                (worldZ + noiseOrigin.y) * noiseScale);

            return PickByWeight(Mathf.Clamp01(noise));
        }

        GameObject PickByWeight(float normalized)
        {
            float total = 0f;

            foreach (TileEntry entry in tiles)
            {
                if (entry == null || entry.prefab == null)
                    continue;

                total += Mathf.Max(0f, entry.weight);
            }

            if (total <= 0f)
                return null;

            float target = normalized * total;
            float accumulated = 0f;

            foreach (TileEntry entry in tiles)
            {
                if (entry == null || entry.prefab == null)
                    continue;

                accumulated += Mathf.Max(0f, entry.weight);

                if (target <= accumulated)
                    return entry.prefab;
            }

            // 부동소수 잔차로 끝을 넘긴 경우 - 마지막 유효 타일
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                if (tiles[i] != null && tiles[i].prefab != null)
                    return tiles[i].prefab;
            }

            return null;
        }

        /// <summary>
        /// 타일마다 다른 1도 미만 기울기 (roll/pitch). 좌표 해시라 결정적이고,
        /// 인접 타일과 상관이 없어 미세하게 흐트러진다. yaw는 건드리지 않는다.
        /// </summary>
        public Quaternion SampleTilt(float worldX, float worldZ, Vector2 noiseOrigin)
        {
            if (maxTiltDegrees <= 0f)
                return Quaternion.identity;

            float pitch = SignedHash(worldX + noiseOrigin.x, worldZ + noiseOrigin.y, 17.13f);
            float roll = SignedHash(worldX + noiseOrigin.x, worldZ + noiseOrigin.y, 91.71f);

            return Quaternion.Euler(pitch * maxTiltDegrees, 0f, roll * maxTiltDegrees);
        }

        // -1..1 해시. 좌표가 같으면 항상 같은 값 (프레임/드로우 순서 무관)
        static float SignedHash(float x, float z, float salt)
        {
            float value = Mathf.Sin(x * 12.9898f + z * 78.233f + salt) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2f - 1f;
        }
    }
}
