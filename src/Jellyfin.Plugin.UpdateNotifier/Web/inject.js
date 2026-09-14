'use strict';

/*
 * Adds the avatar badge and the profile-menu entry.
 *
 * Jellyfin 12's toolbar is React/MUI. The avatar button carries
 * aria-controls="app-user-menu" and the dropdown is a keepMounted MUI Menu with
 * id="app-user-menu"; both are stable, unlike the build-generated emotion class
 * names, which is why the menu entry clones its classes from a real MenuItem.
 *
 * The injector serves this script to every signed-in session, so nothing here
 * touches the network or the DOM until the session is known to be an admin's.
 */
(function () {
    var MENU_ID = 'app-user-menu';
    var BUTTON_SELECTOR = 'button[aria-controls="' + MENU_ID + '"]';
    var PAGE_HREF = '#/configurationpage?name=UpdateNotifier';
    var BADGE_CLASS = 'pluginUpdateNotifierBadge';
    var ITEM_CLASS = 'pluginUpdateNotifierItem';

    // A restart pending "at some point" does not need catching sooner.
    var POLL_MS = 300000;
    var RETRY_MS = 60000;
    var WAIT_MS = 5000;
    var IDLE_MS = 60000;
    var MENU_MS = 10000;

    var pending = false;
    var count = 0;
    var styleInjected = false;
    var observer = null;
    var scheduled = false;

    var timer = null;
    var lastFetch = 0;
    var nextDue = 0;
    var userId = null;
    var admin = null;
    var resolving = false;

    function injectStyle() {
        if (styleInjected) return;
        styleInjected = true;
        var style = document.createElement('style');
        style.textContent =
            '.' + BADGE_CLASS + '{position:absolute;top:0px;right:0px;width:10px;height:10px;' +
            'border-radius:50%;background:#cc3333;box-shadow:0 0 0 2px rgba(0,0,0,.35);' +
            'pointer-events:none;z-index:1}' +
            '.' + BADGE_CLASS + 'Host{position:relative}' +
            '.' + ITEM_CLASS + ' .' + BADGE_CLASS + '{position:static;margin-left:8px;' +
            'display:inline-block;box-shadow:none;vertical-align:middle}';
        document.head.appendChild(style);
    }

    function itemText() {
        return 'Plugin Updates (' + count + ')';
    }

    function makeBadge() {
        var dot = document.createElement('span');
        dot.className = BADGE_CLASS;
        dot.setAttribute('aria-hidden', 'true');
        return dot;
    }

    function syncBadge() {
        var button = document.querySelector(BUTTON_SELECTOR);
        if (!button) return;
        var dot = button.querySelector('.' + BADGE_CLASS);

        if (pending && !dot) {
            injectStyle();
            button.classList.add(BADGE_CLASS + 'Host');
            button.appendChild(makeBadge());
        } else if (!pending && dot) {
            dot.remove();
            button.classList.remove(BADGE_CLASS + 'Host');
        }
    }

    function buildItem(dashboardLink) {
        var item = document.createElement('a');
        item.className = dashboardLink.className + ' ' + ITEM_CLASS;
        item.setAttribute('role', 'menuitem');
        item.setAttribute('tabindex', '-1');
        item.href = PAGE_HREF;

        var iconWrap = document.createElement('div');
        var srcIcon = dashboardLink.querySelector('.MuiListItemIcon-root');
        if (srcIcon) iconWrap.className = srcIcon.className;
        var icon = document.createElement('span');
        icon.className = 'material-icons';
        icon.setAttribute('aria-hidden', 'true');
        icon.textContent = 'system_update_alt';
        iconWrap.appendChild(icon);

        var textWrap = document.createElement('div');
        var srcText = dashboardLink.querySelector('.MuiListItemText-root');
        if (srcText) textWrap.className = srcText.className;
        var label = document.createElement('span');
        var srcLabel = dashboardLink.querySelector('.MuiListItemText-primary');
        if (srcLabel) label.className = srcLabel.className;
        label.setAttribute('data-pun-label', '');
        label.textContent = itemText();
        textWrap.appendChild(label);

        item.appendChild(iconWrap);
        item.appendChild(textWrap);
        item.appendChild(makeBadge());

        // Closing the menu is the host's job; clicking the backdrop does it.
        item.addEventListener('click', function () {
            var backdrop = document.querySelector('#' + MENU_ID + ' .MuiBackdrop-root');
            if (backdrop) backdrop.click();
        });

        return item;
    }

    function syncMenuItem() {
        var menu = document.getElementById(MENU_ID);
        if (!menu) return;

        var existing = menu.querySelector('.' + ITEM_CLASS);

        if (!pending) {
            if (existing) existing.remove();
            return;
        }

        if (existing) {
            var label = existing.querySelector('[data-pun-label]');
            // An unconditional write is a DOM mutation, which would retrigger
            // the observer that called us.
            if (label && label.textContent !== itemText()) {
                label.textContent = itemText();
            }
            return;
        }

        // Anchoring to the Dashboard entry keeps this out of non-admin menus,
        // since that entry only renders for administrators.
        var dashboardLink = menu.querySelector('a[href$="/dashboard"]');
        if (!dashboardLink) return;

        injectStyle();
        dashboardLink.parentNode.insertBefore(buildItem(dashboardLink), dashboardLink);
    }

    function sync() {
        if (observer) observer.disconnect();
        try {
            syncBadge();
            syncMenuItem();
        } finally {
            if (observer) {
                observer.observe(document.body, { childList: true, subtree: true });
            }
        }
    }

    function scheduleSync() {
        if (scheduled) return;
        scheduled = true;
        // A timer rather than requestAnimationFrame: rAF is suspended in hidden
        // tabs, which would latch this flag and stop syncing until refocus.
        setTimeout(function () {
            scheduled = false;
            sync();
        }, 200);
    }

    // React re-renders the toolbar and drops the injected nodes. A body-wide
    // observer is the costliest thing here, so it runs only while a badge is up.
    function setObserving(on) {
        if (on && !observer) {
            observer = new MutationObserver(scheduleSync);
            observer.observe(document.body, { childList: true, subtree: true });
        } else if (!on && observer) {
            observer.disconnect();
            observer = null;
        }
    }

    function apply(nextPending, nextCount) {
        pending = nextPending;
        count = nextCount;
        setObserving(pending);
        sync();
    }

    function api() {
        return typeof ApiClient !== 'undefined' && ApiClient ? ApiClient : null;
    }

    function signedInId(client) {
        try {
            return (client.getCurrentUserId && client.getCurrentUserId()) || null;
        } catch (e) {
            return null;
        }
    }

    function schedule(ms) {
        if (timer) clearTimeout(timer);
        timer = document.hidden ? null : setTimeout(tick, ms);
    }

    // Capped so a user switch is still noticed while nextDue is far off.
    function scheduleNext() {
        var wait = nextDue - Date.now();
        if (wait < 0) wait = 0;
        schedule(wait < IDLE_MS ? wait : IDLE_MS);
    }

    function refresh(client) {
        lastFetch = Date.now();
        nextDue = lastFetch + POLL_MS;
        // summary rather than status: the badge needs a flag and a number, and
        // status carries every plugin's changelog in full alongside them.
        client.getJSON(client.getUrl('PluginUpdateNotifier/summary'))
            .then(function (summary) {
                var n = (summary && summary.Count) || 0;
                apply(
                    !!(summary && summary.PendingRestart) && n > 0 && !summary.Dismissed,
                    n);
                scheduleNext();
            })
            .catch(function (err) {
                // Elevation failed: re-resolve rather than retry a doomed request.
                if (err && (err.status === 401 || err.status === 403)) admin = null;
                apply(false, 0);
                nextDue = Date.now() + RETRY_MS;
                scheduleNext();
            });
    }

    function tick() {
        timer = null;

        var client = api();
        var id = client ? signedInId(client) : null;

        // Sign-in, sign-out and user switches happen without a page load.
        if (id !== userId) {
            userId = id;
            admin = null;
            lastFetch = 0;
            nextDue = 0;
            apply(false, 0);
        }

        if (!id) {
            schedule(WAIT_MS);
            return;
        }

        if (admin === null) {
            if (resolving) {
                schedule(IDLE_MS);
                return;
            }
            resolving = true;
            client.getCurrentUser().then(function (user) {
                resolving = false;
                admin = !!(user && user.Policy && user.Policy.IsAdministrator);
                tick();
            }, function () {
                resolving = false;
                schedule(RETRY_MS);
            });
            return;
        }

        if (!admin) {
            // Nothing to ask for: the endpoint would answer 403 every time.
            schedule(IDLE_MS);
            return;
        }

        if (nextDue - Date.now() > 0) {
            scheduleNext();
            return;
        }

        refresh(client);
    }

    function start() {
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                if (timer) clearTimeout(timer);
                timer = null;
                return;
            }
            // Resumes the schedule; tick() refetches only if the answer is stale.
            if (!timer) tick();
        });

        document.addEventListener('click', function (e) {
            if (!admin) return;
            var t = e.target;
            if (!t || !t.closest || !t.closest(BUTTON_SELECTOR)) return;
            if (Date.now() - lastFetch < MENU_MS) return;
            var client = api();
            if (client) refresh(client);
        }, true);

        tick();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
