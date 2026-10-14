using System;
using Features.Spells;
using UnityEngine;

namespace Features.Characters {
    /// <summary>One animation: its frames played over <see cref="Duration"/> seconds.</summary>
    [Serializable]
    public class SpriteClip {
        public Sprite[] Frames = Array.Empty<Sprite>();
        [Min(0.01f)] public float Duration = 1f;
        public bool Loop;

        public bool IsEmpty => Frames == null || Frames.Length == 0;

        /// <summary>Frame at <paramref name="time"/> seconds (looped, or held on the last frame).</summary>
        public Sprite At(float time) => FrameAt(Duration > 0f ? time / Duration : 1f);

        /// <summary>Frame at a normalized time (0 = first frame, 1 = end).</summary>
        public Sprite FrameAt(float normalized) {
            if (IsEmpty) return null;
            int count = Frames.Length;
            int index = Loop ? (int)Mathf.Repeat(normalized * count, count) : Mathf.Clamp((int)(normalized * count), 0, count - 1);
            return Frames[Mathf.Clamp(index, 0, count - 1)];
        }
    }

    [Serializable]
    public struct SpellSpriteClip {
        public SpellDefinition Spell;
        public SpriteClip Clip;
    }

    /// <summary>
    /// The animated look of a character (design doc §3): sprite clips pre-rendered from its 3D
    /// model at the game camera angle, facing right (flipped for the left). CharacterVisual plays
    /// idle / run, the attack timed on the windup (the impact frame lands on the hit), a cast clip
    /// per spell, the hit reaction and the death. Read-only data.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Characters/Sprite Animations", fileName = "Anim_")]
    public class SpriteAnimationSet : ScriptableObject {
        [SerializeField] private SpriteClip idle = new() { Loop = true };
        [SerializeField] private SpriteClip run = new() { Loop = true };
        [SerializeField] private SpriteClip attack = new();
        [Tooltip("Normalized time of the attack clip where the blow lands.")]
        [SerializeField, Range(0.05f, 1f)] private float attackImpact = 0.45f;
        [SerializeField] private SpriteClip hit = new();
        [SerializeField] private SpriteClip death = new();
        [SerializeField] private SpriteClip dash = new() { Loop = true };
        [Tooltip("Played for spells without their own clip.")]
        [SerializeField] private SpriteClip cast = new();
        [SerializeField] private SpellSpriteClip[] spellClips = Array.Empty<SpellSpriteClip>();
        [Tooltip("Run clip played at this move speed; faster or slower moves play it faster or slower.")]
        [SerializeField, Min(0.1f)] private float runReferenceSpeed = 3.25f;

        public SpriteClip Idle => idle;
        public SpriteClip Run => run;
        public SpriteClip Attack => attack;
        public float AttackImpact => attackImpact;
        public SpriteClip Hit => hit;
        public SpriteClip Death => death;
        public SpriteClip Dash => dash;
        public float RunReferenceSpeed => runReferenceSpeed;
        public SpellSpriteClip[] SpellClips => spellClips;

        /// <summary>The clip of <paramref name="spell"/>, else the generic cast clip, else the attack.</summary>
        public SpriteClip CastClip(SpellDefinition spell) {
            foreach (SpellSpriteClip c in spellClips)
                if (c.Spell == spell && c.Clip != null && !c.Clip.IsEmpty) return c.Clip;
            return !cast.IsEmpty ? cast : attack;
        }

        /// <summary>First idle frame: the still sprite (portrait fallback, editor previews).</summary>
        public Sprite Still => idle.FrameAt(0f);
    }
}
