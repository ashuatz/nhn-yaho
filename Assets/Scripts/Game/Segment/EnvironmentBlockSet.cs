using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 배경 블록 렌더 자산 묶음 (사용자 지적 2026-07-26: 인스턴싱 배경이 트림시트를
    /// 쓰지 않는다). 크기별로 구운 트림시트 메시 + 팔레트 머티리얼을 담아 두고,
    /// 런타임 배경(EnvironmentRenderer)이 존을 만들 때마다 여기서 조회한다 -
    /// 사전 배치로 되돌아가지 않고도 존이 계속 이어지면서 트림시트 룩이 유지된다.
    ///
    /// 왜 크기별 메시인가: 트림시트는 UV가 메시에 구워져 있어 스케일을 걸면
    /// 텍셀 밀도가 블록마다 달라진다. 그래서 크기를 셀 격자에 스냅해 종류를 줄이고
    /// (셀 2m 기준 존당 20종 남짓) 같은 크기끼리 인스턴싱으로 묶는다.
    ///
    /// 굽기와 배선은 에디터 메뉴 Scavenger > Field Trim Sheet > Bake Background Blocks.
    /// 이 에셋이 없거나 비어 있으면 렌더러는 기존 큐브 인스턴싱으로 폴백한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Environment Block Set", fileName = "EnvironmentBlockSet")]
    public sealed class EnvironmentBlockSet : ScriptableObject
    {
        /// <summary>구운 메시 한 종 (셀 수 -> 메시).</summary>
        [System.Serializable]
        public sealed class Entry
        {
            public Vector3Int cells = Vector3Int.one;
            public Mesh mesh;
        }

        [Header("셀 한 칸의 크기 (m). 작을수록 크기 종류(=드로우콜)가 늘어난다")]
        public float cellSpan = 2f;

        [Header("축당 셀 수 상한. 큰 블록의 정점 폭발을 막는다")]
        [Min(1)] public int maxCellsPerAxis = 12;

        [Header("크기별 구운 메시 (에디터 베이커가 채운다)")]
        public List<Entry> entries = new List<Entry>();

        [Header("팔레트 인덱스별 머티리얼 (SegmentEnvironment.Palette 순서)")]
        public List<Material> paletteMaterials = new List<Material>();

        // 셀 -> 항목 인덱스. 도메인 리로드/에셋 변경 때 다시 만든다
        Dictionary<Vector3Int, int> lookup;

        /// <summary>쓸 수 있는 구성인가 (메시와 머티리얼이 모두 있어야 한다).</summary>
        public bool IsReady
        {
            get
            {
                if (entries == null || entries.Count == 0)
                    return false;

                if (paletteMaterials == null || paletteMaterials.Count == 0)
                    return false;

                foreach (Entry entry in entries)
                {
                    if (entry != null && entry.mesh != null)
                        return true;
                }

                return false;
            }
        }

        void OnEnable()
        {
            lookup = null;
        }

        void OnValidate()
        {
            cellSpan = Mathf.Max(0.25f, cellSpan);
            lookup = null;
        }

        /// <summary>크기(m)를 셀 수로 스냅한다. 축당 최소 1, 최대 maxCellsPerAxis.</summary>
        public Vector3Int ResolveCells(Vector3 size)
        {
            return new Vector3Int(
                ResolveAxisCells(size.x),
                ResolveAxisCells(size.y),
                ResolveAxisCells(size.z));
        }

        int ResolveAxisCells(float length)
        {
            int count = Mathf.RoundToInt(Mathf.Abs(length) / cellSpan);

            return Mathf.Clamp(count, 1, Mathf.Max(1, maxCellsPerAxis));
        }

        /// <summary>셀 수 -> 실제 크기(m). 메시에 구워진 치수다.</summary>
        public Vector3 CellsToSize(Vector3Int cells)
        {
            return new Vector3(cells.x * cellSpan, cells.y * cellSpan, cells.z * cellSpan);
        }

        /// <summary>
        /// 이 크기를 그릴 항목 인덱스. 정확히 같은 셀 구성이 없으면 가장 가까운 것을 쓴다 -
        /// 표본에 없던 크기가 나와도 배경이 통째로 빠지지 않게 하는 폴백이다.
        /// </summary>
        public int ResolveEntry(Vector3 size)
        {
            if (entries == null || entries.Count == 0)
                return -1;

            Vector3Int cells = ResolveCells(size);

            EnsureLookup();

            if (lookup.TryGetValue(cells, out int index))
                return index;

            return FindNearest(cells);
        }

        public Mesh MeshAt(int index)
        {
            if (entries == null || index < 0 || index >= entries.Count)
                return null;

            Entry entry = entries[index];

            if (entry == null)
                return null;

            return entry.mesh;
        }

        /// <summary>항목의 실제 크기(m). 위치 보정에 쓴다.</summary>
        public Vector3 SizeAt(int index)
        {
            if (entries == null || index < 0 || index >= entries.Count)
                return Vector3.one;

            Entry entry = entries[index];

            if (entry == null)
                return Vector3.one;

            return CellsToSize(entry.cells);
        }

        public Material MaterialFor(int paletteIndex)
        {
            if (paletteMaterials == null || paletteMaterials.Count == 0)
                return null;

            int index = Mathf.Clamp(paletteIndex, 0, paletteMaterials.Count - 1);

            return paletteMaterials[index];
        }

        /// <summary>
        /// 스냅으로 커진 만큼 위치를 보정한다. 두 가지를 지킨다.
        /// - 윗면 유지: 커진 블록이 위로 자라면 카메라측 지형이 보행면을 가린다
        ///   (배경 규칙 "카메라측 가림 금지")
        /// - 복도쪽 면 유지: x로 커질 때 안쪽으로 자라면 통로를 침범한다
        /// z는 진행 방향이라 중심을 유지해도 이웃 블록과 겹치는 정도로 끝난다.
        /// </summary>
        public Vector3 AlignPosition(Vector3 position, Vector3 originalSize, Vector3 snappedSize)
        {
            float growthX = snappedSize.x - originalSize.x;
            float growthY = snappedSize.y - originalSize.y;

            float outward = position.x >= 0f ? 1f : -1f;

            return new Vector3(
                position.x + growthX * 0.5f * outward,
                position.y - growthY * 0.5f,
                position.z);
        }

        void EnsureLookup()
        {
            if (lookup != null)
                return;

            lookup = new Dictionary<Vector3Int, int>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry == null || entry.mesh == null)
                    continue;

                lookup[entry.cells] = i;
            }
        }

        int FindNearest(Vector3Int cells)
        {
            int best = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry == null || entry.mesh == null)
                    continue;

                int distance = Mathf.Abs(entry.cells.x - cells.x)
                               + Mathf.Abs(entry.cells.y - cells.y)
                               + Mathf.Abs(entry.cells.z - cells.z);

                if (distance >= bestDistance)
                    continue;

                best = i;
                bestDistance = distance;
            }

            return best;
        }
    }
}
