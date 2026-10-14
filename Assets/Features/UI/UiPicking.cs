using UnityEngine;
using UnityEngine.UIElements;

namespace Features.UI {
    /// <summary>
    /// Is the pointer over the UI (UI Toolkit)? Gameplay clicks under an interactive element (a
    /// button, the shop, a panel) are ignored so they do not also move the character or cast.
    /// Pure helper: picks in every active UIDocument; roots and elements set to PickingMode.Ignore
    /// (the HUD's empty areas) let the click through.
    /// </summary>
    public static class UiPicking {
        public static bool IsPointerOverUi(Vector2 screenPosition) {
            foreach (UIDocument document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) {
                if (!document.isActiveAndEnabled) continue;
                VisualElement root = document.rootVisualElement;
                IPanel panel = root?.panel;
                if (panel == null) continue;
                Vector2 point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
                VisualElement picked = panel.Pick(point);
                if (picked != null && picked != root) return true;
            }
            return false;
        }
    }
}
