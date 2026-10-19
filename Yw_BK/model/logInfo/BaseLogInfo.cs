#region 功能修改描述與時間

/*
    描述：入庫日誌紀錄基底類別
    日期：2024-07-15
*/

#endregion

using YwCore;
using YwCore.model.interfaces;

namespace Yw_BK.model.logInfo
{
    /// <summary>
    /// 入庫日誌紀錄基底類別
    /// </summary>
    internal abstract class BaseLogInfo : ILogInfo
    {
        /// <summary>
        /// GPTYPE
        /// </summary>
        public string GpType { get; }
        /// <summary>
        /// 日誌模式
        /// </summary>
        public LogMode LogMode { get; protected set; }
        /// <summary>
        /// 來源
        /// </summary>
        public Source Source { get; }
        /// <summary>
        /// 備註
        /// </summary>
        public string Remark { get; }
        /// <summary>
        /// 平台入口
        /// </summary>
        public Entrance Entrance { get; }
        /// <summary>
        /// 耗時
        /// </summary>
        public TimeSpan? ExecTime { get; }

        /// <summary>
        /// 查詢KMode日誌
        /// </summary>
        /// <param name="source">來源</param>
        /// <param name="gpType">GPTYPE</param>
        /// <param name="sendData">送出數據</param>
        /// <param name="resultData">回傳數據</param>
        /// <param name="execTime">動作耗時</param>
        /// <param name="entrance">平台入口</param>
        /// <param name="remark">備註</param>
        public BaseLogInfo(Source source, string gpType, TimeSpan? execTime = default, Entrance entrance = Entrance.None, string remark = "")
        {
            Source = source;
            GpType = gpType;
            Entrance = entrance;
            Remark = remark;
            ExecTime = execTime;
        }

        /// <summary>
        /// 輸出日誌內容
        /// </summary>
        /// <returns></returns>
        public abstract string OutputLog();
    }
}
