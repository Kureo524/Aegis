using UnityEngine;
using Systems.EventBusses;

namespace Systems.Inputs.Events {
    // Input events published by InputReader, consumed by PlayerCommandInput / CameraController
    
    // Screen-space only: projecting onto the ground is the consumer's job (it owns the camera).
    public readonly struct PointerCommandEvent : IEvent {
        public readonly Vector2 ScreenPosition;
        
        public PointerCommandEvent(Vector2 screenPosition) {
            ScreenPosition = screenPosition;
        }
    }
    
    public readonly struct PointerPositionEvent : IEvent {
        public readonly Vector2 Position;
        
        public PointerPositionEvent(Vector2 position) {
            Position = position;
        }
    }
    
    public readonly struct ConfirmEvent : IEvent {
        public readonly Vector2 ScreenPosition;
        
        public ConfirmEvent(Vector2 screenPosition) {
            ScreenPosition = screenPosition;
        }
    }
    
    public readonly struct CancelEvent : IEvent { }
    
    public readonly struct AttackMoveEvent : IEvent { }
    
    public readonly struct ElementKeyEvent : IEvent {
        public readonly int Slot; // 0, 1, 2
        
        public ElementKeyEvent(int slot) {
            Slot = slot;
        }
    }
    
    public readonly struct CastSpellEvent : IEvent { }
    
    public readonly struct SignatureEvent : IEvent { }
    
    public readonly struct ResetElementsEvent : IEvent { }
    
    public readonly struct StopEvent : IEvent { }
    
    public readonly struct CameraLockEvent : IEvent { }
    
    public readonly struct ZoomEvent : IEvent {
        public readonly float Delta;
        
        public ZoomEvent(float delta) {
            Delta = delta;
        }
    }
}
