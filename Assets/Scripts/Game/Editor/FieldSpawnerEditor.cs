using Scavenger.Field;
using Scavenger.Segment;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// FieldSpawner 인스펙터. 기본 인스펙터 아래에 배경 블록을 실제 오브젝트로
    /// 생성하는 버튼을 붙인다.
    ///
    /// 런타임 배경은 GPU 인스턴싱이라 에디트 모드에서 보이지 않는다.
    /// buildBackgroundBlocks 플래그 바로 옆에 두어 그 배경을 눈으로 확인하고
    /// 손으로 다듬는 경로를 만든다. 생성 로직은 BackgroundBlockBuilder가 소유한다.
    /// </summary>
    [CustomEditor(typeof(FieldSpawner))]
    public sealed class FieldSpawnerEditor : UnityEditor.Editor
    {
        // 인스펙터는 선택이 바뀌면 파괴되므로 설정은 세션 상태에 둔다.
        // 프로젝트 에셋에는 남기지 않는다 (에디터 전용 작업 값).
        const string SeedKey = "Scavenger.BackgroundBlocks.Seed";
        const string ZoneCountKey = "Scavenger.BackgroundBlocks.ZoneCount";
        const string DefinitionGuidKey = "Scavenger.BackgroundBlocks.DefinitionGuid";

        const int DefaultSeed = 12345;
        const int DefaultZoneCount = 6;

        SerializedProperty buildBackgroundBlocksProperty;

        void OnEnable()
        {
            buildBackgroundBlocksProperty = serializedObject.FindProperty("buildBackgroundBlocks");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12f);
            DrawBackgroundBlockSection();
        }

        void DrawBackgroundBlockSection()
        {
            EditorGUILayout.LabelField("배경 블록 사전 배치 (에디터)", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "런타임 배경은 GPU 인스턴싱이라 에디트 모드에서는 보이지 않는다. "
                + "아래 버튼으로 실제 오브젝트를 만들면 씬에서 보고 손으로 다듬을 수 있다.",
                MessageType.None);

            ZoneDefinition definition = LoadDefinition();
            int seed = SessionState.GetInt(SeedKey, DefaultSeed);
            int zoneCount = SessionState.GetInt(ZoneCountKey, DefaultZoneCount);

            EditorGUI.BeginChangeCheck();

            definition = (ZoneDefinition)EditorGUILayout.ObjectField(
                "Zone Definition", definition, typeof(ZoneDefinition), false);
            seed = EditorGUILayout.IntField("Seed", seed);
            zoneCount = EditorGUILayout.IntSlider(
                "Zone Count", zoneCount, BackgroundBlockBuilder.MinZoneCount, BackgroundBlockBuilder.MaxZoneCount);

            if (EditorGUI.EndChangeCheck())
            {
                StoreDefinition(definition);
                SessionState.SetInt(SeedKey, seed);
                SessionState.SetInt(ZoneCountKey, zoneCount);
            }

            if (definition == null)
                EditorGUILayout.HelpBox("Zone Definition이 비어 있다. 런타임과 같은 기본값으로 생성한다.", MessageType.None);

            EditorGUILayout.Space(6f);

            EnvironmentAuthoring existing = BackgroundBlockBuilder.FindExisting();

            DrawButtons(definition, seed, zoneCount, existing);
            DrawStatus(existing);
        }

        void DrawButtons(ZoneDefinition definition, int seed, int zoneCount, EnvironmentAuthoring existing)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("실제 객체로 생성", GUILayout.Height(24f)))
                    Build(definition, seed, zoneCount);

                using (new EditorGUI.DisabledScope(existing == null))
                {
                    if (GUILayout.Button("제거", GUILayout.Height(24f), GUILayout.Width(80f)))
                        BackgroundBlockBuilder.Clear();
                }
            }
        }

        static void Build(ZoneDefinition definition, int seed, int zoneCount)
        {
            EnvironmentAuthoring created = BackgroundBlockBuilder.Build(definition, seed, zoneCount);

            if (created == null)
                return;

            Selection.activeGameObject = created.gameObject;
            EditorGUIUtility.PingObject(created.gameObject);
        }

        void DrawStatus(EnvironmentAuthoring existing)
        {
            if (existing == null)
                return;

            EditorGUILayout.Space(4f);

            int blockCount = BackgroundBlockBuilder.CountBlocks(existing);

            EditorGUILayout.HelpBox(
                $"사전 배치됨: 블록 {blockCount}개, z {existing.coveredFromZ:F0} ~ {existing.coveredToZ:F0}. "
                + "블록은 자유롭게 수정/삭제/추가 가능하다.",
                MessageType.Info);

            DrawDoubleBackgroundWarning();
        }

        /// <summary>
        /// FieldSpawner.BuildBackground는 EnvironmentAuthoring 커버 범위를 확인하지 않는다.
        /// 사전 배치를 둔 채 플레이하면 인스턴싱 배경이 위에 겹쳐 그려진다.
        /// </summary>
        void DrawDoubleBackgroundWarning()
        {
            if (buildBackgroundBlocksProperty == null)
                return;

            if (!buildBackgroundBlocksProperty.boolValue)
                return;

            EditorGUILayout.HelpBox(
                "Build Background Blocks가 켜져 있다. 현재 런타임 생성은 사전 배치 범위를 "
                + "건너뛰지 않으므로, 플레이하면 인스턴싱 배경이 이 오브젝트들 위에 겹쳐 그려진다. "
                + "사전 배치만 쓰려면 위 체크를 끈다.",
                MessageType.Warning);

            if (!GUILayout.Button("Build Background Blocks 끄기"))
                return;

            buildBackgroundBlocksProperty.boolValue = false;
            serializedObject.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------------
        // 세션 상태 (에셋 참조는 GUID로 보관해 도메인 리로드를 견딘다)
        // ---------------------------------------------------------------------

        static ZoneDefinition LoadDefinition()
        {
            string guid = SessionState.GetString(DefinitionGuidKey, string.Empty);

            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                return null;

            return AssetDatabase.LoadAssetAtPath<ZoneDefinition>(path);
        }

        static void StoreDefinition(ZoneDefinition definition)
        {
            if (definition == null)
            {
                SessionState.SetString(DefinitionGuidKey, string.Empty);
                return;
            }

            string path = AssetDatabase.GetAssetPath(definition);

            if (string.IsNullOrEmpty(path))
            {
                SessionState.SetString(DefinitionGuidKey, string.Empty);
                return;
            }

            SessionState.SetString(DefinitionGuidKey, AssetDatabase.AssetPathToGUID(path));
        }
    }
}
