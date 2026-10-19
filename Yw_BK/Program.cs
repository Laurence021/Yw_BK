#region 功能修改描述與時間

/*
    描述：選擇來源頁面
    日期：2019-01-07

    描述：移除MAR來源
    日期：2022-07-06
*/

#endregion

using Gp.MonitorService;
using System.Net;
using Yw_BK.model;
using YwCore;
using YwCore.main;
using YwCore.model.interfaces;

namespace Yw_BK
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                bool isRestart = args.Length > 0;

                // 解析傳遞過來的參數
                if (isRestart)
                {
                    Config.ChooseSet = args;
                }

                string name = "CU整合版籃球-{0}來源";
                int choose = isRestart ? int.Parse(Config.ChooseSet[0]) : FrmChoose.Show("CU整合版籃球");
                int tmp = 0;
                int mode = isRestart ? int.Parse(Config.ChooseSet[2]) : 0;
                string lang = isRestart ? Config.ChooseSet[3] : "中文";

                switch (choose)
                {
                    case 1:
                        name = string.Format(name, "PIN");
                        Config.source = Source.PIN;
                        Config.lang = Lang.Vi;

                        #region 判斷點擊的平台

                        tmp = isRestart ? int.Parse(Config.ChooseSet[1]) : FrmEntrance.Show(Config.source, name);

                        switch (tmp)
                        {
                            case 1:
                                Config.entrance = Entrance.PIN;
                                break;
                            case 2:
                                Config.entrance = Entrance.LEO;
                                mode = FrmLoginMode.Show(Entrance.LEO, name, mode);
                                break;
                            case 3:
                                Config.entrance = Entrance.Asia;
                                break;
                        }

                        #endregion
                        break;
                    case 2:
                        name = string.Format(name, "XB");
                        Config.source = Source.XB;
                        Config.lang = Lang.Cn;

                        #region 判斷點擊的平台

                        tmp = isRestart ? int.Parse(Config.ChooseSet[1]) : FrmEntrance.Show(Config.source, name);

                        switch (tmp)
                        {
                            case 1:
                                Config.entrance = Entrance.XB;
                                break;
                            case 2:
                                Config.entrance = Entrance.XB_New;
                                break;
                        }

                        #endregion
                        break;
                    case 3:
                        name = string.Format(name, "BET365ZD");
                        Config.source = Source.BET365;
                        Config.lang = Lang.En;
                        Config.gpType = GpType.Zd;            //設定apName後綴
                        Common.apNameSuffix = Config.gpType.ToString();
                        break;
                    case 4:
                        name = string.Format(name, "BET365DS");
                        Config.source = Source.BET365;
                        Config.lang = Lang.Cn;
                        Config.gpType = GpType.Ds;            //設定apName後綴
                        Common.apNameSuffix = Config.gpType.ToString();
                        break;
                    case 5:
                        name = string.Format(name, "JZ");
                        Config.source = Source.JZ;
                        Config.lang = Lang.Cn;
                        break;
                    case 6:
                        name = string.Format(name, "SB");
                        Config.source = Source.SB;
                        Config.lang = Lang.Cn;

                        #region 判斷點擊的平台

                        tmp = isRestart ? int.Parse(Config.ChooseSet[1]) : FrmEntrance.Show(Config.source, name);

                        switch (tmp)
                        {
                            case 1:
                                Config.entrance = Entrance.KU;
                                mode = FrmLoginMode.Show(Entrance.KU, name, mode);
                                break;
                            case 2: Config.entrance = Entrance.Dafa; break;
                            case 4: Config.entrance = Entrance.NoLogin; break;
                        }

                        #endregion

                        break;
                    case 7:
                        name = string.Format(name, "VBET");
                        Config.source = Source.VBET;
                        Config.lang = Lang.En;
                        break;
                    case 8:
                        name = string.Format(name, "SBO(NBA)");
                        Config.source = Source.SBO;
                        Config.lang = Lang.Cn;
                        break;
                    case 9:
                        name = string.Format(name, "MyStake");
                        Config.source = Source.MyStake;
                        Config.lang = Lang.En;
                        break;
                    case 10:
                        name = string.Format(name, "Radar");
                        Config.source = Source.Radar;
                        Config.lang = Lang.En;
                        break;
                }

                //設定共用區dll
                Common.sport = Config.sport;
                Common.source = Config.source;
                Common.entrance = Config.entrance;
                Common.lang = Config.lang;
                Common.icon = Properties.Resources.icon_1;
                Config.ChooseSet = new string[4] { choose.ToString(), tmp.ToString(), mode.ToString(), lang };

                //註冊應用程序關閉前事件 => 檢查重開放清單是否要寫入關閉
                Application.ApplicationExit += (obj, eventArgs) =>
                {
                    if (Config.isFollow)
                    {
                        Config.EventCloseClearSourceOpenStatus();
                        Common.SendMessage.Send($"{Config.gpName}籃球已被＂X＂關閉");
                    }
                };

                //基礎設定
                Common.FirstStart();
                Config.GameCacheServiceClient = new Gp.Service.BkGameCacheService.BkGameCacheServiceClient(Common.CallInvoker);
                Config.GameServiceClient = new Gp.Service.BkGameService.BkGameServiceClient(Common.CallInvoker);
                Config.OddsCacheServiceClient = new Gp.Service.BkOddsCacheService.BkOddsCacheServiceClient(Common.CallInvoker);
                Config.OddsServiceClient = new Gp.Service.BkOddsService.BkOddsServiceClient(Common.CallInvoker);
                Config.ServiceClient = new ZDBD.Service.ZdbdService.ZdbdServiceClient(Common.CallInvoker);
                RegisterMonitorInit();
                Config.GetReplaceLeagueService = new ReplaceLeagueService(CancellationToken.None);
                Config.GetReplaceTeamService = new ReplaceTeamService(CancellationToken.None);

                Common.LoadLeagueReplaceSetting(Common.GetApplicationNameForLeagueReplace(Common.source, Common.entrance), ref Common.leagueReplaceDic);
                Config.NbaLeagueExcludeSetting(Common.GetApplicationNameForLeagueReplace(Common.source, Common.entrance), ref Config.nbaleagueRecludeDic);
                //後綴副名稱
                WebName.HandleSuffixName();

                YwCore.Properties.Resources.Culture = Properties.Resources.Culture = Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(Common.langDic[lang]);

                SetProxySwitch();

                //判斷該程式是否執行
                using (Mutex mutex = new Mutex(true, name, out bool createNew))
                {
                    if (isRestart || createNew)
                    {
                        switch (choose)
                        {
                            case 1:
                                Application.Run(new FrmPIN());
                                break;
                            case 2:
                                Application.Run(new FrmXB());
                                break;
                            case 3:
                                Application.Run(new Frm365());
                                break;
                            case 4:
                                Application.Run(new Frm365());
                                break;
                            case 5:
                                Application.Run(new FrmJZ());
                                break;
                            case 6:
                                Application.Run(new FrmSB());
                                break;
                            case 7:
                                Application.Run(new FrmVBET());
                                break;
                            case 8:
                                Application.Run(new FrmSBO());
                                break;
                            case 9:
                                Application.Run(new FrmMyStake());
                                break;
                            case 10:
                                Application.Run(new FrmRadar());
                                break;
                        }
                    }
                    else
                    {
                        MessageBox.Show(name + "已經在運行中...");
                        Environment.Exit(1);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 設定Proxy功能
        /// </summary>
        private static void SetProxySwitch()
        {
            IConfiguration configuration = new Configuration();

            switch (Config.source)
            {
                case Source.BET365:
                case Source.SPB:
                    Config.Proxy = AutoChangeProxy.Build(configuration);
                    break;
                default:
                    Config.Proxy = new ProxySwitch(configuration);
                    break;
            }
        }

        /// <summary>
        /// 監控服務初始設定
        /// </summary>
        static void RegisterMonitorInit()
        {
            Config.RegisterMonitor = new YwCore.model.RegisterMonitor(Common.MonitorChannel);
            Config.RegisterMonitor.RefreshRegisterEvent += (r) =>
            {
                RegisterType registerType = RegisterType.None;

                if (Config.isFollow)
                {
                    if (Config.isOpenDs)
                    {
                        registerType |= RegisterType.Ds;
                    }

                    if (Config.isOpenZd)
                    {
                        registerType |= RegisterType.Zd;
                    }

                    if (Config.isOpenZp)
                    {
                        registerType |= RegisterType.Zp;
                    }
                }

                return new RegisterApRequest() { Gp = new RegisterGP() { GpType = registerType, HostName = Dns.GetHostName(), ProcessId = Environment.ProcessId, Sport = (int)Config.sport, SourceId = (int)Config.source } };
            };

            Config.RegisterMonitor.RecoverFailed += (obj, response) =>
            {
                Task.Run(() =>
                {
                    MessageBox.Show($"時間：{DateTime.Now.ToString("MM-dd HH:mm:ss")}{Environment.NewLine}{response.Message}", $"{Common.GetApplicationName()} 恢復失敗提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            };
            Config.ReplaceNameMonitor = new YwCore.model.ReplaceNameMonitor(Common.MonitorChannel, Config.sport, ProgramType.Gp);
            Config.AccountMonitor = new YwCore.model.AccountMonitor(Common.MonitorChannel);
            Config.AccountMonitor.RefreshRegisterEvent += (r) =>
            {
                List<RegisterAccountRequest> requests = new List<RegisterAccountRequest>();

                if (!FrmUser.users.IsEmpty && new GetAccountMonitor().IsEnabled)
                {
                    var request = new RegisterAccountRequest() { Entrance = $"{Config.entrance.GetDescriptionText()}|{Config.source.GetDescriptionText()}", HostName = Dns.GetHostName(), IgnoreCase = true, ProgramName = Common.GetApplicationName(), Used = true };
                    request.Accounts.AddRange(FrmUser.users.Values.Select(a => a.User));
                    requests.Add(request);
                }

                return requests;
            };

            Config.AccountMonitor.RecoverFailed += (obj, response) =>
            {
                Task.Run(() =>
                {
                    MessageBox.Show($"時間：{DateTime.Now.ToString("MM-dd HH:mm:ss")}{Environment.NewLine}{response.Message}", $"{Common.GetApplicationName()} 恢復失敗提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            };
        }
    }
}