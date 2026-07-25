using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 존 하나 (필드 규칙 문서 2.1 (2)). 블록이 모인 25 x 7 덩어리.
    /// 바닥은 제거 단위인 FloorRow(1블록 두께)로 분할해 보유하고,
    /// 제거 기준선이 뒤에서 다가오면 행 단위로 정상 -> 제거 예정 -> 제거로 넘긴다.
    /// 생성/제거의 단일 경계는 FieldSpawner - 존은 자기 바닥 상태만 소유한다.
    /// </summary>
    public sealed class Zone : MonoBehaviour
    {
        /// <summary>존 인덱스 (스테이지 내 순번, 0부터).</summary>
        public int ZoneIndex { get; private set; }

        public float StartZ { get; private set; }
        public float EndZ { get; private set; }

        /// <summary>스테이지의 마지막 존인가 (탈출 지점 배치 대상).</summary>
        public bool IsLastZone { get; private set; }

        /// <summary>남은 바닥 행이 없으면 존 오브젝트를 정리할 수 있다.</summary>
        public bool IsFullyRemoved
        {
            get { return remainingNormalRows == 0 && rows.Count == 0; }
        }

        static readonly Color FloorColor = new Color(0.3f, 0.31f, 0.33f);

        readonly List<FloorRow> rows = new List<FloorRow>();
        int remainingNormalRows;

        /// <summary>
        /// 바닥 행을 만들고 존 범위를 확정한다. startZ는 존의 시작 월드 z.
        /// 행은 z+ 방향으로 blockSize 간격, 존 너비 전체를 덮는다.
        /// </summary>
        public void Build(ZoneDefinition definition, int zoneIndex, float startZ, bool isLastZone)
        {
            ZoneIndex = zoneIndex;
            IsLastZone = isLastZone;
            StartZ = startZ;
            EndZ = startZ + definition.lengthMeters;

            float blockSize = definition.blockSize;
            float width = definition.zoneWidthBlocks * blockSize;

            for (int block = 0; block < definition.zoneLengthBlocks; block++)
            {
                float centerZ = startZ + (block + 0.5f) * blockSize;

                GameObject rowObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rowObject.name = $"FloorRow_{block:D2}";
                rowObject.transform.SetParent(transform, true);
                rowObject.transform.position = new Vector3(0f, -0.1f, centerZ);
                rowObject.transform.localScale = new Vector3(width, 0.2f, blockSize);

                Tint(rowObject, FloorColor);

                FloorRow row = rowObject.AddComponent<FloorRow>();
                rows.Add(row);
            }

            remainingNormalRows = rows.Count;
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

        /// <summary>런 재시작/스테이지 정리에서 즉시 파괴할 때 사용.</summary>
        public void DestroyImmediateAll()
        {
            // Destroy는 프레임 끝까지 지연되므로 먼저 비활성화 - 이전 스테이지의
            // 드랍/상호작용이 같은 프레임에 동작하지 못하게 한다
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        static void Tint(GameObject block, Color color)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            blockRenderer.material.color = color;
        }
    }
}
