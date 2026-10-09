using Systems.Characters;
using Systems.EventBusses;
using UnityEngine;

namespace Systems.Controllers {
    public class PlayerCharacterController : MonoBehaviour {
        
        [SerializeField] private MovingCharacter movingCharacter;
        
        private void OnEnable() {
            InputsEventBus.OnMovementInputChanged += ApplyMoveInput;
            Debug.Log("Input binded");
        }

        private void ApplyMoveInput(Vector2 direction) {
            Debug.Log(direction);
            movingCharacter.ApplyMovementDirection(direction);
        }
    }
}
