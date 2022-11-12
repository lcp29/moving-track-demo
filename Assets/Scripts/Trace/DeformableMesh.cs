using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
public class DeformableMesh : MonoBehaviour
{
    [SerializeField] private Transform player;

    private Mesh originalMesh;
    private Mesh tessellatedMesh;

    private static readonly Vector3 playerBoundSize = new Vector3(4, 100, 4); // should be (16,100,16) just for test

    struct MeshVertexAttributes
    {
        public Vector3 position;
        public Vector2 uv;
    }

    private NativeArray<MeshVertexAttributes> originalVertexAttributes;

    private NativeArray<Vector3> originalVertexPositions;
    private NativeArray<Vector2> originalVertexUVs;
    private NativeArray<int> originalIndices;

    public bool deformingEnabled;

    // job for calculating triangles that should be tessellated
    [BurstCompile(CompileSynchronously = true)]
    struct TessellationCullingJob : IJobFor
    {
        [ReadOnly] public Bounds playerBound;
        [ReadOnly] public NativeArray<MeshVertexAttributes> originalVertexAttributes;
        [ReadOnly] public Matrix4x4 fatherTransform;
        [ReadOnly] public NativeArray<int> originalIndices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> noTessellationIndices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> tessellationIndices;

        public int noTessArrayIndex;
        public int tessArrayIndex;

        public void Execute(int i)
        {
            int baseIndex = i * 3;
            for (int j = 0; j < 3; ++j)
            {
                int originalIndex = originalIndices[baseIndex + j];
                Vector3 originalVertex =
                    fatherTransform.MultiplyPoint(originalVertexAttributes[originalIndex].position);
                if (playerBound.Contains(originalVertex))
                {
                    int tessIndex = Interlocked.Increment(ref tessArrayIndex) * 3;
                    tessellationIndices[tessIndex] = originalIndices[baseIndex];
                    tessellationIndices[tessIndex + 1] = originalIndices[baseIndex + 1];
                    tessellationIndices[tessIndex + 2] = originalIndices[baseIndex + 2];
                    return;
                }
            }

            int noTessIndex = Interlocked.Increment(ref noTessArrayIndex) * 3;
            noTessellationIndices[noTessIndex] = originalIndices[baseIndex];
            noTessellationIndices[noTessIndex + 1] = originalIndices[baseIndex + 1];
            noTessellationIndices[noTessIndex + 2] = originalIndices[baseIndex + 2];
        }
    }

    private void Awake()
    {
        originalMesh = GetComponent<MeshFilter>().mesh;
        tessellatedMesh = new Mesh();
        tessellatedMesh.subMeshCount = 2;
        tessellatedMesh.Clear();
        deformingEnabled = false;
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
        Debug.Log(originalVertexAttributes.Length);
        originalVertexPositions.Dispose();
        originalVertexUVs.Dispose();
        tessellatedMesh.SetVertexBufferParams(originalVertexPositions.Length,
            new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
            });
        tessellatedMesh.SetVertexBufferData(originalVertexAttributes, 0, 0, originalVertexAttributes.Length);
        GetComponent<MeshFilter>().mesh = tessellatedMesh;
        deformingEnabled = true;
    }

    private void OnDisable()
    {
        if (originalVertexAttributes.IsCreated)
            originalVertexAttributes.Dispose();
        if (originalIndices.IsCreated)
            originalIndices.Dispose();
        GetComponent<MeshFilter>().mesh = originalMesh;
        deformingEnabled = false;
    }

    void Update()
    {
        if (Mathf.Max(Mathf.Abs(transform.position.x - player.position.x),
                Mathf.Abs(transform.position.z - player.position.z)) <= 16.0f)
        {
            if (!deformingEnabled)
            {
                OnEnable();
            }

            // check if the vertices are in 16m x 16m bound
            Bounds playerBound = new Bounds(player.position, playerBoundSize);
            NativeArray<int> noTessellationIndices = new NativeArray<int>(originalIndices.Length, Allocator.TempJob);
            NativeArray<int> tessellationIndices = new NativeArray<int>(originalIndices.Length, Allocator.TempJob);
            for (int i = 0; i < tessellationIndices.Length; ++i)
                tessellationIndices[i] = Int32.MaxValue;
            int noTessIndexArrayLength = 0;
            int tessIndexArrayLength = 0;
            JobHandle firstCullingHandle = default;
            firstCullingHandle = new TessellationCullingJob
            {
                playerBound = playerBound,
                fatherTransform = transform.localToWorldMatrix,
                originalIndices = originalIndices,
                originalVertexAttributes = originalVertexAttributes,
                noTessellationIndices = noTessellationIndices,
                tessellationIndices = tessellationIndices,
                noTessArrayIndex = -1,
                tessArrayIndex = -1
            }.Schedule(originalIndices.Length / 3, firstCullingHandle);
            firstCullingHandle.Complete();

            // getting vertex count
            for (;
                 tessIndexArrayLength < tessellationIndices.Length &&
                 tessellationIndices[tessIndexArrayLength] != Int32.MaxValue;
                 ++tessIndexArrayLength) ;
            noTessIndexArrayLength = originalIndices.Length - tessIndexArrayLength;

            // setting submeshes
            tessellatedMesh.SetIndexBufferParams(tessIndexArrayLength, IndexFormat.UInt32);
            tessellatedMesh.SetIndexBufferData(tessellationIndices, 0, 0, tessIndexArrayLength,
                MeshUpdateFlags.DontValidateIndices);
            tessellatedMesh.SetSubMesh(0, new SubMeshDescriptor(0, tessIndexArrayLength));
            // without this call ⬇️ it will cause culling failure and occasional surface disappearance
            tessellatedMesh.RecalculateBounds();
            tessellatedMesh.RecalculateNormals();
            tessellatedMesh.RecalculateTangents();
            noTessellationIndices.Dispose();
            tessellationIndices.Dispose();
        }
        else if (Mathf.Max(Mathf.Abs(transform.position.x - player.position.x),
                     Mathf.Abs(transform.position.z - player.position.z)) > 32.0f)
        {
            OnDisable();
        }
    }
}