using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scavenger.ArtTools
{
    /// <summary>큐브 면이 아틀라스에서 어떤 셀을 쓸지.</summary>
    public enum TrimSheetCellMode
    {
        /// <summary>모든 타일이 같은 셀. 균일한 룩.</summary>
        Single,

        /// <summary>타일마다 셀을 섞는다. 아틀라스를 실제로 활용하는 경로.</summary>
        MixPerTile,
    }

    /// <summary>한 큐브를 굽는 데 필요한 입력.</summary>
    public struct TrimSheetCubeOptions
    {
        /// <summary>월드 크기(m). 피벗은 Unity 기본 큐브와 같은 중심이다.</summary>
        public Vector3 size;

        /// <summary>셀 한 장이 덮는 월드 크기(m). 타일 분할 수를 결정한다.</summary>
        public float worldUnitsPerCell;

        public TrimSheetCellMode cellMode;

        /// <summary><see cref="TrimSheetCellMode.Single"/>에서 쓸 셀 인덱스.</summary>
        public int cellIndex;

        /// <summary>타일마다 UV를 90도 단위로 돌린다. 셀 사방에 모르타르가 있어 이음선은 유지된다.</summary>
        public bool randomRotation;

        /// <summary>격자점 산포 크기(m). 타일 하나하나가 다른 모양이 된다.</summary>
        public float shapeJitter;

        /// <summary>격자점을 안쪽으로 눌러 넣는 크기(m). 모서리가 닳은 룩.</summary>
        public float shapePress;

        public int seed;
    }

    /// <summary>
    /// 트림시트 큐브 메시 빌더.
    ///
    /// 아틀라스 셀은 텍스처의 일부라서 UV를 1 밖으로 늘려 타일링할 수 없다
    /// (이웃 셀이 나온다). 그래서 각 면을 <see cref="TrimSheetCubeOptions.worldUnitsPerCell"/>
    /// 크기의 쿼드 격자로 쪼개고 쿼드 하나가 셀 하나를 꽉 채우게 UV를 굽는다.
    /// 결과: 머티리얼 한 장, 큐브 크기와 무관하게 일정한 텍셀 밀도, 타일 단위 셀 혼합.
    ///
    /// 정점 위치는 큐브 전체에 걸친 정수 격자에서 뽑고, 변위는 그 격자 좌표의
    /// 해시로 결정한다. 순차 난수로 흔들면 인접한 면이 공유 정점을 서로 다르게
    /// 밀어버려 큐브 모서리가 갈라진다 - 해시라야 어느 면에서 계산해도 같은 값이 나온다.
    /// </summary>
    public static class TrimSheetCubeMesh
    {
        /// <summary>한 축의 타일 분할 상한. 큰 블록 + 촘촘한 셀에서 정점 폭발을 막는다.</summary>
        public const int MaxTilesPerAxis = 32;

        const float MinSize = 0.05f;

        /// <summary>
        /// 변위가 타일 크기의 이 비율을 넘지 못하게 한다.
        /// 넘으면 이웃 쿼드를 관통해 면이 뒤집힌다.
        /// </summary>
        const float MaxOffsetPerTile = 0.25f;

        /// <summary>
        /// 면 하나가 큐브 격자의 어느 평면인지. 격자 좌표를 계산하기 위한 축 매핑이라
        /// 방향 벡터가 아니라 축 인덱스로 들고 있다.
        ///
        /// 와인딩 규약: 삼각형 (0,1,2)의 앞면 법선 = cross(uDir, vDir)
        /// (Unity 기본 쿼드와 같은 규약). 아래 표는 그걸 만족하도록 짜여 있다.
        /// </summary>
        struct Face
        {
            /// <summary>면이 놓인 축 (0=x, 1=y, 2=z).</summary>
            public int fixedAxis;

            /// <summary>그 축의 최대쪽 면인지 (아니면 최소쪽).</summary>
            public bool fixedAtMax;

            public int uAxis;
            public bool uForward;

            public int vAxis;
            public bool vForward;
        }

        static readonly Face[] Faces =
        {
            // +X: uDir = -z, vDir = +y
            new Face { fixedAxis = 0, fixedAtMax = true, uAxis = 2, uForward = false, vAxis = 1, vForward = true },

            // -X: uDir = +z, vDir = +y
            new Face { fixedAxis = 0, fixedAtMax = false, uAxis = 2, uForward = true, vAxis = 1, vForward = true },

            // +Y: uDir = +x, vDir = -z
            new Face { fixedAxis = 1, fixedAtMax = true, uAxis = 0, uForward = true, vAxis = 2, vForward = false },

            // -Y: uDir = +x, vDir = +z
            new Face { fixedAxis = 1, fixedAtMax = false, uAxis = 0, uForward = true, vAxis = 2, vForward = true },

            // +Z: uDir = +x, vDir = +y
            new Face { fixedAxis = 2, fixedAtMax = true, uAxis = 0, uForward = true, vAxis = 1, vForward = true },

            // -Z: uDir = -x, vDir = +y
            new Face { fixedAxis = 2, fixedAtMax = false, uAxis = 0, uForward = false, vAxis = 1, vForward = true },
        };

        /// <summary>
        /// 큐브 메시를 만든다. 반환 메시는 아직 에셋이 아니다
        /// (<see cref="TrimSheetAssets.SaveMesh"/>로 저장한다).
        /// </summary>
        public static Mesh Build(TrimSheetDefinition definition, TrimSheetCubeOptions options)
        {
            if (definition == null)
                return null;

            Vector3 size = ClampSize(options.size);
            float cellSpan = Mathf.Max(TrimSheetDefinition.MinWorldUnitsPerCell, options.worldUnitsPerCell);

            Vector3Int counts = new Vector3Int(
                ResolveTileCount(size.x, cellSpan),
                ResolveTileCount(size.y, cellSpan),
                ResolveTileCount(size.z, cellSpan));

            ResolveOffsetLimits(size, counts, options, out float jitter, out float press);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            // 시드 고정 - 같은 입력이면 같은 메시가 나와야 다시 구웠을 때 룩이 유지된다
            System.Random rng = new System.Random(options.seed);

            for (int i = 0; i < Faces.Length; i++)
                AppendFace(Faces[i], definition, options, size, counts, jitter, press, rng, vertices, normals, uvs, triangles);

            Mesh mesh = new Mesh();
            mesh.name = "TrimSheetCube";

            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);

            // 노멀맵을 나중에 물릴 수 있게 탄젠트까지 채운다
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// 메시가 실제로 쓰는 크기. 0/음수 입력을 막는다.
        /// 콜라이더 등 부수 컴포넌트도 이걸 써야 메시와 어긋나지 않는다.
        /// </summary>
        public static Vector3 ClampSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinSize, size.x),
                Mathf.Max(MinSize, size.y),
                Mathf.Max(MinSize, size.z));
        }

        /// <summary>
        /// 면 길이를 셀 크기로 나눠 타일 수를 정한다. 최소 1장은 항상 깔린다.
        ///
        /// 타일 수가 정해지면 그 수로 면을 정확히 나눠 덮는다. 즉 면 길이가 셀 크기의
        /// 정수배가 아니면 타일이 늘거나 줄어든다 (1.49m / 셀 1m = 1장을 1.49m로 늘림).
        /// 타일을 잘라서 텍셀 밀도를 지키는 대안은 면 경계에서 모르타르가 끊겨 더 나쁘다.
        /// 실제 늘어난 정도는 창의 CUBE 섹션이 표시한다.
        /// </summary>
        public static int ResolveTileCount(float faceLength, float cellSpan)
        {
            float span = Mathf.Max(TrimSheetDefinition.MinWorldUnitsPerCell, cellSpan);
            int count = Mathf.RoundToInt(Mathf.Max(MinSize, faceLength) / span);

            return Mathf.Clamp(count, 1, MaxTilesPerAxis);
        }

        /// <summary>
        /// 변위 상한. 가장 작은 타일의 <see cref="MaxOffsetPerTile"/>배를 넘으면
        /// 이웃 쿼드를 관통해 면이 뒤집히므로 잘라낸다.
        /// </summary>
        static void ResolveOffsetLimits(
            Vector3 size,
            Vector3Int counts,
            TrimSheetCubeOptions options,
            out float jitter,
            out float press)
        {
            float smallestTile = Mathf.Min(
                size.x / counts.x,
                Mathf.Min(size.y / counts.y, size.z / counts.z));

            float limit = smallestTile * MaxOffsetPerTile;

            jitter = Mathf.Clamp(options.shapeJitter, 0f, limit);
            press = Mathf.Clamp(options.shapePress, 0f, limit);
        }

        static void AppendFace(
            Face face,
            TrimSheetDefinition definition,
            TrimSheetCubeOptions options,
            Vector3 size,
            Vector3Int counts,
            float jitter,
            float press,
            System.Random rng,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int tileCountU = counts[face.uAxis];
            int tileCountV = counts[face.vAxis];

            for (int v = 0; v < tileCountV; v++)
            {
                for (int u = 0; u < tileCountU; u++)
                {
                    Vector3 v0 = ResolveVertex(face, u, v, tileCountU, tileCountV, size, counts, options, jitter, press);
                    Vector3 v1 = ResolveVertex(face, u + 1, v, tileCountU, tileCountV, size, counts, options, jitter, press);
                    Vector3 v2 = ResolveVertex(face, u + 1, v + 1, tileCountU, tileCountV, size, counts, options, jitter, press);
                    Vector3 v3 = ResolveVertex(face, u, v + 1, tileCountU, tileCountV, size, counts, options, jitter, press);

                    int baseIndex = vertices.Count;

                    vertices.Add(v0);
                    vertices.Add(v1);
                    vertices.Add(v2);
                    vertices.Add(v3);

                    // 쿼드마다 하드 엣지. 변위된 실제 코너에서 법선을 다시 구해야
                    // 눌린 모양이 라이팅에 드러난다 (축 법선을 그대로 쓰면 평평해 보인다).
                    Vector3 normal = ResolveQuadNormal(v0, v1, v2, v3);

                    for (int i = 0; i < 4; i++)
                        normals.Add(normal);

                    AppendTileUvs(definition, options, rng, uvs);

                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 3);
                }
            }
        }

        static Vector3 ResolveVertex(
            Face face,
            int u,
            int v,
            int tileCountU,
            int tileCountV,
            Vector3 size,
            Vector3Int counts,
            TrimSheetCubeOptions options,
            float jitter,
            float press)
        {
            Vector3Int coord = ResolveLatticeCoord(face, u, v, tileCountU, tileCountV, counts);

            return ResolveLatticePosition(coord, size, counts)
                   + ResolveLatticeOffset(coord, counts, options, jitter, press);
        }

        /// <summary>
        /// 면 위의 (u, v) 타일 코너를 큐브 전체 격자 좌표로. 인접한 면이 공유하는
        /// 코너는 여기서 같은 정수 좌표로 떨어져야 변위 후에도 붙어 있는다.
        /// </summary>
        static Vector3Int ResolveLatticeCoord(
            Face face,
            int u,
            int v,
            int tileCountU,
            int tileCountV,
            Vector3Int counts)
        {
            Vector3Int coord = Vector3Int.zero;

            coord[face.fixedAxis] = face.fixedAtMax ? counts[face.fixedAxis] : 0;
            coord[face.uAxis] = face.uForward ? u : tileCountU - u;
            coord[face.vAxis] = face.vForward ? v : tileCountV - v;

            return coord;
        }

        static Vector3 ResolveLatticePosition(Vector3Int coord, Vector3 size, Vector3Int counts)
        {
            return new Vector3(
                -size.x * 0.5f + coord.x * (size.x / counts.x),
                -size.y * 0.5f + coord.y * (size.y / counts.y),
                -size.z * 0.5f + coord.z * (size.z / counts.z));
        }

        /// <summary>
        /// 격자점 변위. 좌표 해시로 결정하므로 어느 면에서 계산해도 같은 값이다.
        /// 큐브 바깥 경계에 걸친 축이 많은 점(코너 &gt; 엣지 &gt; 면 내부)을 더 크게 움직인다 -
        /// "끝쪽 정점을 눌러" 손으로 다듬은 석재처럼 보이게 하는 부분.
        /// </summary>
        static Vector3 ResolveLatticeOffset(
            Vector3Int coord,
            Vector3Int counts,
            TrimSheetCubeOptions options,
            float jitter,
            float press)
        {
            if (jitter <= 0f && press <= 0f)
                return Vector3.zero;

            int extremeCount = 0;
            Vector3 inward = Vector3.zero;

            for (int axis = 0; axis < 3; axis++)
            {
                if (coord[axis] == 0)
                {
                    extremeCount++;
                    inward[axis] = 1f;

                    continue;
                }

                if (coord[axis] == counts[axis])
                {
                    extremeCount++;
                    inward[axis] = -1f;
                }
            }

            // 표면 격자점은 최소 한 축이 경계에 있다. 내부 점은 메시에 나오지 않는다.
            if (extremeCount == 0)
                return Vector3.zero;

            float weight = ResolveExtremeWeight(extremeCount);

            uint hash = HashCoord(coord, options.seed);

            Vector3 scatter = new Vector3(
                SignedUnit(hash, 0),
                SignedUnit(hash, 1),
                SignedUnit(hash, 2));

            float pressAmount = press * weight * Unit01(hash, 3);

            return scatter * (jitter * weight) + inward.normalized * pressAmount;
        }

        /// <summary>코너 = 3축, 엣지 = 2축, 면 내부 = 1축이 경계에 걸린다.</summary>
        static float ResolveExtremeWeight(int extremeCount)
        {
            if (extremeCount >= 3)
                return 1f;

            if (extremeCount == 2)
                return 0.6f;

            return 0.25f;
        }

        static Vector3 ResolveQuadNormal(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            // 쿼드를 이루는 두 삼각형의 법선을 합친다 (와인딩과 같은 순서)
            Vector3 first = Vector3.Cross(v1 - v0, v2 - v0);
            Vector3 second = Vector3.Cross(v2 - v0, v3 - v0);

            Vector3 combined = first + second;

            // 변위가 쿼드를 붕괴시킨 병리적 경우 - 상한이 있어 실제로는 오지 않는다
            if (combined.sqrMagnitude < 1e-12f)
                return Vector3.up;

            return combined.normalized;
        }

        static void AppendTileUvs(
            TrimSheetDefinition definition,
            TrimSheetCubeOptions options,
            System.Random rng,
            List<Vector2> uvs)
        {
            int cellIndex = options.cellIndex;

            // 가중치 추첨 - 크랙 셀 가중치를 낮춰 손상 타일을 드문드문 섞는다
            if (options.cellMode == TrimSheetCellMode.MixPerTile)
                cellIndex = definition.PickWeightedCell(rng);

            Rect cell = definition.GetCellUv(cellIndex);

            // 정점 순서(좌하 -> 우하 -> 우상 -> 좌상)에 맞춘 셀 코너
            Vector2[] corners =
            {
                new Vector2(cell.xMin, cell.yMin),
                new Vector2(cell.xMax, cell.yMin),
                new Vector2(cell.xMax, cell.yMax),
                new Vector2(cell.xMin, cell.yMax),
            };

            int rotation = 0;

            if (options.randomRotation)
                rotation = rng.Next(4);

            for (int i = 0; i < 4; i++)
                uvs.Add(corners[(i + rotation) % 4]);
        }

        // ---------------------------------------------------------------------
        // 격자 좌표 해시 (FNV-1a + 비트 믹싱)
        // ---------------------------------------------------------------------

        static uint HashCoord(Vector3Int coord, int seed)
        {
            uint hash = 2166136261u;

            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)coord.x) * 16777619u;
            hash = (hash ^ (uint)coord.y) * 16777619u;
            hash = (hash ^ (uint)coord.z) * 16777619u;

            return Mix(hash);
        }

        static uint Mix(uint hash)
        {
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;

            return hash;
        }

        /// <summary>같은 해시에서 채널별로 다른 0~1 값을 뽑는다.</summary>
        static float Unit01(uint hash, int channel)
        {
            uint mixed = Mix(hash ^ (uint)(channel * 0x9E3779B9u));

            return (mixed & 0xFFFFFFu) / (float)0xFFFFFF;
        }

        static float SignedUnit(uint hash, int channel)
        {
            return Unit01(hash, channel) * 2f - 1f;
        }
    }
}
