using System;
using Scavenger.Loot;
using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 수명 주기의 얇은 코디네이터. 상태 전이는 RunStateMachine,
    /// 시간 정책은 RunTimer에 위임하고 여기서는 시드/깊이만 관리한다.
    /// 씬에 미리 배치되는 컴포넌트 - 런타임 AddComponent 조립 금지 (프로젝트 규약).
    /// </summary>
    [RequireComponent(typeof(RunStateMachine))]
    [RequireComponent(typeof(RunTimer))]
    public sealed class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        public RunStateMachine StateMachine { get; private set; }
        public RunTimer Timer { get; private set; }
        public RunSettings Settings { get; private set; }

        /// <summary>결정적 재현용 시드. 런 시작 시 확정된다.</summary>
        public int Seed { get; private set; }

        /// <summary>현재 깊이. 1부터 시작, 전진 선택 시 +1.</summary>
        public int Depth { get; private set; }

        /// <summary>런 전용 난수. 시드 기반이므로 같은 시드 = 같은 배치.</summary>
        public System.Random Rng { get; private set; }

        /// <summary>런 한정 인벤토리. 사망 시 전량 소멸.</summary>
        public RunInventory Inventory { get; } = new RunInventory();

        public event Action RunStarted;
        public event Action<int> DepthChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[Run] Duplicate RunManager destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            StateMachine = GetComponent<RunStateMachine>();
            Timer = GetComponent<RunTimer>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Configure(RunSettings settings)
        {
            Settings = settings;
        }

        public void StartRun()
        {
            if (Settings == null)
                Settings = RunSettings.CreateDefault();

            if (!StateMachine.TryTransition(RunState.Running))
                return;

            Seed = ResolveSeed();
            Rng = new System.Random(Seed);
            Depth = 1;
            Inventory.Clear();

            Timer.Begin();

            UnityEngine.Debug.Log($"[Run] Started. seed={Seed} depth={Depth}");
            RunStarted?.Invoke();
        }

        public void AdvanceDepth()
        {
            if (StateMachine.Current != RunState.Running)
                return;

            Depth += 1;
            DepthChanged?.Invoke(Depth);
        }

        /// <summary>탈출 성공. 정산은 RunSettlement가 StateChanged로 수행.</summary>
        public void CompleteExtraction()
        {
            // 타이머 정지를 전이(이벤트 발화)보다 먼저 - 이벤트 구독자가
            // 예외를 던져도 런 마감 처리가 반쯤 남지 않게 한다 (Codex 검토 반영)
            if (StateMachine.Current != RunState.Running)
                return;

            Timer.Stop();
            UnityEngine.Debug.Log($"[Run] Extracted. depth={Depth} elapsed={Timer.Elapsed:F1}s");

            StateMachine.TryTransition(RunState.Extracted);
        }

        /// <summary>사망. 전량 손실 - 스태시에 아무것도 반영되지 않는다.</summary>
        public void KillRun(string cause)
        {
            if (StateMachine.Current != RunState.Running)
                return;

            Timer.Stop();
            UnityEngine.Debug.Log($"[Run] Dead. cause={cause} depth={Depth} elapsed={Timer.Elapsed:F1}s");

            StateMachine.TryTransition(RunState.Dead);
        }

        int ResolveSeed()
        {
            if (Settings.seedOverride != 0)
                return Settings.seedOverride;

            return Environment.TickCount;
        }
    }
}
