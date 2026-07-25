using NUnit.Framework;
using Scavenger.Loot;

namespace Scavenger.Tests
{
    /// <summary>
    /// 아이템 정의 컬럼 (드랍 문서 9.1 / 가방 문서 7.3). 데이터 세팅의 전제라
    /// 헬퍼 동작을 고정해 둔다 - 값이 비었을 때의 폴백이 특히 중요하다.
    /// </summary>
    public sealed class LootDefinitionTests
    {
        [Test]
        public void CanSpawnInZone_EmptyList_AllowsEveryZone()
        {
            LootDefinition definition = LootDefinition.Create("paper", "폐지", 10, 1, 1f);

            Assert.IsTrue(definition.CanSpawnInZone(0));
            Assert.IsTrue(definition.CanSpawnInZone(7));
        }

        [Test]
        public void CanSpawnInZone_RestrictsToListedZones()
        {
            LootDefinition definition = LootDefinition.Create("paper", "폐지", 10, 1, 1f);
            definition.spawnZones.Add(2);
            definition.spawnZones.Add(3);

            Assert.IsFalse(definition.CanSpawnInZone(1));
            Assert.IsTrue(definition.CanSpawnInZone(2));
            Assert.IsTrue(definition.CanSpawnInZone(3));
        }

        [Test]
        public void CompressedWeight_StepZero_IsOriginalWeight()
        {
            LootDefinition definition = LootDefinition.Create("metal", "고철", 30, 2, 1f, 4f);

            Assert.AreEqual(4f, definition.CompressedWeight(0), 0.0001f);
        }

        [Test]
        public void CompressedWeight_MissingStep_FallsBackToPreviousWeight()
        {
            LootDefinition definition = LootDefinition.Create("metal", "고철", 30, 2, 1f, 4f);
            definition.compressedWeight1 = 3.6f;

            // 2회 압축 값이 없으면 1회 압축 무게를 유지한다 (압축 불가 단계)
            Assert.AreEqual(3.6f, definition.CompressedWeight(1), 0.0001f);
            Assert.AreEqual(3.6f, definition.CompressedWeight(2), 0.0001f);
            Assert.AreEqual(1, definition.MaxCompressStep);
        }

        [Test]
        public void CompressedWeight_BothSteps_UsesEachStep()
        {
            LootDefinition definition = LootDefinition.Create("metal", "고철", 30, 2, 1f, 4f);
            definition.compressedWeight1 = 3.6f;
            definition.compressedWeight2 = 3.2f;

            Assert.AreEqual(3.6f, definition.CompressedWeight(1), 0.0001f);
            Assert.AreEqual(3.2f, definition.CompressedWeight(2), 0.0001f);
            Assert.AreEqual(2, definition.MaxCompressStep);
        }

        [Test]
        public void CompressedWeight_NoCompression_KeepsOriginal()
        {
            LootDefinition definition = LootDefinition.Create("stone", "돌", 5, 1, 1f, 2f);

            Assert.AreEqual(0, definition.MaxCompressStep);
            Assert.AreEqual(2f, definition.CompressedWeight(2), 0.0001f);
        }

        [Test]
        public void GradeName_CoversFourGrades()
        {
            Assert.AreEqual("일반", LootDefinition.GradeName(1));
            Assert.AreEqual("희귀", LootDefinition.GradeName(2));
            Assert.AreEqual("영웅", LootDefinition.GradeName(3));
            Assert.AreEqual("전설", LootDefinition.GradeName(LootDefinition.MaxTier));
        }

        [Test]
        public void Create_ClampsTierToGradeRange()
        {
            LootDefinition definition = LootDefinition.Create("odd", "이상한 것", 1, 9, 1f);

            Assert.AreEqual(LootDefinition.MaxTier, definition.tier);
        }
    }
}
