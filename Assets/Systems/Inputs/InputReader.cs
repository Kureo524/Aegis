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
    /// Letter bindings are remapped to the configured <see cref="KeyboardLayout"/> at startup.
    /// Registered as an entry point in the RootLifetimeScope.
    /// </summary>
    public sealed class InputReader : IInitializable, IDisposable {
        private readonly IEventBus _eventBus;
        private readonly ControlsSettings _settings;
        private GameActionMaps _input;
        private Vector2 _pointerPosition;

        public InputReader(IEventBus eventBus, ControlsSettings settings) {
            _eventBus = eventBus;
            _settings = settings;
        }

        public void Initialize() {
            _input = new GameActionMaps();
            ApplyKeyboardLayout(_input.asset, _settings != null ? _settings.KeyboardLayout : KeyboardLayout.Qwerty);
            GameActionMaps.GameplayActions g = _input.Gameplay;

            g.PointerPosition.performed += OnPointerPosition;
            g.PointerPosition.canceled  += OnPointerPosition;
            g.PointerCommand.performed  += OnPointerCommand;
            g.Confirm.performed         += OnConfirm;
            g.Cancel.performed          += OnCancel;
            g.AttackMove.performed      += OnAttackMove;
            g.AttackMove.canceled       += OnAttackMove;
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
            _input.UI.Cancel.performed  += OnUiCancel;

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
            g.AttackMove.canceled       -= OnAttackMove;
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
            _input.UI.Cancel.performed  -= OnUiCancel;

            _input.Disable();
            // The generated Dispose() uses Object.Destroy, which is refused outside Play mode (tests)
            if (Application.isPlaying) _input.Dispose();
            else UnityEngine.Object.DestroyImmediate(_input.asset);
            _input = null;
        }

        private void OnPointerPosition(InputAction.CallbackContext ctx) {
            _pointerPosition = ctx.ReadValue<Vector2>();
            _eventBus.Publish(new PointerPositionEvent(_pointerPosition));
        }

        private void OnPointerCommand(InputAction.CallbackContext ctx) => _eventBus.Publish(new PointerCommandEvent(_pointerPosition));
        private void OnConfirm(InputAction.CallbackContext ctx)        => _eventBus.Publish(new ConfirmEvent(_pointerPosition));
        private void OnCancel(InputAction.CallbackContext ctx)         => _eventBus.Publish(new CancelEvent());
        private void OnAttackMove(InputAction.CallbackContext ctx)     => _eventBus.Publish(new AttackMoveEvent(ctx.performed));
        private void OnElement1(InputAction.CallbackContext ctx)       => PublishElementKey(0);
        private void OnElement2(InputAction.CallbackContext ctx)       => PublishElementKey(1);
        private void OnElement3(InputAction.CallbackContext ctx)       => PublishElementKey(2);
        private void PublishElementKey(int slot) =>
            _eventBus.Publish(new ElementKeyEvent(slot, levelUp: _input.Gameplay.LevelUpModifier.IsPressed()));
        private void OnCastSpell(InputAction.CallbackContext ctx)      => _eventBus.Publish(new CastSpellEvent());
        private void OnSignature(InputAction.CallbackContext ctx)      => _eventBus.Publish(new SignatureEvent());
        private void OnResetElements(InputAction.CallbackContext ctx)  => _eventBus.Publish(new ResetElementsEvent());
        private void OnStop(InputAction.CallbackContext ctx)           => _eventBus.Publish(new StopEvent());
        private void OnCameraLock(InputAction.CallbackContext ctx)     => _eventBus.Publish(new CameraLockEvent());
        private void OnUiCancel(InputAction.CallbackContext ctx)       => _eventBus.Publish(new UiCancelEvent());
        /// <summary>Moves every single-letter keyboard binding to where that letter is on <paramref name="layout"/>.</summary>
        public static void ApplyKeyboardLayout(InputActionAsset asset, KeyboardLayout layout) {
            const string prefix = "<Keyboard>/";
            foreach (InputActionMap map in asset.actionMaps) {
                foreach (InputAction action in map.actions) {
                    for (int i = 0; i < action.bindings.Count; i++) {
                        string path = action.bindings[i].path;
                        if (path == null || path.Length != prefix.Length + 1 || !path.StartsWith(prefix)) continue;
                        char letter = path[prefix.Length];
                        if (letter < 'a' || letter > 'z') continue;

                        string key = KeyboardLayouts.PhysicalKey(layout, letter);
                        if (key.Length != 1 || key[0] != letter) action.ApplyBindingOverride(i, prefix + key);
                    }
                }
            }
        }

        private void OnZoom(InputAction.CallbackContext ctx)           => _eventBus.Publish(new ZoomEvent(ctx.ReadValue<Vector2>().y));
    }
}
