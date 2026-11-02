using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Main-thread, reference-counted GLB templates. A cancelled consumer never cancels another consumer's import.</summary>
    internal static class ShipModelCache
    {
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        internal sealed class Lease : IDisposable
        {
            private Entry entry;
            internal Lease(Entry value) { entry = value; }
            public GameObject Instantiate(Transform parent)
            {
                if (entry == null) throw new ObjectDisposedException(nameof(Lease));
                return UnityEngine.Object.Instantiate(entry.Template, parent, false);
            }
            public void Dispose() { var old = entry; entry = null; if (old != null) Release(old); }
        }
        internal sealed class Entry
        {
            public string Key;
            public GltfImport Import;
            public GameObject Template;
            public Task Loading;
            public int References;
        }
        internal static async Task<Lease> Acquire(string key, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Entries.TryGetValue(key, out var entry))
            {
                entry = new Entry { Key = key, References = 1 };
                Entries.Add(key, entry); entry.Loading = Load(entry);
            }
            else entry.References++;
            try
            {
                await Wait(entry.Loading, token); token.ThrowIfCancellationRequested();
                return new Lease(entry);
            }
            catch { Release(entry); throw; }
        }
        private static async Task Load(Entry entry)
        {
            string path = "Galaxy101/Ships/" + entry.Key;
            try
            {
                var request = Resources.LoadAsync<TextAsset>(path);
                while (!request.isDone) await Task.Yield();
                var bytes = request.asset as TextAsset;
                if (bytes == null) throw new InvalidOperationException("Missing Resources/" + path + ".bytes; export the original ship GLB and copy its bytes without changing geometry.");
                // glTFast's default defer agent creates a persistent MonoBehaviour, which is invalid in EditMode.
                // Editor rendering has no frame budget to protect; runtime imports keep glTFast's normal scheduler.
                entry.Import = new GltfImport(deferAgent: Application.isPlaying ? null : new UninterruptedDeferAgent(), logger: new GLTFast.Logging.ConsoleLogger());
                bool loaded;
                try
                {
                    loaded = await entry.Import.Load(bytes.bytes, importSettings: new ImportSettings { GenerateMipMaps = true, AnisotropicFilterLevel = 8, NodeNameMethod = NameImportMethod.Original });
                }
                finally { Resources.UnloadAsset(bytes); }
                if (!loaded) throw new InvalidOperationException("glTFast rejected Resources/" + path + ".bytes.");
                entry.Template = new GameObject("Galaxy101 ship template " + entry.Key) { hideFlags = HideFlags.HideAndDontSave };
                entry.Template.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(entry.Template);
                if (!await entry.Import.InstantiateMainSceneAsync(entry.Template.transform)) throw new InvalidOperationException("glTFast could not instantiate " + path + ".");
                SetHidden(entry.Template.transform);
            }
            catch
            {
                Destroy(entry.Template); entry.Template = null; entry.Import?.Dispose(); entry.Import = null; throw;
            }
            finally
            {
                // A final consumer can disappear while glTFast is awaiting GPU/decode jobs.
                if (entry.References == 0) DisposeEntry(entry);
            }
        }
        private static async Task Wait(Task task, CancellationToken token)
        {
            if (!token.CanBeCanceled) { await task; return; }
            var cancelled = new TaskCompletionSource<bool>();
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task) throw new OperationCanceledException(token);
                await task;
            }
        }
        private static void Release(Entry entry)
        {
            if (--entry.References != 0) return;
            if (entry.Loading != null && entry.Loading.IsCompleted) DisposeEntry(entry);
            else if (entry.Loading != null) ObserveAbandoned(entry.Loading);
        }
        private static async void ObserveAbandoned(Task task)
        {
            // Observe a shared fault even when every waiting visual was cancelled. Load owns disposal.
            try { await task; } catch (Exception e) { Debug.LogError("Galaxy101 abandoned ship import failed: " + e.Message); }
        }
        private static void DisposeEntry(Entry entry)
        {
            if (Entries.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry)) Entries.Remove(entry.Key);
            Destroy(entry.Template); entry.Template = null; entry.Import?.Dispose(); entry.Import = null;
        }
        private static void SetHidden(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (int i = 0; i < root.childCount; i++) SetHidden(root.GetChild(i));
        }
        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
