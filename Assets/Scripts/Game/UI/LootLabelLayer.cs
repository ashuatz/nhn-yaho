using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 아이템 머리 위 간략 설명 라벨 (M5-1, 사용자 지시: 뭐가 뭔지 알 수 있게).
    /// 루팅 스팟과 떨어진 조각을 화면에 투영해 이름/가치/무게/설명을 띄운다.
    /// 라벨 풀은 프리팹의 비활성 템플릿을 복제해 만든다 (개수 고정, 매 프레임 재배치).
    /// </summary>
    public sealed class LootLabelLayer : MonoBehaviour
    {
        [Header("uGUI 리그 (HudCanvas 프리팹이 배선)")]
        public RectTransform labelRoot;
        public Text labelTemplate;

        [Header("표시 규칙")]
        public int maxLabels = 8;
        public float maxDistance = 14f;
        public float spotLabelHeight = 1.15f;
        public float pickupLabelHeight = 0.6f;

        struct Candidate
        {
            public Vector3 WorldPosition;
            public string Label;
            public float SqrDistance;
        }

        readonly List<Candidate> candidates = new List<Candidate>(32);

        Text[] pool;
        Camera viewCamera;
        PlayerController player;

        void Awake()
        {
            if (labelRoot == null || labelTemplate == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[LootLabel] uGUI 리그 미배선. HudCanvas 프리팹을 재생성하거나 배선할 것.");
                enabled = false;
                return;
            }

            pool = new Text[Mathf.Max(1, maxLabels)];

            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(labelTemplate, labelRoot);
                pool[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!IsRunning() || !TryResolveScene())
            {
                HideAll();
                return;
            }

            GatherCandidates();

            // 가까운 것부터 - 풀 초과분은 먼 것이 밀린다
            candidates.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));

            int used = 0;

            foreach (Candidate candidate in candidates)
            {
                if (used >= pool.Length)
                    break;

                Vector3 screen = viewCamera.WorldToScreenPoint(candidate.WorldPosition);

                // 카메라 뒤는 표시하지 않는다
                if (screen.z <= 0f)
                    continue;

                Text label = pool[used];
                label.text = candidate.Label;
                label.rectTransform.position = new Vector3(screen.x, screen.y, 0f);

                if (!label.gameObject.activeSelf)
                    label.gameObject.SetActive(true);

                used += 1;
            }

            for (int i = used; i < pool.Length; i++)
            {
                if (pool[i].gameObject.activeSelf)
                    pool[i].gameObject.SetActive(false);
            }
        }

        void GatherCandidates()
        {
            candidates.Clear();

            Vector3 playerPosition = player.transform.position;
            float maxSqr = maxDistance * maxDistance;

            foreach (LootSpot spot in LootSpot.All)
            {
                if (spot == null || spot.Definition == null)
                    continue;

                float sqr = (spot.transform.position - playerPosition).sqrMagnitude;

                if (sqr > maxSqr)
                    continue;

                candidates.Add(new Candidate
                {
                    WorldPosition = spot.transform.position + Vector3.up * spotLabelHeight,
                    Label = BuildSpotLabel(spot.Definition),
                    SqrDistance = sqr,
                });
            }

            foreach (LootPickup pickup in LootPickup.All)
            {
                if (pickup == null || !pickup.IsResting || pickup.Definition == null)
                    continue;

                float sqr = (pickup.transform.position - playerPosition).sqrMagnitude;

                if (sqr > maxSqr)
                    continue;

                candidates.Add(new Candidate
                {
                    WorldPosition = pickup.transform.position + Vector3.up * pickupLabelHeight,
                    Label = $"{pickup.Definition.displayName} 조각 +{pickup.PieceValue}",
                    SqrDistance = sqr,
                });
            }
        }

        static string BuildSpotLabel(LootDefinition definition)
        {
            string headline = $"{definition.displayName} +{definition.value} (무게 {definition.weight:F0})";

            if (string.IsNullOrEmpty(definition.shortDescription))
                return headline;

            return $"{headline}\n{definition.shortDescription}";
        }

        void HideAll()
        {
            if (pool == null)
                return;

            foreach (Text label in pool)
            {
                if (label != null && label.gameObject.activeSelf)
                    label.gameObject.SetActive(false);
            }
        }

        static bool IsRunning()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }

        bool TryResolveScene()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            return viewCamera != null && player != null;
        }
    }
}
