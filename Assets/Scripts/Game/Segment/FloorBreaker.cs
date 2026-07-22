using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 충격 지점의 바닥 파괴 공용 유틸 (낙하물/기둥 붕괴 공용 - 3종째 규칙에 따른 추출).
    /// 전폭 함몰 = 점프 없는 플레이어에게 우회 불가 봉쇄이므로, 스트립을 분할해
    /// 충격 쪽만 침몰시키고 반대쪽에 안전 레인을 남긴다 (땅 꺼짐 트랩과 동일 규칙).
    /// </summary>
    public static class FloorBreaker
    {
        /// <summary>
        /// point 바로 아래의 바닥 스트립. RaycastAll - 위에 플레이어/잡동사니
        /// 콜라이더가 겹쳐 있어도 바닥을 찾는다. 없으면 null.
        /// </summary>
        public static FloorStrip FindStripBelow(Vector3 point, float probeDistance = 4f)
        {
            Ray probe = new Ray(point + Vector3.up * 1f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(probe, probeDistance);

            foreach (RaycastHit hit in hits)
            {
                FloorStrip strip = hit.collider.GetComponentInParent<FloorStrip>();

                if (strip == null || strip.IsSinking)
                    continue;

                return strip;
            }

            return null;
        }

        /// <summary>
        /// 스트립을 분할해 impactX 쪽만 침몰. 좁은 조각/단차 루트(합성 스케일)는
        /// 통째로 침몰. 부착물(루트/폭탄 등)은 소속 조각으로 재부착 (리스케일 왜곡 방지).
        /// </summary>
        public static void SinkWithSafeLane(FloorStrip strip, float impactX, float safeLaneWidth)
        {
            if (strip == null || strip.IsSinking)
                return;

            Transform stripTransform = strip.transform;
            float fullWidth = stripTransform.localScale.x;

            if (fullWidth < safeLaneWidth * 2f)
            {
                strip.Sink();
                return;
            }

            float laneWidth = Mathf.Clamp(safeLaneWidth, 1f, fullWidth - 1f);
            float sinkWidth = fullWidth - laneWidth;
            float sinkSide = impactX >= stripTransform.position.x ? 1f : -1f;

            Vector3 scale = stripTransform.localScale;
            Vector3 localPosition = stripTransform.localPosition;

            // 부착물(루트/폭탄 등)은 부모 리스케일 왜곡을 피해 잠시 떼어둔다
            List<Transform> attachments = new List<Transform>();

            for (int i = stripTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = stripTransform.GetChild(i);
                child.SetParent(stripTransform.parent, true);
                attachments.Add(child);
            }

            // 침몰 조각 (충격 쪽 가장자리, 신규)
            float sinkCenterX = sinkSide * (fullWidth * 0.5f - sinkWidth * 0.5f);

            GameObject sinkObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sinkObject.name = "FloorStrip (Broken)";
            sinkObject.transform.SetParent(stripTransform.parent, false);
            sinkObject.transform.localScale = new Vector3(sinkWidth, scale.y, scale.z);
            sinkObject.transform.localPosition = new Vector3(
                localPosition.x + sinkCenterX, localPosition.y, localPosition.z);

            FloorStrip sinkStrip = sinkObject.AddComponent<FloorStrip>();
            sinkStrip.depthMeters = strip.depthMeters;
            SegmentEnvironment.TintGameObject(sinkObject, new Color(0.3f, 0.31f, 0.33f));

            // 원본 = 안전 레인 (반대쪽 가장자리로 축소)
            float laneCenterX = -sinkSide * (fullWidth * 0.5f - laneWidth * 0.5f);
            stripTransform.localScale = new Vector3(laneWidth, scale.y, scale.z);
            stripTransform.localPosition = new Vector3(
                localPosition.x + laneCenterX, localPosition.y, localPosition.z);

            // 부착물 재부착: 충격 조각 위 = 함께 침몰, 안전 레인 위 = 유지
            float splitLocalX = localPosition.x + sinkSide * (fullWidth * 0.5f - sinkWidth);
            float splitX = stripTransform.parent != null
                ? stripTransform.parent.TransformPoint(new Vector3(splitLocalX, 0f, 0f)).x
                : splitLocalX;

            foreach (Transform attachment in attachments)
            {
                bool onSinkSide = sinkSide > 0f
                    ? attachment.position.x >= splitX
                    : attachment.position.x <= splitX;

                attachment.SetParent(onSinkSide ? sinkStrip.transform : stripTransform, true);
            }

            // 붕괴 전선이 나중에 지나갈 때 함께 처리되도록 등록
            if (CollapseFront.Instance != null)
                CollapseFront.Instance.RegisterStrips(new List<FloorStrip> { sinkStrip });

            sinkStrip.Sink();
        }
    }
}
