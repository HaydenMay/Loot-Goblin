mergeInto(LibraryManager.library, {
  LootGoblinDisableBrowserTouchGestures: function () {
    var canvas = Module.canvas;
    if (!canvas || canvas.dataset.lootGoblinTouchReady) return;

    canvas.dataset.lootGoblinTouchReady = "true";
    canvas.style.touchAction = "none";
    canvas.style.webkitUserSelect = "none";
    canvas.style.userSelect = "none";
    canvas.addEventListener("touchmove", function (event) { event.preventDefault(); }, { passive: false });
  }
});
