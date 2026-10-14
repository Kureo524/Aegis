using System.Collections;
using Features.Characters;
using Features.Combat;
using Features.Enemies;
using Features.Movement;
using Features.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode {
    /// <summary>Animated sprites (CC0 models pre-rendered): loops, attack on the windup, death after the body is gone.</summary>
    public class CharacterAnimationTests {
        private static IObjectResolver Game => Object.FindFirstObjectByType<Containers.GameLifetimeScope>().Container;
        private static Character Player => Game.Resolve<EnemyDirector>().Player;

        [UnityTearDown]
        public IEnumerator TearDown() {
            Object.Destroy(VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryCharacter_HasItsAnimations_AndItsName() {
            yield return GameFlowSmokeTests.LoadGameScene();
            var stage = Game.Resolve<StageDefinition>();
            var seen = new System.Collections.Generic.HashSet<CharacterDefinition> { Player.Definition };
            foreach (WaveDefinition w in stage.Waves)
                foreach (HordeSpawnEntry e in w.SpawnTable) seen.Add(e.Definition);
            foreach (CharacterDefinition d in seen) {
                Assert.IsNotNull(d.Animations, $"{d.name} has no animations");
                Assert.IsFalse(d.Animations.Idle.IsEmpty, $"{d.name} idle");
                Assert.IsFalse(d.Animations.Death.IsEmpty, $"{d.name} death");
            }
            Assert.IsFalse(Player.Definition.Animations.Run.IsEmpty);
            SpriteAnimationSet ez = Player.Definition.Animations;
            Assert.AreEqual(4, ez.SpellClips.Length, "Q W E R have their own clips");
            foreach (SpellSpriteClip c in ez.SpellClips) {
                Assert.IsNotNull(c.Spell);
                Assert.AreSame(c.Clip, ez.CastClip(c.Spell));
                Assert.IsFalse(c.Clip.IsEmpty);
            }
        }

        [UnityTest]
        public IEnumerator Player_RunsThenIdles_AndTheAttackPlaysOnTheWindup() {
            yield return GameFlowSmokeTests.LoadGameScene();
            yield return null;
            var visual = Player.GetComponentInChildren<CharacterVisual>();
            SpriteAnimationSet set = Player.Definition.Animations;
            Assert.IsTrue(visual.IsAnimated);

            Player.GetComponent<CharacterMovement>().SetDestination(Player.transform.position + new Vector3(-6f, 0f, -4f));
            yield return new WaitForSeconds(0.4f);
            Assert.Contains(visual.Renderer.sprite, set.Run.Frames, "running: a run frame");

            Player.GetComponent<CharacterMovement>().Stop();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.Contains(visual.Renderer.sprite, set.Idle.Frames, "standing: an idle frame");

            visual.PlayWindup(0.3f);
            yield return null;
            Assert.AreSame(set.Attack, visual.CurrentAction);
            Assert.Contains(visual.Renderer.sprite, set.Attack.Frames);
        }

        [UnityTest]
        public IEnumerator Death_PlaysOnItsOwn_OnceTheBodyIsGone() {
            yield return GameFlowSmokeTests.LoadGameScene();
            yield return null;
            HordeSpawnEntry entry = Game.Resolve<StageDefinition>().Waves[0].SpawnTable[0];
            Character enemy = Game.Resolve<HordeSpawner>().Spawn(entry.Prefab, entry.Definition, Player.transform.position + new Vector3(5f, 0f, 0f));
            var deaths = Game.Resolve<DeathAnimations>();
            int before = deaths.PlayingCount;

            enemy.TakeDamage(new DamageInfo(1e7f, DamageType.True, false, false, Player));
            yield return null;
            Assert.IsFalse(enemy.IsAlive);
            Assert.AreEqual(before + 1, deaths.PlayingCount, "the death clip plays at the spot");

            yield return new WaitForSeconds(entry.Definition.Animations.Death.Duration + 1f);
            Assert.AreEqual(before, deaths.PlayingCount, "then fades away");
        }
    }
}
