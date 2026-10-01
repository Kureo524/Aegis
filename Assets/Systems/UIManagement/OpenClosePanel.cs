using System;
using NUnit.Framework;
using UnityEngine;

public class OpenClosePanel : MonoBehaviour {
    protected CanvasGroup CanvasGroup { get; set; }

    private void Start() {
        if (TryGetComponent<CanvasGroup>(out var group)) CanvasGroup = group;
        Assert.IsNotNull(CanvasGroup);
    }

#pragma warning disable CS1998
    public async virtual Awaitable OpenPanel() { }
    public async virtual Awaitable ClosePanel() { }
#pragma warning restore CS1998
}
