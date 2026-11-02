using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101
{
    internal sealed class ShipVisual : IDisposable
    {
        private readonly GameObject root;
        private readonly Transform[] heads = new Transform[4], bodies = new Transform[4];
        private readonly Quaternion[] bodyRotation = new Quaternion[4];
        private readonly Transform rotatingRing;
        private readonly Quaternion ringRotation;
        private readonly Light lamp;
        private readonly float phaseOffset;
        public ShipVisual(GameObject model, int index, Material glass)
        {
            root = model; root.name = "Galaxy101 ship instance " + index;
            var all = root.GetComponentsInChildren<Transform>(true);
            Transform authoredRoot = null;
            foreach (var t in all) { t.gameObject.layer = 30; t.gameObject.hideFlags = HideFlags.HideAndDontSave; if (t.name.StartsWith("gemi_", StringComparison.Ordinal)) authoredRoot = t; }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes; renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                // The lounge domes are the GLBs' only blended material. glTFast's clearcoat graph draws that blend
                // nearly opaque, hiding the okey table and players; the native glass keeps the source's opacity .2.
                var materials = renderer.sharedMaterials; bool swapped = false;
                for (int k = 0; k < materials.Length; k++)
                    if (glass != null && materials[k] != null && materials[k].renderQueue >= (int)RenderQueue.Transparent && materials[k].shader.name.StartsWith("Shader Graphs/glTF-", StringComparison.Ordinal)) { materials[k] = glass; swapped = true; }
                if (swapped) renderer.sharedMaterials = materials;
            }
            if (authoredRoot == null) { Debug.LogError("Galaxy101 ship " + index + " has no original gemi_ root; dynamic mesh bindings are unavailable."); return; }
            if (index >= 3) return;
            var loungePosition = index == 0 ? new Vector3(.25f, .55f, 0) : index == 1 ? new Vector3(-.1f, .32f, 0) : new Vector3(-1.5f, -.18f, 0);
            var lounge = ChildAt(authoredRoot, loungePosition);
            if (lounge == null) { Debug.LogError("Galaxy101 ship " + index + " is missing its original lounge transform."); return; }
            int count = 0;
            for (int i = 0; i < lounge.childCount && count < 4; i++)
            {
                var seat = lounge.GetChild(i); var head = ChildAt(seat, new Vector3(0, .3f, .33f)); var body = ChildAt(seat, new Vector3(0, .19f, .33f));
                if (head == null || body == null) continue;
                heads[count] = head; bodies[count] = body; bodyRotation[count] = body.localRotation; count++;
            }
            if (count != 4) Debug.LogError("Galaxy101 ship " + index + " lounge expected four original head/body pairs, found " + count + ".");
            var light = new GameObject("Galaxy101 ship lounge light") { hideFlags = HideFlags.HideAndDontSave, layer = 30 }; light.transform.SetParent(lounge, false); light.transform.localPosition = new Vector3(0, .55f, 0);
            lamp = light.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color32(255, 221, 168, 255); lamp.range = 1.6f; lamp.intensity = 2.4f / Mathf.PI; lamp.shadows = LightShadows.None; lamp.cullingMask = 1 << 30;
            if (index == 2) { rotatingRing = ChildAt(authoredRoot, new Vector3(.15f, 0, 0)); if (rotatingRing != null) ringRotation = rotatingRing.localRotation; }
            phaseOffset = index * .37f;
        }
        private static Transform ChildAt(Transform parent, Vector3 position)
        {
            for (int i = 0; i < parent.childCount; i++) { var t = parent.GetChild(i); if ((t.localPosition - position).sqrMagnitude < .000001f) return t; } return null;
        }
        public void Apply(ShipPose pose, float time, float phase)
        {
            root.SetActive(pose.Visible); if (!pose.Visible) return;
            var forward = System.Numerics.Vector3.Normalize(pose.Velocity);
            var side = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(forward, pose.Up));
            var rotation = Quaternion.LookRotation(GalaxyShips.Convert(side), GalaxyShips.Convert(pose.Up));
            root.transform.SetLocalPositionAndRotation(GalaxyShips.Convert(pose.Position), rotation * Quaternion.AngleAxis(pose.Roll * Mathf.Rad2Deg, Vector3.right));
            if (rotatingRing != null) rotatingRing.localRotation = ringRotation * Quaternion.AngleAxis(time * .35f * Mathf.Rad2Deg, Vector3.right);
            for (int i = 0; i < 4; i++)
            {
                if (heads[i] == null) continue;
                float p = i * 1.7f + phaseOffset;
                heads[i].localPosition = new Vector3(0, .3f + Mathf.Max(0, Mathf.Sin(time * 2 + p)) * .01f, .33f);
                heads[i].localRotation = Quaternion.AngleAxis(-Mathf.Sin(time * .7f + p) * .55f * Mathf.Rad2Deg, Vector3.up);
                bodies[i].localRotation = bodyRotation[i] * Quaternion.AngleAxis(Mathf.Sin(time * 1.3f + p) * .05f * Mathf.Rad2Deg, Vector3.right);
            }
            if (lamp != null) lamp.intensity = (2.2f + Mathf.Sin(time * 2.2f + phase) * .2f) / Mathf.PI; // three.js candela: see GalaxyShips key light
        }
        public void Dispose() => GalaxyShips.DestroyOwned(root);
    }
}
