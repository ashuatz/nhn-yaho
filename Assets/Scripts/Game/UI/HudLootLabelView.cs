using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scavenger.UI
{
    /// <summary>
    /// 아이템 머리 위 간략 라벨 (uGUI LootLabelLayer 대체).
    /// LootSpot.All / LootPickup.All을 순회해 플레이어 반경 안의 대상만 화면에 투영한다.
    /// 월드 -> 패널 좌표 변환은 RuntimePanelUtils가 담당 (카메라 투영 + 패널 스케일 반영).
    /// 라벨은 풀로 재사용한다 - 매 프레임 생성하면 GC가 튄다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudLootLabelView : MonoBehaviour
    {
        [Header("표시 범위")]
        public float maxDistance = 14f;
        [Range(1, 24)] public int maxLabels = 8;

        [Header("월드 오프셋 (아이템 머리 위)")]
        public float labelHeight = 0.9f;

        UIDocument document;
        VisualElement layer;
        Camera viewCamera;
        PlayerController player;

        readonly List<Label> pool = new List<Label>();
        readonly List<Candidate> candidates = new List<Candidate>();

        struct Candidate
        {
            public Vector3 WorldPosition;
            public string Text;
            public float SqrDistance;
        }

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
        }

        void OnDisable()
        {
            layer = null;
            pool.Clear();
        }

        void LateUpdate()
        {
            if (!EnsureBound())
                return;

            candidates.Clear();

            if (IsRunActive() && TryResolveRefs())
            {
                CollectSpots();
                CollectPickups();
                candidates.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));
            }

            DrawLabels();
        }

        bool EnsureBound()
        {
            if (layer != null)
                return true;

            if (document == null)
                return false;

            VisualElement root = document.rootVisualElement;

            if (root == null)
                return false;

            layer = root.Q<VisualElement>("label-layer");
            return layer != null;
        }

        bool TryResolveRefs()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            if (viewCamera == null)
                viewCamera = Camera.main;

            return player != null && viewCamera != null;
        }

        void CollectSpots()
        {
            float maxSqr = maxDistance * maxDistance;
            Vector3 origin = player.transform.position;

            foreach (LootSpot spot in LootSpot.All)
            {
                if (spot == null || spot.Definition == null)
                    continue;

                float sqr = (spot.transform.position - origin).sqrMagnitude;

                if (sqr > maxSqr)
                    continue;

                candidates.Add(new Candidate
                {
                    WorldPosition = spot.transform.position,
                    Text = BuildText(spot.Definition, spot.Definition.value, spot.Definition.weight),
                    SqrDistance = sqr,
                });
            }
        }

        void CollectPickups()
        {
            float maxSqr = maxDistance * maxDistance;
            Vector3 origin = player.transform.position;

            foreach (LootPickup pickup in LootPickup.All)
            {
                if (pickup == null || pickup.Definition == null)
                    continue;

                float sqr = (pickup.transform.position - origin).sqrMagnitude;

                if (sqr > maxSqr)
                    continue;

                candidates.Add(new Candidate
                {
                    WorldPosition = pickup.transform.position,
                    Text = BuildText(pickup.Definition, pickup.PieceValue, pickup.PieceWeight),
                    SqrDistance = sqr,
                });
            }
        }

        static string BuildText(LootDefinition definition, int value, float weight)
        {
            string grade = LootDefinition.GradeName(definition.tier);

            return $"{definition.displayName} · {grade}\n+{value} ({weight:F1}kg)";
        }

        void DrawLabels()
        {
            int shown = Mathf.Min(candidates.Count, maxLabels);

            EnsurePool(shown);

            for (int i = 0; i < pool.Count; i++)
            {
                Label label = pool[i];

                if (i >= shown)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                Candidate candidate = candidates[i];
                Vector3 world = candidate.WorldPosition + Vector3.up * labelHeight;

                // 카메라 뒤쪽은 그리지 않는다 (뒤집힌 좌표로 화면에 튄다)
                Vector3 viewPoint = viewCamera.WorldToViewportPoint(world);

                if (viewPoint.z <= 0f)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                Vector2 panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(
                    layer.panel, world, viewCamera);

                label.style.display = DisplayStyle.Flex;
                label.text = candidate.Text;
                label.style.left = panelPoint.x;
                label.style.top = panelPoint.y;
            }
        }

        void EnsurePool(int count)
        {
            while (pool.Count < count)
            {
                Label label = new Label();
                label.AddToClassList("world-label");
                label.pickingMode = PickingMode.Ignore;

                // 좌상단 기준 좌표를 아이템 머리 위 중앙으로 옮긴다
                label.style.translate = new StyleTranslate(
                    new Translate(Length.Percent(-50f), Length.Percent(-100f)));

                layer.Add(label);
                pool.Add(label);
            }
        }

        static bool IsRunActive()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }
    }
}
