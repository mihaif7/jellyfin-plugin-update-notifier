'use strict';

/*
 * Adds the avatar badge and the profile-menu entry.
 *
 * Jellyfin 12's toolbar is React/MUI. The avatar button carries
 * aria-controls="app-user-menu" and the dropdown is a keepMounted MUI Menu with
 * id="app-user-menu"; both are stable, unlike the build-generated emotion class
 * names, which is why the menu entry clones its classes from a real MenuItem.
 *
 * The injector serves this script to every signed-in session. Admins poll the
 * summary; any other user asks once, and a 403 (not on the plugin's Settings
 * list) keeps them quiet until PROBE_MS later, so the admin's choice lands
 * without a reload.
 */
(function () {
    var MENU_ID = 'app-user-menu';
    var BUTTON_SELECTOR = 'button[aria-controls="' + MENU_ID + '"]';
    var PAGE_HREF = '#/configurationpage?name=UpdateNotifier';
    var DASHBOARD_SELECTOR = 'a[href$="/dashboard"]';
    var SETTINGS_SELECTOR = 'a[href$="/mypreferencesmenu"]';
    var BADGE_CLASS = 'pluginUpdateNotifierBadge';
    var ITEM_CLASS = 'pluginUpdateNotifierItem';
    var POPUP_CLASS = 'pluginUpdateNotifierPopup';
    var NOTICE_HINT = 'Sign in with an administrator account to restart the server and apply these plugin changes.';

    // A restart pending "at some point" does not need catching sooner.
    var POLL_MS = 300000;
    var RETRY_MS = 60000;
    var WAIT_MS = 5000;
    var IDLE_MS = 60000;
    var MENU_MS = 10000;
    var PROBE_MS = 1800000;

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
    var notified = false;
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
            'display:inline-block;box-shadow:none;vertical-align:middle}' +
            // A non-admin's entry leads nowhere, so it drops the hover
            // highlight and shows a help cursor; hovering it explains why.
            // !important on both: themes force their own cursor on menu items.
            '.' + ITEM_CLASS + '[aria-disabled="true"],.' + ITEM_CLASS + '[aria-disabled="true"] *{cursor:help !important}' +
            '.' + ITEM_CLASS + '[aria-disabled="true"]:hover{background-color:transparent !important}' +
            // Above MUI's popover layer (1300), which holds the menu.
            '.' + POPUP_CLASS + '{position:fixed;z-index:1500;max-width:240px;padding:8px 12px;' +
            'border-radius:6px;background:rgba(32,32,32,.97);color:#fff;font-size:.8rem;line-height:1.45;' +
            'box-shadow:0 6px 18px rgba(0,0,0,.45);pointer-events:none}';
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
        // Non-admins cannot open the dashboard page. aria-disabled also makes
        // MUI's keyboard navigation skip the entry.
        if (admin) {
            item.href = PAGE_HREF;
        } else {
            item.setAttribute('aria-disabled', 'true');
        }

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

        if (admin) {
            // Closing the menu is the host's job; clicking the backdrop does it.
            item.addEventListener('click', function () {
                var backdrop = document.querySelector('#' + MENU_ID + ' .MuiBackdrop-root');
                if (backdrop) backdrop.click();
            });
            return item;
        }

        item.setAttribute('aria-description', NOTICE_HINT);
        item.addEventListener('mouseenter', function () { showPopup(item); });
        item.addEventListener('mouseleave', hidePopup);
        // Touch screens have no hover: a tap shows it instead, and a tap
        // anywhere else hides it. Not a toggle, since a tap also fires the
        // mouseenter above and would close what it just opened.
        item.addEventListener('click', function (e) {
            e.preventDefault();
            showPopup(item);
        });

        return item;
    }

    var popup = null;
    var popupAnchor = null;

    // Rendered on the body rather than inside the menu, whose paper clips
    // its overflow.
    function showPopup(anchor) {
        if (!popup) {
            popup = document.createElement('div');
            popup.className = POPUP_CLASS;
            popup.setAttribute('aria-hidden', 'true');
            popup.textContent = NOTICE_HINT;
        }
        // Measured at the origin: left where it was last shown, it would
        // shrink to fit the space remaining there.
        popup.style.left = '0px';
        popup.style.top = '0px';
        document.body.appendChild(popup);
        popupAnchor = anchor;

        // Centred below the entry, kept inside the viewport.
        var rect = anchor.getBoundingClientRect();
        var left = rect.left + (rect.width - popup.offsetWidth) / 2;
        if (left + popup.offsetWidth > window.innerWidth - 8) left = window.innerWidth - 8 - popup.offsetWidth;
        if (left < 8) left = 8;
        var top = rect.bottom + 4;
        if (top + popup.offsetHeight > window.innerHeight - 8) {
            top = rect.top - popup.offsetHeight - 4;
        }
        popup.style.left = left + 'px';
        popup.style.top = top + 'px';
    }

    function hidePopup() {
        popupAnchor = null;
        if (popup && popup.parentNode) popup.parentNode.removeChild(popup);
    }

    function syncMenuItem() {
        var menu = document.getElementById(MENU_ID);
        if (!menu) return;

        var existing = menu.querySelector('.' + ITEM_CLASS);

        // Built for the other kind of user: a switch between admin and
        // non-admin needs the other anchor, link and state.
        if (existing && existing.hasAttribute('href') !== !!admin) {
            existing.remove();
            existing = null;
        }

        if (!pending) {
            if (existing) existing.remove();
            hidePopup();
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

        // Admins get it above Dashboard, which only renders for them; anyone
        // else below Settings. Either entry also supplies the MUI classes.
        var anchor = menu.querySelector(admin ? DASHBOARD_SELECTOR : SETTINGS_SELECTOR);
        if (!anchor) return;

        injectStyle();
        anchor.parentNode.insertBefore(buildItem(anchor), admin ? anchor : anchor.nextSibling);
    }

    function sync() {
        // React can drop the entry while its popup is up, and a removed
        // node never fires mouseleave.
        if (popupAnchor && !document.body.contains(popupAnchor)) hidePopup();
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
            // Signing out navigates, which lands here while a badge is up.
            if (switched()) {
                recheck();
                return;
            }
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

    // Sign-in, sign-out and user switches happen without a page load.
    function switched() {
        var client = api();
        return (client ? signedInId(client) : null) !== userId;
    }

    function recheck() {
        if (timer) clearTimeout(timer);
        tick();
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
        var askedFor = userId;
        lastFetch = Date.now();
        nextDue = lastFetch + POLL_MS;
        // summary rather than status: the badge needs a flag and a number, and
        // status carries every plugin's changelog in full alongside them.
        client.getJSON(client.getUrl('PluginUpdateNotifier/summary'))
            .then(function (summary) {
                // The answer belongs to a user who has since signed out.
                if (userId !== askedFor) return;
                notified = true;
                var n = (summary && summary.Count) || 0;
                apply(
                    !!(summary && summary.PendingRestart) && n > 0 && !summary.Dismissed,
                    n);
                scheduleNext();
            })
            .catch(function (err) {
                if (userId !== askedFor) return;
                notified = false;
                apply(false, 0);
                var denied = err && (err.status === 401 || err.status === 403);
                // Not on the list: ask again much later. An admin denied has
                // likely been demoted, so re-resolve rather than retry.
                if (denied && !admin) {
                    nextDue = Date.now() + PROBE_MS;
                } else {
                    if (denied) admin = null;
                    nextDue = Date.now() + RETRY_MS;
                }
                scheduleNext();
            });
    }

    function tick() {
        timer = null;

        var client = api();
        var id = client ? signedInId(client) : null;

        if (id !== userId) {
            userId = id;
            admin = null;
            notified = false;
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
                if (userId === id) {
                    admin = !!(user && user.Policy && user.Policy.IsAdministrator);
                }
                tick();
            }, function () {
                resolving = false;
                schedule(RETRY_MS);
            });
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

        // Back or a shortcut can navigate while the menu is still open.
        window.addEventListener('hashchange', hidePopup);

        document.addEventListener('click', function (e) {
            var t = e.target;
            // A tap outside the entry, the backdrop included, closes the popup.
            if (!t || !t.closest || !t.closest('.' + ITEM_CLASS)) hidePopup();
            if (!t || !t.closest || !t.closest(BUTTON_SELECTOR)) return;
            // The menu is about to open: it must not show the last user's entry.
            if (switched()) {
                recheck();
                return;
            }
            if (!notified) return;
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
