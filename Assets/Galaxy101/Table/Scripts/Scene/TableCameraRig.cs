using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>Camera views, eased transitions and idle breathing (spec/09 §3).</summary>
    public sealed class TableCameraRig : MonoBehaviour
    {
        public enum View { Player, Top, Lobby, Deal, Mid }

        private Camera cam;
        private Vector3 fromPos, fromTarget, toPos, toTarget;
        private float started, duration, shakeUntil;

        /// <summary>Rocket landing: the camera jitters for <paramref name="seconds"/>, fading out (±(left·0.03) m).</summary>
        public void Shake(float seconds) => shakeUntil = Time.time + seconds;

        public Camera Camera => cam;
        public View Current { get; private set; } = View.Player;

        public void Initialize(Camera camera, View initial)
        {
            cam = camera;
            cam.fieldOfView = TableConstants.FieldOfView;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
            var v = ViewOf(initial); fromPos = toPos = v.Position; fromTarget = toTarget = v.Target; duration = 0; Current = initial;
            Apply(Time.time);
        }

        /// <summary>Move to a view with easeInOutCubic (1.3 s for a view change, 3.0 s countdown, 0.8 s deal start).</summary>
        public void GoTo(View view, float seconds)
        {
            Evaluate(Time.time, out fromPos, out fromTarget);
            var v = ViewOf(view); toPos = v.Position; toTarget = v.Target; started = Time.time; duration = seconds; Current = view;
        }

        /// <summary>celebrate(): lean towards an opponent's corner (16 % of the way, 4 cm lower, look half way), then GoTo back.</summary>
        public void Lean(Vector3 threeCorner, float seconds)
        {
            Evaluate(Time.time, out fromPos, out fromTarget);
            toPos = Vector3.Lerp(toPos, threeCorner, 0.16f) + new Vector3(0, -0.04f, 0); toTarget = Vector3.Lerp(toTarget, threeCorner, 0.5f);
            started = Time.time; duration = seconds;
        }

        private static TableConstants.CameraView ViewOf(View v) => v == View.Top ? TableConstants.TopView : v == View.Mid ? TableConstants.MidView : v == View.Lobby ? TableConstants.LobbyView : v == View.Deal ? TableConstants.DealView : TableConstants.PlayerView;

        private void Evaluate(float now, out Vector3 pos, out Vector3 target)
        {
            float u = duration <= 0 ? 1 : TableConstants.EaseInOutCubic(Mathf.Clamp01((now - started) / duration));
            pos = Vector3.Lerp(fromPos, toPos, u); target = Vector3.Lerp(fromTarget, toTarget, u);
        }

        private void LateUpdate() { if (cam != null) Apply(Time.time); }

        private void Apply(float now)
        {
            Evaluate(now, out var pos, out var target);
            // Breathing: x += sin(t·0.21)·0.006, y += sin(t·0.33)·0.004 (three space).
            pos.x += Mathf.Sin(now * 0.21f) * 0.006f; pos.y += Mathf.Sin(now * 0.33f) * 0.004f;
            if (now < shakeUntil) { float k = (shakeUntil - now) * 0.03f; pos.x += (Random.value - 0.5f) * k; pos.y += (Random.value - 0.5f) * k; }
            var p = ThreeSpace.ToUnity(pos); var t = ThreeSpace.ToUnity(target);
            cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(t - p, Vector3.up));
            FitAspect();
        }

        /// <summary>Vertical FOV is 40° at 16:9; wider screens keep the 16:9 horizontal FOV so the table is never cropped.</summary>
        private void FitAspect()
        {
            float aspect = cam.aspect, reference = 16f / 9f;
            if (aspect >= reference) { cam.fieldOfView = TableConstants.FieldOfView; return; }
            float h = 2 * Mathf.Atan(Mathf.Tan(TableConstants.FieldOfView * Mathf.Deg2Rad / 2) * reference);
            cam.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(h / 2) / aspect) * Mathf.Rad2Deg;
        }
    }
}
