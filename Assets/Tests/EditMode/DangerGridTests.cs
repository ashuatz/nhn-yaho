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
        public void CellsInCircle_AllCellCentersWithinRadius()
        {
            Vector2 center = new Vector2(3.2f, -1.7f);
            float radius = 2.4f;

            List<Vector2Int> cells = DangerGrid.CellsInCircle(center, radius, 1f);

            Assert.Greater(cells.Count, 0);

            foreach (Vector2Int cell in cells)
            {
                Vector2 cellCenter = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                Assert.LessOrEqual((cellCenter - center).magnitude, radius + 0.0001f);
            }
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
