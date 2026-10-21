using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Okey101.Presentation
{
    public sealed class TableInput : MonoBehaviour
    {
        private InputAction position;
        private InputAction press;
        private GameBootstrap game;
        private void Awake()
        {
            position = new InputAction("Table pointer", InputActionType.Value, "<Pointer>/position");
            press = new InputAction("Table click", InputActionType.Button, "<Pointer>/press");
        }
        public void Initialize(GameBootstrap bootstrap) { game = bootstrap; }
        private void OnEnable() { position.Enable(); press.Enable(); }
        private void OnDisable() { position.Disable(); press.Disable(); }
        private void OnDestroy() { position.Dispose(); press.Dispose(); }
        private void Update()
        {
            if (game == null || !game.CanControl || !press.WasPressedThisFrame()) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var ray = game.Presenter.TableCamera.ScreenPointToRay(position.ReadValue<Vector2>());
            if (!Physics.Raycast(ray, out var hit, 100)) return;
            var tile = hit.collider.GetComponentInParent<TileView>();
            if (tile != null)
            {
                if (tile.Role == TileRole.Hand) game.ClickTile(tile.Id);
                else if (tile.Role == TileRole.Stock) game.RequestDraw();
                else if (tile.Role == TileRole.Discard) game.RequestPreviousDraw();
                else if (tile.Role == TileRole.Meld && tile.MeldId.HasValue) game.SelectMeldTile(tile.MeldId.Value, tile.Id);
            }
            else if (game.Presenter.Rack.TryGetSlot(hit.collider, out var slot)) game.MoveSelectedTo(slot);
        }
    }
}
