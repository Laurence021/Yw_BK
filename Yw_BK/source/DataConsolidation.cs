#region 功能修改描述與時間

/*
    描述：回傳數據集中處理
    日期：2023-12-15
*/

#endregion

using System.Collections.Concurrent;
using System.Linq;

namespace Yw_BK.source
{
    /// <summary>
    /// 回傳數據集中處理
    /// </summary>
    public class DataConsolidation
    {
        //接收回應的總資料  key:發送流水號, value:來源資料集合
        private ConcurrentDictionary<long, ConcurrentBag<string>> recvDic = new ConcurrentDictionary<long, ConcurrentBag<string>>();
        //發送請求的總次數  key:發送流水號, value:請求的總次數
        private ConcurrentDictionary<long, int> sendDic = new ConcurrentDictionary<long, int>();

        /// <summary>
        /// 回傳數據集中處理
        /// </summary>
        public DataConsolidation()
        {

        }

        /// <summary>
        /// 數據批次回傳
        /// </summary>
        /// <param name="sendId">請求流水號</param>
        /// <param name="data">數據</param>
        /// <param name="batchBag">批次數據</param>
        /// <returns></returns>
        public bool DataCentralize(long sendId, string data, out ConcurrentBag<string> batchBag)
        {
            //添加數據
            batchBag = recvDic.GetOrAdd(sendId, new ConcurrentBag<string>());
            batchBag.Add(data);

            //發送及接收的總次數一致，則為成功請求一輪
            if (sendDic.TryGetValue(sendId, out int sendCount) &&
                batchBag.Count == sendCount)
            {
                //刪除流水號小於"此次結束收發的流水號"
                foreach (long tmpSendId in recvDic.Keys.Where(a => a < sendId))
                {
                    recvDic.TryRemove(tmpSendId, out var value);
                }

                //刪除流水號小於"此次結束收發的流水號"
                foreach (long tmpSendId in sendDic.Keys.Where(a => a < sendId))
                {
                    sendDic.TryRemove(tmpSendId, out int value);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 設定發送資訊
        /// </summary>
        /// <param name="sendId">發送id</param>
        /// <param name="sendCount">發送筆數</param>
        public void SetSendInfo(long sendId, int sendCount)
        {
            sendDic.TryAdd(sendId, sendCount);
        }
    }
}
