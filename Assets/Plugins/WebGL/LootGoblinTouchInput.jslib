mergeInto(LibraryManager.library, {
  LootGoblinDisableBrowserTouchGestures: function () {
    var canvas = Module.canvas;
    if (!canvas || canvas.dataset.lootGoblinTouchReady) return;

    canvas.dataset.lootGoblinTouchReady = "true";
    canvas.style.touchAction = "none";
    canvas.style.webkitUserSelect = "none";
    canvas.style.userSelect = "none";
    canvas.addEventListener("touchmove", function (event) { event.preventDefault(); }, { passive: false });
    canvas.addEventListener("pointerdown", function (event) {
      if (canvas.setPointerCapture) canvas.setPointerCapture(event.pointerId);
    }, true);
    var releaseJoystick = function () {
      if (typeof SendMessage === "function") SendMessage("LOOT GOBLIN - Playable Prototype", "ReleaseFromBrowser");
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
  }
});
