using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Obstacle
{
    /// <summary>
    /// 굴러오는 블록 (웹 프로토타입 이식). 플레이어 정면(포그 너머)에서 스폰돼
    /// 플레이어 쪽(-z)으로 굴러오며, 접촉 시 HP 피해를 준다. 뒤로 지나가거나
    /// 붕괴 전선에 닿으면 스스로 사라진다. 개별 블록의 이동/충돌만 담당하고
    /// 스폰 주기는 RollingBlockSpawner가 소유. 수치는 스포너가 주입.
    /// </summary>
    public sealed class RollingBlock : MonoBehaviour
    {
        float speed;
        float damage;

        float despawnBehind;
        bool hit;

        Transform visual;
        Material visualMaterial;
        PlayerController player;
        PlayerHealth playerHealth;
        float spin;

        const float ContactRadius = 0.85f;
        const float BehindMargin = 14f;

        public void Initialize(float speed, float damage, PlayerController player)
        {
            this.speed = speed;
            this.damage = damage;
            this.player = player;

            if (player != null)
                playerHealth = player.Health;

            BuildVisual();
        }

        void Update()
        {
            if (!IsRunActive())
            {
                Destroy(gameObject);
                return;
            }

            Advance();
            SpinVisual();
            CheckContact();
            CheckDespawn();
        }

        void OnDestroy()
        {
            if (visualMaterial != null)
                Destroy(visualMaterial);
        }

        void Advance()
        {
            // 플레이어 쪽(-z)으로 굴러온다
            transform.position += Vector3.back * (speed * Time.deltaTime);
        }

        void SpinVisual()
        {
            if (visual == null)
                return;

            // 진행 방향(-z)에 맞춰 x축으로 구른다
            spin += speed * Time.deltaTime * 90f;
            visual.localRotation = Quaternion.Euler(-spin, 0f, 0f);
        }

        void CheckContact()
        {
            if (hit)
                return;

            if (player == null || player.State == PlayerState.Dead || playerHealth == null)
                return;

            Vector3 playerPosition = player.transform.position;

            float dx = Mathf.Abs(playerPosition.x - transform.position.x);
            float dz = Mathf.Abs(playerPosition.z - transform.position.z);

            if (dx > ContactRadius || dz > ContactRadius)
                return;

            // 한 블록당 1회만 피해 - 굴러 지나가며 여러 프레임 겹쳐도 중복 타격 없음
            hit = true;

            playerHealth.Damage(damage, "rolling");

            if (CameraShake.Instance != null)
                CameraShake.Instance.AddImpulse(0.3f);
        }

        void CheckDespawn()
        {
            float playerZ = player != null ? player.transform.position.z : despawnBehind;

            // 플레이어 뒤로 충분히 지나갔거나 붕괴 전선에 닿으면 정리
            if (transform.position.z < playerZ - BehindMargin)
            {
                Destroy(gameObject);
                return;
            }

            CollapseFront collapse = CollapseFront.Instance;

            if (collapse != null && transform.position.z < collapse.FrontZ)
                Destroy(gameObject);
        }

        void BuildVisual()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            cube.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            // 판정은 거리 기반 - 콜라이더 없음
            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Destroy(cubeCollider);

            Renderer renderer = cube.GetComponent<Renderer>();

            if (renderer != null)
            {
                visualMaterial = renderer.material;
                visualMaterial.color = new Color(0.43f, 0.37f, 0.3f);
            }

            visual = cube.transform;
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
