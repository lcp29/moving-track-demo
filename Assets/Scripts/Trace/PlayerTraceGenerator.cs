using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class PlayerTraceGenerator : MonoBehaviour
{
    [SerializeField] private ComputeShader deformingShader;

    [SerializeField] private Transform[] foots;

    [SerializeField] private Material deformableMaterial;

    private static readonly int playerPositionId = Shader.PropertyToID("_PlayerPosition");
    private static readonly int targetHeightmapId = Shader.PropertyToID("_TraceHeightmap");
    private static readonly int timerMapId = Shader.PropertyToID("_TimerMap");
    private static readonly int footPositionId = Shader.PropertyToID("_FootPosition");
    private static readonly int timeId = Shader.PropertyToID("_Time");
    private static readonly int deltaTimeId = Shader.PropertyToID("_DeltaTime");

    private int deformingKernelId = 0;
    private int clearRTKernelId = 0;
    private int refillKernelId = 0;

    private void Awake()
    {
        //Application.targetFrameRate = 60;
        if (TraceHeightmapManager.traceHeightmap == null)
        {
            // 1024 x 1024 16 m x 16 m
            TraceHeightmapManager.traceHeightmap =
                new RenderTexture(1024, 1024, 0, GraphicsFormat.R32G32B32A32_SFloat);
            TraceHeightmapManager.traceHeightmap.depthStencilFormat = GraphicsFormat.None;
            TraceHeightmapManager.traceHeightmap.enableRandomWrite = true;
            TraceHeightmapManager.traceHeightmap.Create();
        }

        if (TraceHeightmapManager.timerMap == null)
        {
            TraceHeightmapManager.timerMap =
                new RenderTexture(1024, 1024, 0, GraphicsFormat.R16_SFloat);
            TraceHeightmapManager.timerMap.depthStencilFormat = GraphicsFormat.None;
            TraceHeightmapManager.timerMap.enableRandomWrite = true;
            TraceHeightmapManager.timerMap.Create();
        }
        deformingKernelId = deformingShader.FindKernel("surfaceDeform");
        clearRTKernelId = deformingShader.FindKernel("clearRT");
        refillKernelId = deformingShader.FindKernel("refillSurface");
        deformableMaterial.SetTexture(targetHeightmapId, TraceHeightmapManager.traceHeightmap);
        deformingShader.SetTexture(clearRTKernelId, timerMapId, TraceHeightmapManager.timerMap);
        deformingShader.SetTexture(clearRTKernelId, targetHeightmapId, TraceHeightmapManager.traceHeightmap);
        deformingShader.SetFloat(timeId, Time.time);
        deformingShader.Dispatch(clearRTKernelId, 1024 / 8, 1024 / 8, 1);
    }

    void Update()
    {
        var playerPosition = transform.position;
        for (int i = 0; i < foots.Length; ++i)
        {
            deformingShader.SetFloat(timeId, Time.time);
            deformingShader.SetVector(playerPositionId, playerPosition);
            deformingShader.SetVector(footPositionId, foots[i].position);
            deformingShader.SetTexture(deformingKernelId, timerMapId, TraceHeightmapManager.timerMap);
            deformingShader.SetTexture(deformingKernelId, targetHeightmapId, TraceHeightmapManager.traceHeightmap);
            // 32 x 32 around the foot
            deformingShader.Dispatch(deformingKernelId, 32 / 8, 32 / 8, 1);
        }

        deformingShader.SetFloat(timeId, Time.time);
        deformingShader.SetFloat(deltaTimeId, Time.deltaTime);
        deformingShader.SetVector(playerPositionId, playerPosition);
        deformingShader.SetTexture(refillKernelId, timerMapId, TraceHeightmapManager.timerMap);
        deformingShader.SetTexture(refillKernelId, targetHeightmapId, TraceHeightmapManager.traceHeightmap);
        deformingShader.Dispatch(refillKernelId, 1024 / 8, 1024 / 8, 1);
        deformableMaterial.SetVector(playerPositionId, playerPosition);
    }
}
