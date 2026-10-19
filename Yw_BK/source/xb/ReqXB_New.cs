#region 功能修改描述與時間

/*
    描述：XB請求器
    日期：2019-05-16
    
    描述：調整使用內頁玩法數量判斷countevents參數
    日期：2021-07-23
*/

#endregion

using System;
using System.IO;
using System.Net;
using System.Text;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// XB請求器
    /// </summary>
    public class ReqXB_New
    {
        /// <summary>
        /// 設定非同步要求器
        /// </summary>
        /// <param name="requestInfo">要求器</param>
        /// <param name="champsList">此次請求清單</param>
        public static void SetHttpWebRequest(ReqInfo reqInfo, string proxy = null, decimal mainId = 0m, string champsList = "")
        {
            string urlInfo = string.Empty;

            //GP類型,解析失敗則跳過
            GpType gpType;
            if (!Enum.TryParse(reqInfo.ThreadInfo.StrType, out gpType)) { return; }

            //聯盟ID參數
            string champs = string.IsNullOrEmpty(champsList) ? string.Empty : $"&champs={string.Join(",", champsList)}";

            //url設定
            switch (gpType)
            {
                case GpType.Ds:
                case GpType.Zp:
                    urlInfo = $"{Config.url}service-api/LineFeed/GetSportsShortZip?sports=3{champs}&lng=cn&country=90&partner=57&virtualSports=true&gr=463&groupChamps=true{GetTimeParameter(gpType)}";
                    break;
                case GpType.Zd:
                    urlInfo = $"{Config.url}service-api/LiveFeed/GetSportsShortZip?sports=3{champs}&lng=cn&gr=463&country=90&partner=57&virtualSports=true&groupChamps=true";
                    break;
                case GpType.DsMore:
                case GpType.ZpMore:
                    urlInfo = $"{Config.url}service-api/LineFeed/GetGameZip?id={mainId}&lng=cn&cfview=4&isSubGames=true&GroupEvents=true&countevents=2500&partner=57&topGroups=&country=90&marketType=1";
                    break;
                case GpType.ZdMore:
                    urlInfo = $"{Config.url}service-api/LiveFeed/GetGameZip?id={mainId}&lng=cn&cfview=4&isSubGames=true&GroupEvents=true&countevents=2500&partner=57&topGroups=&country=90&marketType=1";
                    break;
            }

            //協定
            reqInfo.Req = WebRequest.CreateHttp(urlInfo);
            reqInfo.Req.Method = "GET";
            reqInfo.Req.UserAgent = YwCore.Common.USERAGENT;
            reqInfo.Req.Accept = "text/html, application/xhtml+xml, */*";
            reqInfo.Req.ContentType = "application/json";
            reqInfo.Req.AllowAutoRedirect = false;
            reqInfo.Req.KeepAlive = true;
            reqInfo.Req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            reqInfo.Req.Proxy = string.IsNullOrEmpty(proxy) ? reqInfo.Req.Proxy : new WebProxy(proxy);

            if ((gpType & GpType.More) > 0)
            {
                reqInfo.SourceMatchId = mainId;
                reqInfo.PostData = mainId.ToString();
            }
        }

        /// <summary>
        /// 來源左側球類選單請求+解析處理
        /// </summary>
        /// <<param name = "proxy" > 代理 </ param >
        public static void RequestLeague(string proxy, GpType gpType)
        {
            string menuUrl = string.Empty;

            //url設定
            switch (gpType)
            {
                case GpType.Ds:
                case GpType.Zp:
                    menuUrl = $"{Config.url}service-api/LineFeed/GetSportsShortZip?sports=3&lng=cn&country=90&partner=57&virtualSports=true&gr=463&groupChamps=true{GetTimeParameter(gpType)}";
                    break;
                case GpType.Zd:
                    menuUrl = $"{Config.url}service-api/LiveFeed/GetSportsShortZip?sports=3&lng=cn&gr=463&country=90&partner=57&virtualSports=true&groupChamps=true";
                    break;
            }

            HttpWebRequest request = WebRequest.CreateHttp(menuUrl);
            request.Method = "GET";
            request.UserAgent = YwCore.Common.USERAGENT;
            request.Accept = "text/html, application/xhtml+xml, */*";
            request.ContentType = "application/json";
            request.AllowAutoRedirect = false;
            request.KeepAlive = true;
            request.Proxy = string.IsNullOrEmpty(proxy) ? request.Proxy : new WebProxy(proxy);

            //來源回應內容
            string html = string.Empty;

            try
            {
                //接收回應
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        html = reader.ReadToEnd();
                    }
                }

                string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString("00");
                LogFile.SetLog(LogMode.Html, html, gpType + "Menu", dateNow);

                //解析
                ParserXB.ParserLeague(html, gpType);
            }
            finally
            {
                request.Abort();
                request = null;
            }
        }

        /// <summary>
        /// 取得時間參數
        /// </summary>
        private static string GetTimeParameter(GpType gpType)
        {
            int hour = DateTime.Now.Hour;
            DateTime date = hour >= 12 ? DateTime.Now.Date.AddDays(1).AddHours(12) : DateTime.Now.Date.AddHours(12);
            DateTime zpTDate = date.AddHours(24);

            //請求參數(類型、時間區段)
            switch (gpType)
            {
                case GpType.Ds:
                    return $"&tsTo={GetTimeStamp(date)}";
                case GpType.Zp:

                    //是否只抓隔日賽事
                    if (Config.isTomorrow)
                    {
                        return $"&tsFrom={GetTimeStamp(date)}&tsTo={GetTimeStamp(zpTDate)}";
                    }
                    else
                    {
                        return $"&tsFrom={GetTimeStamp(date)}";
                    }

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 取得時間戳
        /// </summary>
        private static long GetTimeStamp(DateTime date)
        {
            return new DateTimeOffset(date).ToUnixTimeSeconds();
        }
    }
}
