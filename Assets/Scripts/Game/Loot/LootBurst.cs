using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 파밍 연출용 파편 버스트 (사용자 지시: 뒤지는 동안 파티클이 팍팍 튄다).
    /// 그레이박스 규격에 맞춰 ParticleSystem 대신 큐브 파편 (SinkDebris와 동일 계열).
    /// 순수 비주얼 - 콜라이더 없음, 난수는 인스턴스 시드 전용 스트림
    /// (RunManager.Rng 배치 스트림 오염 금지 - SinkDebris 선례).
    /// </summary>
    public sealed class LootBurst : MonoBehaviour
    {
        const float Gravity = 9.81f;
        const float LifetimeMin = 0.45f;
        const float LifetimeMax = 0.85f;
        const float ChipSizeMin = 0.07f;
        const float ChipSizeMax = 0.16f;
        const float UpSpeedMin = 2.2f;
        const float UpSpeedMax = 4.2f;
        const float SideSpeedMax = 1.8f;
        const float TumbleMaxDegrees = 540f;

        sealed class Chip
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 TumbleAxis;
            public float TumbleSpeed;
            public float Life;
        }

        Chip[] chips;
        Material sharedMaterial;
        int aliveCount;

        /// <summary>origin에서 파편 count개를 위로 튀긴다. 색은 대상 오브젝트 톤.</summary>
        public static void Spawn(Vector3 origin, int count, Color color)
        {
            if (!Application.isPlaying || count <= 0)
                return;

            GameObject root = new GameObject("LootBurst");
            root.transform.position = origin;

            LootBurst burst = root.AddComponent<LootBurst>();
            burst.Build(origin, count, color);
        }

        void Build(Vector3 origin, int count, Color color)
        {
            System.Random rng = new System.Random(GetInstanceID());

            sharedMaterial = CreateRuntimeMaterial(color);
            chips = new Chip[count];
            aliveCount = count;

            for (int i = 0; i < count; i++)
                chips[i] = CreateChip(origin, rng);
        }

        void Update()
        {
            if (chips == null)
                return;

            float deltaTime = Time.deltaTime;

            for (int i = 0; i < chips.Length; i++)
            {
                Chip chip = chips[i];

                if (chip == null || chip.Transform == null)
                    continue;

                chip.Life -= deltaTime;

                if (chip.Life <= 0f)
                {
                    Destroy(chip.Transform.gameObject);
                    chips[i] = null;
                    aliveCount -= 1;
                    continue;
                }

                chip.Velocity.y -= Gravity * deltaTime;
                chip.Transform.position += chip.Velocity * deltaTime;
                chip.Transform.Rotate(chip.TumbleAxis, chip.TumbleSpeed * deltaTime, Space.World);
            }

            if (aliveCount <= 0)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (sharedMaterial != null)
                Destroy(sharedMaterial);
        }

        Chip CreateChip(Vector3 origin, System.Random rng)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = "Chip";
            chip.transform.SetParent(transform, true);
            chip.transform.position = origin;

            float size = Mathf.Lerp(ChipSizeMin, ChipSizeMax, (float)rng.NextDouble());
            chip.transform.localScale = new Vector3(size, size, size);

            Collider chipCollider = chip.GetComponent<Collider>();

            if (chipCollider != null)
                Destroy(chipCollider);

            Renderer chipRenderer = chip.GetComponent<Renderer>();

            if (chipRenderer != null)
            {
                chipRenderer.sharedMaterial = sharedMaterial;
                chipRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float sideSpeed = (float)rng.NextDouble() * SideSpeedMax;

            Vector3 velocity = new Vector3(
                Mathf.Cos(angle) * sideSpeed,
                Mathf.Lerp(UpSpeedMin, UpSpeedMax, (float)rng.NextDouble()),
                Mathf.Sin(angle) * sideSpeed);

            Vector3 tumbleAxis = new Vector3(
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f);

            if (tumbleAxis.sqrMagnitude < 0.01f)
                tumbleAxis = Vector3.right;

            return new Chip
            {
                Transform = chip.transform,
                Velocity = velocity,
                TumbleAxis = tumbleAxis.normalized,
                TumbleSpeed = ((float)rng.NextDouble() * 2f - 1f) * TumbleMaxDegrees,
                Life = Mathf.Lerp(LifetimeMin, LifetimeMax, (float)rng.NextDouble()),
            };
        }

        static Material CreateRuntimeMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            return material;
        }
    }
}
