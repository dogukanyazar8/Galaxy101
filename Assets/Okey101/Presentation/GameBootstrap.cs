using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Application;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Okey101.Presentation
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool loadTableScene;
        [SerializeField] private PauseMenu pausePrefab;
        [SerializeField] private ResultPanel resultPrefab;
        private readonly TurnDraft draft = new TurnDraft();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HashSet<TileId> selection = new HashSet<TileId>();
        private readonly Dictionary<TileId, TileFace> choices = new Dictionary<TileId, TileFace>();
        private CancellationTokenSource previewWork, commandWork;
        private CancellationTokenSource botWork;
        private BotRunner botRunner;
        private Task botTask;
        private Task activeCommand;
        private Task<bool> leaveTask;
        private bool flowBusy, focusHandling, backgroundAcceptance;
        private string botFailure;
        private TurnToolbar toolbar;
        private PlayerView view;
        private long editVersion;
        private bool busy, confirming, destroyed, previewPending, multiSelect;
        public LocalSession Session { get; private set; }
        public TablePresenter Presenter { get; private set; }
        public TurnPreview Preview { get; private set; }
        public TurnDraft Draft => draft;
        public bool BotsEnabled { get; private set; }
        public bool BotThinking { get; private set; }
        public LocalSettings Settings {get;private set;}=new LocalSettings();
        public void ConfigureUI(PauseMenu pause,ResultPanel result){pausePrefab=pause;resultPrefab=result;}
        internal PauseMenu CreatePauseMenu(Transform canvas)
        {
            var menu=pausePrefab!=null?Instantiate(pausePrefab,canvas):new GameObject("Pause menu",typeof(RectTransform),typeof(PauseMenu)).GetComponent<PauseMenu>();
            menu.transform.SetParent(canvas,false);menu.Initialize(this);return menu;
        }
        internal ResultPanel CreateResultPanel(Transform canvas)
        {
            var panel=resultPrefab!=null?Instantiate(resultPrefab,canvas):new GameObject("Result panel",typeof(RectTransform),typeof(ResultPanel)).GetComponent<ResultPanel>();
            panel.transform.SetParent(canvas,false);panel.Initialize(this);return panel;
        }
        public void ApplySettings(LocalSettings settings)
        {Settings=settings??throw new ArgumentNullException(nameof(settings));MenuUI.Apply(settings);Presenter.Motion.ReducedMotion=settings.ReducedMotion;}
        public void HandleFocus(bool focused){if(!focused&&!backgroundAcceptance&&!destroyed&&Session!=null)SetPaused(true);}
        private void OnApplicationFocus(bool focused){if(focusHandling)HandleFocus(focused);}
        private void OnApplicationPause(bool paused){if(focusHandling&&paused)HandleFocus(false);}
        public Task<bool> RequestLeaveAsync()
        {
            if(destroyed||Session==null||(toolbar?.Pause?.SettingsOpen??false))return Task.FromResult(false);
            if(leaveTask!=null&&!leaveTask.IsCompleted)return leaveTask;
            leaveTask=LeaveAsync();return leaveTask;
        }
        private async Task<bool> LeaveAsync()
        {
            SetPaused(true);
            if(Session.GetView(0).Result==null&&!await toolbar.Leave.ConfirmAsync(lifetime.Token))return false;
            Shutdown();await DrainWorkAsync();
            var load=SceneManager.LoadSceneAsync("Bootstrap",LoadSceneMode.Single);while(!load.isDone)await Task.Yield();
            return true;
        }
        public async Task StartNextRoundAsync()
        {
            if(destroyed||flowBusy||Session?.GetView(0).Result==null)return;
            flowBusy=true;botWork?.Cancel();previewWork?.Cancel();UpdateToolbar();
            try
            {
                await DrainWorkAsync();if(destroyed)return;
                Session.StartNextRound(MainMenu.NewSeed());BotsEnabled=true;botFailure=null;multiSelect=false;Refresh(0);
            }
            finally{flowBusy=false;UpdateToolbar();}
        }
        private async Task DrainWorkAsync()
        {
            foreach(var work in new[]{botTask,activeCommand})
                if(work!=null)
                    try{await work;}catch(OperationCanceledException){}
        }
        public void SetBotsEnabled(bool enabled)
        {
            if (destroyed || Session == null || BotsEnabled == enabled) return;
            BotsEnabled = enabled;
            if (enabled) botFailure = null;
            if (!enabled) botWork?.Cancel();
            else if (view.Actor != 0) Refresh(0);
            UpdateToolbar();
        }
        public void ToggleBots() { SetBotsEnabled(!BotsEnabled); }
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
        public void EnableBackgroundAcceptance()
        {if(!UnityEngine.Application.isBatchMode)throw new InvalidOperationException("Background acceptance requires batch mode.");backgroundAcceptance=true;}
        public void InstallDiagnosticRound(RoundState state)
        {
            if (!CanControl || BotThinking) throw new InvalidOperationException("Wait for the current command before loading a diagnostic round.");
            SetBotsEnabled(false); previewWork?.Cancel(); Session.Start(state); botFailure = null; Refresh(0);
        }
        public async Task StepDiagnosticBotAsync()
        {
            if (!CanControl || BotThinking || Session.GetView(0).Result != null) return;
            SetBotsEnabled(false); busy = true; UpdateToolbar();
            Session.GetView(0, out var epoch);
            using (var work = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, epoch))
            {
                commandWork = work;
                try
                {
                    var before = Session.GetView(0);
                    await botRunner.RunOneTurnAsync(before.ActivePlayer, work.Token);
                    if (!destroyed && Session.GetView(0).Version != before.Version)
                        await PresentAcceptedAsync(before, Session.GetLastAcceptedReceipt(0).Events);
                }
                finally { commandWork = null; busy = false; if (!destroyed) Refresh(0); }
            }
        }
        public async Task<RuleResult<bool>> InstallDiagnosticReplayAsync(ReplayRecord record, CancellationToken token)
        {
            if (!CanControl || BotThinking) return RuleResult<bool>.Fail(ErrorCode.WrongPhase);
            SetBotsEnabled(false); previewWork?.Cancel(); busy = true; UpdateToolbar();
            using (var work = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token))
            {
                commandWork = work;
                try { return await Session.RestoreReplayAsync(record, work.Token); }
                finally { commandWork = null; busy = false; if (!destroyed) Refresh(0); }
            }
        }
#endif
        public bool MultiSelect => multiSelect;
        public bool IsSearchingDiscard { get; private set; }
        public bool CanControl => !destroyed && !flowBusy && !busy && !confirming && !BotThinking && Session != null && !Session.IsPaused && !(toolbar?.Leave?.IsOpen ?? false) && !(Presenter?.Motion?.IsPlaying ?? false) && !(toolbar?.Picker?.IsOpen ?? false);
        public bool CanCommit => CanControl && !previewPending && Preview != null && Preview.Valid && selection.Count == 1;
        public IReadOnlyList<TileId> SelectedTiles => Array.AsReadOnly(selection.OrderBy(id => id.Value).ToArray());
        public void ConfigureLoader(bool enabled) { loadTableScene = enabled; }
        private void Start()
        {
            if (Session != null) return;
            if (loadTableScene) { gameObject.AddComponent<MainMenu>(); return; }
            focusHandling=true;
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-okeyPractice");
            if ((Debug.isDebugBuild || UnityEngine.Application.isEditor) && index >= 0 && index + 1 < args.Length)
                Initialize(PracticeDeals.Create(args[index + 1]), true);
            else
#endif
                InitializeNewGame(MainMenu.NewSeed(), true);
            ApplySettings(MenuUI.LoadSettings());
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            if(BackgroundAcceptance.Requested)BackgroundAcceptance.Configure(this);
            var soakArgs=Environment.GetCommandLineArgs();var soakIndex=Array.IndexOf(soakArgs,"-okeyA4Output");
            if(soakIndex>=0&&soakIndex+1<soakArgs.Length)
            {
                var smoke=Array.IndexOf(soakArgs,"-okeyA4Smoke")>=0;
                gameObject.AddComponent<WindowsSoakProbe>().Initialize(this,soakArgs[soakIndex+1],smoke?1:30,smoke?2:600,smoke?5:1800);
            }
#endif
        }
        public void Initialize(uint seed, int starter) => Initialize(seed, starter, false);
        public void InitializeNewGame(uint seed,bool enableBots) => Initialize(seed,(int)(seed%4),enableBots);
        public void Initialize(uint seed, int starter, bool enableBots) => InitializeSession(session => session.Start(seed, starter), enableBots);
        public void Initialize(RoundState initial) => Initialize(initial, false);
        public void Initialize(RoundState initial, bool enableBots) => InitializeSession(session => session.Start(initial), enableBots);
        private void InitializeSession(Action<LocalSession> start, bool enableBots)
        {
            if (Session != null) throw new InvalidOperationException("The table is already initialized.");
            Session = new LocalSession(); start(Session);
            botRunner = new BotRunner(Session); BotsEnabled = enableBots;
            Presenter = GetComponent<TablePresenter>() ?? gameObject.AddComponent<TablePresenter>();
            Presenter.Initialize();
            toolbar = gameObject.AddComponent<TurnToolbar>(); toolbar.Initialize(this);
            gameObject.AddComponent<TableInput>().Initialize(this);
            UnityEngine.Application.targetFrameRate = 60; Refresh(0);
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            GetComponent<DebugPanel>()?.Initialize(this);
            var acceptanceArgs = Environment.GetCommandLineArgs();
            var acceptanceIndex = Array.IndexOf(acceptanceArgs, "-okeyA3Output");
            if (acceptanceIndex >= 0 && acceptanceIndex + 1 < acceptanceArgs.Length)
                gameObject.AddComponent<TableAcceptanceProbe>().Initialize(this, acceptanceArgs[acceptanceIndex + 1]);
#endif
            Debug.Log($"A2_MACHINE resolution={Screen.width}x{Screen.height} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} ramMB={SystemInfo.systemMemorySize}");
        }
        private void Refresh(int actor)
        {
            view = Session.GetView(actor); draft.Reset(view); Preview = null;
            selection.Clear(); choices.Clear(); Presenter.Render(view); UpdateToolbar(); RequestPreview();
            Debug.Log($"A2_RENDER actor={actor} version={view.Version} hand={view.OwnHand.Count} visible={Presenter.VisibleOwnTileIds.Count} stock={view.StockCount}");
            if (view.Result != null) Debug.Log($"A2_RESULT reason={view.Result.FinishInfo.Reason} winner={view.Result.FinishInfo.Winner} scores={string.Join(",", view.Result.Scores.Select(score => score.Total?.ToString() ?? "cancelled"))}");
        }
        private void Update()
        {
            if (!BotsEnabled || destroyed || flowBusy || Session == null || Session.IsPaused || (toolbar?.Leave?.IsOpen??false) || busy || confirming || (botTask != null && !botTask.IsCompleted)) return;
            if (view == null || view.ActivePlayer == 0 || view.Result != null) return;
            var current = Session.GetView(0);
            if (current.Result == null && current.ActivePlayer != 0) botTask = PlayBotsAsync();
        }
        private async Task PlayBotsAsync()
        {
            Session.GetView(0, out var epoch);
            using (var work = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, epoch))
            {
                botWork = work; BotThinking = true; UpdateToolbar();
                try
                {
                    while (BotsEnabled && !work.IsCancellationRequested && !Session.IsPaused)
                    {
                        var before = Session.GetView(0);
                        if (before.Result != null || before.ActivePlayer == 0) break;
                        await botRunner.RunOneTurnAsync(before.ActivePlayer, work.Token);
                        if (destroyed || work.IsCancellationRequested) return;
                        var after = Session.GetView(0);
                        if (after.RoundId != before.RoundId) return;
                        if (after.Version == before.Version) throw new InvalidOperationException("Bot turn completed without a state transition.");
                        await PresentAcceptedAsync(before, Session.GetLastAcceptedReceipt(0).Events);
                        Refresh(0);
                        Debug.Log($"A2_BOT actor={before.ActivePlayer} fromVersion={before.Version} toVersion={after.Version} opening={after.Players[before.ActivePlayer].Opening} hand={after.Players[before.ActivePlayer].HandCount}");
                    }
                }
                catch (OperationCanceledException) when (work.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (work.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    if (!destroyed) { BotsEnabled = false; botFailure = "Bot turu tamamlanamadı; otomatik oyun durduruldu."; Debug.LogException(exception); toolbar.SetMessage(botFailure); }
                }
                finally
                {
                    botWork = null; BotThinking = false;
                    if (!destroyed) Refresh(0);
                }
            }
        }
        private IReadOnlyList<TileId> VisibleHand => Preview != null && Preview.ActionsValid ? Preview.ProjectedOwnHand
            : Preview == null ? view.OwnHand : view.OwnHand.Where(id => !Preview.ReservedTiles.Contains(id)).ToArray();
        private void UpdateToolbar() { if (!destroyed && view != null) toolbar.Render(view, flowBusy || busy || confirming || BotThinking, Session.IsPaused, CanCommit); }
        private void UpdateSelection()
        {
            if (selection.Count == 1) draft.SelectDiscard(selection.First()); else draft.ClearDiscard();
            Presenter.SelectMany(selection); RequestPreview();
        }
        public void SelectTile(TileId id)
        {
            if (!CanControl || !VisibleHand.Contains(id)) return;
            var clear = selection.Count == 1 && selection.Contains(id); selection.Clear();
            if (!clear) selection.Add(id); UpdateSelection();
        }
        public void ToggleGroupTile(TileId id)
        {
            if (!CanControl || !VisibleHand.Contains(id)) return;
            if (!selection.Remove(id)) selection.Add(id); UpdateSelection();
        }
        public void ToggleMultiSelect() { if (CanControl) { multiSelect = !multiSelect; UpdateToolbar(); } }
        public void ClickTile(TileId id)
        {
            if (multiSelect) { ToggleGroupTile(id); return; }
            if (!CanControl) return;
            if (selection.Count == 1 && !selection.Contains(id)) MoveSelectedTo(Presenter.Rack.IndexOf(id)); else SelectTile(id);
        }
        public void MoveSelectedTo(int slot) { if (CanControl && !multiSelect && selection.Count == 1) Presenter.Move(selection.First(), slot); }
        public void SortHand() { if (CanControl) Presenter.Sort(); }
        public void SetPerspective(int actor)
        { if (CanControl && !BotsEnabled && (Debug.isDebugBuild || UnityEngine.Application.isEditor)) Refresh(actor); }
        public void FollowActivePlayer() { if (Session != null) SetPerspective(view.ActivePlayer); }
        public void TogglePause(){if(Session!=null)SetPaused(!Session.IsPaused);}
        public void SetPaused(bool paused)
        {
            if (destroyed || Session == null || Session.IsPaused==paused) return;
            if(!paused&&((toolbar?.Leave?.IsOpen??false)||(toolbar?.Pause?.SettingsOpen??false)))return;
            commandWork?.Cancel(); previewWork?.Cancel(); botWork?.Cancel(); toolbar.Penalty.Resolve(false); toolbar.Picker.Close();
            Session.SetPaused(paused); UpdateToolbar();
            Presenter.Motion.SetPaused(Session.IsPaused); Presenter.Audio.SetPaused(Session.IsPaused);
            if (!Session.IsPaused && !Presenter.Motion.IsPlaying) RequestPreview();
        }
        public void CancelPendingDraw() { if (IsSearchingDiscard) commandWork?.Cancel(); }
        public void ResetDraft()
        {
            if (!CanControl) return;
            draft.Reset(view); selection.Clear(); choices.Clear(); Presenter.Render(view); RequestPreview();
        }
        public void UndoDraft()
        { if (CanControl) { draft.UndoLast(); selection.Clear(); draft.ClearDiscard(); RequestPreview(); } }
        public void ChooseBinding(TileId id, TileFace face)
        {
            draft.ChooseBinding(id, face); choices[id] = face; RequestPreview();
        }
        public void RequestJokerPicker()
        {
            if (!CanControl) return;
            var indicator = TileCatalog.Create()[view.Indicator.Value].PrintedFace.Value;
            var staged = Preview?.BindingChoices.Keys.ToArray() ?? Array.Empty<TileId>();
            var target = Presenter.Melds.SelectedTile;
            var joker = selection.Where(id => TileCatalog.IsRealJoker(id, indicator)).Cast<TileId?>().FirstOrDefault()
                ?? (target.HasValue && staged.Contains(target.Value) ? target : staged.Cast<TileId?>().FirstOrDefault());
            if (joker.HasValue) toolbar.Picker.Show(joker.Value, staged.Concat(new[] { joker.Value }));
            else toolbar.SetMessage("Bir gerçek okey seçin veya taslağa okeyli grup ekleyin.");
        }
        private PlacedTile[] SelectedPlacements()
        {
            var indicator = TileCatalog.Create()[view.Indicator.Value].PrintedFace.Value;
            var result = new List<PlacedTile>();
            foreach (var id in selection.OrderBy(id => id.Value))
            {
                if (TileCatalog.IsRealJoker(id, indicator))
                {
                    if (!choices.TryGetValue(id, out var face))
                    { toolbar.SetMessage("Önce okeyin temsil edeceği taşı seçin."); toolbar.Picker.Show(id); return null; }
                    result.Add(new PlacedTile(id, face));
                }
                else result.Add(new PlacedTile(id, TileCatalog.GetNonJokerFace(id, indicator)));
            }
            return result.ToArray();
        }
        public void StageSelectedGroup(MeldKind kind)
        {
            if (!CanControl) return;
            var tiles = SelectedPlacements(); if (tiles == null) return;
            var group = new MeldProposal(kind, tiles);
            var valid = MeldValidator.Validate(group, TileCatalog.Create()[view.Indicator.Value].PrintedFace.Value);
            if (!valid.Success) { toolbar.SetMessage(TableUI.Error(valid.Error)); return; }
            draft.StageGroup(group); ClearAfterStage();
        }
        public void StageSelectedExtension(bool prepend)
        {
            if (!CanControl || !Presenter.Melds.SelectedMeldId.HasValue) { toolbar.SetMessage("Masadan bir hedef grup seçin."); return; }
            var tiles = SelectedPlacements(); if (tiles == null || tiles.Length == 0) return;
            draft.StageAction(new ExtendMeld(Presenter.Melds.SelectedMeldId.Value, prepend, tiles)); ClearAfterStage();
        }
        public void SelectMeldTile(int meldId, TileId id)
        { if (CanControl) { Presenter.Melds.Select(meldId, id); toolbar.SetMessage("Hedef grup seçildi. İşlenecek taşı ıstakadan seçin."); } }
        public void StageSelectedSwap()
        {
            if (!CanControl || selection.Count != 1 || !Presenter.Melds.SelectedMeldId.HasValue || !Presenter.Melds.SelectedTile.HasValue)
            { toolbar.SetMessage("Istakadan bir normal taş ve masadan değişecek okeyi seçin."); return; }
            draft.StageAction(new SwapJoker(Presenter.Melds.SelectedMeldId.Value, Presenter.Melds.SelectedTile.Value, selection.First())); ClearAfterStage();
        }
        private void ClearAfterStage() { selection.Clear(); draft.ClearDiscard(); RequestPreview(); }
        private async void RequestPreview()
        {
            try { await RecomputePreviewAsync(); }
            catch (Exception exception) { if (!destroyed) { Debug.LogException(exception); toolbar.SetMessage("Önizleme hesaplanamadı."); } }
        }
        public async Task<TurnPreview> RecomputePreviewAsync()
        {
            if (Presenter.Motion.IsPlaying) return Preview;
            previewWork?.Cancel();
            var work = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); previewWork = work;
            var revision = ++editVersion; previewPending = true; UpdateToolbar();
            try
            {
                var preview = await draft.PreviewAsync(work.Token);
                if (!destroyed && revision == editVersion && !work.IsCancellationRequested)
                {
                    Preview = preview;
                    Presenter.RenderPreview(view, preview); Presenter.SelectMany(selection);
                    toolbar.Opening.Show(preview);
                    toolbar.SetMessage(botFailure ?? (preview.Valid ? $"Atış hazır. Tur cezası: {preview.DiscardPenalty}." : TableUI.Error(preview.Error)));
                }
                return preview;
            }
            finally
            {
                if (revision == editVersion) { previewPending = false; previewWork = null; UpdateToolbar(); }
                work.Dispose();
            }
        }
        private Task<CommandReceipt> Track(Task<CommandReceipt> task){if(!task.IsCompleted)activeCommand=task;return task;}
        public Task<CommandReceipt> DrawAsync(DrawSource source, CancellationToken token = default)
            => Track(ExecuteCommandAsync(new DrawCommand(Metadata(), source), token));
        public Task<CommandReceipt> DiscardSelectedAsync(bool acceptPenalty = false, CancellationToken token = default)
            => Track(CommitSelectedAsync(acceptPenalty,token));
        private async Task<CommandReceipt> CommitSelectedAsync(bool acceptPenalty,CancellationToken token)
        {
            if (busy) return Rejected(ErrorCode.WrongPhase);
            if (selection.Count != 1) return Rejected(ErrorCode.LastTileRequired);
            busy = true; UpdateToolbar();
            try
            {
                draft.SelectDiscard(selection.First());
                var preview = await RecomputePreviewAsync();
                if (!preview.Valid) return Rejected(preview.Error);
                var plan = draft.Build();
                return await Submit(new CommitTurnCommand(Metadata(), new TurnPlan(plan.Actions, plan.Discard, acceptPenalty)), token);
            }
            finally { busy = false; UpdateToolbar(); }
        }
        private CommandMeta Metadata() => new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version);
        private CommandReceipt Rejected(ErrorCode error) => new CommandReceipt(false, error, view.Version, Array.Empty<VisibleEvent>());
        private async Task<CommandReceipt> ExecuteCommandAsync(GameCommand command, CancellationToken token)
        {
            if (busy || confirming) return Rejected(ErrorCode.WrongPhase);
            busy = true; IsSearchingDiscard = command is DrawCommand draw && draw.Source == DrawSource.PreviousDiscard; UpdateToolbar();
            try { return await Submit(command, token); }
            finally { busy = false; IsSearchingDiscard = false; UpdateToolbar(); }
        }
        private async Task<CommandReceipt> Submit(GameCommand command, CancellationToken token)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
            {
                commandWork = linked;
                try
                {
                    var receipt = await Session.SubmitAsync(command, linked.Token);
                    if (destroyed) return receipt;
                    if (receipt.Accepted)
                    {
                        toolbar.SetMessage("Hamle kaydedildi.");
                        await PresentAcceptedAsync(view, receipt.Events);
                        if (!destroyed) Refresh(view.Actor);
                    }
                    else toolbar.SetMessage(TableUI.Error(receipt.Error));
                    return receipt;
                }
                finally { commandWork = null; }
            }
        }
        private async Task PresentAcceptedAsync(PlayerView before, IReadOnlyList<VisibleEvent> events)
        {
            previewWork?.Cancel(); previewWork = null; ++editVersion; previewPending = false;
            // An accepted command survives pause/caller cancellation. Only scene lifetime cancels its presentation.
            await Presenter.PresentAcceptedAsync(before, Session.GetView(before.Actor), events, lifetime.Token);
        }
        public async void RequestDraw() { await RunFromUI(() => DrawAsync(DrawSource.Stock)); }
        public async void RequestPreviousDraw() { await RunFromUI(() => DrawAsync(DrawSource.PreviousDiscard)); }
        public async void RequestDiscard(bool acceptPenalty = false)
        {
            await RunFromUI(async () =>
            {
                if (!CanControl) return Rejected(ErrorCode.WrongPhase);
                var preview = await RecomputePreviewAsync();
                if (!preview.Valid) { toolbar.SetMessage(TableUI.Error(preview.Error)); return Rejected(preview.Error); }
                if (preview.DiscardPenalty > 0 && !acceptPenalty)
                {
                    confirming = true; UpdateToolbar();
                    bool accepted;
                    try { accepted = await toolbar.Penalty.ConfirmAsync(preview.DiscardPenalty, lifetime.Token); }
                    finally { confirming = false; UpdateToolbar(); }
                    if (!accepted || destroyed || Session.IsPaused) return Rejected(ErrorCode.Cancelled);
                    acceptPenalty = true;
                }
                return await DiscardSelectedAsync(acceptPenalty);
            });
        }
        private async Task RunFromUI(Func<Task<CommandReceipt>> action)
        {
            try { await action(); }
            catch (OperationCanceledException) { if (!destroyed) toolbar.SetMessage("İşlem iptal edildi."); }
            catch (Exception exception) { if (!destroyed) {Debug.LogException(exception);toolbar.SetMessage("Hamle tamamlanamadı. Yeniden deneyin.");} }
        }
        private void Shutdown()
        {
            if(destroyed)return;
            destroyed = true; previewWork?.Cancel(); commandWork?.Cancel(); botWork?.Cancel(); lifetime.Cancel(); botRunner?.Dispose(); Session?.Dispose(); lifetime.Dispose();
        }
        private void OnDestroy(){Shutdown();}
    }
}
