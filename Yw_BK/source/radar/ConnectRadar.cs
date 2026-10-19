#region 功能修改描述與時間

/*
    描述：連線雷達Server
    日期：2024-05-10
*/

#endregion

using GP_RadarService.Client;
using GP_RadarService.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using YwCore;

namespace Yw_BK.source.radar
{
    /// <summary>
    /// 連線雷達服務
    /// </summary>
    public class ConnectRadar
    {
        /// <summary>
        /// 連線物件
        /// </summary>
        private ServiceConnect serviceConnect = new ServiceConnect();
        /// <summary>
        /// 訂閱球類
        /// </summary>
        public Subscribe.Sport Sport { get; }

        /// <summary>
        /// 是否已開通
        /// </summary>
        public bool IsOpen => serviceConnect.IsOpen;

        /// <summary>
        /// 數據接收事件
        /// </summary>
        public event EventHandler<Received> MessageReceived;

        /// <summary>
        /// 連線事件
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// 斷線事件
        /// </summary>
        public event EventHandler<string> Disconnected;

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="sport">球類</param>

        public ConnectRadar(Subscribe.Sport sport)
        {
            Sport = sport;
        }

        /// <summary>
        /// 啟動
        /// </summary>
        /// <param name="producerId">生產者</param>
        /// <param name="url">連線網址</param>
        /// <param name="proxy">代理</param>
        public async Task ConnectAsync(List<int> producerId, string url = "ws://127.0.0.1:9001/", string proxy = null)
        {
            try
            {
                serviceConnect?.Dispose();
                serviceConnect = new ServiceConnect();
                Uri uri = new Uri(url);
                WebProxy webProxy = !string.IsNullOrEmpty(proxy) ? new WebProxy(proxy) : null;
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(TimeSpan.FromSeconds(30));

                serviceConnect.Connected += async (s, e) =>
                {
                    Connected?.Invoke(s, e);

                    await Subscribe(producerId);
                };

                serviceConnect.Disconnect += (s, e) =>
                {
                    Disconnected?.Invoke(this, e);
                };

                serviceConnect.MessagingReceived += MessageReceived;

                await serviceConnect.Open(uri, CancellationToken.None, webProxy);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, Source.Radar, $"RadarConnectError: {ex}");
            }
        }

        /// <summary>
        /// 斷線
        /// </summary>
        /// <returns></returns>
        public async Task CloseAsync()
        {
            if (serviceConnect != null)
            {
                await serviceConnect.Close();
            }
        }

        /// <summary>
        /// 訂閱
        /// </summary>
        /// <param name="producerId"></param>
        /// <returns></returns>
        public async Task Subscribe(List<int> producerId)
        {
            Subscribe subscribe = new Subscribe()
            {
                ProducerId = producerId,
                SubscribeSport = new List<Subscribe.Sport>() { Sport }
            };

            await serviceConnect.Subscribe(subscribe, CancellationToken.None);
        }
    }

}
