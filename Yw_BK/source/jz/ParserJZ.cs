#region 功能修改描述與時間

/*
    描述：JZ解析
    日期：2019-06-25

    描述：調整有關Odds變數命名
    日期：2022-02-11

    描述：添加大格解析
    日期：2022-02-16

    描述：添加組別解析
    日期：2022-06-02

    描述：調整單隊玩法匹配邏輯
    日期：2023-02-13

    描述：調整隊伍名稱解析
    日期：2023-03-13
*/

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;
using YwCore.main;

namespace Yw_BK
{
    /// <summary>
    /// JZ數據解析
    /// </summary>
    public class ParserJZ
    {
        //記錄前次判斷到需要重新獲取首包的時間
        private static DateTime dateTime = new DateTime();
        //篩選[國家]正則
        private static Regex countryRegex = new Regex("\\[.*\\]");
        //篩選數字
        private static Regex numRegex = new Regex("([0-9]+)");
        //雷達二進制開關列舉
        private enum RteEnum
        {
            A = 0b0001,//開資訊
            B = 0b0010,//開動畫
            C = 0b0011 //都開
        }

        /// <summary>
        /// 數據解析
        /// </summary>
        /// <param name="dataInfo">來源資料</param>
        public static void Parser(DataInfo dataInfo)
        {
            //暫存賽事字典
            ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
            //暫存轉播開關字典
            ConcurrentDictionary<string, int> tmpMultiStatusDic = new ConcurrentDictionary<string, int>();
            //中繼解析
            ReturnData returnData = dataInfo.IsAyh ?
                YwCore.Common.formatDataJZv2Ayh.DataToModel(dataInfo.Data, FormatDataJZv2.RelationType.Relation) :
                YwCore.Common.formatDataJZv2.DataToModel(dataInfo.Data, FormatDataJZv2.RelationType.Relation);
            //存放已處理之odds.Id+聯盟     
            HashSet<Tuple<long, string>> playHashSet = new HashSet<Tuple<long, string>>();
            //是否觸發重新訂閱
            bool isResubscribe = false;

            foreach (Data data in returnData.datas)
            {
                string[] fullLeague = new string[7];
                string[] tmpLeague = data.anqc.Split(new string[] { "||" }, StringSplitOptions.None);

                for (int i = 0; i < tmpLeague.Length; i++)
                {
                    fullLeague[i] = tmpLeague[i].Trim();
                }

                //兼容版, JZ只有5語系則NBB印語放英文
                fullLeague[5] = tmpLeague.Length == 5 ? tmpLeague[2] : tmpLeague[5];
                fullLeague[6] = tmpLeague[0];
                //聯盟簡體名稱
                string league = fullLeague[1];
                //簡稱切割-
                string[] partLeague = league.Split('-');
                var games = dataInfo.IsAyh ? data.game.Where(a => YwCore.Common.formatDataJZv2Ayh.JzCountryTypes.Contains(a.ct)) : data.game;

                string leagueName = GetSuffix(league, out string suffix);

                //判斷乾淨簡體名稱有無-
                string tmp = league.Replace(leagueName, "");
                //JZ全語系聯盟乾淨名稱
                string[] fullNameJZ = fullLeague;

                if (tmp.Contains("-"))
                {
                    fullNameJZ = fullLeague.Select(a => a.Remove(a.IndexOf('-'))).ToArray();
                }

                foreach (Game game in games)
                {
                    GameInfo gameInfo = new GameInfo(game.gid);
                    gameInfo.League.Name = leagueName;
                    gameInfo.Suffix.Name = suffix;
                    gameInfo.League.FullNameJZ = fullNameJZ;
                    //匹配3x3籃球
                    bool is3x3 = Config.Is3x3(leagueName);

                    CommonLogic.GetClearFull(game.ta.Split(new string[] { "||" }, StringSplitOptions.None), out string[] teamAFull, out string[] teamA2Full);
                    CommonLogic.GetClearFull(game.tb.Split(new string[] { "||" }, StringSplitOptions.None), out string[] teamBFull, out string[] teamB2Full);

                    gameInfo.TeamA.Name = teamAFull[1];
                    gameInfo.TeamB.Name = teamBFull[1];

                    gameInfo.TeamA.FullNameJZ = teamAFull;
                    gameInfo.TeamB.FullNameJZ = teamBFull;

                    switch (game.zc)
                    {
                        case 0: //B隊
                            gameInfo.Home = 2;
                            break;
                        case 1: //A隊
                            gameInfo.Home = 1;
                            break;
                        case 2: //中立
                            gameInfo.Home = 0;
                            break;
                    }

                    #region 設定賽事基本狀態

                    //帳務日不跟JZ，一樣由比賽日期推算
                    gameInfo.SetGameDate(Convert.ToDateTime(game.gd));

                    switch (game.kzdp)
                    {
                        case 0:
                            gameInfo.GType = (byte)GType.BkDs;
                            gameInfo.GpType = GpType.Ds;
                            break;
                        case 1:
                            gameInfo.GType = (byte)GType.BkDs;
                            gameInfo.GpType = GpType.Ds;
                            break;
                        case 2:
                            gameInfo.GType = (byte)GType.BkZd;
                            gameInfo.GpType = GpType.Zd;
                            gameInfo.Status = 5;
                            break;
                        default:
                            continue;
                    }

                    #endregion

                    string teamA = gameInfo.TeamA.Name;
                    string teamB = gameInfo.TeamB.Name;
                    string key = "";
                    bool isTeamA = false;
                    //先找暫存字典，沒有再找總字典
                    //單隊玩法找主盤邏輯
                    if (teamA.Equals(teamB))
                    {
                        //欲查找對應之主客隊標記
                        int tmpHome = 0;

                        if (league.Contains("主队总得分"))
                        {
                            tmpHome = 1;
                        }
                        else if (league.Contains("客队总得分"))
                        {
                            tmpHome = 2;
                        }

                        //取得匹配主盤賽事的key
                        string[] tmpKey = gameInfo.GetPairKey();

                        gameInfo = tmpGameDic.Values.FirstOrDefault(a => a.GetPairKey(tmpHome).Contains(tmpKey[0]));

                        if (gameInfo != null)
                        {
                            //單隊是否為A隊
                            isTeamA = teamA.Equals(gameInfo.TeamA.SourceName) ? true : false;
                        }
                        else
                        {
                            gameInfo = Config.gameDic.Values.FirstOrDefault(a => a.GetPairKey(tmpHome).Contains(tmpKey[0]))?.DeepClone();

                            if (gameInfo != null)
                            {
                                //單隊是否為A隊
                                isTeamA = teamA.Equals(gameInfo.TeamA.SourceName) ? true : false;
                                gameInfo.ObjDic = new ConcurrentDictionary<decimal, GpdmInfo>();
                            }
                            else
                            {
                                continue;
                            }
                        }

                        key = gameInfo.GetGameDictKey();
                    }
                    else
                    {
                        key = gameInfo.GetGameDictKey();

                        //暫存字典有則取出接續解析
                        if (tmpGameDic.TryGetValue(key, out GameInfo outGameInfo))
                        {
                            gameInfo = outGameInfo;
                        }
                        else if (Config.gameDic.TryGetValue(key, out outGameInfo))
                        {
                            gameInfo = outGameInfo.DeepClone();
                            gameInfo.ObjDic = new ConcurrentDictionary<decimal, GpdmInfo>();

                            //雷達Id
                            gameInfo.MatchId = CombineMatchId(game.mid, game.rte.GetValueOrDefault(-1));
                            //開賽日期
                            gameInfo.SetGameDate(Convert.ToDateTime(game.gd));

                        }
                        else
                        {
                            //雷達Id
                            gameInfo.MatchId = CombineMatchId(game.mid, game.rte.GetValueOrDefault(-1));
                            //開賽日期
                            gameInfo.SetGameDate(Convert.ToDateTime(game.gd));
                        }

                        //轉播二進制開關(以轉播中斷為主)
                        int tmpMultiStatus = game.ssyc == 1 ? 16 : game.zbwxzd == 1 ? 2 : game.yyyc == 1 ? 1 : 0;

                        //有設置開關再加字典
                        if (tmpMultiStatus > 0)
                        {
                            tmpMultiStatusDic.TryAdd(gameInfo.GetGameDictKey(), tmpMultiStatus);
                        }

                        //直播數據不完整或轉播衛星中斷而清空ID
                        if (game.lvc == "" || game.lvt == "" || game.zbwxzd == 1)
                        {
                            if (game.odds.Any(a => gameInfo.LiveUrlDic.Keys.Contains(a.id.ToString())))
                            {
                                gameInfo.LiveUrlDic.Clear();
                                gameInfo.ChannelId = 0;
                            }
                        }
                        else
                        {
                            LiveUrlModel value = new LiveUrlModel() { gameId = game.zbwid, ball = data.bte, lvc = game.lvc, lvt = game.lvt, mbChannel = game.mbChannel, pcChannel = game.pcChannel };
                            gameInfo.LiveUrlDic.AddOrUpdate(game.zbwid, value, (k, v) => value);

                            #region 取得直播Id

                            NameInfo tmpStream = null;

                            switch (game.lvc)
                            {
                                case "ys2":

                                    if (game.lvt.Contains("因雨"))
                                    {
                                        tmpStream = StreamInfo.streamDic.FirstOrDefault(p => p.Value.Tw.Contains("因雨")).Value;
                                    }

                                    break;
                                case "zd2":

                                    if (game.lvt.Contains("衛星轉播"))
                                    {
                                        tmpStream = StreamInfo.streamDic.FirstOrDefault(p => p.Value.Tw.Contains("衛星轉播")).Value;
                                    }

                                    break;
                                default:

                                    if (game.lvc.StartsWith("R_"))
                                    {
                                        tmpStream = StreamInfo.streamDic.FirstOrDefault(p => p.Value.ChannelCode == "R_ra01").Value;
                                    }
                                    else
                                    {
                                        //智能雷達流也需要匹配到(EX:us01_ra01)                                        
                                        string tmpGameLvc = game.lvc.EndsWith("2") ? game.lvc.Remove(game.lvc.Length - 1).ToString() : game.lvc;
                                        string[] tmpChannel = tmpGameLvc.Split('_');
                                        tmpGameLvc = tmpChannel.Length == 2 ? $"{tmpChannel[0]}1_{tmpChannel[1]}" : $"{tmpChannel[0]}1";
                                        tmpStream = StreamInfo.streamDic.FirstOrDefault(p => p.Value.ChannelCode == tmpGameLvc).Value;
                                    }

                                    break;
                            }

                            //緩存不存在給0
                            if (tmpStream != null)
                            {
                                gameInfo.ChannelId = tmpStream.Id;
                            }
                            else
                            {
                                gameInfo.ChannelId = 0;
                            }

                            #endregion
                        }
                    }

                    #region 玩法PL解析

                    //GameInfo集合 [索引 0:預設]
                    List<GameInfo> gameList = new List<GameInfo>();
                    //複製Gaminfo用來解析Odds，僅用於解析Odds
                    GameInfo tmpGameinfo = gameInfo.Clone();
                    //裝進集合預設位置
                    gameList.Add(tmpGameinfo);
                    tmpGameinfo.ObjDic = new ConcurrentDictionary<decimal, GpdmInfo>();

                    foreach (var oddsGroup in game.odds.GroupBy(a => a.id))
                    {
                        Odds odds = oddsGroup.First();
                        //迴圈每次起始還原預設Gaminfo
                        tmpGameinfo = gameList[0];

                        #region 判斷odds層的後綴是否額外生出GameInfo

                        GetSuffix(odds.apn, out string tmpSuffix, true);

                        if (!string.IsNullOrEmpty(tmpSuffix) && gameInfo.Suffix.Name != tmpSuffix)
                        {
                            tmpGameinfo = tmpGameinfo.DeepClone();
                            tmpGameinfo.Suffix.Name = tmpSuffix;
                            tmpGameinfo.ObjDic = new ConcurrentDictionary<decimal, GpdmInfo>();
                            //從集合取出
                            GameInfo tmpGame = gameList.SingleOrDefault(p => p.GetGameDictKey() == tmpGameinfo.GetGameDictKey());

                            key = tmpGameinfo.GetGameDictKey();

                            //集合已存在則取出接續解析
                            if (tmpGame != null)
                            {
                                tmpGameinfo = tmpGame;
                            }
                            else
                            {
                                //裝進集合
                                gameList.Add(tmpGameinfo);
                            }
                        }

                        #endregion

                        //存放特殊玩法對應Specials
                        Dictionary<string, int> correspond = new Dictionary<string, int>();
                        //特殊玩法判斷
                        int specials = -1;
                        //分數或第N次標記
                        int num = 0;
                        //NBB夢幻配對不是特殊玩法
                        string oddsApn = odds.apn.Contains("梦幻配对") ? "" : odds.apn;

                        if (tmpGameinfo.GpType == GpType.Ds)
                        {
                            switch (odds.cc)
                            {
                                case 0:

                                    int sc = GetScene(oddsApn);

                                    if (Regex.IsMatch(oddsApn, "优先获得[0-9]*分"))
                                    {
                                        num = Convert.ToInt32(numRegex.Match(oddsApn).Result("$1"));

                                        switch (sc)
                                        {
                                            case 0: specials = 101; break;  //全場
                                            case 1: specials = 102; break;  //上半
                                            case 2: specials = 103; break;  //下半
                                            case 11: specials = 104; break; //第一節
                                            case 12: specials = 105; break; //第二節
                                            case 13: specials = 106; break; //第三節
                                            default: specials = 107; break; //第四節
                                        }
                                    }
                                    else
                                    {
                                        num = GetNumber(oddsApn);

                                        correspond.Add("总得分", isTeamA ? 14 : 15);
                                        correspond.Add("获得两分球", 130);
                                        correspond.Add("犯规", 151);
                                        correspond.Add("首先获得篮板球", 40);
                                        correspond.Add("首次暂停", 41);
                                        correspond.Add("获得三分球", sc != 1 && sc != 2 && sc != 11 ? (sc == 0 ? 260 : sc == 12 ? 270 : sc == 13 ? 280 : 290) + num : -1);
                                        correspond.Add("获得罚球", sc != 1 && sc != 2 && sc != 11 ? (sc == 0 ? 160 : sc == 12 ? 170 : sc == 13 ? 180 : 190) + num : -1);
                                        correspond.Add("优先得分", sc == 2 ? 111 : sc == 12 ? 112 : sc == 14 ? 113 : -1);
                                        correspond.Add("最后得分", sc == 1 ? 121 : sc == 11 ? 122 : sc == 13 ? 123 : -1);
                                        correspond.Add("三分球总数", 13);
                                        correspond.Add("助攻总数", 31);
                                        correspond.Add("篮板球总数", 32);
                                        correspond.Add("抢断总数", 33);
                                        correspond.Add("盖帽总数", 34);
                                        correspond.Add("罚球得分总数", 35);
                                        correspond.Add("总失误", 36);

                                        specials = GetSpecials(0, oddsApn, correspond, tmpGameinfo.Suffix.Name);
                                    }

                                    break;
                                case 1:

                                    correspond.Add("总得分", isTeamA ? 16 : 17);
                                    specials = GetSpecials(1, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                case 2:

                                    correspond.Add("总得分", isTeamA ? 18 : 19);
                                    specials = GetSpecials(2, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                case 8:
                                    //玩法唯一key
                                    if (playHashSet.Add(Tuple.Create(odds.id, data.anqc)))
                                    {
                                        //處理群組集合內odds
                                        foreach (var subOdds in oddsGroup)
                                        {
                                            switch (subOdds.play)
                                            {
                                                case "Q1":
                                                    specials = 60;
                                                    break;
                                                case "Q2":
                                                    specials = 70;
                                                    break;
                                                case "Q3":
                                                    specials = 80;
                                                    break;
                                                case "SY":
                                                    continue;
                                            }

                                            //更新建立Kinfo
                                            SetKinfo(tmpGameinfo, specials, subOdds.play, (byte)num, subOdds);
                                        }
                                    }
                                    else//相同id則設為需要重新訂閱
                                    {
                                        isResubscribe = true;
                                    }

                                    continue;
                                case 11:

                                    correspond.Add("总得分", isTeamA ? 20 : 21);
                                    specials = GetSpecials(3, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                case 12:

                                    correspond.Add("总得分", isTeamA ? 22 : 23);
                                    specials = GetSpecials(4, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                case 13:

                                    correspond.Add("总得分", isTeamA ? 24 : 25);
                                    specials = GetSpecials(5, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                case 14:

                                    correspond.Add("总得分", isTeamA ? 26 : 27);
                                    specials = GetSpecials(6, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                default:
                                    break;
                            }

                            //特殊玩法為-1不解析(蒐集玩法名)
                            if (specials == -1)
                            {
                                LogFile.SetLog(LogMode.Remind, string.Format("{0}:{1},apn:{2},cc:{3}", tmpGameinfo.GpType, data.anqc, odds.apn, odds.cc));
                                continue;
                            }
                        }
                        else
                        {
                            switch (odds.cc)
                            {
                                case 0:

                                    if (oddsApn.Contains("总得分"))
                                    {
                                        correspond.Add("第一节", isTeamA ? 20 : 21);
                                        correspond.Add("第二节", isTeamA ? 22 : 23);
                                        correspond.Add("第三节", isTeamA ? 24 : 25);
                                        correspond.Add("第四节", isTeamA ? 26 : 27);

                                        specials = GetSpecials(0, oddsApn, correspond, tmpGameinfo.Suffix.Name);
                                        specials = specials > 0 ? specials : isTeamA ? 14 : 15;
                                    }
                                    else
                                    {
                                        correspond.Add("第一节", 3);
                                        correspond.Add("第二节", 4);
                                        correspond.Add("第三节", 5);
                                        correspond.Add("第四节", 6);
                                        correspond.Add("三分球总数", 13);
                                        specials = GetSpecials(0, oddsApn, correspond, tmpGameinfo.Suffix.Name);
                                    }

                                    break;
                                case 1:
                                    correspond.Add("总得分", isTeamA ? 16 : 17);
                                    specials = GetSpecials(1, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                case 2:

                                    correspond.Add("总得分", isTeamA ? 18 : 19);
                                    specials = GetSpecials(2, oddsApn, correspond, tmpGameinfo.Suffix.Name);

                                    break;
                                default:
                                    break;

                            }

                            //ZD不需紀錄
                            if (specials == -1)
                            {
                                continue;
                            }
                        }

                        //玩法唯一key
                        var playKey = Tuple.Create(odds.id, data.anqc);

                        if (playHashSet.Add(playKey))
                        {
                            //處理群組集合內odds
                            foreach (var subOdds in oddsGroup)
                            {
                                //更新建立Kinfo
                                SetKinfo(tmpGameinfo, specials, subOdds.play, (byte)num, subOdds);
                            }
                        }
                        else//相同id則設為需要重新訂閱
                        {
                            isResubscribe = true;
                        }

                        //首次先初始建立其他基本KTYPE，跳過已刪除的
                        if (!odds.isRemove)
                        {
                            //是否為主盤
                            bool isMaster = game.odds.First(a => a.play == odds.play).id == odds.id;
                            //取得SPECIALS對應的KTYPE群
                            string[] play = Config.GetKinfoArray(tmpGameinfo.GpType, specials, isMaster);
                            //篩選來源資料中沒出現的Play
                            var defaultPlay = play.Except(oddsGroup.Select(a => a.play));

                            foreach (string playType in defaultPlay)
                            {
                                SetKinfo(tmpGameinfo, specials, playType);
                            }
                        }
                    }

                    #endregion

                    foreach (GameInfo tmpGame in gameList)
                    {
                        GameInfo tmpInfo = tmpGame;

                        //key不同不比對、單隊玩法賽事不解析賽事資訊
                        if (gameInfo.GetGameDictKey() == tmpGame.GetGameDictKey() && !teamA.Equals(teamB))
                        {
                            //比較原賽事資訊
                            gameInfo.Compare(tmpGame, gameInfo.GpType);
                            tmpInfo = gameInfo;

                            #region 設定比分

                            //狀態
                            gameInfo.Status = GetStatus(game.sta, gameInfo.GpType);

                            if (!string.IsNullOrEmpty(game.sta))
                            {
                                //全場分數                               
                                gameInfo.Score.SetScore(byte.TryParse(game.sca, out byte sca) & byte.TryParse(game.scb, out byte scb), sca, scb);

                                string[] rsa = string.IsNullOrEmpty(game.rsa) ? new string[0] : game.rsa.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                string[] rsb = string.IsNullOrEmpty(game.rsb) ? new string[0] : game.rsb.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                                if (!gameInfo.League.Name.Contains("NCAA"))
                                {
                                    //單節比分
                                    for (int i = 0; i < rsa.Length && i < rsb.Length; i++)
                                    {
                                        bool isOpen = byte.TryParse(rsa[i], out byte scoreA) & byte.TryParse(rsb[i], out byte scoreB);

                                        switch (i)
                                        {
                                            case 0:
                                                gameInfo.Score1.SetScore(isOpen, scoreA, scoreB);
                                                break;
                                            case 1:
                                                gameInfo.Score2.SetScore(isOpen, scoreA, scoreB);
                                                break;
                                            case 2:
                                                gameInfo.Score3.SetScore(isOpen, scoreA, scoreB);
                                                break;
                                            case 3:
                                                gameInfo.Score4.SetScore(isOpen, scoreA, scoreB);
                                                break;
                                        }
                                    }

                                    //上下半比分

                                    if (byte.TryParse((gameInfo.Score1.GetScoreA() + gameInfo.Score2.GetScoreA()).ToString(), out byte scoreHalf1A) &&
                                        byte.TryParse((gameInfo.Score1.GetScoreB() + gameInfo.Score2.GetScoreB()).ToString(), out byte scoreHalf1B) &&
                                        byte.TryParse((gameInfo.Score3.GetScoreA() + gameInfo.Score4.GetScoreA()).ToString(), out byte scoreHalf2A) &&
                                        byte.TryParse((gameInfo.Score3.GetScoreB() + gameInfo.Score4.GetScoreB()).ToString(), out byte scoreHalf2B))
                                    {
                                        gameInfo.ScoreHalf1.SetScore(gameInfo.Score1.GetIsOpen(), scoreHalf1A, scoreHalf1B);
                                        gameInfo.ScoreHalf2.SetScore(gameInfo.Score3.GetIsOpen(), scoreHalf2A, scoreHalf2B);
                                    }
                                }
                                else
                                {
                                    //上下半比分
                                    for (int i = 0; i < rsa.Length && i < rsb.Length; i++)
                                    {
                                        bool isOpen = byte.TryParse(rsa[i], out byte scoreA) & byte.TryParse(rsb[i], out byte scoreB);

                                        switch (i)
                                        {
                                            case 0:
                                                gameInfo.ScoreHalf1.SetScore(isOpen, scoreA, scoreB);
                                                break;
                                            case 1:
                                                gameInfo.ScoreHalf2.SetScore(isOpen, scoreA, scoreB);
                                                break;
                                        }
                                    }
                                }

                                //剩餘秒數
                                if (TimeSpan.TryParse("00:" + game.rmt, out TimeSpan time))
                                {
                                    gameInfo.LastSec = Convert.ToInt32(time.TotalSeconds);
                                }
                                else
                                {
                                    gameInfo.LastSec = 0;
                                }
                            }
                            else
                            {
                                gameInfo.Score.SetIsOpen(false);
                                gameInfo.Score1.SetIsOpen(false);
                                gameInfo.Score2.SetIsOpen(false);
                                gameInfo.Score3.SetIsOpen(false);
                                gameInfo.Score4.SetIsOpen(false);
                                gameInfo.ScoreHalf1.SetIsOpen(false);
                                gameInfo.ScoreHalf2.SetIsOpen(false);
                                gameInfo.LastSec = 0;
                            }

                            #endregion
                        }

                        //更新賽事資訊
                        tmpGameDic.AddOrUpdate(tmpGame.GetGameDictKey(), tmpInfo, (k, v) =>
                        {
                            //僅需添加遺失的部分
                            foreach (var obj in tmpGame.ObjDic)
                            {
                                v.ObjDic.AddOrUpdate(obj.Key, obj.Value, (ok, ov) => obj.Value);
                            }

                            return v;
                        });
                    }
                }
            }

            //字典資訊最後完整更新
            foreach (GameInfo gInfo in tmpGameDic.Values)
            {
                string key = gInfo.GetGameDictKey();

                //設置轉播二進制開關
                if (tmpMultiStatusDic.TryGetValue(key, out int multiStatus))
                {
                    gInfo.MultiStatus = multiStatus;
                }
                else
                {
                    gInfo.MultiStatus = 0;
                }

                //因應來源名稱替換功能，需要抓出修改前的賽事進行關閉
                foreach (GameInfo game in Config.gameDic.Values.Where(a => a.GetSourceGameDictKey() == gInfo.GetSourceGameDictKey() && a.GetGameDictKey() != key))
                {
                    game.DataId = -1;
                }

                //存在才處理
                if (Config.gameDic.TryGetValue(key, out GameInfo getGame))
                {
                    //移除不見的GPDM(鴛鴦盤遞補)
                    var exceptGpdm = getGame.ObjDic.Keys.Except(gInfo.ObjDic.Keys);
                    var removeKinfo = getGame.ObjDic.Join(exceptGpdm, a => a.Key, b => b, (a, b) => a.Value.Values);
                    removeKinfo.SelectMany(a => a).ToList().ForEach(a => a.DataId = -1);

                    //比對更新
                    getGame.Compare(gInfo, gInfo.GpType);

                    //僅首包、刪除動作才需判斷玩法存在與否
                    if (returnData.actionType == ActionType.First || returnData.actionType == ActionType.Delete)
                    {
                        if (!getGame.ObjDic.Values.SelectMany(a => a.Values).Any(a => a.DataId >= 0))
                        {
                            getGame.DataId = -1;
                        }
                    }
                }
                //不存在直接添加
                else
                {
                    //刪除包動作或無GPDM不需再添加
                    if (returnData.actionType != ActionType.Delete && gInfo.ObjDic.Count != 0)
                    {
                        Config.gameDic.TryAdd(key, gInfo);
                    }
                }
            }

            //刪除、重訂指令進行重訂
            if ((returnData.actionType == ActionType.Delete && isResubscribe) || returnData.actionType == ActionType.ReSubscribe)
            {
                //取當下時間
                DateTime now = DateTime.Now;

                if (now.Subtract(dateTime).TotalSeconds >= 4)
                {
                    //設定最後判斷到的時間
                    dateTime = now;
                    //觸發Config異動事件
                    System.Threading.Tasks.Task.Run(async () => { await System.Threading.Tasks.Task.Delay(5000); Config.ConfigChange(); });
                }
            }
        }

        //JZ賽事狀態轉NBB賽事狀態
        private static int GetStatus(string status, GpType gpType)
        {
            switch (status)
            {
                case "21": return 1;    //第1節
                case "22": return 2;    //第2節
                case "23": return 3;    //第3節
                case "24": return 4;    //第4節
                case "11": return 11;   //上半
                case "12": return 12;   //下半
                case "1": return 6;     //中場
                case "2": return 7;     //中斷
                default:
                    if (gpType == GpType.Zd)
                    {
                        return 5;      //滾球中
                    }
                    else
                    {
                        return 0;
                    }
            }
        }

        /// <summary>
        /// 後綴處理
        /// </summary>
        /// <param name="league">來源聯盟完整名稱</param>
        /// <param name="suffix">分割後後綴</param>
        /// <param name="isOddsApn">是否為odds層解析</param>
        /// <returns></returns>
        private static string GetSuffix(string league, out string suffix, bool isOddsApn = false)
        {
            //防止傳進來是全語系，需再切出簡體語系
            if (league.Contains("||"))
            {
                string[] tmpSplit = league.Split(new string[] { "||" }, StringSplitOptions.None);
                league = tmpSplit.Length > 1 ? tmpSplit[1] : league;
            }

            string leagueReg = @"(总冠军赛|热身赛|季后赛|[1-9].*分钟)";
            Match match = Regex.Match(league, isOddsApn ? leagueReg : "-" + leagueReg);
            Match minorMatch = Regex.Match(league, @"球员|梦幻配对");
            suffix = "";
            string[] leagueSplit = league.Split('-');

            if (match.Success)
            {
                suffix = match.Result("$1").Trim();
            }

            if (leagueSplit.Length > 1 && minorMatch.Success)
            {
                //固定取最後一個拼接後綴
                if (string.IsNullOrEmpty(suffix))
                {
                    suffix = leagueSplit[leagueSplit.Length - 1].Trim();
                }
                else
                {
                    suffix += "-" + leagueSplit[leagueSplit.Length - 1].Trim();
                }
            }
            else
            {
                Match otherMatch = Regex.Match(league, "(扣篮大赛|三分球大赛|技巧挑战赛).*");

                if (otherMatch.Success)
                {
                    suffix = string.IsNullOrEmpty(suffix) ? otherMatch.Value.Trim() : suffix + "-" + otherMatch.Value.Trim();
                }
            }

            if (league.IndexOf('-') >= 0)
            {
                return league.Remove(league.IndexOf('-'));
            }
            else
            {
                return league;
            }
        }

        /// <summary>
        /// 設置Kinfo
        /// </summary>
        /// <param name="gameInfo">賽事</param>
        /// <param name="specials">標題分類編號</param>
        /// <param name="play">小格項目</param>
        /// <param name="setType">預設SetType</param>
        /// <param name="odds">來源Odds資料</param>
        private static void SetKinfo(GameInfo gameInfo, int specials, string play, byte setType = 0, Odds odds = null)
        {
            int kType = 0;              //kTYPE
            int strong = 0;             //RT[1:A隊讓 0:B隊讓]
            decimal plate = 0;          //PK
            decimal plA = 0;            //A_PL
            decimal plB = 0;            //B_PL
            decimal gpdm = specials;    //GPDM
            bool isOpen = false;        //玩法開關                                        
            bool is3x3 = Config.Is3x3(gameInfo.League.Name); //是否為3x3籃球 (三對三ZD不開OE)
            //生成物件
            KInfo kInfo = new KInfo("", 0, 0);
            BInfo bInfoA = new BInfo(0);
            BInfo bInfoB = new BInfo(0);

            if (odds != null)
            {
                if (odds.rfpk != null)
                {
                    plate = FormatDataJZv2.PlateConvert(odds.rfpk);
                }

                if (odds.rf3.HasValue)
                {
                    strong = odds.rf3.Value == 0 ? 1 : 0;
                }

                decimal.TryParse(odds.a, out plA);
                decimal.TryParse(odds.b, out plB);

                //PL為負代表關閉玩法
                isOpen = plA > 0 && plB > 0 && odds.l.GetValueOrDefault(0) == 1;
                plA = plA > 0 ? plA : 0;
                plB = plB > 0 ? plB : 0;
            }

            switch (play)
            {
                case "RF":
                case "Q1":  //搶首
                case "Q2":  //搶尾
                case "Q3":  //單節最高

                    //全場RF
                    if (Config.SetKindType(gameInfo.GpType, specials, 1, out kType))
                    {
                        setType = setType == 0 ? odds != null ? gameInfo.GetSetType(kType) : (byte)1 : setType;
                        gpdm = gpdm * 100 + setType;
                        //生成物件
                        kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;

                        bInfoA = new BInfo(Config.GetBType(kType, setType, 1));
                        bInfoB = new BInfo(Config.GetBType(kType, setType, 2));

                        //設置數據
                        kInfo.SetLine(plate, strong);
                        bInfoA.SetOdds(plA);
                        bInfoB.SetOdds(plB);
                        kInfo.SetIsOpen(isOpen);
                    }
                    else
                    {
                        return;
                    }

                    break;
                case "DX":

                    //全場OU
                    if (Config.SetKindType(gameInfo.GpType, specials, 2, out kType))
                    {
                        setType = setType == 0 ? odds != null ? gameInfo.GetSetType(kType) : (byte)1 : setType;
                        gpdm = gpdm * 100 + setType;
                        //生成物件
                        kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                        bInfoA = new BInfo(Config.GetBType(kType, setType, 1));
                        bInfoB = new BInfo(Config.GetBType(kType, setType, 2));

                        //設置數據
                        kInfo.SetLine(plate);
                        bInfoA.SetOdds(plA);
                        bInfoB.SetOdds(plB);
                        kInfo.SetIsOpen(isOpen);
                    }
                    else
                    {
                        return;
                    }

                    break;
                case "DY":

                    //全場1*2
                    if (Config.SetKindType(gameInfo.GpType, specials, 3, out kType))
                    {
                        setType = setType == 0 ? odds != null ? gameInfo.GetSetType(kType) : (byte)1 : setType;
                        gpdm = gpdm * 100 + setType;

                        //限定setType只能有一個
                        if (setType == 1)
                        {
                            //生成物件
                            kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
                            bInfoA = new BInfo(Config.GetBType(kType, setType, 1));
                            bInfoB = new BInfo(Config.GetBType(kType, setType, 2));

                            //設置數據
                            kInfo.SetLine(plate, strong);
                            bInfoA.SetOdds(plA);
                            bInfoB.SetOdds(plB);
                            kInfo.SetIsOpen(isOpen);
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }

                    break;
                case "DS":

                    //OE
                    if (Config.SetKindType(gameInfo.GpType, specials, 4, out kType))
                    {
                        setType = setType == 0 ? odds != null ? gameInfo.GetSetType(kType) : (byte)1 : setType;
                        gpdm = gpdm * 100 + setType;

                        //限定setType只能有一個
                        if (setType == 1)
                        {
                            //生成物件
                            kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
                            bInfoA = new BInfo(Config.GetBType(kType, setType, 1));
                            bInfoB = new BInfo(Config.GetBType(kType, setType, 2));
                            decimal odds_O = 0;
                            decimal odds_E = 0;

                            //手動設置OE
                            if (gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe)
                            {
                                odds_O = odds_E = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                kInfo.IsManual = true;
                                isOpen = true;
                            }
                            //(排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                            else if (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe && !is3x3 && (specials < 14 || specials > 29))
                            {
                                odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                kInfo.IsManual = true;
                                isOpen = true;
                            }
                            //跟來源OE (三對三ZD不開OE)
                            else if (!(gameInfo.GpType == GpType.Zd && is3x3))
                            {
                                //PL
                                odds_O = plA;
                                odds_E = plB;
                                kInfo.IsManual = false;
                            }

                            //設置數據
                            bInfoA.SetOdds(odds_O);
                            bInfoB.SetOdds(odds_E);
                            kInfo.SetIsOpen(isOpen);
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }

                    break;
                default:
                    return;
            }

            //是否已刪除
            if (odds != null && odds.isRemove)
            {
                kInfo.DataId = -1;
                kInfo.IsUpdate = true;
            }
            else if (odds != null && !odds.isRemove)
            {
                kInfo.DataId = 0;
            }

            //放進字典
            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
            //JZ大格開關轉換成NBB顯示開關(沒有大格預設開，兼容舊版大隔開才會給數據)
            bool nbbDisplay = odds != null ? odds.sdbs.GetValueOrDefault(1) > 0 : true;
            GpdmInfo gpdmInfo = gameInfo.ObjDic.GetOrAdd(gpdm, new GpdmInfo(key: gpdm, display: nbbDisplay));
            //TS來源ID，-1代表無來源
            gpdmInfo.TsSource = odds != null ? TsGpbjToNBBSource(odds.gpbj.GetValueOrDefault(-1)) : Source.None;

            //分組，-1代表空值
            if (odds != null && odds.zqgrp.GetValueOrDefault(-1) > 0)
            {
                gpdmInfo.Group = odds.zqgrp;
            }

            if (gpdmInfo.Display == false)
            {
                kInfo.SetIsOpen(gpdmInfo.Display);
            }

            gpdmInfo.AddOrUpdate(kType, kInfo, (k, v) => kInfo);
        }

        //取得第N次數字
        private static int GetNumber(string name)
        {
            int num = 0;

            if (name.Contains("首先") || name.Contains("第一次") || name.Contains("优先") || name.Contains("首次"))
            {
                num = 1;
            }
            else if (name.Contains("第二次"))
            {
                num = 2;
            }
            else if (name.Contains("第三次"))
            {
                num = 3;
            }
            else if (name.Contains("第四次"))
            {
                num = 4;
            }
            else if (name.Contains("第五次"))
            {
                num = 5;
            }
            else if (name.Contains("最后"))
            {
                num = 9;
            }

            return num;
        }

        //取得第N節數字
        private static int GetScene(string name)
        {
            int num = 0;

            if (name.Contains("上半"))
            {
                num = 1;
            }
            else if (name.Contains("下半"))
            {
                num = 2;
            }
            else if (name.Contains("第一节"))
            {
                num = 11;
            }
            else if (name.Contains("第二节"))
            {
                num = 12;
            }
            else if (name.Contains("第三节"))
            {
                num = 13;
            }
            else if (name.Contains("第四节"))
            {
                num = 14;
            }

            return num;
        }

        /// <summary>
        /// 整理玩法對應的Specials
        /// </summary>
        /// <param name="defaultValue">基本預設值</param>
        /// <param name="apn">特殊玩法名稱</param>
        /// <param name="correspond">名稱對應Specials</param>
        /// <param name="suffix">後綴名稱</param>
        /// <returns></returns>
        private static int GetSpecials(int defaultValue, string apn, Dictionary<string, int> correspond, string suffix)
        {
            if (!string.IsNullOrEmpty(apn) && !string.IsNullOrEmpty(suffix))
            {
                apn = apn.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries)[1];
                apn = apn.Replace(suffix, "");
            }

            var obj = correspond.Where(a => apn.Contains(a.Key));

            if (obj.Any())
            {
                return obj.FirstOrDefault().Value;
            }
            else if (!string.IsNullOrEmpty(apn))
            {
                return -1;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 組合雷達ID完整字段
        /// </summary>
        /// <param name="mid">雷達ID</param>
        /// <param name="rte">雷達二進制開關</param>
        /// <returns></returns>
        private static string CombineMatchId(string mid, int rte)
        {
            if (rte >= 0 && !string.IsNullOrEmpty(mid))
            {
                //1:A 開資訊，2:B 開動畫，3:C 都開
                string result = Enum.GetName(typeof(RteEnum), rte & 3);

                return $"{mid}{result}";
            }
            else
            {
                return mid;
            }
        }

        /// <summary>
        /// TSGpbj轉NBB來源
        /// </summary>
        /// <param name="sourceId">TSGpbj</param>
        /// <returns></returns>
        private static Source TsGpbjToNBBSource(int sourceId)
        {
            Source source = Source.None;

            switch (sourceId)
            {
                case 1: source = Source.HG; break;
                case 2: source = Source.CMD; break;
                case 3: source = Source.PIN; break;
                case 5: source = Source.SB; break;
                case 6: source = Source.BET365; break;
                case 12: source = Source.XB; break;
                case 13: source = Source.VBET; break;
                case 16: source = Source.SBO; break;
                case 17: source = Source.SPB; break;
                case 18: source = Source.LIVEBET; break;
                case 19: source = Source.MyStake; break;
            }

            return source;
        }
    }
}
