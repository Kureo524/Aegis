using Features.CameraControl;
using Features.Flow;
using Features.Options;
using Features.Progression;
using Features.Spells;
using Features.StateMachines;
using Systems.EventBusses;
using Systems.Inputs;
using Systems.UIManagement;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Containers {
    /// <summary>
    /// Application-wide scope (design doc §14.4). Lives on a prefab referenced by the
    /// VContainerSettings asset (Assets/Data/Settings), so VContainer instantiates it once,
    /// before the first scene scope, and keeps it across scene loads. Every scene scope is
    /// automatically its child.
    /// </summary>
    public class RootLifetimeScope : LifetimeScope {
        [SerializeField] private GameFlowStateMachine stateMachine;
        [SerializeField] private GameFlowSettings gameFlowSettings;
        [SerializeField] private CameraSettings cameraSettings;
        [SerializeField] private ExperienceCurve experienceCurve;
        [SerializeField] private ControlsSettings controlsSettings;
        [SerializeField] private SpellSettings spellSettings;
        [SerializeField] private Features.UI.UiTheme uiTheme;
        [SerializeField] private Features.Meta.MetaSettings metaSettings;
        [SerializeField] private Features.Characters.ChampionRoster championRoster;
        [SerializeField] private Features.Run.StageCatalog stageCatalog;

        protected override void Configure(IContainerBuilder builder) {
            // Infrastructure
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();
            builder.Register<PanelManager>(Lifetime.Singleton).As<IPanelManager>();
            builder.Register<Features.UI.UiPicking>(Lifetime.Singleton).As<IPointerOverUi>();   // presentation, seen by the gameplay as an interface
            builder.RegisterEntryPoint<InputReader>().AsSelf();

            // Settings (ScriptableObjects)
            builder.RegisterInstance(gameFlowSettings);
            builder.RegisterInstance(cameraSettings);
            builder.RegisterInstance(experienceCurve);
            builder.RegisterInstance(controlsSettings);
            builder.RegisterInstance(spellSettings);
            builder.RegisterInstance(uiTheme);
            builder.RegisterInstance(metaSettings);
            builder.Register<Features.Meta.MetaProgress>(Lifetime.Singleton);   // persistent currency and trees (JSON)
            builder.Register<UserOptions>(Lifetime.Singleton);   // player's choices (JSON) over the defaults
            if (championRoster != null) builder.RegisterInstance(championRoster);
            else builder.RegisterInstance(ScriptableObject.CreateInstance<Features.Characters.ChampionRoster>());
            builder.RegisterEntryPoint<Features.Characters.ChampionSelection>().AsSelf();   // champion of the next runs
            if (stageCatalog != null) builder.RegisterInstance(stageCatalog);
            else builder.RegisterInstance(ScriptableObject.CreateInstance<Features.Run.StageCatalog>());
            builder.RegisterEntryPoint<Features.Run.StageSelection>().AsSelf();   // stage of the next runs (short run by default)

            // Game flow
            builder.RegisterComponent(stateMachine);
            builder.RegisterEntryPoint<GameFlowController>();
            builder.RegisterEntryPoint<InputMapSwitcher>();
            builder.RegisterEntryPoint<TimeScaleService>();
            builder.RegisterEntryPoint<CursorConfinement>();
        }
    }
}
