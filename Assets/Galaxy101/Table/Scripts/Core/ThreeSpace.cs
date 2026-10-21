using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// The prototype (table3d.js) is authored in three.js space (right-handed, Y up). glTFast converts
    /// GLB content by mirroring X, so every value ported from the prototype goes through the same mirror:
    /// position (x, y, z) → (−x, y, z); rotation quaternion (x, y, z, w) → (x, −y, −z, w).
    /// Keeping layout math in three space lets the JS formulas be ported literally.
    /// </summary>
    public static class ThreeSpace
    {
        public static Vector3 ToUnity(Vector3 three) => new Vector3(-three.x, three.y, three.z);
        public static Vector3 ToUnity(float x, float y, float z) => new Vector3(-x, y, z);
        public static Quaternion ToUnity(Quaternion three) => new Quaternion(three.x, -three.y, -three.z, three.w);

        /// <summary>three.js Euler (default order XYZ, intrinsic) as a three-space quaternion.</summary>
        public static Quaternion Euler(float x, float y, float z)
        {
            return Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
        }

        public static Quaternion AxisAngle(Vector3 axis, float radians) => Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis);
    }

    /// <summary>A pose in three space: what the prototype's poseIn() returns.</summary>
    public readonly struct ThreePose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public ThreePose(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
        public Vector3 UnityPosition => ThreeSpace.ToUnity(Position);
        public Quaternion UnityRotation => ThreeSpace.ToUnity(Rotation);
        public void ApplyTo(Transform t) { t.SetPositionAndRotation(UnityPosition, UnityRotation); }
    }

    /// <summary>A three-space frame (the prototype's seatF / rackF Object3D) composed from a parent frame.</summary>
    public sealed class ThreeFrame
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public ThreeFrame(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
        public static readonly ThreeFrame World = new ThreeFrame(Vector3.zero, Quaternion.identity);
        public ThreeFrame Child(Vector3 localPosition, Quaternion localRotation) => new ThreeFrame(Position + Rotation * localPosition, Rotation * localRotation);
        /// <summary>poseIn(frame, x, y, z, ql)</summary>
        public ThreePose Pose(float x, float y, float z, Quaternion? local = null) => new ThreePose(Position + Rotation * new Vector3(x, y, z), local.HasValue ? Rotation * local.Value : Rotation);
    }
}
