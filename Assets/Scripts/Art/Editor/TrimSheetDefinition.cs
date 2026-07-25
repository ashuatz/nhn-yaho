using UnityEngine;

namespace Scavenger.ArtTools
{
    /// <summary>
    /// 트림시트 아틀라스 규격. 아틀라스가 균등 격자로 나뉘고 각 셀이
    /// 상하좌우로 이음선 없이 반복되는 단일 타일이라고 전제한다.
    ///
    /// 에디터 전용 어셈블리에 있다. 이건 오소링 데이터고 런타임에 나가는 건
    /// 결과물(구운 메시 + 머티리얼)뿐이다. 씬/프리팹이 이 에셋을 참조하면
    /// 빌드에서 끊어지므로 참조하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Trim Sheet Definition", fileName = "TrimSheet")]
    public sealed class TrimSheetDefinition : ScriptableObject
    {
        [Tooltip("셀이 배치된 아틀라스. 하프 텍셀 인셋 계산에 크기를 쓴다.")]
        public Texture2D atlas;

        [Tooltip("아틀라스를 Base Map으로 물린 머티리얼. 구운 큐브에 배정된다.")]
        public Material material;

        [Min(1)]
        [Tooltip("아틀라스 가로 셀 개수.")]
        public int columns = 2;

        [Min(1)]
        [Tooltip("아틀라스 세로 셀 개수.")]
        public int rows = 2;

        [Min(MinWorldUnitsPerCell)]
        [Tooltip("셀 한 장이 덮는 월드 크기(m). 텍셀 밀도를 결정한다.")]
        public float worldUnitsPerCell = 0.5f;

        [Tooltip("셀 경계에서 반 텍셀 안쪽으로 UV를 좁혀 이웃 셀 번짐을 막는다.")]
        public bool insetHalfTexel = true;

        [Tooltip("혼합 모드에서 셀이 뽑힐 상대 가중치. 셀 순서와 같다. 비거나 짧으면 나머지는 1로 본다.")]
        public float[] cellWeights;

        public const float MinWorldUnitsPerCell = 0.05f;

        public int CellCount => Mathf.Max(1, columns) * Mathf.Max(1, rows);

        /// <summary>규격만으로 큐브를 구울 수 있는 상태인지.</summary>
        public bool IsReady()
        {
            if (material == null)
                return false;

            // 아틀라스가 없으면 하프 텍셀 인셋을 계산할 수 없다 (경계에서 이웃 셀이 새어 나온다)
            if (atlas == null)
                return false;

            if (columns < 1 || rows < 1)
                return false;

            return true;
        }

        /// <summary>셀 인덱스의 상대 가중치. 지정이 없으면 1 (균등).</summary>
        public float GetCellWeight(int cellIndex)
        {
            if (cellWeights == null)
                return 1f;

            if (cellIndex < 0 || cellIndex >= cellWeights.Length)
                return 1f;

            return Mathf.Max(0f, cellWeights[cellIndex]);
        }

        /// <summary>
        /// 가중치에 따라 셀을 하나 뽑는다. 크랙 셀을 낮은 가중치로 두면
        /// 손상 타일이 드문드문 섞인다.
        /// </summary>
        public int PickWeightedCell(System.Random rng)
        {
            int count = CellCount;
            float total = 0f;

            for (int i = 0; i < count; i++)
                total += GetCellWeight(i);

            // 가중치가 전부 0이면 균등 추첨으로 되돌린다
            if (total <= 0f)
                return rng.Next(count);

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;

            for (int i = 0; i < count; i++)
            {
                cursor += GetCellWeight(i);

                if (pick < cursor)
                    return i;
            }

            return count - 1;
        }

        /// <summary>
        /// 셀 인덱스를 UV 사각형으로. 인덱스는 좌하단 0부터 가로 우선으로 증가한다.
        /// 범위를 벗어난 인덱스는 감싼다 (랜덤/프리셋이 셀 수보다 커도 안전하게).
        /// </summary>
        public Rect GetCellUv(int cellIndex)
        {
            int count = CellCount;
            int columnCount = Mathf.Max(1, columns);
            int rowCount = Mathf.Max(1, rows);

            int wrapped = ((cellIndex % count) + count) % count;

            int column = wrapped % columnCount;
            int row = wrapped / columnCount;

            float cellWidth = 1f / columnCount;
            float cellHeight = 1f / rowCount;

            Rect uv = new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight);

            if (!insetHalfTexel)
                return uv;

            return ApplyHalfTexelInset(uv);
        }

        /// <summary>
        /// 셀 경계를 반 텍셀 안으로 당긴다. 바이리니어 필터가 이웃 셀 텍셀을 물어와
        /// 타일 가장자리에 다른 셀 색이 스미는 걸 막는다.
        /// </summary>
        Rect ApplyHalfTexelInset(Rect uv)
        {
            if (atlas == null)
                return uv;

            float insetU = 0.5f / atlas.width;
            float insetV = 0.5f / atlas.height;

            // 셀이 인셋보다 작은 병리적 아틀라스는 손대지 않는다 (UV가 뒤집힌다)
            if (insetU * 2f >= uv.width || insetV * 2f >= uv.height)
                return uv;

            return new Rect(
                uv.x + insetU,
                uv.y + insetV,
                uv.width - insetU * 2f,
                uv.height - insetV * 2f);
        }
    }
}
