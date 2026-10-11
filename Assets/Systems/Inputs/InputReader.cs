using Systems.Inputs.Events;
using System;
using GameAssets.ActionMaps;
using Systems.EventBusses;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems.Inputs {
    
    public enum EActionTypes { UI, Game, }
    
    // [NEW] Non-static, IDisposable, publishes via IEventBus (design doc §14.4, §14.9)
    public class InputReader : IDisposable {

        private GameActionMaps _input;
        private bool _disposed;
        private IEventBus _eventBus;
        private Vector2 _pointerPosition;

        public void Initialize(IEventBus eventBus, params EActionTypes[] actionsToEnable) {
            Debug.Log("Initializing InputReader");
            Cleanup();
            _disposed = false;
            
            _eventBus = eventBus;
            _input = new GameActionMaps();
            
            // Pointer position (always tracked)
            _input.Gameplay.PointerPosition.performed += OnPointerPositionPerformed;
            _input.Gameplay.PointerPosition.canceled += OnPointerPositionPerformed;
            
            // Pointer command (right click)
            _input.Gameplay.PointerCommand.performed += OnPointerCommandPerformed;
            
            // Confirm (left click)
            _input.Gameplay.Confirm.performed += OnConfirmPerformed;
            
            // Cancel (escape)
            _input.Gameplay.Cancel.performed += OnCancelPerformed;
            
            // Attack move (Q)
            _input.Gameplay.AttackMove.performed += OnAttackMovePerformed;
            
            // Elements (A, Z, E)
            _input.Gameplay.Element1.performed += ctx => OnElementKeyPerformed(0);
            _input.Gameplay.Element2.performed += ctx => OnElementKeyPerformed(1);
            _input.Gameplay.Element3.performed += ctx => OnElementKeyPerformed(2);
            
            // Cast spell (R)
            _input.Gameplay.CastSpell.performed += OnCastSpellPerformed;
            
            // Signature (F)
            _input.Gameplay.Signature.performed += OnSignaturePerformed;
            
            // Reset elements (T)
            _input.Gameplay.ResetElements.performed += OnResetElementsPerformed;
            
            // Stop (S)
            _input.Gameplay.Stop.performed += OnStopPerformed;
            
            // Camera lock (middle mouse)
            _input.Gameplay.CameraLock.performed += OnCameraLockPerformed;
            
            // Zoom (scroll wheel)
            _input.Gameplay.Zoom.performed += OnZoomPerformed;
            _input.Gameplay.Zoom.canceled += OnZoomPerformed;
            
            if (actionsToEnable.Length == 0) return;
            
            foreach (EActionTypes type in Enum.GetValues(typeof(EActionTypes))) { DisableInput(type); }
            foreach (var actionType in actionsToEnable) { EnableInput(actionType); }
        }

        private void OnPointerPositionPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) {
            _pointerPosition = ctx.ReadValue<Vector2>();
            _eventBus.Publish(new PointerPositionEvent(_pointerPosition));
        }

        private void OnPointerCommandPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new PointerCommandEvent(_pointerPosition, Vector3.zero));

        private void OnConfirmPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new ConfirmEvent(_pointerPosition));

        private void OnCancelPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new CancelEvent());

        private void OnAttackMovePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new AttackMoveEvent());

        private void OnElementKeyPerformed(int slot) =>
            _eventBus.Publish(new ElementKeyEvent(slot));

        private void OnCastSpellPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new CastSpellEvent());

        private void OnSignaturePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new SignatureEvent());

        private void OnResetElementsPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new ResetElementsEvent());

        private void OnStopPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new StopEvent());

        private void OnCameraLockPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new CameraLockEvent());

        private void OnZoomPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx) =>
            _eventBus.Publish(new ZoomEvent(ctx.ReadValue<Vector2>().y));

        public void EnableInput(EActionTypes actionType) {
            if (_disposed || _input == null) return;
            switch (actionType) {
                case EActionTypes.UI : _input.UI.Enable(); break;
                case EActionTypes.Game : _input.Gameplay.Enable(); break;
            }
        }

        public void DisableInput(EActionTypes actionType) {
            if (_disposed || _input == null) return;
            switch (actionType) {
                case EActionTypes.UI : _input.UI.Disable(); break;
                case EActionTypes.Game : _input.Gameplay.Disable(); break;
            }
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            Cleanup();
        }

        private void Cleanup() {
            if (_input != null) {
                _input.Gameplay.PointerPosition.performed -= OnPointerPositionPerformed;
                _input.Gameplay.PointerPosition.canceled -= OnPointerPositionPerformed;
                _input.Gameplay.PointerCommand.performed -= OnPointerCommandPerformed;
                _input.Gameplay.Confirm.performed -= OnConfirmPerformed;
                _input.Gameplay.Cancel.performed -= OnCancelPerformed;
                _input.Gameplay.AttackMove.performed -= OnAttackMovePerformed;
                _input.Gameplay.CastSpell.performed -= OnCastSpellPerformed;
                _input.Gameplay.Signature.performed -= OnSignaturePerformed;
                _input.Gameplay.ResetElements.performed -= OnResetElementsPerformed;
                _input.Gameplay.Stop.performed -= OnStopPerformed;
                _input.Gameplay.CameraLock.performed -= OnCameraLockPerformed;
                _input.Gameplay.Zoom.performed -= OnZoomPerformed;
                _input.Gameplay.Zoom.canceled -= OnZoomPerformed;
                _input.Dispose();
                _input = null;
            }
        }
    }
}
