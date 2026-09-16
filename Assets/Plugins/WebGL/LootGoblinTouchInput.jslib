mergeInto(LibraryManager.library, {
  LootGoblinDisableBrowserTouchGestures: function () {
    var canvas = Module.canvas;
    if (!canvas || canvas.dataset.lootGoblinTouchReady) return;

    canvas.dataset.lootGoblinTouchReady = "true";
    window.lootGoblinTouchReleasePending = false;
    canvas.style.touchAction = "none";
    canvas.style.webkitUserSelect = "none";
    canvas.style.userSelect = "none";
    canvas.addEventListener("touchmove", function (event) { event.preventDefault(); }, { passive: false });
    window.addEventListener("touchstart", function () { window.lootGoblinTouchReleasePending = false; }, true);
    window.addEventListener("touchend", function () { window.lootGoblinTouchReleasePending = true; }, true);
    window.addEventListener("touchcancel", function () { window.lootGoblinTouchReleasePending = true; }, true);
    window.addEventListener("blur", function () { window.lootGoblinTouchReleasePending = true; });
  },

  LootGoblinConsumeBrowserTouchRelease: function () {
    if (!window.lootGoblinTouchReleasePending) return 0;
    window.lootGoblinTouchReleasePending = false;
    return 1;
  }
});
