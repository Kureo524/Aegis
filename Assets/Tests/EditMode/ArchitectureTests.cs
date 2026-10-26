using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Features.Score;
using NUnit.Framework;
using Systems.EventBusses;

namespace Game.Tests.EditMode {
    /// <summary>
    /// The architecture rules (design doc §14), checked on the compiled code: no global mutable
    /// state, the layers only depend downwards (Containers → Presentation → Features → Systems),
    /// and a system that only talks
    /// through the bus (the score) is referenced by no other one, so it can be removed.
    /// </summary>
    public class ArchitectureTests {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static Assembly Features => typeof(ScoreService).Assembly;
        private static Assembly Systems => typeof(EventBus).Assembly;

        private static IEnumerable<Type> GameTypes => Features.GetTypes().Concat(Systems.GetTypes())
            .Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() == null);

        [Test]
        public void NoStaticMutableField_NoSingleton() {
            var offenders = new List<string>();
            foreach (Type type in GameTypes) {
                foreach (FieldInfo f in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                    if (f.IsLiteral || f.IsInitOnly || f.GetCustomAttribute<CompilerGeneratedAttribute>() != null) continue;
                    offenders.Add($"{type.FullName}.{f.Name}");
                }
                PropertyInfo instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (instance != null && instance.PropertyType == type) offenders.Add($"{type.FullName}.Instance");
            }
            Assert.IsEmpty(offenders, "static mutable state: " + string.Join(", ", offenders));
        }

        [Test]
        public void Layers_Systems_Gameplay_Presentation_Containers() {
            string[] refs = Systems.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            CollectionAssert.DoesNotContain(refs, Features.GetName().Name);
            CollectionAssert.DoesNotContain(refs, "Game.Containers");
            string[] gameplay = Features.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            CollectionAssert.DoesNotContain(gameplay, "Game.Containers", "the gameplay does not know how it is wired");
            CollectionAssert.DoesNotContain(gameplay, "Game.Presentation", "the gameplay does not know its UI");
            Assert.AreEqual("Game.Presentation", typeof(Features.UI.Panels.RunEndPanel).Assembly.GetName().Name, "the UI is its own layer");
        }

        [Test]
        public void NoSystem_DependsOnTheScore_SoItCanBeRemoved() {
            var users = new List<string>();
            foreach (Type type in GameTypes) {
                if (type == typeof(ScoreService) || type.Namespace == typeof(ScoreService).Namespace) continue;
                foreach (FieldInfo f in type.GetFields(All))
                    if (f.FieldType == typeof(ScoreService)) users.Add($"{type.Name}.{f.Name}");
                foreach (MethodBase m in type.GetConstructors(All).Cast<MethodBase>().Concat(type.GetMethods(All)))
                    if (m.GetParameters().Any(p => p.ParameterType == typeof(ScoreService))) users.Add($"{type.Name}.{m.Name}");
            }
            Assert.IsEmpty(users, "the score only talks through the bus: " + string.Join(", ", users));
        }
    }
}
