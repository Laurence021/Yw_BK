#region 功能修改描述與時間

/*
    描述：PIN解析器
    日期：2019-01-09
            
    描述：排除 ZD 全場/上下半/單節 主客隊總得分 oe設置、調整GameInfo.gType判斷
    日期：2020-08-13
                    
    描述：調整ZD開賽時間有查詢過才使用
    日期：2021-06-29
                    
    描述：添加解析第二節單隊總得分
    日期：2023-02-15
                        
    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16
*/

#endregion

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;
using System.Linq;

namespace Yw_BK.source
{
    /// <summary>
    /// PIN解析器
    /// </summary>
    public class ParserPIN
    {
        public static string codeJson = string.Empty;

        /// <summary>
        /// PIN解析
        /// </summary>
        /// <param name="html">來源數據</param>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">GP類型</param>
        /// <returns></returns>
        public static void Parser(string html, long sendId, GpType gpType)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //Jason
                JObject obj = JObject.Parse(html.Replace(@"\r\n", ""));

                //來源內容防呆
                if (string.IsNullOrEmpty(obj["Sport"].ToString()))
                {
                    return;
                }

                //特殊玩法
                int specials = -1;
                //是否單隊GpType
                bool isTeamTotal = false;

                switch (gpType)
                {
                    case GpType.Ds:
                    case GpType.Zd:
                    case GpType.Zp:
                        specials = 0;
                        break;
                    case GpType.Xb: specials = 2; break;
                    case GpType.Dj: specials = 3; break;
                    case GpType.DjQ2: specials = 4; break;
                    case GpType.Ac: specials = 14; isTeamTotal = true; break;
                    case GpType.AcHT: specials = 16; isTeamTotal = true; break;
                    case GpType.AcQ1: specials = 20; isTeamTotal = true; break;
                    case GpType.AcQ2: specials = 22; isTeamTotal = true; break;
                }

                JArray datas = (JArray)obj["Sport"]["Markets"];

                for (int i = 0; i < datas.Count; i++)
                {
                    //判斷賽事類型
                    string type = gpType == GpType.Ds ? "today" : gpType == GpType.Zd ? "live" : gpType == GpType.Zp ? "early markets" : "";
                    string marketName = datas[i]["MarketName"].ToString().ToLower();

                    //(Dj、DjQ2、Xb、Ac..)執行緒直接處理
                    if (marketName.Contains(type) || gpType == GpType.Xb || gpType == GpType.Dj || gpType == GpType.DjQ2 || isTeamTotal)
                    {
                        foreach (JToken gamesContainers in datas[i]["GamesContainers"].Children())
                        {
                            //數據類別化
                            PINDataFormat.MatchDetail matchData = (PINDataFormat.MatchDetail)JsonConvert.DeserializeObject(gamesContainers.Last.ToString(), typeof(PINDataFormat.MatchDetail));

                            //新增取得鴛鴦盤處理
                            JProperty data = (JProperty)gamesContainers;
                            string code = data.Name.Substring(data.Name.LastIndexOf("_") + 1) + ":3";

                            if (!codeJson.Contains(code))
                            {
                                codeJson += code + ",";
                            }

                            var gameLines = matchData.GameLines.Select(a => new { Lvl = Convert.ToInt32(a.Lvl) + 5, Value = a });
                            //鴛鴦盤排序
                            var sort = gameLines.OrderBy(a => GetSortIndex(a.Lvl, 5));

                            foreach (var gameLineObj in sort)
                            {
                                var gameLine = gameLineObj.Value;
                                GameInfo gameInfo = new GameInfo(gameLine.EvId.ToString(), sendId);

                                int lvl = gameLineObj.Lvl;
                                bool haveHt = false;

                                //設定賽事ID
                                if (gpType == GpType.Dj)
                                {
                                    if (!gameLine.TmH.Pd.Contains("1st Q"))
                                    {
                                        continue;
                                    }
                                }
                                else if (gpType == GpType.DjQ2)
                                {
                                    if (!gameLine.TmH.Pd.Contains("2nd Q"))
                                    {
                                        continue;
                                    }
                                }
                                else if (gpType == GpType.Xb)
                                {
                                    if (!gameLine.TmH.Pd.Contains("2nd H"))
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    haveHt = true;
                                }

                                //限制FT與Dj鴛鴦盤抓取數量
                                if ((Config.sameCount == 1 && !(lvl == 5)) ||
                                    (Config.sameCount == 2 && !(lvl == 4 || lvl == 5 || lvl == 6)) ||
                                    (Config.sameCount == 3 && !(lvl == 3 || lvl == 4 || lvl == 5 || lvl == 6 || lvl == 7)) ||
                                    (!Config.isSbDjSame && (gpType == GpType.Dj || gpType == GpType.DjQ2) && lvl != 5))
                                {
                                    continue;
                                }

                                //[Dj,DjQ2,Xb,Ac..] 額外區分GpType
                                if (gpType == GpType.Xb || gpType == GpType.Dj || gpType == GpType.DjQ2 || isTeamTotal)
                                {
                                    gameInfo.GpType = marketName.Contains("today") ? GpType.Ds : marketName.Contains("live") ? GpType.Zd : GpType.Zp;
                                }
                                else
                                {
                                    gameInfo.GpType = gpType;
                                }

                                bool isNBA = matchData.DisplayName.Contains("NBA") && !matchData.DisplayName.Contains("WNBA");

                                //NBA ZD目前只跟全場球隊總得分
                                if (gameInfo.GpType == GpType.Zd)
                                {
                                    bool excludeAc = isNBA ? gpType != GpType.Ac : isTeamTotal;

                                    if (excludeAc) { continue; }
                                }
                                //非NBA不抓2、4節
                                else if (!isNBA && (specials == 22 || specials == 26)) { continue; }

                                //設定聯盟
                                gameInfo.League.Name = matchData.DisplayName.Replace("'", "’").Trim();

                                //設定預設後綴
                                if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                                {
                                    gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Vi;
                                    gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                                    gameInfo.Suffix.Id = suffixId;
                                }

                                //是否交換隊伍 (特定聯盟來源隊伍名稱位置, 球隊總得分會與主盤相反)
                                bool isChangTeam = (isNBA && !isTeamTotal) || (matchData.DisplayName.Contains("NCAA") && isTeamTotal);
                                //設定賽事隊伍名稱
                                string teamA = gameLine.TmH.Txt.Replace("(n)", "").Replace("[n]", "").Replace("(N)", "").Replace("[N]", "").Replace("'", "’").Replace("(W)", "(w)").Trim();
                                string teamB = gameLine.TmA.Txt.Replace("(n)", "").Replace("[n]", "").Replace("(N)", "").Replace("[N]", "").Replace("'", "’").Replace("(W)", "(w)").Trim();
                                ConvertTools.ExchangeValue(ref teamA, ref teamB, isChangTeam);
                                gameInfo.TeamA.Name = teamA;
                                gameInfo.TeamB.Name = teamB;

                                //賽事類型
                                byte gType = (byte)(gameInfo.GpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                                gameInfo.GType = gType;

                                //防止兩隊名稱一樣
                                if (gameInfo.TeamA.Name.Equals(gameInfo.TeamB.Name)) { continue; }

                                gameInfo.SetGameDate(gameLine.Date);

                                //設定主客標記
                                gameInfo.Home = isNBA ? 2 : 1;

                                //是否為3x3籃球
                                bool is3x3 = Config.Is3x3(gameInfo.League.Name);

                                //設定賽事狀態
                                gameInfo.Status = gameInfo.GpType == GpType.Zd ? is3x3 ? 1 : 5 : gameInfo.Status;

                                string key = gameInfo.GetGameDictKey();

                                if (tmpGameDic.TryGetValue(key, out GameInfo outGameInfo))
                                {
                                    gameInfo = outGameInfo;
                                }
                                else if (gpType == GpType.Ds || gpType == GpType.Zd || gpType == GpType.Zp)
                                {
                                    //首次添加
                                    tmpGameDic.TryAdd(key, gameInfo);
                                }
                                else
                                {
                                    if (Config.gameDic.TryGetValue(key, out outGameInfo))
                                    {
                                        gameInfo = outGameInfo.CloneBase();
                                        tmpGameDic.TryAdd(key, gameInfo);
                                    }
                                }

                                #region 解析玩法

                                #region FT賽事資訊PK_PL處理 建立賽事

                                if (((gpType == GpType.Ds && Config.isOpenDs) ||
                                    (gpType == GpType.Zd && Config.isOpenZd) ||
                                    (gpType == GpType.Zp && Config.isOpenZp) ||
                                    (gpType == GpType.Dj && Config.isOpenDj) ||
                                    (gpType == GpType.DjQ2 && Config.isOpenDj) ||
                                    (gpType == GpType.Xb && Config.isOpenXb)) && gameLine.SpWagerMap != null)
                                {
                                    //全場RF
                                    if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeRF);
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType);
                                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                        BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                        BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                                        //RFPK_PL
                                        string isHdpOpened = gameLine.SpWagerMap[0] != "" ? gameLine.SpWagerMap[0] : gameLine.SpWagerMap[2];

                                        if (isHdpOpened != "")
                                        {
                                            //RT
                                            int strong = gameLine.SpWagerMap[0] != "" ? 1 : gameLine.SpWagerMap[2] != "" ? 0 : 0;
                                            if (isChangTeam) { strong = strong == 1 ? 0 : 1; }

                                            //PK,PL
                                            decimal plate = decimal.Parse(isHdpOpened);
                                            decimal odds_A = GetHrefText(gameLine.SpWagerMap[1]);
                                            decimal odds_B = GetHrefText(gameLine.SpWagerMap[3]);
                                            ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangTeam);
                                            //設置數據
                                            kInfo.SetLine(plate, strong);
                                            bInfoA.SetOdds(odds_A);
                                            bInfoB.SetOdds(odds_B);
                                            kInfo.SetIsOpen();
                                        }

                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                        kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                    }

                                    //全場OU
                                    if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeOU);
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                        kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                        BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                                        //OU 設定OUPK_PL
                                        string isOuOpened = gameLine.TotWagerMap[0];

                                        if (isOuOpened != "")
                                        {
                                            //PK,PL
                                            decimal plate = decimal.Parse(isOuOpened);
                                            decimal odds_O = GetHrefText(gameLine.TotWagerMap[1]);
                                            decimal odds_U = GetHrefText(gameLine.TotWagerMap[2]);
                                            //設置數據
                                            kInfo.SetLine(plate);
                                            bInfoO.SetOdds(odds_O);
                                            bInfoU.SetOdds(odds_U);
                                            kInfo.SetIsOpen();
                                        }

                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                    }

                                    int tmpType = gameInfo.GpType == GpType.Zd ? 1 : 3;

                                    //全場1*2
                                    if (Config.SetKindType(gameInfo.GpType, specials, tmpType, out int kTypeDy))
                                    {
                                        byte setType = 1;

                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDy, setType);
                                        BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                                        BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));
                                        //PL
                                        decimal odds_A = GetHrefText(gameLine.WagerMap[1]);
                                        decimal odds_B = GetHrefText(gameLine.WagerMap[2]);
                                        ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangTeam);
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = specials * 100 + setType;

                                        if (odds_A != 0 && odds_B != 0)
                                        {
                                            //設置數據
                                            bInfoA.SetOdds(odds_A);
                                            bInfoB.SetOdds(odds_B);
                                            kInfo.SetIsOpen();

                                            if (gameInfo.GpType == GpType.Zd)
                                            {
                                                // ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                                                if (Config.isZdOnlyPK &&
                                                gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                                                gpdmInfo.TryGetValue(kTypeDy, out KInfo kInfoRf) && kInfoRf.IsOpen &&
                                                (kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                                                kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                                                Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > Math.Abs(odds_A - odds_B) ||
                                                (specials == 0 && kInfoRf.Line == 500)))
                                                {
                                                    //更新kInfo
                                                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                    //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                                    kInfo.SetIsOpen(!(specials == 0 && kInfoRf.Line == 500 && Math.Min(odds_A, odds_B) < 1.74m));
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

                                        if (gameInfo.GpType != GpType.Zd)
                                        {
                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);

                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeDy, kInfo);
                                        }
                                    }

                                    //全場OE
                                    if (Config.SetKindType(gameInfo.GpType, specials, 4, out int kTypeOE))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeOE);

                                        //限定setType只能有一個
                                        if (setType == 1)
                                        {
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType);
                                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                            BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                            decimal odds = 0m;

                                            //手動設置OE
                                            if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                                            {
                                                if (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe && !is3x3)
                                                {
                                                    odds = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                                }
                                                else if (Config.isOpenDsZpOe)
                                                {
                                                    odds = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                                }

                                                kInfo.IsManual = true;
                                            }

                                            //設置數據
                                            if (odds > 0m)
                                            {
                                                bInfoO.SetOdds(odds);
                                                bInfoE.SetOdds(odds);
                                                kInfo.SetIsOpen();
                                            }

                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                            kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                            //生成gpdm並新增對應字典
                                            decimal gpdm = specials * 100 + setType;
                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                        }
                                    }

                                }

                                #endregion

                                #region HT賽事資訊PK_PL處理 建立賽事

                                if (haveHt &&
                                    ((gpType == GpType.Ds && Config.isOpenDs) ||
                                    (gpType == GpType.Zd && Config.isOpenZd) ||
                                    (gpType == GpType.Zp && Config.isOpenZp)) && gameLine.SpWagerMap != null)
                                {
                                    int tmpSpecials = specials == 0 ? 1 : specials;

                                    if ((!string.IsNullOrEmpty(gameLine.SpWagerMap[4]) || !string.IsNullOrEmpty(gameLine.SpWagerMap[6]) || !string.IsNullOrEmpty(gameLine.TotWagerMap[3])))
                                    {
                                        //限制HT鴛鴦盤抓取數量
                                        if (!Config.isSbDjSame && lvl != 5)
                                        {
                                            continue;
                                        }

                                        //上半RF
                                        if (Config.SetKindType(gameInfo.GpType, tmpSpecials, 1, out int kTypeRF))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeRF);
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType);
                                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                                            //RFPK_PL
                                            string is1hHdpOpened = gameLine.SpWagerMap[4] != "" ? gameLine.SpWagerMap[4] : gameLine.SpWagerMap[6];

                                            if (is1hHdpOpened != "")
                                            {
                                                //RT
                                                int strong = gameLine.SpWagerMap[4] != "" ? 1 : gameLine.SpWagerMap[6] != "" ? 0 : 0;
                                                if (isChangTeam) { strong = strong == 1 ? 0 : 1; }
                                                //PK,PL
                                                decimal plate = decimal.Parse(is1hHdpOpened);
                                                decimal odds_A = GetHrefText(gameLine.SpWagerMap[5]);
                                                decimal odds_B = GetHrefText(gameLine.SpWagerMap[7]);
                                                ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangTeam);
                                                //設置數據
                                                kInfo.SetLine(plate, strong);
                                                bInfoA.SetOdds(odds_A);
                                                bInfoB.SetOdds(odds_B);
                                                kInfo.SetIsOpen();
                                            }

                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                            //生成gpdm並新增對應字典
                                            decimal gpdm = tmpSpecials * 100 + setType;
                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                        }

                                        //上半OU
                                        if (Config.SetKindType(gameInfo.GpType, tmpSpecials, 2, out int kTypeOU))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeOU);
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                            kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                                            //OU 設定OUPK_PL
                                            string is1hOuOpened = gameLine.TotWagerMap[3];

                                            if (is1hOuOpened != "")
                                            {
                                                //PK,PL
                                                decimal plate = decimal.Parse(is1hOuOpened);
                                                decimal odds_O = GetHrefText(gameLine.TotWagerMap[4]);
                                                decimal odds_U = GetHrefText(gameLine.TotWagerMap[5]);
                                                //設置數據
                                                kInfo.SetLine(plate);
                                                bInfoO.SetOdds(odds_O);
                                                bInfoU.SetOdds(odds_U);
                                                kInfo.SetIsOpen();
                                            }

                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                            //生成gpdm並新增對應字典
                                            decimal gpdm = tmpSpecials * 100 + setType;
                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                        }

                                        //上半1*2
                                        if (Config.SetKindType(gameInfo.GpType, tmpSpecials, 3, out int kTypeDy))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeDy);

                                            //限定setType只能有一個
                                            if (gameInfo.GpType != GpType.Zd && setType == 1)
                                            {
                                                //生成物件放進字典
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDy, setType);
                                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeDy, setType, 1));
                                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeDy, setType, 2));
                                                //PL
                                                decimal odds_A = GetHrefText(gameLine.WagerMap[4]);
                                                decimal odds_B = GetHrefText(gameLine.WagerMap[5]);
                                                ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isChangTeam);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = tmpSpecials * 100 + setType;

                                                if (odds_A != 0 && odds_B != 0)
                                                {
                                                    //設置數據
                                                    bInfoA.SetOdds(odds_A);
                                                    bInfoB.SetOdds(odds_B);
                                                    kInfo.SetIsOpen();

                                                    if (gameInfo.GpType == GpType.Zd)
                                                    {
                                                        // ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                                                        if (Config.isZdOnlyPK &&
                                                            gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                                                            gpdmInfo.TryGetValue(kTypeDy, out KInfo kInfoRf) && kInfoRf.IsOpen &&
                                                            kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                                                            kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                                                            Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > Math.Abs(odds_A - odds_B))
                                                        {
                                                            //更新kInfo
                                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
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

                                                if (gameInfo.GpType != GpType.Zd)
                                                {
                                                    //放進字典
                                                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeDy, kInfo);
                                                }
                                            }
                                        }

                                        //上半OE
                                        if (Config.SetKindType(gameInfo.GpType, tmpSpecials, 4, out int kTypeOE))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeOE);

                                            //限定setType只能有一個
                                            if (setType == 1)
                                            {
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType);
                                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                                decimal odds = 0m;

                                                //手動設置OE
                                                if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                                                {
                                                    if (gameInfo.GpType == GpType.Zd)
                                                    {
                                                        odds = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                                    }
                                                    else if (Config.isOpenDsZpOe)
                                                    {
                                                        odds = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                                    }

                                                    kInfo.IsManual = true;
                                                }

                                                //設置數據
                                                if (odds > 0m)
                                                {
                                                    bInfoO.SetOdds(odds);
                                                    bInfoE.SetOdds(odds);
                                                    kInfo.SetIsOpen();
                                                }

                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = tmpSpecials * 100 + setType;
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                            }
                                        }
                                    }
                                }

                                #endregion

                                #region AC賽事資訊PK_PL處理 建立賽事

                                if (isTeamTotal && Config.isOpenAc)
                                {
                                    #region 主隊

                                    int tmpSpecialsA = isNBA ? specials + 1 : specials;

                                    //主隊OU
                                    if (Config.SetKindType(gameInfo.GpType, tmpSpecialsA, 2, out int kTypeOU))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeOU);
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                        BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                                        if (decimal.TryParse(isChangTeam ? gameLine.WagerMap[3] : gameLine.WagerMap[0], out decimal newPlate))
                                        {
                                            //PK,PL
                                            decimal plate = newPlate;
                                            decimal odds_O = GetHrefText(isChangTeam ? gameLine.WagerMap[4] : gameLine.WagerMap[1]);
                                            decimal odds_U = GetHrefText(isChangTeam ? gameLine.WagerMap[5] : gameLine.WagerMap[2]);
                                            //設置數據
                                            kInfo.SetLine(plate);
                                            bInfoO.SetOdds(odds_O);
                                            bInfoU.SetOdds(odds_U);
                                            kInfo.SetIsOpen();
                                        }

                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = tmpSpecialsA * 100 + setType;
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                    }

                                    //主隊OE
                                    if (Config.SetKindType(gameInfo.GpType, tmpSpecialsA, 4, out int kTypeOE))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeOE);

                                        //限定setType只能有一個
                                        if (setType == 1)
                                        {
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType);
                                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                            BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                            decimal odds = 0m;

                                            //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                                            if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe))
                                            {
                                                odds = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                                kInfo.IsManual = true;
                                            }

                                            //設置數據
                                            if (odds > 0m)
                                            {
                                                bInfoO.SetOdds(odds);
                                                bInfoE.SetOdds(odds);
                                                kInfo.SetIsOpen();
                                            }

                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                            kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                            //生成gpdm並新增對應字典
                                            decimal gpdm = tmpSpecialsA * 100 + setType;
                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                        }
                                    }

                                    #endregion

                                    #region 客隊

                                    //客隊OU
                                    kTypeOU = 0;
                                    int tmpSpecialsB = isNBA ? specials : specials + 1;

                                    if (Config.SetKindType(gameInfo.GpType, tmpSpecialsB, 2, out kTypeOU))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeOU);
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                        BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                        BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                                        if (decimal.TryParse(isChangTeam ? gameLine.WagerMap[0] : gameLine.WagerMap[3], out decimal newPlate))
                                        {
                                            //PK,PL
                                            decimal plate = newPlate;
                                            decimal odds_O = GetHrefText(isChangTeam ? gameLine.WagerMap[1] : gameLine.WagerMap[4]);
                                            decimal odds_U = GetHrefText(isChangTeam ? gameLine.WagerMap[2] : gameLine.WagerMap[5]);
                                            //設置數
                                            kInfo.SetLine(plate);
                                            bInfoO.SetOdds(odds_O);
                                            bInfoU.SetOdds(odds_U);
                                            kInfo.SetIsOpen();
                                        }

                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                        kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                        //生成gpdm並新增對應字典
                                        decimal gpdm = tmpSpecialsB * 100 + setType;
                                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                    }

                                    //客隊OE
                                    if (Config.SetKindType(gameInfo.GpType, tmpSpecialsB, 4, out kTypeOE))
                                    {
                                        byte setType = gameInfo.GetSetType(kTypeOE);

                                        //限定setType只能有一個
                                        if (setType == 1)
                                        {
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType);
                                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                            BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                            decimal odds = 0m;

                                            //手動設置OE (排除 ZD 全場/上下半/單節 主客隊總得分 oe設置)
                                            if (gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe)
                                            {
                                                odds = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                                kInfo.IsManual = true;
                                            }

                                            //設置數據
                                            if (odds > 0m)
                                            {
                                                bInfoO.SetOdds(odds);
                                                bInfoE.SetOdds(odds);
                                                kInfo.SetIsOpen();
                                            }

                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                            kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                            //生成gpdm並新增對應字典
                                            decimal gpdm = tmpSpecialsB * 100 + setType;
                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                        }
                                    }

                                    #endregion
                                }

                                #endregion

                                #endregion
                            }
                        }
                    }
                }

                //緩存比對處理
                if (tmpGameDic.Count > 0)
                {
                    foreach (KeyValuePair<string, GameInfo> kvp in tmpGameDic)
                    {
                        kvp.Value.CheckPlAndSet();

                        //存在才處理
                        if (Config.gameDic.TryGetValue(kvp.Key, out GameInfo getGame))
                        {
                            getGame.Compare(kvp.Value, gpType);
                        }
                        //不存在直接添加
                        else
                        {
                            Config.gameDic.TryAdd(kvp.Key, kvp.Value.DeepClone());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, gpType.ToString() + Environment.NewLine + ex.ToString());
                throw new Exception("PIN-" + gpType.ToString().ToUpper() + "來源內容解析失敗");
            }
        }

        /// <summary>
        /// 取得鴛鴦盤排序
        /// </summary>
        /// <param name="index">欲排序位置</param>
        /// <param name="selectedIndex">主要盤位置</param>
        /// <returns></returns>
        public static int GetSortIndex(int index, int selectedIndex)
        {
            int sort = -1;

            //主要盤位置優先
            if (index == selectedIndex)
            {
                sort = 0;
            }
            //位於主要盤左邊 順序2n-1
            else if (index < selectedIndex)
            {
                sort = 2 * Math.Abs(selectedIndex - index) - 1;
            }
            //位於主要盤右邊 順序2n
            else
            {
                sort = 2 * Math.Abs(selectedIndex - index);
            }

            return sort;
        }

        //去除Html標籤
        public static decimal GetHrefText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Regex regex = new Regex(@"<a\s*[^>]*>([\s\S]+?)</a>", RegexOptions.IgnoreCase);
                System.Text.RegularExpressions.Match m = regex.Match(text);

                if (m.Success)
                {
                    string innerHTML = m.Result("$1");

                    if (innerHTML == "&nbsp;")
                    {
                        return 0;
                    }

                    if (innerHTML.Contains("."))
                    {
                        return decimal.Parse(innerHTML.Substring(0, innerHTML.IndexOf(".") + 3));
                    }

                    return decimal.Parse(innerHTML);
                }
            }

            return 0;
        }
    }
}