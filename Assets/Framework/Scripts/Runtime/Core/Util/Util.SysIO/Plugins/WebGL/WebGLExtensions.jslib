mergeInto(LibraryManager.library, {

  $NovaSyncFsState: {
    isSyncing: false,
    hasPendingSync: false,
    hasWarnedUnavailable: false,

    complete: function (callback, err) {
      NovaSyncFsState.isSyncing = false;
      if (err) {
        console.error('[Nova][SysIO] FS.syncfs failed:', err);
      }
      Module.dynCall_vi(callback, err ? 1 : 0);

      if (NovaSyncFsState.hasPendingSync) {
        NovaSyncFsState.hasPendingSync = false;
        NovaSyncFsState.request(callback);
      }
    },

    request: function (callback) {
      if (NovaSyncFsState.isSyncing) {
        NovaSyncFsState.hasPendingSync = true;
        return;
      }

      NovaSyncFsState.isSyncing = true;
      if (typeof indexedDB === 'undefined') {
        if (!NovaSyncFsState.hasWarnedUnavailable) {
          NovaSyncFsState.hasWarnedUnavailable = true;
          console.warn('[Nova][SysIO] IndexedDB is unavailable; file changes are kept for this session only.');
        }
        NovaSyncFsState.complete(callback, null);
        return;
      }

      try {
        FS.syncfs(false, function (err) {
          NovaSyncFsState.complete(callback, err);
        });
      } catch (err) {
        NovaSyncFsState.complete(callback, err);
      }
    }
  },

  SyncFs__deps: ['$NovaSyncFsState'],
  SyncFs: function (callback) {
    NovaSyncFsState.request(callback);
  },

  OpenURL: function (url, target) {
    window.open(UTF8ToString(url), UTF8ToString(target));
  }

});
