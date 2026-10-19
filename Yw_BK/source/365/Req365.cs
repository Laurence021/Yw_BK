#region 功能修改描述與時間

/*
    描述：365數據接收分配處理
    日期：2022-08-10
*/

#endregion

using Yw_BK.model;
using YwCore;
using YwCore.source._365.interfaces;

namespace Yw_BK.source
{
    /// <summary>
    /// 365來源數據分配處理
    /// </summary>
    public class Req365 : IProcessZdData
    {
        /// <summary>
        /// 數據處理
        /// </summary>
        /// <param name="data">數據</param>
        /// <param name="sport">球類</param>
        public void ProcessData(string data, Common365.Sport365 sport)
        {
            Config.zdQueue.Enqueue(new DataInfo(GpType.Zd.ToString(), data, 0, 0, sport.ToString("g"), true));
        }
    }
}
