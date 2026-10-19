#region 功能修改描述與時間

/*
    描述：UpdateMainGameLog入庫日誌紀錄
    日期：2024-07-15
*/

#endregion

using Gp.Model;
using Newtonsoft.Json.Linq;
using YwCore;
using YwCore.tools;

namespace Yw_BK.model.logInfo
{
    /// <summary>
    /// UpdateMainGameLog入庫日誌紀錄
    /// </summary>
    internal class UpdateMainGameLog : BaseLogInfo
    {
        /// <summary>
        /// 入庫資料
        /// </summary>
        public IEnumerable<BkGameUpdateDto> SendData { get; }
        /// <summary>
        /// 資料
        /// </summary>
        public IEnumerable<BkGameUpdateResultDto> ResultData { get; }

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
        public UpdateMainGameLog(Source source, string gpType, IEnumerable<BkGameUpdateDto> sendData, IEnumerable<BkGameUpdateResultDto> resultData, TimeSpan? execTime = null, Entrance entrance = Entrance.None, string remark = "") : base(source, gpType, execTime, entrance, remark)
        {
            SendData = sendData;
            ResultData = resultData;
            LogMode = LogMode.Update_Main_Game;
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
                             a.IsLive,
                             a.Status,
                             a.LastSec,
                             a.ChannelId,
                             a.ScoreA,
                             a.ScoreB,
                             a.ScoreHalf1A,
                             a.ScoreHalf1B,
                             a.ScoreHalf2A,
                             a.ScoreHalf2B,
                             a.Score1A,
                             a.Score1B,
                             a.Score2A,
                             a.Score2B,
                             a.Score3A, 
                             a.Score3B,
                             a.Score4A, 
                             a.Score4B,
                             a.LiveUrl,
                             a.MultiStatus,
                             a.QuarterTime,
                             Result = d == null ? 0 : 1,
                         };

            var first = ResultData.FirstOrDefault();
            string dbTime = "";

            if (first != null)
            {
                dbTime = TimeStampHelper.ToLocalDateTime(first.UpdateDate).ToString("yyyy-MM-dd HH:mm:ss.ffff");
            }

            Dictionary<string, object> dic = new Dictionary<string, object>()
            {
                {"耗時",$"{ExecTime?.TotalMilliseconds}ms" },
                {"資料庫時間",dbTime },
                {"影響/總筆數",$"{ResultData.Count()}/{SendData.Count()}" }
            };

            JObject jobj = JObject.FromObject(dic);
            jobj.Add(LogMode.ToString(), JArray.FromObject(result));

            string str = $"{message}{jobj.ToString(Newtonsoft.Json.Formatting.None)}";
            return str;
        }
    }
}
