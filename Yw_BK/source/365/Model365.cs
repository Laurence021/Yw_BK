#region 功能修改描述與時間

/*
    描述：365DS用GamieInfo
    建立時間：2020-02-26
*/

#endregion

using System;
using Yw_BK.model;

namespace Yw_BK.source
{
    [Serializable]
    /// <summary>
    /// 365用GameInfo
    /// </summary>
    public class GameInfo365 : GameInfo
    {
        public GameInfo365(string sourceId, long dataId = 0) : base(sourceId, dataId)
        {
        }

        /// <summary>
        /// FI
        /// </summary>
        public string FI { get; set; }
        /// <summary>
        /// 上半FI
        /// </summary>
        public string Half_FI { get; set; }
        /// <summary>
        /// NF
        /// </summary>
        public string NF { get; set; }
    }
}
