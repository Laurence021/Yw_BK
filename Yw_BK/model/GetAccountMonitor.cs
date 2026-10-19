using YwCore.model;
using YwCore.model.interfaces;

namespace Yw_BK.model
{
    /// <summary>
    /// 帳號監控功能
    /// </summary>
    internal class GetAccountMonitor : IGetAccountMonitor
    {
        public bool IsEnabled => Config.IsLogin && (Config.isOpenZd || Config.isOpenDs || Config.isOpenZp) && AccountMonitor != null;

        public AccountMonitor AccountMonitor => Config.AccountMonitor;
    }
}
