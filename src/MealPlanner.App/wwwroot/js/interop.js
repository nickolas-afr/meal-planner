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

    const b64uToBuf = s => {
        const b = s.replace(/-/g, '+').replace(/_/g, '/');
        const bin = atob(b + '='.repeat((4 - b.length % 4) % 4));
        const out = new Uint8Array(bin.length);
        for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
        return out.buffer;
    };
    const bufToB64u = buf => {
        if (buf == null) return null;
        const bytes = new Uint8Array(buf);
        let bin = '';
        for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
        return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    };
    // Rethrows keeping the DOMException name in the message so .NET's JSException shows it.
    async function ceremony(fn) {
        try { return await fn(); }
        catch (e) {
            const err = new Error((e && e.name ? e.name : 'Error') + ': ' + (e && e.message ? e.message : ''));
            err.name = e && e.name ? e.name : 'Error';
            throw err;
        }
    }
    const webauthn = {
        // Returns the credential as JSON (base64url binary fields).
        create(optionsJson) {
            return ceremony(async () => {
                const o = JSON.parse(optionsJson);
                o.challenge = b64uToBuf(o.challenge);
                o.user.id = b64uToBuf(o.user.id);
                (o.excludeCredentials || []).forEach(c => { c.id = b64uToBuf(c.id); });
                const cred = await navigator.credentials.create({ publicKey: o });
                if (!cred) throw new DOMException('No credential', 'NotAllowedError');
                const r = cred.response;
                const response = { clientDataJSON: bufToB64u(r.clientDataJSON), attestationObject: bufToB64u(r.attestationObject) };
                if (typeof r.getTransports === 'function') response.transports = r.getTransports();
                return JSON.stringify({ id: cred.id, rawId: bufToB64u(cred.rawId), type: cred.type, response, clientExtensionResults: {} });
            });
        },
        get(optionsJson) {
            return ceremony(async () => {
                const o = JSON.parse(optionsJson);
                o.challenge = b64uToBuf(o.challenge);
                (o.allowCredentials || []).forEach(c => { c.id = b64uToBuf(c.id); });
                const cred = await navigator.credentials.get({ publicKey: o, mediation: 'optional' });
                if (!cred) throw new DOMException('No credential', 'NotAllowedError');
                const r = cred.response;
                return JSON.stringify({
                    id: cred.id, rawId: bufToB64u(cred.rawId), type: cred.type,
                    response: {
                        clientDataJSON: bufToB64u(r.clientDataJSON),
                        authenticatorData: bufToB64u(r.authenticatorData),
                        signature: bufToB64u(r.signature),
                        userHandle: bufToB64u(r.userHandle)
                    },
                    clientExtensionResults: {}
                });
            });
        }
    };

    const visibility = {
        isVisible() { return document.visibilityState === 'visible'; },
        // dotnetRef.OnVisibilityChanged(bool visible)
        onVisibilityChange(dotnetRef) {
            document.addEventListener('visibilitychange', () => {
                try { dotnetRef.invokeMethodAsync('OnVisibilityChanged', document.visibilityState === 'visible'); } catch (e) { console.warn(e); }
            });
        }
    };

    const extras = {
        webauthn,
        visibility,
        isOnline() { return navigator.onLine; },
        onConnectivityChange(dotnetRef) {
            // Report the event's own type: it is what changed, and navigator.onLine can lag behind it.
            const notify = (e) => { try { dotnetRef.invokeMethodAsync('OnConnectivityChanged', e.type === 'online'); } catch (err) { console.warn(err); } };
            window.addEventListener('online', notify);
            window.addEventListener('offline', notify);
        },
        async share(text) {
            if (!navigator.share) return false;
            try { await navigator.share({ text }); return true; } catch (e) { return false; }
        },
        async copy(text) {
            try { await navigator.clipboard.writeText(text); return true; } catch (e) { return false; }
        }
    };

    window.mealPlanner = Object.assign(api, { sortable, sw, downloadFile }, extras);
})();
