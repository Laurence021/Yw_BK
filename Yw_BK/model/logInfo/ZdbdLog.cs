#region 功能修改描述與時間

/*
    描述：ZdbdLog日誌紀錄
    日期：2024-07-15
*/

#endregion

using Newtonsoft.Json.Linq;
using YwCore;

namespace Yw_BK.model.logInfo
{
    /// <summary>
    /// DataToAPLog日誌紀錄
    /// </summary>
    internal class ZdbdLog : BaseLogInfo
    {
        /// <summary>
        /// 入庫資料
        /// </summary>
        public IEnumerable<object> SendData { get; }


        /// <summary>
        /// 寫入ZDBD日誌
        /// </summary>
        /// <param name="source">來源</param>
        /// <param name="gpType">GPTYPE</param>
        /// <param name="sendData">送出數據</param>
        /// <param name="logMode">紀錄類型</param>
        /// <param name="execTime">動作耗時</param>
        /// <param name="entrance">平台入口</param>
        /// <param name="remark">備註</param>
        public ZdbdLog(Source source, string gpType, IEnumerable<object> sendData, LogMode logMode, TimeSpan? execTime = null, Entrance entrance = Entrance.None, string remark = "") : base(source, gpType, execTime, entrance, remark)
        {
            SendData = sendData;
            LogMode = logMode;
        }

        /// <summary>
        /// 輸出日誌內容
        /// </summary>
        /// <returns></returns>
        public override string OutputLog()
        {
            string message = $"{Remark}{Environment.NewLine}";

            Dictionary<string, object> dic = new Dictionary<string, object>()
            {
                {"耗時", $"{ExecTime?.TotalMilliseconds}ms" },
                {LogMode.ToString(), SendData }
            };

            Dictionary<string, object> root = new Dictionary<string, object>()
            {
                { GpType, dic }
            };

            var json = JObject.FromObject(root);

            string str = $"{message}{json.ToString(Newtonsoft.Json.Formatting.None)}";
            return str;
        }
    }
}
