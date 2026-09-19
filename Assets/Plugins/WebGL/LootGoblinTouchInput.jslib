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
    canvas.addEventListener("pointerdown", function (event) {
      window.lootGoblinTouchReleasePending = false;
      if (canvas.setPointerCapture) canvas.setPointerCapture(event.pointerId);
    }, true);
    var releaseJoystick = function () {
      window.lootGoblinTouchReleasePending = true;
    };
    canvas.addEventListener("pointerup", releaseJoystick, true);
    canvas.addEventListener("pointercancel", releaseJoystick, true);
    canvas.addEventListener("lostpointercapture", releaseJoystick, true);
    window.addEventListener("touchend", releaseJoystick, true);
    window.addEventListener("touchcancel", releaseJoystick, true);
    window.addEventListener("blur", releaseJoystick);
    document.addEventListener("visibilitychange", function () {
      if (document.hidden) releaseJoystick();
    });
  },

  LootGoblinConsumeBrowserTouchRelease: function () {
    if (!window.lootGoblinTouchReleasePending) return 0;
    window.lootGoblinTouchReleasePending = false;
    return 1;
  }
});
