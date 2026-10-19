#region 功能修改描述與時間

/*
    描述：365解析
    日期：2019-05-04

    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16
*/

#endregion

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// 365解析
    /// </summary>
    public partial class Parser365
    {
        /// <summary>
        /// 365玩法ID
        /// </summary>
        private enum PlayId
        {
            /// <summary>
            /// MG_FT
            /// </summary>
            mg_FT = 112,
            /// <summary>
            /// MG_FT
            /// </summary>
            mg_OE = 113,
            /// <summary>
            /// MG_1H
            /// </summary>
            mg_1H = 100,
            /// <summary>
            /// MG_2H
            /// </summary>
            mg_2H = 104,
            /// <summary>
            /// MG_Q1
            /// </summary>
            mg_Q1 = 102,
            /// <summary>
            /// MG_Q2
            /// </summary>
            mg_Q2 = 106,
            /// <summary>
            /// MG_Q3
            /// </summary>
            mg_Q3 = 108,
            /// <summary>
            /// MG_Q4
            /// </summary>
            mg_Q4 = 110,
            /// <summary>
            /// 全場單隊總入球
            /// </summary>
            teamTotal_FT = 528,
            /// <summary>
            /// 上半單隊總入球
            /// </summary>
            teamTotal_HT = 529,
            /// <summary>
            /// 下半單隊總入球
            /// </summary>
            teamTotal_2H = 530,
            /// <summary>
            /// Q1單隊總入球
            /// </summary>
            teamTotal_Q1 = 531,
            /// <summary>
            /// Q2單隊總入球
            /// </summary>
            teamTotal_Q2 = 532,
            /// <summary>
            /// Q3單隊總入球
            /// </summary>
            teamTotal_Q3 = 533,
            /// <summary>
            /// Q4單隊總入球
            /// </summary>
            teamTotal_Q4 = 534
        }

        //來源玩法篩選條件
        private static string[] filter = {
            PlayId.mg_FT.ToString("d"),
            PlayId.mg_OE.ToString("d"),
            PlayId.mg_1H.ToString("d"),
            PlayId.mg_2H.ToString("d"),
            PlayId.mg_Q1.ToString("d"),
            PlayId.mg_Q2.ToString("d"),
            PlayId.mg_Q3.ToString("d"),
            PlayId.mg_Q4.ToString("d"),
            PlayId.teamTotal_FT.ToString("d"),
            PlayId.teamTotal_HT.ToString("d"),
            PlayId.teamTotal_2H.ToString("d"),
            PlayId.teamTotal_Q1.ToString("d"),
            PlayId.teamTotal_Q2.ToString("d"),
            PlayId.teamTotal_Q3.ToString("d"),
            PlayId.teamTotal_Q4.ToString("d")
        };

        /// <summary>
        /// ZD內頁解析
        /// </summary>
        /// <param name="dataInfo">來源推送內頁資料</param>
        public static void MoreParser(DataInfo dataInfo, Connect365 connect)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //取得365推送資料物件
                ConcurrentDictionary<string, EvModel> evModels = connect.FormatData.DataToModel(dataInfo.Data, filter);

                for (int i = 0; i < evModels.Count; i++)
                {
                    EvModel ev = evModels.ElementAt(i).Value;
                    GameInfo gameInfo = new GameInfo(ev.It);
                    gameInfo.GType = (byte)GType.BkZd;
                    gameInfo.GpType = GpType.Zd;

                    #region 設定聯盟、隊伍、後綴

                    //聯盟
                    gameInfo.League.Name = ev.Ct;
                    //是否為3x3籃球
                    bool is3x3 = Config.Is3x3(gameInfo.League.Name);
                    bool isNBA = gameInfo.League.Name.Contains("NBA") && !gameInfo.League.Name.Contains("WNBA") && !Config.CheckNbaExcludeName(gameInfo.League.Name);
                    bool isOpenConvertML = isNBA ? Config.isNBAConvertML : Config.isOTConvertML;
                    bool isChangeTeam = false;
                    //NCAA 、WNBA 、夏季聯賽 、G聯盟 隊伍反抓
                    if (gameInfo.League.Name.Equals("WNBA") || gameInfo.League.Name.StartsWith("NCAA") || Config.CheckNbaExcludeName(gameInfo.League.Name)) { isChangeTeam = true; }
                    string teamA = ev.TeAModel.Na.Trim();
                    string teamB = ev.TeBModel.Na.Trim();
                    //交換對伍
                    ConvertTools.ExchangeValue(ref teamA, ref teamB, isChangeTeam);
                    gameInfo.TeamA.Name = teamA;
                    gameInfo.TeamB.Name = teamB;

                    //設定預設後綴
                    if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                    {
                        gameInfo.Suffix.Name = WebName.suffixDic[suffixId].En;
                        gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                        gameInfo.Suffix.Id = suffixId;
                    }

                    //無聯盟則不往下解析
                    if (string.IsNullOrEmpty(gameInfo.League.Name) || string.IsNullOrEmpty(gameInfo.TeamA.Name) || string.IsNullOrEmpty(gameInfo.TeamB.Name))
                    {
                        //取出對應gameInfo
                        GameInfo getGameInfo = Config.gameDic.Values.FirstOrDefault(a => !string.IsNullOrEmpty(a.SourceId) && a.SourceId == gameInfo.SourceId);

                        if (getGameInfo != null)
                        {
                            getGameInfo.DataId = -1;
                        }

                        continue;
                    }

                    #endregion

                    #region 設定比分、賽事狀態

                    List<ScModel> _scModels = new List<ScModel>();

                    for (int num = 0; num < ev.EsModel.ScModels.Count; num++)
                    {
                        SlModel scoreA = ev.EsModel.ScModels[num].SlAModel;
                        SlModel scoreB = ev.EsModel.ScModels[num].SlBModel;
                        ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isChangeTeam);
                        ScModel sc = new ScModel() { SlAModel = scoreA, SlBModel = scoreB };
                        _scModels.Add(sc);
                    }

                    int status = 0;
                    int inplayTimer = 0;

                    if (string.IsNullOrEmpty(ev.EsModel.It))
                    {
                        continue;
                    }

                    //正規籃球賽
                    if (ev.EsModel.ScModels.Count >= 8)
                    {
                        int pointA = ParserScore(_scModels[7].SlAModel.D1) - ParserScore(_scModels[3].SlAModel.D1); //主隊下半分數
                        int pointB = ParserScore(_scModels[7].SlBModel.D1) - ParserScore(_scModels[3].SlBModel.D1); //客隊下半分數
                        gameInfo.Score.SetScore(byte.TryParse(_scModels[7].SlAModel.D1, out byte sca) & byte.TryParse(_scModels[7].SlBModel.D1, out byte scb), sca, scb);
                        gameInfo.Score1.SetScore(byte.TryParse(_scModels[1].SlAModel.D1, out sca) & byte.TryParse(_scModels[1].SlBModel.D1, out scb), sca, scb);
                        gameInfo.Score2.SetScore(byte.TryParse(_scModels[2].SlAModel.D1, out sca) & byte.TryParse(_scModels[2].SlBModel.D1, out scb), sca, scb);
                        gameInfo.Score3.SetScore(byte.TryParse(_scModels[4].SlAModel.D1, out sca) & byte.TryParse(_scModels[4].SlBModel.D1, out scb), sca, scb);
                        gameInfo.Score4.SetScore(byte.TryParse(_scModels[5].SlAModel.D1, out sca) & byte.TryParse(_scModels[5].SlBModel.D1, out scb), sca, scb);
                        gameInfo.ScoreHalf1.SetScore(byte.TryParse(_scModels[3].SlAModel.D1, out sca) & byte.TryParse(_scModels[3].SlBModel.D1, out scb), sca, scb);
                        gameInfo.ScoreHalf2.SetScore(byte.TryParse(pointA.ToString(), out sca) & byte.TryParse(pointB.ToString(), out scb), sca, scb);
                    }
                    //NCAAB
                    else if (ev.EsModel.ScModels.Count >= 5)
                    {
                        gameInfo.Score.SetScore(byte.TryParse(_scModels[4].SlAModel.D1, out byte sca) & byte.TryParse(_scModels[4].SlBModel.D1, out byte scb), sca, scb);
                        gameInfo.ScoreHalf1.SetScore(byte.TryParse(_scModels[1].SlAModel.D1, out sca) & byte.TryParse(_scModels[1].SlBModel.D1, out scb), sca, scb);
                        gameInfo.ScoreHalf2.SetScore(byte.TryParse(_scModels[2].SlAModel.D1, out sca) & byte.TryParse(_scModels[2].SlBModel.D1, out scb), sca, scb);
                    }
                    //三對三
                    else
                    {
                        gameInfo.Score.SetScore(byte.TryParse(_scModels.Last().SlAModel.D1, out byte sca) & byte.TryParse(_scModels.Last().SlBModel.D1, out byte scb), sca, scb);
                    }

                    switch (ev.Cp)
                    {
                        case "Q1":
                            status = 1;
                            break;
                        case "Q2":
                            status = 2;
                            break;
                        case "Q3":
                            status = 3;
                            break;
                        case "Q4":
                            status = 4;
                            break;
                        case "HT":
                            status = 6;
                            break;
                        case "1st":
                            status = 11;
                            break;
                        case "2nd":
                            status = 12;
                            break;
                        default:
                            status = 5;
                            break;
                    }

                    if (ev.Cp.Contains("OT"))
                    {
                        status = 13;
                    }

                    //剩餘時間
                    if (status != 6)
                    {
                        inplayTimer = Convert.ToInt16(ev.Tm) * 60 + Convert.ToInt16(ev.Ts);
                    }

                    gameInfo.Status = is3x3 ? 1 : status;
                    gameInfo.LastSec = inplayTimer > 0 ? inplayTimer : 0;//防止來源計算後為負值
                    gameInfo.Home = isNBA ? 2 : 1;

                    string key = gameInfo.GetGameDictKey();

                    //開賽、帳務時間沿用字典內
                    if (Config.gameDic.TryGetValue(key, out GameInfo outGame))
                    {
                        //尚未查詢開賽時間, 沿用原設定開賽時間
                        if (outGame.PaymentDate == DateTime.MinValue)
                        {
                            gameInfo.SetGameDate(outGame.GameDateFromSource, true);
                        }
                        else
                        {
                            gameInfo.SetGameDate(outGame.GameDate);
                        }
                    }
                    else
                    {
                        //來源ZD無開賽日，須從DB撈取
                        gameInfo.SetGameDate(Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd HH:mm")), true);
                    }

                    #endregion

                    //自訂義玩法編號
                    int specials = -1;

                    #region 解析玩法

                    var oeMg = ev.MgModels.FirstOrDefault(a => a.Id == PlayId.mg_OE.ToString("d"));
                    //var maTitleOe = oeMg?.maModels.FirstOrDefault(a => a.Na.Trim() == string.Empty)?.paModels.Select((value, index) => new { PairNum = index, Value = value });
                    //var paModelAOe = oeMg?.maModels.FirstOrDefault(a => a.Na.Trim() == "奇数")?.paModels.Select((value, index) => new { PairNum = index, Value = value });
                    //var paModelBOe = oeMg?.maModels.FirstOrDefault(a => a.Na.Trim() == "偶数")?.paModels.Select((value, index) => new { PairNum = index, Value = value });
                    //var betGroupOe = ConvertTools.IsInnerJoin(maTitleOe, paModelAOe, paModelBOe) ? maTitleOe.Join(paModelAOe, a => a.PairNum, b => b.PairNum, (inner, result) => new { inner.PairNum, inner.Value.Na, PlA = result.Value })
                    //                      .Join(paModelBOe, a => a.PairNum, b => b.PairNum, (inner, result) => new { inner.Na, inner.PlA, PlB = result.Value }) : null;

                    #region 全場

                    var playType0 = ev.MgModels.FirstOrDefault(a => a.Id == PlayId.mg_FT.ToString("d"));
                    //OE玩法下的PA包
                    var paOe0 = oeMg?.maModels.FirstOrDefault(a => a.Na == "Match")?.paModels.OrderBy(a => a.Or);

                    if (playType0 != null || paOe0 != null)
                    {
                        ////標題群組
                        //var maTitle = playType0?.maModels.Where(a => a.Id == "0").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////A隊 PL群組
                        //var paModelA = playType0?.maModels.Where(a => a.Id == "1").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////B隊 PL群組
                        //var paModelB = playType0?.maModels.Where(a => a.Id == "2").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////玩法只有一組時, 排除三項玩法
                        //bool isThree = maTitle?.Count() == 1 && playType0?.maModels.Count > 3;
                        ////玩法資訊關聯集合
                        //var betGroup = ConvertTools.IsNotNull(maTitle?.FirstOrDefault(), paModelA?.FirstOrDefault(), paModelB?.FirstOrDefault()) ?
                        //     maTitle?.FirstOrDefault().Join(paModelA?.FirstOrDefault(), a => a.Value.Id, b => b.Value.Ma, (inner, result) => new { inner.Value.Na, PlA = result.Value })
                        //    .Join(paModelB?.FirstOrDefault(), a => a.PlA.Ma, b => b.Value.Ma, (inner, result) => new { inner.Na, inner.PlA, PlB = result.Value }) : null;

                        decimal gpdm = specials = 0;

                        //全場RF
                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                        {
                            byte setType = gameInfo.GetSetType(kTypeRF);
                            //生成物件
                            KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeRF, setType);
                            kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                            //挑出比賽RF排除三項玩法
                            var rfPa = playType0?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Spread") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);
                            var dyPa = playType0?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Money") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                            //RT
                            int strong = 1;
                            //PL
                            decimal plate = 0;
                            decimal odds_A = 0;
                            decimal odds_B = 0;
                            bool isOpen = false;

                            if (rfPa != null && rfPa.All(s => s.Su == "0"))
                            {
                                PaModel paA = isChangeTeam ? rfPa.ElementAt(1) : rfPa.ElementAt(0);
                                PaModel paB = isChangeTeam ? rfPa.ElementAt(0) : rfPa.ElementAt(1);

                                if (decimal.TryParse(paA.Ha, out decimal pkRf))
                                {
                                    //RT
                                    strong = paA.Ha.Contains("-") ? 1 : 0;
                                    //PL
                                    plate = Math.Abs(pkRf);
                                    odds_A = CalculateOD(paA.Od);
                                    odds_B = CalculateOD(paB.Od);
                                    isOpen = true;

                                    //"勾轉換正盤"當RF小於等於1.5，且被讓為低水低於0.74時，DY最平水需要抓DY
                                    //"勾轉換正盤"當RF小於等於1.5，低水邊高於0.74(含)時，DY最平水需要抓DY
                                    //當ZD參數設置"勾只跟PK"、"未勾轉換正盤"，DY最平水需要抓DY
                                    //"勾只跟PK"、"未勾轉換正盤"來源【全場】最平水RF PK為0.5，此時抓取DY
                                    if (dyPa != null && (plate == 0.5m || (Config.isZdOnlyPK && !isOpenConvertML) ||
                                       (isOpenConvertML && plate <= 1.5m && ((strong == 1 && odds_B < 0.74m) || (strong == 0 && odds_A < 0.74m) || Math.Min(odds_A, odds_B) >= 0.74m))))
                                    {
                                        PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                        PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                        decimal odds_A_Dy = CalculateOD(paADy.Od);
                                        decimal odds_B_Dy = CalculateOD(paBDy.Od);

                                        if (plate == 0.5m || (Math.Abs(odds_A_Dy - odds_B_Dy) < Math.Abs(odds_A - odds_B)))
                                        {
                                            strong = 1;
                                            plate = 0;
                                            odds_A = odds_A_Dy;
                                            odds_B = odds_B_Dy;
                                            //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                            isOpen = !(plate == 0.5m && Math.Min(odds_A_Dy, odds_B_Dy) < 0.74m);
                                        }
                                    }
                                }
                            }
                            //ZD參數設置"未勾只跟PK"，無RF需抓DY
                            else if (!Config.isZdOnlyPK && dyPa != null)
                            {
                                PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                strong = 1;
                                plate = 0;
                                odds_A = CalculateOD(paADy.Od);
                                odds_B = CalculateOD(paBDy.Od);
                                isOpen = true;
                            }

                            //設置數據
                            kInfo.SetLine(plate, strong);
                            bInfoA.SetOdds(odds_A);
                            bInfoB.SetOdds(odds_B);
                            kInfo.SetIsOpen(isOpen);
                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                            //生成gpdm並新增對應字典
                            gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                        }

                        //全場OU
                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOU);
                            //生成物件
                            KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOU, setType);
                            kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                            //挑出比賽OU盤
                            var ouPa = playType0?.maModels?.FirstOrDefault(a => (a.Na == "Total") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                            if (ouPa != null && ouPa.All(s => s.Su == "0") && decimal.TryParse(ouPa.ElementAt(0).Ha, out decimal pkOu))
                            {
                                //PL
                                decimal plate = Math.Abs(pkOu);
                                decimal odds_A = CalculateOD(ouPa.ElementAt(0).Od);
                                decimal odds_B = CalculateOD(ouPa.ElementAt(1).Od);

                                //設置數據
                                kInfo.SetLine(plate);
                                bInfoO.SetOdds(odds_A);
                                bInfoU.SetOdds(odds_B);
                                kInfo.SetIsOpen(ouPa.ElementAt(0).Su == "0");
                            }

                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                            //生成gpdm並新增對應字典
                            gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                        }

                        //全場OE
                        if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOE);

                            //限定setType只能有一個
                            if (setType == 1)
                            {
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOE, setType);
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                decimal odds_O = 0m;
                                decimal odds_E = 0m;
                                bool isOpen = false;

                                if (!is3x3)
                                {
                                    //手動設置OE
                                    if (Config.isOpenZdOe)
                                    {
                                        odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                        kInfo.IsManual = true;
                                        isOpen = true;
                                    }
                                    else if (paOe0 != null)
                                    {
                                        //OEPL
                                        PaModel plA = paOe0.ElementAt(0);
                                        PaModel plB = paOe0.ElementAt(1);
                                        int.TryParse(plA.Su, out int tmpSu);
                                        odds_O = CalculateOD(plA.Od);
                                        odds_E = CalculateOD(plB.Od);
                                        isOpen = plA.Su == "0" && plB.Su == "0";
                                    }
                                }

                                isOpen &= odds_O > 0m && odds_E > 0m;

                                //設置數據
                                bInfoO.SetOdds(odds_O);
                                bInfoE.SetOdds(odds_E);
                                kInfo.SetIsOpen(isOpen);

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                //生成gpdm並新增對應字典
                                gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                            }
                        }
                    }

                    #endregion

                    #region 上半

                    var playType1 = ev.MgModels.FirstOrDefault(a => a.Id == PlayId.mg_1H.ToString("d"));
                    //OE玩法下的PA包
                    var paOe1 = oeMg?.maModels.FirstOrDefault(a => a.Na == "1st Half")?.paModels.OrderBy(a => a.Or);

                    if (playType1 != null || paOe1 != null)
                    {
                        ////標題群組
                        //var maTitle = playType1?.maModels.Where(a => a.Id == "0").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////A隊 PL群組
                        //var paModelA = playType1?.maModels.Where(a => a.Id == "1").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////B隊 PL群組
                        //var paModelB = playType1?.maModels.Where(a => a.Id == "2").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////玩法只有一組時, 排除三項玩法
                        //bool isThree = maTitle?.Count() == 1 && playType1?.maModels.Count > 3;
                        ////玩法資訊關聯集合
                        //var betGroup = ConvertTools.IsNotNull(maTitle?.FirstOrDefault(), paModelA?.FirstOrDefault(), paModelB?.FirstOrDefault()) ?
                        //     maTitle?.FirstOrDefault().Join(paModelA?.FirstOrDefault(), a => a.Value.Id, b => b.Value.Ma, (inner, result) => new { inner.Value.Na, PlA = result.Value })
                        //    .Join(paModelB?.FirstOrDefault(), a => a.PlA.Ma, b => b.Value.Ma, (inner, result) => new { inner.Na, inner.PlA, PlB = result.Value }) : null;

                        int gpdm = specials = 1;

                        //上半RF
                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                        {
                            byte setType = gameInfo.GetSetType(kTypeRF);
                            //生成物件
                            KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeRF, setType);
                            kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                            //挑出比賽RF排除三項玩法
                            var rfPa = playType1?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Spread") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);
                            var dyPa = playType1?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Money") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                            //RT
                            int strong = 1;
                            //PL
                            decimal plate = 0;
                            decimal odds_A = 0;
                            decimal odds_B = 0;
                            bool isOpen = false;

                            if (rfPa != null && rfPa.All(s => s.Su == "0"))
                            {
                                PaModel paA = isChangeTeam ? rfPa.ElementAt(1) : rfPa.ElementAt(0);
                                PaModel paB = isChangeTeam ? rfPa.ElementAt(0) : rfPa.ElementAt(1);

                                if (decimal.TryParse(paA.Ha, out decimal pkRf))
                                {
                                    //RT
                                    strong = paA.Ha.Contains("-") ? 1 : 0;
                                    //PL
                                    plate = Math.Abs(pkRf);
                                    odds_A = CalculateOD(paA.Od);
                                    odds_B = CalculateOD(paB.Od);
                                    isOpen = true;

                                    if (dyPa != null)
                                    {
                                        PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                        PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                        decimal odds_A_Dy = CalculateOD(paADy.Od);
                                        decimal odds_B_Dy = CalculateOD(paBDy.Od);

                                        //"勾轉換正盤"當RF小於等於0.5，且被讓為低水低於0.74時，DY最平水需要抓DY
                                        //"勾轉換正盤"當RF小於等於0.5，低水邊高於0.74(含)時，DY最平水需要抓DY
                                        //"勾轉換正盤"當RF為1.5，這時候要另外判斷DY是否為平水(相同水位)，如果DY相同水位則需抓DY
                                        //當ZD參數設置"勾只跟PK"、"未勾轉換正盤"，DY最平水需要抓DY
                                        if (((Config.isZdOnlyPK && !isOpenConvertML) ||
                                            (isOpenConvertML && plate <= 0.5m && ((strong == 1 && odds_B < 0.74m) || (strong == 0 && odds_A < 0.74m) || Math.Min(odds_A, odds_B) >= 0.74m))) &&
                                            (Math.Abs(odds_A_Dy - odds_B_Dy) < Math.Abs(odds_A - odds_B)) ||
                                            (isOpenConvertML && plate == 1.5m && odds_A_Dy == odds_B_Dy))
                                        {
                                            strong = 1;
                                            plate = 0;
                                            odds_A = odds_A_Dy;
                                            odds_B = odds_B_Dy;
                                            isOpen = true;
                                        }
                                    }
                                }
                            }
                            //ZD參數設置"未勾只跟PK"，無RF需抓DY
                            else if (!Config.isZdOnlyPK && dyPa != null)
                            {
                                PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                strong = 1;
                                plate = 0;
                                odds_A = CalculateOD(paADy.Od);
                                odds_B = CalculateOD(paBDy.Od);
                                isOpen = true;
                            }

                            //設置數據
                            kInfo.SetLine(plate, strong);
                            bInfoA.SetOdds(odds_A);
                            bInfoB.SetOdds(odds_B);
                            kInfo.SetIsOpen(isOpen);
                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                            //生成gpdm並新增對應字典
                            gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                        }

                        //上半OU
                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOU);
                            //生成物件
                            KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOU, setType);
                            kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                            //挑出比賽OU盤
                            var ouPa = playType1?.maModels?.FirstOrDefault(a => (a.Na == "Total") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                            if (ouPa != null && ouPa.All(s => s.Su == "0") && decimal.TryParse(ouPa.ElementAt(0).Ha, out decimal pkOu))
                            {
                                //PL
                                decimal plate = Math.Abs(pkOu);
                                decimal odds_O = CalculateOD(ouPa.ElementAt(0).Od);
                                decimal odds_U = CalculateOD(ouPa.ElementAt(1).Od);
                                //設置數據
                                kInfo.SetLine(plate);
                                bInfoO.SetOdds(odds_O);
                                bInfoU.SetOdds(odds_U);
                                kInfo.SetIsOpen(ouPa.ElementAt(0).Su == "0");
                            }

                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                            //生成gpdm並新增對應字典
                            gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                        }

                        //上半OE
                        if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOE);

                            //限定setType只能有一個
                            if (setType == 1)
                            {
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOE, setType);
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                decimal odds_O = 0m;
                                decimal odds_E = 0m;
                                bool isOpen = false;

                                if (!is3x3)
                                {
                                    //手動設置OE
                                    if (Config.isOpenZdOe)
                                    {
                                        odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                        kInfo.IsManual = true;
                                        isOpen = true;
                                    }
                                    else if (paOe1 != null)
                                    {
                                        //OEPL
                                        PaModel plA = paOe1.ElementAt(0);
                                        PaModel plB = paOe1.ElementAt(1);
                                        int.TryParse(plA.Su, out int tmpSu);
                                        odds_O = CalculateOD(plA.Od);
                                        odds_E = CalculateOD(plB.Od);
                                        isOpen = plA.Su == "0" && plB.Su == "0";
                                    }
                                }

                                isOpen &= odds_O > 0m && odds_E > 0m;

                                //設置數據
                                bInfoO.SetOdds(odds_O);
                                bInfoE.SetOdds(odds_E);
                                kInfo.SetIsOpen(isOpen);

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                //生成gpdm並新增對應字典
                                gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                            }
                        }
                    }

                    #endregion

                    #region 下半

                    var playType2 = ev.MgModels.FirstOrDefault(a => a.Id == PlayId.mg_2H.ToString("d"));
                    //OE玩法下的PA包
                    var paOe2 = oeMg?.maModels.FirstOrDefault(a => a.Na == "2nd Half")?.paModels.OrderBy(a => a.Or);

                    if (playType2 != null || paOe2 != null)
                    {
                        ////標題群組
                        //var maTitle = playType2?.maModels.Where(a => a.Id == "0").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////A隊 PL群組
                        //var paModelA = playType2?.maModels.Where(a => a.Id == "1").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////B隊 PL群組
                        //var paModelB = playType2?.maModels.Where(a => a.Id == "2").Select(b => b.paModels.Select((value) => new { Value = value }));
                        ////玩法只有一組時, 排除三項玩法
                        //bool isThree = maTitle?.Count() == 1 && playType2?.maModels.Count > 3;
                        ////玩法資訊關聯集合
                        //var betGroup = ConvertTools.IsNotNull(maTitle?.FirstOrDefault(), paModelA?.FirstOrDefault(), paModelB?.FirstOrDefault()) ?
                        //     maTitle?.FirstOrDefault().Join(paModelA?.FirstOrDefault(), a => a.Value.Id, b => b.Value.Ma, (inner, result) => new { inner.Value.Na, PlA = result.Value })
                        //    .Join(paModelB?.FirstOrDefault(), a => a.PlA.Ma, b => b.Value.Ma, (inner, result) => new { inner.Na, inner.PlA, PlB = result.Value }) : null;

                        int gpdm = specials = 2;

                        //下半RF
                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                        {
                            byte setType = gameInfo.GetSetType(kTypeRF);
                            //生成物件
                            KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeRF, setType);
                            kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                            //挑出比賽RF排除三項玩法
                            var rfPa = playType2?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Spread") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);
                            var dyPa = playType2?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Money") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                            //RT
                            int strong = 1;
                            //PL
                            decimal plate = 0;
                            decimal odds_A = 0;
                            decimal odds_B = 0;
                            bool isOpen = false;

                            if (rfPa != null && rfPa.All(s => s.Su == "0"))
                            {
                                PaModel paA = isChangeTeam ? rfPa.ElementAt(1) : rfPa.ElementAt(0);
                                PaModel paB = isChangeTeam ? rfPa.ElementAt(0) : rfPa.ElementAt(1);

                                if (decimal.TryParse(paA.Ha, out decimal pk))
                                {
                                    //RT
                                    strong = paA.Ha.Contains("-") ? 1 : 0;
                                    //PL
                                    plate = Math.Abs(pk);
                                    odds_A = CalculateOD(paA.Od);
                                    odds_B = CalculateOD(paB.Od);
                                    isOpen = true;

                                    if (dyPa != null)
                                    {
                                        PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                        PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                        decimal odds_A_Dy = CalculateOD(paADy.Od);
                                        decimal odds_B_Dy = CalculateOD(paBDy.Od);

                                        //"勾轉換正盤"當RF小於等於0.5，且被讓為低水低於0.74時，DY最平水需要抓DY
                                        //"勾轉換正盤"當RF小於等於0.5，低水邊高於0.74(含)時，DY最平水需要抓DY
                                        //"勾轉換正盤"當RF為1.5，這時候要另外判斷DY是否為平水(相同水位)，如果DY相同水位則需抓DY
                                        //當ZD參數設置"勾只跟PK"、"未勾轉換正盤"，DY最平水需要抓DY
                                        if (((Config.isZdOnlyPK && !isOpenConvertML) ||
                                            (isOpenConvertML && plate <= 0.5m && ((strong == 1 && odds_B < 0.74m) || (strong == 0 && odds_A < 0.74m) || Math.Min(odds_A, odds_B) >= 0.74m))) &&
                                            (Math.Abs(odds_A_Dy - odds_B_Dy) < Math.Abs(odds_A - odds_B)) ||
                                            (isOpenConvertML && plate == 1.5m && odds_A_Dy == odds_B_Dy))
                                        {
                                            strong = 1;
                                            plate = 0;
                                            odds_A = odds_A_Dy;
                                            odds_B = odds_B_Dy;
                                            isOpen = true;
                                        }
                                    }
                                }
                            }
                            //ZD參數設置"未勾只跟PK"，無RF需抓DY
                            else if (!Config.isZdOnlyPK && dyPa != null)
                            {
                                PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                strong = 1;
                                plate = 0;
                                odds_A = CalculateOD(paADy.Od);
                                odds_B = CalculateOD(paBDy.Od);
                                isOpen = true;
                            }

                            //設置數據
                            kInfo.SetLine(plate, strong);
                            bInfoA.SetOdds(odds_A);
                            bInfoB.SetOdds(odds_B);
                            kInfo.SetIsOpen(isOpen);
                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                            //生成gpdm並新增對應字典
                            gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                        }

                        //下半OU
                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOU);
                            //生成物件
                            KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOU, setType);
                            kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                            //挑出比賽OU盤
                            var ouPa = playType2?.maModels?.FirstOrDefault(a => (a.Na == "Total") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                            if (ouPa != null && ouPa.All(s => s.Su == "0") && decimal.TryParse(ouPa.ElementAt(0).Ha, out decimal pk))
                            {
                                //PL
                                decimal plate = Math.Abs(pk);
                                decimal odds_O = CalculateOD(ouPa.ElementAt(0).Od);
                                decimal odds_U = CalculateOD(ouPa.ElementAt(1).Od);
                                //設置數據
                                kInfo.SetLine(plate);
                                bInfoO.SetOdds(odds_O);
                                bInfoU.SetOdds(odds_U);
                                kInfo.SetIsOpen(ouPa.ElementAt(0).Su == "0");
                            }

                            //放進字典
                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                            //生成gpdm並新增對應字典
                            gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                        }

                        //下半OE
                        if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                        {
                            byte setType = gameInfo.GetSetType(kTypeOE);

                            //限定setType只能有一個
                            if (setType == 1)
                            {
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOE, setType);
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                decimal odds_O = 0m;
                                decimal odds_E = 0m;
                                bool isOpen = false;

                                if (!is3x3)
                                {
                                    //手動設置OE
                                    if (Config.isOpenZdOe)
                                    {
                                        odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                        kInfo.IsManual = true;
                                        isOpen = true;
                                    }
                                    else if (paOe2 != null)
                                    {
                                        //OEPL
                                        PaModel plA = paOe2.ElementAt(0);
                                        PaModel plB = paOe2.ElementAt(1);
                                        int.TryParse(plA.Su, out int tmpSu);
                                        odds_O = CalculateOD(plA.Od);
                                        odds_E = CalculateOD(plB.Od);
                                        isOpen = plA.Su == "0" && plB.Su == "0";
                                    }
                                }

                                isOpen &= odds_O > 0m && odds_E > 0m;

                                //設置數據
                                bInfoO.SetOdds(odds_O);
                                bInfoE.SetOdds(odds_E);
                                kInfo.SetIsOpen(isOpen);

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                //生成gpdm並新增對應字典
                                gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                            }
                        }
                    }

                    #endregion

                    #region 單節

                    var playType3 = ev.MgModels.Where(a => (a.Id == PlayId.mg_Q1.ToString("d") || a.Id == PlayId.mg_Q2.ToString("d") ||
                                                           a.Id == PlayId.mg_Q3.ToString("d") || a.Id == PlayId.mg_Q4.ToString("d")) ||
                                                           a.Id == PlayId.mg_OE.ToString("d"));

                    if (playType3 != null)
                    {
                        for (int num = 1; num < 5; num++)
                        {
                            string quarter = num == 1 ? "1st Quarter" : num == 2 ? "2nd Quarter" : num == 3 ? "3rd Quarter" : "4th Quarter";
                            //挑出各節玩法
                            var playTypeEach = playType3.FirstOrDefault(a => Regex.IsMatch(a.Na, quarter + " Lines"));
                            //OE玩法下的PA包
                            var paOe3 = oeMg?.maModels.FirstOrDefault(a => a.Na == quarter)?.paModels.OrderBy(a => a.Or);

                            if (playTypeEach != null || paOe3 != null)
                            {
                                ////標題群組
                                //var maTitle = playTypeEach?.maModels.Where(a => a.Id == "0").Select(b => b.paModels.Select((value) => new { Value = value }));
                                ////A隊 PL群組
                                //var paModelA = playTypeEach?.maModels.Where(a => a.Id == "1").Select(b => b.paModels.Select((value) => new { Value = value }));
                                ////B隊 PL群組
                                //var paModelB = playTypeEach?.maModels.Where(a => a.Id == "2").Select(b => b.paModels.Select((value) => new { Value = value }));
                                ////玩法只有一組時, 排除三項玩法
                                //bool isThree = maTitle?.Count() == 1 && playTypeEach?.maModels.Count > 3;
                                ////玩法資訊關聯集合
                                //var betGroup = ConvertTools.IsNotNull(maTitle?.FirstOrDefault(), paModelA?.FirstOrDefault(), paModelB?.FirstOrDefault()) ?
                                //     maTitle?.FirstOrDefault().Join(paModelA?.FirstOrDefault(), a => a.Value.Id, b => b.Value.Ma, (inner, result) => new { inner.Value.Na, PlA = result.Value })
                                //    .Join(paModelB?.FirstOrDefault(), a => a.PlA.Ma, b => b.Value.Ma, (inner, result) => new { inner.Na, inner.PlA, PlB = result.Value }) : null;

                                //第一節:specials = 3，第二節:specials = 4往下類推
                                int gpdm = specials = num + 2;

                                //單節RF
                                if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeRF);
                                    //生成物件
                                    KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeRF, setType);
                                    kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                                    BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                    BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                                    //挑出比賽RF排除三項玩法
                                    var rfPa = playTypeEach?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Spread") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);
                                    var dyPa = playTypeEach?.maModels?.FirstOrDefault(a => a.Na.StartsWith("Money") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);
                                    //RT
                                    int strong = 1;
                                    //PL
                                    decimal plate = 0;
                                    decimal odds_A = 0;
                                    decimal odds_B = 0;
                                    bool isOpen = false;

                                    if (rfPa != null && rfPa.All(s => s.Su == "0"))
                                    {
                                        PaModel paA = isChangeTeam ? rfPa.ElementAt(1) : rfPa.ElementAt(0);
                                        PaModel paB = isChangeTeam ? rfPa.ElementAt(0) : rfPa.ElementAt(1);

                                        if (decimal.TryParse(paA.Ha, out decimal pk))
                                        {
                                            //RT
                                            strong = paA.Ha.Contains("-") ? 1 : 0;
                                            //PL
                                            plate = Math.Abs(pk);
                                            odds_A = CalculateOD(paA.Od);
                                            odds_B = CalculateOD(paB.Od);
                                            isOpen = true;

                                            if (dyPa != null)
                                            {
                                                PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                                PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                                decimal odds_A_Dy = CalculateOD(paADy.Od);
                                                decimal odds_B_Dy = CalculateOD(paBDy.Od);

                                                //"勾轉換正盤"當RF小於等於0.5，且被讓為低水低於0.74時，DY最平水需要抓DY
                                                //"勾轉換正盤"當RF小於等於0.5，低水邊高於0.74(含)時，DY最平水需要抓DY
                                                //"勾轉換正盤"當RF為1.5，這時候要另外判斷DY是否為平水(相同水位)，如果DY相同水位則需抓DY
                                                //當ZD參數設置"勾只跟PK"、"未勾轉換正盤"，DY最平水需要抓DY
                                                if (((Config.isZdOnlyPK && !isOpenConvertML) ||
                                                    (isOpenConvertML && plate <= 0.5m && ((strong == 1 && odds_B < 0.74m) || (strong == 0 && odds_A < 0.74m) || Math.Min(odds_A, odds_B) >= 0.74m))) &&
                                                    (Math.Abs(odds_A_Dy - odds_B_Dy) < Math.Abs(odds_A - odds_B)) ||
                                                    (isOpenConvertML && plate == 1.5m && odds_A_Dy == odds_B_Dy))
                                                {
                                                    strong = 1;
                                                    plate = 0;
                                                    odds_A = odds_A_Dy;
                                                    odds_B = odds_B_Dy;
                                                    isOpen = true;
                                                }
                                            }
                                        }
                                    }
                                    //ZD參數設置"未勾只跟PK"，無RF需抓DY
                                    else if (!Config.isZdOnlyPK && dyPa != null)
                                    {
                                        PaModel paADy = isChangeTeam ? dyPa.ElementAt(1) : dyPa.ElementAt(0);
                                        PaModel paBDy = isChangeTeam ? dyPa.ElementAt(0) : dyPa.ElementAt(1);
                                        strong = 1;
                                        plate = 0;
                                        odds_A = CalculateOD(paADy.Od);
                                        odds_B = CalculateOD(paBDy.Od);
                                        isOpen = true;
                                    }

                                    //設置數據
                                    kInfo.SetLine(plate, strong);
                                    bInfoA.SetOdds(odds_A);
                                    bInfoB.SetOdds(odds_B);
                                    kInfo.SetIsOpen(isOpen);
                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                    //生成gpdm並新增對應字典
                                    gpdm = specials * 100 + setType;
                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                }

                                //單節OU
                                if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeOU);
                                    //生成物件
                                    KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOU, setType);
                                    kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                                    BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                    BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                                    //挑出比賽OU盤
                                    var ouPa = playTypeEach?.maModels?.FirstOrDefault(a => (a.Na == "Total") && a.paModels.Count == 2)?.paModels.OrderBy(a => a.Or);

                                    if (ouPa != null && ouPa.All(s => s.Su == "0"))
                                    {
                                        //PL
                                        decimal plate = Math.Abs(decimal.Parse(ouPa.ElementAt(0).Ha));
                                        decimal odds_O = CalculateOD(ouPa.ElementAt(0).Od);
                                        decimal odds_U = CalculateOD(ouPa.ElementAt(1).Od);
                                        //設置數據
                                        kInfo.SetLine(plate);
                                        bInfoO.SetOdds(odds_O);
                                        bInfoU.SetOdds(odds_U);
                                        kInfo.SetIsOpen(ouPa.ElementAt(0).Su == "0");
                                    }

                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                    kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                    //生成gpdm並新增對應字典
                                    gpdm = specials * 100 + setType;
                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                }

                                //單節OE
                                if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeOE);

                                    //限定setType只能有一個
                                    if (setType == 1)
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOE, setType);
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                        BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                        decimal odds_O = 0m;
                                        decimal odds_E = 0m;
                                        bool isOpen = false;

                                        if (!is3x3)
                                        {
                                            //手動設置OE
                                            if (Config.isOpenZdOe)
                                            {
                                                odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                                kInfo.IsManual = true;
                                                isOpen = true;
                                            }
                                            else if (paOe3 != null)
                                            {
                                                //OEPL
                                                PaModel plA = paOe3.ElementAt(0);
                                                PaModel plB = paOe3.ElementAt(1);
                                                int.TryParse(plA.Su, out int tmpSu);
                                                odds_O = CalculateOD(plA.Od);
                                                odds_E = CalculateOD(plB.Od);
                                                isOpen = plA.Su == "0" && plB.Su == "0";
                                            }
                                        }

                                        isOpen &= odds_O > 0m && odds_E > 0m;

                                        //設置數據
                                        bInfoO.SetOdds(odds_O);
                                        bInfoE.SetOdds(odds_E);
                                        kInfo.SetIsOpen(isOpen);

                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                        //生成gpdm並新增對應字典
                                        gpdm = specials * 100 + setType;
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                    }
                                }
                            }
                        }
                    }

                    #endregion

                    #region 總入球玩法

                    string[] playIds = new string[] { PlayId.teamTotal_FT.ToString("d"), PlayId.teamTotal_HT.ToString("d"), PlayId.teamTotal_2H.ToString("d"), PlayId.teamTotal_Q1.ToString("d"), PlayId.teamTotal_Q2.ToString("d"), PlayId.teamTotal_Q3.ToString("d"), PlayId.teamTotal_Q4.ToString("d") };

                    var teamTotalMg = ev.MgModels.Where(a => playIds.Contains(a.Id));

                    foreach (MgModel mg in teamTotalMg)
                    {
                        int gpdm, tmpSpecials = -1;

                        if (Enum.TryParse(mg.Id, false, out PlayId playId))
                        {
                            switch (playId)
                            {
                                case PlayId.teamTotal_FT:
                                    tmpSpecials = 14;
                                    break;
                                case PlayId.teamTotal_HT:
                                    tmpSpecials = 16;
                                    break;
                                case PlayId.teamTotal_2H:
                                    tmpSpecials = 18;
                                    break;
                                case PlayId.teamTotal_Q1:
                                    tmpSpecials = 20;
                                    break;
                                case PlayId.teamTotal_Q2:
                                    tmpSpecials = 22;
                                    break;
                                case PlayId.teamTotal_Q3:
                                    tmpSpecials = 24;
                                    break;
                                case PlayId.teamTotal_Q4:
                                    tmpSpecials = 26;
                                    break;
                                default:
                                    tmpSpecials = -1;
                                    break;
                            }
                        }

                        foreach (MaModel ma in mg.maModels)
                        {
                            specials = tmpSpecials;

                            if (isChangeTeam)
                            {
                                if (ma.Na == ev.TeAModel.Na)
                                {
                                    specials = tmpSpecials + 1;
                                }
                            }
                            else
                            {
                                if (ma.Na == ev.TeBModel.Na)
                                {
                                    specials = tmpSpecials + 1;
                                }
                            }

                            //全場OU
                            if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                            {
                                byte setType = gameInfo.GetSetType(kTypeOU);
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOU, setType);
                                kInfo.IsSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                                var ouPa = ma.paModels.OrderBy(a => a.Id);

                                if (ouPa.Count() == 2)
                                {
                                    var ouA = ouPa.FirstOrDefault();
                                    var ouB = ouPa.LastOrDefault();

                                    if (decimal.TryParse(ouA.Ha, out decimal pk))
                                    {
                                        //PL
                                        decimal plate = Math.Abs(pk);
                                        decimal odds_A = CalculateOD(ouA.Od);
                                        decimal odds_B = CalculateOD(ouB.Od);

                                        //設置數據
                                        kInfo.SetLine(plate);
                                        bInfoO.SetOdds(odds_A);
                                        bInfoU.SetOdds(odds_B);
                                        kInfo.SetIsOpen(ouA.Su == "0");
                                    }

                                }

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                //生成gpdm並新增對應字典
                                gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                            }

                            //全場OE
                            if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                            {
                                byte setType = gameInfo.GetSetType(kTypeOE);

                                //限定setType只能有一個
                                if (setType == 1)
                                {
                                    //生成物件
                                    KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOE, setType);
                                    BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                    BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));

                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                    kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                    //生成gpdm並新增對應字典
                                    gpdm = specials * 100 + setType;
                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                }
                            }
                        }
                    }

                    #endregion

                    #endregion

                    if (ev.IsRemove || Config.FilterLeagueHashSet.Any(filter => gameInfo.League.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        gameInfo.DataId = -1;
                    }
                    else
                    {
                        gameInfo.CheckPlAndSet(isOpenConvertML);
                    }

                    //因應來源名稱替換功能，需要抓出修改前的賽事進行關閉
                    foreach (GameInfo game in Config.gameDic.Values.Where(a => a.GetSourceGameDictKey() == gameInfo.GetSourceGameDictKey() && a.GetGameDictKey() != key))
                    {
                        game.DataId = -1;
                    }

                    //存在才處理
                    if (Config.gameDic.TryGetValue(key, out GameInfo getGame))
                    {
                        getGame.Compare(gameInfo, GpType.Zd);

                        //註記刪除不存在的玩法
                        var delKinfo = getGame.ObjDic.Where(a => !gameInfo.ObjDic.Keys.Contains(a.Key)).SelectMany(a => a.Value);

                        foreach (KeyValuePair<int, KInfo> kInfo in delKinfo)
                        {
                            kInfo.Value.DataId = -1;
                        }
                    }
                    //不存在直接添加
                    else
                    {
                        if (gameInfo.DataId >= 0)
                        {
                            var oldGame = Config.gameDic.Values.FirstOrDefault(a => a.SourceId == gameInfo.SourceId);

                            if (oldGame != null)
                            {
                                oldGame.DataId = -1;
                            }

                            Config.gameDic.TryAdd(key, gameInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, GpType.Zd.ToString() + Environment.NewLine + ex.ToString() + Environment.NewLine + "來源資料:" + dataInfo.Data);
            }
        }

        // 計算OD值
        private static decimal CalculateOD(string data)
        {
            //PL
            string[] tmpOD = data.Split('/');

            if (tmpOD.Length != 2 || tmpOD[1] == "0")
            {
                return 0;
            }
            else
            {
                decimal odd = decimal.Floor((decimal.Parse(tmpOD[0]) / decimal.Parse(tmpOD[1])) * 100m) / 100m;
                return odd >= 0 ? odd : 0;
            }
        }

        //轉換比分
        private static byte ParserScore(string tempScore)
        {
            if (byte.TryParse(tempScore, out byte score))
            {
                return score;
            }
            else
            {
                return 0;
            }
        }
    }
}
