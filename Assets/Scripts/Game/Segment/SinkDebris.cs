using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 바닥 붕괴의 그리드 단위 낙하 연출 (사용자 지시). 판정은 FloorStrip이
    /// 일괄로 하고, 여기서는 표면을 1m 셀 조각으로 쪼개 조각마다 지연/틸트를
    /// 넣어 무너짐을 표현한다. 순수 비주얼 - 콜라이더 없음.
    /// </summary>
    public sealed class SinkDebris : MonoBehaviour
    {
        const float CellSize = 1f;
        const float MaxDelaySeconds = 0.4f;
        const float FallSpeedMin = 3.8f;
        const float FallSpeedMax = 5.5f;
        const float TiltSpeedMaxDegrees = 60f;
        const float KillY = -9f;
        const int MaxCells = 80;

        sealed class Piece
        {
            public Transform Transform;
            public float Delay;
            public float FallSpeed;
            public Vector3 TiltAxis;
            public float TiltSpeed;
        }

        Piece[] pieces;
        Material sharedMaterial;
        int aliveCount;

        /// <summary>
        /// 표면 렌더러 자리에 셀 조각들을 만들고 원본 렌더러는 끈다.
        /// 조각은 월드에 독립 배치 - 스트립 본체의 일괄 침몰과 무관하게 연출된다.
        /// </summary>
        public static void Spawn(Renderer surface)
        {
            if (surface == null || !Application.isPlaying)
                return;

            GameObject root = new GameObject("SinkDebris");
            root.transform.position = surface.transform.position;

            SinkDebris debris = root.AddComponent<SinkDebris>();
            debris.Build(surface);

            surface.enabled = false;
        }

        void Build(Renderer surface)
        {
            Transform source = surface.transform;
            Vector3 scale = source.lossyScale;

            int countX = Mathf.Max(1, Mathf.RoundToInt(scale.x / CellSize));
            int countZ = Mathf.Max(1, Mathf.RoundToInt(scale.z / CellSize));

            // 폭주 방지 상한 - 넘으면 셀을 키워서라도 개수를 맞춘다
            while (countX * countZ > MaxCells)
            {
                countX = Mathf.Max(1, countX / 2);
                countZ = Mathf.Max(1, countZ / 2);
            }

            float pieceWidth = scale.x / countX;
            float pieceDepth = scale.z / countZ;

            sharedMaterial = CreateMaterial(surface);
            pieces = new Piece[countX * countZ];
            aliveCount = pieces.Length;

            System.Random rng = new System.Random(GetInstanceID());
            Vector3 origin = source.position;
            int index = 0;

            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    Vector3 offset = new Vector3(
                        (x + 0.5f) * pieceWidth - scale.x * 0.5f,
                        0f,
                        (z + 0.5f) * pieceDepth - scale.z * 0.5f);

                    pieces[index] = CreatePiece(origin + offset, pieceWidth, scale.y, pieceDepth, rng);
                    index += 1;
                }
            }
        }

        void Update()
        {
            if (pieces == null)
                return;

            float deltaTime = Time.deltaTime;

            for (int i = 0; i < pieces.Length; i++)
            {
                Piece piece = pieces[i];

                if (piece == null || piece.Transform == null)
                    continue;

                if (piece.Delay > 0f)
                {
                    piece.Delay -= deltaTime;
                    continue;
                }

                // 낙하 가속 + 조각별 틸트 회전 = 무너짐 표현
                piece.FallSpeed += 9.81f * deltaTime;

                Vector3 position = piece.Transform.position;
                position.y -= piece.FallSpeed * deltaTime;
                piece.Transform.position = position;

                piece.Transform.Rotate(piece.TiltAxis, piece.TiltSpeed * deltaTime, Space.World);

                if (position.y > KillY)
                    continue;

                Destroy(piece.Transform.gameObject);
                pieces[i] = null;
                aliveCount -= 1;
            }

            if (aliveCount <= 0)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (sharedMaterial != null)
                Destroy(sharedMaterial);
        }

        Piece CreatePiece(Vector3 position, float width, float height, float depth, System.Random rng)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Piece";
            piece.transform.SetParent(transform, true);
            piece.transform.position = position;
            piece.transform.localScale = new Vector3(width * 0.96f, height, depth * 0.96f);

            Collider pieceCollider = piece.GetComponent<Collider>();

            if (pieceCollider != null)
                Destroy(pieceCollider);

            Renderer pieceRenderer = piece.GetComponent<Renderer>();

            if (pieceRenderer != null)
            {
                pieceRenderer.sharedMaterial = sharedMaterial;
                pieceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Vector3 tiltAxis = new Vector3(
                (float)rng.NextDouble() * 2f - 1f, 0f, (float)rng.NextDouble() * 2f - 1f);

            if (tiltAxis.sqrMagnitude < 0.01f)
                tiltAxis = Vector3.right;

            return new Piece
            {
                Transform = piece.transform,
                Delay = (float)rng.NextDouble() * MaxDelaySeconds,
                FallSpeed = Mathf.Lerp(FallSpeedMin, FallSpeedMax, (float)rng.NextDouble()),
                TiltAxis = tiltAxis.normalized,
                TiltSpeed = ((float)rng.NextDouble() * 2f - 1f) * TiltSpeedMaxDegrees,
            };
        }

        // 원본 표면 색을 이어받는 런타임 전용 머티리얼 (조각 공유, 파괴 시 해제)
        Material CreateMaterial(Renderer surface)
        {
            Material material = new Material(surface.sharedMaterial);
            return material;
        }
    }
}
