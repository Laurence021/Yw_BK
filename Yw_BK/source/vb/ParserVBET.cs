#region 功能修改描述與時間

/*
    描述：VBET解析
    日期：2020-04-27
            
    描述：排除 ZD 全場/上下半/單節 主客隊總得分 oe設置
    日期：2020-08-13
                            
    描述：調整ZD開賽時間有查詢過才使用
    日期：2021-06-29
*/

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Yw_BK.model;
using Yw_BK.source;
using YwCore;

namespace Yw_BK
{
    /// <summary>
    /// VB數據解析
    /// </summary>
    public class ParserVBET
    {
        #region 玩法類型篩選 [來源參數 : Market_type]

        //全場
        private static string[] matchFT = { "MatchHandicap", "MatchTotal", "MatchOddEvenTotal", "MatchWinner2" };
        //單節
        private static string[] period = { "QuarterHandicap", "QuarterTotal", "QuarterOddEvenTotal", "QuarterWinner2" };

        #endregion

        /// <summary>
        /// ZD內頁解析
        /// </summary>
        /// <param name="dataInfo">來源推送內頁資料</param>
        public static void MoreParser(DataInfo dataInfo, ConnectVBET connect)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //取得365推送資料物件
                ConcurrentDictionary<string, VbLeague> returnData = connect.FormatData.DataToModel(dataInfo.Data);

                //聯盟層
                foreach (VbLeague leagueInfo in returnData.Values)
                {
                    if (leagueInfo == null) { continue; }
                    string league = leagueInfo.Name;

                    if (string.IsNullOrEmpty(league) || league == "null") { continue; }

                    //賽事層
                    foreach (VbGame tmpGame in leagueInfo.GameDic.Values)
                    {
                        if (tmpGame == null) { continue; }

                        string teamA = tmpGame.Team1_name;
                        string teamB = tmpGame.Team2_name;

                        if (string.IsNullOrEmpty(teamA) || string.IsNullOrEmpty(teamB) || teamA == "null" || teamB == "null")
                        {
                            continue;
                        }

                        GameInfoVBET gameInfo = new GameInfoVBET(tmpGame.Id.ToString());
                        gameInfo.GType = (byte)GType.BkZd;
                        gameInfo.GpType = GpType.Zd;
                        gameInfo.League.Name = league;

                        if (Config.FilterLeagueHashSet.Any(filter => gameInfo.League.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            gameInfo.DataId = -1;
                        }

                        //設定預設後綴
                        if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                        {
                            gameInfo.Suffix.Name = WebName.suffixDic[suffixId].En;
                            gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                            gameInfo.Suffix.Id = suffixId;
                        }

                        bool isChangeTeam = false;
                        //NCAA, 夏季聯賽, G聯盟 隊伍反抓
                        if ((league.Contains("NCAA") && !league.Contains("Women")) || Config.CheckNbaExcludeName(league)) { isChangeTeam = true; }
                        //交換對伍
                        ConvertTools.ExchangeValue(ref teamA, ref teamB, isChangeTeam);
                        gameInfo.TeamA.Name = teamA;
                        gameInfo.TeamB.Name = teamB;

                        //無聯盟隊伍則不往下解析
                        if (string.IsNullOrEmpty(gameInfo.League.Name) || string.IsNullOrEmpty(gameInfo.TeamA.Name) || string.IsNullOrEmpty(gameInfo.TeamB.Name))
                        {
                            continue;
                        }

                        #region 設定比分、賽事狀態

                        if (tmpGame.Stats != null)
                        {
                            gameInfo.Score1.SetScore(ParserScore(tmpGame.Stats.score_set1, out byte Score1A, out byte Score1B), isChangeTeam ? Score1B : Score1A, isChangeTeam ? Score1A : Score1B);
                            gameInfo.Score2.SetScore(ParserScore(tmpGame.Stats.score_set2, out byte Score2A, out byte Score2B), isChangeTeam ? Score2B : Score2A, isChangeTeam ? Score2A : Score2B);
                            gameInfo.Score3.SetScore(ParserScore(tmpGame.Stats.score_set3, out byte Score3A, out byte Score3B), isChangeTeam ? Score3B : Score3A, isChangeTeam ? Score3A : Score3B);
                            gameInfo.Score4.SetScore(ParserScore(tmpGame.Stats.score_set4, out byte Score4A, out byte Score4B), isChangeTeam ? Score4B : Score4A, isChangeTeam ? Score4A : Score4B);

                            if (byte.TryParse((gameInfo.Score1.GetScoreA() + gameInfo.Score2.GetScoreA()).ToString(), out byte scoreHalf1A) &&
                                byte.TryParse((gameInfo.Score1.GetScoreB() + gameInfo.Score2.GetScoreB()).ToString(), out byte scoreHalf1B) &&
                                byte.TryParse((gameInfo.Score3.GetScoreA() + gameInfo.Score4.GetScoreA()).ToString(), out byte scoreHalf2A) &&
                                byte.TryParse((gameInfo.Score3.GetScoreB() + gameInfo.Score4.GetScoreB()).ToString(), out byte scoreHalf2B))
                            {
                                gameInfo.ScoreHalf1.SetScore(true, scoreHalf1A, scoreHalf1B);
                                gameInfo.ScoreHalf2.SetScore(true, scoreHalf2A, scoreHalf2B);
                            }
                        }

                        gameInfo.Home = 1;

                        if (tmpGame.Info != null)
                        {
                            //設定目前總分
                            gameInfo.Score.SetScore(ParserScore(tmpGame.Info.Score1, tmpGame.Info.Score2, out byte ScoreA, out byte ScoreB), isChangeTeam ? ScoreB : ScoreA, isChangeTeam ? ScoreA : ScoreB);
                            //賽事狀態
                            gameInfo.Status = GetStatus(tmpGame.Info.Current_game_state, league);

                            //剩餘秒數
                            if (!string.IsNullOrEmpty(tmpGame.Info.Current_game_time)
                                && TimeSpan.TryParse("00:" + tmpGame.Info.Current_game_time, out TimeSpan time))
                            {
                                gameInfo.LastSec = Convert.ToInt32(time.TotalSeconds);
                            }
                            else
                            {
                                gameInfo.LastSec = 0;
                            }
                        }

                        //設定賽事key
                        string key = gameInfo.GetGameDictKey();

                        //設定比賽時間
                        DateTime simpleDate = Convert.ToDateTime("1970/01/01 8:00:00").AddSeconds(double.Parse(tmpGame.Start_ts));
                        gameInfo.SetGameDate(Convert.ToDateTime(simpleDate.ToString("yyyy-MM-dd HH:mm")));

                        #endregion

                        #region 全場、上半、第N節

                        for (int specials = 0; specials <= 6; specials++)
                        {
                            List<Market> markets = null;

                            switch (specials)
                            {
                                //全場
                                case 0:
                                    //取出所有全場玩法
                                    markets = tmpGame.MarketDic.Values.Where(a => a != null && matchFT.Contains(a.Market_type.Trim())).ToList();
                                    break;
                                //第一節
                                case 3:
                                //第二節 
                                case 4:
                                //第三節
                                case 5:
                                //第四節
                                case 6:
                                    //取出所有第N節玩法
                                    markets = tmpGame.MarketDic.Values.Where(a => a != null && a.Sequence == (specials - 2)
                                                                               && period.Contains(a.Market_type.Trim())).ToList();
                                    break;
                                default:
                                    continue;
                            }

                            //沒玩法則跳過
                            if (markets.Count == 0) { continue; }

                            #region RF

                            //RF
                            if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                            {
                                #region 抓取最接近平水的pk

                                List<Market> market_RFs = markets.Where(a => a.Display_key.Trim().ToUpper().Equals("HANDICAP")).ToList<Market>();

                                decimal pk = 0;
                                int strong = 1;
                                decimal odds_A = 0;
                                decimal odds_B = 0;
                                decimal gapRf = 0;

                                foreach (Market marketRf in market_RFs)
                                {
                                    if (marketRf == null || marketRf.OddDic == null) { continue; }

                                    EventOdd eventA = marketRf.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("HOME")));
                                    EventOdd eventB = marketRf.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("AWAY")));

                                    if (eventA != null && eventB != null && eventA.Pk != -999 && eventA.Pl >= 0 && eventB.Pl >= 0)
                                    {
                                        //是否交換
                                        ConvertTools.ExchangeValue(ref eventA, ref eventB, isChangeTeam);
                                        //取得與平水的差距
                                        decimal tmpGapRf = Math.Abs(eventA.Pl - eventB.Pl);

                                        //[起始設定] or [判斷比較低水] 才設定
                                        if ((odds_A == 0 && odds_B == 0)
                                            || (tmpGapRf < gapRf))
                                        {
                                            pk = Math.Abs(eventA.Pk);
                                            strong = eventA.Pk < 0 ? 1 : 0;
                                            odds_A = eventA.Pl;
                                            odds_B = eventB.Pl;
                                            gapRf = tmpGapRf;
                                        }
                                    }
                                }

                                #endregion

                                byte setType = gameInfo.GetSetType(kTypeRF);
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeRF, setType);
                                kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                                //設置數據
                                kInfo.SetLine(pk, strong);
                                bInfoA.SetOdds(odds_A);
                                bInfoB.SetOdds(odds_B);
                                kInfo.SetIsOpen(pk > 0 && odds_A > 0 && odds_B > 0);

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                //生成gpdm並新增對應字典
                                decimal gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                            }

                            #endregion

                            #region OU

                            //OU
                            if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                            {
                                #region 抓取最接近平水的pk

                                List<Market> market_OUs = markets.Where(a => a.Display_key.Trim().ToUpper().Equals("TOTALS")).ToList<Market>();

                                decimal pk = 0;
                                decimal odds_A = 0;
                                decimal odds_B = 0;
                                decimal gapRf = 0;

                                foreach (Market marketOu in market_OUs)
                                {
                                    if (marketOu == null || marketOu.OddDic == null) { continue; }

                                    EventOdd eventA = marketOu.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("OVER")));
                                    EventOdd eventB = marketOu.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("UNDER")));

                                    if (eventA != null && eventB != null && eventA.Pk != -999 && eventA.Pl >= 0 && eventB.Pl >= 0)
                                    {
                                        //取得與平水的差距
                                        decimal tmpGapOu = Math.Abs(eventA.Pl - eventB.Pl);

                                        //[起始設定] or [判斷比較低水] 才設定
                                        if ((odds_A == 0 && odds_B == 0)
                                            || (tmpGapOu < gapRf))
                                        {
                                            pk = Math.Abs(eventA.Pk);
                                            odds_A = eventA.Pl;
                                            odds_B = eventB.Pl;
                                            gapRf = tmpGapOu;
                                        }
                                    }
                                }

                                #endregion

                                byte setType = gameInfo.GetSetType(kTypeOU);
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeOU, setType);
                                kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                                //設置數據
                                kInfo.SetLine(pk);
                                bInfoA.SetOdds(odds_A);
                                bInfoB.SetOdds(odds_B);
                                kInfo.SetIsOpen(pk > 0 && odds_A > 0 && odds_B > 0);

                                //放進字典
                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                //生成gpdm並新增對應字典
                                decimal gpdm = specials * 100 + setType;
                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                            }

                            #endregion

                            #region OE

                            Market market_OE = markets.FirstOrDefault(a => a.Display_key.Trim().ToUpper().Equals("ODD/EVEN"));

                            //OE
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

                                    //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                                    if (Config.isOpenZdOe && (specials < 14 || specials > 29))
                                    {
                                        if (gameInfo.GpType == GpType.Zd)
                                        {
                                            odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                        }

                                        kInfo.IsManual = true;
                                        isOpen = true;
                                    }
                                    else if (market_OE != null && market_OE.OddDic != null)
                                    {
                                        EventOdd oddA = market_OE.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("ODD")));
                                        EventOdd oddB = market_OE.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("EVEN")));

                                        if (oddA != null && oddB != null)
                                        {
                                            odds_O = oddA.Pl >= 0 ? oddA.Pl : 0;
                                            odds_E = oddB.Pl >= 0 ? oddB.Pl : 0;
                                        }
                                    }

                                    isOpen = odds_O > 0m && odds_E > 0m;

                                    //設置數據
                                    bInfoO.SetOdds(odds_O);
                                    bInfoE.SetOdds(odds_E);
                                    kInfo.SetIsOpen(isOpen);

                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                    kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                    //生成gpdm並新增對應字典
                                    decimal gpdm = specials * 100 + setType;
                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                }
                            }

                            #endregion

                            if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeDy))
                            {
                                Market market_DY = markets.FirstOrDefault(a => a.Display_key.Trim().ToUpper().Equals("P1P2"));

                                byte setType = 1;
                                //生成物件
                                KInfo kInfo = new KInfo(GpType.Zd.ToString(), 0, kTypeDy, setType);
                                kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));

                                if (market_DY != null && market_DY.OddDic != null)
                                {
                                    EventOdd oddA = market_DY.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("P1")));
                                    EventOdd oddB = market_DY.OddDic.Values.FirstOrDefault(a => a != null && (a.Type.Trim().ToUpper().Equals("P2")));

                                    if (oddA != null && oddB != null && oddA.Pl > 0 && oddB.Pl > 0)
                                    {
                                        //設置數據
                                        bInfoA.SetOdds(oddA.Pl);
                                        bInfoB.SetOdds(oddB.Pl);
                                        kInfo.SetIsOpen();

                                        decimal gpdm = specials * 100 + setType;

                                        //ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                                        //"勾只跟PK"來源【全場】最平水RF PK為0.5，此時抓取獨贏
                                        if (Config.isZdOnlyPK &&
                                            gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                                            gpdmInfo.TryGetValue(kTypeDy, out KInfo kInfoRf) && kInfoRf.IsOpen &&
                                            (kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                                            kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                                            Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > Math.Abs(oddA.Pl - oddB.Pl) ||
                                            (specials == 0 && kInfoRf.Line == 500)))
                                        {
                                            //更新kInfo
                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                            //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                            kInfo.SetIsOpen(!(specials == 0 && kInfoRf.Line == 500 && Math.Min(oddA.Pl, oddB.Pl) < 0.74m));
                                            gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                        }
                                        //ZD參數設置"未勾只跟PK"，無RF需抓DY
                                        else if (!Config.isZdOnlyPK &&
                                                (!gameInfo.ObjDic.Keys.Contains(gpdm) ||
                                                (gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo _gpdmInfo) &&
                                                (!_gpdmInfo.Keys.Contains(kTypeDy) || (_gpdmInfo.TryGetValue(kTypeDy, out KInfo _kInfoRf) && !_kInfoRf.IsOpen)))))
                                        {
                                            //更新kInfo
                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm][kTypeDy] = kInfo;
                                        }
                                    }
                                }
                            }
                        }
                        #endregion

                        //因應來源名稱替換功能，需要抓出修改前的賽事進行關閉
                        foreach (GameInfo game in Config.gameDic.Values.Where(a => a.GetSourceGameDictKey() == gameInfo.GetSourceGameDictKey() && a.GetGameDictKey() != key))
                        {
                            game.DataId = -1;
                        }

                        gameInfo.CheckPlAndSet();

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

                #region 判斷刪除來源消失賽事

                foreach (GameInfoVBET gameInfo in Config.gameDic.Values.Where(a => a.GpType == GpType.Zd))
                {
                    //判斷不存在於目前賽事id清單，則設定刪除
                    if (int.TryParse(gameInfo.SourceId,out int sourceId) && !connect.gameIdList.Contains(sourceId))
                    {
                        gameInfo.DataId = -1;
                    }
                }

                #endregion
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, GpType.Zd.ToString() + Environment.NewLine + ex.ToString() + Environment.NewLine + "來源資料:" + dataInfo.Data);
            }
        }

        //轉換比分
        private static bool ParserScore(dynamic scoreObj, out byte ScoreA, out byte ScoreB)
        {
            ScoreA = ScoreB = 0;

            if (scoreObj == null)
            {
                return false;
            }
            else
            {
                return byte.TryParse(scoreObj.team1_value?.ToString(), out ScoreA) && byte.TryParse(scoreObj.team2_value?.ToString(), out ScoreB);
            }
        }

        //轉換比分
        private static bool ParserScore(object A, object B, out byte ScoreA, out byte ScoreB)
        {
            ScoreA = ScoreB = 0;
            return byte.TryParse(A?.ToString(), out ScoreA) && byte.TryParse(B?.ToString(), out ScoreB);
        }

        //取得賽事狀態
        private static int GetStatus(string current_game_state, string league)
        {
            if (string.IsNullOrEmpty(current_game_state)) { return 5; }

            switch (current_game_state.ToLower())
            {
                case "set1":
                    int status = (league.Contains("NCAA") && !league.Contains("Women")) ? 11 : 1;
                    return status;
                case "set2":
                    status = (league.Contains("NCAA") && !league.Contains("Women")) ? 12 : 2;
                    return status;
                case "set3": return 3;
                case "set4": return 4;
                case "set5": return 13;
                default: return 5;
            }
        }
    }
}
