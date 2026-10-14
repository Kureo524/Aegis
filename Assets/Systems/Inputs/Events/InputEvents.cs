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
    
    /// <summary>Attack-move key (Q, physical A in QWERTY) pressed or released: aiming lasts while it is held.</summary>
    public readonly struct AttackMoveEvent : IEvent {
        public readonly bool Pressed;
        public AttackMoveEvent(bool pressed) => Pressed = pressed;
    }
    
    /// <summary>
    /// A spell key: Q W E R (slots 0–3, on A Z E R in AZERTY) or D F (summoner spells, slots 4–5).
    /// Pressed, or released (casts in the "quick cast with indicator" mode). With Ctrl held
    /// (LevelUp), Q W E R spend a skill point on the ability instead.
    /// </summary>
    public readonly struct AbilityKeyEvent : IEvent {
        public readonly int Slot;
        public readonly bool LevelUp;
        public readonly bool Released;

        public AbilityKeyEvent(int slot, bool levelUp = false, bool released = false) {
            Slot = slot;
            LevelUp = levelUp;
            Released = released;
        }
    }
    
    public readonly struct StopEvent : IEvent { }
    
    public readonly struct CameraLockEvent : IEvent { }
    
    public readonly struct ZoomEvent : IEvent {
        public readonly float Delta;
        
        public ZoomEvent(float delta) {
            Delta = delta;
        }
    }

    /// <summary>The shop key (P) was pressed.</summary>
    public readonly struct ShopKeyEvent : IEvent { }
}
