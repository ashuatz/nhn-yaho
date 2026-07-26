using System.Collections.Generic;
using Scavenger.Field;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 밝기 설정(GreyboxTheme)을 이미 만들어진 에셋에 반영한다.
    ///
    /// 런타임 생성물은 설정을 매번 읽으므로 즉시 반영되지만, 프리팹과 머티리얼은
    /// 구워진 결과라 다시 만들어야 한다. 창(Greybox Brightness)의 "적용"이 여기를 부른다.
    ///
    /// 미리보기 표도 여기 목록을 쓴다 - 창과 적용이 서로 다른 색을 보면 안 된다.
    /// </summary>
    public static class GreyboxThemeApplier
    {
        /// <summary>미리보기 한 줄 (이름 + 기준색 + 갈래).</summary>
        public readonly struct Sample
        {
            public readonly string Label;
            public readonly Color BaseColor;
            public readonly GreyboxTone Tone;

            public Sample(string label, Color baseColor, GreyboxTone tone)
            {
                Label = label;
                BaseColor = baseColor;
                Tone = tone;
            }
        }

        const string FloorGridPath = "Assets/Resources/FloorGrid.mat";

        /// <summary>창이 표로 보여주는 기준색 목록 (실제 생성에 쓰는 값과 같은 출처).</summary>
        public static List<Sample> CollectSamples()
        {
            List<Sample> samples = new List<Sample>();

            samples.Add(new Sample("바닥 타일 A", FieldPrefabTemplates.TileColors[0], GreyboxTone.Field));
            samples.Add(new Sample("바닥 타일 B", FieldPrefabTemplates.TileColors[1], GreyboxTone.Field));
            samples.Add(new Sample("바닥 타일 C", FieldPrefabTemplates.TileColors[2], GreyboxTone.Field));
            samples.Add(new Sample("바닥 행", FieldPrefabTemplates.FloorColor, GreyboxTone.Field));
            samples.Add(new Sample("파밍 포인트 상단", FieldPrefabTemplates.TopPointColor, GreyboxTone.Field));
            samples.Add(new Sample("파밍 포인트 하단", FieldPrefabTemplates.BottomPointColor, GreyboxTone.Field));
            samples.Add(new Sample("아이템 오브젝트 일반", FarmingObjectTemplates.NormalColor, GreyboxTone.Field));
            samples.Add(new Sample("아이템 오브젝트 희귀", FarmingObjectTemplates.RareColor, GreyboxTone.Field));
            samples.Add(new Sample("아이템 오브젝트 영웅", FarmingObjectTemplates.HeroColor, GreyboxTone.Field));
            Color[] palette = Scavenger.Segment.SegmentEnvironment.Palette;

            for (int i = 0; i < palette.Length; i++)
            {
                string name = Scavenger.Segment.SegmentEnvironment.PaletteName(i);
                samples.Add(new Sample($"배경 {i:00} {name}", palette[i], GreyboxTone.Background));
            }

            samples.Add(new Sample("플레이어 몸", GreyboxSceneSetup.PlayerBodyBaseColor, GreyboxTone.Character));
            samples.Add(new Sample("플레이어 머리", GreyboxSceneSetup.PlayerHeadBaseColor, GreyboxTone.Character));

            return samples;
        }

        /// <summary>
        /// 설정을 에셋에 반영한다. 순서가 중요하다 - 이름 있는 머티리얼을 먼저 맞춘 뒤
        /// 프리팹을 구워야 프리팹이 참조하는 머티리얼이 이미 새 밝기다.
        /// </summary>
        public static void ApplyToAssets()
        {
            RefreshNamedMaterials();

            FieldTrimSheetPrefabs.RebuildFieldPrefabsNow();
            EnvironmentBlockSetBaker.BakeBackgroundBlocksNow();

            AssetDatabase.SaveAssets();

            GreyboxTheme theme = GreyboxThemeAccess.Load();

            UnityEngine.Debug.Log(
                $"[Greybox] 밝기 적용: 전체 x{theme.brightness:0.##} "
                + $"(필드 x{theme.fieldBrightness:0.##} / 배경 x{theme.backgroundBrightness:0.##} "
                + $"/ 캐릭터 x{theme.characterBrightness:0.##})");
        }

        /// <summary>
        /// 프리팹 재생성으로는 갱신되지 않는 이름 있는 머티리얼 (기준/플레이어/바닥 그리드).
        /// Ensure는 이미 있는 에셋의 색도 현재 값으로 맞춘다.
        /// </summary>
        static void RefreshNamedMaterials()
        {
            // Common.mat은 건드리지 않는다 - 틴트의 **기준(흰색)** 이라 여기에 밝기를 걸면
            // 이 머티리얼을 복사해 만드는 트림시트 아틀라스까지 같이 어두워진다
            GreyboxMaterials.Ensure(
                "PlayerBody", GreyboxSceneSetup.PlayerBodyBaseColor, null, GreyboxTone.Character);
            GreyboxMaterials.Ensure(
                "PlayerHead", GreyboxSceneSetup.PlayerHeadBaseColor, null, GreyboxTone.Character);

            Material floorGrid = AssetDatabase.LoadAssetAtPath<Material>(FloorGridPath);

            if (floorGrid == null)
                return;

            Color gridColor = GreyboxThemeAccess.Tint(
                GreyboxMaterials.FloorGridBaseColor, GreyboxTone.Field);

            if (floorGrid.color == gridColor)
                return;

            floorGrid.color = gridColor;
            EditorUtility.SetDirty(floorGrid);
        }
    }
}
