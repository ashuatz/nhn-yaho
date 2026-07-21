using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 그레이박스 생성/제거의 단일 경계. 생성 방식(Instantiate/Destroy)을
    /// 이 클래스 뒤에 숨겨 추후 풀링 교체가 가능하게 한다 (구현계획 v0.0.2).
    /// S2: 바닥+벽 1구간. S3+: 루트/폭탄/신호 배치, S5: 체인/제거.
    /// </summary>
    public sealed class SegmentSpawner : MonoBehaviour
    {
        public SegmentDefinition Definition { get; private set; }

        readonly List<GameObject> aliveSegments = new List<GameObject>();

        public void Configure(SegmentDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>startZ부터 시작하는 구간 하나를 만들고 루트를 돌려준다.</summary>
        public GameObject BuildSegment(int depth, float startZ)
        {
            if (Definition == null)
                Definition = SegmentDefinition.CreateDefault();

            GameObject root = new GameObject($"Segment_depth{depth}");
            root.transform.SetParent(transform);
            root.transform.position = new Vector3(0f, 0f, startZ);

            BuildShell(root.transform);

            aliveSegments.Add(root);
            return root;
        }

        public void DespawnAll()
        {
            foreach (GameObject segment in aliveSegments)
            {
                if (segment != null)
                    Destroy(segment);
            }

            aliveSegments.Clear();
        }

        // -- 그레이박스 셸 -------------------------------------------------

        void BuildShell(Transform parent)
        {
            float length = Definition.lengthMeters;
            float halfWidth = Definition.corridorHalfWidth;
            float wallHeight = Definition.wallHeight;

            // 바닥: 윗면이 y=0에 오도록
            GameObject floor = CreateBlock(parent, "Floor");
            floor.transform.localScale = new Vector3(halfWidth * 2f + 1f, 0.2f, length);
            floor.transform.localPosition = new Vector3(0f, -0.1f, length * 0.5f);
            Tint(floor, new Color(0.35f, 0.35f, 0.38f));

            // 좌우 가벽
            GameObject leftWall = CreateBlock(parent, "WallLeft");
            leftWall.transform.localScale = new Vector3(0.5f, wallHeight, length);
            leftWall.transform.localPosition = new Vector3(-(halfWidth + 0.25f), wallHeight * 0.5f, length * 0.5f);
            Tint(leftWall, new Color(0.25f, 0.25f, 0.3f));

            GameObject rightWall = CreateBlock(parent, "WallRight");
            rightWall.transform.localScale = new Vector3(0.5f, wallHeight, length);
            rightWall.transform.localPosition = new Vector3(halfWidth + 0.25f, wallHeight * 0.5f, length * 0.5f);
            Tint(rightWall, new Color(0.25f, 0.25f, 0.3f));
        }

        static GameObject CreateBlock(Transform parent, string blockName)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);
            return block;
        }

        static void Tint(GameObject block, Color color)
        {
            Renderer blockRenderer = block.GetComponent<Renderer>();

            if (blockRenderer == null)
                return;

            blockRenderer.material.color = color;
        }
    }
}
