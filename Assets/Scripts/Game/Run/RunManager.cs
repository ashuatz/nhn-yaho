using System;
using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 수명 주기의 얇은 코디네이터. 상태 전이는 RunStateMachine,
    /// 시간 정책은 RunTimer에 위임하고 여기서는 조립과 시드/깊이만 관리한다.
    /// </summary>
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

            StateMachine = gameObject.AddComponent<RunStateMachine>();
            Timer = gameObject.AddComponent<RunTimer>();
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

            // 타이머 한계도 시드 난수에서 뽑아 같은 시드 = 같은 런이 되게 한다
            float limit = Mathf.Lerp(
                Settings.timerMinSeconds,
                Settings.timerMaxSeconds,
                (float)Rng.NextDouble());
            Timer.Begin(limit);

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

        /// <summary>탈출 성공. 정산은 S7에서 연결.</summary>
        public void CompleteExtraction()
        {
            if (!StateMachine.TryTransition(RunState.Extracted))
                return;

            Timer.Stop();
            UnityEngine.Debug.Log($"[Run] Extracted. depth={Depth} elapsed={Timer.Elapsed:F1}s");
        }

        /// <summary>사망. 전량 손실은 S7 정산에서 연결.</summary>
        public void KillRun(string cause)
        {
            if (!StateMachine.TryTransition(RunState.Dead))
                return;

            Timer.Stop();
            UnityEngine.Debug.Log($"[Run] Dead. cause={cause} depth={Depth} elapsed={Timer.Elapsed:F1}s");
        }

        int ResolveSeed()
        {
            if (Settings.seedOverride != 0)
                return Settings.seedOverride;

            return Environment.TickCount;
        }
    }
}
