#region 功能修改描述與時間

/*
    描述：GP組態
    日期：2019-01-07

    描述：調整主客隊總得分玩法ZD屏蔽OE
    日期：2020-09-02

    描述：啟動/停止GP 添加設定RMQ時間戳 [配合YwCore(1.0.2.68)]
    日期：2020-12-23

    描述：外網來源添加排除聯盟清單功能 [配合YwCore(1.0.6.67)]
    日期：2024-02-27

    描述：外網自動移盤新需求
    日期：2025-04-11

    描述：聯盟名稱替換功能服務化
    日期：2026-04-01

    描述：隊伍名稱替換功能
    日期：2026-09-11
*/

#endregion

using Gp.Model;
using Gp.Service;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Yw_BK.main;
using Yw_BK.model.logInfo;
using YwCore;
using YwCore.main;
using YwCore.model;
using YwCore.model.interfaces;
using ZDBD.Service;

namespace Yw_BK.model
{
    public static class Config
    {
        /// <summary>
        /// 靜態建構子
        /// </summary>
        static Config()
        {
            OnConfigChanged += () =>
            {
                foreach (var info in remindInfos.Values)
                {
                    info.RefreshEnabled();
                }
            };
        }

        //是否啟動
        private static bool _isFollow;
        /// <summary>
        /// 是否開始登入流程
        /// </summary>
        public static bool IsLogin;
        /// <summary>
        /// 版本號
        /// </summary>
        public static string version = $"1.1.7.12({new DirectoryInfo(Application.StartupPath).Parent.Name.Replace(".", "")})";
        /// <summary>
        /// 來源網址
        /// </summary>
        public static string url;
        /// <summary>
        /// 入口設置
        /// </summary>
        public static string[] ChooseSet;
        /// <summary>
        /// 新比賽庫Odds
        /// </summary>
        public static BkOddsCacheService.BkOddsCacheServiceClient OddsCacheServiceClient;
        /// <summary>
        /// 新比賽庫Game
        /// </summary>
        public static BkGameCacheService.BkGameCacheServiceClient GameCacheServiceClient;
        /// <summary>
        /// 主庫Odds
        /// </summary>
        public static BkOddsService.BkOddsServiceClient OddsServiceClient;
        /// <summary>
        /// 主庫Game
        /// </summary>
        public static BkGameService.BkGameServiceClient GameServiceClient;
        /// <summary>
        /// ZDBD服務
        /// </summary>
        public static ZdbdService.ZdbdServiceClient ServiceClient;
        /// <summary>
        /// GP監控註冊服務
        /// </summary>
        public static RegisterMonitor RegisterMonitor { get; set; }
        /// <summary>
        /// 帳號使用監控服務
        /// </summary>
        public static AccountMonitor AccountMonitor { get; set; }
        /// <summary>
        /// 替換名稱監控服務
        /// </summary>
        public static ReplaceNameMonitor ReplaceNameMonitor { get; set; }
        /// <summary>
        /// 是否啟動
        /// </summary>
        public static bool isFollow
        {
            get
            {
                return _isFollow;
            }
            set
            {
                _isFollow = value;

                if (value)
                {
                    remindInfos.Values.Where(a => a.GetEnabled() && (a.GetSendFlag() & 3) != 3).ToList().ForEach(a => a.SetLastTime(DateTime.Now));
                    YwCore.Common.SendMessage.Send($"{gpName}籃球已開始GP");
                    //啟動定時查詢系統設定 
                    StreamInfo.Start();
                    //刷新緩存
                    Task.Run(() => { SetField.RefreshWebNameBuf(null); });
                }
                else
                {
                    YwCore.Common.SendMessage.Send($"{gpName}籃球已停止GP");
                    EventCloseClearSourceOpenStatus();//檢查重開放清單是否要寫入關閉
                    ConfigChange();
                    //停止定時查詢系統設定 
                    StreamInfo.Stop();
                }
            }
        }
        /// <summary>
        /// 執行緒集合
        /// </summary>
        public static ConcurrentDictionary<GpType, ThreadInfo> threadInfos = new ConcurrentDictionary<GpType, ThreadInfo>();
        /// <summary>
        /// 運動類型
        /// </summary>
        public static readonly Sport sport = Sport.BK;
        /// <summary>
        /// 來源
        /// </summary>
        public static Source source = Source.None;
        /// <summary>
        /// 語系
        /// </summary>
        public static Lang lang = Lang.None;
        /// <summary>
        /// 所登入的平台
        /// </summary>
        public static Entrance entrance = Entrance.None;
        /// <summary>
        /// GP訊息
        /// </summary>
        public static string gpName { get { return entrance != Entrance.None ? $"({entrance.GetDescriptionText()}){source.GetDescriptionText()}" : $"{source.GetDescriptionText()}"; } }
        /// <summary>
        /// GP類型
        /// </summary>
        public static GpType gpType = GpType.None;
        /// <summary>
        /// 賽事字典 [key:賽事字串 value:物件]
        /// </summary>
        public static ConcurrentDictionary<string, GameInfo> gameDic = new ConcurrentDictionary<string, GameInfo>();
        /// <summary>
        /// gId字典  [key:gId  value:賽事key}
        /// </summary>
        public static ConcurrentDictionary<long, string> gIdDic = new ConcurrentDictionary<long, string>();
        /// <summary>
        /// 庫裡新比賽表緩存
        /// </summary>
        public static ConcurrentBag<GameCache> gameCacheBag = new ConcurrentBag<GameCache>();
        /// <summary>
        /// 不GP聯盟 ｜key：聯盟唯一值game.EndGType + game.League.Name + game.Suffix.Name｜value：添加時間
        /// </summary>
        public static BlockGameList BlockList = new BlockGameList();
        /// <summary>
        /// 提醒訊息任務集合
        /// </summary>
        public static ConcurrentDictionary<string, IRemind> remindInfos { get; set; } = new ConcurrentDictionary<string, IRemind>();
        /// <summary>
        /// 解析排除聯盟列表
        /// </summary>
        public static HashSet<string> FilterLeagueHashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// NBA聯盟排除清單字典(識別成其他聯盟)  key:AP名稱(BK-XB) value:清單
        /// </summary>
        public static Dictionary<string, HashSet<string>> nbaleagueRecludeDic = new Dictionary<string, HashSet<string>>();
        /// <summary>
        /// 來源發生異常防護(脫勾關小格)
        /// </summary>
        public static bool AbnormalHandleEnabled { get; set; }
        /// <summary>
        /// 設定檔變動事件委派
        /// </summary>
        public delegate void ConfigChanged();
        /// <summary>
        /// 設定檔變動事件
        /// </summary>
        public static event ConfigChanged OnConfigChanged;
        /// <summary>
        /// 觸發設定檔變動事件
        /// </summary>
        public static void ConfigChange()
        {
            OnConfigChanged?.Invoke();
        }
        /// <summary>
        /// 更新時間戳事件
        /// </summary>
        public static void LastTimeChange(SubscribeType gpType)
        {
            //註記提醒任務最後更新時間戳
            if (remindInfos.TryGetValue(gpType.ToString("g"), out IRemind remind))
            {
                remind.SetLastTime(DateTime.Now);
            }
        }
        /// <summary>
        /// 關閉監控處理
        /// </summary>
        public static CloseNotifyHandle CloseNotify = new CloseNotifyHandle();

        /// <summary>
        /// 變更Form畫面
        /// </summary>
        public static IChangeFormDesign ChangeFormDesign;
        /// <summary>
        /// 是否為三對三籃球
        /// </summary>
        public static bool Is3x3(string name)
        {
            return !string.IsNullOrEmpty(name) && Regex.Match(name, @"[3３]\s?[xхXＸ＊*]\s?[3３]|三對三|三对三", RegexOptions.Compiled | RegexOptions.IgnoreCase).Success;
        }

        #region 主庫相關

        /// <summary>
        /// 查詢主庫kMode佇列
        /// </summary>
        public static ConcurrentQueue<KModeSelectRequest> dtKModeQueue = new ConcurrentQueue<KModeSelectRequest>();

        /// <summary>
        /// 查詢主庫odds差距值佇列
        /// </summary>
        public static ConcurrentQueue<OddsSelectRequest> dtOddsQueue = new ConcurrentQueue<OddsSelectRequest>();

        #endregion

        #region 來源數據佇列

        /// <summary>
        /// Ds佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> dsQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Zd佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> zdQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Zp佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> zpQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Dj佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> djQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// DjQ2佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> djQ2Queue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Xb佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> xbQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Ac佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> acQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Ac上半佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> acHtQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Ac第一節佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> acQ1Queue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Ac第二節佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> acQ2Queue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Ds更多玩法佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> dsMoreQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Zd更多玩法佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> zdMoreQueue = new ConcurrentQueue<DataInfo>();
        /// <summary>
        /// Zp更多玩法佇列
        /// </summary>
        public static ConcurrentQueue<DataInfo> zpMoreQueue = new ConcurrentQueue<DataInfo>();

        #endregion

        #region 參數設置

        /// <summary>
        /// 是否使用代理
        /// </summary>
        public static bool isProxy;
        /// <summary>
        /// 代理功能
        /// </summary>
        public static ProxySwitch Proxy { get; set; }
        /// <summary>
        /// 是否切換GP
        /// </summary>
        public static bool isTransfer;
        /// <summary>
        /// 是否PL關閉清零
        /// </summary>
        public static bool isOddsClearZero;
        /// <summary>
        /// 是否跟奧運(JZ用)
        /// </summary>
        public static bool isTrackOlympics;

        #region Ds

        /// <summary>
        /// 是否開啟Ds
        /// </summary>
        public static bool isOpenDs;
        /// <summary>
        /// 是否開啟Ds1H
        /// </summary>
        public static bool isOpenDsHt;
        /// <summary>
        /// 是否開啟DsHtOe
        /// </summary>
        public static bool isOpenDsHtOe;
        /// <summary>
        /// 是否開啟DsZpOe
        /// </summary>
        public static bool isOpenDsZpOe;
        /// <summary>
        /// 是否開啟DsMore
        /// </summary>
        public static bool isOpenDsMore;
        /// <summary>
        /// 是否只跟PK
        /// </summary>
        public static bool isDsOnlyPK;
        /// <summary>
        /// DsZp_OE_PL
        /// </summary>
        public static decimal dsOERatio;
        /// <summary>
        /// Ds間隔秒數
        /// </summary>
        public static int dsSec;
        /// <summary>
        /// DsMore間隔秒數
        /// </summary>
        public static int dsMoreSec;
        /// <summary>
        /// Ds RF差距
        /// </summary>
        public static decimal gapDsRf;
        /// <summary>
        /// Ds OU差距
        /// </summary>
        public static decimal gapDsOu;
        /// <summary>
        /// Ds OE差距
        /// </summary>
        public static decimal gapDsOe;
        /// <summary>
        /// Ds 1*2差距
        /// </summary>
        public static decimal gapDsDy;
        /// <summary>
        /// Ds OE倍率
        /// </summary>
        public static decimal ratioDsOe;
        /// <summary>
        /// Ds 1*2倍率
        /// </summary>
        public static decimal ratioDsDy;

        #endregion

        #region Zd

        /// <summary>
        /// 是否開啟Zd
        /// </summary>
        public static bool isOpenZd;
        /// <summary>
        /// 是否開啟Zd1H
        /// </summary>
        public static bool isOpenZdHt;
        /// <summary>
        /// 是否開啟ZdHtOe
        /// </summary>
        public static bool isOpenZdHtOe;
        /// <summary>
        /// 是否開啟ZdOe
        /// </summary>
        public static bool isOpenZdOe;
        /// <summary>
        /// 是否開啟ZdMore
        /// </summary>
        public static bool isOpenZdMore;
        /// <summary>
        /// 是否只跟PK
        /// </summary>
        public static bool isZdOnlyPK;
        /// <summary>
        /// 是否開啟OT轉換正盤
        /// </summary>
        public static bool isOTConvertML;
        /// <summary>
        /// 是否開啟NBA轉換正盤
        /// </summary>
        public static bool isNBAConvertML;
        /// <summary>
        /// Zd_OE_PL
        /// </summary>
        public static decimal zdOERatio;
        /// <summary>
        /// Zd間隔秒數
        /// </summary>
        public static int zdSec;
        /// <summary>
        /// ZdMore間隔秒數
        /// </summary>
        public static int zdMoreSec;
        /// <summary>
        /// Zd RF差距
        /// </summary>
        public static decimal gapZdRf;
        /// <summary>
        /// Zd OU差距
        /// </summary>
        public static decimal gapZdOu;
        /// <summary>
        /// Zd OE差距
        /// </summary>
        public static decimal gapZdOe;
        /// <summary>
        /// Zd 1*2差距
        /// </summary>
        public static decimal gapZdDy;
        /// <summary>
        /// Zd OE倍率
        /// </summary>
        public static decimal ratioZdOe;
        /// <summary>
        /// Zd 1*2倍率
        /// </summary>
        public static decimal ratioZdDy;
        /// <summary>
        /// 自動移到ZD頁
        /// </summary>
        public static bool AutoCreateZdGame { get; set; }

        #endregion

        #region Zp

        /// <summary>
        /// 是否只抓明日賽事
        /// </summary>
        public static bool isTomorrow;
        /// <summary>
        /// 是否開啟Zp
        /// </summary>
        public static bool isOpenZp;
        /// <summary>
        /// 是否開啟Zp1H
        /// </summary>
        public static bool isOpenZpHt;
        /// <summary>
        /// 是否開啟ZpHtOe
        /// </summary>
        public static bool isOpenZpHtOe;
        /// <summary>
        /// 是否開啟ZpMore
        /// </summary>
        public static bool isOpenZpMore;
        /// <summary>
        /// Zp間隔秒數
        /// </summary>
        public static int zpSec;
        /// <summary>
        /// ZpMore間隔秒數
        /// </summary>
        public static int zpMoreSec;
        /// <summary>
        /// Zp RF差距
        /// </summary>
        public static decimal gapZpRf;
        /// <summary>
        /// Zp OU差距
        /// </summary>
        public static decimal gapZpOu;
        /// <summary>
        /// Zp OE差距
        /// </summary>
        public static decimal gapZpOe;
        /// <summary>
        /// Zp 1*2差距
        /// </summary>
        public static decimal gapZpDy;
        /// <summary>
        /// Zp OE倍率
        /// </summary>
        public static decimal ratioZpOe;
        /// <summary>
        /// Zp 1*2倍率
        /// </summary>
        public static decimal ratioZpDy;

        #endregion

        #region Other

        /// <summary>
        /// 鴛鴦盤數量[0:全部 1:1盤 2:3盤 3:5盤] SB XB 此設定只影響解析NBA DS 全上的鴛鴦盤
        /// </summary>
        public static int sameCount;
        /// <summary>
        /// HT、Dj是否跟鴛鴦盤
        /// </summary>
        public static bool isSbDjSame;
        /// <summary>
        /// 是否開啟Dj
        /// </summary>
        public static bool isOpenDj;
        /// <summary>
        /// 是否開啟Xb
        /// </summary>
        public static bool isOpenXb;
        /// <summary>
        /// 是否開啟Xb
        /// </summary>
        public static bool isOpenAc;
        /// <summary>
        /// Dj間隔秒數
        /// </summary>
        public static int djSec;
        /// <summary>
        /// Xb間隔秒數
        /// </summary>
        public static int xbSec;
        /// <summary>
        /// Ac間隔秒數
        /// </summary>
        public static int acSec;
        /// <summary>
        /// HT RF差距
        /// </summary>
        public static decimal gapHtRf;
        /// <summary>
        /// HT OU差距
        /// </summary>
        public static decimal gapHtOu;
        /// <summary>
        /// HT OE差距
        /// </summary>
        public static decimal gapHtOe;
        /// <summary>
        /// HT 1*2差距
        /// </summary>
        public static decimal gapHtDy;
        /// <summary>
        /// XB RF差距
        /// </summary>
        public static decimal gapXbRf;
        /// <summary>
        /// XB OU差距
        /// </summary>
        public static decimal gapXbOu;
        /// <summary>
        /// XB OE差距
        /// </summary>
        public static decimal gapXbOe;
        /// <summary>
        /// XB 1*2差距
        /// </summary>
        public static decimal gapXbDy;
        /// <summary>
        /// Dj RF差距
        /// </summary>
        public static decimal gapDjRf;
        /// <summary>
        /// Dj OU差距
        /// </summary>
        public static decimal gapDjOu;
        /// <summary>
        /// Dj OE差距
        /// </summary>
        public static decimal gapDjOe;
        /// <summary>
        /// Dj 1*2差距
        /// </summary>
        public static decimal gapDjDy;

        #endregion

        #endregion

        #region 來源共用

        /// <summary>
        /// 聯盟替換功能
        /// </summary>
        public static ReplaceLeagueService GetReplaceLeagueService { get; set; }
        /// <summary>
        /// 隊伍替換功能
        /// </summary>
        public static ReplaceTeamService GetReplaceTeamService { get; set; }
        /// <summary>
        /// 蒐集所有待查詢的kMode至佇列(查詢加總)
        /// </summary>
        public static void CollectKindModeToQueue()
        {
            //啟動才處理
            if (isFollow)
            {
                //[主庫]暫存表列表
                List<KModeSelectRequest> requestList = new List<KModeSelectRequest>();
                //[主庫]需要查詢kMode的列表
                KModeSelectRequest request = new KModeSelectRequest() { Sport = (int)sport };
                //暫存待查詢kMode的字典
                ConcurrentDictionary<string, KModeSelectDto> kModeDic = new ConcurrentDictionary<string, KModeSelectDto>();

                foreach (GameInfo gameInfo in gameDic.Values)
                {
                    foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                    {
                        foreach (KInfo kInfo in gpdmPair.Value.Values)
                        {
                            //同kType setType只新增一筆
                            foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                            {
                                //非新比賽頁面才查詢
                                if (bInfo.Status > 0)
                                {
                                    var kmodeSelect = new KModeSelectDto { Gpdm = Convert.ToInt64(gpdmPair.Key), GId = gameInfo.GId, KType = kInfo.KType, SetType = kInfo.SetType, SourceId = (int)source };
                                    kModeDic.TryAdd(kmodeSelect.ToString(), kmodeSelect);
                                }
                                break;
                            }
                        }
                    }
                }

                //加入資料表
                foreach (var obj in kModeDic.Values)
                {
                    request.Data.Add(obj);

                    //超過筆數則分批查詢
                    if (request.Data.Count > 2000)
                    {
                        requestList.Add(request);
                        request = new KModeSelectRequest() { Sport = (int)sport };
                    }
                }

                //加入列表
                if (request.Data.Count > 0) { requestList.Add(request); }

                if (requestList.Count > 0)
                {
                    Task.Delay(1000).ContinueWith(t =>
                    {
                        //[主庫]分批放入佇列
                        foreach (var batch in requestList)
                        {
                            dtKModeQueue.Enqueue(batch);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 蒐集所有Odds至佇列(查詢差距值)
        /// </summary>
        public static void CollectOddsToQueue()
        {
            //啟動才處理
            if (isFollow)
            {
                //[主庫]暫存表列表
                List<OddsSelectRequest> requestList = new List<OddsSelectRequest>();
                //[主庫]需要查詢odds的列表
                OddsSelectRequest request = new OddsSelectRequest() { Sport = (int)sport };
                //篩選出有Id的賽事
                List<GameInfo> gameList = (from game in gameDic.Values
                                           where game.Id > 0 && game.ObjDic.Count > 0
                                           select game).ToList();
                //挑選須查詢的玩法
                foreach (GameInfo gameInfo in gameList)
                {
                    foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                    {
                        GpdmInfo tmpDic = gpdmPair.Value;

                        foreach (KInfo kInfo in tmpDic.Values)
                        {
                            List<BInfo> bList = (from bInfo in kInfo.BInfoDic.Values
                                                 where bInfo.Id > 0
                                                 select bInfo).ToList();
                            foreach (BInfo bInfo in bList)
                            {
                                request.Data.Add(new OddsSelectDto
                                {
                                    Gpdm = Convert.ToInt64(gpdmPair.Key),
                                    Id = bInfo.Id,
                                    GId = gameInfo.GId,
                                    KType = kInfo.KType,
                                    BType = bInfo.BType,
                                    SetType = Convert.ToInt32(kInfo.SetType),
                                    SourceId = Convert.ToInt32(source)
                                });
                            }
                        }
                    }

                    //超過筆數換下一群組(分批查詢)
                    if (request.Data.Count > Common.eachBatchLimit)
                    {
                        requestList.Add(request);
                        request = new OddsSelectRequest() { Sport = (int)sport };
                    }
                }

                //最後一個dataTable加入集合
                if (request.Data.Count > 0) { requestList.Add(request); }

                //[主庫]分批放入佇列
                foreach (var batch in requestList)
                {
                    dtOddsQueue.Enqueue(batch);
                }
            }
        }

        /// <summary>
        /// 取得對應KType值
        /// </summary>
        /// <param name="gpType">GP類型</param>
        /// <param name="specials">特殊玩法</param>
        /// <param name="type">哪類玩法(1:RF, 2:OU, 3:DY, 4:OE)</param>
        /// <param name="kType"></param>
        /// <returns></returns>
        public static bool SetKindType(GpType gpType, int specials, int type, out int kType)
        {
            kType = 0;

            //全場
            if (specials == 0)
            {
                switch (type)
                {
                    case 1:
                        kType = 12001;
                        break;
                    case 2:
                        kType = 12002;
                        break;
                    case 3:
                        kType = gpType != GpType.Zd ? 12003 : 0; //ZD不開
                        break;
                    case 4:
                        kType = 12004;
                        break;
                }
            }
            //上半
            else if (specials == 1)
            {
                switch (type)
                {
                    case 1:
                        kType = 12101;
                        break;
                    case 2:
                        kType = 12102;
                        break;
                    case 3:
                        kType = gpType != GpType.Zd ? 12103 : 0; //ZD不開
                        break;
                    case 4:
                        kType = 12104;
                        break;
                }
            }
            //下半
            else if (specials == 2)
            {
                switch (type)
                {
                    case 1:
                        kType = 12201;
                        break;
                    case 2:
                        kType = 12202;
                        break;
                    case 3:
                        kType = 0;
                        break;
                    case 4:
                        kType = 12204;
                        break;
                }
            }
            //單節
            else if (specials >= 3 && specials <= 6)
            {
                switch (type)
                {
                    case 1:
                        kType = specials == 3 ? 12301 : specials == 4 ? 12401 : specials == 5 ? 12501 : 12601;
                        break;
                    case 2:
                        kType = specials == 3 ? 12302 : specials == 4 ? 12402 : specials == 5 ? 12502 : 12602;
                        break;
                    case 3:
                        kType = gpType == GpType.Zd ? 0 : specials == 3 ? 12303 : specials == 4 ? 12403 : specials == 5 ? 12503 : 12603;
                        break;
                    case 4:
                        kType = specials == 3 ? 12304 : specials == 4 ? 12404 : specials == 5 ? 12504 : 12604;
                        break;
                }
            }
            //搶首
            else if (specials == 60 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12021;
                        break;
                }
            }
            //首先得分讓分
            else if (specials > 110 && specials < 120 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = specials == 111 ? 12209 : specials == 112 ? 12409 : 12609;
                        break;
                }
            }
            //搶尾
            else if (specials == 70 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12022;
                        break;
                }
            }
            //最後得分讓分
            else if (specials > 120 && specials < 130 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = specials == 121 ? 12109 : specials == 122 ? 12309 : 12509;
                        break;
                }
            }
            //單節最高得分
            else if (specials == 80 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12023;
                        break;
                }
            }
            //先得N分
            else if (specials > 100 && specials < 110 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = specials == 101 ? 12012 : specials == 102 ? 12112 : specials == 103 ? 12212 : specials == 104 ? 12312 : specials == 105 ? 12412 : specials == 106 ? 12512 : 12612;
                        break;
                }
            }
            //三分球總數
            else if (specials == 13)
            {
                switch (type)
                {
                    case 1:
                        kType = 12701;
                        break;
                    case 2:
                        kType = 12702;
                        break;
                    case 4:
                        kType = 12703;
                        break;
                }
            }
            //總得分
            else if (specials >= 14 && specials < 30)
            {
                //主隊
                if ((specials % 2) == 0)
                {
                    switch (type)
                    {
                        case 2:
                            kType = specials == 16 ? 12105 : specials == 18 ? 12205 : specials == 20 ? 12305 : specials == 22 ? 12405 : specials == 24 ? 12505 : specials == 26 ? 12605 : 12005;
                            break;
                        case 4:

                            if (gpType != GpType.Zd)
                            {
                                kType = specials == 16 ? 12106 : specials == 18 ? 12206 : specials == 20 ? 12306 : specials == 22 ? 12406 : specials == 24 ? 12506 : specials == 26 ? 12606 : 12006;
                            }

                            break;
                    }
                }
                //客隊
                else
                {
                    switch (type)
                    {
                        case 2:
                            kType = specials == 17 ? 12107 : specials == 19 ? 12207 : specials == 21 ? 12307 : specials == 23 ? 12407 : specials == 25 ? 12507 : specials == 27 ? 12607 : 12007;
                            break;
                        case 4:

                            if (gpType != GpType.Zd)
                            {
                                kType = specials == 17 ? 12108 : specials == 19 ? 12208 : specials == 21 ? 12308 : specials == 23 ? 12408 : specials == 25 ? 12508 : specials == 27 ? 12608 : 12008;
                            }

                            break;
                    }
                }
            }
            //球隊大三元
            else if (specials >= 30 && specials < 40 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = specials == 31 ? 12704 : specials == 32 ? 12707 : specials == 33 ? 12710 : specials == 34 ? 12713 : specials == 35 ? 12716 : 12719;
                        break;
                    case 2:
                        kType = specials == 31 ? 12705 : specials == 32 ? 12708 : specials == 33 ? 12711 : specials == 34 ? 12714 : specials == 35 ? 12717 : 12720;
                        break;
                    case 4:
                        kType = specials == 31 ? 12706 : specials == 32 ? 12709 : specials == 33 ? 12712 : specials == 34 ? 12715 : specials == 35 ? 12718 : 12721;
                        break;
                }
            }
            //獲得籃板球
            else if (specials == 40 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12803;
                        break;
                }
            }
            //暫停
            else if (specials == 41 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12804;
                        break;
                }
            }
            //獲得兩分球
            else if (specials == 130 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12801;
                        break;
                }
            }
            //犯規
            else if (specials > 150 && specials < 160 && gpType != GpType.Zd)
            {
                switch (type)
                {
                    case 1:
                        kType = 12802;
                        break;
                }
            }
            //罰球
            else if (specials > 160 && specials < 200 && gpType != GpType.Zd)
            {
                int scene = specials > 160 && specials < 170 ? 0 : specials > 170 && specials < 180 ? 1 : specials > 180 && specials < 190 ? 2 : 3;

                switch (type)
                {
                    case 1:
                        kType = scene == 0 ? 12011 : scene == 1 ? 12411 : scene == 2 ? 12511 : 12611;
                        break;
                }
            }
            //三分球
            else if (specials > 260 && specials < 300 && gpType != GpType.Zd)
            {
                int scene = specials > 260 && specials < 270 ? 0 : specials > 270 && specials < 280 ? 1 : specials > 280 && specials < 290 ? 2 : 3;

                switch (type)
                {
                    case 1:
                        kType = scene == 0 ? 12010 : scene == 1 ? 12410 : scene == 2 ? 12510 : 12610;
                        break;
                }
            }

            return kType == 0 ? false : true;
        }

        /// <summary>
        /// 取得各玩法類型基本KINFO集合
        /// </summary>
        /// <param name="gpType">GP類型</param>
        /// <param name="specials">玩法標題分類</param>
        /// <param name="isMaster">是否為主盤</param>
        /// <returns></returns>
        public static string[] GetKinfoArray(GpType gpType, int specials, bool isMaster)
        {
            string[] play = new string[0];

            switch (specials)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:

                    if (gpType == GpType.Ds)
                    {
                        if (isMaster)
                        {
                            play = new string[] { "RF", "DX", "DS", "DY" };
                        }
                        else
                        {
                            play = new string[] { "RF", "DX" };
                        }
                    }
                    else
                    {
                        if (isMaster)
                        {
                            play = new string[] { "RF", "DX", "DS" };
                        }
                        else
                        {
                            play = new string[] { "RF", "DX" };
                        }
                    }

                    break;
                case 13:
                    play = new string[] { "RF", "DX", "DS" };

                    break;
                default:

                    //主客隊總得分只有DS才需要開OE
                    if (specials >= 14 && specials < 30 && gpType == GpType.Ds)
                    {
                        play = new string[] { "DX", "DS" };
                    }
                    else if (specials >= 30 && specials < 40)
                    {
                        play = new string[] { "RF", "DX", "DS" };
                    }

                    break;
            }

            return play;
        }

        /// <summary>
        /// 取得bType
        /// </summary>
        /// <param name="kType"></param>
        /// <param name="serType"></param>
        /// <param name="b">兩項/三項</param>
        /// <returns></returns>
        public static int GetBType(int kType, byte setType, int b)
        {
            switch (kType)
            {
                case 12012:
                case 12112:
                case 12212:
                case 12312:
                case 12412:
                case 12512:
                case 12612:
                    return kType * 1000 + setType * 10 + b;
                case 12010:
                case 12011:
                case 12109:
                case 12209:
                case 12309:
                case 12409:
                case 12410:
                case 12411:
                case 12509:
                case 12510:
                case 12511:
                case 12609:
                case 12610:
                case 12611:
                case 12801:
                case 12802:
                case 12803:
                case 12804:
                    return kType * 100 + setType * 10 + b;
                default:
                    return kType * 10 + b;

            }
        }

        /// <summary>
        /// 停止或程式終止，檢查重開放清單是否要寫入關閉
        /// </summary>
        internal static void EventCloseClearSourceOpenStatus()
        {
            try
            {
                #region 檢查重開放清單

                //[新比賽庫]來源開關異動紀錄暫存表
                SourceOpenRecordRequest sourceOpenRequest = new SourceOpenRecordRequest();
                //來源開關異動暫存字典
                ConcurrentDictionary<string, SourceOpenRecordDto> sourceDic = new ConcurrentDictionary<string, SourceOpenRecordDto>();

                foreach (GameInfo gameInfo in gameDic.Values)
                {
                    foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                    {
                        GpdmInfo tmpDic = gpdmPair.Value;

                        //提前生成玩法不須判斷
                        if (tmpDic.IsGenerate) { continue; }

                        //排序kType取第一筆
                        KInfo kInfo = tmpDic.Values.OrderBy(a => a.KType).FirstOrDefault();

                        if (kInfo != null && kInfo.KTypeGroup > 0)
                        {
                            SourceOpenRecordDto dto = new SourceOpenRecordDto { GId = gameInfo.GId, GType = gameInfo.GType, KTypeGroup = kInfo.KTypeGroup, SourceId = (byte)Common.source, IsOpen = 0 };
                            sourceDic.TryAdd(dto.ToString(), dto);
                        }

                        foreach (KInfo tmp in tmpDic.Values)
                        {
                            tmp.IsSourceOpenRecord = false;//清除寫入狀態
                        }
                    }
                }

                //加入暫存表
                foreach (var obj in sourceDic.Values)
                {
                    sourceOpenRequest.Data.Add(obj);
                }

                //寫入來源開關異動紀錄
                if (sourceOpenRequest.Data.Count > 0)
                {
                    var response = Common.GameCacheServiceClient.SourceOpenRecord(sourceOpenRequest);

                    if (!string.IsNullOrEmpty(response.ErrorMsg))
                    {
                        LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                    }
                }

                #endregion

                int count = 0;

                //停止GP 關閉所有開放中的賽事(關小格+脫勾)
                while (!CloseGame())
                {
                    count++;
                    //失敗3次則跳開
                    if (count > 2) { break; }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        /// <summary>
        /// 停止或程式終止，關閉所有開放中的賽事(關小格+脫勾)
        /// </summary>
        /// <param name="gpType">關閉類型</param>
        /// <param name="causeMessage">關閉原因 1:停止or關閉GP  2:來源超時未獲取數據</param>
        /// <returns></returns>
        public static bool CloseGame(GpType gpType = GpType.Zd, int causeMessage = 0)
        {
            try
            {
                #region 關閉主庫玩法與脫勾

                //[主庫]暫存表列表(分批更新)
                List<List<BkOddsUpdateDto>> dtMainOddsList = new List<List<BkOddsUpdateDto>>();
                //[主庫]更新玩法暫存表
                List<BkOddsUpdateDto> dtMainOdds = new List<BkOddsUpdateDto>();

                foreach (GameInfo game in gameDic.Values)
                {
                    if ((game.GpType & gpType) > 0)
                    {
                        foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in game.ObjDic)
                        {
                            GpdmInfo tmpDic = gpdmPair.Value;

                            foreach (KInfo kInfo in tmpDic.Values)
                            {
                                foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                                {
                                    //[主庫]
                                    if (bInfo.Status == 1)
                                    {
                                        //JZ特別邏輯
                                        //是否PL清零 (只判斷ZD)
                                        bool isClearZero = game.GpType == GpType.Zd && isOddsClearZero;
                                        //脫鉤關小格
                                        dtMainOdds.Add(SetField.SetUpdateMainOddsRow(game, gpdmPair.Key, kInfo, bInfo, false, false, isClearZero, false, true));
                                    }
                                }
                            }
                        }

                        //[主庫][玩法]超過筆數則先添加至佇列
                        if (dtMainOdds.Count > Common.eachBatchLimit)
                        {
                            dtMainOddsList.Add(dtMainOdds);
                            dtMainOdds = new List<BkOddsUpdateDto>();
                        }
                    }
                }

                //[主庫][玩法]將最後一個dataTable加入佇列
                if (dtMainOdds.Count > 0)
                {
                    dtMainOddsList.Add(dtMainOdds);
                }

                //[主庫][玩法]分批更新
                foreach (var dtMain in dtMainOddsList)
                {
                    var request = new BkOddsUpdateRequest() { IsRemove = false, IsUpdateDisplay = false };
                    request.Data.AddRange(dtMain);
                    //設定發送時間
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    //入庫執行
                    var response = OddsServiceClient.BatchUpdateV2(request);
                    stopwatch.Stop();

                    if (response.ResultStatus != 1)
                    {
                        LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                        continue;
                    }

                    //加入日誌佇列
                    LogProcess.AddQueue(new UpdateMainOddsLog(source, "", UpdateMainOddsLog.LogType.Stop, request.Data, response.GameServerResult, stopwatch.Elapsed, Config.entrance));
                }

                #endregion

                return true;
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// 判斷來源玩法PK開錯
        /// </summary>
        /// <param name="plate">PK</param>
        /// <param name="kType">Ktype</param>
        /// <returns></returns>
        public static bool IsErrorLine(decimal plate, int kType)
        {
            switch (kType)
            {
                case 12010:
                case 12011:
                case 12012:
                case 12021:
                case 12022:
                case 12023:
                case 12109:
                case 12112:
                case 12209:
                case 12212:
                case 12309:
                case 12312:
                case 12409:
                case 12410:
                case 12411:
                case 12412:
                case 12509:
                case 12510:
                case 12511:
                case 12512:
                case 12609:
                case 12610:
                case 12611:
                case 12612:
                case 12801:
                case 12802:
                case 12803:
                case 12804:
                    return plate != 0m;
            }

            return false;
        }

        /// <summary>
        /// 單隊玩法是否交換Specials
        /// </summary>
        /// <param name="specials">玩法標題分類</param>
        /// <param name="isChange">是否交換</param>
        /// <returns></returns>
        public static int TeamTotalIsChangeSpecials(int specials, bool isChange)
        {
            switch (specials)
            {
                //全場
                case 14: return isChange ? 15 : specials;
                case 15: return isChange ? 14 : specials;
                //上半
                case 16: return isChange ? 17 : specials;
                case 17: return isChange ? 16 : specials;
                //下半
                case 18: return isChange ? 19 : specials;
                case 19: return isChange ? 18 : specials;
                //第一節
                case 20: return isChange ? 21 : specials;
                case 21: return isChange ? 20 : specials;
                //第二節
                case 22: return isChange ? 23 : specials;
                case 23: return isChange ? 22 : specials;
                //第三節
                case 24: return isChange ? 25 : specials;
                case 25: return isChange ? 24 : specials;
                //第四節
                case 26: return isChange ? 27 : specials;
                case 27: return isChange ? 26 : specials;
                default: return specials;
            }
        }

        /// <summary>
        /// 選擇需轉換GP來源
        /// </summary>
        /// <returns></returns>
        public static Source ChooseTransferSource()
        {
            return FrmChoose.Show("需被轉換的來源", source);
        }

        /// <summary>
        /// 讀取NBA聯盟排除設置(識別為其他聯盟)
        /// </summary>
        /// <param name="apName">AP名稱</param>
        /// <param name="dic">需覆蓋的字典</param>
        public static void NbaLeagueExcludeSetting(string apName, ref Dictionary<string, HashSet<string>> dic)
        {
            //檔案路徑
            DirectoryInfo dir = new DirectoryInfo(Application.StartupPath).Parent.Parent.Parent;
            string filePath = dir.FullName + "\\Info\\BK_NbaLeagueExclude.txt";

            try
            {
                if (!File.Exists(filePath)) { return; }//檔案不存在則跳過

                //暫存字典
                Dictionary<string, HashSet<string>> tmpDic = new Dictionary<string, HashSet<string>>();

                FileInfo fileInfo = new FileInfo(filePath);
                FileStream stream = fileInfo.OpenRead();
                StreamReader reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string data = reader.ReadLine();

                    if (data.StartsWith($"{apName},"))
                    {
                        string[] temp = data.Split(',');

                        if (temp.Length == 2)//須符合格式
                        {
                            var excludeNames = tmpDic.GetOrAdd(temp[0], new HashSet<string>());
                            excludeNames.Add(temp[1]);
                        }
                    }
                }

                stream.Close();
                reader.Close();

                //覆蓋字典
                dic = tmpDic;
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        /// <summary>
        /// 檢查NBA聯盟名稱是否包含需排除名稱，是則須設成其他球類
        /// </summary>
        /// <param name="leagueName"></param>
        /// <returns></returns>
        public static bool CheckNbaExcludeName(string leagueName)
        {
            bool result = false;
            string apName = YwCore.Common.GetApplicationNameForLeagueReplace(YwCore.Common.source, YwCore.Common.entrance);

            if (!string.IsNullOrEmpty(leagueName) && nbaleagueRecludeDic.TryGetValue(apName, out var excludeNames))
            {
                result = excludeNames.Any(filter => leagueName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return result;
        }

        /// <summary>
        /// 整理NBA DS 全場上半 鴛鴦盤Odds
        /// </summary>
        /// <param name="allPlateInfos">來源所有Odds(已排序)</param>
        /// <param name="a_plateInfo">A盤資訊</param>
        /// <param name="type">類型: rf:1  ou:2 </param>
        /// <param name="index">第幾盤</param>
        /// <param name="sameCount">總共抓幾盤</param>
        /// <param name="allBadGood">全好或全壞</param>
        /// <param name="plateInfos_ref">存放整理後的Odds [B,C,D,E盤]</param>
        /// <param name="isMinus">存放全好全壞固定向上或向下抓</param>
        public static void ArrangedNbaDsFtHtSameCountOdds(List<PlateInfo> allPlateInfos, PlateInfo a_plateInfo, int type, int index, int sameCount, bool allBadGood, ref PlateInfo[] plateInfos_ref, ref bool isMinus)
        {
            #region 鴛鴦盤抓法描述

            //全場 上半複合盤(3盤)
            //RF 客讓
            //A盤：最接近平水PK為A盤 
            //B盤：固定抓取來源比A盤好最少100 % PK，並跳過DY盤
            //C盤：固定抓取來源比A盤壞最少100 % PK
            //RF 主讓
            //A盤：最接近平水PK為A盤 
            //B盤：固定抓取來源比A盤壞最少100 % PK
            //C盤：固定抓取來源比A盤好最少100 % PK，並跳過DY盤
            //註:全場不抓取0輸盤
            //OU
            //A盤：最接近平水PK為A盤
            //B盤：固定抓取來源比A盤壞最少100 % PK
            //C盤：固定抓取來源比A盤好最少100 % PK
            //
            //全場 上半複合盤(5盤)
            //RF 客讓
            //A盤：最接近平水PK為A盤
            //B盤：固定抓取來源比A盤好最少100 % PK，並跳過DY盤
            //C盤：固定抓取來源比B盤好最少100 % PK，並跳過DY盤
            //D盤：固定抓取來源比A盤壞最少100 % PK
            //E盤：固定抓取來源比D盤壞最少100 % PK
            //如果B盤有換RT C盤需抓取B盤壞於最少100%的平輸盤
            //全場不抓取0輸盤
            //沒開0輸的全場
            //忽略讓邊 1平 > 0 的跳動％數，補到另一邊
            //有開0輸的玩法
            //忽略讓邊 0.5 > 0 的跳動％數，補到另一邊
            //RF 主讓
            //A盤：最接近平水PK為A盤
            //B盤：固定抓取來源比A盤壞最少100 % PK
            //C盤：固定抓取來源比B盤壞最少100 % PK
            //D盤：固定抓取來源比A盤好最少100 % PK，並跳過DY盤
            //E盤：固定抓取來源比D盤好最少100 % PK，並跳過DY盤
            //註:全場不抓取0輸盤
            //如果D盤有換RT E盤需抓取D盤壞於最少100 % 的平輸盤
            //全場不抓取0輸盤
            //沒開0輸的全場
            //忽略讓邊 1平 > 0 的跳動％數，補到另一邊
            //有開0輸的玩法
            //忽略讓邊 0.5 > 0 的跳動％數，補到另一邊
            //OU
            //A盤：最接近平水PK為A盤
            //B盤：固定抓取來源比C盤壞最少100 % PK
            //C盤：固定抓取來源比A盤壞最少100 % PK
            //D盤：固定抓取來源比A盤好最少100 % PK
            //E盤：固定抓取來源比D盤好最少100 % PK
            //
            //2025-03-03
            //鴛鴦盤場數設為3：只剩壞盤則B>C越來越壞；只剩好盤則B>C越來越好
            //鴛鴦盤場數設為5：只剩壞盤則B>E越來越壞；只剩好盤則B>E越來越好
            //
            //當來源除最接近平水的PK外，只剩壞盤
            //A盤：最接近平水PK為A盤
            //B盤：固定抓取來源比A盤壞最少100 % PK
            //C盤：固定抓取來源比B盤壞最少100 % PK
            //D盤：固定抓取來源比C盤壞最少100 % PK
            //E盤：固定抓取來源比D盤壞最少100 % PK
            //
            //當來源除最接近平水的PK外，只剩好盤
            //A盤：最接近平水PK為A盤
            //B盤：固定抓取來源比A盤好最少100 % PK
            //C盤：固定抓取來源比B盤好最少100 % PK
            //D盤：固定抓取來源比C盤好最少100 % PK
            //E盤：固定抓取來源比D盤好最少100 % PK

            #endregion

            PlateInfo plateInfo = null;

            if (allPlateInfos != null && allPlateInfos.Count > 0 && a_plateInfo != null)
            {
                if (index == 1)
                {
                    //全好全壞，抓B盤位置
                    if (allBadGood)
                    {
                        //先往上找，若找到後續固定往上，找不到往下找，後續固定往下找
                        plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, false);

                        if (plateInfo == null)
                        {
                            plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, true);
                            isMinus = true;
                        }
                    }
                    //B盤: RF 客讓 固定抓取來源比A盤好最少100 % PK，並跳過DY盤 / 主讓 固定抓取來源比A盤壞最少100 % PK            
                    else if (type == 1)
                    {
                        plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, false);
                    }
                    //B盤: OU 3盤: 固定抓取來源比A盤壞最少100 % PK  5盤: 固定抓取來源比C盤壞最少100 % PK
                    else
                    {
                        if (sameCount == 3)
                        {
                            plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, true);
                        }
                        else
                        {
                            //5盤先抓C盤：固定抓取來源比A盤壞最少100 % PK，再抓B盤
                            PlateInfo c_plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, true);

                            //若有C盤，固定抓取來源比A盤壞最少100 % PK，再抓B盤
                            if (c_plateInfo != null)
                            {
                                plateInfo = GetNearPk(allPlateInfos, c_plateInfo.Plate, 0.5m, true);

                                //儲存C盤
                                plateInfos_ref[1] = c_plateInfo;
                            }
                        }
                    }
                }
                else if (index == 2)
                {
                    //全好全壞由B盤固定方向抓
                    if (allBadGood)
                    {
                        if (plateInfos_ref[0] != null)
                        {
                            plateInfo = GetNearPk(allPlateInfos, plateInfos_ref[0].Plate, 0.5m, isMinus);
                        }
                    }
                    else if (type == 1)
                    {
                        //C盤 3盤 客讓: 固定抓取來源比A盤壞最少100 % PK  主讓:固定抓取來源比A盤好最少100 % PK，並跳過DY盤
                        if (sameCount == 3)
                        {
                            plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, true);
                        }
                        //C盤 5盤 客讓:固定抓取來源比B盤好最少100 % PK，並跳過DY盤  主讓: 固定抓取來源比B盤壞最少100 % PK
                        else
                        {
                            if (plateInfos_ref[0] != null)
                            {
                                plateInfo = GetNearPk(allPlateInfos, plateInfos_ref[0].Plate, 0.5m, false);
                            }
                        }
                    }
                    else
                    {
                        //C盤：固定抓取來源比A盤好最少100 % PK
                        if (sameCount == 3)
                        {
                            plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, false);
                        }
                        //C盤：固定抓取來源比A盤壞最少100 % PK (B盤已經儲存過)                      
                    }
                }
                else if (index == 3)
                {
                    //全好全壞由C盤固定方向抓
                    if (allBadGood)
                    {
                        if (plateInfos_ref[1] != null)
                        {
                            plateInfo = GetNearPk(allPlateInfos, plateInfos_ref[1].Plate, 0.5m, isMinus);
                        }
                    }
                    else if (type == 1)
                    {
                        //D盤 5盤 客讓: 固定抓取來源比A盤壞最少100%PK 主讓:固定抓取來源比A盤好最少100%PK，並跳過DY盤
                        plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, true);
                    }
                    else
                    {
                        //D盤：固定抓取來源比A盤好最少100%PK
                        plateInfo = GetNearPk(allPlateInfos, a_plateInfo.Plate, 0.5m, false);
                    }
                }
                else if (index == 4)
                {
                    //全好全壞由D盤固定方向抓
                    if (allBadGood)
                    {
                        if (plateInfos_ref[2] != null)
                        {
                            plateInfo = GetNearPk(allPlateInfos, plateInfos_ref[2].Plate, 0.5m, isMinus);
                        }
                    }
                    //E盤：客讓 固定抓取來源比D盤壞最少100%PK 主讓 固定抓取來源比D盤好最少100%PK，並跳過DY盤
                    else if (type == 1)
                    {
                        if (plateInfos_ref[2] != null)
                        {
                            plateInfo = GetNearPk(allPlateInfos, plateInfos_ref[2].Plate, 0.5m, true);
                        }
                    }
                    //E盤：固定抓取來源比D盤好最少100%PK
                    else
                    {
                        if (plateInfos_ref[2] != null)
                        {
                            plateInfo = GetNearPk(allPlateInfos, plateInfos_ref[2].Plate, 0.5m, false);
                        }
                    }
                }
            }

            //存放到List
            if (plateInfo != null)
            {
                plateInfos_ref[index - 1] = plateInfo;
            }
        }

        /// <summary>
        /// 利用主要PK和差距獲得對應PK
        /// </summary>
        /// <param name="plateInfoList">PK PL 清單</param>
        /// <param name="mainPk">主要PK</param>
        /// <param name="difPk">至少差距PK</param>
        /// <param name="isMinus">是否往下抓</param>
        /// <returns></returns>
        private static PlateInfo GetNearPk(List<PlateInfo> plateInfoList, decimal mainPk, decimal difPk, bool isMinus)
        {
            //RF PK由主讓大往客讓大排，isMinus向下抓/ OU PK由小排到大
            decimal targetPk = isMinus ? mainPk + difPk : mainPk - difPk;

            for (int i = 0; i < plateInfoList.Count; i++)
            {
                int index = isMinus ? i : plateInfoList.Count - 1 - i;
                PlateInfo tmpPlateInfo = plateInfoList[index];
                decimal tmpPk = tmpPlateInfo.Plate;

                //0 PK 跳過
                if (tmpPk == 0m)
                {
                    continue;
                }

                //抓取達條件第一筆PlateInfo
                if ((isMinus && tmpPk >= targetPk) || (!isMinus && tmpPk <= targetPk))
                {
                    return tmpPlateInfo;
                }
            }

            return null;
        }

        /// <summary>
        /// 排除/指定聯盟
        /// </summary>
        /// <param name="leagueName">聯盟名稱</param>
        /// <returns></returns>
        public static bool ExcludeAssingLeague(string leagueName)
        {
            switch (Common.LeagueMode)
            {
                case 1://模式1，包含則不解析
                    return Common.ExcludeModeHashSet.Any(s => string.Equals(s, leagueName, StringComparison.OrdinalIgnoreCase));
                case 2://模式2，不包含則不解析
                    return !Common.AssignModeHashSet.Any(s => string.Equals(s, leagueName, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// 檢查來源是否登入及發送MT提醒
        /// </summary>
        /// <param name="isLogin">前次登入狀態</param>
        /// <param name="users">帳號集合</param>
        /// <returns>登入否</returns>
        public static bool CheckSourceLoginAndRemind(bool isLogin, IEnumerable<UserInfo> users)
        {
            bool isLoginNew = Common.CheckSourceLogin(source, users);

            if (isLoginNew != isLogin && isFollow)
            {
                string sendMsg = isLoginNew ? $"{gpName}籃球 帳號已登入" : $"{gpName}籃球 帳號未登入";
                Common.SendMessage.Send(sendMsg);
            }

            return isLoginNew;
        }
        /// <summary>
        ///重新啟動應用程式
        /// </summary>
        public static void RestartApplication()
        {
            // 構建傳遞的參數字串
            string args = string.Join(" ", ChooseSet);
            // 取得目前執行檔路徑 (.NET 6 建議使用 Environment.ProcessPath)
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;

            // 建立啟動資訊
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true
            };

            // 啟動新的應用程式實例
            System.Diagnostics.Process.Start(startInfo);
        }

        #endregion
    }

    /// <summary>
    /// Proxy參數設置
    /// </summary>
    public class Configuration : IConfiguration
    {
        /// <summary>
        /// 檔名
        /// </summary>
        public string ProxyFileName => "Proxys";
        /// <summary>
        /// 開關
        /// </summary>
        public bool OpenProxy { get => Config.isProxy; set => Config.isProxy = value; }
        /// <summary>
        /// 存檔key
        /// </summary>
        public string FileKey { get; set; } = YwCore.Common.GetApplicationName();
    }
}
