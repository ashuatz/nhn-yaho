using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 필드 세그먼트 하나 (필드 규칙 문서 2.1 (2)). 블록이 모인 25 x 7 덩어리.
    /// 바닥은 제거 단위인 FloorRow(1블록 두께)로 분할해 보유하고,
    /// 제거 기준선이 뒤에서 다가오면 행 단위로 정상 -> 제거 예정 -> 제거로 넘긴다.
    /// 생성/제거의 단일 경계는 FieldSpawner - 존은 자기 바닥 상태만 소유한다.
    ///
    /// 존과 존 사이 구간(Junction)도 같은 클래스가 담당한다 - 길이만 짧고
    /// 드랍/파밍 포인트 없이 웨이포인트만 놓이는 세그먼트 (IsJunction).
    /// </summary>
    public sealed class Zone : MonoBehaviour
    {
        /// <summary>세그먼트 순번 (필드 전체 통산, 0부터).</summary>
        public int ZoneIndex { get; private set; }

        public float StartZ { get; private set; }
        public float EndZ { get; private set; }

        /// <summary>존과 존 사이 구간인가 (웨이포인트 전용 세그먼트).</summary>
        public bool IsJunction { get; private set; }

        /// <summary>남은 바닥 행이 없으면 존 오브젝트를 정리할 수 있다.</summary>
        public bool IsFullyRemoved
        {
            get { return remainingNormalRows == 0 && rows.Count == 0; }
        }

        // 2026-07-26 밝기 보정 (V x1.7). 바닥 타일/행 프리팹 색과 같은 값
        static readonly Color FloorColor = new Color(0.51f, 0.53f, 0.56f);

        readonly List<FloorRow> rows = new List<FloorRow>();
        int remainingNormalRows;

        /// <summary>존 생성에 필요한 리소스 묶음 (FieldSpawner가 구성).</summary>
        public struct BuildContext
        {
            public ZoneDefinition Definition;

            /// <summary>바닥 타일 구성. 있으면 타일 조합이 최우선.</summary>
            public FieldTileSet TileSet;

            /// <summary>단일 메시 행 프리팹. 타일셋이 없을 때 사용.</summary>
            public FloorRow RowPrefab;

            /// <summary>노이즈 오프셋 (런 시드 유래 - 같은 시드면 같은 바닥).</summary>
            public Vector2 NoiseOrigin;

            /// <summary>이 세그먼트의 길이 (블록 수). 존과 구간이 서로 다르다.</summary>
            public int LengthBlocks;
        }

        /// <summary>
        /// 바닥 행을 만들고 세그먼트 범위를 확정한다. startZ는 시작 월드 z.
        /// 행은 z+ 방향으로 blockSize 간격, 존 너비 전체를 덮는다.
        /// 바닥 구성 우선순위: 타일셋(노이즈 조합) -> 행 프리팹 -> 코드 큐브 폴백.
        /// </summary>
        public void Build(BuildContext context, int zoneIndex, float startZ, bool isJunction)
        {
            ZoneDefinition definition = context.Definition;

            int lengthBlocks = Mathf.Max(1, context.LengthBlocks);

            ZoneIndex = zoneIndex;
            IsJunction = isJunction;
            StartZ = startZ;
            EndZ = startZ + lengthBlocks * definition.blockSize;

            float blockSize = definition.blockSize;
            float width = definition.zoneWidthBlocks * blockSize;

            bool useTiles = context.TileSet != null && context.TileSet.HasTiles;

            for (int block = 0; block < lengthBlocks; block++)
            {
                float centerZ = startZ + (block + 0.5f) * blockSize;

                FloorRow row;

                if (useTiles)
                    row = BuildTiledRow(context, block, centerZ, width, blockSize, definition);
                else if (context.RowPrefab != null)
                    row = InstantiateRow(context.RowPrefab, block, new Vector3(0f, -0.1f, centerZ));
                else
                    row = BuildGreyboxRow(block, new Vector3(0f, -0.1f, centerZ), width, blockSize);

                rows.Add(row);
            }

            remainingNormalRows = rows.Count;
        }

        /// <summary>
        /// 타일 조합 행. 행은 빈 컨테이너이고 너비만큼 타일을 깐다.
        /// 타일 종류는 노이즈로 뭉치게, 기울기는 타일마다 1도 미만으로 흐트러진다.
        /// 콜라이더는 타일에서 걷어내고 행에 하나만 둔다 - 타일 수만큼 늘리지 않는다.
        /// </summary>
        FloorRow BuildTiledRow(
            BuildContext context, int block, float centerZ, float width, float blockSize,
            ZoneDefinition definition)
        {
            FieldTileSet tileSet = context.TileSet;

            GameObject rowObject = new GameObject($"FloorRow_{block:D2}");
            rowObject.transform.SetParent(transform, true);
            rowObject.transform.position = new Vector3(0f, 0f, centerZ);

            for (int lane = 0; lane < definition.zoneWidthBlocks; lane++)
            {
                float centerX = -width * 0.5f + (lane + 0.5f) * blockSize;

                GameObject tilePrefab = tileSet.PickTile(centerX, centerZ, context.NoiseOrigin);

                if (tilePrefab == null)
                    continue;

                Quaternion tilt = tileSet.SampleTilt(centerX, centerZ, context.NoiseOrigin);
                Vector3 position = new Vector3(centerX, tileSet.tileSurfaceOffsetY, centerZ);

                GameObject tile = Instantiate(tilePrefab, position, tilt, rowObject.transform);
                tile.name = $"Tile_{lane:D2}";

                // 콜라이더는 행이 하나로 대표한다 (타일마다 두면 물리 비용만 늘어난다)
                Collider[] tileColliders = tile.GetComponentsInChildren<Collider>();

                foreach (Collider tileCollider in tileColliders)
                    Destroy(tileCollider);
            }

            BoxCollider rowCollider = rowObject.AddComponent<BoxCollider>();
            rowCollider.size = new Vector3(width, tileSet.tileThickness, blockSize);
            rowCollider.center = new Vector3(0f, tileSet.tileSurfaceOffsetY, 0f);

            return rowObject.AddComponent<FloorRow>();
        }

        // 프리팹 복제 - 스케일은 프리팹이 소유한다 (아트가 정한 형태를 코드가 덮지 않는다).
        // 존 규격과 프리팹 크기가 어긋나면 프리팹 쪽을 규격에 맞추는 것이 원칙
        FloorRow InstantiateRow(FloorRow rowPrefab, int block, Vector3 position)
        {
            FloorRow row = Instantiate(rowPrefab, position, Quaternion.identity, transform);
            row.name = $"FloorRow_{block:D2}";
            return row;
        }

        FloorRow BuildGreyboxRow(int block, Vector3 position, float width, float blockSize)
        {
            GameObject rowObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rowObject.name = $"FloorRow_{block:D2}";
            rowObject.transform.SetParent(transform, true);
            rowObject.transform.position = position;
            rowObject.transform.localScale = new Vector3(width, 0.2f, blockSize);

            GreyboxPalette.Apply(rowObject, FloorColor);

            return rowObject.AddComponent<FloorRow>();
        }

        /// <summary>
        /// 제거 기준선(월드 z)보다 뒤에 있는 행을 제거 예정으로 넘긴다.
        /// 낙하가 끝난 행은 목록에서 정리한다 (파괴는 행이 자체 수행).
        /// </summary>
        public void UpdateRemoval(float removeLineZ, float shakeSeconds)
        {
            remainingNormalRows = 0;

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                FloorRow row = rows[i];

                if (row == null)
                {
                    rows.RemoveAt(i);
                    continue;
                }

                if (row.State == FloorRowState.Normal)
                {
                    if (row.CenterZ < removeLineZ)
                        row.BeginPending(shakeSeconds);
                    else
                        remainingNormalRows += 1;
                }
            }
        }

        /// <summary>
        /// 요소를 발밑 행의 자식으로 붙인다 - 바닥이 낙하하면 함께 떨어진다.
        /// 붙일 행이 없으면 존 루트에 남긴다 (월드 위치 유지).
        /// </summary>
        public void AttachToRow(Transform feature)
        {
            float z = feature.position.z;
            FloorRow nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (FloorRow row in rows)
            {
                if (row == null)
                    continue;

                float distance = Mathf.Abs(row.CenterZ - z);

                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = row;
            }

            if (nearest == null)
                return;

            feature.SetParent(nearest.transform, true);
        }

        /// <summary>존 바닥 행 목록 (파밍 포인트 등 외부 배치가 참조).</summary>
        public IReadOnlyList<FloorRow> Rows
        {
            get { return rows; }
        }

        /// <summary>런 재시작/스테이지 정리에서 즉시 파괴할 때 사용.</summary>
        public void DestroyImmediateAll()
        {
            // Destroy는 프레임 끝까지 지연되므로 먼저 비활성화 - 이전 스테이지의
            // 드랍/상호작용이 같은 프레임에 동작하지 못하게 한다
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        // 머티리얼은 GreyboxPalette가 공급한다 (Common.mat 기반 공유 인스턴스)
    }
}
