using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 밀기 트랩 (M3-3). 감지 반경에 플레이어가 들어오면 짧은 예고(점멸) 후
    /// 붕괴 쪽(-z)으로 밀어낸다. 밀린 거리만큼 붕괴 전선/구멍/폭탄과의 조합 위협.
    /// 쿨다운 후 재장전 - 머무르면 반복해서 밀린다.
    /// 수치는 SegmentSpawner 프리팹 필드에서 주입.
    /// </summary>
    public sealed class PushTrap : MonoBehaviour
    {
        [Header("발동 (스포너가 주입)")]
        public float detectionRadius = 2.2f;
        public float telegraphSeconds = 0.45f;
        public float pushSpeed = 7.5f;
        public float cooldownSeconds = 3.5f;

        static readonly Color IdleColor = new Color(0.55f, 0.2f, 0.65f);
        static readonly Color WarnColor = new Color(0.95f, 0.4f, 1f);

        PlayerController player;
        Renderer visualRenderer;
        float telegraphRemaining;
        float cooldownRemaining;
        bool telegraphing;

        public void Initialize(float detectionRadius, float telegraphSeconds, float pushSpeed, float cooldownSeconds)
        {
            this.detectionRadius = detectionRadius;
            this.telegraphSeconds = telegraphSeconds;
            this.pushSpeed = pushSpeed;
            this.cooldownSeconds = cooldownSeconds;

            BuildVisual();
        }

        void Update()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
                SetVisualColor(IdleColor);
                return;
            }

            if (!TryResolvePlayer())
                return;

            if (telegraphing)
            {
                TickTelegraph();
                return;
            }

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance > detectionRadius * detectionRadius)
                return;

            telegraphing = true;
            telegraphRemaining = telegraphSeconds;
        }

        void TickTelegraph()
        {
            // 예고 점멸 - 기폭 직전 폭탄과 같은 문법 (빠른 점멸 = 곧 발동)
            float pulse = Mathf.PingPong(Time.time * 9f, 1f);
            SetVisualColor(Color.Lerp(IdleColor, WarnColor, pulse));

            telegraphRemaining -= Time.deltaTime;

            if (telegraphRemaining > 0f)
                return;

            Fire();
        }

        void Fire()
        {
            telegraphing = false;
            cooldownRemaining = cooldownSeconds;
            SetVisualColor(IdleColor);

            // 예고가 끝난 시점에 아직 반경 안이면 붕괴 쪽으로 민다
            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance > detectionRadius * detectionRadius)
                return;

            player.Motor.AddImpulse(Vector3.back * pushSpeed);

            if (CameraShake.Instance != null)
                CameraShake.Instance.AddImpulse(0.35f);
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        void SetVisualColor(Color color)
        {
            if (visualRenderer == null)
                return;

            visualRenderer.material.color = color;
        }

        void BuildVisual()
        {
            // 본체 + -z쪽 피스톤 노브 (밀어내는 방향 표시)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Visual";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.6f, 0.7f, 0.35f);
            body.transform.localPosition = new Vector3(0f, 0.35f, 0.15f);

            Collider bodyCollider = body.GetComponent<Collider>();

            if (bodyCollider != null)
                Destroy(bodyCollider);

            visualRenderer = body.GetComponent<Renderer>();
            SetVisualColor(IdleColor);

            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Cube);
            knob.name = "Knob";
            knob.transform.SetParent(transform, false);
            knob.transform.localScale = new Vector3(0.35f, 0.35f, 0.25f);
            knob.transform.localPosition = new Vector3(0f, 0.35f, -0.15f);

            Collider knobCollider = knob.GetComponent<Collider>();

            if (knobCollider != null)
                Destroy(knobCollider);

            Renderer knobRenderer = knob.GetComponent<Renderer>();

            if (knobRenderer != null)
                knobRenderer.material.color = new Color(0.9f, 0.9f, 0.95f);
        }
    }
}
