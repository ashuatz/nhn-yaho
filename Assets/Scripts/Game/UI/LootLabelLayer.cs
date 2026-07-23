using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 아이템 머리 위 간략 설명 라벨.
    /// 자동 수집(ADR-0008 웹 이식) 전환으로 길가 아이템은 이름/설명 라벨 없이
    /// 발광 아이콘(LootVisual 큐브)만으로 표시한다 - showLabels가 기본 false.
    /// 라벨이 필요하면 인스펙터에서 showLabels를 켠다 (리그/로직은 보존).
    /// </summary>
    public sealed class LootLabelLayer : MonoBehaviour
    {
        [Header("라벨 표시 여부 (웹 이식: 기본 꺼짐 - 아이콘만)")]
        public bool showLabels = false;

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
            // 자동 수집 이식: 길가 아이템은 아이콘만 - 라벨은 기본 꺼짐
            if (!showLabels)
            {
                HideAll();
                return;
            }

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

            // 자동 수집(ADR-0008)으로 조각(LootPickup)이 사라져 스팟 라벨만 표시한다
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
