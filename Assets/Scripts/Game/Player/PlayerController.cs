using System;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 상태 소유자. 입력을 읽고 상태에 따라 PlayerMotor를 구동한다.
    /// 조작 (ADR-0002): 클릭/스페이스 = 한 스텝 전진, A/D = 좌우, E 홀드 = 루팅.
    /// 실행 순서 -100: 입력 스냅샷을 소비자(LootSpot 등)보다 먼저 갱신해
    /// 프레임 지연 취소 문제를 막는다 (Codex 검토 반영).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerController : MonoBehaviour
    {
        public PlayerState State { get; private set; } = PlayerState.Advancing;

        /// <summary>이번 프레임 좌우 입력 (-1..1). 루팅 취소 판정에도 사용.</summary>
        public float LateralInput { get; private set; }

        /// <summary>상호작용(E) 홀드 여부. LootSpot이 읽는다.</summary>
        public bool InteractHeld { get; private set; }

        public event Action<PlayerState, PlayerState> StateChanged;

        PlayerMotor motor;

        public PlayerMotor Motor
        {
            get { return motor; }
        }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
        }

        void Update()
        {
            // 사망 후에는 입력 스냅샷도 갱신하지 않는다 - 루팅 등 소비자가 잔존 입력을 못 쓰게
            if (State == PlayerState.Dead)
                return;

            ReadInput();

            if (State == PlayerState.AtChoice)
                return;

            if (State == PlayerState.Looting)
            {
                // 루팅 중 전진 비허용 (ADR-0001) - 클릭 무시, 이동 완전 정지
                return;
            }

            if (ReadStepPressed())
                motor.RequestStep();

            motor.Tick(LateralInput);
        }

        // -- 외부 시스템 진입점 --------------------------------------------

        /// <summary>LootSpot이 홀드 시작 시 호출. 통상 상태에서만 진입 가능.</summary>
        public bool TryBeginLoot()
        {
            if (State != PlayerState.Advancing)
                return false;

            // 스텝 관성이 루팅 중에 이어지지 않게 정리
            motor.CancelSteps();

            Transition(PlayerState.Looting);
            return true;
        }

        /// <summary>LootSpot이 루팅 완료/취소 시 호출.</summary>
        public void EndLoot()
        {
            if (State != PlayerState.Looting)
                return;

            Transition(PlayerState.Advancing);
        }

        /// <summary>ChoiceNode 진입 시 호출.</summary>
        public void EnterChoice()
        {
            if (State == PlayerState.Dead)
                return;

            motor.CancelSteps();
            Transition(PlayerState.AtChoice);
        }

        /// <summary>선택 후 전진 재개.</summary>
        public void ExitChoice()
        {
            if (State != PlayerState.AtChoice)
                return;

            Transition(PlayerState.Advancing);
        }

        /// <summary>폭탄 등 즉사 요인이 호출.</summary>
        public void Kill(string cause)
        {
            if (State == PlayerState.Dead)
                return;

            motor.CancelSteps();

            // 잔존 입력 제거 - 사망 프레임에 루팅 완료/취소 판정이 이전 입력을 쓰지 못하게
            LateralInput = 0f;
            InteractHeld = false;

            Transition(PlayerState.Dead);

            if (RunManager.Instance != null)
                RunManager.Instance.KillRun(cause);
        }

        /// <summary>런 재시작 시 부트스트랩이 호출.</summary>
        public void ResetForNewRun()
        {
            motor.CancelSteps();
            Transition(PlayerState.Advancing);
        }

        // -- 내부 --------------------------------------------------------

        void ReadInput()
        {
            LateralInput = 0f;
            InteractHeld = false;

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                LateralInput -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                LateralInput += 1f;

            InteractHeld = keyboard.eKey.isPressed;
        }

        static bool ReadStepPressed()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                return true;

            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            return false;
        }

        void Transition(PlayerState next)
        {
            if (State == next)
                return;

            PlayerState previous = State;
            State = next;

            StateChanged?.Invoke(previous, next);
        }
    }
}
