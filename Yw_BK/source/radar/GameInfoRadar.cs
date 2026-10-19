#region 功能修改描述與時間

/*
    描述：雷達用GameInfo
    日期：2023-05-05
*/

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source.radar
{
    /// <summary>
    /// 雷達用GameInfo
    /// </summary>
    [Serializable]
    public class GameInfoRadar : GameInfo
    {
        /// <summary>
        /// 雷達聯盟ID
        /// </summary>
        public string TournamentId { get; set; } = "";
        /// <summary>
        /// 雷達季ID
        /// </summary>
        public string SeasonId { get; set; } = "";
        /// <summary>
        /// 共幾節
        /// </summary>
        public int RegularNnumberOfPeriods { get; set; }

        /// <summary>
        /// 雷達用GameInfo
        /// </summary>
        /// <param name="sourceId">來源ID</param>
        /// <param name="dataId">數據索引</param>
        public GameInfoRadar(string sourceId, long dataId = 0) : base(sourceId, dataId)
        {
        }

        /// <summary>
        /// 玩法比對
        /// </summary>
        /// <param name="newGame"></param>
        /// <param name="gpType"></param>
        protected override void OddsCompare(GameInfo newGame, GpType gpType)
        {
            //有數據才比對
            if (newGame.ObjDic.Count > 0)
            {
                //無玩法則直接複製
                if (ObjDic.Count == 0)
                {
                    (newGame as GameInfoRadar)?.AutoCreate();
                    ObjDic = new ConcurrentDictionary<decimal, GpdmInfo>(newGame.ObjDic);
                }
                else
                {
                    foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in newGame.ObjDic)
                    {
                        decimal gpdm = gpdmPair.Key;

                        //group存在
                        if (ObjDic.TryGetValue(gpdm, out GpdmInfo tmpObj))
                        {
                            //來源數據取代GP提前生成
                            tmpObj.IsGenerate = gpdmPair.Value.IsGenerate;

                            //處理各個玩法
                            foreach (KeyValuePair<int, KInfo> kPair in gpdmPair.Value)
                            {
                                //kType存在，取出玩法資訊
                                if (tmpObj.TryGetValue(kPair.Key, out KInfo getKinfo))
                                {
                                    //SetType一致才比對
                                    if (getKinfo.SetType == kPair.Value.SetType)
                                    {
                                        if (getKinfo.StrType == kPair.Value.StrType)
                                        {
                                            (getKinfo as KInfoRadar).Compare(kPair.Value);
                                        }
                                    }
                                }
                                //kType不存在則直接添加
                                else
                                {
                                    if (kPair.Value.BInfoDic.Count > 0)
                                    {
                                        tmpObj.TryAdd(kPair.Key, kPair.Value);
                                    }
                                }
                            }
                        }
                        //kType不存在則直接添加
                        else
                        {
                            if (gpdmPair.Value.Values.Any(a => a.BInfoDic.Count > 0))
                            {
                                (gpdmPair.Value as GpdmInfoRadar).AutoCreate(GpType);
                                tmpObj = ObjDic.GetOrAdd(gpdmPair.Key, gpdmPair.Value);
                            }
                        }

                        ////全場主盤以外的玩法
                        //if (tmpObj != null && tmpObj.Key != 1)
                        //{
                        //    //全玩法為停止關閉狀態，則刪除整條
                        //    if (tmpObj.Values.All(a => Math.Abs((a as KInfoRadar).MarketStatus) != 1))
                        //    {
                        //        foreach (var kinfo in tmpObj.Values)
                        //        {
                        //            kinfo.DataId = -1;
                        //        }
                        //    }
                        //}
                    }
                }
            }

            foreach (GpdmInfo pairs in ObjDic.Values)
            {
                if (!newGame.ObjDic.TryGetValue(pairs.Key, out GpdmInfo newPairs))
                {
                    pairs.Values.ToList().ForEach(a => a.DataId = -1);
                }
            }

        }

        /// <summary>
        /// 自動補生成玩法格子
        /// </summary>
        public void AutoCreate()
        {
            foreach (GpdmInfoRadar gpdm in ObjDic.Values)
            {
                //全玩法為停止關閉狀態，則刪除整條
                if (gpdm.Values.All(a => Math.Abs((int)(a as KInfoRadar).MarketStatus) != 1))
                {
                    ObjDic.TryRemove(gpdm.Key, out var del);
                }
                else
                {
                    gpdm.AutoCreate(GpType);

                }
            }
        }
    }
}
