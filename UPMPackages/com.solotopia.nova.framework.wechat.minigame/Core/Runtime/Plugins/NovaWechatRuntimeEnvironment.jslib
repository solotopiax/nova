// modify: local fork - 区分普通浏览器 WebGL 与微信小游戏容器。
mergeInto(LibraryManager.library, {
    NovaWechatIsMiniGame: function () {
        return typeof wx !== 'undefined' || typeof GameGlobal !== 'undefined' ? 1 : 0;
    }
});
