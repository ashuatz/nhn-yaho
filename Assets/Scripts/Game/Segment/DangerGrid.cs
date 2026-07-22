using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 위험 범위 그리드 표시 (M3-1, 큐비트 방식). 위험 영역을 바닥 셀 단위로
    /// 점멸시켜 범위를 명확히 읽게 한다. 폭탄 폭발 반경부터 적용, 땅 꺼짐(M3-2)
    /// 등 후속 위협도 같은 API를 쓴다.
    /// API: ShowCircle/ShowRect -> 핸들, Hide(핸들). 셀은 풀링.
    /// 셀 계산은 정적 순수 함수 - EditMode 테스트 대상.
    /// </summary>
    public sealed class DangerGrid : MonoBehaviour
    {
        public static DangerGrid Instance { get; private set; }

        [Header("셀 (월드 정렬 그리드)")]
        public float cellSize = 1f;
        [Range(0.5f, 1f)] public float cellVisualScale = 0.92f;
        public float cellY = 0.03f;

        [Header("점멸")]
        public Color baseColor = new Color(0.9f, 0.15f, 0.1f, 0.35f);
        public float pulseFrequency = 2.2f;
        [Range(0f, 1f)] public float pulseAlphaMin = 0.18f;
        [Range(0f, 1f)] public float pulseAlphaMax = 0.55f;

        readonly Dictionary<int, List<GameObject>> activeShapes = new Dictionary<int, List<GameObject>>();
        readonly Stack<GameObject> cellPool = new Stack<GameObject>();

        int nextHandle = 1;
        Material cellMaterial;

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (cellMaterial == null || activeShapes.Count == 0)
                return;

            // 전체 셀 공통 점멸 - 셀별 개별 애니메이션 불필요 (그레이박스)
            float wave01 = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;

            Color color = baseColor;
            color.a = Mathf.Lerp(pulseAlphaMin, pulseAlphaMax, wave01);
            cellMaterial.color = color;
        }

        void OnDestroy()
        {
            if (cellMaterial != null)
                Destroy(cellMaterial);
        }

        /// <summary>원형 위험 범위 표시. 반환 핸들로 Hide.</summary>
        public int ShowCircle(Vector3 center, float radius)
        {
            List<Vector2Int> cells = CellsInCircle(
                new Vector2(center.x, center.z), radius, cellSize);

            return ShowCells(cells);
        }

        /// <summary>사각 위험 범위 표시 (size = x/z 전체 폭). 반환 핸들로 Hide.</summary>
        public int ShowRect(Vector3 center, Vector2 size)
        {
            List<Vector2Int> cells = CellsInRect(
                new Vector2(center.x, center.z), size, cellSize);

            return ShowCells(cells);
        }

        public void Hide(int handle)
        {
            if (!activeShapes.TryGetValue(handle, out List<GameObject> cells))
                return;

            foreach (GameObject cell in cells)
            {
                if (cell == null)
                    continue;

                cell.SetActive(false);
                cellPool.Push(cell);
            }

            activeShapes.Remove(handle);
        }

        // -- 셀 계산 (순수 함수, EditMode 테스트 대상) ------------------------

        /// <summary>셀 중심이 반경 안에 드는 월드 정렬 셀 목록.</summary>
        public static List<Vector2Int> CellsInCircle(Vector2 center, float radius, float cellSize)
        {
            List<Vector2Int> cells = new List<Vector2Int>();

            if (radius <= 0f || cellSize <= 0f)
                return cells;

            int minX = Mathf.FloorToInt((center.x - radius) / cellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) / cellSize);
            int minY = Mathf.FloorToInt((center.y - radius) / cellSize);
            int maxY = Mathf.FloorToInt((center.y + radius) / cellSize);

            float sqrRadius = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 cellCenter = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

                    if ((cellCenter - center).sqrMagnitude <= sqrRadius)
                        cells.Add(new Vector2Int(x, y));
                }
            }

            return cells;
        }

        /// <summary>셀 중심이 사각 범위 안에 드는 월드 정렬 셀 목록.</summary>
        public static List<Vector2Int> CellsInRect(Vector2 center, Vector2 size, float cellSize)
        {
            List<Vector2Int> cells = new List<Vector2Int>();

            if (size.x <= 0f || size.y <= 0f || cellSize <= 0f)
                return cells;

            Vector2 half = size * 0.5f;

            int minX = Mathf.FloorToInt((center.x - half.x) / cellSize);
            int maxX = Mathf.FloorToInt((center.x + half.x) / cellSize);
            int minY = Mathf.FloorToInt((center.y - half.y) / cellSize);
            int maxY = Mathf.FloorToInt((center.y + half.y) / cellSize);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 cellCenter = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

                    bool insideX = Mathf.Abs(cellCenter.x - center.x) <= half.x;
                    bool insideY = Mathf.Abs(cellCenter.y - center.y) <= half.y;

                    if (insideX && insideY)
                        cells.Add(new Vector2Int(x, y));
                }
            }

            return cells;
        }

        // -- 내부 -----------------------------------------------------------

        int ShowCells(List<Vector2Int> cells)
        {
            List<GameObject> shape = new List<GameObject>(cells.Count);

            foreach (Vector2Int cell in cells)
            {
                GameObject cellObject = RentCell();

                cellObject.transform.position = new Vector3(
                    (cell.x + 0.5f) * cellSize, cellY, (cell.y + 0.5f) * cellSize);
                cellObject.SetActive(true);

                shape.Add(cellObject);
            }

            int handle = nextHandle;
            nextHandle += 1;

            activeShapes.Add(handle, shape);
            return handle;
        }

        GameObject RentCell()
        {
            while (cellPool.Count > 0)
            {
                GameObject pooled = cellPool.Pop();

                if (pooled != null)
                    return pooled;
            }

            return CreateCell();
        }

        GameObject CreateCell()
        {
            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = "DangerCell";
            cell.transform.SetParent(transform, false);
            cell.transform.localScale = new Vector3(
                cellSize * cellVisualScale, 0.05f, cellSize * cellVisualScale);

            // 표시 전용 - 충돌 금지
            Collider cellCollider = cell.GetComponent<Collider>();

            if (cellCollider != null)
                Destroy(cellCollider);

            Renderer cellRenderer = cell.GetComponent<Renderer>();

            if (cellRenderer != null)
            {
                cellRenderer.sharedMaterial = EnsureMaterial();
                cellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cellRenderer.receiveShadows = false;
            }

            return cell;
        }

        // 런타임 전용 머티리얼 - 에셋 저장 없음 (에디터 저장 시에만 마젠타 이슈)
        Material EnsureMaterial()
        {
            if (cellMaterial != null)
                return cellMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            cellMaterial = new Material(shader);
            cellMaterial.color = baseColor;

            // URP Unlit 투명 설정
            cellMaterial.SetFloat("_Surface", 1f);
            cellMaterial.SetFloat("_Blend", 0f);
            cellMaterial.SetOverrideTag("RenderType", "Transparent");
            cellMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cellMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            cellMaterial.SetFloat("_ZWrite", 0f);
            cellMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return cellMaterial;
        }
    }
}
