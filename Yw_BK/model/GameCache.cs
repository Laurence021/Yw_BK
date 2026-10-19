#region 功能修改描述與時間

/*
    描述：庫裡新建賽事表資訊
    建立時間：2019-05-08
*/

#endregion

using System;

namespace Yw_BK.model
{
    /// <summary>
    /// 新建賽事資訊
    /// </summary>
    public class GameCache
    {
        /// <summary>
        /// 數據ID
        /// </summary>
        public int F_Id { get; set; }
        /// <summary>
        /// 聯盟ID
        /// </summary>
        public int F_AllyId { get; set; }
        /// <summary>
        /// 後綴ID
        /// </summary>
        public int F_SuffixId { get; set; }
        /// <summary>
        /// 隊伍A ID
        /// </summary>
        public int F_TmaId { get; set; }
        /// <summary>
        /// 隊伍B ID
        /// </summary>
        public int F_TmbId { get; set; }
        /// <summary>
        /// 開賽時間
        /// </summary>
        public DateTime F_GameDate { get; set; }
        /// <summary>
        /// 帳務時間
        /// </summary>
        public DateTime F_PaymentDate { get; set; }
    }
}
