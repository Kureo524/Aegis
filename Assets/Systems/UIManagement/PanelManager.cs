using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Systems.UIManagement {
    public interface IPanelManager {
        /// <summary>
        /// Queues an open (<paramref name="toOpen"/> = true) or close request. Requests run one at a
        /// time, in order. Resolves to false if the request is redundant or the panel threw.
        /// </summary>
        Task<bool> TryOpenOrClosePanelAsync(OpenClosePanel panel, bool toOpen, bool inFront = false);
    }

    /// <summary>Serializes panel animations so two panels never transition at the same time.
    /// Registered as a singleton in the RootLifetimeScope (no static state).</summary>
    public sealed class PanelManager : IPanelManager {
        private readonly List<OpenClosePanel> _openPanels = new();
        private readonly List<(OpenClosePanel panel, bool inFront, bool toOpen, TaskCompletionSource<bool> done)> _queue = new();

        private bool _isBusy;

        public Task<bool> TryOpenOrClosePanelAsync(OpenClosePanel panel, bool toOpen, bool inFront = false) {
            if (panel == null || !IsPoolable(panel, toOpen)) return Task.FromResult(false);

            var done = new TaskCompletionSource<bool>();
            _queue.Add((panel, inFront, toOpen, done));

            if (!_isBusy) _ = HandlePanelRoutine();

            return done.Task;
        }

        private async Awaitable HandlePanelRoutine() {
            _isBusy = true;
            try {
                while (_queue.Count > 0) {
                    var data = _queue[0];
                    bool success = false;

                    try {
                        await (data.toOpen ? data.panel.OpenPanel() : data.panel.ClosePanel());

                        if (data.toOpen) _openPanels.Insert(data.inFront ? 0 : _openPanels.Count, data.panel);
                        else _openPanels.Remove(data.panel);

                        success = true;
                    }
                    catch (System.Exception e) {
                        Debug.LogException(e);
                    }
                    finally {
                        _queue.RemoveAt(0);
                        data.done.TrySetResult(success);
                    }
                }
            }
            finally {
                _isBusy = false;
            }
        }

        // The last queued request for a panel wins; otherwise compare with the current open state.
        private bool IsPoolable(OpenClosePanel panel, bool toOpen) {
            for (int i = _queue.Count - 1; i >= 0; i--) {
                if (_queue[i].panel == panel) return toOpen != _queue[i].toOpen;
            }
            return _openPanels.Contains(panel) != toOpen;
        }
    }
}
