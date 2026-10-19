#region 功能修改描述與時間

/*
    描述：UpdateMainOddsLog入庫日誌紀錄
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
    /// UpdateMainOddsLog入庫日誌紀錄
    /// </summary>
    internal class UpdateMainOddsLog : BaseLogInfo
    {
        /// <summary>
        /// 日誌類型
        /// </summary>
        public enum LogType
        {
            Update,
            UpdateJzf,
            Close,
            Stop
        }

        /// <summary>
        /// 入庫資料
        /// </summary>
        public IEnumerable<BkOddsUpdateDto> SendData { get; }
        /// <summary>
        /// 資料
        /// </summary>
        public IEnumerable<BkOddsUpdateResultDto> ResultData { get; }

        /// <summary>
        /// 更新主庫玩法日誌
        /// </summary>
        /// <param name="source">來源</param>
        /// <param name="gpType">GPTYPE</param>
        /// <param name="logType">日誌類型</param>
        /// <param name="sendData">送出數據</param>
        /// <param name="resultData">回傳數據</param>
        /// <param name="execTime">動作耗時</param>
        /// <param name="entrance">平台入口</param>
        /// <param name="remark">備註</param>
        public UpdateMainOddsLog(Source source, string gpType, LogType logType, IEnumerable<BkOddsUpdateDto> sendData, IEnumerable<BkOddsUpdateResultDto> resultData, TimeSpan? execTime = null, Entrance entrance = Entrance.None, string remark = "") : base(source, gpType, execTime, entrance, remark)
        {
            SendData = sendData;
            ResultData = resultData;

            switch (logType)
            {
                case LogType.Update:
                    LogMode = LogMode.Update_Main_Odds;
                    break;
                case LogType.UpdateJzf:
                    LogMode = LogMode.Update_Main_Jzf;
                    break;
                case LogType.Stop:
                    LogMode = LogMode.Update_Main_StopGP;
                    break;
                default:
                    LogMode = LogMode.Update_Main_Close;
                    break;
            }
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
                             a.Gpdm,
                             a.KType,
                             a.BType,
                             a.SetType,
                             a.SourceId,
                             a.IsFollow,
                             a.Display,
                             a.IsOpen,
                             a.SourceOpen,
                             a.LineAt,
                             a.Line,
                             a.Odds,
                             a.Rate31,
                             LineModify = d?.LineModify,
                             LineJump = d?.LineJump,
                             OddsJump = d?.OddsJump,
                             OddsModify = d?.OddsModify,
                             Odds31 = d?.Odds31,
                             OldLineAt = d?.OldLineAt.GetHashCode(),
                             NewLineAt = d?.NewLineAt.GetHashCode(),
                             IsChangeLineAt = d?.IsChangeLineAt,
                             Result = d == null ? 0 : 1,
                         };

            //輕量化日誌
            var group = result.ToLookup(element => element.GId,
                                        element => new { element.Gpdm, element.Id, element.KType, element.BType, element.SetType, element.SourceId, element.IsFollow, element.Display, element.IsOpen, element.SourceOpen, element.LineAt, element.Line, element.Odds, element.Rate31, element.LineJump, element.LineModify, element.OddsJump, element.OddsModify, element.Odds31, element.OldLineAt, element.NewLineAt, element.Result })
                                        .ToDictionary(group1 => group1.Key,
                                                      group1 => group1.ToLookup(element => element.Gpdm,
                                                                      element => new { element.Id, element.KType, element.BType, element.SetType, element.SourceId, element.IsFollow, element.Display, element.IsOpen, element.SourceOpen, element.LineAt, element.Line, element.Odds, element.Rate31, element.LineJump, element.LineModify, element.OddsJump, element.OddsModify, element.Odds31, element.OldLineAt, element.NewLineAt, element.Result })
                                                      .ToDictionary(group2 => group2.Key,
                                                                    group2 => group2.ToLookup(element => element.KType,
                                                                                    element => new { element.Id, element.BType, element.SetType, element.SourceId, element.IsFollow, element.Display, element.IsOpen, element.SourceOpen, element.LineAt, element.Line, element.Odds, element.Rate31, element.LineJump, element.LineModify, element.OddsJump, element.OddsModify, element.Odds31, element.OldLineAt, element.NewLineAt, element.Result })
                                                                    .ToDictionary(group3 => group3.Key)));

            var reduceLog = JObject.FromObject(group);

            var first = ResultData.FirstOrDefault();
            string dbTime = "";

            if (first != null)
            {
                dbTime = TimeStampHelper.ToLocalDateTime(first.UpdateDate).ToString("yyyy-MM-dd HH:mm:ss.ffff");
            }

            Dictionary<string, object> dic = new Dictionary<string, object>()
            {
                {"耗時",$"{ExecTime?.TotalMilliseconds}ms" },
                {"資料庫時間", dbTime },
                {"影響/總筆數",$"{ResultData.Count(a=>a.Id>0)}/{SendData.Count()}" }
            };

            JObject jobj = JObject.FromObject(dic);
            jobj.Add(LogMode.ToString(), reduceLog);

            string str = $"{message}{jobj.ToString(Newtonsoft.Json.Formatting.None)}";
            return str;
        }
    }
}
