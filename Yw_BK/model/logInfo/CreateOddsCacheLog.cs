#region 功能修改描述與時間

/*
    描述：CreateOddsCacheLog入庫日誌紀錄
    日期：2024-07-15
*/

#endregion

using Gp.Model;
using Newtonsoft.Json.Linq;
using YwCore;

namespace Yw_BK.model.logInfo
{
    /// <summary>
    /// CreateOddsCacheLog入庫日誌紀錄
    /// </summary>
    internal class CreateOddsCacheLog : BaseLogInfo
    {
        /// <summary>
        /// 入庫資料
        /// </summary>
        public IEnumerable<BkOddsCacheCreateDto> SendData { get; }
        /// <summary>
        /// 資料
        /// </summary>
        public IEnumerable<BkOddsCacheCreateResultDto> ResultData { get; }

        /// <summary>
        /// 建立新比賽玩法日誌
        /// </summary>
        /// <param name="source">來源</param>
        /// <param name="gpType">GPTYPE</param>
        /// <param name="sendData">送出數據</param>
        /// <param name="resultData">回傳數據</param>
        /// <param name="execTime">動作耗時</param>
        /// <param name="entrance">平台入口</param>
        /// <param name="remark">備註</param>
        public CreateOddsCacheLog(Source source, string gpType, IEnumerable<BkOddsCacheCreateDto> sendData, IEnumerable<BkOddsCacheCreateResultDto> resultData, TimeSpan? execTime = null, Entrance entrance = Entrance.None, string remark = "") : base(source, gpType, execTime, entrance, remark)
        {
            SendData = sendData;
            ResultData = resultData;
            LogMode = LogMode.Insert_Odds;
        }

        /// <summary>
        /// 輸出日誌內容
        /// </summary>
        /// <returns></returns>
        public override string OutputLog()
        {
            string message = $"{Remark}{Environment.NewLine}";

            var result = from a in SendData
                         join b in ResultData on new { a.GId, a.KType, a.BType, a.SetType } equals new { b.GId, b.KType, b.BType, b.SetType } into c
                         from d in c.DefaultIfEmpty()
                         select new
                         {
                             Id = d?.Id,
                             a.GId,
                             a.Gpdm,
                             a.KType,
                             a.BType,
                             a.SetType,
                             a.SourceId,
                             a.IsOpen,
                             a.LineAt,
                             a.Line,
                             a.Odds,
                             Result = d == null ? 0 : 1,
                         };

            //輕量化日誌
            var group = result.ToLookup(element => element.GId,
                                        element => new { element.Gpdm, element.Id, element.KType, element.BType, element.SetType, element.SourceId, element.LineAt, element.Line, element.IsOpen, element.Odds, element.Result })
                                        .ToDictionary(group1 => group1.Key,
                                                      group1 => group1.ToLookup(element => element.Gpdm,
                                                                      element => new { element.Id, element.KType, element.BType, element.SetType, element.SourceId, element.LineAt, element.Line, element.IsOpen, element.Odds, element.Result })
                                                      .ToDictionary(group2 => group2.Key,
                                                                    group2 => group2.ToLookup(element => element.KType,
                                                                                    element => new { element.Id, element.BType, element.SetType, element.SourceId, element.LineAt, element.Line, element.IsOpen, element.Odds, element.Result })
                                                                    .ToDictionary(group3 => group3.Key)));

            var reduceLog = JObject.FromObject(group);

            Dictionary<string, object> dic = new Dictionary<string, object>()
            {
                {"耗時",$"{ExecTime?.TotalMilliseconds}ms" },
                {"資料庫時間", "" },
                {"影響/總筆數",$"{ResultData.Count(a=>a.Result==1)}/{SendData.Count()}" }
            };

            JObject jobj = JObject.FromObject(dic);
            jobj.Add(LogMode.ToString(), reduceLog);

            string str = $"{message}{jobj.ToString(Newtonsoft.Json.Formatting.None)}";
            return str;
        }
    }
}
