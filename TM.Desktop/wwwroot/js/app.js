"use strict";
(() => {
    let unappliedEdits = false;
    const clearSecretsLater = () => {
        document.querySelectorAll("[data-secret]").forEach(element => {
            window.setTimeout(() => { element.textContent = "Hidden"; }, 20000);
        });
    };
    document.addEventListener("input", event => {
        if (event.target.closest("[data-editor]")) unappliedEdits = true;
    });
    document.addEventListener("submit", event => {
        if (unappliedEdits && event.target.closest("#workspace") && !event.target.matches("[data-editor]")) {
            if (!window.confirm("Discard edits that have not been applied to the document?")) {
                event.preventDefault();
                event.stopImmediatePropagation();
            }
        }
    }, true);
    document.addEventListener("htmx:afterSwap", event => {
        if (event.detail.target.id === "workspace") {
            unappliedEdits = false;
            clearSecretsLater();
        }
    });
    document.addEventListener("keydown", event => {
        if (!(event.ctrlKey || event.metaKey) || event.altKey) return;
        if (event.target.matches("input, textarea, select, [contenteditable='true']")) return;
        const command = {
            s: "save", l: "lock", e: "expand", r: "collapse", f: "focus-selected",
            h: "sort-name", d: "sort-date", t: "timestamp"
        }[event.key.toLowerCase()];
        if (!command) return;
        event.preventDefault();
        if (unappliedEdits) {
            window.alert("Apply edits before saving or locking the document.");
            return;
        }
        document.querySelector(`[data-command="${command}"]`)?.click();
    });
    document.addEventListener("click", event => {
        const link = event.target.closest("a[href]");
        if (link && new URL(link.href, location.href).origin !== location.origin) event.preventDefault();
    });
    window.addEventListener("beforeunload", event => {
        if (unappliedEdits || document.querySelector("#workspace")?.dataset.dirty === "true") event.preventDefault();
    });
    clearSecretsLater();
})();
