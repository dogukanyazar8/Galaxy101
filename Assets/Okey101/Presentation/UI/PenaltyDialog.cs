using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class PenaltyDialog : MonoBehaviour
    {
        private TaskCompletionSource<bool> pending;
        private GameObject panel;
        private Text message;
        public bool IsOpen => pending != null;
        public void Initialize(Transform canvas)
        {
            var shade = TableUI.Panel(canvas, "Penalty confirmation", Vector2.zero, Vector2.one, new Color(0, 0, 0, .72f)); panel = shade.gameObject;
            var card = TableUI.Panel(shade, "Penalty card", new Vector2(.28f, .35f), new Vector2(.72f, .65f));
            message = TableUI.Label(card, "Ceza", new Vector2(.05f, .42f), new Vector2(.95f, .94f), 30);
            TableUI.Button(card, "Kabul et ve turu onayla", new Vector2(.05f, .10f), new Vector2(.66f, .33f), () => Resolve(true));
            TableUI.Button(card, "Vazgeç", new Vector2(.70f, .10f), new Vector2(.95f, .33f), () => Resolve(false));
            panel.SetActive(false);
        }
        public async Task<bool> ConfirmAsync(int penalty, CancellationToken token)
        {
            if (penalty <= 0) throw new ArgumentOutOfRangeException(nameof(penalty));
            if (pending != null) throw new InvalidOperationException("A penalty confirmation is already open.");
            token.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); pending = completion;
            if (panel != null) { message.text = $"Bu atış {penalty} ceza puanı getirir.\nTuru onaylamak istiyor musunuz?"; panel.SetActive(true); }
            try
            {
                using (token.Register(() => completion.TrySetCanceled())) return await completion.Task;
            }
            finally { pending = null; if (panel != null) panel.SetActive(false); }
        }
        public void Resolve(bool accepted) { pending?.TrySetResult(accepted); }
        private void OnDisable() { pending?.TrySetCanceled(); }
        private void OnDestroy() { pending?.TrySetCanceled(); }
    }
}
