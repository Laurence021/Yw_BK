#region 功能修改描述與時間

/*
    描述：SB相關數據相關處理
    建立時間：2020-06-30
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// SB請求器
    /// </summary>
    public static class ReqSB
    {
        // <summary>
        /// SB要請求的球類
        /// </summary>
        public static SportSb SportSb = SportSb.BK;
        /// <summary>
        /// SB連線物件
        /// </summary>
        public static CommonSB commonSB { get; set; }
        /// <summary>
        /// 接收到數據，丟到對應柱列
        /// </summary>
        /// <param name="connectSB">SB連線物件</param>
        /// <param name="data">來源推送資料</param>
        /// <param name="gptype">類型</param>
        /// <param name="sportSb">球類</param>
        public static void ReceiveData(CommonSB commonSB, string data, string gptype, SportSb sportSb)
        {
            try
            {
                switch (gptype)
                {
                    case "Ds":
                        Config.dsQueue.Enqueue(new DataInfo(gptype, data));
                        break;
                    case "Zd":
                        Config.zdQueue.Enqueue(new DataInfo(gptype, data));
                        break;
                    case "Zp":
                        Config.zpQueue.Enqueue(new DataInfo(gptype, data));
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, "接收資料M到柱列過程發生錯誤：" + Environment.NewLine + ex.ToString());
            }
        }

        #region 訂閱相關

        /// <summary>
        /// 訂閱處理
        /// </summary>
        /// <param name="userInfo">帳號資訊</param>
        public static void SubscribeHandle()
        {
            try
            {
                List<object> subscribeCommand = new List<object>();

                if (Config.isOpenZd)
                {
                    subscribeCommand.AddRange(commonSB.GetSubscribeCommand(SubscribeType.Zd, TransferBetGroup(SubscribeType.Zd), SportSb));
                }

                if (Config.isOpenDs)
                {
                    subscribeCommand.AddRange(commonSB.GetSubscribeCommand(SubscribeType.Ds, TransferBetGroup(SubscribeType.Ds), SportSb));
                }

                if (Config.isOpenZp)
                {
                    subscribeCommand.AddRange(commonSB.GetSubscribeCommand(SubscribeType.Zp, TransferBetGroup(SubscribeType.Zp), SportSb));
                }

                //發送訂閱指令
                commonSB.SendSubscribe(subscribeCommand.ToArray());
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, "發送訂閱指令錯誤: " + ex.Message + Environment.NewLine + ex.ToString());
            }
        }

        /// <summary>
        /// 轉換對應賽事類型的陣列
        /// </summary>
        /// <param name="gpType">賽事類型</param>
        /// <returns>玩法陣列</returns>
        public static int[] TransferBetGroup(SubscribeType gpType)
        {
            List<int> result = new List<int>();

            //BetType
            //HDP(全、上半)  1, 7
            //OU(全、上半)   3, 8
            //勝負盤(全、上半)  20, 21
            //OE(全、上半)   2, 12
            //第N節HDP、OU、OE、勝負 609, 610, 611, 612
            //主队OU盘(全、上半、第N節) 401,403,615, (新)461, 463, 651 >>目前瀏覽器新舊都有訂閱
            //客队OU盘(全、上半、第N節) 402,404,616, (新)462, 464, 652
            //首先获得(全、上半、下半、第N節) 604,606,607,613
            //最后获得(全) 605
            bool isOpen = gpType == SubscribeType.Ds ? Config.isOpenDs : gpType == SubscribeType.Zd ? Config.isOpenZd : gpType == SubscribeType.Zp ? Config.isOpenZp : false;

            if (isOpen)
            {
                if (gpType == SubscribeType.Zd)
                {
                    #region 依照開關設定所需請求的玩法類型

                    result.AddRange(new int[] { 1, 3, 2, 7, 8, 12, 20, 21, 609, 610, 611, 612, 401, 402, 403, 404, 461, 462, 463, 464, 615, 616, 651, 652 });

                    #endregion
                }
                else
                {
                    #region 依照開關設定所需請求的玩法類型

                    result.AddRange(new int[] { 1, 3, 20, 2, 7, 8, 21, 12, 609, 610, 611, 612, 401, 402, 403, 404, 461, 462, 463, 464, 615, 616, 606, 607, 613, 604, 605, 651, 652 });

                    #endregion
                }
            }

            return result.ToArray();
        }

        #endregion
    }
}
