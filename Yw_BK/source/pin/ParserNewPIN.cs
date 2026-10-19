#region 功能修改描述與時間

/*
    描述：新版PIN 解析器
    日期：2021-02-09
                    
    描述：調整ZD開賽時間有查詢過才使用
    日期：2021-06-29

    描述：添加單隊總得分第(2~4)節玩法解析
    日期：2023-02-15
                        
    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16
*/

#endregion

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// 新版PIN 解析器
    /// </summary>
    public class ParserNewPIN
    {
        /// <summary>
        /// 時間戳資訊 <GpType, timestamp[0]ZD [1]DS>
        /// </summary>
        private static ConcurrentDictionary<GpType, long[]> timestampDic = new ConcurrentDictionary<GpType, long[]>();
        /// <summary>
        /// 存放來源Odds_a, Odds_b位置
        /// </summary>
        private static Dictionary<string, int[]> cdOddsIndex = new Dictionary<string, int[]>()
        {
            { "rf", new int[]{ 3, 4 } },
            { "ou", new int[]{ 2, 3 } },
            { "dy", new int[]{ 1, 0} },
        };

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
                //賽事總字典
                ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;
                //Jason
                JObject obj = JObject.Parse(html.Replace(@"\r\n", ""));

                //設定解析類型
                string[] types;

                switch (gpType)
                {
                    case GpType.Ds:
                    case GpType.Zp:
                        types = new string[] { "n" };
                        break;
                    case GpType.Zd:
                        types = new string[] { "l" };
                        break;
                    case GpType.Ac:
                        types = new string[] { "l", "n" };
                        break;
                    default:
                        types = new string[] { };
                        break;
                }

                //依類型解析
                foreach (string type in types)
                {
                    if (obj.TryGetValue(type, out JToken datas))
                    {
                        var firstNode = datas.FirstOrDefault();

                        //只解析籃球
                        if (firstNode == null || firstNode[1].ToString() != "Basketball") { continue; }

                        #region 利用時間戳判斷是否為回跳數據，則跳過不解析

                        if (long.TryParse(firstNode[3].ToString(), out long timestamp))
                        {
                            int index = type == "l" ? 0 : 1;
                            byte gType = index == 0 ? (byte)GType.BkZd : (byte)GType.BkDs;
                            var info = timestampDic.GetOrAdd(gpType, new long[2]);

                            //時間戳比原本還要小為舊數據，跳過不解析
                            if (info[index] > 0 && info[index] > timestamp)
                            {
                                //取得請求型態對應賽事, 更新數據流水號 (防止因不解析而導致關盤)
                                foreach (GameInfo gameInfo in gameDic.Values.Where(a => a.GType == gType))
                                {
                                    string strType = gpType.ToString();
                                    var kInfos = gameInfo.ObjDic.Values.SelectMany(b => b.Values).Where(k => k.StrType == strType);

                                    foreach (KInfo kInfo in kInfos)
                                    {
                                        kInfo.DataId = sendId;
                                    }

                                    if (kInfos.Count() > 0) { gameInfo.DataId = sendId; }
                                }

                                //記錄被排除的回跳數據
                                LogFile.SetLog(LogMode.Remind_NoELK, $"GpType:{gpType}, SendId:{sendId}, Type:{type}, (舊)timestamp:{info[index]}, (新)timestamp:{timestamp}");
                                continue;
                            }

                            //更新時間戳
                            info[index] = timestamp;
                        }

                        #endregion

                        JToken leagues = firstNode[2];

                        //聯盟層
                        foreach (JToken jTokenLeague in leagues)
                        {
                            string league = jTokenLeague[1].ToString();
                            JToken games = jTokenLeague[2];

                            //賽事層
                            foreach (JToken jTokenGame in games)
                            {
                                if (jTokenGame.Count() < 8) { continue; }

                                GameInfo gameInfo = new GameInfo(jTokenGame[0].ToString(), sendId);
                                //聯盟
                                gameInfo.League.Name = league.Replace("'", "’");

                                if (Config.FilterLeagueHashSet.Any(filter => gameInfo.League.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    break;
                                }

                                //是否為NBA賽事
                                bool isNBA = Regex.IsMatch(gameInfo.League.Name, "^NBA") && !Config.CheckNbaExcludeName(gameInfo.League.Name);
                                //是否為3x3籃球
                                bool is3x3 = Config.Is3x3(gameInfo.League.Name);
                                //賽事狀態
                                gameInfo.Status = gpType == GpType.Zd && is3x3 ? 1 : gameInfo.Status;
                                //隊名
                                string teamA = jTokenGame[1].ToString().Replace("(n)", "").Replace("[n]", "").Replace("(N)", "").Replace("[N]", "").Replace("'", "’").Replace("-", " ").Trim();
                                string teamB = jTokenGame[2].ToString().Replace("(n)", "").Replace("[n]", "").Replace("(N)", "").Replace("[N]", "").Replace("'", "’").Replace("-", " ").Trim();
                                ConvertTools.ExchangeValue(ref teamA, ref teamB, isNBA);
                                gameInfo.TeamA.Name = teamA;
                                gameInfo.TeamB.Name = teamB;

                                //隊伍防呆[防止兩隊名稱一樣]
                                if (gameInfo.TeamA.Name.Equals(gameInfo.TeamB.Name))
                                {
                                    continue;
                                }

                                //設定預設後綴
                                if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                                {
                                    gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Vi;
                                    gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                                    gameInfo.Suffix.Id = suffixId;
                                }

                                long unixTimeStamp = long.Parse(jTokenGame[4].ToString());
                                gameInfo.SetGameDate(DateTimeOffset.FromUnixTimeMilliseconds(unixTimeStamp).LocalDateTime);

                                //[Ac] 額外區分Ds、Zp、Zd
                                if (gpType == GpType.Ac)
                                {
                                    #region 區分Ds、Zp、Zd

                                    int hour = DateTime.Now.Hour;
                                    DateTime DsDate = hour >= 12 ? DateTime.Now.Date.AddDays(1).AddHours(12) : DateTime.Now.Date.AddHours(12);

                                    if (type == "l")
                                    {
                                        if (!Config.isOpenZd) { continue; }
                                        gameInfo.GType = (byte)GType.BkZd;
                                        gameInfo.GpType = GpType.Zd;
                                    }
                                    else
                                    {
                                        if (gameInfo.PaymentDate >= DsDate)
                                        {
                                            if (!Config.isOpenZp) { continue; }
                                            gameInfo.GType = (byte)GType.BkDs;
                                            gameInfo.GpType = GpType.Zp;
                                        }
                                        else
                                        {
                                            if (!Config.isOpenDs) { continue; }
                                            gameInfo.GType = (byte)GType.BkDs;
                                            gameInfo.GpType = GpType.Ds;
                                        }

                                        if (gameInfo.GameDate <= DateTime.Now)
                                        {
                                            continue;
                                        }
                                    }

                                    #endregion
                                }
                                else
                                {
                                    if (gpType != GpType.Zd)
                                    {
                                        if (gameInfo.GameDate <= DateTime.Now)
                                        {
                                            continue;
                                        }

                                        gameInfo.GType = (byte)GType.BkDs;
                                    }
                                    else
                                    {
                                        gameInfo.GType = (byte)GType.BkZd;
                                    }

                                    gameInfo.GpType = gpType;
                                }

                                //設定主客標記
                                gameInfo.Home = isNBA ? 2 : 1;

                                #region 解析玩法

                                JToken plays = jTokenGame[8];
                                //玩法開關
                                bool isOpenOdds = jTokenGame[17].ToString() == "O";

                                if (gpType != GpType.Ac)
                                {
                                    //NBA ZD、NBA OT DS (全、上、下、第1 2 3 4)，OT ZD(全、上、下)
                                    int count = gpType == GpType.Zd && !isNBA ? 3 : 7;

                                    for (int num = 0; num < count; num++)
                                    {
                                        if (!Config.isOpenXb && num == 2 || !Config.isOpenDj && num >= 3) { continue; }
                                        //特殊玩法
                                        int specials = num;
                                        //查詢場次Token(全場:0，上半:1, 下半:2，第一節:3)
                                        string selectToken = num.ToString();
                                        //是否抓鴛鴦盤(DS 全上)
                                        bool isDsSameCount = gpType != GpType.Zd && num <= 1;

                                        #region 賽事資訊PK_PL處理 建立賽事

                                        JToken playGame = plays.SelectToken(selectToken);

                                        if (playGame != null && playGame.HasValues)
                                        {
                                            //RF盤抓取
                                            var sameRf = playGame.ElementAtOrDefault(0).Children().Select((Node, Index) => new { Index, Node }).ToArray();
                                            //OU盤抓取
                                            var sameOu = playGame.ElementAtOrDefault(1).Children().Select((Node, Index) => new { Index, Node }).ToArray();

                                            //盤數
                                            int sameCount = Config.sameCount == 1 ? 1 : Config.sameCount == 2 ? 3 : 5;

                                            //全場RF (DS A盤額外處理(需抓來源最平水))
                                            if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kTypeRF) && cdOddsIndex.TryGetValue("rf", out int[] rfIndex) && !isDsSameCount)
                                            {
                                                var selected = sameRf.FirstOrDefault(a => a.Node[8].ToString() == "0");
                                                //選定盤位置
                                                int selectedIndex = selected == null ? sameRf.Length / 2 : selected.Index;
                                                //排序
                                                var rf = sameRf?.OrderBy(a => GetSortIndex(a.Index, selectedIndex));

                                                if (num > 2 && !Config.isSbDjSame)
                                                {
                                                    sameCount = 1;
                                                }

                                                for (int i = 0; rf != null && i < sameCount; i++)
                                                {
                                                    byte setType = Convert.ToByte(i + 1);
                                                    //生成物件
                                                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType);
                                                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                                    BInfo bInfoA = new BInfo(kTypeRF * 10 + 1);
                                                    BInfo bInfoB = new BInfo(kTypeRF * 10 + 2);

                                                    //RF 設定PK_PL
                                                    var playInfo = rf.ElementAtOrDefault(i)?.Node;

                                                    if (playInfo != null)
                                                    {
                                                        //RT
                                                        decimal.TryParse(playInfo.ElementAtOrDefault(0).ToString(), out decimal pk);
                                                        int lineAt = pk < 0 ? 0 : 1;
                                                        if (isNBA) { lineAt = lineAt == 1 ? 0 : 1; }
                                                        //PK,PL
                                                        decimal plate = Math.Abs(pk);
                                                        decimal.TryParse(playInfo.ElementAtOrDefault(rfIndex[0]).ToString(), out decimal plA);
                                                        decimal.TryParse(playInfo.ElementAtOrDefault(rfIndex[1]).ToString(), out decimal plB);
                                                        ConvertTools.ExchangeValue(ref plA, ref plB, isNBA);
                                                        //設置數據
                                                        kInfo.SetLine(plate, lineAt);
                                                        bInfoA.SetOdds(plA);
                                                        bInfoB.SetOdds(plB);
                                                        kInfo.SetIsOpen(!string.IsNullOrEmpty(playInfo.ElementAtOrDefault(rfIndex[0]).ToString()) && isOpenOdds);
                                                    }

                                                    //放進字典
                                                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                    //生成gpdm並新增對應字典
                                                    decimal gpdm = specials * 100 + setType;
                                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                                }
                                            }

                                            //全場OU (DS A盤額外處理(需抓來源最平水))
                                            if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kTypeOU) && cdOddsIndex.TryGetValue("ou", out int[] ouIndex) && !isDsSameCount)
                                            {
                                                var selected = sameOu.FirstOrDefault(a => a.Node[5].ToString() == "0");
                                                //選定盤位置
                                                int selectedIndex = selected == null ? sameOu.Length / 2 : selected.Index;
                                                //排序
                                                var ou = sameOu?.OrderBy(a => GetSortIndex(a.Index, selectedIndex));

                                                if (num > 2 && !Config.isSbDjSame)
                                                {
                                                    sameCount = 1;
                                                }

                                                for (int i = 0; ou != null && i < sameCount; i++)
                                                {
                                                    byte setType = Convert.ToByte(i + 1);
                                                    //生成物件
                                                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                                    BInfo bInfoO = new BInfo(kTypeOU * 10 + 1);
                                                    BInfo bInfoU = new BInfo(kTypeOU * 10 + 2);

                                                    //OU 設定PK_PL
                                                    var playInfo = ou.ElementAtOrDefault(i)?.Node;

                                                    if (playInfo != null)
                                                    {
                                                        decimal.TryParse(playInfo.ElementAtOrDefault(0).ToString(), out decimal plate);
                                                        decimal.TryParse(playInfo.ElementAtOrDefault(ouIndex[0]).ToString(), out decimal plA);
                                                        decimal.TryParse(playInfo.ElementAtOrDefault(ouIndex[1]).ToString(), out decimal plB);
                                                        //設置數據
                                                        kInfo.SetLine(plate);
                                                        bInfoO.SetOdds(plA);
                                                        bInfoU.SetOdds(plB);
                                                        kInfo.SetIsOpen(!string.IsNullOrEmpty(playInfo.ElementAtOrDefault(ouIndex[0]).ToString()) && isOpenOdds);
                                                    }

                                                    //放進字典
                                                    kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                                    kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                                    //生成gpdm並新增對應字典
                                                    decimal gpdm = specials * 100 + setType;
                                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                                }
                                            }

                                            int tmpType = gameInfo.GpType == GpType.Zd ? 1 : 3;

                                            //全場1*2
                                            if (Config.SetKindType(gameInfo.GpType, specials, tmpType, out int kTypeDy) && cdOddsIndex.TryGetValue("dy", out int[] dyIndex))
                                            {
                                                byte setType = 1;

                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDy, setType);
                                                BInfo bInfoA = new BInfo(kTypeDy * 10 + 1);
                                                BInfo bInfoB = new BInfo(kTypeDy * 10 + 2);
                                                // 1*2 PL
                                                var playInfo = playGame.ElementAtOrDefault(2);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = specials * 100 + setType;

                                                if (playInfo.HasValues)
                                                {
                                                    decimal.TryParse(playInfo.ElementAtOrDefault(dyIndex[0]).ToString(), out decimal plA);
                                                    decimal.TryParse(playInfo.ElementAtOrDefault(dyIndex[1]).ToString(), out decimal plB);
                                                    ConvertTools.ExchangeValue(ref plA, ref plB, isNBA);
                                                    //設置數據
                                                    bInfoA.SetOdds(plA);
                                                    bInfoB.SetOdds(plB);
                                                    kInfo.SetIsOpen(!string.IsNullOrEmpty(playInfo.ElementAtOrDefault(dyIndex[1]).ToString()) && isOpenOdds);

                                                    if (gameInfo.GpType == GpType.Zd)
                                                    {
                                                        // ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                                                        if (Config.isZdOnlyPK &&
                                                        gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                                                        gpdmInfo.TryGetValue(kTypeDy, out KInfo kInfoRf) && kInfoRf.IsOpen &&
                                                        (kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                                                        kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                                                        Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > Math.Abs(plA - plB) ||
                                                        (specials == 0 && kInfoRf.Line == 500)))
                                                        {
                                                            kInfo.IsSourceFollow = 1;
                                                            //更新kInfo
                                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                            //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                                            kInfo.SetIsOpen(!(specials == 0 && kInfoRf.Line == 500 && Math.Min(plA, plB) < 1.74m));
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
                                                byte setType = 1;

                                                //限定setType只能有一個
                                                if (setType == 1)
                                                {
                                                    //生成物件
                                                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType);
                                                    BInfo bInfoO = new BInfo(kTypeOE * 10 + 1);
                                                    BInfo bInfoE = new BInfo(kTypeOE * 10 + 2);
                                                    decimal odds = 0m;

                                                    //手動設置OE
                                                    if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                                                    {
                                                        if (gameInfo.GpType == GpType.Zd && !is3x3)
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

                                            //DS全上鴛鴦盤 RF OU 額外處理
                                            if (isDsSameCount)
                                            {
                                                DsSameCount(sameRf, sameOu, gameInfo, specials, gpType, sendId, isOpenOdds, isNBA);
                                            }
                                        }
                                    }

                                    #endregion
                                }
                                else
                                {
                                    for (int i = 0; i <= 6; i++)
                                    {
                                        int specials = 0;

                                        // i = 0 全場 , i = 1 上半 ,  i = 2 下半(目前沒有要跟) , i = 3 第一節 ..依此類推
                                        switch (i)
                                        {
                                            case 0: specials = 14; break;
                                            case 1: specials = 16; break;
                                            case 3: specials = 20; break;
                                            case 4: specials = 22; break;
                                            case 5: specials = 24; break;
                                            case 6: specials = 26; break;
                                            default: continue;
                                        }

                                        //NBA ZD目前只跟全場球隊總得分
                                        if (gameInfo.GpType == GpType.Zd)
                                        {
                                            bool excludeAc = isNBA ? i > 0 : true;

                                            if (excludeAc) { continue; }
                                        }
                                        //非NBA不抓2、4節
                                        else if (!isNBA && (specials == 22 || specials == 26)) { continue; }

                                        //PL資訊
                                        JToken play = plays.SelectToken($"{i}");
                                        //總得分資訊
                                        var total = play?.ElementAtOrDefault(0);

                                        for (int j = 0; total != null && j < 2; j++)
                                        {
                                            //A OR B隊玩法
                                            int tmpSpecials = Config.TeamTotalIsChangeSpecials(specials += j, isNBA);

                                            //全場A、B隊總得分
                                            if (Config.SetKindType(gameInfo.GpType, tmpSpecials, 2, out int kTypeOU) && cdOddsIndex.TryGetValue("ou", out int[] ouIndex))
                                            {
                                                byte setType = 1;
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                                BInfo bInfoO = new BInfo(kTypeOU * 10 + 1);
                                                BInfo bInfoU = new BInfo(kTypeOU * 10 + 2);

                                                //OU 設定PK_PL
                                                var playInfo = total.ElementAtOrDefault(j)?.FirstOrDefault();

                                                decimal.TryParse(playInfo?.ElementAtOrDefault(0).ToString(), out decimal plate);
                                                decimal.TryParse(playInfo?.ElementAtOrDefault(ouIndex[0]).ToString(), out decimal plA);
                                                decimal.TryParse(playInfo?.ElementAtOrDefault(ouIndex[1]).ToString(), out decimal plB);
                                                //設置數據
                                                kInfo.SetLine(plate);
                                                bInfoO.SetOdds(plA);
                                                bInfoU.SetOdds(plB);
                                                kInfo.SetIsOpen(plA > 0 && plB > 0 && isOpenOdds);

                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                                kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = tmpSpecials * 100 + setType;
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                            }

                                            //全場A、B隊總得分OE
                                            if (Config.SetKindType(gameInfo.GpType, tmpSpecials, 4, out int kTypeOE))
                                            {
                                                byte setType = 1;

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
                                                    decimal gpdm = tmpSpecials * 100 + setType;
                                                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                                }
                                            }
                                        }
                                    }
                                }

                                #endregion

                                #region 補上玩法缺少的項目

                                foreach (KeyValuePair<decimal, GpdmInfo> tmp in gameInfo.ObjDic.OrderBy(e => e.Key))
                                {
                                    decimal gpdm = tmp.Key;
                                    GpdmInfo gpdmDic = tmp.Value;

                                    if (gpdmDic.Any())
                                    {
                                        //取得specials值
                                        int tmpSpecials = GetSpecials(gpdm);

                                        //RF
                                        if (Config.SetKindType(gpType, tmpSpecials, 1, out int kTypeRF) && !gpdmDic.ContainsKey(kTypeRF))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeRF);
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType);
                                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                        }

                                        //OU
                                        if (Config.SetKindType(gpType, tmpSpecials, 2, out int kTypeOU) && !gpdmDic.ContainsKey(kTypeOU))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeOU);
                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType);
                                            BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                            BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                                            //放進字典
                                            kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                            kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                            gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                        }

                                        //DY
                                        if (Config.SetKindType(gpType, tmpSpecials, 3, out int kTypeDY) && !gpdmDic.ContainsKey(kTypeDY))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeDY);

                                            //限定setType只能有一個
                                            if (setType == 1)
                                            {
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDY, setType);
                                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeDY, setType, 1));
                                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeDY, setType, 2));
                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeDY, kInfo);
                                            }
                                        }

                                        //OE
                                        if (Config.SetKindType(gpType, tmpSpecials, 4, out int kTypeOE) && !gpdmDic.ContainsKey(kTypeOE))
                                        {
                                            byte setType = gameInfo.GetSetType(kTypeOE);

                                            //限定setType只能有一個
                                            if (setType == 1)
                                            {
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType);
                                                BInfo bInfoO = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                                BInfo bInfoE = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                                decimal odds_A;
                                                decimal odds_B;

                                                //手動設置OE 
                                                if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                                                {
                                                    if (gameInfo.GpType == GpType.Zd)
                                                    {
                                                        odds_A = odds_B = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                                    }
                                                    else
                                                    {
                                                        odds_A = odds_B = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                                    }

                                                    //設置數據
                                                    bInfoO.SetOdds(odds_A);
                                                    bInfoE.SetOdds(odds_B);
                                                    kInfo.SetIsOpen(odds_A > 0 && odds_B > 0);

                                                    kInfo.IsManual = true;
                                                }

                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                                kInfo.BInfoDic.TryAdd(bInfoE.BType, bInfoE);
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                            }
                                        }
                                    }
                                }

                                #endregion

                                gameInfo.CheckPlAndSet();
                                tmpGameDic.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
                            }
                        }
                    }
                }

                //緩存比對處理
                foreach (KeyValuePair<string, GameInfo> kvp in tmpGameDic)
                {
                    //存在才處理
                    if (gameDic.TryGetValue(kvp.Key, out GameInfo getGame))
                    {
                        getGame.Compare(kvp.Value, gpType);
                    }
                    //不存在直接添加
                    else
                    {
                        gameDic.TryAdd(kvp.Key, kvp.Value);
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
        /// DS 鴛鴦盤處理
        /// </summary>
        /// <param name="sameRf">來源rf</param>
        /// <param name="sameOu">來源ou</param>
        /// <param name="gameInfo">GameInfo</param>
        /// <param name="specials">玩法標記</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="sendId">sendId</param>
        /// <param name="isOpenOdds">來源是否開放</param>
        /// <param name="isNBA">NBA</param>
        private static void DsSameCount(dynamic sameRf, dynamic sameOu, GameInfo gameInfo, int specials, GpType gpType, long sendId, bool isOpenOdds, bool isNBA)
        {
            #region 整理成成PlateInfo

            //RF處理
            List<PlateInfo> rfList = new List<PlateInfo>();

            foreach (var item in sameRf)
            {
                JToken tmpItem = item?.Node;

                if (tmpItem != null && tmpItem.Count() > 8 &&
                    cdOddsIndex.TryGetValue("rf", out int[] rfIndex) &&
                    decimal.TryParse(tmpItem.ElementAtOrDefault(0).ToString(), out decimal pk) &&
                    decimal.TryParse(tmpItem.ElementAtOrDefault(rfIndex[0]).ToString(), out decimal pla) &&
                    decimal.TryParse(tmpItem.ElementAtOrDefault(rfIndex[1]).ToString(), out decimal plb))
                {
                    pk = isNBA ? -pk : pk;
                    rfList.Add(new PlateInfo(pk, pla, plb));
                }
            }

            //排序rf
            rfList = rfList.OrderBy(x => x.Plate).ToList();
            //主盤
            PlateInfo rf_a = rfList.OrderBy(x => Math.Abs(x.Ratio_H - x.Ratio_A))?.FirstOrDefault();
            int index_a_rf = rfList.IndexOf(rf_a);

            //OU處理
            List<PlateInfo> ouList = new List<PlateInfo>();

            foreach (var item in sameOu)
            {
                JToken tmpItem = item?.Node;

                if (tmpItem != null && tmpItem.Count() > 5 &&
                    cdOddsIndex.TryGetValue("ou", out int[] ouIndex) &&
                    decimal.TryParse(tmpItem.ElementAtOrDefault(0).ToString(), out decimal pk) &&
                    decimal.TryParse(tmpItem.ElementAtOrDefault(ouIndex[0]).ToString(), out decimal pla) &&
                    decimal.TryParse(tmpItem.ElementAtOrDefault(ouIndex[1]).ToString(), out decimal plb))
                {
                    ouList.Add(new PlateInfo(pk, pla, plb));
                }
            }

            //排序ou
            ouList = ouList.OrderBy(x => x.Plate).ToList();
            //主盤
            PlateInfo ou_a = ouList.OrderBy(x => Math.Abs(x.Ratio_H - x.Ratio_A))?.FirstOrDefault();
            int index_a_ou = ouList.IndexOf(ou_a);

            #endregion

            //要抓取鴛鴦盤的數量 (上半需要勾上選勾才判斷)
            int sameCount = (specials == 1 && !Config.isSbDjSame) || Config.sameCount == 1 ? 1 : Config.sameCount == 2 ? 3 : 5;
            //存放抓取鴛鴦盤(存放BCDE盤)
            PlateInfo[] plateInfos_rf = new PlateInfo[4];
            PlateInfo[] plateInfos_ou = new PlateInfo[4];
            //判斷是否全好全壞
            bool isAllBacdGood_rf = index_a_rf == 0 || index_a_rf == rfList.Count - 1;
            bool isAllBacdGood_ou = index_a_ou == 0 || index_a_ou == ouList.Count - 1;
            //存放全好全壞固定方向抓法
            bool[] isMinus = new bool[2];

            //整理排序鴛鴦盤要抓的鴛鴦盤
            for (int i = 1; i < sameCount; i++)
            {
                //rf
                Config.ArrangedNbaDsFtHtSameCountOdds(rfList, rf_a, 1, i, sameCount, isAllBacdGood_rf, ref plateInfos_rf, ref isMinus[0]);
                //ou
                Config.ArrangedNbaDsFtHtSameCountOdds(ouList, ou_a, 2, i, sameCount, isAllBacdGood_ou, ref plateInfos_ou, ref isMinus[1]);
            }

            //排除為null，遞補邏輯
            plateInfos_rf = plateInfos_rf.Where(x => x != null).ToArray();
            plateInfos_ou = plateInfos_ou.Where(x => x != null).ToArray();

            //給pkpl (A盤RF OU 一起給)
            for (int i = 0; i < sameCount; i++)
            {
                PlateInfo rfPlateInfo = i == 0 ? rf_a : plateInfos_rf.Length > i - 1 ? plateInfos_rf[i - 1] : null;
                PlateInfo ouPlateInfo = i == 0 ? ou_a : plateInfos_ou.Length > i - 1 ? plateInfos_ou[i - 1] : null;

                //rf
                if (Config.SetKindType(gameInfo.GpType, specials, 1, out int rfkType))
                {
                    byte setType = gameInfo.GetSetType(rfkType);
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, rfkType, setType);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;

                    BInfo bInfoA = new BInfo(Config.GetBType(rfkType, setType, 1));
                    BInfo bInfoB = new BInfo(Config.GetBType(rfkType, setType, 2));

                    if (rfPlateInfo != null)
                    {
                        //PK,PL
                        decimal plate = Math.Abs(rfPlateInfo.Plate);
                        decimal odds_A = decimal.Floor(rfPlateInfo.Ratio_H * 100m) / 100m;
                        decimal odds_B = decimal.Floor(rfPlateInfo.Ratio_A * 100m) / 100m;
                        ConvertTools.ExchangeValue(ref odds_A, ref odds_B, isNBA);
                        //RT[1:A隊讓 0:B隊讓]
                        int strong = rfPlateInfo.Plate < 0 ? 0 : 1;

                        //設置數據
                        kInfo.SetLine(plate, strong);
                        bInfoA.SetOdds(odds_A);
                        bInfoB.SetOdds(odds_B);
                        kInfo.SetIsOpen(isOpenOdds);
                    }

                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    //生成gpdm並新增對應字典
                    decimal gpdm = specials * 100 + setType;
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(rfkType, kInfo);
                }

                //ou
                if (Config.SetKindType(gameInfo.GpType, specials, 2, out int oukType))
                {
                    byte setType = gameInfo.GetSetType(oukType);
                    //生成物件
                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, oukType, setType);
                    kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;

                    BInfo bInfoA = new BInfo(Config.GetBType(oukType, setType, 1));
                    BInfo bInfoB = new BInfo(Config.GetBType(oukType, setType, 2));

                    if (ouPlateInfo != null)
                    {
                        //PK,PL
                        decimal plate = Math.Abs(ouPlateInfo.Plate);
                        decimal odds_A = decimal.Floor(ouPlateInfo.Ratio_H * 100m) / 100m;
                        decimal odds_B = decimal.Floor(ouPlateInfo.Ratio_A * 100m) / 100m;

                        kInfo.SetLine(plate);
                        bInfoA.SetOdds(odds_A);
                        bInfoB.SetOdds(odds_B);
                        kInfo.SetIsOpen(isOpenOdds);
                    }

                    //放進字典
                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                    //生成gpdm並新增對應字典
                    decimal gpdm = specials * 100 + setType;
                    gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                    gameInfo.ObjDic[gpdm].TryAdd(oukType, kInfo);
                }
            }
        }

        /// <summary>
        /// 取得鴛鴦盤排序
        /// </summary>
        /// <param name="index">欲排序位置</param>
        /// <param name="selectedIndex">主要盤位置</param>
        /// <returns></returns>
        private static int GetSortIndex(int index, int selectedIndex)
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

        /// <summary>
        /// 從已知Gpdm倒推取得Specials值
        /// </summary>
        /// <param name="gpdm">賽事Gpdm</param>
        /// <returns>返回Specials</returns>
        private static int GetSpecials(decimal gpdm)
        {
            int specials = -1;
            int.TryParse(decimal.Truncate(gpdm / 100m).ToString(), out specials);
            return specials;
        }
    }
}