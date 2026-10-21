using System;
using Okey101.Core.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class TurnToolbar : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.055f, .11f, .10f, .96f);
        private static readonly Color Cream = new Color(.95f, .93f, .85f);
        private static readonly Color Gold = new Color(.88f, .72f, .42f);
        private Font font;
        private RectTransform canvas;
        private Text heading, message;
        private readonly Text[] players = new Text[4];
        private Button draw, previousDraw, cancelDraw, discard, sort, follow, multi, botToggle;
        private Text localMode;
        private Text target;
        public ResultPanel Results {get;private set;}
        public PauseMenu Pause {get;private set;}
        public LeaveRoundDialog Leave {get;private set;}
        private GameBootstrap game;
        private readonly System.Collections.Generic.List<Button> editButtons = new System.Collections.Generic.List<Button>();
        private readonly System.Collections.Generic.List<Button> viewButtons = new System.Collections.Generic.List<Button>();
        public OpeningPanel Opening { get; private set; }
        public JokerBindingPicker Picker { get; private set; }
        public PenaltyDialog Penalty { get; private set; }

        public void Initialize(GameBootstrap game)
        {
            this.game = game;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Table interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<RectTransform>();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Input events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var header = Panel(canvas, "Header", new Vector2(0, .91f), Vector2.one, Ink);
            Label(header, "101  /  OKEY", new Vector2(.025f, .08f), new Vector2(.25f, .92f), 36, Gold, TextAnchor.MiddleLeft);
            heading = Label(header, "", new Vector2(.32f, .08f), new Vector2(.82f, .92f), 26, Cream);
            Button(header, "Duraklat", new Vector2(.86f, .19f), new Vector2(.975f, .81f), game.TogglePause);
            players[0] = PlayerPanel(new Vector2(.025f, .17f), new Vector2(.205f, .29f));
            players[1] = PlayerPanel(new Vector2(.81f, .51f), new Vector2(.975f, .64f));
            players[2] = PlayerPanel(new Vector2(.385f, .77f), new Vector2(.615f, .875f));
            players[3] = PlayerPanel(new Vector2(.025f, .51f), new Vector2(.19f, .64f));
            Label(canvas, "GÖSTERGE", new Vector2(.38f, .48f), new Vector2(.47f, .51f), 20, Gold);
            Label(canvas, "KAPALI DESTE", new Vector2(.47f, .48f), new Vector2(.56f, .51f), 20, Gold);
            Label(canvas, "SERİ / SAYI", new Vector2(.025f, .70f), new Vector2(.19f, .74f), 18, new Color(.58f, .68f, .58f));
            Label(canvas, "ÇİFT", new Vector2(.22f, .61f), new Vector2(.33f, .65f), 18, new Color(.58f, .68f, .58f));
            viewButtons.Add(TableUI.Button(canvas, "Gruplar / yakın bak", new Vector2(.81f, .69f), new Vector2(.975f, .74f), game.Presenter.Viewport.OpenGroups));
            viewButtons.Add(TableUI.Button(canvas, "Genel görünüm", new Vector2(.81f, .65f), new Vector2(.975f, .69f), game.Presenter.Viewport.ResetFocus));
            message = Label(canvas, "Taşa tıkla: seç. Ardından taş veya boş yuvaya tıkla: taşı.",
                new Vector2(.21f, .335f), new Vector2(.79f, .38f), 23, Cream);
            var footer = Panel(canvas, "Actions", Vector2.zero, new Vector2(1, .085f), Ink);
            sort = Button(footer, "Sırala", new Vector2(.17f, .16f), new Vector2(.25f, .84f), game.SortHand);
            draw = Button(footer, "Kapalıdan çek", new Vector2(.26f, .16f), new Vector2(.36f, .84f), game.RequestDraw);
            previousDraw = Button(footer, "Yandan al", new Vector2(.37f, .16f), new Vector2(.47f, .84f), game.RequestPreviousDraw);
            discard = Button(footer, "At ve turu onayla", new Vector2(.48f, .16f), new Vector2(.67f, .84f), () => game.RequestDiscard());
            cancelDraw = Button(footer, "İptal", new Vector2(.68f, .16f), new Vector2(.75f, .84f), game.CancelPendingDraw);
            if (Debug.isDebugBuild || UnityEngine.Application.isEditor)
            {
                localMode = Label(footer, "", new Vector2(.02f, .08f), new Vector2(.15f, .92f), 20, Gold);
                botToggle = Button(footer, "Botlar: açık", new Vector2(.76f, .16f), new Vector2(.86f, .84f), game.ToggleBots);
                follow = Button(footer, "Koltuğa geç", new Vector2(.87f, .16f), new Vector2(.975f, .84f), game.FollowActivePlayer);
            }
            var actions = Panel(canvas, "Draft actions", new Vector2(.215f, .39f), new Vector2(.785f, .46f), Ink);
            ActionButton(actions, "Çoklu seç", 0, 1, game.ToggleMultiSelect, true);
            ActionButton(actions, "Sıralı per", 1, 1, () => game.StageSelectedGroup(MeldKind.Run));
            ActionButton(actions, "Sayı grubu", 2, 1, () => game.StageSelectedGroup(MeldKind.Set));
            ActionButton(actions, "Çift", 3, 1, () => game.StageSelectedGroup(MeldKind.Pair));
            ActionButton(actions, "Okey karşılığı", 4, 1, game.RequestJokerPicker);
            ActionButton(actions, "Başa işle", 0, 0, () => game.StageSelectedExtension(true));
            ActionButton(actions, "Sona işle", 1, 0, () => game.StageSelectedExtension(false));
            ActionButton(actions, "Okey değiştir", 2, 0, game.StageSelectedSwap);
            ActionButton(actions, "Son işlemi al", 3, 0, game.UndoDraft);
            ActionButton(actions, "Taslağı sil", 4, 0, game.ResetDraft);
            var targets = Panel(canvas, "Table targets", new Vector2(.81f, .34f), new Vector2(.975f, .49f), Ink);
            target = Label(targets, "", new Vector2(.03f, .43f), new Vector2(.97f, .97f), 22, Cream);
            viewButtons.Add(TableUI.Button(targets, "Önceki", new Vector2(.04f, .06f), new Vector2(.47f, .35f), () => game.Presenter.Viewport.Step(-1)));
            viewButtons.Add(TableUI.Button(targets, "Sonraki", new Vector2(.53f, .06f), new Vector2(.96f, .35f), () => game.Presenter.Viewport.Step(1)));
            Opening = gameObject.AddComponent<OpeningPanel>(); Opening.Initialize(canvas);
            Results=game.CreateResultPanel(canvas);
            Picker = gameObject.AddComponent<JokerBindingPicker>(); Picker.Initialize(game.ChooseBinding); Picker.Build(canvas);
            Penalty = gameObject.AddComponent<PenaltyDialog>(); Penalty.Initialize(canvas);
            Pause=game.CreatePauseMenu(canvas);
            var leave=new GameObject("Leave round dialog",typeof(RectTransform),typeof(LeaveRoundDialog));
            Leave=leave.GetComponent<LeaveRoundDialog>();Leave.Initialize(canvas);
            game.Presenter.Viewport.Build(canvas);
        }
        private void ActionButton(Transform parent, string text, int column, int row, Action action, bool isMulti = false)
        {
            var button = TableUI.Button(parent, text, new Vector2(.004f + column * .20f, .04f + row * .50f),
                new Vector2(.196f + column * .20f, .46f + row * .50f), action, 20);
            editButtons.Add(button); if (isMulti) multi = button;
        }
        private Text PlayerPanel(Vector2 min, Vector2 max)
        { return Label(Panel(canvas, "Player", min, max, Ink), "", new Vector2(.05f, .08f), new Vector2(.95f, .92f), 24, Cream); }
        public void Render(PlayerView view, bool busy, bool paused, bool hasSelection)
        {
            heading.text = $"Eşsiz 101    ·    Deste {view.StockCount}    ·    Sıra {view.ActivePlayer + 1}";
            for (var p = 0; p < 4; p++)
            {
                var opening = view.Players[p].Opening == OpeningKind.None ? "Açmadı" : view.Players[p].Opening == OpeningKind.Pairs ? "Çift açtı" : "Seri açtı";
                players[p].text = $"{(p == view.Actor ? "SİZ" : game.BotsEnabled && p != 0 ? "BOT " + p : "OYUNCU " + (p + 1))}{(p == view.ActivePlayer ? "  • SIRA" : "")}\n{view.Players[p].HandCount} taş  ·  {opening}";
                players[p].color = p == view.ActivePlayer ? Gold : Cream;
            }
            var available = !busy && !paused && !Picker.IsOpen;
            foreach (var button in viewButtons) button.interactable = available;
            if (!available) game.Presenter.Viewport.Close();
            var ownTurn = available && view.Actor == view.ActivePlayer;
            sort.interactable = available;
            draw.interactable = ownTurn && view.Phase == RoundPhase.AwaitingDraw;
            previousDraw.interactable = draw.interactable && view.Discards[(view.Actor + 3) % 4].Count > 0;
            cancelDraw.interactable = game.IsSearchingDiscard && !paused;
            discard.interactable = ownTurn && view.Phase == RoundPhase.PlanningTurn && hasSelection;
            foreach (var button in editButtons) button.interactable = ownTurn && view.Phase == RoundPhase.PlanningTurn;
            multi.GetComponentInChildren<Text>().text = game.MultiSelect ? "Çoklu seç: açık" : "Çoklu seç: kapalı";
            if (follow != null)
            {
                follow.interactable = available && !game.BotsEnabled && view.Actor != view.ActivePlayer;
                botToggle.interactable = !paused;
                botToggle.GetComponentInChildren<Text>().text = game.BotsEnabled ? "Botlar: açık" : "Botlar: kapalı";
                localMode.text = game.BotsEnabled ? "YEREL OYUN\n3 bot" : "YEREL TEST\nElle kontrol";
            }
            if (view.Result != null)
            {
                Results.Show(view.Result);Results.SetActionsEnabled(available);SetMessage("El tamamlandı.");
            }
            else Results.Show(null);
            Pause.SetVisible(paused);
            if(Leave.IsOpen)Leave.transform.SetAsLastSibling();
        }
        private void LateUpdate() { if (target != null && game.Presenter != null) target.text = game.Presenter.Melds.TargetDescription; }
        public void SetMessage(string value) { message.text = value; }
        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; return rect;
        }
        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        { var rect = Rect(parent, name, min, max); rect.gameObject.AddComponent<Image>().color = color; return rect; }
        private Text Label(Transform parent, string value, Vector2 min, Vector2 max, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rect = Rect(parent, value.Length > 0 ? value : "Label", min, max); var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = anchor;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private Button Button(Transform parent, string text, Vector2 min, Vector2 max, Action action)
        {
            var rect = Panel(parent, text, min, max, new Color(.16f, .25f, .22f)); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => action());
            var colors = button.colors; colors.highlightedColor = new Color(.90f, .78f, .51f); colors.disabledColor = new Color(.4f, .4f, .4f, .65f); button.colors = colors;
            Label(rect, text, Vector2.zero, Vector2.one, 25, Cream); return button;
        }
    }
}
