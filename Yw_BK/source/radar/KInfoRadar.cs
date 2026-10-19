#region 功能修改描述與時間

/*
    描述：雷達用KInfo
    日期：2023-07-18
*/

#endregion

using System;
using Yw_BK.model;

namespace Yw_BK.source.radar
{
    /// <summary>
    /// 雷達用GameInfo
    /// </summary>
    [Serializable]
    public class KInfoRadar : KInfo
    {
        /// <summary>
        /// 雷達玩法狀態
        /// </summary>
        public int MarketStatus { get; set; }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="strType">GP類型</param>
        /// <param name="dataId">數據更新索引</param>
        /// <param name="kType">玩法類型</param>
        /// <param name="setType">所屬集合</param>
        /// <param name="opt">玩法選項</param>
        /// <param name="matchId">來源gid</param>
        public KInfoRadar(string strType, long dataId, int kType, byte setType = 1, decimal matchId = 0m) : base(strType, dataId, kType, setType, matchId)
        {
        }

        public override void Compare(KInfo newObj)
        {
            base.Compare(newObj);
            MarketStatus = (newObj as KInfoRadar)?.MarketStatus ?? MarketStatus;
        }
    }

}
