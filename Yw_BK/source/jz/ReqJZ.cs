#region 功能修改描述與時間

/*
    描述：JZ數據第一手接收分配處理
    日期：2019-06-25

    描述：添加奧運使用的接收分配處理
    日期：2021-05-13
*/

#endregion

using System;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// JZ來源數據分配處理
    /// </summary>
    public class ReqJZ
    {
        /// <summary>
        /// 接收資料處理
        /// </summary>
        /// <param name="userInfo">帳號</param>
        /// <param name="data">來源推送原始資料</param>
        public static void GetResponse(UserInfo userInfo, object data)
        {
            string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString();

            if (userInfo.GameType == SubscribeType.Ds)
            {
                //記錄來源日誌
                LogFile.SetLog(LogMode.Html, Environment.NewLine + data.ToString(), userInfo.GameType.ToString("g"), dateNow);
                Config.dsQueue.Enqueue(new DataInfo(SubscribeType.Ds.ToString(), data.ToString()));

            }
            else if (userInfo.GameType == SubscribeType.Zd)
            {
                //記錄來源日誌
                LogFile.SetLog(LogMode.Html, Environment.NewLine + data.ToString(), userInfo.GameType.ToString("g"), dateNow);
                Config.zdQueue.Enqueue(new DataInfo(SubscribeType.Zd.ToString(), data.ToString()));
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// 接收資料處理
        /// </summary>
        /// <param name="userInfo">帳號</param>
        /// <param name="data">來源推送原始資料</param>
        public static void GetResponseAyh(UserInfo userInfo, object data)
        {
            string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString();

            if (userInfo.GameType == SubscribeType.Ds)
            {
                //記錄來源日誌
                LogFile.SetLog(LogMode.Html, Environment.NewLine + data.ToString(), $"{userInfo.GameType.ToString("g")}Ayh", dateNow);
                Config.dsMoreQueue.Enqueue(new DataInfo(SubscribeType.Ds.ToString(), data.ToString(), 0, true));
            }
            else if (userInfo.GameType == SubscribeType.Zd)
            {
                //記錄來源日誌
                LogFile.SetLog(LogMode.Html, Environment.NewLine + data.ToString(), $"{userInfo.GameType.ToString("g")}Ayh", dateNow);
                Config.zdMoreQueue.Enqueue(new DataInfo(SubscribeType.Zd.ToString(), data.ToString(), 0, true));
            }
            else
            {
                return;
            }
        }
    }
}