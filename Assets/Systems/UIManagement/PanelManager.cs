using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Systems.UIManagement {
    public static class PanelManager {
        private static readonly List<OpenClosePanel> OpenPanels = new();
        private static readonly List<(OpenClosePanel panel, bool inFront, bool toOpen, TaskCompletionSource<bool> done)> BusyPool = new();
        
        private static bool _isBusy;

        public static Task<bool> TryOpenOrClosePanelAsync(OpenClosePanel panel, bool inFront = false, bool toOpen = false) {
            if (!IsPoolable(panel, toOpen)) return Task.FromResult(false);

            var done = new TaskCompletionSource<bool>();
            BusyPool.Add((panel, inFront, toOpen, done));

            if (!_isBusy) _ = HandlePanelRoutine();

            return done.Task;
        }

        private static async Awaitable HandlePanelRoutine() {
            _isBusy = true;
            try {
                while (BusyPool.Count > 0) {
                    var data = BusyPool[0];
                    bool success = false;

                    try {
                        await (data.toOpen ? data.panel.OpenPanel() : data.panel.ClosePanel());

                        if (data.toOpen) AddPanel(data.panel, data.inFront);
                        else RemovePanel(data.panel);

                        success = true;
                    }
                    catch (System.Exception e) {
                        Debug.LogException(e);
                    }
                    finally {
                        BusyPool.RemoveAt(0);
                        data.done.TrySetResult(success);
                    }
                }
            }
            finally {
                _isBusy = false;
            }
        }

        private static void AddPanel(OpenClosePanel panel, bool inFront) =>
            OpenPanels.Insert(inFront ? 0 : OpenPanels.Count, panel);

        private static void RemovePanel(OpenClosePanel panel) => OpenPanels.Remove(panel);

        private static bool IsPoolable(OpenClosePanel panelToCheck, bool doOpen) {
            for (int i = BusyPool.Count - 1; i >= 0; i--) {
                if (!BusyPool[i].panel.Equals(panelToCheck)) continue;
                return doOpen != BusyPool[i].toOpen;
            }
            return OpenPanels.Contains(panelToCheck) != doOpen;
        }
    }
}