using Scavenger.Segment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 배경 사전 배치 윈도우. 시드/구간 수를 지정해 배경을 씬에 생성해두고
    /// 손으로 다듬을 수 있다. EnvironmentAuthoring 마커가 커버하는 범위에서는
    /// 런타임 배경 생성이 스킵되어 다듬은 결과가 그대로 쓰인다 (ADR-0004).
    /// </summary>
    public sealed class EnvironmentAuthoringWindow : EditorWindow
    {
        int seed = 12345;
        int segmentCount = 6;
        SegmentDefinition definition;

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
            segmentCount = Mathf.Clamp(EditorGUILayout.IntField("Segment Count", segmentCount), 1, 50);
            definition = (SegmentDefinition)EditorGUILayout.ObjectField(
                "Segment Definition", definition, typeof(SegmentDefinition), false);

            EditorGUILayout.Space(8f);

            EnvironmentAuthoring existing = FindFirstObjectByType<EnvironmentAuthoring>();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                    Generate();

                using (new EditorGUI.DisabledScope(existing == null))
                {
                    if (GUILayout.Button("Clear"))
                        Clear();
                }
            }

            if (existing != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    $"사전 배치됨: z {existing.coveredFromZ:F0} ~ {existing.coveredToZ:F0}. "
                    + "이 범위는 런타임 배경 생성이 스킵된다. 블록은 자유롭게 수정/추가 가능.",
                    MessageType.Info);
            }
        }

        void Generate()
        {
            Clear();

            SegmentDefinition activeDefinition = definition;

            if (activeDefinition == null)
                activeDefinition = SegmentDefinition.CreateDefault();

            GameObject root = new GameObject("PreplacedEnvironment");
            EnvironmentAuthoring authoring = root.AddComponent<EnvironmentAuthoring>();

            System.Random rng = new System.Random(seed);

            for (int i = 0; i < segmentCount; i++)
            {
                GameObject chunk = new GameObject($"EnvChunk_{i}");
                chunk.transform.SetParent(root.transform, false);
                chunk.transform.localPosition = new Vector3(0f, 0f, i * activeDefinition.lengthMeters);

                SegmentEnvironment.BuildGameObjects(chunk.transform, activeDefinition, rng);
            }

            authoring.coveredFromZ = 0f;
            authoring.coveredToZ = segmentCount * activeDefinition.lengthMeters;

            Undo.RegisterCreatedObjectUndo(root, "Generate Preplaced Environment");
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        static void Clear()
        {
            EnvironmentAuthoring existing = FindFirstObjectByType<EnvironmentAuthoring>();

            if (existing == null)
                return;

            UnityEngine.SceneManagement.Scene scene = existing.gameObject.scene;
            Undo.DestroyObjectImmediate(existing.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
