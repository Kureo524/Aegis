using System;
using GameAssets.ActionMaps;
using Systems.EventBusses;
using Systems.Inputs.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Systems.Inputs {

    public enum EActionTypes { UI, Game }

    /// <summary>
    /// Single owner of the generated action maps. Translates raw input into typed events on the
    /// <see cref="IEventBus"/> (design doc §14.3, §14.9). Every map starts disabled; which map is
    /// active is decided by the game state (see Features.Flow.InputMapSwitcher).
    /// Registered as an entry point in the RootLifetimeScope.
    /// </summary>
    public sealed class InputReader : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private GameActionMaps _input;
        private Vector2 _pointerPosition;

        public InputReader(IEventBus eventBus) {
            _eventBus = eventBus;
        }

        public void Initialize() {
            _input = new GameActionMaps();
            GameActionMaps.GameplayActions g = _input.Gameplay;

            g.PointerPosition.performed += OnPointerPosition;
            g.PointerPosition.canceled  += OnPointerPosition;
            g.PointerCommand.performed  += OnPointerCommand;
            g.Confirm.performed         += OnConfirm;
            g.Cancel.performed          += OnCancel;
            g.AttackMove.performed      += OnAttackMove;
            g.Element1.performed        += OnElement1;
            g.Element2.performed        += OnElement2;
            g.Element3.performed        += OnElement3;
            g.CastSpell.performed       += OnCastSpell;
            g.Signature.performed       += OnSignature;
            g.ResetElements.performed   += OnResetElements;
            g.Stop.performed            += OnStop;
            g.CameraLock.performed      += OnCameraLock;
            g.Zoom.performed            += OnZoom;
            g.Zoom.canceled             += OnZoom;

            _input.Disable();
        }

        public void EnableInput(EActionTypes actionType) {
            switch (actionType) {
                case EActionTypes.UI:   _input?.UI.Enable(); break;
                case EActionTypes.Game: _input?.Gameplay.Enable(); break;
            }
        }

        public void DisableInput(EActionTypes actionType) {
            switch (actionType) {
                case EActionTypes.UI:   _input?.UI.Disable(); break;
                case EActionTypes.Game: _input?.Gameplay.Disable(); break;
            }
        }

        public void Dispose() {
            if (_input == null) return;
            GameActionMaps.GameplayActions g = _input.Gameplay;

            g.PointerPosition.performed -= OnPointerPosition;
            g.PointerPosition.canceled  -= OnPointerPosition;
            g.PointerCommand.performed  -= OnPointerCommand;
            g.Confirm.performed         -= OnConfirm;
            g.Cancel.performed          -= OnCancel;
            g.AttackMove.performed      -= OnAttackMove;
            g.Element1.performed        -= OnElement1;
            g.Element2.performed        -= OnElement2;
            g.Element3.performed        -= OnElement3;
            g.CastSpell.performed       -= OnCastSpell;
            g.Signature.performed       -= OnSignature;
            g.ResetElements.performed   -= OnResetElements;
            g.Stop.performed            -= OnStop;
            g.CameraLock.performed      -= OnCameraLock;
            g.Zoom.performed            -= OnZoom;
            g.Zoom.canceled             -= OnZoom;

            _input.Disable();
            _input.Dispose();
            _input = null;
        }

        private void OnPointerPosition(InputAction.CallbackContext ctx) {
            _pointerPosition = ctx.ReadValue<Vector2>();
            _eventBus.Publish(new PointerPositionEvent(_pointerPosition));
        }

        private void OnPointerCommand(InputAction.CallbackContext ctx) => _eventBus.Publish(new PointerCommandEvent(_pointerPosition));
        private void OnConfirm(InputAction.CallbackContext ctx)        => _eventBus.Publish(new ConfirmEvent(_pointerPosition));
        private void OnCancel(InputAction.CallbackContext ctx)         => _eventBus.Publish(new CancelEvent());
        private void OnAttackMove(InputAction.CallbackContext ctx)     => _eventBus.Publish(new AttackMoveEvent());
        private void OnElement1(InputAction.CallbackContext ctx)       => _eventBus.Publish(new ElementKeyEvent(0));
        private void OnElement2(InputAction.CallbackContext ctx)       => _eventBus.Publish(new ElementKeyEvent(1));
        private void OnElement3(InputAction.CallbackContext ctx)       => _eventBus.Publish(new ElementKeyEvent(2));
        private void OnCastSpell(InputAction.CallbackContext ctx)      => _eventBus.Publish(new CastSpellEvent());
        private void OnSignature(InputAction.CallbackContext ctx)      => _eventBus.Publish(new SignatureEvent());
        private void OnResetElements(InputAction.CallbackContext ctx)  => _eventBus.Publish(new ResetElementsEvent());
        private void OnStop(InputAction.CallbackContext ctx)           => _eventBus.Publish(new StopEvent());
        private void OnCameraLock(InputAction.CallbackContext ctx)     => _eventBus.Publish(new CameraLockEvent());
        private void OnZoom(InputAction.CallbackContext ctx)           => _eventBus.Publish(new ZoomEvent(ctx.ReadValue<Vector2>().y));
    }
}
