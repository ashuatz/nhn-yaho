using System.Collections.Generic;
using NUnit.Framework;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Tests
{
    // 위험 그리드 셀 계산 (M3-1): 정적 순수 함수만 검증
    public sealed class DangerGridTests
    {
        [Test]
        public void CellsInCircle_AllCellsOverlapRadius()
        {
            Vector2 center = new Vector2(3.2f, -1.7f);
            float radius = 2.4f;

            List<Vector2Int> cells = DangerGrid.CellsInCircle(center, radius, 1f);

            Assert.Greater(cells.Count, 0);

            foreach (Vector2Int cell in cells)
            {
                // 셀 사각형의 최근접점이 반경 안이어야 실제로 겹친 것이다
                float nearestX = Mathf.Clamp(center.x, cell.x, cell.x + 1f);
                float nearestY = Mathf.Clamp(center.y, cell.y, cell.y + 1f);

                Vector2 nearest = new Vector2(nearestX, nearestY);
                Assert.Less((nearest - center).magnitude, radius);
            }
        }

        [Test]
        public void CellsInCircle_IncludesPartiallyOverlappedEdgeCell()
        {
            // 셀 (1,1)의 중심 (1.5,1.5)은 반경 1.7 밖이지만 최근접 모서리 (1,1)은
            // 반경 안 - 중심 판정이면 위험한데 안전해 보이는 셀 (Codex 검토 사례)
            List<Vector2Int> cells = DangerGrid.CellsInCircle(Vector2.zero, 1.7f, 1f);

            Assert.Contains(new Vector2Int(1, 1), cells);
        }

        [Test]
        public void CellsInCircle_CoversCellUnderCenter()
        {
            Vector2 center = new Vector2(0.5f, 0.5f);

            List<Vector2Int> cells = DangerGrid.CellsInCircle(center, 0.6f, 1f);

            Assert.Contains(new Vector2Int(0, 0), cells);
        }

        [Test]
        public void CellsInCircle_ZeroRadius_Empty()
        {
            List<Vector2Int> cells = DangerGrid.CellsInCircle(Vector2.zero, 0f, 1f);

            Assert.AreEqual(0, cells.Count);
        }

        [Test]
        public void CellsInRect_MatchesExpectedFootprint()
        {
            // 중심 (1, 1), 4x2 사각 -> 셀 중심 x는 -0.5..2.5 중 [-1..3) 범위 검사
            Vector2 center = new Vector2(1f, 1f);

            List<Vector2Int> cells = DangerGrid.CellsInRect(center, new Vector2(4f, 2f), 1f);

            // x: 셀 중심 -0.5, 0.5, 1.5, 2.5 (경계 포함) / y: 셀 중심 0.5, 1.5
            Assert.AreEqual(8, cells.Count);
            Assert.Contains(new Vector2Int(-1, 0), cells);
            Assert.Contains(new Vector2Int(2, 1), cells);
        }

        [Test]
        public void CellsInRect_InvalidSize_Empty()
        {
            List<Vector2Int> cells = DangerGrid.CellsInRect(Vector2.zero, new Vector2(0f, 2f), 1f);

            Assert.AreEqual(0, cells.Count);
        }
    }
}
