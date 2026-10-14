using System;
using System.Collections.Generic;
using Features.Events;
using Systems.EventBusses;
using UnityEngine;
using VContainer.Unity;

namespace Features.Characters {
    /// <summary>
    /// Plays the death clips (design doc §3) once the bodies are gone: a pooled billboard sprite at
    /// the spot, the clip, a short hold on the last frame, then a fade. Game scope.
    /// </summary>
    public sealed class DeathAnimations : IStartable, ITickable, IDisposable {
        private const float Hold = 0.35f, Fade = 0.5f;
        private const int MaxPlaying = 80;

        private sealed class Playing {
            public SpriteRenderer Renderer;
            public SpriteClip Clip;
            public float Start;
        }

        private readonly Camera _camera;
        private readonly IDisposable _subscription;
        private readonly List<Playing> _playing = new();
        private readonly Stack<SpriteRenderer> _free = new();
        private Transform _root;

        public int PlayingCount => _playing.Count;

        public DeathAnimations(IEventBus eventBus, Camera camera) {
            _camera = camera;
            _subscription = eventBus.Subscribe<DeathAnimationEvent>(Play);
        }

        public void Start() { }

        private void Play(DeathAnimationEvent e) {
            if (e.Clip == null || e.Clip.IsEmpty) return;
            if (_playing.Count >= MaxPlaying) Release(0);   // the oldest makes room
            if (_root == null) _root = new GameObject("=== Death animations ===").transform;
            SpriteRenderer r = _free.Count > 0 ? _free.Pop() : new GameObject("Death").AddComponent<SpriteRenderer>();
            r.transform.SetParent(_root, false);
            r.gameObject.SetActive(true);
            r.transform.position = e.Position;
            r.transform.localScale = e.Scale;
            r.flipX = e.FlipX;
            r.color = Color.white;
            r.sprite = e.Clip.FrameAt(0f);
            _playing.Add(new Playing { Renderer = r, Clip = e.Clip, Start = Time.time });
        }

        public void Tick() {
            Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            for (int i = _playing.Count - 1; i >= 0; i--) {
                Playing p = _playing[i];
                if (p.Renderer == null) {
                    _playing.RemoveAt(i);
                    continue;
                }
                float t = Time.time - p.Start;
                p.Renderer.transform.rotation = facing;
                p.Renderer.sprite = p.Clip.At(Mathf.Min(t, p.Clip.Duration));
                float fade = (t - p.Clip.Duration - Hold) / Fade;
                if (fade >= 1f) Release(i);
                else if (fade > 0f) p.Renderer.color = new Color(1f, 1f, 1f, 1f - fade);
            }
        }

        private void Release(int index) {
            SpriteRenderer r = _playing[index].Renderer;
            _playing.RemoveAt(index);
            if (r == null) return;
            r.gameObject.SetActive(false);
            _free.Push(r);
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
