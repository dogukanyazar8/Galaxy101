using Okey101.Core.Model;
using UnityEngine;

namespace Okey101.Presentation
{
    public sealed class TableAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip draw, place, discard;
        private AudioSource source;
        private readonly int[] lastFrame = { -1, -1, -1 };
        public float Volume { get; set; } = .38f;
        public void Configure(AudioClip drawClip, AudioClip placeClip, AudioClip discardClip)
        { draw = drawClip; place = placeClip; discard = discardClip; }
        public void Play(VisibleEvent visibleEvent)
        {
            var kind = visibleEvent.Kind == VisibleEventKind.Draw ? 0 : visibleEvent.Kind == VisibleEventKind.Discard ? 2
                : visibleEvent.Kind == VisibleEventKind.GroupsOpened || visibleEvent.Kind == VisibleEventKind.GroupAdded
                || visibleEvent.Kind == VisibleEventKind.GroupExtended || visibleEvent.Kind == VisibleEventKind.JokerSwapped ? 1 : -1;
            if (kind < 0 || lastFrame[kind] == Time.frameCount) return;
            var clip = kind == 0 ? draw : kind == 1 ? place : discard;
            if (clip == null) return;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
                source.spatialBlend = 0; source.priority = 160;
            }
            lastFrame[kind] = Time.frameCount; source.PlayOneShot(clip, Mathf.Clamp01(Volume));
        }
        public void SetPaused(bool paused) { if (source != null) { if (paused) source.Pause(); else source.UnPause(); } }
        private void OnDisable() { if (source != null) source.Stop(); }
    }
}
