using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>Scene constants from spec/data/table_scene.json and table3d.js (three space, metres, radians).</summary>
    public static class TableConstants
    {
        public const float TY = 0.76f, TW = 0.05f, TH = 0.07f, TD = 0.010f, SL = 0.053f, RZ = 0.8f, TILT = -0.36f;
        public const float HALF = 0.86f, FELT = 0.76f;
        public static readonly float[] SeatYaw = { 0, Mathf.PI / 2, Mathf.PI, -Mathf.PI / 2 };
        public const int SlotsPerRow = 15, Rows = 2;

        /// <summary>slotLocal(r, i): r = 1 bottom row, r = 0 top row; i = 0…14 (rack-local, tilted frame).</summary>
        public static Vector3 SlotLocal(int row, int index)
        {
            return new Vector3((index - 7) * SL, row == 1 ? 0.045f : 0.128f, row == 1 ? -0.003f + TD / 2 : -0.022f + TD / 2);
        }

        public static readonly Vector3 RackLocal = new Vector3(0, TY + 0.033f, RZ);
        public static readonly Vector3 ChairLocal = new Vector3(0, 0, 1.32f);
        public static readonly Vector3 DiscardLocal = new Vector3(0.6f, 0, 0.6f);
        public const float IndicatorTilt = -0.42f, IndicatorZ = 0.07f;

        public struct CameraView { public Vector3 Position, Target; public CameraView(Vector3 p, Vector3 t) { Position = p; Target = t; } }
        public static readonly CameraView PlayerView = new CameraView(new Vector3(0, 1.55f, 1.62f), new Vector3(0, 0.62f, -0.25f));
        public static readonly CameraView TopView = new CameraView(new Vector3(0, 2.60f, 1.42f), new Vector3(0, 0.58f, 0.16f));
        public static readonly CameraView LobbyView = new CameraView(new Vector3(0, 3.20f, 2.95f), new Vector3(0, 0.45f, -0.30f));
        public const float FieldOfView = 40;

        public static float EaseOutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);
        public static float EaseInOutCubic(float t) => t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;

        /// <summary>The prototype's string hash (deterministic jitter per tile id).</summary>
        public static float Hash(string s)
        {
            int h = 7;
            unchecked { foreach (char c in s) h = h * 31 + c; }
            return (uint)h % 1000 / 1000f;
        }
    }
}
