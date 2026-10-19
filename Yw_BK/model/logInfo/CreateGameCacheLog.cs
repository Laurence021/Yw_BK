#region 功能修改描述與時間

/*
    描述：CreateGameCacheLog入庫日誌紀錄
    日期：2024-07-15
*/

#endregion

using Gp.Model;
using Newtonsoft.Json.Linq;
using YwCore;

namespace Yw_BK.model.logInfo
{
    /// <summary>
    /// CreateGameCacheLog入庫日誌紀錄
    /// </summary>
    internal class CreateGameCacheLog : BaseLogInfo
    {
        /// <summary>
        /// 入庫資料
        /// </summary>
        public IEnumerable<BkGameCacheCreateDto> SendData { get; }
        /// <summary>
        /// 資料
        /// </summary>
        public IEnumerable<BkGameCacheCreateResultDto> ResultData { get; }

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
        public CreateGameCacheLog(Source source, string gpType, IEnumerable<BkGameCacheCreateDto> sendData, IEnumerable<BkGameCacheCreateResultDto> resultData, TimeSpan? execTime = null, Entrance entrance = Entrance.None, string remark = "") : base(source, gpType, execTime, entrance, remark)
        {
            SendData = sendData;
            ResultData = resultData;
            LogMode = LogMode.Insert_Game;
        }

        /// <summary>
        /// 輸出日誌內容
        /// </summary>
        /// <returns></returns>
        public override string OutputLog()
        {
            string message = $"{Remark}{Environment.NewLine}";

            JArray sendArray = JArray.FromObject(SendData);
            JArray resultArray = JArray.FromObject(ResultData);

            // 把 Result 轉成 Dictionary 方便比對
            var resultDict = resultArray
                .Children<JObject>()
                .ToDictionary(r => r["Key"].ToString());

            JArray mergedArray = new JArray();

            foreach (var sendItem in sendArray.Children<JObject>())
            {
                var id = sendItem["Key"].ToString();
                JObject merged = new JObject(sendItem); // 複製一份 send

                if (id != null && resultDict.TryGetValue(id, out var resultItem))
                {
                    merged.Merge(resultItem, new JsonMergeSettings
                    {
                        MergeNullValueHandling = MergeNullValueHandling.Ignore,
                        PropertyNameComparison = StringComparison.InvariantCultureIgnoreCase
                    });
                }

                mergedArray.Add(merged);
            }

            Dictionary<string, object> dic = new Dictionary<string, object>()
             {
                 {"耗時",$"{ExecTime?.TotalMilliseconds}ms" },
                 {"資料庫時間","" },
                 {"影響/總筆數",$"{ResultData.Count(a=>a.Result == 1)}/{mergedArray.Count}" }
             };

            JObject jobj = JObject.FromObject(dic);
            jobj.Add(LogMode.ToString(), mergedArray);

            string str = $"{message}{jobj.ToString(Newtonsoft.Json.Formatting.None)}";
            return str;
        }
    }
}
