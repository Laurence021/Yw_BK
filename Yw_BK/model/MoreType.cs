#region 功能修改描述與時間

/*
    描述：更多請求使用type(HG內頁使用)
    建立時間：2018-11-02
*/

#endregion

using System.Collections.Concurrent;

namespace Yw_BK.model
{
    /// <summary>
    /// 內頁請求型態
    /// </summary>
    public class MoreType
    {
        /// <summary>
        /// 來源主賽事Id佇列
        /// </summary>
        public ConcurrentQueue<decimal> MoreQueue { get; set; } = new ConcurrentQueue<decimal>();
        /// <summary>
        /// 是否開始
        /// </summary>
        public bool IsStart { get; set; }
        /// <summary>
        /// GP類型
        /// </summary>
        public GpType GpType { get; set; }
        /// <summary>
        /// 蒐集所有內頁請求物件
        /// </summary>
        public List<IAsyncResult> WaitHandles { get; set; } = new List<IAsyncResult>();


        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="gpType">GP類型</param>
        public MoreType(GpType gpType)
        {
            GpType = gpType;
        }
    }
}