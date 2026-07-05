// Small named interop helpers invoked from Blazor via IJSRuntime — replaces an inline
// JS.InvokeVoidAsync("eval", ...) call (audit P2-15). No dynamic eval.
window.medassist = {
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    }
};
