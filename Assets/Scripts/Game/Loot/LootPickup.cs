using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 루팅 완료 시 튀어나오는 아이템 조각 (사용자 지시).
    /// 포물선을 그리며 바닥에 떨어지고, 착지 후 상호작용 키(E)로 줍는다.
    /// 가치/무게는 원본 정의를 조각 수로 나눈 것 - 획득 시점에 인벤토리 반영.
    /// 지지 스트립의 자식으로 붙어 바닥과 함께 침몰한다 (LootSpot과 동일 규칙).
    /// </summary>
    public sealed class LootPickup : MonoBehaviour
    {
        /// <summary>씬의 모든 조각. HUD 라벨/프롬프트가 순회한다.</summary>
        public static readonly List<LootPickup> All = new List<LootPickup>();

        /// <summary>줍기 가능 조각 (범위 안 + 착지 완료). HUD 키 프롬프트 참조.</summary>
        public static LootPickup PromptTarget { get; private set; }

        /// <summary>원본 정의 (공유 참조 - 조각용 런타임 SO를 만들지 않는다).</summary>
        public LootDefinition Definition { get; private set; }

        /// <summary>이 조각의 가치/무게 지분. 합계 = 원본 (LootSpot이 분배).</summary>
        public int PieceValue { get; private set; }
        public float PieceWeight { get; private set; }

        /// <summary>착지 완료 여부. 착지 전에는 줍을 수 없다.</summary>
        public bool IsResting { get; private set; }

        const float PickupRadius = 1.3f;
        const float Gravity = 9.81f;
        const float BounceRestitution = 0.35f;
        const float BounceLateralDamp = 0.5f;
        const float KillY = -8f;
        const float VisualSize = 0.32f;

        Vector3 velocity;
        float floorY;
        int bouncesLeft = 1;
        Vector3 tumbleAxis = Vector3.right;
        float tumbleSpeed;
        Material visualMaterial;
        Material trailMaterial;
        Transform visual;
        TrailRenderer trail;

        static PlayerController player;

        /// <summary>
        /// 발사 초기화. velocity로 날아가 floorY(발사 지점의 바닥 높이)에 착지한다.
        /// pieceValue/pieceWeight = 이 조각의 지분 (원본 정의는 공유 참조).
        /// </summary>
        public static LootPickup Launch(
            Transform parent, LootDefinition definition, int pieceValue, float pieceWeight,
            Vector3 worldOrigin, Vector3 velocity, float floorY, System.Random rng)
        {
            GameObject pickupObject = new GameObject($"LootPickup_{definition.id}");
            pickupObject.transform.SetParent(parent, true);
            pickupObject.transform.position = worldOrigin;

            LootPickup pickup = pickupObject.AddComponent<LootPickup>();
            pickup.Definition = definition;
            pickup.PieceValue = pieceValue;
            pickup.PieceWeight = pieceWeight;
            pickup.velocity = velocity;
            pickup.floorY = floorY;

            Vector3 axis = new Vector3(
                (float)rng.NextDouble() * 2f - 1f, 0f, (float)rng.NextDouble() * 2f - 1f);

            if (axis.sqrMagnitude < 0.01f)
                axis = Vector3.right;

            pickup.tumbleAxis = axis.normalized;
            pickup.tumbleSpeed = 180f + (float)rng.NextDouble() * 360f;

            pickup.BuildVisual(definition.tier);
            pickup.BuildTrail(definition.tier);
            return pickup;
        }

        void OnEnable()
        {
            All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);

            if (PromptTarget == this)
                PromptTarget = null;
        }

        void OnDestroy()
        {
            // 비주얼용 인스턴스 머티리얼 해제 (누수 방지 - Codex 교차 검토)
            if (visualMaterial != null)
                Destroy(visualMaterial);

            if (trailMaterial != null)
                Destroy(trailMaterial);
        }

        void Update()
        {
            // 침몰한 바닥과 함께 떨어진 조각 정리
            if (transform.position.y < KillY)
            {
                Destroy(gameObject);
                return;
            }

            if (!IsResting)
            {
                TickFlight();
                return;
            }

            TickPickup();
        }

        void TickFlight()
        {
            float deltaTime = Time.deltaTime;

            velocity.y -= Gravity * deltaTime;
            transform.position += velocity * deltaTime;

            // 회전은 비주얼 자식만 자기 중심으로 - 루트를 돌리면 오프셋 자식이
            // 궤도를 돌아 메쉬가 깨져 보인다 (사용자 지적)
            if (visual != null)
                visual.Rotate(tumbleAxis, tumbleSpeed * deltaTime, Space.World);

            if (velocity.y >= 0f || transform.position.y > floorY)
                return;

            // 착지: 한 번 짧게 튀고 정지 - 곡선 낙하의 마무리 감
            Vector3 position = transform.position;
            position.y = floorY;
            transform.position = position;

            if (bouncesLeft > 0)
            {
                bouncesLeft -= 1;
                velocity.y = -velocity.y * BounceRestitution;
                velocity.x *= BounceLateralDamp;
                velocity.z *= BounceLateralDamp;
                return;
            }

            IsResting = true;

            if (visual != null)
                visual.localRotation = Quaternion.identity;

            // 트레일은 잔상만 남기고 페이드 - 착지 후에는 그리지 않는다
            if (trail != null)
            {
                trail.emitting = false;
                Destroy(trail.gameObject, 0.6f);
                trail = null;
            }
        }

        void TickPickup()
        {
            if (!CanBePicked())
            {
                if (PromptTarget == this)
                    PromptTarget = null;

                return;
            }

            if (PromptTarget == null)
                PromptTarget = this;

            if (!player.InteractHeld)
                return;

            Collect();
        }

        bool CanBePicked()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return false;

            if (!TryResolvePlayer())
                return false;

            if (player.State != PlayerState.Advancing && player.State != PlayerState.Looting)
                return false;

            Vector3 delta = player.transform.position - transform.position;
            return delta.sqrMagnitude <= PickupRadius * PickupRadius;
        }

        void Collect()
        {
            RunManager run = RunManager.Instance;
            run.Inventory.Add(Definition, PieceValue, PieceWeight);

            UnityEngine.Debug.Log(
                $"[Loot] 줍기 {Definition.displayName} +{PieceValue} (total {run.Inventory.TotalValue})");

            LootBurst.Spawn(transform.position, 3, LootDefinition.GradeColor(Definition.tier));
            Destroy(gameObject);
        }

        static bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = Object.FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        // 비행 잔상 (사용자 지시: 간단한 트레일). 착지 시 페이드 후 제거
        void BuildTrail(int tier)
        {
            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = new Vector3(0f, VisualSize * 0.5f, 0f);

            trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0.02f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            trailMaterial = CreateTrailMaterial(LootDefinition.GradeColor(tier));
            trail.sharedMaterial = trailMaterial;

            Color tierColor = LootDefinition.GradeColor(tier);
            trail.startColor = tierColor;
            trail.endColor = new Color(tierColor.r, tierColor.g, tierColor.b, 0f);
        }

        // 끝 페이드(버텍스 알파)가 살도록 투명 서페이스로 설정. 셰이더 부재 폴백 포함
        static Material CreateTrailMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.color = color;

            // URP 서페이스 타입 = Transparent (알파 블렌드)
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }

        void BuildVisual(int tier)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(VisualSize, VisualSize, VisualSize);
            cube.transform.localPosition = new Vector3(0f, VisualSize * 0.5f, 0f);
            visual = cube.transform;

            // 판정은 거리 기반 - 콜라이더 불필요
            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Destroy(cubeCollider);

            Renderer cubeRenderer = cube.GetComponent<Renderer>();

            if (cubeRenderer != null)
            {
                visualMaterial = new Material(cubeRenderer.sharedMaterial);
                visualMaterial.color = LootDefinition.GradeColor(tier);
                cubeRenderer.sharedMaterial = visualMaterial;
            }
        }
    }
}
