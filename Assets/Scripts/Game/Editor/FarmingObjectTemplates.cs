using System.Collections.Generic;
using Scavenger.Field;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 아이템 오브젝트 더미 프리팹 (파밍 문서 3.2: 상자 / 도자기 항아리 x 3등급).
    /// 아트가 형태를 교체할 자리이며, 코드는 프리팹의 FarmingObject 계약만 읽는다
    /// (종류 / 등급 / 상호작용 시간 / 뚜껑 / 아이템이 나오는 자리).
    ///
    /// 있으면 절대 덮어쓰지 않는다 - 다듬은 결과가 메뉴 재실행으로 사라지지 않게.
    /// 규격이 바뀌어 갱신해야 하면 Rebuild 메뉴를 쓴다 (경로가 같아 GUID 유지).
    /// </summary>
    public static class FarmingObjectTemplates
    {
        public const string ObjectFolder = "Assets/Prefabs/Field/Objects";

        /// <summary>더미 프리팹 한 종의 규격.</summary>
        struct Template
        {
            public string Name;
            public FarmingObjectKind Kind;
            public FarmingPointGrade Grade;
        }

        static readonly Template[] Templates =
        {
            new Template { Name = "ItemObject_Box_Normal", Kind = FarmingObjectKind.Box, Grade = FarmingPointGrade.Normal },
            new Template { Name = "ItemObject_Box_Rare", Kind = FarmingObjectKind.Box, Grade = FarmingPointGrade.Rare },
            new Template { Name = "ItemObject_Box_Hero", Kind = FarmingObjectKind.Box, Grade = FarmingPointGrade.Hero },
            new Template { Name = "ItemObject_Jar_Normal", Kind = FarmingObjectKind.Jar, Grade = FarmingPointGrade.Normal },
            new Template { Name = "ItemObject_Jar_Rare", Kind = FarmingObjectKind.Jar, Grade = FarmingPointGrade.Rare },
            new Template { Name = "ItemObject_Jar_Hero", Kind = FarmingObjectKind.Jar, Grade = FarmingPointGrade.Hero },
        };

        // 등급 색 (파밍 포인트 그레이박스 색과 같은 축 - 아트가 형태로 대체할 임시 표현)
        static readonly Color NormalColor = new Color(0.42f, 0.38f, 0.3f);
        static readonly Color RareColor = new Color(0.28f, 0.38f, 0.5f);
        static readonly Color HeroColor = new Color(0.42f, 0.32f, 0.5f);

        // 상자 규격 (m). 뚜껑은 뒤쪽 모서리를 축으로 젖혀진다
        const float BoxWidth = 0.72f;
        const float BoxHeight = 0.5f;
        const float BoxLidThickness = 0.09f;

        // 항아리 규격 (m). 몸통 + 목 + 뚜껑
        const float JarWidth = 0.6f;
        const float JarHeight = 0.62f;

        [MenuItem("Scavenger/Ensure Farming Object Prefabs")]
        public static void EnsureFarmingObjectPrefabs()
        {
            EnsureFolders();

            foreach (Template template in Templates)
            {
                string path = ResolvePath(template);

                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                    continue;

                SaveTemplate(template, path);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 현재 템플릿으로 다시 만든다 (덮어쓴다). 트림시트 경로도 이 함수를 거친다 -
        /// 무엇을 만들지는 GreyboxBlockFactory가 정한다.
        /// </summary>
        public static void RebuildFarmingObjectPrefabsNow()
        {
            EnsureFolders();

            foreach (Template template in Templates)
                SaveTemplate(template, ResolvePath(template));

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Scavenger/Rebuild Farming Object Prefabs")]
        public static void RebuildFarmingObjectPrefabs()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "아이템 오브젝트 프리팹 재생성",
                "ItemObject_* 6종을 기본 템플릿으로 다시 만든다.\n"
                + "프리팹에서 직접 다듬은 내용이 있으면 사라진다.",
                "재생성", "취소");

            if (!confirmed)
                return;

            RebuildFarmingObjectPrefabsNow();
        }

        /// <summary>배선용 목록. 없는 프리팹은 건너뛴다.</summary>
        public static List<FarmingObject> LoadAll()
        {
            List<FarmingObject> objects = new List<FarmingObject>(Templates.Length);

            foreach (Template template in Templates)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResolvePath(template));

                if (prefab == null)
                    continue;

                FarmingObject component = prefab.GetComponent<FarmingObject>();

                if (component == null)
                    continue;

                objects.Add(component);
            }

            return objects;
        }

        static string ResolvePath(Template template)
        {
            return $"{ObjectFolder}/{template.Name}.prefab";
        }

        static void SaveTemplate(Template template, string path)
        {
            GameObject root = BuildTemplate(template);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            UnityEngine.Debug.Log($"[Setup] 아이템 오브젝트 프리팹 생성: {path}");
        }

        static GameObject BuildTemplate(Template template)
        {
            GameObject root = new GameObject(template.Name);

            Color color = ResolveGradeColor(template.Grade);
            Transform lid = null;

            if (template.Kind == FarmingObjectKind.Box)
                lid = BuildBox(root.transform, color, template.Grade);
            else
                lid = BuildJar(root.transform, color, template.Grade);

            // 아이템이 나오는 자리 - 오브젝트 위쪽. 마커라 메시가 없다
            GameObject pop = new GameObject("PopOrigin");
            pop.transform.SetParent(root.transform, false);
            pop.transform.localPosition = new Vector3(0f, ResolvePopHeight(template.Kind), 0f);

            FarmingObject farmingObject = root.AddComponent<FarmingObject>();
            farmingObject.kind = template.Kind;
            farmingObject.grade = template.Grade;
            farmingObject.interactSeconds = FarmingObject.DefaultInteractSeconds(template.Grade);
            farmingObject.lid = lid;
            farmingObject.popOrigin = pop.transform;

            return root;
        }

        /// <summary>
        /// 상자. 뚜껑은 빈 GameObject(경첩)에 매달아 뒤쪽 모서리를 축으로 젖혀지게 한다 -
        /// 뚜껑 자체를 돌리면 중심을 축으로 돌아 상자를 뚫는다.
        /// </summary>
        static Transform BuildBox(Transform parent, Color color, FarmingPointGrade grade)
        {
            float half = BoxWidth * 0.5f;

            GameObject body = GreyboxBlockFactory.Create(
                "Body", new Vector3(BoxWidth, BoxHeight, BoxWidth), color, withCollider: true);

            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, BoxHeight * 0.5f, 0f);

            // 등급 띠 - 형태 베리에이션이 들어오기 전의 임시 구분
            GameObject band = GreyboxBlockFactory.Create(
                "Band", new Vector3(BoxWidth * 1.03f, 0.08f, BoxWidth * 1.03f),
                color * 1.5f, withCollider: false);

            band.transform.SetParent(parent, false);
            band.transform.localPosition = new Vector3(0f, BoxHeight * 0.55f, 0f);

            GameObject hinge = new GameObject("Lid");
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = new Vector3(0f, BoxHeight, -half);

            GameObject lid = GreyboxBlockFactory.Create(
                "LidPlate", new Vector3(BoxWidth, BoxLidThickness, BoxWidth),
                color * 1.25f, withCollider: false);

            lid.transform.SetParent(hinge.transform, false);
            lid.transform.localPosition = new Vector3(0f, BoxLidThickness * 0.5f, half);

            AddGradeGlow(parent, grade, BoxHeight);

            return hinge.transform;
        }

        /// <summary>
        /// 도자기 항아리. 몸통은 원기둥이고 뚜껑은 위에 얹힌 얇은 원반이다
        /// (트림시트 블록 경로에서는 몸통도 직육면체가 된다 - 그레이박스 단계 허용).
        /// </summary>
        static Transform BuildJar(Transform parent, Color color, FarmingPointGrade grade)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(parent, false);

            // 원기둥은 높이 2가 기본 - 절반으로 환산한다
            body.transform.localScale = new Vector3(JarWidth, JarHeight * 0.5f, JarWidth);
            body.transform.localPosition = new Vector3(0f, JarHeight * 0.5f, 0f);

            // 프리미티브 원기둥의 캡슐 콜라이더는 옆면이 둥글어, 옆에 선 캐릭터가
            // 곡면을 타고 계속 밀려난다 (실제로 상호작용 사거리 밖까지 밀려났다).
            // 시각만 원기둥으로 두고 충돌은 상자로 대신한다
            RemoveCollider(body);
            AssignMaterial(body, color);

            GameObject bodyCollider = new GameObject("BodyCollider");
            bodyCollider.transform.SetParent(parent, false);
            bodyCollider.transform.localPosition = new Vector3(0f, JarHeight * 0.5f, 0f);

            BoxCollider box = bodyCollider.AddComponent<BoxCollider>();
            box.size = new Vector3(JarWidth, JarHeight, JarWidth);

            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck";
            neck.transform.SetParent(parent, false);
            neck.transform.localScale = new Vector3(JarWidth * 0.55f, 0.07f, JarWidth * 0.55f);
            neck.transform.localPosition = new Vector3(0f, JarHeight, 0f);

            RemoveCollider(neck);
            AssignMaterial(neck, color * 0.85f);

            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lid.name = "Lid";
            lid.transform.SetParent(parent, false);
            lid.transform.localScale = new Vector3(JarWidth * 0.62f, 0.05f, JarWidth * 0.62f);
            lid.transform.localPosition = new Vector3(0f, JarHeight + 0.12f, 0f);

            RemoveCollider(lid);
            AssignMaterial(lid, color * 1.3f);

            AddGradeGlow(parent, grade, JarHeight);

            return lid.transform;
        }

        /// <summary>
        /// 등급 강조 (파밍 문서 2.3: 멀리서도 알아볼 수 있게). 영웅만 포인트 라이트를
        /// 붙인다 - 모든 오브젝트에 광원을 달면 존마다 광원이 수십 개가 된다.
        /// </summary>
        static void AddGradeGlow(Transform parent, FarmingPointGrade grade, float height)
        {
            if (grade != FarmingPointGrade.Hero)
                return;

            GameObject glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(parent, false);
            glowObject.transform.localPosition = new Vector3(0f, height + 0.3f, 0f);

            Light glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = HeroColor * 2f;
            glow.range = 3.2f;
            glow.intensity = 1.4f;
            glow.shadows = LightShadows.None;
        }

        static float ResolvePopHeight(FarmingObjectKind kind)
        {
            if (kind == FarmingObjectKind.Jar)
                return JarHeight + 0.35f;

            return BoxHeight + 0.35f;
        }

        static Color ResolveGradeColor(FarmingPointGrade grade)
        {
            if (grade == FarmingPointGrade.Hero)
                return HeroColor;

            if (grade == FarmingPointGrade.Rare)
                return RareColor;

            return NormalColor;
        }

        static void AssignMaterial(GameObject target, Color color)
        {
            Renderer targetRenderer = target.GetComponent<Renderer>();

            if (targetRenderer == null)
                return;

            // 디스크 에셋 머티리얼만 프리팹에 물린다 (인메모리는 리로드 후 마젠타)
            targetRenderer.sharedMaterial = GreyboxMaterials.EnsureForColor(color);
        }

        static void RemoveCollider(GameObject target)
        {
            Collider existing = target.GetComponent<Collider>();

            if (existing == null)
                return;

            Object.DestroyImmediate(existing);
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Field"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Field");

            if (!AssetDatabase.IsValidFolder(ObjectFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs/Field", "Objects");
        }
    }
}
