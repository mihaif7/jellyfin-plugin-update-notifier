'use strict';

/*
 * Adds the avatar badge and the profile-menu entry.
 *
 * Jellyfin 12's toolbar is React/MUI. The avatar button carries
 * aria-controls="app-user-menu" and the dropdown is a keepMounted MUI Menu with
 * id="app-user-menu"; both are stable, unlike the build-generated emotion class
 * names, which is why the menu entry clones its classes from a real MenuItem.
 */
(function () {
    var POLL_MS = 60000;
    var MENU_ID = 'app-user-menu';
    var BUTTON_SELECTOR = 'button[aria-controls="' + MENU_ID + '"]';
    var PAGE_HREF = '#/configurationpage?name=UpdateNotifier';
    var BADGE_CLASS = 'pluginUpdateNotifierBadge';
    var ITEM_CLASS = 'pluginUpdateNotifierItem';

    var pending = false;
    var count = 0;
    var styleInjected = false;
    var observer = null;
    var scheduled = false;

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

    function refresh() {
        if (typeof ApiClient === 'undefined' || !ApiClient) return;
        ApiClient.getJSON(ApiClient.getUrl('PluginUpdateNotifier/status'))
            .then(function (status) {
                var updates = (status && status.Updates) || [];
                count = updates.length;
                pending = !!(status && status.PendingRestart) && count > 0 && !status.Dismissed;
                sync();
            })
            .catch(function () {
                // Non-admins get 403 from the elevated endpoint; stay silent.
                pending = false;
                count = 0;
                sync();
            });
    }

    function start() {
        refresh();
        setInterval(refresh, POLL_MS);

        // The poll alone goes stale between ticks, and hidden tabs throttle it
        // further, so re-check at the moments the count is actually read.
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) refresh();
        });
        document.addEventListener('click', function (e) {
            var t = e.target;
            if (t && t.closest && t.closest(BUTTON_SELECTOR)) refresh();
        }, true);

        // React re-renders the toolbar on navigation and drops injected nodes.
        observer = new MutationObserver(scheduleSync);
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
