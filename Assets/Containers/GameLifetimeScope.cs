using System.Collections.Generic;
using Features.CameraControl;
using Features.Characters;
using Features.Combat;
using Features.Economy;
using Features.Enemies;
using Features.Run;
using Features.Augments;
using Features.Loot;
using Features.Spells;
using Systems.Pooling;
using Features.Flow;
using Features.StateMachines;
using Features.UI;
using Features.UI.DebugTools;
using Features.UI.Panels;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Containers {
    /// <summary>
    /// Game scene scope (TestScene for now). Destroyed with the scene, so all run state is reset
    /// on every load (design doc §14.4). Shared services come from the RootLifetimeScope.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope {
        public Camera mainCamera;
        public CameraController cameraController;
        public QuitOverlay quitOverlay;
        public CharacterDebugPanel characterDebugPanel;
        public PlayerHud playerHud;
        public PauseMenu pauseMenu;
        public RunHudView runHud;
        public AugmentPanel augmentPanel;
        public BossHudView bossHud;
        public RunEndPanel runEndPanel;
        public ShopPanel shopPanel;
        public AnvilPanel anvilPanel;

        [Header("Run")]
        [Tooltip("The stage played in this scene: waves, bonus waves, lives (design doc §9.1).")]
        public StageDefinition stage;
        public RunStateMachine runStateMachine;
        [Tooltip("Score of the run (design doc §9.9). Empty: no score system, the rest of the run is unchanged.")]
        public Features.Score.ScoreRules scoreRules;
        [Tooltip("Elixirs, anvils, items and stat choices of the shops (design doc §9.3).")]
        public ShopCatalog shopCatalog;
        [Tooltip("Augments and draft rules (design doc §9.4).")]
        public AugmentCatalog augmentCatalog;
        [Tooltip("Ground loot dropped by the player's passives (design doc §9.5).")]
        public LootSettings lootSettings;
        public DamageNumbersView damageNumbers;

        [Header("Player")]
        [Tooltip("Player prefab variant (Assets/Prefabs/Characters/Player).")]
        public Character playerPrefab;
        public CharacterDefinition playerDefinition;
        public Transform playerSpawn;

        [Header("Enemies")]
        [Tooltip("Flow field, AI rate and continuous spawn of the horde (design doc §9.6).")]
        public HordeSettings hordeSettings;
        [Tooltip("Fixed characters spawned at scene start (training dummies).")]
        public List<CharacterSpawn> enemySpawns = new();

        protected override void Configure(IContainerBuilder builder) {
            // The champion picked in the menu (root scope); this scene's own one otherwise
            CharacterDefinition champion = playerDefinition;
            if (Parent != null && Parent.Container.TryResolve(out Features.Characters.ChampionSelection selection) && selection.Current != null)
                champion = selection.Current;

            builder.RegisterComponent(mainCamera);
            builder.Register<PrefabPool>(Lifetime.Scoped);
            builder.Register<ProjectileLauncher>(Lifetime.Scoped);
            builder.Register<SpellExecutor>(Lifetime.Scoped);
            builder.RegisterInstance(hordeSettings);
            builder.RegisterEntryPoint<FlowFieldService>().AsSelf();
            builder.RegisterEntryPoint<EnemyDirector>().AsSelf();
            builder.RegisterEntryPoint<HordeSpawner>().AsSelf();
            builder.RegisterEntryPoint<Wallet>().AsSelf();
            builder.RegisterEntryPoint<Features.Characters.DeathAnimations>().AsSelf();
            builder.RegisterComponent(cameraController);
            if (quitOverlay != null) builder.RegisterComponent(quitOverlay);
            if (characterDebugPanel != null) builder.RegisterComponent(characterDebugPanel);
            if (playerHud != null) builder.RegisterComponent(playerHud);
            if (damageNumbers != null) builder.RegisterComponent(damageNumbers);
            if (pauseMenu != null) builder.RegisterComponent(pauseMenu);
            if (runHud != null) builder.RegisterComponent(runHud);
            if (augmentPanel != null) builder.RegisterComponent(augmentPanel);
            if (bossHud != null) builder.RegisterComponent(bossHud);
            if (runEndPanel != null) builder.RegisterComponent(runEndPanel);
            builder.RegisterInstance(stage);
            builder.RegisterComponent(runStateMachine);
            // Meta progression of the run's champion (§9.7)
            builder.Register(r => new Features.Meta.RunModifiers(r.Resolve<Features.Meta.MetaProgress>(), champion), Lifetime.Singleton);
            builder.RegisterEntryPoint<Features.Meta.MetaApplier>();
            builder.RegisterEntryPoint<RunDirector>().AsSelf();
            builder.RegisterEntryPoint<RewardController>().AsSelf();
            if (scoreRules != null) {
                builder.RegisterInstance(scoreRules);
                builder.RegisterEntryPoint<Features.Score.ScoreService>().AsSelf();
            }
            builder.RegisterInstance(shopCatalog);
            builder.Register<PlayerUpgrades>(Lifetime.Scoped);
            builder.RegisterEntryPoint<Inventory>().AsSelf();
            builder.RegisterEntryPoint<ShopService>().AsSelf();
            if (shopPanel != null) builder.RegisterComponent(shopPanel);
            if (anvilPanel != null) builder.RegisterComponent(anvilPanel);
            builder.RegisterInstance(augmentCatalog);
            builder.RegisterEntryPoint<AugmentController>().AsSelf();
            builder.RegisterInstance(lootSettings);
            builder.RegisterEntryPoint<LootService>().AsSelf();
            builder.RegisterEntryPoint<PauseInput>();

            builder.RegisterEntryPoint<PlayerSpawner>()
                .WithParameter(playerPrefab)
                .WithParameter(champion)
                .WithParameter(playerSpawn);
            builder.RegisterEntryPoint<EnemySpawner>()
                .WithParameter<IReadOnlyList<CharacterSpawn>>(enemySpawns);

            builder.RegisterEntryPoint<SceneEntryPoint>().WithParameter(GameState.Game);
        }
    }
}
