using System;
using Systems.UIManagement;

namespace Systems.EventBusses {
    public class UIEventBus {
        public static event Action<OpenClosePanel> OnPanelTryOpen;
        public static event Action<OpenClosePanel> OnPanelTryClose; 
    }
}
