using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 프리팹 템플릿이 쓰는 직육면체 블록 생성기.
    ///
    /// 기본은 프리미티브 큐브 + 색 머티리얼(그레이박스)이고, 트림시트 경로
    /// (<see cref="FieldTrimSheetPrefabs"/>)가 <see cref="Source"/>를 잠시 갈아끼워
    /// 같은 템플릿에서 트림시트 메시가 나오게 한다 - 프리팹 형태를 정의하는 코드는
    /// 한 벌만 두고 "무엇을 만들지"만 바꾸는 구조다 (배경 트림시트 연동과 같은 방식).
    ///
    /// 교체는 반드시 try/finally로 되돌린다. 남아 있으면 이후의 다른 프리팹 생성까지
    /// 트림시트 메시로 나온다.
    ///
    /// <see cref="Context"/>는 지금 만드는 프리팹의 이름이고, 블록 이름과 합쳐져
    /// **에셋 이름**이 된다 (사용자 지시 2026-07-26: 색상 해시 이름 금지 -
    /// 에셋 이름만 보고 어디에 쓰이는지 알 수 있어야 한다).
    /// </summary>
    internal static class GreyboxBlockFactory
    {
        /// <summary>생성한 에셋 이름의 기본 접두사 (용도를 지정하지 않은 경우).</summary>
        internal const string DefaultContext = "Field";

        /// <summary>
        /// 블록 하나를 만드는 대체 경로 (에셋 이름, 크기, 색) -> 오브젝트.
        /// 반환물은 지정 크기를 이미 반영해야 한다 (스케일이든 구운 메시든).
        /// </summary>
        internal static System.Func<string, Vector3, Color, GameObject> Source;

        /// <summary>지금 만드는 프리팹 이름 (예: FarmingPoint_Top). 에셋 이름의 앞부분.</summary>
        internal static string Context = DefaultContext;

        /// <summary>
        /// 용도 컨텍스트를 잠시 바꾼다. 템플릿마다 using으로 감싸면 되돌리기를 잊지 않는다.
        /// </summary>
        internal static Scope UseContext(string context)
        {
            return new Scope(context);
        }

        internal readonly struct Scope : System.IDisposable
        {
            readonly string previous;

            public Scope(string context)
            {
                previous = Context;
                Context = string.IsNullOrEmpty(context) ? DefaultContext : context;
            }

            public void Dispose()
            {
                Context = previous;
            }
        }

        /// <summary>이 블록이 만드는 에셋의 이름 (프리팹 이름 + 블록 역할).</summary>
        internal static string ResolveAssetName(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
                return Context;

            return $"{Context}_{blockName}";
        }

        /// <summary>
        /// 블록 하나. 콜라이더가 필요하면 크기에 맞는 BoxCollider를 보장한다
        /// (대체 경로가 이미 붙였으면 그대로 둔다 - 구운 메시는 크기가 미세하게 다르다).
        /// </summary>
        internal static GameObject Create(string blockName, Vector3 size, Color color, bool withCollider)
        {
            GameObject block = CreateBody(blockName, size, color);
            block.name = blockName;

            Collider existing = block.GetComponent<Collider>();

            if (!withCollider)
            {
                if (existing != null)
                    Object.DestroyImmediate(existing);

                return block;
            }

            if (existing != null)
                return block;

            BoxCollider box = block.AddComponent<BoxCollider>();
            box.size = size;

            return block;
        }

        /// <summary>
        /// 프리미티브가 아닌 부위(원기둥 등)의 머티리얼. 이름 규칙을 블록과 맞춘다.
        /// </summary>
        internal static Material EnsureMaterial(string blockName, Color color)
        {
            // 색은 기준색이고 밝기 보정은 GreyboxMaterials가 건다 (설정: GreyboxTheme)
            return GreyboxMaterials.Ensure(
                ResolveAssetName(blockName), color, MaterialSubfolder, Field.GreyboxTone.Field);
        }

        /// <summary>필드 프리팹 머티리얼 폴더 (Assets/Materials/Greybox 하위).</summary>
        internal const string MaterialSubfolder = "Field";

        static GameObject CreateBody(string blockName, Vector3 size, Color color)
        {
            if (Source != null)
            {
                GameObject custom = Source(ResolveAssetName(blockName), size, color);

                if (custom != null)
                    return custom;
            }

            // 기본 경로: 프리미티브 큐브를 스케일로 늘린다
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = size;

            Renderer cubeRenderer = cube.GetComponent<Renderer>();

            if (cubeRenderer != null)
                cubeRenderer.sharedMaterial = EnsureMaterial(blockName, color);

            return cube;
        }
    }
}
