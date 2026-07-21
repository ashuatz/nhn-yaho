using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 시한폭발형 장애물 (v0.1 유일 장애물).
    /// 배치 시점이 아니라 플레이어가 감지 반경에 들어와야 카운트다운 시작
    /// (교차 검증 반영 - 보기도 전에 터지는 문제 방지).
    /// 파생 1종뿐이므로 추상화 없음. 2종째 추가 시 공통 계약 추출.
    /// </summary>
    public sealed class Bomb : MonoBehaviour
    {
        float fuseSeconds;
        float explosionRadius;

        bool armed;
        float fuseElapsed;
        PlayerController trackedPlayer;
        Renderer visualRenderer;

        static readonly Color IdleColor = new Color(0.4f, 0.1f, 0.1f);
        static readonly Color WarnColor = new Color(1f, 0.2f, 0.1f);

        public void Initialize(float detectionRadius, float fuseSeconds, float explosionRadius)
        {
            this.fuseSeconds = fuseSeconds;
            this.explosionRadius = explosionRadius;

            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = detectionRadius;

            BuildVisual();
        }

        void OnTriggerEnter(Collider other)
        {
            if (armed)
                return;

            PlayerController player = other.GetComponent<PlayerController>();

            if (player == null)
                return;

            // 감지 반경 진입 = 기폭 시작. 이후 벗어나도 멈추지 않는다
            armed = true;
            fuseElapsed = 0f;
            trackedPlayer = player;
        }

        void Update()
        {
            if (!armed)
                return;

            fuseElapsed += Time.deltaTime;

            UpdateBlink();

            if (fuseElapsed < fuseSeconds)
                return;

            Explode();
        }

        void UpdateBlink()
        {
            if (visualRenderer == null)
                return;

            // 기폭이 가까울수록 빠르게 점멸
            float remaining01 = Mathf.Clamp01(1f - fuseElapsed / fuseSeconds);
            float frequency = Mathf.Lerp(14f, 3f, remaining01);
            float pulse = Mathf.PingPong(Time.time * frequency, 1f);

            visualRenderer.material.color = Color.Lerp(IdleColor, WarnColor, pulse);
        }

        void Explode()
        {
            if (trackedPlayer != null)
            {
                float distance = Vector3.Distance(trackedPlayer.transform.position, transform.position);

                if (distance <= explosionRadius)
                    trackedPlayer.Kill("bomb");
            }

            SpawnExplosionVisual();
            Destroy(gameObject);
        }

        void SpawnExplosionVisual()
        {
            GameObject blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blast.name = "BombBlast";
            blast.transform.position = transform.position;
            blast.transform.localScale = Vector3.one * (explosionRadius * 2f);

            Collider blastCollider = blast.GetComponent<Collider>();

            if (blastCollider != null)
                Destroy(blastCollider);

            Renderer blastRenderer = blast.GetComponent<Renderer>();

            if (blastRenderer != null)
                blastRenderer.material.color = new Color(1f, 0.45f, 0.1f);

            Destroy(blast, 0.25f);
        }

        void BuildVisual()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Destroy(cubeCollider);

            visualRenderer = cube.GetComponent<Renderer>();

            if (visualRenderer != null)
                visualRenderer.material.color = IdleColor;
        }
    }
}
