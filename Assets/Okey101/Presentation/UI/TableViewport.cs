using UnityEngine;
using UnityEngine.UI;
using System;

namespace Okey101.Presentation
{
    public sealed class TableViewport : MonoBehaviour
    {
        private MeldAreaView melds;
        private GameObject picker;
        private RectTransform content;
        private Func<bool> canInteract;
        public void Initialize(MeldAreaView value, Func<bool> allowed = null) { melds = value; canInteract = allowed ?? (() => true); }
        public void FocusGroup(int meldId) { if (!canInteract()) return; melds.FocusGroup(meldId); Close(); }
        public void ResetFocus() { if (canInteract()) melds.ResetFocus(); }
        public void Step(int direction) { if (canInteract()) melds.Step(direction); }
        public void Close() { if (picker != null) picker.SetActive(false); }
        public void Build(Transform canvas)
        {
            var shade = TableUI.Panel(canvas, "Public group browser", Vector2.zero, Vector2.one, new Color(0, 0, 0, .48f));
            picker = shade.gameObject;
            var panel = TableUI.Panel(shade, "Group browser card", new Vector2(.26f, .48f), new Vector2(.74f, .89f));
            TableUI.Label(panel, "Group browser title", new Vector2(.04f, .86f), new Vector2(.70f, .98f), 26).text = "MASADAKİ GRUPLAR";
            TableUI.Button(panel, "Kapat", new Vector2(.76f, .87f), new Vector2(.97f, .98f), () => picker.SetActive(false));
            var viewport = TableUI.Rect(panel, "Scroll viewport", new Vector2(.03f, .05f), new Vector2(.97f, .83f));
            viewport.gameObject.AddComponent<Image>().color = new Color(.04f, .08f, .07f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = TableUI.Rect(viewport, "All public groups", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            picker.SetActive(false);
        }
        public void OpenGroups()
        {
            if (picker == null || !canInteract()) return;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var group in melds.Groups)
            {
                var id = group.Id;
                var button = TableUI.Button(content, $"Grup {id + 1} · Oyuncu {group.Owner + 1} · {group.Group.Tiles.Count} taş",
                    Vector2.zero, Vector2.one, () => FocusGroup(id), 24);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
            }
            picker.SetActive(true); picker.transform.SetAsLastSibling(); content.anchoredPosition = Vector2.zero;
        }
    }
}
