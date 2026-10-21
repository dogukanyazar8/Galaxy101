using System;
using Okey101.Application;
using Okey101.Core.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class OpeningPanel : MonoBehaviour
    {
        private Text summary;
        public bool CanConfirm { get; private set; }
        public void Initialize(Transform canvas)
        {
            summary = TableUI.Label(canvas, "Opening summary", new Vector2(.215f, .765f), new Vector2(.38f, .89f), 23);
        }
        public void Show(TurnPreview preview)
        {
            CanConfirm = preview != null && preview.Valid;
            if (summary == null || preview == null) return;
            var bindingText = ""; var index = 0;
            foreach (var choice in preview.BindingChoices) bindingText += $"\nOkey {++index}: {TableUI.Face(choice.Value)}";
            summary.text = $"Açılış: {preview.OpeningTotal}/101\nÇift: {preview.PairCount}/5 · İşlenen: {preview.SeriesTilesUsed}" + bindingText;
        }
    }
    internal static class TableUI
    {
        internal static readonly Color Cream = new Color(.95f, .93f, .85f);
        internal static readonly Color Ink = new Color(.055f, .11f, .10f, .98f);
        internal static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; return rect;
        }
        internal static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color? color = null)
        { var rect = Rect(parent, name, min, max); rect.gameObject.AddComponent<Image>().color = color ?? Ink; return rect; }
        internal static Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size = 23)
        {
            var rect = Rect(parent, name, min, max); var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = size;
            label.color = Cream; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        internal static Button Button(Transform parent, string text, Vector2 min, Vector2 max, Action action, int size = 22)
        {
            var rect = Panel(parent, text, min, max, new Color(.16f, .25f, .22f)); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); if(action!=null) button.onClick.AddListener(() => action());
            Label(rect, text, Vector2.zero, Vector2.one, size).text = text; return button;
        }
        internal static string Face(TileFace face) => ColorName(face.Color) + " " + face.Number;
        internal static string ColorName(TileColor color) => color == TileColor.Red ? "Kırmızı" : color == TileColor.Yellow ? "Sarı" : color == TileColor.Blue ? "Mavi" : "Siyah";
        internal static string Error(ErrorCode error)
        {
            switch (error)
            {
                case ErrorCode.None: return "Hazır";
                case ErrorCode.LastTileRequired: return "Atılacak taşı seçin.";
                case ErrorCode.InvalidOpening: return "Açılış için 101 sayı veya 5 çift gerekli.";
                case ErrorCode.InvalidGroup: return "Grup geçersiz; taşları ve okey karşılıklarını kontrol edin.";
                case ErrorCode.InvalidTile: return "Taş bu taslakta elde değil veya tekrar kullanıldı.";
                case ErrorCode.ProcessingLimit: return "Çift açan oyuncu turda en fazla 2 taş işleyebilir.";
                case ErrorCode.OpeningRequired: return "Önce kendi açılışınızı hazırlayın.";
                case ErrorCode.OpeningTypeLocked: return "Açılış türü değiştirilemez.";
                case ErrorCode.InvalidReplacement: return "Değişim taşı okeyin sabit karşılığıyla eşleşmeli.";
                case ErrorCode.JokerBindingLocked: return "Masadaki okeyin karşılığı sabit.";
                case ErrorCode.DiscardMustBeUsed: return "Yandan alınan taş bu tur masada kullanılmalı.";
                case ErrorCode.NoLegalUse: return "Yandan alınan taş için tamamlanabilir tur yok.";
                case ErrorCode.PreviousDiscardUnavailable: return "Önceki oyuncunun alınabilir atığı yok.";
                case ErrorCode.WrongPlayer: return "Sıra başka oyuncuda.";
                case ErrorCode.WrongPhase: return "Önce taş çekin veya aktif oyuncuya geçin.";
                case ErrorCode.Cancelled: return "İşlem iptal edildi.";
                case ErrorCode.PenaltyConfirmationRequired: return "Ceza onayı gerekiyor.";
                default: return "Taslak doğrulanamadı; kalıcı masa değişmedi.";
            }
        }
    }
}
