using Features.StateMachines;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>
    /// Registered by each scene scope with the state that scene stands for (Menu scene → Menu,
    /// game scene → Game). Lets any scene be launched directly from the Editor and still go
    /// through the state machine.
    /// </summary>
    public sealed class SceneEntryPoint : IStartable {
        private readonly GameFlowStateMachine _machine;
        private readonly GameState _sceneState;

        public SceneEntryPoint(GameFlowStateMachine machine, GameState sceneState) {
            _machine = machine;
            _sceneState = sceneState;
        }

        public void Start() {
            if (_machine.HasState && _machine.CurrentState == _sceneState) return;
            _machine.TryChange(_sceneState);
        }
    }
}
