#region 功能修改描述與時間

/*
    描述：VBET用GamieInfo
    建立時間：2020-05-22
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
    public class GameInfoVBET : GameInfo
    {
        public GameInfoVBET(string sourceId, long dataId = 0) : base(sourceId, dataId)
        {
        }
    }
}
