#region 功能修改描述與時間

/*
    描述：LEO-PIN請求器
    日期：2021-02-09
    
    描述：添加來源維修防護
    日期：2021-03-16

    描述：調整可用帳號取得方式
    日期：2023-11-08
*/

#endregion

using System.Net;
using Yw_BK.model;
using YwCore;
using YwCore.source;

namespace Yw_BK.source
{
    /// <summary>
    /// LEO-PIN請求器
    /// </summary>
    internal class ReqNewPIN : IRequest
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
        public ReqNewPIN(long sendId, GpType gpType, string mainUrl, IReadOnlyDictionary<string, UserInfo> users)
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
            //盤數量
            int num = 100;

            long timeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            //_g 和 wm (亞洲版參數)
            string reqParams = Config.entrance == Entrance.Asia ? "_g=0&wm=dz" : "_g=0&wm=ld";
            //ZP用只抓隔日賽事
            string date = Config.isTomorrow ? DateTime.Now.AddHours(12).ToString("yyyy-MM-dd") : string.Empty;

            //設定來源網址
            switch (GPType)
            {
                case GpType.Ds:
                    urlInfo = $"{user.RedirectUrl}/sports-service/sv/odds/events?mk=1&sp=4&ot=1&btg=1&o=1&lg=&ev=&d=&l={num}&v=0&me=0&more=false&c=CN&tm=0&g=QQ%3D%3D&pa=0&cl={num}&{reqParams}&pimo=0%2C1&inl=false&_={timeStamp}&locale=en_US";
                    break;
                case GpType.Zd:
                    urlInfo = $"{user.RedirectUrl}/sports-service/sv/odds/events?mk=2&sp=4&ot=1&btg=1&o=1&lg=&ev=&d=&l={num}&v=0&me=0&more=false&c=CN&tm=0&g=QQ%3D%3D&pa=0&cl={num}&{reqParams}&pimo=0%2C1&inl=false&_={timeStamp}&locale=en_US";
                    break;
                case GpType.Zp:
                    urlInfo = $"{user.RedirectUrl}/sports-service/sv/odds/events?mk=0&sp=4&ot=1&btg=1&o=1&lg=&ev=&d={date}&l={num}&v=0&me=0&more=false&c=CN&tm=0&g=QQ%3D%3D&pa=0&cl={num}&{reqParams}&pimo=0%2C1&inl=false&_={timeStamp}&locale=en_US";
                    break;
                case GpType.Ac:
                    urlInfo = $"{user.RedirectUrl}/sports-service/sv/odds/events?mk=3&sp=4&ot=1&btg=100&o=1&lg=&ev=&d=&l={num}&v=0&me=0&more=false&c=CN&tm=0&g=QQ%3D%3D&pa=0&cl={num}&{reqParams}&inl=false&_={timeStamp}&locale=en_US";
                    break;
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
            LogFile.SetLog(LogMode.Html, Source.PIN, Config.entrance, str, GPType.ToString(), dateNow);

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
                if (responseInfo.Html.IndexOf("Your session has expired", StringComparison.OrdinalIgnoreCase) >= 0)
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
                else if (responseInfo.Html.IndexOf("<title>Website Under Maintenanced</title>", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    //設定警告訊息
                    Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.SourceMaintenance);
                    YwCore.Common.SendMessage.MaintenanceSend();
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
                        case GpType.Ac:
                            Config.acQueue.Enqueue(dataInfo);
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
            LogFile.SetLog(LogMode.Error, Source.PIN, Config.entrance, GPType.ToString() + Environment.NewLine + ex.ToString());

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
                user.WebExCount = 0;
                user.ClearStatus();
            }
        }
    }
}