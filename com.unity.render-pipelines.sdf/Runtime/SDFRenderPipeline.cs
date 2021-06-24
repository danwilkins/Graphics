using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.SDFRP
{
    public static class Utilities
    {
        static Mesh s_FullscreenMesh = null;
        /// <summary>
        /// Returns a mesh that you can use with <see cref="CommandBuffer.DrawMesh(Mesh, Matrix4x4, Material)"/> to render full-screen effects.
        /// </summary>
        public static Mesh fullscreenMesh
        {
            get
            {
                if (s_FullscreenMesh != null)
                    return s_FullscreenMesh;

                float topV = 1.0f;
                float bottomV = 0.0f;

                s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                s_FullscreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

                s_FullscreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

                s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                s_FullscreenMesh.UploadMeshData(true);
                return s_FullscreenMesh;
            }
        }
    }

    [ExecuteInEditMode]
    public class SDFRenderPipeline : RenderPipeline
    {
        internal static SDFRenderPipelineAsset currentAsset
                => GraphicsSettings.currentRenderPipeline is SDFRenderPipelineAsset sdfAsset ? sdfAsset : null;

    //    internal static HDRenderPipeline currentPipeline
    //            => RenderPipelineManager.currentPipeline is HDRenderPipeline hdrp ? hdrp : null;

        Material m_DepthOfFieldMaterial = null;

        static int Frame = 0;

        private SDFSceneData m_SdfSceneData;
        private SDFRayMarch m_SdfRayMarch;

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            ClearBackground(context, cameras);
            foreach (Camera camera in cameras)
            {
                SDFCameraData cameraData = new SDFCameraData();
                cameraData.InitializeCameraData(camera);

                CommandBuffer cmd = new CommandBuffer();
                cmd.name = "Camera Setup";
                cmd.SetViewport(camera.pixelRect);
                cmd.SetRenderTarget(camera.targetTexture);
                cmd.SetViewMatrix(camera.worldToCameraMatrix);
                cmd.SetProjectionMatrix(camera.projectionMatrix);
                cameraData.UpdateGlobalShaderVariables(cmd);
                context.ExecuteCommandBuffer(cmd);
                cmd.Release();

                SDFRenderer[] SDFObjects = GameObject.FindObjectsOfType<SDFRenderer>();
                if (SDFObjects.Length > 0)
                {
                    GetDataFromSceneGraph(SDFObjects, camera.pixelRect);
                    CreateObjectList(context, camera, SDFObjects.Length);

                    // SDF Rendering
                    {
                        if (camera.cameraType == CameraType.Game && camera.enabled)
                        {
                            CommandBuffer cmdRayMarch = new CommandBuffer();
                            cmdRayMarch.name = "RayMarch";
                            cameraData.UpdateComputeShaderVariables(cmdRayMarch, currentAsset.rayMarchingCS);

                            if (m_SdfRayMarch == null) // TODO: or if resolution has changed
                            {
                                m_SdfRayMarch = new SDFRayMarch(camera.pixelRect);
                            }
                            m_SdfRayMarch.RayMarch(cmdRayMarch, currentAsset.rayMarchingCS, m_SdfSceneData);

                            context.ExecuteCommandBuffer(cmdRayMarch);
                            cmdRayMarch.Release();
                        }
                    }
                }

                if (currentAsset.EnableDepthOfField)
                {
                    if (m_DepthOfFieldMaterial == null)
                    {
                        m_DepthOfFieldMaterial = new Material(Shader.Find("Hidden/SDFRP/DepthOfField"));
                    }
                    if (camera.cameraType == CameraType.Game && camera.enabled)
                    {

                        CommandBuffer cmdDOF = new CommandBuffer();
                        cmdDOF.name = "DepthOfField";

                        cmdDOF.SetGlobalColor("BackgroundColor", currentAsset.clearColor);
                        cmdDOF.SetGlobalInt("lensRes", currentAsset.lensRes);
                        cmdDOF.SetGlobalFloat("lensDis", camera.nearClipPlane);
                        cmdDOF.SetGlobalFloat("focalDis", currentAsset.focalDis);
                        cmdDOF.SetGlobalFloat("lensSiz", currentAsset.lensSiz);
                        cmdDOF.DrawMesh(Utilities.fullscreenMesh, Matrix4x4.identity, m_DepthOfFieldMaterial);
                        context.ExecuteCommandBuffer(cmdDOF);
                        cmdDOF.Release();
                    }
                }
            }
            context.Submit();
            Frame++;
        }

        private void ClearBackground(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (Camera camera in cameras)
            {
                CommandBuffer cmd1 = new CommandBuffer();
                if (camera.cameraType == CameraType.Preview)
                {
                    Debug.LogError(camera.pixelRect);
                }
                cmd1.SetViewport(camera.pixelRect);
                cmd1.SetRenderTarget(camera.targetTexture);
                cmd1.ClearRenderTarget(false, true, currentAsset.clearColor);
                context.ExecuteCommandBuffer(cmd1);
                cmd1.Release();
            }
        }

        private void GetDataFromSceneGraph(SDFRenderer[] SDFObjects, Rect pixelRect)
        {
            if (m_SdfSceneData == null) // TODO: assuming fixed numebr of objects in scene for now
            {
                int sdfDataSize = 0;
                foreach (SDFRenderer renderer in SDFObjects)
                {
                    sdfDataSize += renderer.SDFFilter.VoxelField.m_Field.Length;
                }

                m_SdfSceneData = new SDFSceneData(SDFObjects.Length, sdfDataSize, pixelRect);
            }

            // Fill out array of data and array of data-headers
            int offset = 0;
            for(int i = 0; i < SDFObjects.Length; i++)
            {
                VoxelField field = SDFObjects[i].SDFFilter.VoxelField;

                m_SdfSceneData.objectHeaders[i].worldToObjMatrix = SDFObjects[i].gameObject.transform.worldToLocalMatrix; // may not work with shader according to docs?
                m_SdfSceneData.objectHeaders[i].objID = i; // index into data. Change later?
                m_SdfSceneData.objectHeaders[i].numEntries = field.m_Field.Length;
                m_SdfSceneData.objectHeaders[i].startOffset = offset;
                m_SdfSceneData.objectHeaders[i].voxelSize = field.m_VoxelSize;
                Vector3 minExtent = field.MeshBounds.center - 0.5f * field.MeshBounds.size; // is this correct? Can we just pass the counts instead?
                m_SdfSceneData.objectHeaders[i].minExtentX = minExtent.x;
                m_SdfSceneData.objectHeaders[i].minExtentY = minExtent.y;
                m_SdfSceneData.objectHeaders[i].minExtentZ = minExtent.z;
                Vector3 maxExtent = field.MeshBounds.center + 0.5f * field.MeshBounds.size;
                m_SdfSceneData.objectHeaders[i].maxExtentX = maxExtent.x;
                m_SdfSceneData.objectHeaders[i].maxExtentY = maxExtent.y;
                m_SdfSceneData.objectHeaders[i].maxExtentZ = maxExtent.z;
                //m_SdfSceneData.objectHeaders[i].color = SDFObjects[i].SDFMaterial.color;

                Array.Copy(field.m_Field, 0, m_SdfSceneData.sdfData, offset, field.m_Field.Length);
                offset += field.m_Field.Length;
            }

            // Update compute buffers
            m_SdfSceneData.SetObjectHeaderData();
            m_SdfSceneData.SetSDFData();
        }

        private void CreateObjectList(ScriptableRenderContext context, Camera camera, int totalSDFs)
        {
            // TO DO: Don't always clear if no change?
            Array.Clear(m_SdfSceneData.tileDataOffsetIntoObjHeaderValues, 0, m_SdfSceneData.tileDataOffsetIntoObjHeaderValues.Length);
            Array.Clear(m_SdfSceneData.tileHeaders, 0, m_SdfSceneData.tileHeaders.Length);

            Vector3[] cubeVertices =
            {
                new Vector3(-0.5f,-0.5f,-0.5f),
                new Vector3(-0.5f,-0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f,-0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f,-0.5f,-0.5f),
                new Vector3(0.5f,-0.5f, 0.5f),
                new Vector3(0.5f, 0.5f,-0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
            };
            int[] cubeIndices =
            {
                0, 1, 3,
                6, 0, 2,
                5, 0, 4,
                6, 4, 0,
                0, 3, 2,
                5, 1, 0,
                3, 1, 5,
                7, 4, 6,
                4, 7, 5,
                7, 6, 2,
                7, 2, 3,
                7, 3, 5
            };

            var mesh = new Mesh();
            mesh.SetVertices(cubeVertices, 0, 8);
            mesh.SetIndices(cubeIndices, MeshTopology.Triangles, 0);

            CommandBuffer cmd1 = new CommandBuffer();
            Shader shader = currentAsset.tileCullingShader;
            Material material = new Material(shader);
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            // material.SetColor("_Color", Color.red);

            material.SetBuffer("_TileData", m_SdfSceneData.tileOffsetsComputeBuffer);
            cmd1.SetRandomWriteTarget(1, m_SdfSceneData.tileOffsetsComputeBuffer);

            if (camera.cameraType == CameraType.Preview)
            {
                Debug.LogError(camera.pixelRect);
            }
            cmd1.SetViewport(camera.pixelRect);

            SDFSceneData.ObjectHeader[] data = m_SdfSceneData.objectHeaders;
            for (int i = 0; i < data.Length; i++)
            {
                Vector3 minExtent = new Vector3(data[i].minExtentX, data[i].minExtentY, data[i].minExtentZ);
                Vector3 maxExtent = new Vector3(data[i].maxExtentX, data[i].maxExtentY, data[i].maxExtentZ);
                var extents = maxExtent - minExtent;
                Matrix4x4 scale = Matrix4x4.Scale(extents);
                Matrix4x4 finalTRS = data[i].worldToObjMatrix.inverse * scale;       // Check multiply scale correctness later
                propertyBlock.SetInt("_SdfID", i);
                cmd1.DrawMesh(mesh, finalTRS, material, 0, 0, propertyBlock);
            }

            // edit Tile data and headers
            int[] tileFlags = new int[SDFRayMarch.MAX_OBJECTS_IN_SCENE * m_SdfSceneData.numTiles];
            m_SdfSceneData.tileOffsetsComputeBuffer.GetData(tileFlags);

            int curOffset = 0;
            for (int tile = 0; tile < m_SdfSceneData.numTiles; tile++)
            {
                m_SdfSceneData.tileHeaders[tile].offset = curOffset;
                int offset = SDFRayMarch.MAX_OBJECTS_IN_SCENE * tile;
                for (int id = 0; id < SDFRayMarch.MAX_OBJECTS_IN_SCENE; id++)
                {
                    if (tileFlags[offset + id] == 1)
                    {
                        int newOffset = m_SdfSceneData.tileHeaders[tile].offset + m_SdfSceneData.tileHeaders[tile].numObjects;
                        m_SdfSceneData.tileDataOffsetIntoObjHeaderValues[newOffset] = id;
                        m_SdfSceneData.tileHeaders[tile].numObjects += 1;
                        // Debug.Log("Tile " + tile + " now has " + m_SdfSceneData.tileHeaders[tile].numObjects + " objects, newest is id " + id);
                        curOffset++;
                    }
                }
            }
            m_SdfSceneData.SetTileHeaderData();
            m_SdfSceneData.SetTileOffsetIntoObjHeaderData();


            context.ExecuteCommandBuffer(cmd1);
            cmd1.Release();
        }
    }
}
