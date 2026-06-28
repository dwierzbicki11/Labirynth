using System.Numerics;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct LightData
    {
        public Vector4 FlashlightPos;
        public Vector4 FlashlightDir;
        public Vector4 Lantern0;
        public Vector4 Lantern1;
        public Vector4 Lantern2;
        public Vector4 Lantern3;
        public Vector4 Lantern4;
        public Vector4 Lantern5;
        public Vector4 Lantern6;
        public Vector4 Lantern7;
        public int LanternCount;
    }