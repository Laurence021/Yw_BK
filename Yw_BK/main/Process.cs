#region 功能修改描述與時間

/*
    描述：通用主流程
    日期：2019-01-07
       
    描述：添加MyStake流程
    日期：2022-10-27

    描述：添加JZ來源賽事玩法自動切待結算/移回操盤頁邏輯判斷
    日期：2023-03-13

    描述：添加未切待結算提醒處理
    日期：2023-03-27

    描述：新版代理功能
    日期：2023-07-31

    描述：調整請求timeout處理abort順序、逾時紀錄內容統一用reqInfo物件提供
    日期：2023-10-02

    描述：添加自動修改聯盟API
    日期：2024-07-19
*/

#endregion

using Gp.Model;
using Gp.Service;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using Yw_BK.model;
using Yw_BK.model.logInfo;
using Yw_BK.source;
using Yw_BK.source.radar;
using YwCore;
using YwCore.main;
using YwCore.model;
using YwCore.source;
using YwCore.tools;
using ZDBD.Model;

namespace Yw_BK.main
{
    /// <summary>
    /// 主流程
    /// </summary>
    public class Process : IDisposable
    {
        //365連線方法
        private Connect365 _connect365;
        //365請求方法
        private Request365 _request365;
        //JZ連線方法
        private ConnectJZ _connectJZ;
        private ConnectJZ _connectJZ_Ayh;
        //SB共用方法
        private CommonSB _commonSB;
        //PIN請求方法
        private SourcePIN _sourcePIN;
        //Radar連線物件
        private ConnectRadar _connectRadar;
        //Radar數據分流
        private Dispatcher dispatcher;
        //請求回傳數據集中管理
        private Dictionary<GpType, DataConsolidation> dataConsolidationDic;
        //執行緒資訊
        private ConcurrentDictionary<GpType, ThreadInfo> threadInfos = Config.threadInfos;
        //是否處理入庫[0:ZD 1:DS 2:ZP]
        private bool[] isHandle = new bool[3];
        //執行查詢行為使用的鎖(避免重複執行)[0:ZD 1:DS 2:ZP]
        private int[] lockArray = new int[3];
        // 當前網址
        private static string currentUrl = string.Empty;
        //是否將介面變色提示
        private bool changeColor = false;
        //紀錄處理時間(檢查是否達間隔需進行處理使用)
        private static ConcurrentDictionary<GpType, DateTime> recordTimeDic = new ConcurrentDictionary<GpType, DateTime>();
        //Api相關功能處理
        public static AllyApiHandle AllyApi = new AllyApiHandle();
        public static TeamApiHandle TeamApi = new TeamApiHandle();
        public static AutoCreateGameApiHandle CreateGameApi = new AutoCreateGameApiHandle(Common.ApiServiceClient);
        public static GameStatusSetApiHandle GameStatusSetApi = new GameStatusSetApiHandle();
        //未切待結算提醒功能處理
        public static SettlementRecordHandle SettlementRecordHandle = new SettlementRecordHandle();

        #region 計時器

        //登入計時器
        private System.Timers.Timer _timerLogin = new System.Timers.Timer();
        //查詢開賽時間計時器
        private System.Timers.Timer _timerGameDate = new System.Timers.Timer();
        //檢查聯盟隊伍的計時器
        private System.Timers.Timer _timerCheck = new System.Timers.Timer();
        //控制計時器(處理新增更新)
        private System.Timers.Timer _timerCtrl = new System.Timers.Timer();
        //查詢主庫kMode計時器
        private System.Timers.Timer _timerKMode = new System.Timers.Timer();
        //查詢主庫odds差距值計時器
        private System.Timers.Timer _timerOdds = new System.Timers.Timer();
        //控制分組計時器
        private System.Timers.Timer _timerGroup = new System.Timers.Timer();
        //寫入TS來源計時器
        private System.Timers.Timer _timerTsSourceId = new System.Timers.Timer();

        //DS計時器
        private System.Timers.Timer _timerDs = new System.Timers.Timer();
        //DS-More計時器
        private System.Timers.Timer _timerDsMore = new System.Timers.Timer();
        //DS佇列計時器
        private System.Timers.Timer _timerDsQueue = new System.Timers.Timer();
        //DS更多佇列計時器
        private System.Timers.Timer _timerDsMoreQueue = new System.Timers.Timer();

        //ZD計時器
        private System.Timers.Timer _timerZd = new System.Timers.Timer();
        //ZD-More計時器
        private System.Timers.Timer _timerZdMore = new System.Timers.Timer();
        //ZD佇列計時器
        private System.Timers.Timer _timerZdQueue = new System.Timers.Timer();
        //ZD-More佇列計時器
        private System.Timers.Timer _timerZdMoreQueue = new System.Timers.Timer();

        //ZP計時器
        private System.Timers.Timer _timerZp = new System.Timers.Timer();
        //ZP-More計時器
        private System.Timers.Timer _timerZpMore = new System.Timers.Timer();
        //ZP佇列計時器
        private System.Timers.Timer _timerZpQueue = new System.Timers.Timer();
        //ZP-More佇列計時器
        private System.Timers.Timer _timerZpMoreQueue = new System.Timers.Timer();

        //Dj計時器
        private System.Timers.Timer _timerDj = new System.Timers.Timer();
        //Dj佇列計時器
        private System.Timers.Timer _timerDjQueue = new System.Timers.Timer();

        //DjQ2計時器
        private System.Timers.Timer _timerDjQ2 = new System.Timers.Timer();
        //DjQ2佇列計時器
        private System.Timers.Timer _timerDjQ2Queue = new System.Timers.Timer();

        //Xb計時器
        private System.Timers.Timer _timerXb = new System.Timers.Timer();
        //Xb佇列計時器
        private System.Timers.Timer _timerXbQueue = new System.Timers.Timer();

        //Ac計時器
        private System.Timers.Timer _timerAc = new System.Timers.Timer();
        //Ac上半計時器
        private System.Timers.Timer _timerAcHt = new System.Timers.Timer();
        //Ac第一節計時器
        private System.Timers.Timer _timerAcQ1 = new System.Timers.Timer();
        //Ac第二節計時器
        private System.Timers.Timer _timerAcQ2 = new System.Timers.Timer();
        //Ac佇列計時器
        private System.Timers.Timer _timerAcQueue = new System.Timers.Timer();
        //Ac上半佇列計時器
        private System.Timers.Timer _timerAcHtQueue = new System.Timers.Timer();
        //Ac第一節佇列計時器
        private System.Timers.Timer _timerAcQ1Queue = new System.Timers.Timer();
        //Ac第二節佇列計時器
        private System.Timers.Timer _timerAcQ2Queue = new System.Timers.Timer();

        //訊息提醒檢查計時器
        private System.Timers.Timer _timerRemind = new System.Timers.Timer();

        //啟動請求計時器
        private System.Timers.Timer _timerStartRequest = new System.Timers.Timer();
        //計時器集合容器(用於觸發防護關閉)
        private List<System.Timers.Timer> timerList = new List<System.Timers.Timer>();

        //請求聯盟計時器(SBO、XB使用)
        private System.Timers.Timer _timerReqLeague = new System.Timers.Timer();
        private System.Timers.Timer _timerZdReqLeague = new System.Timers.Timer();
        private System.Timers.Timer _timerDsReqLeague = new System.Timers.Timer();
        private System.Timers.Timer _timerZpReqLeague = new System.Timers.Timer();
        #endregion

        #region Start

        /// <summary>
        /// 啟動流程
        /// </summary>
        public void Start()
        {
            //添加類型至執行緒集合
            threadInfos.TryAdd(GpType.Ds, new ThreadInfo(GpType.Ds.ToString()));

            threadInfos.TryAdd(GpType.Zd, new ThreadInfo(GpType.Zd.ToString()));

            threadInfos.TryAdd(GpType.Zp, new ThreadInfo(GpType.Zp.ToString()));

            if (Config.source == Source.XB)
            {
                threadInfos.TryAdd(GpType.DsMore, new ThreadInfo(GpType.DsMore.ToString()));
                threadInfos.TryAdd(GpType.ZdMore, new ThreadInfo(GpType.ZdMore.ToString()));
                threadInfos.TryAdd(GpType.ZpMore, new ThreadInfo(GpType.ZpMore.ToString()));
            }
            else if (Config.source == Source.PIN)
            {
                threadInfos.TryAdd(GpType.Dj, new ThreadInfo(GpType.Dj.ToString()));
                threadInfos.TryAdd(GpType.Xb, new ThreadInfo(GpType.Xb.ToString()));
                threadInfos.TryAdd(GpType.Ac, new ThreadInfo(GpType.Ac.ToString()));
                threadInfos.TryAdd(GpType.AcHT, new ThreadInfo(GpType.AcHT.ToString()));
                threadInfos.TryAdd(GpType.AcQ1, new ThreadInfo(GpType.AcQ1.ToString()));
                threadInfos.TryAdd(GpType.AcQ2, new ThreadInfo(GpType.AcQ2.ToString()));
                threadInfos.TryAdd(GpType.DjQ2, new ThreadInfo(GpType.DjQ2.ToString()));
            }
            else if (Config.source == Source.SBO)
            {
                threadInfos.TryAdd(GpType.DsMore, new ThreadInfo(GpType.DsMore.ToString()));
                threadInfos.TryAdd(GpType.ZdMore, new ThreadInfo(GpType.ZdMore.ToString()));
            }
            else if (Config.source == Source.MyStake)
            {
                threadInfos.TryAdd(GpType.ZdMore, new ThreadInfo(GpType.ZdMore.ToString()));
            }

            //啟動日誌處理
            LogProcess.Start();

            //啟動關閉提醒處理
            CloseInfoHandle.Start();
            //啟動關閉監控處理
            Config.CloseNotify.Start();
            //啟動未切待結算提醒處理
            SettlementRecordHandle.Start();
            //計時器控制
            TimerController();

            //啟動賽事API功能
            if (Config.source == Source.JZ)
            {
                AllyApi.Start();
                TeamApi.Start();
                GameStatusSetApi.Start();
                TeamApi.OnSetTeamInfo += SetField.SetTeamInfo;
                AllyApi.OnSetAllyInfo += SetField.SetAllyInfo;
            }

            CreateGameApi.Start();
            //GP登入流程開始
            Config.IsLogin = true;
        }

        //計時器控制
        private void TimerController()
        {
            //聯盟隊伍匹配
            Common.SetTimer(_timerCheck, 1000, false, CheckTick);
            //控制新建更新計時器
            Common.SetTimer(_timerCtrl, Config.source == Source.Radar ? 500 : 1000, false, CtrlTick);
            //查詢主庫kMode計時器
            Common.SetTimer(_timerKMode, 10, false, SelectKModeTick);
            //查詢主庫odds差距值計時器
            Common.SetTimer(_timerOdds, 10, false, SelectOddsTick);
            //警告訊息檢查計時器
            Common.SetTimer(_timerRemind, 1000, false, CheckRemindTick);
            //請求觸發計時器
            Common.SetTimer(_timerStartRequest, 120000, false, new ElapsedEventHandler((s, e) => RequestTrigger(true)), false);

            //YW來源統一先查庫內ZD開賽時間
            if (!(Config.source == Source.JZ || (Config.source == Source.BET365 && Config.gpType == GpType.Ds) || Config.source == Source.Radar))
            {
                Common.SetTimer(_timerGameDate, 1, false, SelectDateTick);
            }

            if (Config.source == Source.BET365)
            {
                if (Config.gpType == GpType.Zd)
                {
                    //實體化365連線物件
                    _connect365 = new Connect365(Common365.Sport365.BK, Common365.Language365.En, new Req365());
                    _connect365.SourceForbidden += (s, retry) =>
                    {
                        Display.SetDictInfo(Display.Type.Remind, YwCore.Properties.Resources.SourceError);

                        if (Config.isFollow && Config.AbnormalHandleEnabled)
                        {
                            Config.CloseGame();
                            Common.SendMessage.Send($"籃球{Config.source.ToString("g")}來源異常，請檢查是否正常{Environment.NewLine}{Environment.NewLine}1.檢查賽事盤口水位是否正常{Environment.NewLine}有，請正常開放。{Environment.NewLine}否，請重啟跟盤。{Environment.NewLine}重啟跟盤後未恢復請切換備用來源，並檢查紀錄異常原因{Environment.NewLine}{Environment.NewLine}2.請勿勾上跟盤，賽事請改手開");
                        }
                    };

                    _connect365.SourceRecover += (s, retry) =>
                    {
                        Display.SetDictInfo(Display.Type.Remind, "");
                        Common.SendMessage.Send($"籃球{Config.source.ToString("g")}來源回復正常");
                    };

                    Config.Proxy.Changed += (s, e) =>
                    {
                        _connect365.Close();
                    };

                    DateTime LastFaildTime = DateTime.MinValue;

                    Config.Proxy.ChangeFailed += (s, e) =>
                    {
                        if (Config.isFollow && (DateTime.Now - LastFaildTime).TotalMinutes > 10)
                        {
                            LastFaildTime = e.ChangeTime;
                            Common.SendMessage.Send($"籃球{Config.source.ToString("g")}，無可切換之代理，請手動新增第二條代理");
                        }
                    };

                    //委派重新訂閱事件
                    Config.OnConfigChanged += () => { if (Config.isOpenZd) { _connect365.OutsideSubscribe(); } };

                    //Zd佇列
                    Common.SetTimer(_timerZdQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdQueue)));
                }
                else
                {
                    _request365 = new Request365(Config.url, Parser365.GetInnerUrlParam, Common365.Sport365.BK);

                    //請求來源資料計時器
                    Common.SetTimer(_timerDs, 1, false, new ElapsedEventHandler((s, e) =>
                    {
                        if (_request365.LoginUsers.Any(a => a.IsLogin == LoginStatus.LoginSuccess) &&
                        _request365.LoginUsers.Count() == FrmUser.users.Values.Count &&
                        !_request365.LoginUsers.Any(a => a.IsLogin == LoginStatus.NoLogin))
                        {
                            MainTick(s, e, threadInfos[GpType.Ds]);
                        }

                        _timerDs.Start();
                    }));
                    //Ds佇列
                    Common.SetTimer(_timerDsQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.dsQueue)));

                    timerList.Add(_timerDs);
                }

                //登入計時器
                Common.SetTimer(_timerLogin, 1500, false, LoginTick);
            }
            else if (Config.source == Source.JZ)
            {
                //實體化JZ連線物件
                _connectJZ = new ConnectJZ(Config.sport, Config.LastTimeChange);
                _connectJZ_Ayh = new ConnectJZ(Sport.None, Config.LastTimeChange);

                //委派重新整理連線事件
                Config.OnConfigChanged += () =>
                {
                    _connectJZ.Refresh(SubscribeType.Ds, Config.isOpenDs);
                    _connectJZ.Refresh(SubscribeType.Zd, Config.isOpenZd);

                    if (Config.isTrackOlympics)
                    {
                        _connectJZ_Ayh.Refresh(SubscribeType.Ds, Config.isOpenDs);
                        _connectJZ_Ayh.Refresh(SubscribeType.Zd, Config.isOpenZd);
                    }
                };

                //委派重新整理連線事件
                StreamInfo.OnStreamChanged += () =>
                {
                    _connectJZ.Refresh(SubscribeType.Ds, Config.isOpenDs);
                    _connectJZ.Refresh(SubscribeType.Zd, Config.isOpenZd);

                    if (Config.isTrackOlympics)
                    {
                        _connectJZ_Ayh.Refresh(SubscribeType.Ds, Config.isOpenDs);
                        _connectJZ_Ayh.Refresh(SubscribeType.Zd, Config.isOpenZd);
                    }
                };

                //登入計時器
                Common.SetTimer(_timerLogin, 1500, false, LoginTick);
                //Ds佇列
                Common.SetTimer(_timerDsQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.dsQueue)));
                //Zd佇列
                Common.SetTimer(_timerZdQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdQueue)));
                //DsMore佇列(奧運用)
                Common.SetTimer(_timerDsMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.dsMoreQueue)));
                //ZdMore佇列(奧運用)
                Common.SetTimer(_timerZdMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdMoreQueue)));
                //更新分組計時器
                Common.SetTimer(_timerGroup, 5000, false, new ElapsedEventHandler((s, e) => UpdateGroupTick(s, e)));
                //更新TS來源ID計時器
                Common.SetTimer(_timerTsSourceId, 500, false, new ElapsedEventHandler((s, e) => UpdateTsSourceIdTick(s, e)));
            }
            else if (Config.source == Source.SB)
            {
                //實體化SB共用物件
                _commonSB = new CommonSB(Config.entrance, new SportSb[] { ReqSB.SportSb });
                //註冊事件
                _commonSB.Subscribe += ReqSB.SubscribeHandle;
                _commonSB.ReceiveEventM += ReqSB.ReceiveData;
                ReqSB.commonSB = _commonSB;
                //委派事件重新訂閱
                Config.OnConfigChanged += () => { _commonSB.IsResetSubscribe = true; };
                //登入計時器
                Common.SetTimer(_timerLogin, 1500, false, LoginTick);

                #region Ds

                //Ds佇列
                Common.SetTimer(_timerDsQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.dsQueue)));

                #endregion

                #region Zp

                //Zp佇列
                Common.SetTimer(_timerZpQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zpQueue)));

                #endregion

                #region Zd

                //Zd佇列
                Common.SetTimer(_timerZdQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdQueue)));

                #endregion
            }
            else if (Config.source == Source.SBO)
            {
                //聯盟請求計時器
                Common.SetTimer(_timerReqLeague, 100, false, new ElapsedEventHandler((s, e) =>
                {
                    bool isOpen = Config.isOpenDs || Config.isOpenZd || Config.isOpenZp;

                    try
                    {
                        if (isOpen)
                        {
                            //取得代理資訊
                            string proxy = Config.Proxy.GetProxy()?.Address.Authority;
                            ReqSBO.RequestLeague(proxy);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFile.SetLog(LogMode.Error, ex.ToString());
                    }
                    finally
                    {
                        double intreval = isOpen ? 10 * 60 * 1000 : 100;

                        if (((System.Timers.Timer)s).Interval != intreval)
                        {
                            ((System.Timers.Timer)s).Interval = intreval;
                        }

                       ((System.Timers.Timer)s).Start();
                    }
                }));

                #region Ds

                //Ds
                Common.SetTimer(_timerDs, Config.dsSec, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Ds])));
                MoreType moreTypeDs = new MoreType(GpType.Ds);

                //佇列處理
                Common.SetTimer(_timerDsQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.dsQueue)));

                timerList.Add(_timerDs);

                #endregion

                #region Zd

                Common.SetTimer(_timerZd, Config.zdSec, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Zd])));
                MoreType moreTypeZd = new MoreType(GpType.Zd);

                //Zd佇列
                Common.SetTimer(_timerZdQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdQueue)));

                timerList.Add(_timerZd);

                #endregion

                //DsMore
                Common.SetTimer(_timerDsMore, 10000, false, new ElapsedEventHandler((s, e) => MoreTick(s, e, threadInfos[GpType.DsMore], moreTypeDs)));
                //Ds更多佇列
                Common.SetTimer(_timerDsMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueMoreTick(s, e, Config.dsMoreQueue, GpType.DsMore)));

                //ZdMore
                Common.SetTimer(_timerZdMore, 5000, false, new ElapsedEventHandler((s, e) => MoreTick(s, e, threadInfos[GpType.ZdMore], moreTypeZd)));
                //Zd更多佇列處理
                Common.SetTimer(_timerZdMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueMoreTick(s, e, Config.zdMoreQueue, GpType.ZdMore)));

                timerList.Add(_timerDsMore);
                timerList.Add(_timerZdMore);
            }
            else if (Config.source == Source.MyStake)
            {
                #region Zd

                Common.SetTimer(_timerZd, Config.zdSec, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Zd])));
                MoreType moreTypeZd = new MoreType(GpType.Zd);

                //Zd佇列
                Common.SetTimer(_timerZdQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdQueue)));

                timerList.Add(_timerZd);

                #endregion

                //ZdMore
                Common.SetTimer(_timerZdMore, 5000, false, new ElapsedEventHandler((s, e) => MoreTick(s, e, threadInfos[GpType.ZdMore], moreTypeZd)));
                //Zd更多佇列處理
                Common.SetTimer(_timerZdMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueMoreTick(s, e, Config.zdMoreQueue, GpType.ZdMore)));
                timerList.Add(_timerZdMore);
            }
            else if (Config.source == Source.Radar)
            {
                dispatcher = new Dispatcher();
                _connectRadar = new ConnectRadar(GP_RadarService.Models.Subscribe.Sport.Basketball);
                _connectRadar.Connected += (s, e) =>
                {
                    LogFile.SetLog(LogMode.Login, Source.Radar, "連線");
                };

                _connectRadar.Disconnected += (s, e) =>
                {
                    LogFile.SetLog(LogMode.Login, Source.Radar, $"斷線 {e}");
                };

                _connectRadar.MessageReceived += dispatcher.MessageHandle;

                Config.OnConfigChanged += async () =>
                {
                    if (_connectRadar.IsOpen)
                    {
                        List<int> producerId = new List<int>();
                        if (Config.isOpenDs) producerId.Add(3);
                        if (Config.isOpenZd) producerId.Add(1);

                        if (producerId.Count > 0)
                        {
                            await _connectRadar.Subscribe(producerId);
                        }
                        else
                        {
                            await _connectRadar.CloseAsync();
                        }
                    }
                };

                //登入計時器
                Common.SetTimer(_timerLogin, 1500, false, LoginTick);
            }
            else
            {
                //登入計時器
                Common.SetTimer(_timerLogin, 1500, false, LoginTick);

                #region Ds

                //Ds
                Common.SetTimer(_timerDs, Config.dsSec, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Ds])));
                MoreType moreTypeDs = new MoreType(GpType.Ds);

                //佇列處理
                Common.SetTimer(_timerDsQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.dsQueue)));

                timerList.Add(_timerDs);

                #endregion

                #region Zd

                Common.SetTimer(_timerZd, Config.zdSec, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Zd])));
                MoreType moreTypeZd = new MoreType(GpType.Zd);

                //Zd佇列
                Common.SetTimer(_timerZdQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zdQueue)));

                timerList.Add(_timerZd);

                #endregion

                #region Zp

                //Zp
                Common.SetTimer(_timerZp, Config.zpSec, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Zp])));
                MoreType moreTypeZp = new MoreType(GpType.Zp);

                //Zp佇列
                Common.SetTimer(_timerZpQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.zpQueue)));

                timerList.Add(_timerZp);

                #endregion

                if (Config.source == Source.PIN)
                {
                    switch (Config.entrance)
                    {
                        case Entrance.LEO:
                            _sourcePIN = new SourcePIN(new LoginLEO());
                            break;
                        case Entrance.Asia:
                            _sourcePIN = new SourcePIN(new LoginPINAsia());
                            break;
                        case Entrance.PIN:
                        default:
                            _sourcePIN = new SourcePIN(new LoginPIN());
                            break;
                    }

                    //Ac計時器
                    Common.SetTimer(_timerAc, 1600, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Ac])));
                    //Ac佇列計時器
                    Common.SetTimer(_timerAcQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.acQueue)));

                    if (Config.entrance == Entrance.PIN)
                    {
                        //Dj計時器
                        Common.SetTimer(_timerDj, 1150, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Dj])));
                        //Dj佇列處理
                        Common.SetTimer(_timerDjQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.djQueue)));

                        //DjQ2計時器
                        Common.SetTimer(_timerDjQ2, 1300, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.DjQ2])));
                        //DjQ2佇列處理
                        Common.SetTimer(_timerDjQ2Queue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.djQ2Queue)));

                        //Xb計時器
                        Common.SetTimer(_timerXb, 1450, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.Xb])));
                        //Xb佇列計時器
                        Common.SetTimer(_timerXbQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.xbQueue)));

                        //Ac上半計時器
                        Common.SetTimer(_timerAcHt, 1700, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.AcHT])));
                        //Ac上半佇列計時器
                        Common.SetTimer(_timerAcHtQueue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.acHtQueue)));

                        //Ac第一節計時器
                        Common.SetTimer(_timerAcQ1, 1800, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.AcQ1])));
                        //Ac第一節佇列計時器
                        Common.SetTimer(_timerAcQ1Queue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.acQ1Queue)));

                        //Ac第二節計時器
                        Common.SetTimer(_timerAcQ2, 1900, true, new ElapsedEventHandler((s, e) => MainTick(s, e, threadInfos[GpType.AcQ2])));
                        //Ac第二節佇列計時器
                        Common.SetTimer(_timerAcQ2Queue, 1, false, new ElapsedEventHandler((s, e) => QueueTick(s, e, Config.acQ2Queue)));
                    }

                    timerList.Add(_timerDj);
                    timerList.Add(_timerDjQ2);
                    timerList.Add(_timerXb);
                    timerList.Add(_timerAc);

                    if (Config.entrance == Entrance.PIN)
                    {
                        timerList.Add(_timerAcHt);
                        timerList.Add(_timerAcQ1);
                        timerList.Add(_timerAcQ2);
                    }
                }
                else if (Config.source == Source.XB)
                {
                    //回傳數據集中管理
                    dataConsolidationDic = new Dictionary<GpType, DataConsolidation>()
                    {
                        { GpType.Zp, new DataConsolidation()},
                        { GpType.Ds, new DataConsolidation()},
                        { GpType.Zd, new DataConsolidation()},
                    };

                    //聯盟請求計時器
                    Common.SetTimer(_timerDsReqLeague, 100, false, new ElapsedEventHandler((s, e) => ReqLeagueTick(s, e, GpType.Ds)));

                    if (Config.entrance == Entrance.XB_New)
                    {
                        Common.SetTimer(_timerZdReqLeague, 110, false, new ElapsedEventHandler((s, e) => ReqLeagueTick(s, e, GpType.Zd)));
                        Common.SetTimer(_timerZpReqLeague, 120, false, new ElapsedEventHandler((s, e) => ReqLeagueTick(s, e, GpType.Zp)));
                    }

                    //DsMore
                    Common.SetTimer(_timerDsMore, 10000, false, new ElapsedEventHandler((s, e) => MoreTick(s, e, threadInfos[GpType.DsMore], moreTypeDs)));
                    //Ds更多佇列
                    Common.SetTimer(_timerDsMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueMoreTick(s, e, Config.dsMoreQueue, GpType.DsMore)));

                    //ZdMore
                    Common.SetTimer(_timerZdMore, 5000, false, new ElapsedEventHandler((s, e) => MoreTick(s, e, threadInfos[GpType.ZdMore], moreTypeZd)));
                    //Zd更多佇列處理
                    Common.SetTimer(_timerZdMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueMoreTick(s, e, Config.zdMoreQueue, GpType.ZdMore)));

                    //ZpMore
                    Common.SetTimer(_timerZpMore, 10000, false, new ElapsedEventHandler((s, e) => MoreTick(s, e, threadInfos[GpType.ZpMore], moreTypeZp)));
                    //Zp更多佇列處理
                    Common.SetTimer(_timerZpMoreQueue, 1, false, new ElapsedEventHandler((s, e) => QueueMoreTick(s, e, Config.zpMoreQueue, GpType.ZpMore)));

                    timerList.Add(_timerDsMore);
                    timerList.Add(_timerZdMore);
                    timerList.Add(_timerZpMore);
                }
            }
        }

        #endregion

        #region 計時器觸發事件

        //登入觸發事件
        private async void LoginTick(object sender, ElapsedEventArgs e)
        {
            UserInfo userInfo = null;

            try
            {
                Display.SetDictInfo(Display.Type.Warnning, "");

                string msg = "";

                //取出未登入帳號
                foreach (UserInfo tmp in FrmUser.users.Values)
                {
                    //清空訊息
                    Display.SetDictInfo(Display.Type.User, "");

                    if (tmp.Status == 0)
                    {
                        userInfo = tmp;
                        break;
                    }
                }

                if (Config.source == Source.SB)
                {
                    //成功的帳號筆數
                    int successCount = FrmUser.users.Where(a => a.Value.Status == 1).Count();
                    //已有1組帳號成功登入，則不允許其他帳號登入
                    if (successCount > 0)
                    {
                        userInfo = null;
                    }
                    //免登版且沒有帳號，生成假帳號
                    else if (Config.entrance == Entrance.NoLogin && FrmUser.users.Count == 0)
                    {
                        userInfo = new UserInfo("login01", "123", false);
                        FrmUser.users.TryAdd(userInfo.User, userInfo);
                    }
                }
                else if (Config.source == Source.BET365)
                {
                    if (Config.gpType != GpType.Zd)
                    {
                        var notExistUser = _request365.LoginUsers.Where(a => !FrmUser.users.TryGetValue(a.Account, out UserInfo user)).Select(a => a.Account);
                        _request365.RemoveLoginUser(notExistUser.ToArray());
                    }
                    else
                    {
                        userInfo = null;
                    }
                }

                //取得代理資訊
                var webProxy = Config.Proxy.GetProxy();
                string proxy = webProxy?.Address.Authority;

                //有帳號需要登入
                if (userInfo != null)
                {
                    if (Config.isOpenDs || Config.isOpenZd || Config.isOpenZp)
                    {
                        switch (Config.source)
                        {
                            case Source.PIN:
                                _sourcePIN.Login(Config.url, userInfo, proxy);
                                break;
                            case Source.SB:
                                _commonSB.Login(Config.url, userInfo, proxy);
                                break;
                            case Source.BET365:
                                LoginUser365 loginUser = _request365.AddLoginUser(userInfo.User, userInfo.Pwd, () => { userInfo.Status = 0; });

                                if (loginUser.Login(Config.url, _request365, proxy))
                                {
                                    userInfo.Status = 1;
                                }
                                else
                                {
                                    userInfo.Status = -1;
                                }

                                break;
                        }

                        //登入成功
                        if (userInfo.Status == 1)
                        {
                            Display.SetDictInfo(Display.Type.User, YwCore.Properties.Resources.Account + userInfo.User + YwCore.Properties.Resources.LoginSuccess);
                        }
                        //登入失敗
                        else
                        {
                            Display.SetDictInfo(Display.Type.User, YwCore.Properties.Resources.Account + userInfo.User + YwCore.Properties.Resources.LoginFalse);
                            userInfo.Status = -1;
                        }
                    }
                }
                //無帳號
                else
                {
                    switch (Config.source)
                    {
                        case Source.SB:
                            //1:賽事關閉 0:正常
                            threadInfos[GpType.Zd].DataId = Config.isOpenZd ? 0 : 1;
                            threadInfos[GpType.Ds].DataId = Config.isOpenDs ? 0 : 1;
                            threadInfos[GpType.Zp].DataId = Config.isOpenZp ? 0 : 1;

                            //SB定期驗證狀態
                            foreach (UserInfo tmpUser in FrmUser.users.Values)
                            {
                                _commonSB.CheckStatus(tmpUser, proxy);
                            }

                            break;
                        case Source.BET365:

                            if (Config.isOpenZd)
                            {
                                //1:賽事關閉 0:正常
                                threadInfos[GpType.Zd].DataId = 0;
                                _connect365.Connect(new Uri(Config.url), proxy);
                            }
                            else
                            {
                                //1:賽事關閉 0:正常
                                threadInfos[GpType.Zd].DataId = 1;
                                await _connect365.Close();
                            }

                            msg = YwCore.Properties.Resources.ConnectCount + ":" + _connect365.ConnectCount + "/" + _connect365.TotalConnect;
                            Display.SetDictInfo(Display.Type.Custom, msg);
                            break;
                        case Source.JZ:
                            IPEndPoint useProxy = null;

                            if (webProxy != null)
                            {
                                useProxy = new IPEndPoint(IPAddress.Parse(webProxy.Address.Host), webProxy.Address.Port);
                            }

                            //連線JZ
                            _connectJZ.Login(Config.url, ReqJZ.GetResponse, Config.isOpenDs, Config.isOpenZd, useProxy);
                            _connectJZ_Ayh.Login(Config.url, ReqJZ.GetResponseAyh, Config.isOpenDs && Config.isTrackOlympics, Config.isOpenZd && Config.isTrackOlympics, useProxy);

                            //已連線數、總連線數
                            int loginCount, total;

                            if (Config.isTrackOlympics)
                            {
                                loginCount = _connectJZ.userList.Where(x => x.Status == 1).Count() + _connectJZ_Ayh.userList.Where(x => x.Status == 1).Count();
                                total = _connectJZ.userList.Length + _connectJZ_Ayh.userList.Length;
                            }
                            else
                            {
                                loginCount = _connectJZ.userList.Where(x => x.Status == 1).Count();
                                total = _connectJZ.userList.Length;
                            }

                            msg = $"{YwCore.Properties.Resources.ConnectCount}:{loginCount}/{total}";
                            //清空訊息
                            Display.SetDictInfo(Display.Type.Custom, msg);

                            //1:賽事關閉 0:正常
                            threadInfos[GpType.Zd].DataId = Config.isOpenZd ? 0 : 1;
                            threadInfos[GpType.Ds].DataId = Config.isOpenDs ? 0 : 1;

                            break;
                        case Source.Radar:
                            List<int> producerId = new List<int>();

                            if (Config.isOpenDs)
                            {
                                producerId.Add(3);
                                threadInfos[GpType.Ds].DataId = 0;
                            }
                            else
                            {
                                //1:賽事關閉 0:正常
                                threadInfos[GpType.Ds].DataId = 1;
                            }

                            if (Config.isOpenZd)
                            {
                                producerId.Add(1);
                                threadInfos[GpType.Zd].DataId = 0;
                            }
                            else
                            {
                                threadInfos[GpType.Zd].DataId = 1;

                            }

                            if (producerId.Count > 0 && !_connectRadar.IsOpen)
                            {
                                await _connectRadar.ConnectAsync(producerId, Config.url, proxy: proxy);
                            }

                            msg = YwCore.Properties.Resources.ConnectCount + ":" + (_connectRadar.IsOpen ? 1 : 0).ToString() + "/1";
                            Display.SetDictInfo(Display.Type.Custom, msg);
                            break;
                    }
                }

                if (Config.entrance == Entrance.LEO || Config.entrance == Entrance.Asia)
                {
                    //帳號定期檢查登出
                    foreach (UserInfo user in FrmUser.users.Values.Where(a => a.Status == 1))
                    {
                        //LEO重新登出邏輯(帳號登入後1時55分到2時05分間，會重新登入)
                        if (Config.entrance == Entrance.LEO && DateTime.Now.Subtract(user.LoginTimestamp).TotalSeconds > user.TimeLimitForRelogin)
                        {
                            user.Status = 0;
                            Display.SetDictInfo(Display.Type.User, YwCore.Properties.Resources.Account + user.User + YwCore.Properties.Resources.Logout, false);
                        }
                        else if (DateTime.Now.Subtract(user.CheckTime).TotalSeconds > 20)
                        {
                            _sourcePIN.LoginObj.CheckLogout(user.RedirectUrl, user, proxy);
                            user.CheckTime = DateTime.Now;

                            if (user.Status != 1)
                            {
                                Display.SetDictInfo(Display.Type.User, YwCore.Properties.Resources.Account + user.User + YwCore.Properties.Resources.Logout, false);
                            }
                        }
                    }
                }
            }
            catch (WebException wex)
            {
                if (wex.Status == WebExceptionStatus.RequestCanceled ||
                    wex.Status == WebExceptionStatus.ProtocolError ||
                    wex.Status == WebExceptionStatus.Timeout ||
                    wex.Status == WebExceptionStatus.ConnectFailure ||
                    wex.Status == WebExceptionStatus.ServerProtocolViolation)
                {
                    //登入失敗累加次數
                    if (FrmUrl.urlList.TryGetValue(currentUrl, out UrlInfo urlInfo))
                    {
                        urlInfo.SetCheck(urlInfo.ErrorCount);
                    }

                    //切換代理
                    Config.Proxy.Next();

                    if (wex.Message.Contains("維修"))
                    {
                        Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.SourceMaintenance);
                        Common.SendMessage.MaintenanceSend();
                    }
                    else if (wex.Message.Contains("限制"))
                    {
                        Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.Restrict);
                    }
                    else
                    {
                        Display.SetDictInfo(Display.Type.Warnning, wex.Message);
                    }

                    if (userInfo != null)
                    {
                        //登入錯誤次數超過5次，將帳號設為登入失敗(避免多次登入)
                        if (++userInfo.LoginExCount >= 5)
                        {
                            Display.SetDictInfo(Display.Type.User, YwCore.Properties.Resources.Account + userInfo.User + YwCore.Properties.Resources.LoginFalse);
                            userInfo.Status = -1;
                        }
                    }
                }
                else
                {
                    //登入失敗累加次數
                    if (FrmUrl.urlList.TryGetValue(currentUrl, out UrlInfo urlInfo))
                    {
                        urlInfo.SetCheck(urlInfo.ErrorCount);
                    }

                    LogFile.SetLog(LogMode.Error, wex.ToString());
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException?.Message.Contains("net:") == true)
                {
                    //切換代理
                    Config.Proxy.Next();
                    LogFile.SetLog(LogMode.Error, ex.InnerException.ToString());
                }
                else
                {
                    LogFile.SetLog(LogMode.Error, ex.ToString());
                }
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //查詢開賽時間觸發事件
        private void SelectDateTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                var response = Common.GameCacheServiceClient.GameDateSelect(new GameDateSelectRequest() { Sport = (int)Config.sport });

                Config.gameCacheBag = new ConcurrentBag<GameCache>(response.Result.Select(a => new GameCache
                {
                    F_Id = a.Id,
                    F_AllyId = a.AllyId,
                    F_GameDate = TimeStampHelper.ToLocalDateTime(a.GameDate),
                    F_PaymentDate = TimeStampHelper.ToLocalDateTime(a.PaymentDate),
                    F_SuffixId = a.SuffixId,
                    F_TmaId = a.TmaId,
                    F_TmbId = a.TmbId
                }));
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Interval = 600000;
                timer.Start();
            }
        }

        /// <summary>
        /// 更新分組觸發事件
        /// </summary>
        private void UpdateGroupTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                //更新且移到主庫與來源組別有值的情況
                foreach (GameInfo game in Config.gameDic.Values.Where(a => a.IsMoveMain))
                {
                    //取出所有GpdmInfo
                    var gpdmCollect = game.ObjDic.Values;

                    //是否 [Group的組合只有一種]
                    bool isAll = gpdmCollect.GroupBy(a => a.Group).Count() == 1;

                    if (isAll)
                    {
                        #region 更新方式 : 更新同場賽事Gid全部組別

                        //取第一筆
                        GpdmInfo gpdmInfo = gpdmCollect.First();

                        //只需更新組別不同的Kinfo
                        var kInfos = gpdmCollect.SelectMany(a => a.Values).Where(a => gpdmInfo.Group.HasValue && a.Group != gpdmInfo.Group && a.KTypeGroup > 0);
                        bool needUpdate = kInfos.Any();

                        if (needUpdate)
                        {
                            var response = Common.NbbServiceClient.KModeGroupUpdate(new KModeGroupUpdateRequest() { GId = game.GId, Group = gpdmInfo.Group.Value, Sport = (int)Config.sport, KTypeGroup = 0 });

                            if (!string.IsNullOrEmpty(response.ErrorMsg))
                            {
                                LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                                continue;
                            }

                            //更新緩存內群組
                            foreach (KInfo kInfo in kInfos)
                            {
                                kInfo.Group = gpdmInfo.Group.Value;
                            }

                            LogFile.SetLog(LogMode.Update_Group, $"更新組別 => gId:{game.GId}, group:{gpdmInfo.Group.Value}");
                        }

                        #endregion
                    }
                    else
                    {
                        #region 更新方式 : 只更新相同KTypeGroup組別

                        foreach (GpdmInfo gpdmInfo in gpdmCollect.Where(a => a.Group.HasValue))
                        {
                            //只需更新組別不同的Kinfo
                            var kInfos = gpdmInfo.Values.Where(a => a.Group != gpdmInfo.Group && a.KTypeGroup > 0);
                            bool needUpdate = kInfos.Any();

                            if (needUpdate)
                            {
                                int kTypeGroup = kInfos.First().KTypeGroup;

                                var response = Common.NbbServiceClient.KModeGroupUpdate(new KModeGroupUpdateRequest() { GId = game.GId, Group = gpdmInfo.Group.Value, Sport = (int)Config.sport, KTypeGroup = kTypeGroup });

                                if (!string.IsNullOrEmpty(response.ErrorMsg))
                                {
                                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                                    continue;
                                }

                                //更新緩存內群組
                                foreach (KInfo kInfo in kInfos)
                                {
                                    kInfo.Group = gpdmInfo.Group.Value;
                                }

                                LogFile.SetLog(LogMode.Update_Group, $"更新組別 => gId:{game.GId}, gpdm:{gpdmInfo.Key}, kTypeGroup:{kTypeGroup}, group:{gpdmInfo.Group.Value}");
                            }
                        }

                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //Mongo更新結束後處理委派事件
        private static event Action AfterUpdateHandle;

        /// <summary>
        /// 更新TS來源
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UpdateTsSourceIdTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (Config.isFollow && Config.isOpenZd)
                {
                    //當前時間
                    DateTime now = DateTime.Now;
                    List<OddsSettingDto> zdbdOddsSettings = new List<OddsSettingDto>();
                    AfterUpdateHandle = null;

                    //需標單的 ktype
                    int[] ktypes ={ 12001, 12002, 12003, 12005, 12007,
                                    12101, 12102, 12103, 12105, 12107,
                                    12201, 12202, 12203, 12205, 12207,
                                    12301, 12302, 12303, 12305, 12307,
                                    12401, 12402, 12403, 12405, 12407,
                                    12501, 12502, 12503, 12505, 12507,
                                    12601, 12602, 12603, 12605, 12607 };

                    foreach (GameInfo game in Config.gameDic.Values.Where(a => a.GpType == GpType.Zd && a.IsMoveMain))
                    {
                        foreach (GpdmInfo gpdm in game.ObjDic.Values.Where(a => a.IsMain && (a.IsUpdateTsSource || now.Subtract(a.LastInsertTime).TotalSeconds >= 118)))
                        {
                            Source tmpSource = gpdm.TsSource;

                            foreach (KInfo kInfo in gpdm.Values.Where(a => ktypes.Contains(a.KType)))
                            {
                                foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                                {
                                    OddsSettingDto oddsSetting = new OddsSettingDto()
                                    {
                                        GId = game.GId,
                                        GType = game.GType,
                                        KType = kInfo.KType,
                                        BType = bInfo.BType,
                                        SetType = kInfo.SetType,
                                        SourceId = (int)Config.source,
                                        JzSourceId = (int)tmpSource,
                                    };

                                    zdbdOddsSettings.Add(oddsSetting);
                                }
                            }

                            AfterUpdateHandle += () =>
                            {
                                gpdm.IsUpdateTsSource = tmpSource != gpdm.TsSource;
                                gpdm.LastInsertTime = now;
                            };
                        }
                    }

                    #region Mongo

                    //是否成功
                    bool isSuccess = true;

                    if (zdbdOddsSettings.Any())
                    {
                        //設定發送時間
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        //ZDBD Upsert
                        var request = new ZDBD.Service.DataToAPRequest();
                        request.Data.AddRange(zdbdOddsSettings);
                        var oddUpSertResult = await Config.ServiceClient.DataToAPAsync(request);
                        //設定回傳時間
                        stopwatch.Stop();
                        //Mongo回傳內容1
                        string item1 = string.Empty;

                        if (!string.IsNullOrEmpty(oddUpSertResult.ErrorMsg))
                        {
                            item1 = oddUpSertResult.ErrorMsg;
                            LogFile.SetLog(LogMode.Error, $"UpsertMany_Item1:{item1}");
                            isSuccess = false;
                        }

                        //加入佇列
                        LogProcess.AddQueue(new ZdbdLog(Source.JZ, GpType.Zd.ToString(), zdbdOddsSettings, LogMode.UpsertMany, stopwatch.Elapsed, Config.entrance));
                    }

                    if (isSuccess && AfterUpdateHandle != null)
                    {
                        AfterUpdateHandle?.Invoke();
                        AfterUpdateHandle = null;
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //[常用]請求數據觸發事件
        private void MainTick(object sender, ElapsedEventArgs e, ThreadInfo threadInfo)
        {
            if (!Enum.TryParse(threadInfo.StrType, out GpType gpType)) { return; }

            System.Timers.Timer timer = (System.Timers.Timer)sender;
            Source source = Config.source;
            //逾時間隔
            int timeoutInterval = (int)timer.Interval;
            //設置逾時秒數(間隔1秒則逾時3秒，其餘逾時5秒)
            timeoutInterval = timeoutInterval == 1000 ? 3000 : 5000;

            try
            {
                //是否任一主開關開放
                bool isAnyOpen = Config.isOpenDs || Config.isOpenZp || Config.isOpenZd;

                //判斷該選項是否開啟
                if ((gpType == GpType.Ds && !Config.isOpenDs) ||
                    (gpType == GpType.Zd && !Config.isOpenZd) ||
                    (gpType == GpType.Zp && !Config.isOpenZp) ||
                    (gpType == GpType.Dj && (!Config.isOpenDj || !isAnyOpen)) ||
                    (gpType == GpType.DjQ2 && (!Config.isOpenDj || !isAnyOpen)) ||
                    (gpType == GpType.Xb && (!Config.isOpenXb || !isAnyOpen)) ||
                    (gpType == GpType.Ac && (!Config.isOpenAc || !isAnyOpen)) ||
                    (gpType == GpType.AcHT && (!Config.isOpenAc || !isAnyOpen)) ||
                    (gpType == GpType.AcQ1 && (!Config.isOpenAc || !isAnyOpen)) ||
                    (gpType == GpType.AcQ2 && (!Config.isOpenAc || !isAnyOpen)))
                {
                    threadInfo.DataId = threadInfo.SendId + 1;
                    return;
                }

                //發送流水號遞增
                threadInfo.SendId++;
                //建立請求資訊
                ReqInfo reqInfo = new ReqInfo(threadInfo, threadInfo.SendId);
                //取得代理資訊
                string proxy = Config.Proxy.GetProxy()?.Address.Authority;
                //非同步作業
                IAsyncResult result = null;

                switch (source)
                {
                    case Source.PIN:
                        IRequest req;

                        switch (Config.entrance)
                        {
                            case Entrance.LEO:
                            case Entrance.Asia:
                                req = new ReqNewPIN(threadInfo.SendId, gpType, Config.url, FrmUser.users) { Proxy = proxy };
                                break;
                            case Entrance.PIN:
                            default:
                                req = new ReqPIN(threadInfo.SendId, gpType, Config.url, FrmUser.users) { Proxy = proxy };
                                break;
                        }

                        _sourcePIN.GetData(req);
                        return;
                    case Source.XB:

                        if (dataConsolidationDic.TryGetValue(gpType, out DataConsolidation dc))
                        {
                            //取出來源左側球類清單賽事筆數
                            ParserXB.DictLeague.TryGetValue(gpType, out List<string> gameCount);

                            int pageCount = gameCount == null ? 0 : gameCount.Count;
                            //設定發送資訊
                            dc.SetSendInfo(threadInfo.SendId, pageCount == 0 ? 1 : gameCount.Count);

                            if (pageCount > 0)
                            {
                                //分頁請求
                                for (int i = 0; i < pageCount; i++)
                                {
                                    ReqInfo pageReqInfo = new ReqInfo(threadInfo, threadInfo.SendId);

                                    if (Config.entrance == Entrance.XB)
                                    {
                                        ReqXB.SetHttpWebRequest(pageReqInfo, proxy, 0, gameCount[i]);
                                    }
                                    else
                                    {
                                        ReqXB_New.SetHttpWebRequest(pageReqInfo, proxy, 0, gameCount[i]);
                                    }

                                    IAsyncResult tmpResult = pageReqInfo.Req.BeginGetResponse(new AsyncCallback(GetAsyncResponse), pageReqInfo);
                                    ThreadPool.RegisterWaitForSingleObject(tmpResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), pageReqInfo, timeoutInterval, true);
                                }
                            }
                            else
                            {
                                ReqInfo pageReqInfo = new ReqInfo(threadInfo, threadInfo.SendId);

                                if (Config.entrance == Entrance.XB)
                                {
                                    ReqXB.SetHttpWebRequest(pageReqInfo, proxy);
                                }
                                else
                                {
                                    ReqXB_New.SetHttpWebRequest(pageReqInfo, proxy);
                                }

                                IAsyncResult tmpResult = pageReqInfo.Req.BeginGetResponse(new AsyncCallback(GetAsyncResponse), pageReqInfo);
                                ThreadPool.RegisterWaitForSingleObject(tmpResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), pageReqInfo, timeoutInterval, true);
                            }
                        }

                        break;
                    case Source.BET365:
                        _request365.Timeout = Config.dsSec * 1000;
                        _request365.HttpProxy = proxy;
                        _request365.GetDsData(threadInfo, Config.url, Config.dsQueue, proxy);
                        Config.url = _request365.MainUrl;
                        return;
                    case Source.SBO:
                        ReqSBO.SetHttpWebRequest(reqInfo, proxy);
                        if (reqInfo.Req == null) { return; }
                        result = reqInfo.Req.BeginGetResponse(GetAsyncResponse, reqInfo);
                        break;
                    case Source.MyStake:
                        ReqMyStake.SetHttpWebRequest(reqInfo, proxy);
                        if (reqInfo.Req == null) { return; }
                        result = reqInfo.Req.BeginGetResponse(GetAsyncResponse, reqInfo);
                        break;
                }

                if (reqInfo.Req != null)
                {
                    //設定非同步逾時
                    ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), reqInfo, timeoutInterval, true);
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                #region 判斷間隔是否改變

                int sec = Config.dsSec;

                switch (gpType)
                {
                    case GpType.Ds:
                        sec = Config.dsSec;
                        break;
                    case GpType.Zd:
                        sec = Config.zdSec;
                        break;
                    case GpType.Zp:
                        sec = Config.zpSec;
                        break;
                    case GpType.Dj:
                    case GpType.DjQ2:
                        sec = Config.djSec;
                        break;
                    case GpType.Xb:
                        sec = Config.xbSec;
                        break;
                    case GpType.Ac:
                    case GpType.AcQ1:
                    case GpType.AcQ2:
                        sec = Config.acSec;
                        break;
                }

                //間隔改變則重新設置
                if (sec > 0 && sec != timer.Interval)
                {
                    timer.Interval = sec;
                }

                #endregion
            }
        }

        //[更多]請求數據觸發事件
        private void MoreTick(object sender, ElapsedEventArgs e, ThreadInfo threadInfo, MoreType moreType)
        {
            System.Timers.Timer timer = (System.Timers.Timer)sender;
            Source source = Config.source;

            try
            {
                //GP類型
                GpType gpType = moreType.GpType;
                //設置逾時時間
                int moreSec = gpType == GpType.Ds ? Config.dsSec : gpType == GpType.Zd ? Config.zdSec : Config.zpSec;
                //收數據待解析佇列
                ConcurrentQueue<DataInfo> cQueue = gpType == GpType.Ds ? Config.dsMoreQueue : gpType == GpType.Zd ? Config.zdMoreQueue : Config.zpMoreQueue;

                //開始發送
                if (moreType.IsStart)
                {
                    //取無數據則跳過
                    if (!moreType.MoreQueue.TryDequeue(out decimal hgid)) { return; }

                    //設定要求
                    ReqInfo reqInfo = new ReqInfo(threadInfo, threadInfo.SendId);
                    //取得代理資訊
                    string proxy = Config.Proxy.GetProxy()?.Address.Authority;

                    //設置請求&非同步
                    switch (source)
                    {
                        case Source.XB:

                            if (Config.entrance == Entrance.XB)
                            {
                                ReqXB.SetHttpWebRequest(reqInfo, proxy, hgid);
                            }
                            else
                            {
                                ReqXB_New.SetHttpWebRequest(reqInfo, proxy, hgid);
                            }

                            break;
                        case Source.SBO:
                            if (ParserSBO.RequestKey.TryGetValue(gpType, out ConcurrentDictionary<decimal, string> dicSBO) &&
                                dicSBO.TryGetValue(hgid, out string requestKey))
                            {
                                ReqSBO.SetHttpWebRequest(reqInfo, proxy, hgid, requestKey);
                            }
                            break;
                        case Source.MyStake:
                            ReqMyStake.SetHttpWebRequest(reqInfo, proxy, hgid);
                            break;
                        default:
                            return;
                    }

                    //防止null
                    if (reqInfo.Req == null) { return; }

                    //設置逾時秒數(間隔1秒則逾時3秒，其餘逾時5秒)
                    moreSec = moreSec == 1000 ? 3000 : 5000;

                    //設定非同步
                    IAsyncResult result = reqInfo.Req.Method == "GET" ? reqInfo.Req.BeginGetResponse(GetMoreAsyncResponse, reqInfo) : reqInfo.Req.BeginGetResponse(SendPostAsyncRequest, reqInfo);
                    //設定非同步逾時
                    ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), reqInfo, moreSec, true);
                    //蒐集所有內頁請求物件
                    moreType.WaitHandles.Add(result);
                }
                //採集
                else
                {
                    //是否勾選
                    bool isOpen = moreType.GpType == GpType.Ds ? (Config.isOpenDs) :
                                    moreType.GpType == GpType.Zp ? (Config.isOpenZp) :
                                    (Config.isOpenZd);
                    if (!isOpen)
                    {
                        timer.Interval = 3000;
                        return;
                    }

                    HashSet<decimal> tmp = new HashSet<decimal>();

                    switch (source)
                    {
                        case Source.XB:
                            if (!ParserXB.moreDic.TryGetValue(moreType.GpType, out tmp)) { return; }

                            if (ParserXB.dicParentId.TryGetValue(moreType.GpType, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDicParentId))
                            {
                                HashSet<decimal> tmpSceneId = new HashSet<decimal>(tmpDicParentId.Values.SelectMany(v => v));

                                //重建記憶體避免汙染
                                tmp = new HashSet<decimal>(tmp);
                                //合併內頁節次ID一起列入請求
                                tmp.UnionWith(tmpSceneId);
                            }

                            moreType.MoreQueue = new ConcurrentQueue<decimal>(tmp);
                            break;
                        case Source.SBO:
                            if (!ParserSBO.MoreDic.TryGetValue(moreType.GpType, out tmp)) { return; }
                            moreType.MoreQueue = new ConcurrentQueue<decimal>(tmp);
                            break;
                        case Source.MyStake:
                            if (!ParserMyStake.moreDic.TryGetValue(moreType.GpType, out tmp)) { return; }
                            moreType.MoreQueue = new ConcurrentQueue<decimal>(tmp);
                            break;
                    }

                    //待請求佇列有量且待解析佇列為空時則準備發送內頁請求
                    if (moreType.MoreQueue.Count > 0 && cQueue.Count == 0)
                    {
                        //發送筆數
                        int sendCount = moreType.MoreQueue.Count + 1;
                        //計算平均間隔
                        int interval = moreSec / sendCount;

                        //流水號遞增
                        threadInfo.SendId++;
                        //啟動發送請求
                        moreType.IsStart = true;

                        //間隔不同則重新設置
                        if (timer.Interval != interval)
                        {
                            timer.Interval = interval;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString(), moreType.GpType.ToString());
            }
            finally
            {
                if (moreType.WaitHandles.Count > 30)
                {
                    moreType.WaitHandles = moreType.WaitHandles.Where(x => !x.IsCompleted).ToList();
                }
                //佇列無數劇則清除啟動
                else if (moreType.MoreQueue.Count == 0 && (moreType.WaitHandles.Count == 0 || WaitHandle.WaitAll(moreType.WaitHandles.Select(x => x.AsyncWaitHandle).ToArray())))
                {
                    moreType.IsStart = false;
                    moreType.WaitHandles.Clear();
                }

                //啟動計時器
                timer.Start();
            }
        }

        //[常用]佇列觸發事件
        private void QueueTick(object sender, ElapsedEventArgs e, ConcurrentQueue<DataInfo> cQueue)
        {
            try
            {
                //無數據則跳過
                while (cQueue.TryDequeue(out DataInfo dataInfo))
                {
                    if (!Enum.TryParse(dataInfo.StrType, out GpType gpType)) { return; }

                    //註記提醒任務最後更新時間戳
                    if (Config.remindInfos.TryGetValue(gpType.ToString("g"), out IRemind remind))
                    {
                        remind.SetLastTime(DateTime.Now);
                    }

                    //HTML數據處理
                    if (!dataInfo.IsWebSocket)
                    {
                        //防止數據先送後到
                        if (threadInfos.TryGetValue(gpType, out ThreadInfo threadInfo) && threadInfo.DataId < dataInfo.SendId)
                        {
                            int index = gpType == GpType.Zd ? 0 : gpType == GpType.Ds ? 1 : gpType == GpType.Zp ? 2 : 3;

                            #region 依照來源解析

                            bool isComplete = true;

                            if (Config.source == Source.PIN)
                            {
                                switch (Config.entrance)
                                {
                                    case Entrance.LEO:
                                    case Entrance.Asia:
                                        ParserNewPIN.Parser(dataInfo.Data, dataInfo.SendId, gpType);
                                        break;
                                    case Entrance.PIN:
                                    default:
                                        ParserPIN.Parser(dataInfo.Data, dataInfo.SendId, gpType);
                                        break;
                                }
                            }
                            else if (Config.source == Source.XB)
                            {
                                if (Config.entrance == Entrance.XB && gpType == GpType.Zd)
                                {
                                    ParserXB.Parser(dataInfo.Data, dataInfo.SendId, gpType);
                                }
                                else
                                {
                                    if (dataConsolidationDic.TryGetValue(gpType, out DataConsolidation dc) &&
                                         dc.DataCentralize(dataInfo.SendId, dataInfo.Data, out var datas))
                                    {
                                        ParserXB.Parser(datas, dataInfo.SendId, gpType);
                                    }
                                    else
                                    {
                                        isComplete = false;
                                    }
                                }
                            }
                            else if (Config.source == Source.BET365)
                            {
                                //數據解析
                                bool isUpdate = Parser365.DsParser(_request365, dataInfo);

                                //請求數據皆已回來，待處理資料柱列無東西時
                                if (_request365.CompleteId == dataInfo.SendId && !cQueue.Any(a => a.SendId == dataInfo.SendId))
                                {
                                    //數據流水號更新發送流水號
                                    threadInfo.DataId = _request365.CompleteId;
                                }

                                isHandle[1] = isUpdate;
                                return;
                            }
                            else if (Config.source == Source.SBO)
                            {
                                ParserSBO.Parser(dataInfo.Data, dataInfo.SendId, gpType);
                            }
                            else if (Config.source == Source.MyStake)
                            {
                                ParserMyStake.Parser(dataInfo.Data, dataInfo.SendId, gpType);
                            }

                            #endregion

                            if (isComplete)
                            {
                                //數據流水號更新發送流水號
                                threadInfo.DataId = dataInfo.SendId;

                                if (index < 3)
                                {
                                    isHandle[index] = true;
                                }
                            }
                        }
                    }
                    //WebSocket數據處理
                    else
                    {
                        if (Config.source == Source.BET365)
                        {
                            Parser365.MoreParser(dataInfo, _connect365);
                        }
                        else if (Config.source == Source.JZ)
                        {
                            ParserJZ.Parser(dataInfo);
                        }
                        else if (Config.source == Source.SB)
                        {
                            ParserSB.Parser(dataInfo, _commonSB);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //[更多]佇列觸發事件
        private void QueueMoreTick(object sender, ElapsedEventArgs e, ConcurrentQueue<DataInfo> cQueue, GpType gpType)
        {
            GameInfo gameInfo = null;

            try
            {
                //無數據則跳過
                while (cQueue.TryDequeue(out DataInfo dataInfo))
                {
                    //註記提醒任務最後更新時間戳
                    if (Config.remindInfos.TryGetValue(gpType.ToString("g"), out IRemind remind))
                    {
                        remind.SetLastTime(DateTime.Now);
                    }

                    //賽事字典
                    ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;

                    //HTML數據處理
                    if (!dataInfo.IsWebSocket)
                    {
                        if (Config.source == Source.SBO)
                        {
                            //賽事GameKey清單
                            List<string> gameKeys = new List<string>();
                            GpType tmpGpType = gpType & (GpType.Ds | GpType.Zd | GpType.Zp);
                            if (!ParserSBO.DictMain.TryGetValue(tmpGpType, out ConcurrentDictionary<decimal, string> tmpMainDic)) { continue; }

                            //來源目錄額外抓取玩法處理
                            if (ParserSBO.DictPlay.TryGetValue(tmpGpType, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDictPlay)
                                && tmpDictPlay.TryGetValue(dataInfo.MainId, out HashSet<decimal> tmpHashSet))
                            {
                                foreach (decimal sourceId in tmpHashSet)
                                {
                                    if (tmpMainDic.TryGetValue(sourceId, out string gameKey))
                                    {
                                        gameKeys.Add(gameKey);
                                    }
                                }
                            }
                            //一般處理
                            else if (tmpMainDic.TryGetValue(dataInfo.MainId, out string gameKey))
                            {
                                gameKeys.Add(gameKey);
                            }
                            else
                            {
                                continue;
                            }

                            //取首筆gameInfo判斷 防止數據先送後到
                            if (gameDic.TryGetValue(gameKeys.First(), out gameInfo))
                            {
                                long dataId = gameInfo.MoreDataDic.GetOrAdd(dataInfo.MainId, new long[2]).First();

                                //防止數據先送後到
                                if (dataId > dataInfo.SendId) { return; }

                                //解析數據
                                ParserSBO.MoreParser(dataInfo.Data, dataInfo.SendId, gpType, dataInfo.MainId);

                                foreach (string gameKey in gameKeys)
                                {
                                    if (gameDic.TryGetValue(gameKey, out GameInfo getGameInfo))
                                    {
                                        var moreData = getGameInfo.MoreDataDic.GetOrAdd(dataInfo.MainId, new long[2]);
                                        //數據流水號更新發送流水號
                                        moreData[0] = dataInfo.SendId;
                                        //正常解析則清除錯誤次數
                                        moreData[1] = 0;
                                    }
                                }
                            }
                        }
                        //取出賽事
                        else if (gameDic.TryGetValue(dataInfo.GameKey, out gameInfo))
                        {
                            //內頁關連不存在則添加
                            long[] moreDataId = gameInfo.MoreDataDic.GetOrAdd(dataInfo.MainId, _ => new long[2]);

                            //防止數據先送後到
                            if (moreDataId[0] < dataInfo.SendId)
                            {
                                //解析數據
                                if (Config.source == Source.MyStake)
                                {
                                    ParserMyStake.MoreParser(dataInfo.Data, dataInfo.SendId, gpType, dataInfo.MainId, dataInfo.GameKey);
                                }
                                else
                                {
                                    ParserXB.MoreParser(dataInfo.Data, dataInfo.SendId, gpType, dataInfo.MainId, dataInfo.GameKey);
                                }

                                //數據流水號更新發送流水號
                                moreDataId[0] = dataInfo.SendId;
                                //正常解析則清除錯誤次數
                                moreDataId[1] = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //檢查計時器 觸發事件
        private void CheckTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                //未啟動則跳過
                if (!Config.isFollow) { return; }

                //賽事字典
                List<GameInfo> gameDic = Config.gameDic.Values.Where(a => a.ObjDic.Count != 0).ToList();

                #region 查詢聯盟Id

                //挑選沒有聯盟Id的賽事
                List<List<GameInfo>> gameList = (from game in gameDic
                                                 where game.League.Id == 0
                                                 group game by new { game.League.Name } into tmpGroup
                                                 select tmpGroup.ToList()).ToList();

                for (int i = 0; i < gameList.Count; i++)
                {
                    SetField.SetLeague(gameList[i]);
                }

                #endregion

                #region 查詢後綴Id

                //挑選沒有設定後綴Id的賽事
                List<List<GameInfo>> suffixGameList = (from game in gameDic
                                                       where !string.IsNullOrEmpty(game.Suffix.Name) && game.Suffix.Id == 0
                                                       group game by new { game.Suffix.Name } into tmpGrop
                                                       select tmpGrop.ToList()).ToList();

                for (int i = 0; i < suffixGameList.Count; i++)
                {
                    SetField.SetSuffix(suffixGameList[i]);
                }

                #endregion

                #region 查詢隊伍Id

                //挑選沒有隊伍Id的賽事
                List<List<GameInfo>> teamList = (from game in gameDic
                                                 where game.League.Id > 0 && (
                                                 game.TeamA.Id == 0 || game.TeamB.Id == 0)
                                                 group game by new { game.League.Name } into tmpGroup
                                                 select tmpGroup.ToList()).ToList();

                for (int i = 0; i < teamList.Count; i++)
                {
                    SetField.SetTeam(teamList[i]);
                }

                #endregion

                #region 查詢開賽時間

                //挑選需查詢開賽時間的賽事
                List<GameInfo> gameDateList = (from game in gameDic
                                               where game.League.Id > 0 &&
                                                game.TeamA.Id > 0 &&
                                                game.TeamB.Id > 0 &&
                                                (string.IsNullOrEmpty(game.Suffix.Name) ? true : game.Suffix.Id > 0) &&
                                                game.IsDateFromDb && game.PaymentDate == DateTime.MinValue
                                               select game).ToList();

                //取得目前時間
                DateTime dateTime = DateTime.Now;

                foreach (GameInfo game in gameDateList)
                {
                    //從緩存取得該比賽事資訊
                    var cache = Config.gameCacheBag.Where(a => a.F_AllyId == game.League.Id
                    && a.F_SuffixId == game.Suffix.Id
                    && a.F_TmaId == game.TeamA.Id
                    && a.F_TmbId == game.TeamB.Id
                    && a.F_PaymentDate <= dateTime.Date.AddDays(1)
                    && a.F_PaymentDate >= dateTime.Date).OrderBy(a => a.F_GameDate);

                    //有緩存取緩存開賽時間，否則取當前時間
                    if (cache.Any())
                    {
                        GameCache single = cache.First();
                        game.SetGameDate(single.F_GameDate);
                    }
                    else
                    {
                        game.SetGameDate(game.GameDateFromSource);
                    }
                }

                #endregion

                //每3分鐘進行清除
                if (dateTime.Minute % 3 == 0 && dateTime.Second == 0)
                {
                    //清除聯盟隊伍Id
                    foreach (GameInfo gameInfo in gameDic)
                    {
                        if (gameInfo.League.Id == -1) { gameInfo.League.Id = 0; }
                        if (gameInfo.Suffix.Id == -1) { gameInfo.Suffix.Id = 0; }
                        if (gameInfo.TeamA.Id == -1) { gameInfo.TeamA.Id = 0; }
                        if (gameInfo.TeamB.Id == -1) { gameInfo.TeamB.Id = 0; }
                    }
                }

            }
            catch (Exception ex)
            {
                Display.SetDictInfo(Display.Type.Error, YwCore.Properties.Resources.LeagueOrTeamFaild + ": " + ex.Message);
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //控制計時器觸發
        private void CtrlTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                //來源消失判斷
                Close();

                for (int i = 0; i < isHandle.Length; i++)
                {
                    GpType gpType = i == 0 ? GpType.Zd : i == 1 ? GpType.Ds : GpType.Zp;

                    if (isHandle[i] || SetHandleReady(gpType))
                    {
                        HandleData(gpType);
                        isHandle[i] = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Exception baseException = ex.GetBaseException();
                Display.SetDictInfo(Display.Type.Error, baseException.Message);
                LogFile.SetLog(LogMode.Error, baseException.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //查詢主庫玩法計時器
        private void SelectKModeTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (Config.dtKModeQueue.TryDequeue(out KModeSelectRequest dt))
                {
                    //是否啟動
                    if (Config.isFollow)
                    {
                        //[新比賽庫]來源開關異動
                        var request = new SourceOpenRecordRequest();
                        {
                            #region 查詢kMode處理

                            Stopwatch stopwatch = Stopwatch.StartNew();
                            //入庫執行
                            var response = Common.NbbServiceClient.KModeSelect(dt);
                            stopwatch.Stop();
                            //加入日誌佇列
                            LogProcess.AddQueue(new SelectKModeLog(Config.source, "", dt.Data, response.Result, stopwatch.Elapsed, Config.entrance));

                            foreach (var row in response.Result)
                            {
                                decimal gpdm = Convert.ToDecimal(row.Gpdm);
                                long gId = Convert.ToInt64(row.GId);
                                int kType = Convert.ToInt32(row.KType);
                                byte setType = Convert.ToByte(row.SetType);
                                int kModeId = Convert.ToInt32(row.Id);
                                int kTypeGroup = Convert.ToInt32(row.KTypeGroup);
                                decimal oddsTotal = Convert.ToDecimal(row.OddsTotal);
                                decimal oddsSet31 = Convert.ToDecimal(row.OddsSet31);
                                bool isZeroLine = Convert.ToBoolean(row.IsZeroLine);
                                bool isOddsFollow = Convert.ToBoolean(row.IsOddsFollow);
                                short group = Convert.ToInt16(row.Group);

                                //取對應物件
                                if (Config.gIdDic.TryGetValue(gId, out string key)
                                    && Config.gameDic.TryGetValue(key, out GameInfo gameInfo)
                                    && gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo)
                                    && gpdmInfo.TryGetValue(kType, out KInfo kInfo))
                                {
                                    //設置玩法集合
                                    kInfo.KTypeGroup = kTypeGroup;
                                    kInfo.KModeId = kModeId;
                                    kInfo.IsZeroLine = isZeroLine;
                                    kInfo.IsOddsFollow = isOddsFollow;
                                    kInfo.Group = group;

                                    //PL總數不同才設置
                                    if (kInfo.OddsTotal != oddsTotal || kInfo.OddsSet31 != oddsSet31)
                                    {
                                        kInfo.OddsTotal = oddsTotal;
                                        kInfo.OddsSet31 = oddsSet31;
                                        kInfo.IsUpdate = true;
                                    }

                                    {
                                        string dicKey = $"{gId},{gpdm}";

                                        //有查到kTypeGroup, 撈取對應物件加入佇列
                                        if (kTypeGroup > 0)
                                        {
                                            {
                                                if (GameStatusSetApi.tmpDicGameStatusSetApiModel.TryRemove(dicKey, out GameStatusSetApiModel tmpModel))
                                                {
                                                    tmpModel.KTypeGroup = kTypeGroup;
                                                    GameStatusSetApi.AddQueue(tmpModel);
                                                }
                                            }

                                            {
                                                SettlementRecordModel tmpModel = new SettlementRecordModel(gId, gameInfo.GType, kTypeGroup);
                                                //來源消失未超過60秒,移除佇列
                                                SettlementRecordHandle.CheckQueue(tmpModel);

                                                //有寫過未切提醒, GP寫入提醒取消
                                                if (SettlementRecordHandle.SettlementRecord.TryRemove(tmpModel.DicKey, out DateTime time))
                                                {
                                                    request.Data.Add(new SourceOpenRecordDto()
                                                    {
                                                        GId = tmpModel.GId,
                                                        GType = tmpModel.GType,
                                                        KTypeGroup = tmpModel.KTypeGroup,
                                                        SourceId = 0,
                                                        IsOpen = 3,
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            //[新比賽庫]來源開關異動紀錄
                            if (request.Data.Count > 0)
                            {
                                //寫入日誌
                                string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString("00");
                                string content = Environment.NewLine + JsonConvert.SerializeObject(request);
                                LogFile.SetLog(LogMode.Remind_SourceOpenRecord, content, "", dateNow);

                                var insertResponse = Common.GameCacheServiceClient.SourceOpenRecord(request);

                                if (!string.IsNullOrEmpty(insertResponse.ErrorMsg))
                                {
                                    LogFile.SetLog(LogMode.Error, $"{insertResponse.GetType().Name} => {insertResponse.ErrorMsg}");
                                }
                            }

                            #endregion
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Display.SetDictInfo(Display.Type.Error, YwCore.Properties.Resources.MainDB + "kmode : " + ex.Message);
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //查詢主庫玩法計時器
        private void SelectOddsTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (Config.dtOddsQueue.TryDequeue(out OddsSelectRequest request))
                {
                    //是否啟動
                    if (Config.isFollow)
                    {
                        #region 查詢odds差距值

                        Stopwatch stopwatch = Stopwatch.StartNew();
                        //入庫執行
                        var response = Common.NbbServiceClient.OddsSelect(request);
                        stopwatch.Stop();

                        //加入日誌佇列
                        LogProcess.AddQueue(new SelectOddsLog(Config.source, "", request.Data, response.Result, stopwatch.Elapsed, Config.entrance));

                        foreach (var row in response.Result)
                        {
                            decimal oddsModify = Convert.ToDecimal(row.OddsModify);

                            //取對應物件
                            if (Config.gIdDic.TryGetValue(row.GId, out string key)
                                && Config.gameDic.TryGetValue(key, out GameInfo gameInfo)
                                && gameInfo.ObjDic.TryGetValue(row.Gpdm, out GpdmInfo gpdmInfo)
                                && gpdmInfo.TryGetValue(row.KType, out KInfo kInfo)
                                && kInfo.BInfoDic.TryGetValue(row.BType, out BInfo bInfo))
                            {
                                if (kInfo.LineModify != row.LineModify || bInfo.OddsModify != oddsModify)
                                {
                                    kInfo.LineModify = row.LineModify;
                                    bInfo.OddsModify = oddsModify;
                                    kInfo.IsUpdate = true;
                                }
                            }
                        }

                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                Display.SetDictInfo(Display.Type.Error, YwCore.Properties.Resources.MainDB + "odds : " + ex.Message);
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                System.Timers.Timer timer = (System.Timers.Timer)sender;
                timer.Start();
            }
        }

        //XB 左側選單請求
        private void ReqLeagueTick(object sender, ElapsedEventArgs e, GpType gpType)
        {
            System.Timers.Timer timer = (System.Timers.Timer)sender;
            //取得代理資訊
            string proxy = Config.Proxy.GetProxy()?.Address.Authority;
            bool isOpen = false;
            int sec = 100;

            try
            {
                switch (Config.entrance)
                {
                    case Entrance.XB:
                        isOpen = Config.isOpenDs || Config.isOpenZp;
                        sec = 30000;

                        if (isOpen)
                        {
                            //請求來源球類表單抓取聯盟id
                            ReqXB.RequestLeague(proxy);
                        }

                        break;

                    case Entrance.XB_New:
                        switch (gpType)
                        {
                            case GpType.Zd:
                                isOpen = Config.isOpenZd;
                                sec = Config.zdSec;
                                break;
                            case GpType.Ds:
                                isOpen = Config.isOpenDs;
                                sec = Config.dsSec;
                                break;
                            case GpType.Zp:
                                isOpen = Config.isOpenZp;
                                sec = Config.zpSec;
                                break;
                        }

                        if (isOpen)
                        {
                            //請求來源球類表單抓取聯盟id
                            ReqXB_New.RequestLeague(proxy, gpType);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                double intreval = isOpen ? sec : 100;

                if (((System.Timers.Timer)sender).Interval != intreval)
                {
                    ((System.Timers.Timer)sender).Interval = intreval;
                }

               ((System.Timers.Timer)sender).Start();
            }
        }

        #endregion

        #region 發送、接收、逾時

        //非同步POST請求
        private void SendPostAsyncRequest(IAsyncResult result)
        {
            ReqInfo reqInfo = null;

            try
            {
                reqInfo = (ReqInfo)result.AsyncState;

                if (!reqInfo.IsAborted)
                {
                    byte[] buffer = Encoding.ASCII.GetBytes(reqInfo.PostData);

                    using (Stream stream = reqInfo.Req.EndGetRequestStream(result))
                    {
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    IAsyncResult postResult;

                    if (Config.entrance == Entrance.HG && reqInfo.ThreadInfo.StrType.Contains("More"))
                    {
                        postResult = reqInfo.Req.BeginGetResponse(new AsyncCallback(GetMoreAsyncResponse), reqInfo);
                    }
                    else
                    {
                        postResult = reqInfo.Req.BeginGetResponse(new AsyncCallback(GetAsyncResponse), reqInfo);
                    }

                    ThreadPool.RegisterWaitForSingleObject(postResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), reqInfo, 10000, true);
                }
            }
            catch (WebException wex)
            {
                WebExceptionHandle(reqInfo, wex);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        //[常用]非同步接收
        private void GetAsyncResponse(IAsyncResult result)
        {
            ReqInfo reqInfo = null;

            try
            {
                reqInfo = (ReqInfo)result.AsyncState;

                //逾時取消則不處理
                if (reqInfo.IsAborted) { return; }
                string html = string.Empty;

                using (HttpWebResponse webResponse = (HttpWebResponse)reqInfo.Req.EndGetResponse(result))
                {
                    using (StreamReader reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                    {
                        html = reader.ReadToEnd();
                    }
                }

                //無數據則提示或切換代理
                if (string.IsNullOrEmpty(html))
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
                //正常數據處理
                else
                {
                    //清空提示訊息
                    Display.SetDictInfo(Display.Type.Warnning, "");

                    if (reqInfo.UserInfo != null) { reqInfo.UserInfo.WebExCount = 0; }

                    #region 數據寫入日誌

                    string strDateTimeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff");
                    string strType = reqInfo.ThreadInfo.StrType;
                    StringBuilder tmpSb = new StringBuilder();

                    if (Config.source == Source.SBO)
                    {
                        //消除無使用資訊
                        Match onUpdateData = Regex.Match(html, @"onUpdate\(\'od\'\,(.*?)\);");

                        if (onUpdateData.Success)
                        {
                            html = onUpdateData.Result("$1");
                        }

                        tmpSb = new StringBuilder(Environment.NewLine + strDateTimeNow + " | " + strType + " | " + "SendId:" + reqInfo.SendId + Environment.NewLine + html);
                    }
                    else
                    {
                        tmpSb = new StringBuilder(Environment.NewLine + strDateTimeNow + " | " + strType + " | " + "SendId:" + reqInfo.SendId + Environment.NewLine + html);
                    }

                    string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString("00");
                    LogFile.SetLog(LogMode.Html, tmpSb.ToString(), strType, dateNow);

                    #endregion

                    #region 來源數據第一層判斷

                    if (Config.source == Source.SBO)
                    {
                        //不解析
                        if (html.StartsWith("<!DOCTYPE html PUBLIC", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                        //添加維修判斷
                        else if (html.Contains("under maintenance"))
                        {
                            Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.SourceMaintenance);
                            ParserSBO.MoreDic.Clear();
                            Common.SendMessage.MaintenanceSend();
                            return;
                        }
                    }

                    #endregion

                    #region 放進佇列

                    DataInfo dataInfo = new DataInfo(reqInfo.ThreadInfo.StrType, html, reqInfo.SendId);
                    GpType gpType;
                    if (!Enum.TryParse(reqInfo.ThreadInfo.StrType, out gpType)) { return; }

                    switch (gpType)
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
                        case GpType.Xb:
                            Config.xbQueue.Enqueue(dataInfo);
                            break;
                        case GpType.Ac:
                            Config.acQueue.Enqueue(dataInfo);
                            break;
                    }

                    #endregion
                }
            }
            catch (WebException wex)
            {
                if (((HttpWebResponse)wex.Response)?.StatusCode.GetHashCode() == 429)
                {
                    RequestTrigger(false);

                    foreach (GameInfo game in Config.gameDic.Values)
                    {
                        game.DataId = -1;
                    }

                    _timerStartRequest.Start();
                    Display.SetDictInfo(Display.Type.Warnning, $"請求太頻繁導致來源拒絕，已暫停GP數據請求{_timerStartRequest.Interval / 1000 / 60}分鐘");
                }

                WebExceptionHandle(reqInfo, wex);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                reqInfo.Req.Abort();
                reqInfo.Req = null;
            }
        }

        //[更多]非同步接收
        private void GetMoreAsyncResponse(IAsyncResult result)
        {
            ReqInfo reqInfo = null;
            Source source = Config.source;

            try
            {
                reqInfo = (ReqInfo)result.AsyncState;
                //賽事字典
                ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;
                //GP類型
                if (!Enum.TryParse(reqInfo.ThreadInfo.StrType, out GpType gpType)) { return; }
                //判斷用類型
                GpType type = gpType & (GpType.Ds | GpType.Zd | GpType.Zp);
                //來源gId
                decimal sourceMatchId = reqInfo.SourceMatchId;
                ConcurrentDictionary<decimal, string> tmpDictMain;
                //賽事字典key
                string gameKey = "";
                //賽事字典key清單 [SBO使用]
                List<string> gameKeys = new List<string>();

                switch (source)
                {
                    case Source.XB:

                        //XB需額外查詢關聯字典, 找尋主盤gameKey
                        decimal tmpSourceMatchId = sourceMatchId;

                        if (ParserXB.dicParentId.TryGetValue(type, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDicParentId))
                        {
                            ConcurrentDictionary<decimal, HashSet<decimal>> tmpSceneList = new ConcurrentDictionary<decimal, HashSet<decimal>>(tmpDicParentId.Where(x => x.Value.Contains(sourceMatchId)));

                            //查詢主盤ID
                            if (tmpSceneList.Count > 0)
                            {
                                tmpSourceMatchId = tmpSceneList.First().Key;
                            }
                        }

                        if (!(ParserXB.dictMain.TryGetValue(type, out tmpDictMain) && tmpDictMain.TryGetValue(tmpSourceMatchId, out gameKey)))
                        {
                            return;
                        }

                        break;
                    case Source.SBO:

                        if (ParserSBO.DictMain.TryGetValue(type, out tmpDictMain))
                        {
                            HashSet<decimal> tmpHashSet = new HashSet<decimal>();

                            if (ParserSBO.DictPlay.TryGetValue(type, out ConcurrentDictionary<decimal, HashSet<decimal>> tmpDictPlay)
                                && !tmpDictPlay.TryGetValue(sourceMatchId, out tmpHashSet))
                            {
                                tmpHashSet = new HashSet<decimal>();
                                tmpHashSet.Add(sourceMatchId);
                            }

                            foreach (decimal sourceId in tmpHashSet)
                            {
                                if (tmpDictMain.TryGetValue(sourceId, out gameKey))
                                {
                                    gameKeys.Add(gameKey);
                                }
                                else
                                {
                                    gameKey = string.Empty;
                                }
                            }
                        }
                        else
                        {
                            return;
                        }

                        break;
                    case Source.MyStake:

                        if (!(ParserMyStake.dictMain.TryGetValue(type, out tmpDictMain) && tmpDictMain.TryGetValue(sourceMatchId, out gameKey)))
                        {
                            return;
                        }

                        break;
                    default:
                        return;
                }

                //取不到賽事字典的key或找不到對應賽事時則跳過
                if (!gameDic.TryGetValue(gameKey, out GameInfo gameInfo))
                {
                    if (source != Source.SBO)
                    {
                        return;
                    }
                }

                //是否錯誤數據
                bool isError = false;

                //請求逾時後中斷
                if (reqInfo.IsAborted)
                {
                    isError = true;
                }
                //正常處理流程
                else
                {
                    string data = "";
                    //獲取回傳數據
                    using (HttpWebResponse webResponse = (HttpWebResponse)reqInfo.Req.EndGetResponse(result))
                    {
                        using (StreamReader reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                        {
                            data = reader.ReadToEnd();
                        }
                    }

                    //紀錄日誌
                    StringBuilder tmpSb = null;
                    string strDateTimeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff");

                    //若回應無數據則丟棄
                    if (string.IsNullOrEmpty(data))
                    {
                        isError = true;

                        if (reqInfo.UserInfo != null)
                        {
                            tmpSb = new StringBuilder($"{Environment.NewLine}{strDateTimeNow} | {gpType} | SendId:{reqInfo.SendId}, Group:{reqInfo.UserInfo.Group}, Account:{reqInfo.UserInfo.User}, SourceId:{sourceMatchId}, Data:null{Environment.NewLine}");
                        }
                        else
                        {
                            tmpSb = new StringBuilder($"{Environment.NewLine}{strDateTimeNow} | {gpType} | SendId:{reqInfo.SendId}, SourceId:{sourceMatchId}, Data:null{Environment.NewLine}");
                        }
                    }
                    else
                    {
                        #region 寫入日誌

                        if (source == Source.SBO)
                        {
                            if (data.IndexOf(reqInfo.PostData, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                isError = true;
                            }

                            //消除無使用資訊
                            Match onUpdateData = Regex.Match(data, @"onUpdate\(\'od\'\,(.*?)\);");

                            if (onUpdateData.Success)
                            {
                                data = onUpdateData.Result("$1");
                            }
                            else
                            {
                                isError = true;
                            }

                            tmpSb = new StringBuilder($"{Environment.NewLine}{strDateTimeNow} | {gpType} | SendId:{reqInfo.SendId}, SourceId:{sourceMatchId}, {Environment.NewLine}{data}{Environment.NewLine}");
                        }
                        else
                        {
                            tmpSb = new StringBuilder($"{Environment.NewLine}{strDateTimeNow} | {gpType} | SendId:{reqInfo.SendId}, SourceId:{sourceMatchId}, {Environment.NewLine}{data}{Environment.NewLine}");
                        }

                        #endregion

                        if (Config.source == Source.XB)
                        {
                            //錯誤數據格式
                            if (data.Contains("游戏无法在体育中找到") || data.Contains("Success\":false") || data.Contains("Value\":null"))
                            {
                                isError = true;
                            }
                        }
                    }

                    #region 加入佇列

                    if (!isError)
                    {
                        //SBO解析無須GammeKey (內頁已經請求後，玩法字典對應已消失，會導致gameInfo為空)
                        DataInfo dataInfo = source == Source.SBO ?
                                            new DataInfo(reqInfo.ThreadInfo.StrType, data, reqInfo.SendId, sourceMatchId) :
                                            new DataInfo(reqInfo.ThreadInfo.StrType, data, reqInfo.SendId, sourceMatchId, gameInfo.GetGameDictKey());

                        if (!Enum.TryParse(reqInfo.ThreadInfo.StrType, out GpType tmpGpType)) { return; }

                        switch (tmpGpType)
                        {
                            case GpType.DsMore:
                                Config.dsMoreQueue.Enqueue(dataInfo);
                                break;
                            case GpType.ZdMore:
                                Config.zdMoreQueue.Enqueue(dataInfo);
                                break;
                            case GpType.ZpMore:
                                Config.zpMoreQueue.Enqueue(dataInfo);
                                break;
                        }
                    }

                    #endregion

                    //寫入日誌
                    if (tmpSb != null && tmpSb.Length > 0)
                    {
                        LogFile.SetLog(LogMode.Html, tmpSb.ToString(), gpType.ToString(), sourceMatchId.ToString());
                    }
                }

                //錯誤次數累加
                if (isError)
                {
                    if (source == Source.SBO)
                    {
                        foreach (string tmpGameKey in gameKeys)
                        {
                            if (Config.gameDic.TryGetValue(tmpGameKey, out GameInfo getGameInfo))
                            {
                                if (getGameInfo.MoreDataDic.TryGetValue(sourceMatchId, out long[] value))
                                {
                                    value[1]++;
                                }
                            }
                        }
                    }
                    else if (gameInfo.MoreDataDic.TryGetValue(sourceMatchId, out long[] value))
                    {
                        value[1]++;
                    }
                }
            }
            catch (WebException wex)
            {
                if (((HttpWebResponse)wex.Response)?.StatusCode.GetHashCode() == 429)
                {
                    RequestTrigger(false);

                    foreach (GameInfo game in Config.gameDic.Values)
                    {
                        game.DataId = -1;
                    }

                    _timerStartRequest.Start();
                    Display.SetDictInfo(Display.Type.Warnning, $"請求太頻繁導致來源拒絕，已暫停GP數據請求{_timerStartRequest.Interval / 1000 / 60}分鐘");
                }

                WebExceptionHandle(reqInfo, wex);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                reqInfo.Req.Abort();
                reqInfo.Req = null;
            }
        }

        //非同步逾時
        private void TimeoutCallback(object state, bool timeOut)
        {
            try
            {
                if (timeOut)
                {
                    ReqInfo reqInfo = (ReqInfo)state;

                    if (reqInfo.Req != null)
                    {
                        //終止請求
                        reqInfo.IsAborted = true;
                        reqInfo.Req.Abort();
                        //寫日誌
                        LogFile.SetLog(LogMode.Request, reqInfo.GetInfo());
                    }

                    //切換代理
                    if (Config.Proxy.Next())
                    {
                        Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.RequestTimeOut + Environment.NewLine + YwCore.Properties.Resources.ChangeProxy);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
        }

        #endregion

        /// <summary>
        /// web例外處理
        /// </summary>
        /// <param name="reqInfo">請求資訊</param>
        /// <param name="wex">web例外資訊</param>
        private void WebExceptionHandle(ReqInfo reqInfo, WebException wex)
        {
            //是否切換代理
            bool isSwitchProxy = false;

            if (reqInfo.UserInfo != null)
            {
                //累加錯誤次數
                reqInfo.UserInfo.WebExCount++;

                //錯誤小於五次
                if (reqInfo.UserInfo.WebExCount < 5)
                {
                    isSwitchProxy = true;
                }
                else
                {
                    //服務氣連續錯誤，帳號重新登入
                    reqInfo.UserInfo.ClearStatus();

                    if (reqInfo.UserInfo != null)
                    {
                        Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.Account + ":" + reqInfo.UserInfo.User + " " + YwCore.Properties.Resources.LoginAgain);
                    }
                }
            }
            else
            {
                isSwitchProxy = true;
            }

            //切換代理
            if (isSwitchProxy && Config.Proxy.Next())
            {
                Display.SetDictInfo(Display.Type.Warnning, YwCore.Properties.Resources.ProtocolError + Environment.NewLine + YwCore.Properties.Resources.ChangeProxy);
            }

            if (wex != null && wex.Status != WebExceptionStatus.RequestCanceled)
            {
                //寫日誌
            }
        }

        /// <summary>
        /// 關閉賽事或關閉玩法判斷
        /// </summary>
        private void Close()
        {
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;
            //[新比賽庫]來源開關異動紀錄暫存表
            SourceOpenRecordRequest dtSourceOpenRecord = new SourceOpenRecordRequest();
            //來源開關異動暫存字典
            ConcurrentDictionary<string, SourceOpenRecordDto> sourceDic = new ConcurrentDictionary<string, SourceOpenRecordDto>();
            //[新比賽庫]暫存表列表(分批更新)
            List<List<BkOddsCacheUpdateDto>> dtOddsList = new List<List<BkOddsCacheUpdateDto>>();
            //[新比賽庫]更新玩法暫存表
            List<BkOddsCacheUpdateDto> dtOdds = new List<BkOddsCacheUpdateDto>();
            //[主庫]暫存表列表(分批更新)
            Dictionary<GpType, List<List<BkOddsUpdateDto>>> mainOddsDic = new Dictionary<GpType, List<List<BkOddsUpdateDto>>>() { { GpType.Zd, new List<List<BkOddsUpdateDto>>() }, { GpType.Ds, new List<List<BkOddsUpdateDto>>() } };
            //[主庫]更新ZD玩法暫存表
            List<BkOddsUpdateDto> dtMainOddsZd = new List<BkOddsUpdateDto>();
            //[主庫]更新DS玩法暫存表
            List<BkOddsUpdateDto> dtMainOddsDs = new List<BkOddsUpdateDto>();
            //需移除的賽事key列表
            List<string> removeGameList = new List<string>();
            //需移除的玩法key字典
            ConcurrentDictionary<string, ConcurrentDictionary<decimal, List<int>>> removeOddsDic = new ConcurrentDictionary<string, ConcurrentDictionary<decimal, List<int>>>();

            foreach (KeyValuePair<string, GameInfo> gamePair in gameDic)
            {
                GameInfo game = gamePair.Value;
                List<BkOddsUpdateDto> dtMainOdds = game.GpType == GpType.Zd ? dtMainOddsZd : dtMainOddsDs;
                //賽事字典key
                string gameKey = gamePair.Key;
                //是否賽事移除
                bool isGameRemove = false;
                //是否PL清零 (只判斷ZD)
                bool isClearZero = game.GpType == GpType.Zd && Config.isOddsClearZero;
                //清除Game數據庫ID
                Action ClearId = null;

                #region 賽事檢查

                //對應執行緒
                ThreadInfo threadInfo = threadInfos[game.GpType];

                //賽事數據索引小於執行緒數據索引
                if (game.DataId < threadInfo.DataId)
                {
                    isGameRemove = true;
                    //添加至移除列表
                    removeGameList.Add(gameKey);
                }
                //不GP賽事
                else if (game.IsFilter() && game.Id > 0)
                {
                    isGameRemove = true;
                    ClearId = () =>
                    {
                        //移除gid字典對應項目
                        if (Config.gIdDic.TryGetValue(game.GId, out string value) && value == gameKey)
                        {
                            Config.gIdDic.TryRemove(game.GId, out string tmp);
                        }

                        game.Id = 0;
                        game.GId = 0;
                    };
                }

                #endregion

                #region 玩法檢查            

                foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in game.ObjDic)
                {
                    //不需移除緩存內的Kinfo
                    List<KInfo> filterKinfo = new List<KInfo>();

                    GpdmInfo tmpDic = gpdmPair.Value;
                    //群組中是否有其中一個玩法刪除
                    bool isGroupRemove = false;
                    bool isFliter = Config.BlockList.BlockGpdm(game, tmpDic);

                    //判斷同一群組
                    foreach (KInfo kInfo in tmpDic.Values)
                    {
                        //GP類型
                        GpType gpType = GpType.None;
                        if (!Enum.TryParse(kInfo.StrType, out gpType)) { return; }
                        //對應執行緒
                        ThreadInfo info = threadInfos[gpType];
                        //是否移除
                        bool isRemove = false;

                        //賽事刪除
                        if (isGameRemove)
                        {
                            isGroupRemove = true;

                            if (ClearId != null)
                            {
                                filterKinfo.Add(kInfo);
                            }
                        }
                        else if ((gpType & GpType.More) == GpType.More)
                        {
                            if (game.MoreDataDic.TryGetValue(kInfo.MatchId, out long[] value))
                            {
                                //玩法數據索引小於賽事更多數據索引 或 錯誤次數超過5
                                //XB賽事頁面不勾則關閉玩法
                                if (kInfo.DataId < value[0] ||
                                    value[1] > 5 ||
                                   (game.IsFilter() && Config.source == Source.XB))
                                {
                                    isRemove = true;
                                }
                            }
                            else
                            {
                                isRemove = true;
                            }
                        }
                        //玩法數據索引小於執行緒數據索引且不為手動設置
                        else if (kInfo.DataId < info.DataId && !kInfo.IsManual)
                        {
                            isRemove = true;
                        }

                        //玩法消失設為關閉
                        if (isRemove)
                        {
                            isGroupRemove = true;

                            kInfo.IsUpdate = true;
                            kInfo.SetIsOpen(false);
                        }
                        else
                        {
                            if (isFliter)
                            {
                                filterKinfo.AddRange(tmpDic.Values);
                                isGroupRemove = true;
                            }
                        }
                    }

                    //群組中有玩法刪除進行判斷
                    if (isGroupRemove)
                    {
                        //判斷玩法是否全關或只剩手動設置玩法
                        List<KInfo> tmpList = (from kInfo in tmpDic.Values
                                               where kInfo.IsOpen && !kInfo.IsManual
                                               select kInfo).ToList();
                        //判斷是否有未切玩法
                        bool isSettlementRecord = false;

                        //皆為關閉狀態或賽事刪除或設定玩法篩選，直接清除
                        if (tmpList.Count == 0 || isGameRemove || filterKinfo.Count > 0)
                        {
                            if (Config.isFollow)
                            {
                                #region 來源消失判斷是否要寫提醒

                                //提前生成玩法不須判斷
                                if (!tmpDic.IsGenerate)
                                {
                                    //是否來源消失紀錄
                                    bool isRecord = false;
                                    //挑選寫入過開放提示
                                    List<KInfo> kInfoList = (from kInfo in tmpDic.Values
                                                             where kInfo.IsSourceOpenRecord
                                                             select kInfo).ToList();

                                    foreach (KInfo kInfo in kInfoList)
                                    {
                                        //挑選被移到待結算的玩法
                                        List<BInfo> bInfoList = (from bInfo in kInfo.BInfoDic.Values
                                                                 where bInfo.Status == 255
                                                                 select bInfo).ToList();
                                        if (bInfoList.Count > 0)
                                        {
                                            isRecord = true;
                                            break;
                                        }
                                    }

                                    //來源消失寫入紀錄
                                    if (isRecord)
                                    {
                                        //取kType
                                        KInfo kInfo = tmpDic.Values.First();

                                        if (kInfo.KTypeGroup > 0)
                                        {
                                            SourceOpenRecordDto dto = new SourceOpenRecordDto()
                                            {
                                                GId = game.GId,
                                                GType = game.GType,
                                                KTypeGroup = kInfo.KTypeGroup,
                                                SourceId = 0,
                                                IsOpen = 0
                                            };

                                            sourceDic.TryAdd(dto.ToString(), dto);
                                        }
                                    }
                                }

                                #endregion

                                #region 關盤監控寫入

                                //是否移到主庫, 是否GP賽事
                                if (game.GpType == GpType.Zd && tmpDic.IsMain && !game.IsFilter())
                                {
                                    //取出RF、OU KType(關盤監控只判斷這2個)
                                    List<int> kTypeList = tmpDic.Where(a => a.Value.PlayTyp == 1 || a.Value.PlayTyp == 2).Select(a => a.Key).ToList();

                                    //加入關閉監控字典
                                    ZdCloseNotify closeNotify = new ZdCloseNotify(game.GId, game.GType, tmpDic.Key, kTypeList, game.GameDate);
                                    Config.CloseNotify.AddDic(closeNotify);
                                }

                                #endregion
                            }

                            foreach (KInfo kInfo in tmpDic.Values)
                            {
                                //啟動中才處理
                                if (Config.isFollow)
                                {
                                    foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                                    {
                                        //有數據庫Id才加入暫存表
                                        if (bInfo.Id > 0)
                                        {
                                            switch (bInfo.Status)
                                            {
                                                case 0://[新比賽庫]
                                                    dtOdds.Add(SetField.SetUpdateOddsRowNew(game.GId, gpdmPair.Key, kInfo, bInfo, isClearZero, true));
                                                    break;
                                                case 1://[主庫]
                                                    //防護KMode資訊尚未查回導致只跟PK功能失效
                                                    if (kInfo.KModeId > 0)
                                                    {
                                                        //DS賽事 JZ關大格->NB隱藏，不脫鉤
                                                        //ZD賽事 JZ關大格->NB不脫鉤
                                                        bool isDisplay = Config.source == Source.JZ && game.GpType != GpType.Zd ? false : true;
                                                        bool isFollow = Config.source == Source.JZ ? true : false;
                                                        dtMainOdds.Add(SetField.SetUpdateMainOddsRow(game, gpdmPair.Key, kInfo, bInfo, isDisplay, isFollow, isClearZero, true));
                                                    }

                                                    isSettlementRecord = true;
                                                    break;
                                            }

                                            ClearId += () =>
                                            {
                                                bInfo.Id = 0;
                                            };
                                        }
                                    }
                                }

                                #region 添加移除玩法字典

                                if (filterKinfo.IndexOf(kInfo) < 0)
                                {
                                    //gameKey不存在則添加
                                    removeOddsDic.TryAdd(gameKey, new ConcurrentDictionary<decimal, List<int>>());

                                    //gpdm分組不存在則添加
                                    removeOddsDic[gameKey].TryAdd(gpdmPair.Key, new List<int>());

                                    //kType不存在則添加
                                    if (!removeOddsDic[gameKey][gpdmPair.Key].Contains(kInfo.KType))
                                    {
                                        removeOddsDic[gameKey][gpdmPair.Key].Add(kInfo.KType);
                                    }
                                }

                                #endregion
                            }

                            //未切玩法加入佇列、不是提前生成玩法
                            if (isSettlementRecord && !tmpDic.IsGenerate)
                            {
                                KInfo kInfo = tmpDic.Values.First();
                                int id = kInfo.BInfoDic.Values.Select(b => b.Id).First();

                                if (game.GId > 0 && kInfo.KTypeGroup > 0)
                                {
                                    SettlementRecordModel tmpModel = new SettlementRecordModel(game.GId, game.GType, kInfo.KTypeGroup, id);
                                    SettlementRecordHandle.AddQueue(tmpModel);
                                }
                            }
                        }
                    }
                }

                #endregion

                #region 防止數量過多(分批更新)

                //[主庫][玩法]超過筆數則先添加至佇列
                if (dtMainOdds.Count > Common.eachBatchLimit)
                {
                    if (game.GpType == GpType.Zd)
                    {
                        mainOddsDic[GpType.Zd].Add(dtMainOddsZd);
                        dtMainOddsZd = new List<BkOddsUpdateDto>();
                    }
                    else
                    {
                        mainOddsDic[GpType.Ds].Add(dtMainOddsDs);
                        dtMainOddsDs = new List<BkOddsUpdateDto>();
                    }
                }

                //[新比賽庫][玩法]超過筆數換下一群組(分批更新)
                if (dtOdds.Count > Common.eachBatchLimit)
                {
                    dtOddsList.Add(dtOdds);
                    dtOdds = new List<BkOddsCacheUpdateDto>();
                }

                #endregion

                //清除game數據庫ID
                ClearId?.Invoke();
            }

            //[主庫][玩法]將最後一個dataTable加入佇列
            if (dtMainOddsZd.Count > 0)
            {
                mainOddsDic[GpType.Zd].Add(dtMainOddsZd);
            }

            //[主庫][玩法]將最後一個dataTable加入佇列
            if (dtMainOddsDs.Count > 0)
            {
                mainOddsDic[GpType.Ds].Add(dtMainOddsDs);
            }

            //[新比賽庫][玩法]將最後一個列表加入至群組
            if (dtOdds.Count > 0)
            {
                dtOddsList.Add(dtOdds);
            }

            //加入暫存表
            foreach (var obj in sourceDic.Values)
            {
                dtSourceOpenRecord.Data.Add(obj);
            }

            //[新比賽庫]來源開關異動紀錄
            if (dtSourceOpenRecord.Data.Count > 0)
            {
                var response = Common.GameCacheServiceClient.SourceOpenRecord(dtSourceOpenRecord);

                if (!string.IsNullOrEmpty(response.ErrorMsg))
                {
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }
            }

            //工作集合，同時處理主庫、新比賽庫的工作
            List<Task> taskList = new List<Task>();

            foreach (var list in mainOddsDic)
            {
                if (list.Value.Count > 0)
                {
                    //[主庫][玩法]分批更新     
                    Task mainOddsTask = Task.Run(() =>
                    {
                        foreach (var dtMain in list.Value)
                        {
                            var request = new BkOddsUpdateRequest() { IsRemove = true, IsUpdateDisplay = list.Key == GpType.Zd ? false : true };
                            request.Data.AddRange(dtMain);
                            //設定發送時間
                            Stopwatch stopwatch = Stopwatch.StartNew();
                            //入庫執行
                            var response = Config.OddsServiceClient.BatchUpdate(request);
                            stopwatch.Stop();

                            if (!string.IsNullOrEmpty(response.ErrorMsg))
                            {
                                LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                                continue;
                            }

                            //加入日誌佇列
                            LogProcess.AddQueue(new UpdateMainOddsLog(Config.source, "", UpdateMainOddsLog.LogType.Close, request.Data, response.Result, stopwatch.Elapsed, Config.entrance));
                        }
                    });

                    taskList.Add(mainOddsTask);
                }
            }

            if (dtOddsList.Count > 0)
            {
                //[新比賽庫][玩法]分批更新      
                Task newOddsTask = Task.Run(() =>
                {
                    foreach (var dt in dtOddsList)
                    {
                        var request = new BkOddsCacheUpdateRequest();
                        request.Data.AddRange(dt);
                        //設定發送時間
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        //入庫執行
                        var response = Config.OddsCacheServiceClient.BatchUpdate(request);
                        stopwatch.Stop();

                        if (!string.IsNullOrEmpty(response.ErrorMsg))
                        {
                            LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                            continue;
                        }

                        //加入日誌佇列
                        LogProcess.AddQueue(new UpdateOddsCacheLog(Config.source, "", UpdateOddsCacheLog.LogType.Close, request.Data, response.UpdateResult, stopwatch.Elapsed, Config.entrance));
                    }
                });

                taskList.Add(newOddsTask);
            }

            //等待工作都處理完
            Task.WaitAll(taskList.ToArray());

            #region 緩存移除

            //移除賽事
            foreach (string gameKey in removeGameList)
            {
                if (gameDic.TryRemove(gameKey, out GameInfo removeGame))
                {
                    if (removeGame.League.SourceName != removeGame.League.Name)
                    {
                        if (Config.GetReplaceLeagueService.TryGetValue(new ReplaceLeagueKey(ReplaceType.SourceId, removeGame.SourceId), out var model) && removeGame.League.Name == model.ReplaceName && !model.IsKeep)
                        {
                            Config.GetReplaceLeagueService.TryRemove(model.GetKey());
                        }

                        if (!gameDic.Values.Any(a => a.League.Name == removeGame.League.Name))
                        {
                            if (Config.GetReplaceLeagueService.TryGetValue(new ReplaceLeagueKey(ReplaceType.Name, removeGame.League.SourceName), out model) && removeGame.League.Name == model.ReplaceName && !model.IsKeep)
                            {
                                Config.GetReplaceLeagueService.TryRemove(model.GetKey());
                            }
                        }
                    }

                    if (gameDic.Values.All(a => a.SourceId != removeGame.SourceId))
                    {
                        if (removeGame.TeamA.Name != removeGame.TeamA.SourceName)
                        {
                            ReplaceTeamKey key = new ReplaceTeamKey(ReplaceType.SourceId, removeGame.League.Name, removeGame.TeamA.SourceName, removeGame.SourceId);

                            if (Config.GetReplaceTeamService.TryGetValue(key, out var model) && !model.IsKeep)
                            {
                                Config.GetReplaceTeamService.TryRemove(key);
                            }

                            key = new ReplaceTeamKey(ReplaceType.Name, removeGame.League.Name, removeGame.TeamA.SourceName, $"{removeGame.League.Name}_{removeGame.TeamA.SourceName}");

                            if (Config.GetReplaceTeamService.TryGetValue(key, out model) && !model.IsKeep)
                            {
                                Config.GetReplaceTeamService.TryRemove(key);
                            }
                        }

                        if (removeGame.TeamB.Name != removeGame.TeamB.SourceName)
                        {
                            ReplaceTeamKey key = new ReplaceTeamKey(ReplaceType.SourceId, removeGame.League.Name, removeGame.TeamB.SourceName, removeGame.SourceId);

                            if (Config.GetReplaceTeamService.TryGetValue(key, out var model) && !model.IsKeep)
                            {
                                Config.GetReplaceTeamService.TryRemove(key);
                            }

                            key = new ReplaceTeamKey(ReplaceType.Name, removeGame.League.Name, removeGame.TeamB.SourceName, $"{removeGame.League.Name}_{removeGame.TeamB.SourceName}");

                            if (Config.GetReplaceTeamService.TryGetValue(key, out model) && !model.IsKeep)
                            {
                                Config.GetReplaceTeamService.TryRemove(key);
                            }
                        }
                    }

                    //移除gid字典對應項目
                    if (Config.gIdDic.TryGetValue(removeGame.GId, out string value) && value == gameKey)
                    {
                        Config.gIdDic.TryRemove(removeGame.GId, out string tmp);
                    }

                    //啟動中才處理
                    if (Config.isFollow && !(gameKey.EndsWith("_d") && removeGame.DataId == -1))
                    {
                        foreach (var gpdm in removeGame.ObjDic.Values)
                        {
                            KInfo tmpInfo = gpdm.FirstOrDefault().Value;

                            //有移到操盤頁才需打API, 開關有開且無玩法, 加入佇列
                            if (removeGame.GId > 0 && tmpInfo.KTypeGroup > 0 && (Common.apiSwitch & 4) == 4)
                            {
                                GameStatusSetApiModel tmpModel = new GameStatusSetApiModel(GameStatusSetApiModel.ActionMode.Undone, removeGame.GId, tmpInfo.KTypeGroup)
                                {
                                    AllyName = removeGame.League.FullName?.ElementAtOrDefault(0),
                                    TmaName = removeGame.TeamA.FullName?.ElementAtOrDefault(0),
                                    TmbName = removeGame.TeamB.FullName?.ElementAtOrDefault(0),
                                    SuffixName = removeGame.Suffix.FullName?.ElementAtOrDefault(0)
                                };

                                //蒐集玩法與gId
                                string dicKey = $"{removeGame.GId},{gpdm.Key}";
                                GameStatusSetApi.tmpDicGameStatusSetApiModel.TryAdd(dicKey, tmpModel);
                            }
                        }
                    }
                }

                Config.BlockList.RemoveGpdmList(gameKey);
            }

            //移除玩法
            foreach (KeyValuePair<string, ConcurrentDictionary<decimal, List<int>>> gamePair in removeOddsDic)
            {
                //賽事存在
                if (gameDic.TryGetValue(gamePair.Key, out GameInfo gameInfo))
                {
                    foreach (KeyValuePair<decimal, List<int>> gpdmPair in gamePair.Value)
                    {
                        //gpdm群組存在
                        if (gameInfo.ObjDic.TryGetValue(gpdmPair.Key, out GpdmInfo gpdmInfo))
                        {
                            foreach (int kType in gpdmPair.Value)
                            {
                                //從緩存中移除
                                gpdmInfo.TryRemove(kType, out KInfo remove);

                                if (!(gamePair.Key.EndsWith("_d") && gameInfo.DataId == -1))
                                {
                                    //啟動中才處理
                                    //有移到操盤頁才需打API, 開關有開且無玩法, 加入佇列
                                    if (Config.isFollow && gameInfo.GId > 0 && remove.KTypeGroup > 0 && (Common.apiSwitch & 4) == 4 && gpdmInfo.Count == 0)
                                    {
                                        GameStatusSetApiModel tmpModel = new GameStatusSetApiModel(GameStatusSetApiModel.ActionMode.Undone, gameInfo.GId, remove.KTypeGroup)
                                        {
                                            AllyName = gameInfo.League.FullName?.ElementAtOrDefault(0),
                                            TmaName = gameInfo.TeamA.FullName?.ElementAtOrDefault(0),
                                            TmbName = gameInfo.TeamB.FullName?.ElementAtOrDefault(0),
                                            SuffixName = gameInfo.Suffix.FullName?.ElementAtOrDefault(0)
                                        };

                                        //蒐集玩法與gId
                                        string dicKey = $"{gameInfo.GId},{gpdmPair.Key}";
                                        GameStatusSetApi.tmpDicGameStatusSetApiModel.TryAdd(dicKey, tmpModel);
                                    }
                                }
                            }

                            //字典內無玩法則清除
                            if (gpdmInfo.Count == 0)
                            {
                                gameInfo.ObjDic.TryRemove(gpdmPair.Key, out GpdmInfo removeGpdmInfo);
                            }
                        }
                    }
                }
            }

            #endregion
        }

        /// <summary>
        /// 處理數據入庫
        /// </summary>
        /// <param name="gpType">GP類型</param>
        private void HandleData(GpType gpType)
        {
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;
            //挑出對應的賽事數據
            List<GameInfo> gameInfoList = (from game in gameDic.Values
                                           where game.GpType == gpType
                                           select game).ToList();

            #region 挑選符合條件的賽事

            //聯盟、後綴、隊伍、帳務日 皆需有值
            //ZD帳務日期不能小於今日日期、DS開賽時間需大於現在時間
            //排除篩選掉的聯盟、賽事
            //groupby賽事匹配唯一值(聯盟、隊伍、帳務)
            //隊伍有開放才處理
            var group = gameInfoList.Where(game =>
                    game.League.Id > 0 && game.TeamA.Id > 0 && game.TeamB.Id > 0
                    && game.TeamA.Id != game.TeamB.Id
                    && !game.TeamA.IsNotOpen && !game.TeamB.IsNotOpen
                    && (!string.IsNullOrEmpty(game.Suffix.Name) ? game.Suffix.Id > 0 : true)
                    && !(game.GpType == GpType.Zd ? game.PaymentDate.Date < DateTime.Now.Date : game.GameDate < DateTime.Now)
                    && (game.IsDateFromDb ? game.PaymentDate != DateTime.MinValue : true)
                    && !game.IsFilter())
                    .GroupBy(a => new
                    {
                        allyId = a.League.Id,
                        tmaId = a.TeamA.Id,
                        tmbId = a.TeamB.Id,
                        suffixId = a.Suffix.Id,
                        paymentDate = a.PaymentDate
                    }).ToList();

            //最終符合條件可入庫的賽事清單
            List<GameInfo> gameList = new List<GameInfo>();

            foreach (var games in group)
            {
                //數量不為1則屬於例外
                bool isExecption = games.Count() != 1;

                foreach (GameInfo game in games)
                {
                    if (isExecption)//達同賽事唯一值條件，則設定不GP(賽事脫勾)
                    {
                        Config.BlockList.AddGame(game.GetGameDictKey());
                    }
                    else
                    {
                        gameList.Add(game);
                    }
                }
            }

            #endregion

            //更新筆數 陣列索引0:game  1:odds
            int[] updateCount = new int[2];

            if (Config.isFollow && gameList.Count > 0)
            {
                //查詢相關處理
                SelectHandler(gpType);

                //建立賽事
                SetField.CreateGame(gameList, gpType);

                //建立玩法
                SetField.CreateOdds(gameList, gpType);

                //更新賽事
                SetField.UpdateGame(gameList, gpType);

                //更新玩法
                SetField.UpdateOdds(gameList, gpType, out updateCount[1]);
            }

            if (gpType == GpType.Zd)
            {
                //註記提醒任務最後更新時間戳
                if (Config.remindInfos.TryGetValue("ZdUpdate", out IRemind remind) && updateCount[1] > 0)
                {
                    remind.SetLastTime(DateTime.Now);
                }
            }
            else
            {
                //註記提醒任務最後更新時間戳
                if (Config.remindInfos.TryGetValue("DsUpdate", out IRemind remind) && updateCount[1] > 0)
                {
                    remind.SetLastTime(DateTime.Now);
                }
            }

            string msg = gpType.ToString().ToUpper() + ":" + string.Format("{0,4} ", gameInfoList.Count) + YwCore.Properties.Resources.Events + " " + (updateCount[1] > 0 ? YwCore.Properties.Resources.Update + " " + updateCount[1] + " " + YwCore.Properties.Resources.Pieces : "");
            Display.Type typ = gpType == GpType.Ds ? Display.Type.Ds : gpType == GpType.Zd ? Display.Type.Zd : Display.Type.Zp;
            Display.SetDictInfo(typ, msg, updateCount[1] > 0 ? true : false);
        }

        /// <summary>
        /// 查詢處理(game、odds) 需達間隔才進行處理，ZD:2s、其餘3s
        /// </summary>
        /// <param name="gpType"></param>
        /// <returns></returns>
        private void SelectHandler(GpType gpType)
        {
            DateTime now = DateTime.Now;

            if (recordTimeDic.TryGetValue(gpType, out DateTime datetime))
            {
                int index = 0;
                int seconds = 1;

                switch (gpType)
                {
                    case GpType.Ds:
                        seconds = 3;
                        index = 1;
                        break;
                    case GpType.Zd:
                        seconds = 2;
                        index = 0;
                        break;
                    default:
                        seconds = 3;
                        index = 2;
                        break;
                }

                //達指定秒數，且先前已執行完畢
                if (now.Subtract(datetime).TotalSeconds >= seconds && Interlocked.Exchange(ref lockArray[index], 1) == 0)
                {
                    recordTimeDic.TryUpdate(gpType, now, datetime);

                    Task taskSelect = Task.Run(() =>
                    {
                        try
                        {
                            //查詢賽事是否移到主庫
                            SetField.SelectGame(gpType);
                            //查詢玩法是否移到主庫
                            SetField.SelectOdds(gpType);
                            //檢查是否來源開放紀錄(玩法被切待結算)
                            SetField.CheckSourceOpenRecord(gpType);
                        }
                        catch (Exception ex)
                        {
                            Exception baseException = ex.GetBaseException();
                            Display.SetDictInfo(Display.Type.Error, baseException.Message);
                            LogFile.SetLog(LogMode.Error, baseException.ToString());
                        }
                        finally
                        {
                            Interlocked.Exchange(ref lockArray[index], 0);
                        }
                    });


                    if (gpType != GpType.Zd)
                    {
                        taskSelect.Wait();
                    }
                }
            }
            else
            {
                recordTimeDic.TryAdd(gpType, now);
            }
        }

        /// <summary>
        /// 設定是否進入更新流程
        /// </summary>
        /// <param name="gpType">GP類型</param>
        /// <returns></returns>
        private bool SetHandleReady(GpType gpType)
        {
            bool ready = false;

            switch (Config.source)
            {
                case Source.BET365:
                    ready = _connect365?.ConnectCount >= 2;
                    break;
                case Source.JZ:
                    ready = (_connectJZ?.userList.Any(x => x.Status == 1) == true || _connectJZ_Ayh?.userList.Any(x => x.Status == 1) == true);
                    break;
                case Source.SB:
                    ready = FrmUser.users.Values.Any(v => v.Status == 1);
                    break;
                case Source.Radar:
                    ready = _connectRadar?.IsOpen == true &&
                        ((gpType == GpType.Zd && !dispatcher.CheckProducerIsDown(1)) || (gpType != GpType.Zd && !dispatcher.CheckProducerIsDown(3)));
                    break;
            }

            if (ready)
            {
                switch (gpType)
                {
                    case GpType.Ds:
                        return Config.isOpenDs && ((DateTime.Now.Second * 1000) % Config.dsSec == 0);
                    case GpType.Zd:
                        return Config.isOpenZd && ((DateTime.Now.Second * 1000) % Config.zdSec == 0);
                    case GpType.Zp:
                        return Config.isOpenZp && ((DateTime.Now.Second * 1000) % Config.zpSec == 0);
                    default:
                        return false;
                }
            }

            return false;
        }

        //訊息提醒檢查任務計時器
        private void CheckRemindTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                //是否將介面變色提示
                bool tmpChangeColor = false;

                //GP啟動才需檢查
                if (Config.isFollow)
                {
                    foreach (IRemind remind in Config.remindInfos.Values)
                    {
                        //超時檢查
                        if (remind.IsTimeout(e.SignalTime, out int state))
                        {
                            //超時提醒, ZD脫勾關小格需額外判斷開關
                            if (remind.RemindType == "ZdClose" && !remind.IsZdClosed)
                            {
                                remind.IsZdClosed = true;

                                if (!Config.CloseGame(GpType.Zd, 2))
                                {
                                    continue;
                                }
                            }

                            remind.SetSendMessage(RemindMode.Alert);
                            Display.SetDictInfo(Display.Type.Remind, remind.GetMessage(RemindMode.Alert));
                            tmpChangeColor = true;
                        }
                        //超時恢復
                        else
                        {
                            //ZD脫勾關小格需額外判斷開關
                            if (remind.RemindType == "ZdClose" && remind.IsZdClosed)
                            {
                                remind.IsZdClosed = false;
                            }

                            if (remind.GetSendFlag() > 0)
                            {
                                remind.SetSendMessage(RemindMode.Recover);
                            }
                        }
                    }
                }

                if (tmpChangeColor)
                {
                    Config.ChangeFormDesign.ChangeFormColor(System.Drawing.Color.Yellow);
                }
                else
                {
                    Config.ChangeFormDesign.ChangeFormColor(System.Drawing.Color.Pink);
                    //Yellow(異常) => Pink(正常) 當下進行清除提醒
                    if (changeColor != tmpChangeColor) { Display.SetDictInfo(Display.Type.Remind, ""); }
                }

                changeColor = tmpChangeColor;
            }
            finally
            {
                ((System.Timers.Timer)sender).Start();
            }
        }

        /// <summary>
        /// 強制更新
        /// </summary>
        public void UpdateAllData()
        {
            foreach (GameInfo gameInfo in Config.gameDic.Values)
            {
                if (gameInfo.IsUpdate == false)
                {
                    gameInfo.IsUpdate = true;
                    gameInfo.IsUpdateSwitch |= 3;
                }

                foreach (GpdmInfo tmpDic in gameInfo.ObjDic.Values.Where(a => !a.IsGenerate))
                {
                    foreach (KInfo kInfo in tmpDic.Values.Where(a => a.IsUpdate == false))
                    {
                        kInfo.IsUpdate = true;
                    }
                }
            }
        }

        //請求計時器觸發
        private void RequestTrigger(bool start)
        {
            if (start)
            {
                timerList.ForEach(a => a.Start());
            }
            else
            {
                timerList.ForEach(a => a.Stop());
            }
        }

        public void Dispose()
        {

        }
    }
}
