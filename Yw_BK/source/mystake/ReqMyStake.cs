#region 功能修改描述與時間

/*
    描述：XB請求器
    日期：2019-05-16
    
    描述：調整使用內頁玩法數量判斷countevents參數
    日期：2021-07-23
*/

#endregion

using System;
using System.Net;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// MyStake請求器
    /// </summary>
    public class ReqMyStake
    {
        private static string innerUrl = @"https://wss-eu-uk1.ws-amazon.com/";

        /// <summary>
        /// 設定非同步要求器
        /// </summary>
        /// <param name="requestInfo">要求器</param>
        public static void SetHttpWebRequest(ReqInfo reqInfo, string proxy = null, decimal mainId = 0m)
        {
            string urlInfo = string.Empty;

            //GP類型,解析失敗則跳過
            GpType gpType;
            if (!Enum.TryParse(reqInfo.ThreadInfo.StrType, out gpType)) { return; }

            //url設定
            switch (gpType)
            {
                case GpType.Zd:
                    urlInfo = $"{innerUrl}api/cache/get?key=live/headernew/en";
                    break;
                case GpType.ZdMore:
                    urlInfo = $"{innerUrl}api/cache/get?key=live/gamenew/{mainId}";
                    break;
            }

            //協定
            reqInfo.Req = WebRequest.CreateHttp(urlInfo);
            reqInfo.Req.Method = "GET";
            reqInfo.Req.UserAgent = YwCore.Common.USERAGENT;
            reqInfo.Req.Accept = "*/*";
            reqInfo.Req.AllowAutoRedirect = true;
            reqInfo.Req.KeepAlive = true;
            reqInfo.Req.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            reqInfo.Req.Proxy = string.IsNullOrEmpty(proxy) ? reqInfo.Req.Proxy : new WebProxy(proxy);

            if (gpType == GpType.DsMore || gpType == GpType.ZpMore || gpType == GpType.ZdMore)
            {
                reqInfo.SourceMatchId = mainId;
                reqInfo.PostData = mainId.ToString();

            }
        }
    }
}
