using System;
using UnityEngine;

namespace Systems.EventBusses {
    public class InputsEventBus {
        public static event Action<Vector2> OnMovementInputChanged;
        
        public static void FireMovementInputChanged(Vector2 input) => OnMovementInputChanged?.Invoke(input);
    }
}
