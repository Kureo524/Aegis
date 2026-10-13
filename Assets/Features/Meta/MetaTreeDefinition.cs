using System;
using Features.Characters;
using UnityEngine;

namespace Features.Meta {
    /// <summary>The meta tree of one champion (design doc §9.7): its nodes, shown from the main menu.</summary>
    [CreateAssetMenu(menuName = "Game/Meta/Tree", fileName = "MetaTree_")]
    public class MetaTreeDefinition : ScriptableObject {
        [SerializeField] private string displayName;
        [SerializeField] private CharacterDefinition character;
        [SerializeField] private MetaNodeDefinition[] nodes = Array.Empty<MetaNodeDefinition>();

        public string DisplayName => displayName;
        public CharacterDefinition Character => character;
        public MetaNodeDefinition[] Nodes => nodes;
    }
}
