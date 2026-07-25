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
    /// </summary>
    internal static class GreyboxBlockFactory
    {
        /// <summary>
        /// 블록 하나를 만드는 대체 경로 (크기, 색) -> 오브젝트.
        /// 반환물은 지정 크기를 이미 반영해야 한다 (스케일이든 구운 메시든).
        /// </summary>
        internal static System.Func<Vector3, Color, GameObject> Source;

        /// <summary>
        /// 블록 하나. 콜라이더가 필요하면 크기에 맞는 BoxCollider를 보장한다
        /// (대체 경로가 이미 붙였으면 그대로 둔다 - 구운 메시는 크기가 미세하게 다르다).
        /// </summary>
        internal static GameObject Create(string blockName, Vector3 size, Color color, bool withCollider)
        {
            GameObject block = CreateBody(size, color);
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

        static GameObject CreateBody(Vector3 size, Color color)
        {
            if (Source != null)
            {
                GameObject custom = Source(size, color);

                if (custom != null)
                    return custom;
            }

            // 기본 경로: 프리미티브 큐브를 스케일로 늘린다
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = size;

            Renderer cubeRenderer = cube.GetComponent<Renderer>();

            if (cubeRenderer != null)
                cubeRenderer.sharedMaterial = GreyboxMaterials.EnsureForColor(color);

            return cube;
        }
    }
}
