using System.Collections;
using UnityEngine;

namespace Features.Characters {
    /// <summary>Revives its character after a delay (training dummy). Same-prefab component.</summary>
    public class RespawnOnDeath : MonoBehaviour {
        [SerializeField] private Character character;
        [SerializeField] private float delay = 2f;

        private void OnEnable() => character.Died += OnDied;
        private void OnDisable() => character.Died -= OnDied;

        private void OnDied(Character _) => StartCoroutine(ReviveLater());

        private IEnumerator ReviveLater() {
            yield return new WaitForSeconds(delay);
            character.Revive();
        }
    }
}
