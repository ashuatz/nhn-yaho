using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 그레이박스 플레이어 리그 생성. 로직 루트와 비주얼 자식을 분리해
    /// 트랙 B에서 비주얼만 교체할 수 있게 한다 (구현계획 v0.0.2 섹션 3.4).
    /// 비주얼: 큐브 2개 (머리 + 몸) - 사용자 지정.
    /// </summary>
    public static class PlayerFactory
    {
        public static PlayerController Create(Vector3 position)
        {
            GameObject root = new GameObject("Player");
            root.transform.position = position;

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.8f, 0f);

            root.AddComponent<PlayerMotor>();
            PlayerController player = root.AddComponent<PlayerController>();

            BuildVisual(root.transform);

            return player;
        }

        static void BuildVisual(Transform parent)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            // 몸통 큐브
            GameObject body = CreateVisualCube(visual.transform, "Body");
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.45f);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            TintCube(body, new Color(0.8f, 0.6f, 0.2f));

            // 머리 큐브
            GameObject head = CreateVisualCube(visual.transform, "Head");
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            head.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            TintCube(head, new Color(0.9f, 0.75f, 0.6f));
        }

        static GameObject CreateVisualCube(Transform parent, string cubeName)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            cube.transform.SetParent(parent, false);

            // 비주얼 전용 - CharacterController와의 충돌 간섭 제거
            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Object.Destroy(cubeCollider);

            return cube;
        }

        static void TintCube(GameObject cube, Color color)
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();

            if (cubeRenderer == null)
                return;

            cubeRenderer.material.color = color;
        }
    }
}
