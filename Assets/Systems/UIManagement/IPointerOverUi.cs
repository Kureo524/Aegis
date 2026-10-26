using UnityEngine;

namespace Systems.UIManagement {
    /// <summary>
    /// Is the pointer over an interactive UI element? Asked by the gameplay so a click on the HUD
    /// is not also an order; implemented by the presentation layer (Features.UI.UiPicking), so the
    /// gameplay does not depend on it. Root scope service.
    /// </summary>
    public interface IPointerOverUi {
        bool IsPointerOverUi(Vector2 screenPosition);
    }
}
