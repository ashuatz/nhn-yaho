using Scavenger.Diagnostics;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger
{
    /// <summary>
    /// 그레이박스 부트스트랩. 빈 씬의 오브젝트 하나에 붙이면 런 시스템을 코드로 조립한다.
    /// 씬 에셋 의존을 최소화하기 위한 v0.1 전용 구성 - 정식 씬 구성은 트랙 C에서.
    /// 단계(S1..S7)가 진행되며 조립 대상이 늘어난다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("비우면 기본값으로 생성 (그레이박스 편의)")]
        [SerializeField] RunSettings runSettings;

        RunManager runManager;

        void Awake()
        {
            BuildRunSystems();
        }

        void Start()
        {
            runManager.StartRun();
        }

        void Update()
        {
            HandleRestartInput();
        }

        void BuildRunSystems()
        {
            GameObject systems = new GameObject("RunSystems");
            systems.transform.SetParent(transform);

            runManager = systems.AddComponent<RunManager>();
            systems.AddComponent<RunDebugDashboard>();

            if (runSettings == null)
                runSettings = RunSettings.CreateDefault();

            runManager.Configure(runSettings);
        }

        // 그레이박스 편의: 런 종료(탈출/사망) 후 R로 재시작
        void HandleRestartInput()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (!keyboard.rKey.wasPressedThisFrame)
                return;

            RunState state = runManager.StateMachine.Current;

            if (state != RunState.Extracted && state != RunState.Dead)
                return;

            if (!runManager.StateMachine.TryTransition(RunState.Ready))
                return;

            runManager.StartRun();
        }
    }
}
