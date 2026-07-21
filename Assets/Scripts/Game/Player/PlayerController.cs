using System;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 상태 소유자. 입력을 읽고 상태에 따라 PlayerMotor를 구동한다.
    /// 조작 (그레이박스): A/D 또는 좌우 화살표 = 좌우, S 홀드 = 정지, E 홀드 = 루팅(S3).
    /// </summary>
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
            ReadInput();

            if (State == PlayerState.Dead || State == PlayerState.AtChoice)
                return;

            if (State == PlayerState.Looting)
            {
                // 이동은 LootSpot이 루팅을 끝내기 전까지 완전 정지 (ADR-0001)
                return;
            }

            bool brakeHeld = ReadBrakeHeld();

            if (State == PlayerState.Advancing && brakeHeld)
                Transition(PlayerState.Stopped);
            else if (State == PlayerState.Stopped && !brakeHeld)
                Transition(PlayerState.Advancing);

            bool advance = State == PlayerState.Advancing;
            motor.Step(LateralInput, advance);
        }

        // -- 외부 시스템 진입점 --------------------------------------------

        /// <summary>LootSpot이 홀드 시작 시 호출. 전진/정지 상태에서만 진입 가능.</summary>
        public bool TryBeginLoot()
        {
            if (State != PlayerState.Advancing && State != PlayerState.Stopped)
                return false;

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

        /// <summary>ChoiceNode 진입 시 호출 (S5).</summary>
        public void EnterChoice()
        {
            if (State == PlayerState.Dead)
                return;

            Transition(PlayerState.AtChoice);
        }

        /// <summary>선택 후 전진 재개 (S5).</summary>
        public void ExitChoice()
        {
            if (State != PlayerState.AtChoice)
                return;

            Transition(PlayerState.Advancing);
        }

        /// <summary>폭탄 등 즉사 요인이 호출 (S4).</summary>
        public void Kill(string cause)
        {
            if (State == PlayerState.Dead)
                return;

            Transition(PlayerState.Dead);

            if (RunManager.Instance != null)
                RunManager.Instance.KillRun(cause);
        }

        /// <summary>런 재시작 시 부트스트랩이 호출.</summary>
        public void ResetForNewRun()
        {
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

        static bool ReadBrakeHeld()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return false;

            return keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
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
