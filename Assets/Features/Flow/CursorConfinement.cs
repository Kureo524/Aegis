using System;
using UnityEngine;
using VContainer.Unity;

namespace Features.Flow {
    /// <summary>
    /// Confines the cursor to the window in standalone builds (needed for edge scrolling).
    ///
    /// Build-only on purpose, never in the Editor: Cursor.lockState is process-global, and on
    /// Wayland/Hyprland a Confined lock leaks out of the Game view and desyncs the pointer for the
    /// whole Editor (clicks land away from the visible cursor). Do not remove the guard.
    ///
    /// The compositor drops the confinement whenever the window loses focus (workspace switch,
    /// alt-tab), so it is re-applied every frame the window is focused.
    /// </summary>
    public sealed class CursorConfinement : ITickable, IDisposable {
        public void Tick() {
#if !UNITY_EDITOR
            if (Application.isFocused && Cursor.lockState != CursorLockMode.Confined) {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
#endif
        }

        public void Dispose() {
#if !UNITY_EDITOR
            Cursor.lockState = CursorLockMode.None;
#endif
        }
    }
}
