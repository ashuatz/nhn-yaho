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

        // 인스턴스 머티리얼 - 파괴 시 함께 해제 (누수 방지, Codex 검토 반영)
        Material visualMaterial;

        // 위험 그리드 핸들 (M3-1). 0 = 미표시
        int dangerHandle;

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

            // 폭발 범위를 바닥 셀로 표시 (M3-1) - 기폭 시작과 동시에 노출
            if (Scavenger.Segment.DangerGrid.Instance != null)
            {
                dangerHandle = Scavenger.Segment.DangerGrid.Instance.ShowCircle(
                    transform.position, explosionRadius);
            }
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
            if (visualMaterial == null)
                return;

            // 기폭이 가까울수록 빠르게 점멸
            float remaining01 = Mathf.Clamp01(1f - fuseElapsed / fuseSeconds);
            float frequency = Mathf.Lerp(14f, 3f, remaining01);
            float pulse = Mathf.PingPong(Time.time * frequency, 1f);

            visualMaterial.color = Color.Lerp(IdleColor, WarnColor, pulse);
        }

        void Explode()
        {
            if (trackedPlayer != null)
            {
                float distance = Vector3.Distance(trackedPlayer.transform.position, transform.position);

                // 체력 도입 (사용자 지시): 즉사 대신 큰 피해. 컴포넌트 없으면 즉사 폴백
                if (distance <= explosionRadius)
                {
                    PlayerHealth health = trackedPlayer.GetComponent<PlayerHealth>();

                    if (health != null)
                        health.Damage(2, "bomb");
                    else
                        trackedPlayer.Kill("bomb");
                }

                // 피격/근접 폭발 피드백 (사용자 지시): 가까울수록 강한 쉐이크
                if (CameraShake.Instance != null)
                {
                    float proximity01 = 1f - Mathf.Clamp01(distance / (explosionRadius * 2f));
                    CameraShake.Instance.AddImpulse(Mathf.Lerp(0.15f, 0.9f, proximity01));
                }
            }

            // 배경이 폭발에 반응 (brg-shooter 바운스 이식)
            if (Scavenger.Segment.EnvironmentRenderer.Active != null)
            {
                Scavenger.Segment.EnvironmentRenderer.Active.AddImpulse(
                    transform.position, radius: 14f, strength: 2.4f);
            }

            SpawnExplosionVisual();
            Destroy(gameObject);
        }

        // 구간 정리/침몰 파괴 등 어떤 경로로 사라져도 위험 표시를 남기지 않는다
        void OnDestroy()
        {
            if (visualMaterial != null)
                Destroy(visualMaterial);

            if (dangerHandle == 0)
                return;

            if (Scavenger.Segment.DangerGrid.Instance != null)
                Scavenger.Segment.DangerGrid.Instance.Hide(dangerHandle);

            dangerHandle = 0;
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
            {
                // 인스턴스 머티리얼은 GO 파괴로 해제되지 않는다 - 함께 지연 파괴
                Material blastMaterial = blastRenderer.material;
                blastMaterial.color = new Color(1f, 0.45f, 0.1f);
                Destroy(blastMaterial, 0.3f);
            }

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
            {
                visualMaterial = visualRenderer.material;
                visualMaterial.color = IdleColor;
            }
        }
    }
}
