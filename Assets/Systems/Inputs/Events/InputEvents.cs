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

    /// <summary>Escape while a menu has the input (UI map): back / close.</summary>
    public readonly struct UiCancelEvent : IEvent { }
    
    /// <summary>Attack-move key (Q) pressed or released: aiming lasts while it is held.</summary>
    public readonly struct AttackMoveEvent : IEvent {
        public readonly bool Pressed;
        public AttackMoveEvent(bool pressed) => Pressed = pressed;
    }
    
    /// <summary>A / Z / E. With Ctrl held (LevelUp), spends a skill point on the element instead.</summary>
    public readonly struct ElementKeyEvent : IEvent {
        public readonly int Slot; // 0, 1, 2
        public readonly bool LevelUp;

        public ElementKeyEvent(int slot, bool levelUp = false) {
            Slot = slot;
            LevelUp = levelUp;
        }
    }
    
    /// <summary>R pressed, or released (Released: casts in the "quick cast with indicator" mode).</summary>
    public readonly struct CastSpellEvent : IEvent {
        public readonly bool Released;
        public CastSpellEvent(bool released) => Released = released;
    }
    
    /// <summary>F pressed, or released (see <see cref="CastSpellEvent"/>).</summary>
    public readonly struct SignatureEvent : IEvent {
        public readonly bool Released;
        public SignatureEvent(bool released) => Released = released;
    }
    
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
