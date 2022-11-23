using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
public class DeformableMesh : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private ComputeShader deformationShader;

    private Mesh originalMesh;
    private Mesh tessellatedMesh;

    private static readonly Vector3 playerBoundSize = new Vector3(16, 100, 16); // should be (16,100,16) just for test

    private static readonly int cullResultId = Shader.PropertyToID("_CullResult");
    private static readonly int targetHeightmapId = Shader.PropertyToID("_TraceHeightmap");
    private static readonly int tessTriangleCountId = Shader.PropertyToID("_TessTriangleCount");
    private static readonly int vertexBufferStrideId = Shader.PropertyToID("_VertexBufferStride");
    private static readonly int originalVertexBufferId = Shader.PropertyToID("_OriginalVertexBuffer");
    private static readonly int playerPositionId = Shader.PropertyToID("_PlayerPosition");
    private static readonly int originalIndexBufferId = Shader.PropertyToID("_OriginalIndexBuffer");
    private static readonly int cullBoundRadiusId = Shader.PropertyToID("_CullBoundRadius");
    private static readonly int fatherTransformId = Shader.PropertyToID("_FatherTransform");

    private int tessTriangleCullingKernelId = 0;

    struct MeshVertexAttributes
    {
        public Vector3 position;
        public Vector2 uv;
    }

    private NativeArray<MeshVertexAttributes> originalVertexAttributes;

    private NativeArray<Vector3> originalVertexPositions;
    private NativeArray<Vector2> originalVertexUVs;
    private NativeArray<int> originalIndices;

    private NativeArray<int> cullingResult;

    private ComputeBuffer triangleShouldBeTessellated;
    private GraphicsBuffer originalVertexBuffer;
    private GraphicsBuffer originalIndexBuffer;

    private AsyncGPUReadbackRequest readCullingDataRequest;
    private int frameCount;

    public bool deformingEnabled;

    [BurstCompile(CompileSynchronously = true)]
    struct CullSortingJob : IJobFor
    {
        [ReadOnly] public NativeArray<int> cullingResult;
        [ReadOnly] public NativeArray<int> originalIndices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> tessIndices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> noTessIndices;

        // should be initialized with -1
        public int tessIndexIdx;
        public int noTessIndexIdx;

        public void Execute(int i)
        {
            // if should be tessellated
            if (cullingResult[i] == 1)
            {
                int idx = Interlocked.Increment(ref tessIndexIdx) * 3;
                tessIndices[idx] = originalIndices[i * 3];
                tessIndices[idx + 1] = originalIndices[i * 3 + 1];
                tessIndices[idx + 2] = originalIndices[i * 3 + 2];
            }
            else
            {
                int idx = Interlocked.Increment(ref noTessIndexIdx) * 3;
                noTessIndices[idx] = originalIndices[i * 3];
                noTessIndices[idx + 1] = originalIndices[i * 3 + 1];
                noTessIndices[idx + 2] = originalIndices[i * 3 + 2];
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AssembleVertexAttributesJob : IJobFor
    {
        [ReadOnly] public NativeArray<Vector3> originalVerticesPosition;
        [ReadOnly] public NativeArray<Vector2> originalVerticesUV;
        [WriteOnly] public NativeArray<MeshVertexAttributes> originalVerticesAttributes;

        public void Execute(int i)
        {
            originalVerticesAttributes[i] = new MeshVertexAttributes
            {
                position = originalVerticesPosition[i],
                uv = originalVerticesUV[i]
            };
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct ClearIndicesJob : IJobFor
    {
        [WriteOnly] public NativeArray<int> indices;

        public void Execute(int i)
        {
            indices[i] = Int32.MaxValue;
        }
    }

    private void Awake()
    {
        tessTriangleCullingKernelId = deformationShader.FindKernel("tessTriangleCulling");
        originalMesh = GetComponent<MeshFilter>().mesh;
        tessellatedMesh = new Mesh();
        tessellatedMesh.subMeshCount = 2;
        tessellatedMesh.Clear();
        deformingEnabled = false;
        frameCount = 0;
    }

    private void OnEnable()
    {
        originalVertexPositions =
            new NativeArray<Vector3>(originalMesh.vertices, Allocator.TempJob);
        originalVertexUVs =
            new NativeArray<Vector2>(originalMesh.uv, Allocator.TempJob);
        originalVertexAttributes =
            new NativeArray<MeshVertexAttributes>(originalVertexPositions.Length, Allocator.Persistent);
        originalIndices = new NativeArray<int>(originalMesh.triangles, Allocator.Persistent);
        JobHandle assembleVertexAttributesHandle = default;
        assembleVertexAttributesHandle = new AssembleVertexAttributesJob
        {
            originalVerticesPosition = originalVertexPositions,
            originalVerticesUV = originalVertexUVs,
            originalVerticesAttributes = originalVertexAttributes
        }.Schedule(originalVertexAttributes.Length, assembleVertexAttributesHandle);
        assembleVertexAttributesHandle.Complete();
        originalVertexPositions.Dispose();
        originalVertexUVs.Dispose();
        tessellatedMesh.SetVertexBufferParams(originalVertexPositions.Length,
            new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
            });
        tessellatedMesh.SetVertexBufferData(originalVertexAttributes, 0, 0, originalVertexAttributes.Length);
        //GetComponent<MeshFilter>().mesh = tessellatedMesh;
        deformingEnabled = true;

        // if the corresponding triangle should be tessellated
        triangleShouldBeTessellated = new ComputeBuffer(originalIndices.Length / 3, 4);

        originalMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        originalMesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

        originalVertexBuffer = originalMesh.GetVertexBuffer(0);
        originalIndexBuffer = originalMesh.GetIndexBuffer();

        // get culling result into native array for further sorting
        cullingResult = new NativeArray<int>(triangleShouldBeTessellated.count, Allocator.Persistent);
    }

    private void OnDisable()
    {
        if (originalVertexAttributes.IsCreated)
            originalVertexAttributes.Dispose();
        if (originalIndices.IsCreated)
            originalIndices.Dispose();
        if (cullingResult.IsCreated)
            cullingResult.Dispose();
        triangleShouldBeTessellated?.Dispose();
        originalVertexBuffer?.Release();
        originalIndexBuffer?.Release();
        GetComponent<MeshFilter>().mesh = originalMesh;
        deformingEnabled = false;
    }

    void Update()
    {
        // if within the range
        if (Mathf.Max(Mathf.Abs(transform.position.x - player.position.x),
                Mathf.Abs(transform.position.z - player.position.z)) <= 16.0f &&
            TraceHeightmapManager.traceHeightmap != null)
        {
            // enable deforming
            if (!deformingEnabled)
            {
                OnEnable();
            }

            /*if (frameCount > 0)
            {
                readCullingDataRequest.WaitForCompletion();

                // get triangle count
                int tessTriangleCount = 0, noTessTriangleCount = 0;
                for (int i = 0; i < cullingResult.Length; ++i)
                {
                    if (cullingResult[i] == 0)
                        ++noTessTriangleCount;
                    else
                        ++tessTriangleCount;
                }

                // indices of the triangles which will be tessellated or not
                NativeArray<int> noTessellationIndices =
                    new NativeArray<int>(noTessTriangleCount * 3, Allocator.TempJob);
                NativeArray<int> tessellationIndices =
                    new NativeArray<int>(tessTriangleCount * 3, Allocator.TempJob);

                // sort indices into tessellation group and noTessellation group
                JobHandle cullSortingJobHandle = default;
                cullSortingJobHandle = new CullSortingJob
                {
                    noTessIndexIdx = -1,
                    tessIndexIdx = -1,
                    cullingResult = cullingResult,
                    originalIndices = originalIndices,
                    tessIndices = tessellationIndices,
                    noTessIndices = noTessellationIndices
                }.Schedule(cullingResult.Length, cullSortingJobHandle);
                cullSortingJobHandle.Complete();

                // set mesh data
                tessellatedMesh.SetIndexBufferParams(noTessellationIndices.Length, IndexFormat.UInt32);
                tessellatedMesh.SetIndexBufferData(noTessellationIndices, 0, 0, noTessellationIndices.Length,
                    MeshUpdateFlags.DontValidateIndices);
                tessellatedMesh.SetSubMesh(0,
                    new SubMeshDescriptor(0, noTessellationIndices.Length, MeshTopology.Triangles));
                tessellatedMesh.RecalculateBounds();
                tessellatedMesh.RecalculateNormals();
                tessellatedMesh.RecalculateTangents();

                noTessellationIndices.Dispose();
                tessellationIndices.Dispose();
                cullingResult.Dispose();
                cullingResult = new NativeArray<int>(triangleShouldBeTessellated.count, Allocator.Persistent);
            }*/

            // check if the vertices are in 16m x 16m bound
            Bounds playerBound = new Bounds(player.position, playerBoundSize);

            // setting up culling shader data

            deformationShader.SetInt(tessTriangleCountId, triangleShouldBeTessellated.count);
            deformationShader.SetInt(vertexBufferStrideId, originalVertexBuffer.stride);
            deformationShader.SetFloat(cullBoundRadiusId, playerBound.size.x);
            deformationShader.SetVector(playerPositionId, player.position);
            deformationShader.SetMatrix(fatherTransformId, transform.localToWorldMatrix);

            deformationShader.SetTexture(tessTriangleCullingKernelId, targetHeightmapId,
                TraceHeightmapManager.traceHeightmap);
            deformationShader.SetBuffer(tessTriangleCullingKernelId, cullResultId, triangleShouldBeTessellated);

            deformationShader.SetBuffer(tessTriangleCullingKernelId, originalVertexBufferId, originalVertexBuffer);
            deformationShader.SetBuffer(tessTriangleCullingKernelId, originalIndexBufferId, originalIndexBuffer);
            deformationShader.Dispatch(tessTriangleCullingKernelId, triangleShouldBeTessellated.count, 1, 1);

            //readCullingDataRequest =
                AsyncGPUReadback.RequestIntoNativeArray(ref cullingResult, triangleShouldBeTessellated)
                    .WaitForCompletion();

            // ========
            // get triangle count
            int tessTriangleCount = 0, noTessTriangleCount = 0;
            for (int i = 0; i < cullingResult.Length; ++i)
            {
                if (cullingResult[i] == 0)
                    ++noTessTriangleCount;
                else
                    ++tessTriangleCount;
            }

            // indices of the triangles which will be tessellated or not
            NativeArray<int> noTessellationIndices =
                new NativeArray<int>(noTessTriangleCount * 3, Allocator.TempJob);
            NativeArray<int> tessellationIndices =
                new NativeArray<int>(tessTriangleCount * 3, Allocator.TempJob);

            // sort indices into tessellation group and noTessellation group
            JobHandle cullSortingJobHandle = default;
            cullSortingJobHandle = new CullSortingJob
            {
                noTessIndexIdx = -1,
                tessIndexIdx = -1,
                cullingResult = cullingResult,
                originalIndices = originalIndices,
                tessIndices = tessellationIndices,
                noTessIndices = noTessellationIndices
            }.Schedule(cullingResult.Length, cullSortingJobHandle);
            cullSortingJobHandle.Complete();

            // set mesh data
            tessellatedMesh.SetIndexBufferParams(noTessellationIndices.Length, IndexFormat.UInt32);
            tessellatedMesh.SetIndexBufferData(noTessellationIndices, 0, 0, noTessellationIndices.Length,
                MeshUpdateFlags.DontValidateIndices);
            tessellatedMesh.SetSubMesh(0,
                new SubMeshDescriptor(0, noTessellationIndices.Length, MeshTopology.Triangles));
            tessellatedMesh.RecalculateBounds();
            tessellatedMesh.RecalculateNormals();
            tessellatedMesh.RecalculateTangents();

            noTessellationIndices.Dispose();
            tessellationIndices.Dispose();
            cullingResult.Dispose();
            cullingResult = new NativeArray<int>(triangleShouldBeTessellated.count, Allocator.Persistent);
        }
        else if (Mathf.Max(Mathf.Abs(transform.position.x - player.position.x),
                     Mathf.Abs(transform.position.z - player.position.z)) > 32.0f)
        {
            OnDisable();
        }

        ++frameCount;
    }
}