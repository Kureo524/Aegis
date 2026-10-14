using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Characters {
    /// <summary>
    /// The playable champions, in the order of the champion select screen (design doc §7.10). The
    /// first one is the default (and the one of the test scene when nothing was picked).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Champion Roster", fileName = "ChampionRoster")]
    public class ChampionRoster : ScriptableObject {
        [SerializeField] private CharacterDefinition[] champions = Array.Empty<CharacterDefinition>();

        public IReadOnlyList<CharacterDefinition> Champions => champions;
        public CharacterDefinition Default => champions.Length > 0 ? champions[0] : null;
    }
}
