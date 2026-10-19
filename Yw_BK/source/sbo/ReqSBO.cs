#region 功能修改描述與時間

/*
    描述：SBO請求器
    日期：2022-04-14
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
    /// SBO請求器
    /// </summary>
    public class ReqSBO
    {
        /// <summary>
        /// Cookie記錄用
        /// </summary>
        private static CookieCollection cookieCollection = new CookieCollection();

        /// <summary>
        /// 設定非同步要求器
        /// </summary>
        /// <param name="requestInfo">要求器</param>
        public static void SetHttpWebRequest(ReqInfo reqInfo, string proxy = null, decimal mainId = 0, string mainKey = "")
        {
            string urlInfo = string.Empty;

            //GP類型,解析失敗則跳過
            GpType gpType;
            if (!Enum.TryParse(reqInfo.ThreadInfo.StrType, out gpType)) { return; }

            //url設定
            switch (gpType)
            {
                case GpType.Ds:
                    urlInfo = $"{Config.url}zh-CN/euro/篮球";
                    break;
                case GpType.Zd:
                    urlInfo = $"{Config.url}zh-CN/euro/滚球投注/篮球";
                    break;
                case GpType.DsMore:
                    urlInfo = $"{Config.url}zh-CN/euro/篮球/{mainKey}";
                    break;
                case GpType.ZdMore:
                    urlInfo = $"{Config.url}zh-CN/euro/滚球投注/篮球/{mainKey}";
                    break;
                default:
                    return;
            }

            //協定
            reqInfo.Req = WebRequest.CreateHttp(urlInfo);
            reqInfo.Req.Method = "GET";
            reqInfo.Req.UserAgent = YwCore.Common.USERAGENT;
            reqInfo.Req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
            reqInfo.Req.AllowAutoRedirect = true;
            reqInfo.Req.KeepAlive = true;
            reqInfo.Req.Headers["Accept-Language"] = "zh-TW,zh;q=0.9,en-US;q=0.8,en;q=0.7";
            reqInfo.Req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            reqInfo.Req.Proxy = string.IsNullOrEmpty(proxy) ? reqInfo.Req.Proxy : new WebProxy(proxy);

            if (gpType == GpType.DsMore || gpType == GpType.ZpMore || gpType == GpType.ZdMore)
            {
                reqInfo.SourceMatchId = mainId;
                reqInfo.PostData = mainKey;
            }
        }

        /// <summary>
        /// 請求聯盟和對應id
        /// </summary>
        /// <param name="proxy">代理資訊</param>
        public static void RequestLeague(string proxy)
        {
            //導入首頁 設定Cookie
            string url = Config.url;
            string html = GetHttpWebRequest($"{url}zh-CN/euro/篮球", proxy, true);

            System.Text.RegularExpressions.Match tr = System.Text.RegularExpressions.Regex.Match(html, "src=\"([^=]*dynamic.*?)\"");

            if (tr.Success)
            {
                html = GetHttpWebRequest(tr.Result("$1"), proxy, true);
                string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString("00");
                LogFile.SetLog(LogMode.Html, html, "LeaguePage", time: dateNow);

                ParserSBO.ParserLeague(html);
            }
        }

        /// <summary>
        /// GET請求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="proxy"></param>
        /// <param name="isAllowAutoRedirect"></param>
        /// <returns></returns>
        private static string GetHttpWebRequest(string url, string proxy, bool isAllowAutoRedirect = false)
        {
            //協定
            HttpWebRequest request;
            //協定
            request = WebRequest.CreateHttp(url);
            request.Method = "GET";
            request.UserAgent = YwCore.Common.USERAGENT;
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            request.Headers["Accept-Language"] = "zh-TW,zh;q=0.9,en-US;q=0.8,en;q=0.7";
            request.AllowAutoRedirect = isAllowAutoRedirect;
            request.KeepAlive = true;
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.CookieContainer = new CookieContainer();
            request.Proxy = string.IsNullOrEmpty(proxy) ? request.Proxy : new WebProxy(proxy);

            //防止COOKIE為NULL
            foreach (Cookie cookie in cookieCollection)
            {
                if (cookie != null)
                {
                    request.CookieContainer.Add(cookie);
                }
            }

            string html = string.Empty;

            try
            {
                //接收回應
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    if (response != null)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            html = reader.ReadToEnd();
                        }

                        foreach (Cookie cookie in response.Cookies)
                        {
                            if (cookie != null)
                            {
                                cookieCollection.Add(cookie);
                            }
                        }
                    }
                }
            }
            finally
            {
                request.Abort();
                request = null;
            }
            return html;
        }
    }
}
