using System;
using Scavenger.Player;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 끝의 선택지: 탈출(정산) 또는 전진(깊이 +1).
    /// 진입 시 플레이어 정지, W = 전진 / E = 탈출.
    /// 탈출 잠금(시간 초과) 판정은 S6에서 SegmentSpawner 콜백 쪽에 추가.
    /// </summary>
    public sealed class ChoiceNode : MonoBehaviour
    {
        /// <summary>HUD 프롬프트가 참조하는 현재 활성 노드.</summary>
        public static ChoiceNode Active { get; private set; }

        Action<ChoiceNode> onAdvance;
        Action<ChoiceNode> onExtract;
        PlayerController player;
        bool consumed;

        public void Initialize(Action<ChoiceNode> onAdvance, Action<ChoiceNode> onExtract)
        {
            this.onAdvance = onAdvance;
            this.onExtract = onExtract;
        }

        void OnTriggerEnter(Collider other)
        {
            if (consumed || Active == this)
                return;

            PlayerController enteringPlayer = other.GetComponent<PlayerController>();

            if (enteringPlayer == null)
                return;

            player = enteringPlayer;
            player.EnterChoice();
            Active = this;
        }

        void Update()
        {
            if (Active != this || consumed)
                return;

            HandleChoiceInput();
        }

        // Destroy 지연 중 잔존 방지 - 비활성화 즉시 정리 (Codex 검토 반영)
        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        void HandleChoiceInput()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                ChooseAdvance();
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
                ChooseExtract();
        }

        void ChooseAdvance()
        {
            consumed = true;
            Active = null;

            player.ExitChoice();
            onAdvance?.Invoke(this);
        }

        void ChooseExtract()
        {
            consumed = true;
            Active = null;

            onExtract?.Invoke(this);
        }
    }
}
