using Scavenger.ArtTools;
using Scavenger.Field;
using Scavenger.Segment;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 배경 사전 배치 윈도우. 시드/구간 수를 지정해 배경을 씬에 실제 오브젝트로
    /// 생성해두고 손으로 다듬을 수 있다.
    ///
    /// 생성 로직은 <see cref="BackgroundBlockBuilder"/>가 소유한다.
    /// FieldSpawner 인스펙터 버튼과 같은 결과를 내야 하므로 여기서 직접 만들지 않는다.
    /// </summary>
    public sealed class EnvironmentAuthoringWindow : EditorWindow
    {
        int seed = 12345;
        int segmentCount = 6;
        ZoneDefinition definition;

        bool useTrimSheetBlocks = true;
        float trimSheetCellSpan = TrimSheetEnvBlocks.DefaultEnvCellSpan;

        [MenuItem("Scavenger/Environment Authoring")]
        public static void Open()
        {
            EnvironmentAuthoringWindow window = GetWindow<EnvironmentAuthoringWindow>("Env Authoring");
            window.minSize = new Vector2(320f, 260f);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("배경 사전 배치", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            seed = EditorGUILayout.IntField("Seed", seed);
            segmentCount = EditorGUILayout.IntSlider(
                "Segment Count", segmentCount,
                BackgroundBlockBuilder.MinZoneCount, BackgroundBlockBuilder.MaxZoneCount);
            definition = (ZoneDefinition)EditorGUILayout.ObjectField(
                "Segment Definition", definition, typeof(ZoneDefinition), false);

            EditorGUILayout.Space(8f);
            DrawBlockLookSection();
            EditorGUILayout.Space(8f);

            EnvironmentAuthoring existing = BackgroundBlockBuilder.FindExisting();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                    Generate();

                using (new EditorGUI.DisabledScope(existing == null))
                {
                    if (GUILayout.Button("Clear"))
                        BackgroundBlockBuilder.Clear();
                }
            }

            if (existing != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    $"사전 배치됨: 블록 {BackgroundBlockBuilder.CountBlocks(existing)}개, "
                    + $"z {existing.coveredFromZ:F0} ~ {existing.coveredToZ:F0}. "
                    + "블록은 자유롭게 수정/추가 가능.",
                    MessageType.Info);

                EditorGUILayout.HelpBox(
                    "런타임 생성은 이 범위를 건너뛰지 않는다. 사전 배치만 쓰려면 "
                    + "FieldSpawner의 Build Background Blocks를 끈다.",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// 블록 룩 선택. 프리미티브 큐브(단색) vs 트림시트 블록 프리팹(석재 텍스처).
        /// </summary>
        void DrawBlockLookSection()
        {
            EditorGUILayout.LabelField("블록 룩", EditorStyles.boldLabel);

            useTrimSheetBlocks = EditorGUILayout.Toggle("트림시트 블록 사용", useTrimSheetBlocks);

            if (!useTrimSheetBlocks)
            {
                EditorGUILayout.HelpBox(
                    "프리미티브 큐브 + 팔레트 단색. 가장 가볍다 (블록당 24정점).",
                    MessageType.None);

                return;
            }

            trimSheetCellSpan = EditorGUILayout.Slider("셀 크기 (m)", trimSheetCellSpan, 0.5f, 4f);

            EditorGUILayout.HelpBox(
                $"{TrimSheetEnvBlocks.PrefabPath} 인스턴스로 배치한다. "
                + "블록 크기는 셀 격자에 스냅되고 크기별 메시가 재사용된다 - "
                + "스케일을 걸면 텍셀 밀도가 블록마다 달라지기 때문이다.\n"
                + "팔레트 색은 틴트 머티리얼로 유지된다 (깊이 구분).",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "셀을 작게 하면 큰 블록 하나가 수천 쿼드가 된다. "
                + "생성 후 콘솔에 정점 총량이 남으므로 무거우면 셀 크기를 키운다.",
                MessageType.Warning);
        }

        void Generate()
        {
            EnvironmentAuthoring created = BackgroundBlockBuilder.Build(
                definition, seed, segmentCount, useTrimSheetBlocks, trimSheetCellSpan);

            if (created == null)
                return;

            Selection.activeGameObject = created.gameObject;
            EditorGUIUtility.PingObject(created.gameObject);
        }
    }
}
