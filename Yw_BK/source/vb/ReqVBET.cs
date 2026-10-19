#region 功能修改描述與時間

/*
    描述：VB數據第一手接收分配處理
    日期：2020-04-20

    描述：調整外頁接收數據不額外拋出線程處理(拔除Task.Run)
    日期：2022-05-12
*/

#endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// VB請求器
    /// </summary>
    public class ReqVBET
    {
        /// <summary>
        /// 接收資料處理
        /// </summary>
        /// <param name="userInfo">帳號</param>
        /// <param name="connectVBET">VB連線物件</param>
        /// <param name="data">來源推送原始資料</param>
        public void GetResponse(UserInfo userInfo, ConnectVBET connectVBET, object data)
        {
            string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString();

            if (userInfo.GameType == SubscribeType.Other)
            {
                //記錄來源日誌
                LogFile.SetLog(LogMode.Html, Environment.NewLine + data.ToString(), userInfo.GameType.ToString("g"), dateNow);

                //解析聯盟id
                if (connectVBET.GetLeagueId(data.ToString()))
                {
                    //觸發訂閱外頁賽事
                    Task.Run(() =>
                    {
                        connectVBET.OutsideSubscribe();
                    });
                }
            }
            else if (userInfo.GameType == SubscribeType.Zd)
            {
                //記錄來源日誌
                LogFile.SetLog(LogMode.Html, Environment.NewLine + data.ToString(), userInfo.GameType.ToString("g"), dateNow);

                //解析外頁賽事id並觸發訂閱內頁 [陣列索引 0:isGameChange  1:isRemoveGame]
                bool[] sw = connectVBET.GetSubscribeId(data.ToString());

                //是否賽事變動
                if (sw[0])
                {
                    connectVBET.InsideSubscribe();
                }

                //來源是否刪除所有賽事
                if (sw[1])
                {
                    foreach (GameInfoVBET gameInfo in Config.gameDic.Values.Where(a => a.GpType == GpType.Zd))
                    {
                        gameInfo.DataId = -1;
                    }
                }
            }
            else if (userInfo.GameType == SubscribeType.ZdMore)
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
    }
}
