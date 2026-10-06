using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Systems.UIManagement {
    [RequireComponent(typeof(UIDocument))]
    public abstract class OpenClosePanel : MonoBehaviour {

        private void OnEnable() {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            Assert.IsNotNull(rootElement);
            SetupDocument(rootElement);
        }
        
        protected abstract void SetupDocument(VisualElement document);

#pragma warning disable CS1998
        public virtual async Awaitable OpenPanel() { }
        public virtual async Awaitable ClosePanel() { }
#pragma warning restore CS1998
    }
}
