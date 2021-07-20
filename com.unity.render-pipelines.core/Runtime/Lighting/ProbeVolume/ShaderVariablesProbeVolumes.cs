using UnityEngine.Rendering;

namespace UnityEngine.Rendering
{
    /// <summary>
    /// Defines the constant buffer register that will be used as binding point for the Probe Volumes constant buffer.
    /// </summary>
    public enum APVConstantBufferRegister
    {
        GlobalRegister = 5
    }

    [GenerateHLSL(needAccessors = false, generateCBuffer = true, constantRegister = (int)APVConstantBufferRegister.GlobalRegister)]
    internal unsafe struct ShaderVariablesProbeVolumes
    {
        public Matrix4x4 _WStoRS;

        public Vector3 _IndexDim;
        public float _NormalBias;

        public Vector3 _PoolDim;
        public float _ViewBias;

        public float _PVSamplingNoise;
        public Vector3 _MinCellPosition;

        public Vector3 _CellIndicesDim;
        public float _CellInMeters;

        public float _CellInMinBricks;
        public float _MinBrickSize;
        public int _IndexChunkSize;
        public int pad0;
    }
}
