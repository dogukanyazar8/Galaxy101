using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using UnityEngine;

namespace Okey101.Presentation
{
    public sealed class JokerBindingPicker : MonoBehaviour
    {
        private Action<TileId, TileFace> changed;
        private GameObject panel;
        private TileId selected;
        private TileColor color;
        private UnityEngine.UI.Text title;
        private UnityEngine.UI.Button firstJoker, secondJoker;
        private TileId[] editable = Array.Empty<TileId>();
        public bool IsOpen => panel != null && panel.activeSelf;
        public TileId CurrentJoker => selected;
        public void Initialize(Action<TileId, TileFace> onChanged) { changed = onChanged ?? throw new ArgumentNullException(nameof(onChanged)); }
        public void Build(Transform canvas)
        {
            var shade = TableUI.Panel(canvas, "Joker binding", Vector2.zero, Vector2.one, new Color(0, 0, 0, .72f)); panel = shade.gameObject;
            var card = TableUI.Panel(shade, "Choose face", new Vector2(.22f, .25f), new Vector2(.78f, .75f));
            title = TableUI.Label(card, "Okey karşılığı", new Vector2(.04f, .91f), new Vector2(.96f, .99f), 25);
            firstJoker = TableUI.Button(card, "1. okey", new Vector2(.08f, .77f), new Vector2(.46f, .89f), () => SelectJoker(editable[0]));
            secondJoker = TableUI.Button(card, "2. okey", new Vector2(.54f, .77f), new Vector2(.92f, .89f), () => SelectJoker(editable[1]));
            for (var c = 0; c < 4; c++)
            {
                var chosen = (TileColor)c;
                TableUI.Button(card, TableUI.ColorName(chosen), new Vector2(.04f + c * .235f, .61f), new Vector2(.255f + c * .235f, .75f), () =>
                { color = chosen; UpdateTitle(); });
            }
            for (var n = 1; n <= 13; n++)
            {
                var number = n; var x = (n - 1) % 7; var y = (n - 1) / 7;
                TableUI.Button(card, n.ToString(), new Vector2(.04f + x * .133f, .37f - y * .18f), new Vector2(.159f + x * .133f, .52f - y * .18f),
                    () => Choose(selected, new TileFace(color, number)), 28);
            }
            TableUI.Button(card, "Kapat", new Vector2(.70f, .04f), new Vector2(.96f, .15f), Close);
            panel.SetActive(false);
        }
        public void Show(TileId joker, IEnumerable<TileId> options = null)
        {
            editable = (options ?? new[] { joker }).Append(joker).Distinct().OrderBy(id => id.Value).ToArray();
            selected = joker; color = TileColor.Red;
            firstJoker.gameObject.SetActive(editable.Length > 1); secondJoker.gameObject.SetActive(editable.Length > 1);
            UpdateTitle(); panel.SetActive(true);
        }
        public void SelectJoker(TileId joker)
        {
            if (!editable.Contains(joker)) throw new ArgumentException("This joker is not editable.", nameof(joker));
            selected = joker; UpdateTitle();
        }
        private void UpdateTitle() { if (title != null) title.text = $"{Array.IndexOf(editable, selected) + 1}. okey · " + TableUI.ColorName(color); }
        public void Choose(TileId joker, TileFace face)
        {
            if (!face.IsValid) throw new ArgumentException("Invalid represented face.", nameof(face));
            if (changed == null) throw new InvalidOperationException("Initialize the picker first.");
            changed(joker, face); Close();
        }
        public void Close() { if (panel != null) panel.SetActive(false); }
    }
}
