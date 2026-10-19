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
    public class ReqXB
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
                    urlInfo = string.Concat(Config.url, $"LineFeed/GetSportsShortZip?sports=3{champs}&lng=cn&tf=2200000&tz=8&country=90&partner=31&virtualSports=true&gr=17&groupChamps=true");
                    break;
                case GpType.Zp:

                    if (Config.isTomorrow)
                    {
                        if (DateTime.Now.Hour < 12)
                        {
                            //請求的時間區段(XB參數請求多少分鐘)現在~隔日中午12:00以前
                            int reqMinutes = (int)DateTime.Now.Date.AddHours(12).Subtract(DateTime.Now).TotalMinutes;
                            urlInfo = string.Concat(Config.url, $"LineFeed/GetSportsShortZip?sports=3{champs}&lng=cn&tf={reqMinutes + 1440}&tz=8&country=90&partner=31&virtualSports=true&gr=17&groupChamps=true");
                        }
                        else
                        {
                            urlInfo = string.Concat(Config.url, $"LineFeed/GetSportsShortZip?sports=3{champs}&lng=cn&tf=1440&tz=8&country=90&partner=31&virtualSports=true&gr=17&groupChamps=true");
                        }
                    }
                    else
                    {
                        urlInfo = string.Concat(Config.url, $"LineFeed/GetSportsShortZip?sports=3{champs}&lng=cn&tf=2200000&tz=8&country=90&partner=31&virtualSports=true&gr=17&groupChamps=true");
                    }
                   
                    break;
                case GpType.Zd:
                    urlInfo = string.Concat(Config.url, "LiveFeed/Get1x2_Zip?sports=3&count=500&lng=cn&cfview=4&mode=4&country=179&getEmpty=true");
                    break;
                case GpType.DsMore:
                case GpType.ZpMore:
                    urlInfo = string.Concat(Config.url, "LineFeed/GetGameZip?id=" + mainId + "&lng=cn&cfview=4&isSubGames=true&GroupEvents=true&countevents=2500");
                    break;
                case GpType.ZdMore:
                    urlInfo = string.Concat(Config.url, "LiveFeed/GetGameZip?id=" + mainId + "&lng=cn&cfview=4&isSubGames=true&GroupEvents=true&countevents=2500");
                    break;
            }

            //協定
            reqInfo.Req = WebRequest.CreateHttp(urlInfo);
            reqInfo.Req.Method = "GET";
            reqInfo.Req.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/50.0.2785.143 Safari/537.36";
            reqInfo.Req.Accept = "text/html, application/xhtml+xml, */*";
            reqInfo.Req.ContentType = "application/x-www-form-urlencoded";
            reqInfo.Req.AllowAutoRedirect = false;
            reqInfo.Req.KeepAlive = true;
            reqInfo.Req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            reqInfo.Req.Proxy = string.IsNullOrEmpty(proxy) ? reqInfo.Req.Proxy : new WebProxy(proxy);

            if ((gpType & GpType.More) > 0)
            {
                reqInfo.SourceMatchId = mainId;
                reqInfo.PostData = mainId.ToString();

                if (gpType != GpType.ZdMore)
                {
                    reqInfo.Req.Referer = string.Concat(Config.url, "LineFeed/GetGameZip?id=" + mainId + "&lng=cn&cfview=4&isSubGames=true&GroupEvents=true&countevents=2500");
                }
                else
                {
                    reqInfo.Req.Referer = string.Concat(Config.url, "LiveFeed/GetGameZip?id=" + mainId + "&lng=cn&cfview=4&isSubGames=true&GroupEvents=true&countevents=2500");
                }
            }
        }

        /// <summary>
        /// 來源左側球類選單請求+解析處理
        /// </summary>
        /// <<param name = "proxy" > 代理 </ param >
        public static void RequestLeague(string proxy)
        {
            string menuUrl = $"{Config.url}LineFeed/GetSportsShortZip?sports=3&lng=cn&tf=2200000&tz=8&country=90&partner=31&virtualSports=true&gr=17&groupChamps=true";

            HttpWebRequest request = WebRequest.CreateHttp(menuUrl);
            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/50.0.2785.143 Safari/537.36";
            request.Accept = "text/html, application/xhtml+xml, */*";
            request.ContentType = "application/x-www-form-urlencoded";
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
                LogFile.SetLog(LogMode.Html, html, time: dateNow);

                //解析
                ParserXB.ParserLeague(html, GpType.Ds);
            }
            finally
            {
                request.Abort();
                request = null;
            }
        }
    }
}
