#region 功能修改描述與時間

/*
    描述：UpdateGameCacheLog入庫日誌紀錄
    日期：2024-07-15
*/

#endregion

using Gp.Model;
using Newtonsoft.Json.Linq;
using YwCore;

namespace Yw_BK.model.logInfo
{
    /// <summary>
    /// UpdateGameCacheLog入庫日誌紀錄
    /// </summary>
    internal class UpdateGameCacheLog : BaseLogInfo
    {
        /// <summary>
        /// 入庫資料
        /// </summary>
        public IEnumerable<BkGameCacheUpdateDtoV6> SendData { get; }
        /// <summary>
        /// 資料
        /// </summary>
        public IEnumerable<BkGameCacheUpdateResultDto> ResultData { get; }

        /// <summary>
        /// 建立新比賽日誌
        /// </summary>
        /// <param name="source">來源</param>
        /// <param name="gpType">GPTYPE</param>
        /// <param name="sendData">送出數據</param>
        /// <param name="resultData">回傳數據</param>
        /// <param name="execTime">動作耗時</param>
        /// <param name="entrance">平台入口</param>
        /// <param name="remark">備註</param>
        public UpdateGameCacheLog(Source source, string gpType, IEnumerable<BkGameCacheUpdateDtoV6> sendData, IEnumerable<BkGameCacheUpdateResultDto> resultData, TimeSpan? execTime = null, Entrance entrance = Entrance.None, string remark = "") : base(source, gpType, execTime, entrance, remark)
        {
            SendData = sendData;
            ResultData = resultData;
            LogMode = LogMode.Update_Cache_Game;
        }

        /// <summary>
        /// 輸出日誌內容
        /// </summary>
        /// <returns></returns>
        public override string OutputLog()
        {
            string message = $"{Remark}{Environment.NewLine}";

            var result = from a in SendData
                         join b in ResultData on a.Id equals b.Id into c
                         from d in c.DefaultIfEmpty()
                         select new
                         {
                             a.Id,
                             a.GId,
                             a.GameDate,
                             a.PaymentDate,
                             a.QuarterTime,
                             a.MoveMain,
                             Result = d == null ? 0 : 1,
                         };
            Dictionary<string, object> dic = new Dictionary<string, object>()
            {
                {"耗時",$"{ExecTime?.TotalMilliseconds}ms" },
                {"資料庫時間","" },
                {"影響/總筆數",$"{ResultData.Count()}/{SendData.Count()}" }
            };

            JObject jobj = JObject.FromObject(dic);
            jobj.Add(LogMode.ToString(), JArray.FromObject(result));

            string str = $"{message}{jobj.ToString(Newtonsoft.Json.Formatting.None)}";
            return str;
        }
    }
}
