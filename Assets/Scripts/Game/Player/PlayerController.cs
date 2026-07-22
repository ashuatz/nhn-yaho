using System;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 상태 소유자. 입력을 읽고 상태에 따라 PlayerMotor를 구동한다.
    /// 조작 (ADR-0007): WASD/화살표 홀드 = 2D 벡터 이동 (누르는 동안만 이동),
    /// 가상 조이스틱은 SetExternalMoveInput으로 아날로그 벡터를 공급.
    /// E 홀드 = 루팅. 점프 없음. 낙사 있음 (일정 깊이 이하 낙하 시 사망).
    /// 실행 순서 -100: 입력 스냅샷을 소비자(LootSpot 등)보다 먼저 갱신.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerController : MonoBehaviour
    {
        public PlayerState State { get; private set; } = PlayerState.Advancing;

        /// <summary>이번 프레임 좌우 입력 (-1..1). 루팅 취소 판정에 사용.</summary>
        public float LateralInput { get; private set; }

        /// <summary>상호작용(E) 홀드 여부. LootSpot이 읽는다.</summary>
        public bool InteractHeld { get; private set; }

        public event Action<PlayerState, PlayerState> StateChanged;

        const float FallDeathY = -4f;

        PlayerMotor motor;

        // 가상 조이스틱 등 외부 UI가 공급하는 이동 벡터 (매 프레임 갱신 전제)
        Vector2 externalMoveInput;

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
            // 사망 후에는 입력 스냅샷도 갱신하지 않는다
            if (State == PlayerState.Dead)
                return;

            CheckFallDeath();

            if (State == PlayerState.Dead)
                return;

            ReadInput();

            if (State == PlayerState.AtChoice || State == PlayerState.Looting)
            {
                // 이동 입력은 무시하되 중력/붕괴 클램프는 유지 (검증 반영):
                // 이동 벡터는 상태 진입 시 해제되어 있으므로 Tick은 낙하/경계 처리만 한다.
                // 루팅/선택 대기 중에도 발밑이 무너지면 떨어진다 - 붕괴 면역 방지 (ADR-0006)
                motor.Tick();
                return;
            }

            ApplyMoveInput();
            motor.Tick();
        }

        // -- 외부 시스템 진입점 --------------------------------------------

        /// <summary>LootSpot이 홀드 시작 시 호출. 통상 상태에서만 진입 가능.</summary>
        public bool TryBeginLoot()
        {
            if (State != PlayerState.Advancing)
                return false;

            // 루팅 = 정지. 이동 벡터 해제
            motor.ClearMoveInput();

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

            motor.ClearMoveInput();
            Transition(PlayerState.AtChoice);
        }

        /// <summary>선택 후 이동 재개.</summary>
        public void ExitChoice()
        {
            if (State != PlayerState.AtChoice)
                return;

            Transition(PlayerState.Advancing);
        }

        /// <summary>폭탄/낙사/붕괴 등 즉사 요인이 호출.</summary>
        public void Kill(string cause)
        {
            if (State == PlayerState.Dead)
                return;

            motor.ClearMoveInput();

            // 잔존 입력 제거 - 사망 프레임에 루팅 판정이 이전 입력을 쓰지 못하게
            LateralInput = 0f;
            InteractHeld = false;
            externalMoveInput = Vector2.zero;

            Transition(PlayerState.Dead);

            if (RunManager.Instance != null)
                RunManager.Instance.KillRun(cause);
        }

        /// <summary>
        /// 가상 조이스틱 등 외부 UI의 이동 벡터 공급 (ADR-0007).
        /// 드래그 중 매 프레임 호출하고 놓으면 zero를 보낼 것.
        /// </summary>
        public void SetExternalMoveInput(Vector2 input)
        {
            externalMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        /// <summary>런 재시작 시 GameFlow가 호출.</summary>
        public void ResetForNewRun()
        {
            motor.ClearMoveInput();
            motor.ResetVertical();
            Transition(PlayerState.Advancing);
        }

        // -- 내부 --------------------------------------------------------

        void CheckFallDeath()
        {
            if (transform.position.y > FallDeathY)
                return;

            Kill("fall");
        }

        void ReadInput()
        {
            InteractHeld = false;

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null)
                InteractHeld = keyboard.eKey.isPressed;

            // 루팅 취소 판정용 좌우 입력 - 키보드와 조이스틱 합산 (ADR-0001)
            LateralInput = Mathf.Clamp(ReadKeyboardMove().x + externalMoveInput.x, -1f, 1f);
        }

        // 2D 벡터 홀드 이동 (ADR-0007): 키보드 + 가상 조이스틱 합산, 크기 1 클램프
        void ApplyMoveInput()
        {
            motor.SetMoveInput(ReadKeyboardMove() + externalMoveInput);
        }

        static Vector2 ReadKeyboardMove()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return Vector2.zero;

            Vector2 move = Vector2.zero;

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                move.y += 1f;

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                move.y -= 1f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                move.x -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                move.x += 1f;

            return move;
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
