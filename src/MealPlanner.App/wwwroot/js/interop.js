// JS interop for Blazor components (focus trap, focus, drag-reorder). Loaded before blazor.webassembly.js.
(function () {
    const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const traps = new Map();
    const sortables = new Map();
    let nextId = 1;
    let scrollLocks = 0;

    function focusables(el) {
        return [...el.querySelectorAll(FOCUSABLE)].filter(e => e.offsetParent !== null || e === document.activeElement);
    }

    const api = {
        focus(element) {
            if (element && element.focus) element.focus();
        },

        // Traps Tab inside `element`, moves focus into it, calls dotnetRef.OnEscape on Escape.
        // Returns a handle for releaseFocus.
        trapFocus(element, dotnetRef) {
            const id = nextId++;
            const previous = document.activeElement;
            const onKey = e => {
                if (e.key === 'Escape') {
                    e.stopPropagation();
                    if (dotnetRef) dotnetRef.invokeMethodAsync('OnEscape');
                    return;
                }
                if (e.key !== 'Tab') return;
                const items = focusables(element);
                if (items.length === 0) { e.preventDefault(); element.focus(); return; }
                const first = items[0], last = items[items.length - 1];
                const active = document.activeElement;
                if (e.shiftKey && (active === first || !element.contains(active))) { e.preventDefault(); last.focus(); }
                else if (!e.shiftKey && (active === last || !element.contains(active))) { e.preventDefault(); first.focus(); }
            };
            element.addEventListener('keydown', onKey);
            if (!element.hasAttribute('tabindex')) element.setAttribute('tabindex', '-1');
            if (scrollLocks++ === 0) document.body.style.overflow = 'hidden';
            traps.set(id, { element, previous, onKey, dotnetRef });
            const items = focusables(element);
            (items[0] || element).focus({ preventScroll: true });
            return id;
        },

        // Removes the trap and returns focus to the element that had it before.
        releaseFocus(id) {
            const t = traps.get(id);
            if (!t) return;
            traps.delete(id);
            t.element.removeEventListener('keydown', t.onKey);
            if (--scrollLocks <= 0) { scrollLocks = 0; document.body.style.overflow = ''; }
            if (t.previous && t.previous.isConnected && t.previous.focus) t.previous.focus({ preventScroll: true });
        }
    };

    const sortable = {
        // dotnetRef.OnReorder(string[] ids) is called with the data-id of each child in new order.
        init(element, dotnetRef, handleSelector) {
            if (typeof Sortable === 'undefined') return 0;
            const id = nextId++;
            const instance = new Sortable(element, {
                handle: handleSelector,
                animation: 150,
                onEnd: () => {
                    const ids = [...element.children].map(c => c.dataset.id).filter(Boolean);
                    dotnetRef.invokeMethodAsync('OnReorder', ids);
                }
            });
            sortables.set(id, instance);
            return id;
        },
        destroy(id) {
            const s = sortables.get(id);
            if (s) { s.destroy(); sortables.delete(id); }
        }
    };

    let swReg = null;
    let reloading = false;
    const sw = {
        // Registers the service worker; dotnetRef.OnUpdateAvailable() is called when a new worker is waiting.
        async register(dotnetRef) {
            if (!('serviceWorker' in navigator)) return false;
            try {
                const reg = await navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
                swReg = reg;
                const notify = () => { try { dotnetRef.invokeMethodAsync('OnUpdateAvailable'); } catch (e) { console.warn(e); } };
                if (reg.waiting && navigator.serviceWorker.controller) notify();
                reg.addEventListener('updatefound', () => {
                    const worker = reg.installing;
                    if (!worker) return;
                    worker.addEventListener('statechange', () => {
                        if (worker.state === 'installed' && navigator.serviceWorker.controller) notify();
                    });
                });
                navigator.serviceWorker.addEventListener('controllerchange', () => {
                    if (reloading) return;
                    reloading = true;
                    window.location.reload();
                });
                return true;
            } catch (e) {
                console.warn('Service worker registration failed', e);
                return false;
            }
        },

        // Checks for a new version; true when a worker is installed and waiting.
        async checkForUpdate() {
            if (!swReg) return false;
            try { await swReg.update(); } catch (e) { console.warn(e); }
            return !!(swReg.waiting && navigator.serviceWorker.controller);
        },

        // Activates the waiting worker; the page reloads on controllerchange.
        skipWaiting() {
            if (swReg && swReg.waiting) swReg.waiting.postMessage({ type: 'SKIP_WAITING' });
            else window.location.reload();
        }
    };

    function downloadFile(fileName, contentType, text) {
        const url = URL.createObjectURL(new Blob([text], { type: contentType }));
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.style.display = 'none';
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    window.mealPlanner = Object.assign(api, { sortable, sw, downloadFile });
})();
