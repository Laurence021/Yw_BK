#region 功能修改描述與時間

/*
    描述：PIN 請求器
    日期：2021-02-09
            
    描述：PIN 添加導回首頁登出帳號
    日期：2021-09-09

    描述：添加單隊總得分第二節請求
    日期：2023-02-15

    描述：可用帳號取得方式調整
    日期：2023-11-08
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Yw_BK.model;
using YwCore;
using YwCore.source;

namespace Yw_BK.source
{
    /// <summary>
    /// PIN請求器
    /// </summary>
    internal class ReqPIN : IRequest
    {
        //取User用的鎖
        private static object _locker = new object();
        /// <summary>
        /// 主網域
        /// </summary>
        public string MainUrl { get; set; }
        /// <summary>
        /// GP類型
        /// </summary>
        public GpType GPType { get; }

        /// <summary>
        /// 請求流水號
        /// </summary>
        public long SendId { get; }

        /// <summary>
        /// 設定值
        /// </summary>
        public IReadOnlyDictionary<string, UserInfo> Users { get; }

        /// <summary>
        /// 代理
        /// </summary>
        public string Proxy { get; set; }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="sendId"></param>
        /// <param name="gpType"></param>
        /// <param name="mainUrl"></param>
        /// <param name="users"></param>
        public ReqPIN(long sendId, GpType gpType, string mainUrl, IReadOnlyDictionary<string, UserInfo> users)
        {
            SendId = sendId;
            GPType = gpType;
            MainUrl = mainUrl;
            Users = users;
        }

        /// <summary>
        /// 取得可用帳號
        /// </summary>
        /// <returns></returns>
        public UserInfo GetAvailableUser()
        {
            lock (_locker)
            {
                var user = Users.Values.Where(y => y.Status == 1).OrderBy(y => y.SendTime).FirstOrDefault();

                if (user == null)
                {
                    return null;
                }
                else
                {
                    user.SendTime = DateTime.Now;
                    return user;
                }
            }
        }

        /// <summary>
        /// 設定請求Request
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public HttpWebRequest GetRequestSetting(out UserInfo user)
        {
            user = GetAvailableUser();

            if (user == null)
            {
                return null;
            }

            string urlInfo = string.Empty;
            string url = MainUrl.Replace("www", "members");

            //設定來源網址
            switch (GPType)
            {
                case GpType.Ds:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Today/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.Zd:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Live/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.Zp:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Early%20Markets/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.Xb:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/2nd%20Half/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.Dj:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/1st%20Quarter/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.DjQ2:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/2nd%20Quarter/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.Ac:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Team%20Total/1/null/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.AcHT:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Team%20Total/1/1/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.AcQ1:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Team%20Total/1/3/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
                case GpType.AcQ2:
                    urlInfo = string.Concat(url, "Sportsbook/Asia/en-GB/GetLines/Basketball/Team%20Total/1/4/0/Regular/SportsBookAll/Decimal/77/false/?buySellLevels=");
                    break;
            }

            //設定賽事聯盟ID取得鴛鴦盤
            if (ParserPIN.codeJson == "")
            {
                urlInfo += "{}";
            }
            else
            {
                urlInfo += "{" + ParserPIN.codeJson.TrimEnd(new[] { ',' }) + "}";
            }

            return SetRequest(urlInfo, user, Proxy);
        }

        /// <summary>
        /// Request基礎設置
        /// </summary>
        /// <param name="url">請求網址</param>
        /// <param name="cookieCollection">cookie集合</param>
        /// <param name="proxy">代理</param>
        /// <returns></returns>
        private HttpWebRequest SetRequest(string url, UserInfo userInfo, string proxy)
        {
            HttpWebRequest webRequest = WebRequest.CreateHttp(url);
            webRequest.Method = "GET";
            webRequest.UserAgent = YwCore.Common.USERAGENT;
            webRequest.Accept = "application/json, text/javascript, */*";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.AllowAutoRedirect = true;
            webRequest.KeepAlive = true;
            webRequest.CookieContainer = new CookieContainer();
            webRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            webRequest.Proxy = string.IsNullOrEmpty(proxy) ? webRequest.Proxy : new WebProxy(proxy);
            webRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");

            if (userInfo != null)
            {
                webRequest.CookieContainer.Add(YwCore.Common.CopyCookieCollection(userInfo.CookieCollection));
            }

            return webRequest;
        }

        /// <summary>
        /// 回傳內容處理
        /// </summary>
        /// <param name="responseInfo"></param>
        /// <param name="user"></param>
        public void ResponseHandle(ResponseInfo responseInfo, UserInfo user)
        {
            #region 數據寫入日誌

            DateTime dateTime = DateTime.Now;
            string recordTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffff");
            string str = $"{Environment.NewLine}{recordTime} | {GPType} | SendId:{SendId} | User:{user.User}{Environment.NewLine}{responseInfo.Html}";
            string dateNow = dateTime.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString("00");
            LogFile.SetLog(LogMode.Html, Source.PIN, str, GPType.ToString(), dateNow);

            #endregion

            //無數據則提示或切換代理
            if (string.IsNullOrEmpty(responseInfo.Html))
            {
                if (Config.Proxy.Next())
                {
                    Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.NoContent + Environment.NewLine + YwCore.Properties.Resources.ChangeProxy);
                }
                else
                {
                    Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.NoContent + "," + YwCore.Properties.Resources.CheckUrl);
                }
            }
            else
            {
                if (responseInfo.Html.IndexOf("Your session has expired", StringComparison.OrdinalIgnoreCase) >= 0 || responseInfo.Html.StartsWith("<!doctype html>"))
                {
                    user.ClearStatus();
                    //設定警告訊息
                    Display.SetDictInfo(Display.Type.Warnning, "帳號:" + user.User + " 快取過期");
                    return;
                }
                else if (responseInfo.Html.IndexOf("<title>Object moved</title>", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
                else
                {
                    user.WebExCount = 0;

                    DataInfo dataInfo = new DataInfo(GPType.ToString("g"), responseInfo.Html, SendId);

                    switch (GPType)
                    {
                        case GpType.Ds:
                            Config.dsQueue.Enqueue(dataInfo);
                            break;
                        case GpType.Zd:
                            Config.zdQueue.Enqueue(dataInfo);
                            break;
                        case GpType.Zp:
                            Config.zpQueue.Enqueue(dataInfo);
                            break;
                        case GpType.Dj:
                            Config.djQueue.Enqueue(dataInfo);
                            break;
                        case GpType.DjQ2:
                            Config.djQ2Queue.Enqueue(dataInfo);
                            break;
                        case GpType.Xb:
                            Config.xbQueue.Enqueue(dataInfo);
                            break;
                        case GpType.Ac:
                            Config.acQueue.Enqueue(dataInfo);
                            break;
                        case GpType.AcHT:
                            Config.acHtQueue.Enqueue(dataInfo);
                            break;
                        case GpType.AcQ1:
                            Config.acQ1Queue.Enqueue(dataInfo);
                            break;
                        case GpType.AcQ2:
                            Config.acQ2Queue.Enqueue(dataInfo);
                            break;
                    }
                }
            }            
        }

        /// <summary>
        /// 回傳錯誤處理
        /// </summary>
        /// <param name="responseInfo"></param>
        /// <param name="user"></param>
        public void ErrorHandle(Exception ex, UserInfo user)
        {
            LogFile.SetLog(LogMode.Error, Source.PIN, GPType.ToString() + Environment.NewLine + ex.ToString());

            string html = "";

            using (var response = (ex.InnerException as WebException)?.Response)
            {
                if (response != null)
                {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), System.Text.Encoding.UTF8))
                    {
                        html = reader.ReadToEnd();
                    }
                }
            }

            if (html.Contains("Your session has expired") || ++user.WebExCount >= 5)
            {
                user.ClearStatus();
            }
        }
    }
}