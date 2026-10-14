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
    /// Letter bindings are remapped to the configured <see cref="KeyboardLayout"/> at startup, except
    /// the positional ones (spell keys, attack-move, stop).
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
            foreach (InputAction action in AbilityActions(g)) {
                action.performed += OnAbility;
                action.canceled += OnAbility;
            }
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
            foreach (InputAction action in AbilityActions(g)) {
                action.performed -= OnAbility;
                action.canceled -= OnAbility;
            }
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
        // Slot order of AbilityKeyEvent: Q W E R, then D F (design doc §7.1)
        private static InputAction[] AbilityActions(GameActionMaps.GameplayActions g) =>
            new[] { g.Ability1, g.Ability2, g.Ability3, g.Ability4, g.SummonerD, g.SummonerF };

        private void OnAbility(InputAction.CallbackContext ctx) {
            int slot = System.Array.IndexOf(AbilityActions(_input.Gameplay), ctx.action);
            if (slot < 0) return;
            bool released = ctx.canceled;
            bool levelUp = !released && _input.Gameplay.LevelUpModifier.IsPressed();
            _eventBus.Publish(new AbilityKeyEvent(slot, levelUp, released));
        }
        private void OnStop(InputAction.CallbackContext ctx)           => _eventBus.Publish(new StopEvent());
        private void OnCameraLock(InputAction.CallbackContext ctx)     => _eventBus.Publish(new CameraLockEvent());
        private void OnUiCancel(InputAction.CallbackContext ctx)       => _eventBus.Publish(new UiCancelEvent());
        /// <summary>Binding group of keys bound by position (Q W E R, D F, attack-move…): never remapped.</summary>
        public const string PositionalGroup = "Positional";

        /// <summary>
        /// Moves every single-letter keyboard binding to where that letter is on <paramref name="layout"/>,
        /// except the positional ones (MOBA keys stay where QWERTY has them: A Z E R in AZERTY).
        /// </summary>
        public static void ApplyKeyboardLayout(InputActionAsset asset, KeyboardLayout layout) {
            const string prefix = "<Keyboard>/";
            foreach (InputActionMap map in asset.actionMaps) {
                foreach (InputAction action in map.actions) {
                    for (int i = 0; i < action.bindings.Count; i++) {
                        string path = action.bindings[i].path;
                        string groups = action.bindings[i].groups;
                        if (groups != null && groups.Contains(PositionalGroup)) continue;
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
