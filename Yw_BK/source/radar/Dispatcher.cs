#region 功能修改描述與時間

/*
    描述：數據調度處理
    日期：2024-05-10
*/

#endregion

using GP_RadarService.Models;
using System.Data;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source.radar
{
    /// <summary>
    /// 數據調度處理
    /// </summary>
    internal class Dispatcher
    {
        private Dictionary<int, bool> producerHealth = new Dictionary<int, bool>();
        private ParserRadar parser = new ParserRadar();

        /// <summary>
        /// 數據處理
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="received"></param>
        public void MessageHandle(object? obj, Received received)
        {
            if (Config.remindInfos.TryGetValue(GpType.Zd.ToString("g"), out IRemind remindZd))
            {
                remindZd.SetLastTime(DateTime.Now);
            }

            if (Config.remindInfos.TryGetValue(GpType.Ds.ToString("g"), out IRemind remindDs))
            {
                remindDs.SetLastTime(DateTime.Now);
            }

            //賽事數據
            if (received.Game != null)
            {
                LogFile.SetLog(LogMode.Html, Source.Radar, Environment.NewLine + Newtonsoft.Json.JsonConvert.SerializeObject(received.Game), received.Game.ProducerId == 1 ? "Zd" : "Ds", received.Game.Id.ToString());
                GameDataHandle(received.Game);
            }
            //健康度數據
            else if (received.Health != null)
            {
                LogFile.SetLog(LogMode.WsEvent, Source.Radar, Environment.NewLine + Newtonsoft.Json.JsonConvert.SerializeObject(received.Health), "Health", DateTime.Now.ToString("yyyyMMddHH"));
                HealthHandle(received.Health);
            }
            //玩法於特定時間無效
            else if (received.BetCancelInfo != null)
            {
                LogFile.SetLog(LogMode.Html, Source.Radar, Environment.NewLine + Newtonsoft.Json.JsonConvert.SerializeObject(received.BetCancelInfo), "BetCancelInfo", DateTime.Now.ToString("yyyyMMddHH"));
                BetcancelParse(received.BetCancelInfo);
            }
            else
            {
                LogFile.SetLog(LogMode.WsEvent, Source.Radar, "heartbeat", "Health", DateTime.Now.ToString("yyyyMMddHH"), "DsZd");
            }
        }

        /// <summary>
        /// 賽事數據處理
        /// </summary>
        /// <param name="game"></param>
        protected void GameDataHandle(RadarGame game)
        {
            parser.Parse(game);
        }

        /// <summary>
        /// 健康度數據處理
        /// </summary>
        /// <param name="health"></param>
        protected void HealthHandle(IList<ProducerHealth> health)
        {
            List<GpType> down = new List<GpType>();
            List<GpType> up = new List<GpType>();
            string msg = string.Empty;
            var status = health.Select(a => (a.Id, a.IsDown, Msg: a.IsDown ? "異常" : "正常")).ToDictionary(a => a.Id);

            //ZD
            if (status.TryGetValue(1, out var current) && producerHealth.TryGetValue(1, out bool old))
            {
                if (current.IsDown && !old)
                {
                    down.Add(GpType.Zd);

                    if (Config.isFollow && Config.AbnormalHandleEnabled)
                    {
                        Config.CloseGame(GpType.Zd);
                    }
                }
                else if (!current.IsDown && old)
                {
                    up.Add(GpType.Zd);
                }
            }

            //DS
            if (status.TryGetValue(3, out current) && producerHealth.TryGetValue(3, out old))
            {
                if (current.IsDown && !old)
                {
                    down.Add(GpType.Ds);
                }
                else if (!current.IsDown && old)
                {
                    up.Add(GpType.Ds);
                }
            }

            if (down.Count > 0)
            {
                msg = $"籃球{Config.source.ToString("g")}-{string.Join("/", down.Select(a => a.ToString("g")))}來源異常，請檢查是否正常{Environment.NewLine}{Environment.NewLine}1.檢查賽事盤口水位是否正常{Environment.NewLine}有，請正常開放。{Environment.NewLine}否，請重啟跟盤。{Environment.NewLine}重啟跟盤後未恢復請切換備用來源，並檢查紀錄異常原因{Environment.NewLine}{Environment.NewLine}2.請勿勾上跟盤，賽事請改手開";
                Display.SetDictInfo(Display.Type.Remind, YwCore.Properties.Resources.SourceError);

                if (Config.isFollow)
                {
                    YwCore.Common.SendMessage.Send(msg);
                }
            }

            if (up.Count > 0)
            {
                msg = $"籃球{Config.source.ToString("g")}-{string.Join("/", up.Select(a => a.ToString("g")))}來源回復正常";

                if (Config.isFollow)
                {
                    YwCore.Common.SendMessage.Send(msg);
                }
            }

            if (status.Count(a => !a.Value.IsDown) == status.Count)
            {
                Display.SetDictInfo(Display.Type.Remind, "");
            }

            producerHealth = status.ToDictionary(a => a.Key, a => a.Value.IsDown);
        }

        /// <summary>
        ///  //玩法於特定時間無效處理
        /// </summary>
        /// <param name="betCancelInfo"></param>
        protected void BetcancelParse(BetCancelInfo betCancelInfo)
        {

        }

        /// <summary>
        /// 檢查雷達生產者是否未上線
        /// </summary>
        /// <param name="producerId"></param>
        /// <returns></returns>
        public bool CheckProducerIsDown(int producerId)
        {
            if (producerHealth.TryGetValue(producerId, out bool isDown))
            {
                return isDown;
            }
            else
            {
                return true;
            }
        }
    }
}
