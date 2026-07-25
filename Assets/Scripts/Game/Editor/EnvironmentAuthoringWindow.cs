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

        [MenuItem("Scavenger/Environment Authoring")]
        public static void Open()
        {
            EnvironmentAuthoringWindow window = GetWindow<EnvironmentAuthoringWindow>("Env Authoring");
            window.minSize = new Vector2(280f, 160f);
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

        void Generate()
        {
            EnvironmentAuthoring created = BackgroundBlockBuilder.Build(definition, seed, segmentCount);

            if (created == null)
                return;

            Selection.activeGameObject = created.gameObject;
            EditorGUIUtility.PingObject(created.gameObject);
        }
    }
}
