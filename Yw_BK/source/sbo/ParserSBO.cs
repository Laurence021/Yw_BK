#region 功能修改描述與時間

/*
    描述：SBO來源內容解析
    日期：2022-04-14
        
    描述：添加內頁玩法(優先獲得5.10.15.30.40.50分、球隊籃板球總數、球隊助攻總數和球隊抄截總數)
    日期：2022-05-19
        
    描述：添加Ds內頁玩法(總冠軍賽新增的玩法)
    日期：2022-06-14

    描述：外頁解析多判斷內頁關聯ID是否存在
    日期：2022-06-15
                        
    描述：調整NBA隊伍主客位置(上客下主)
    日期：2023-03-16

    描述：添加抓取OE(全/上/下/單節)
    日期：2025-03-11
*/

#endregion

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    /// <summary>
    /// SBO解析
    /// </summary>
    public class ParserSBO
    {
        /// <summary>
        /// 來源Id對應賽事字典key    例: key:11223344  value: gType League TeamA TeamB
        /// </summary>
        public static ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, string>> DictMain = new ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, string>>();
        /// <summary>
        /// 有內頁的來源賽事id集合    key:類型 , value:賽事id列表
        /// </summary>
        public static ConcurrentDictionary<GpType, HashSet<decimal>> MoreDic = new ConcurrentDictionary<GpType, HashSet<decimal>>();
        /// <summary>
        /// 來源Id對應請求key    例: key:11223344  value: League/來源Id/TeamA-对-TeamB
        /// </summary>
        public static ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, string>> RequestKey = new ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, string>>();
        /// <summary>
        /// 玩法字典  例: key:playNames集合索引值  value: 對應來源賽事id清單
        /// </summary>
        public static ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, HashSet<decimal>>> DictPlay = new ConcurrentDictionary<GpType, ConcurrentDictionary<decimal, HashSet<decimal>>>();
        /// <summary>
        /// 當前聯盟字典
        /// </summary>
        private static ConcurrentDictionary<decimal, string> dictLeague = new ConcurrentDictionary<decimal, string>();
        /// <summary>
        /// 來源外頁目錄需抓取的玩法 (不使用索引為0的位置, 撞到可能性高)
        /// </summary>
        private static string[] playNames = new string[] { "", "梦幻配对赛", "得分总数", "total-rebounds", "total-assist", "total-3-points-made" };
        /// <summary>
        /// 來源玩法對應編號 key:special , value:來源玩法編號(依序是RF、OU、DY、OE)
        /// </summary>
        private static Dictionary<string, int[]> playMethodNum = new Dictionary<string, int[]>
        {
            { "0", new int[] { 1, 3, 11, 2 }},           //0: FT
            { "1", new int[] { 7, 9, 1436, 12 }},        //1: 1H
            { "2", new int[] { 565, 566, 0, 567 }},      //2: 2H
            { "3", new int[] { 559, 560, 1413, 561 }},   //3: Q1
            { "4", new int[] { 585, 586, 1414, 587 }},   //4: Q2
            { "5", new int[] { 588, 589, 1448, 590 }},   //5: Q3
            { "6", new int[] { 591, 592, 1449, 593 }},   //6: Q4                    
            { "60", new int[] { 562 }},                  //60;多種玩法 (優先得分)
            { "70", new int[] { 563 }},                  //70;多種玩法 (最後得分)
            { "80", new int[] { 631 }},                  //80;多種玩法 (單節最高)
            { "101-5", new int[] { 741 }},               //101:優先獲得5分
            { "101-10", new int[] { 742 }},              //101:優先獲得10分
            { "101-15", new int[] { 743 }},              //101:優先獲得15分
            { "101-20", new int[] { 568 }},              //101:優先獲得20分
            { "101-30", new int[] { 1123 }},             //101:優先獲得30分
            { "101-40", new int[] { 1124 }},             //101:優先獲得40分
            { "101-50", new int[] { 1125 }},             //101:優先獲得50分
            { "14", new int[] { 0, 747, 0, 0 }},         //14:主隊總得分        (OE:748)
            { "15", new int[] { 0, 750, 0, 0 }},         //15:客隊總得分        (OE:751)
            { "16", new int[] { 0, 1062, 0, 0 }},        //16:上半主隊總得分    (OE:1063)
            { "17", new int[] { 0, 1064, 0, 0 }},        //17:上半客隊總得分    (OE:1065)
            { "20", new int[] { 0, 1033, 0, 0 }},        //20:第一節主隊總得分  (OE:1034)
            { "21", new int[] { 0, 1042, 0, 0 }},        //21:第一節客隊總得分  (OE:1043)
            { "22", new int[] { 0, 1045, 0, 0 }},        //22:第二節主隊總得分  (OE:1046)
            { "23", new int[] { 0, 1048, 0, 0 }},        //23:第二節客隊總得分  (OE:1049)
            { "24", new int[] { 0, 1051, 0, 0 }},        //24:第三節主隊總得分  (OE:1052)
            { "25", new int[] { 0, 1054, 0, 0 }},        //25:第三節客隊總得分  (OE:1055)
            { "26", new int[] { 0, 1057, 0, 0 }},        //26:第四節主隊總得分  (OE:1058)
            { "27", new int[] { 0, 1060, 0, 0 }},        //27:第四節客隊總得分  (OE:1061)
            { "13", new int[] { 644, 645, 0, 0 }},       //13:3分球總數         (OE:646)
            { "31", new int[] { 784, 785, 0, 786 }},     //31:球隊助攻總數 助攻总数
            { "32", new int[] { 781, 782, 0, 783 }},     //32:球隊籃板球總數 篮板总数
            { "33", new int[] { 787, 788, 0, 789 }},     //33:球隊抄截總數 抄截总数            
            { "41", new int[] { 1304, 0, 0, 0 }},        //41:首次暂停   
            { "151", new int[] { 1188, 0, 0, 0 }},       //151:首次犯規
            { "161", new int[] { 878, 0, 0, 0 }},        //161:首先获得罚球得分 First Free Throw Made
            { "171", new int[] { 1287, 0, 0, 0 }},       //171:第二节首先获得罚球得分 2nd Quarter - First Free Throw Made
            { "181", new int[] { 1290, 0, 0, 0 }},       //181:第三节首先获得罚球得分 3rd Quarter - First Free Throw Made
            { "191", new int[] { 1293, 0, 0, 0 }},       //191:第四节首先获得罚球得分 4th Quarter - First Free Throw Made
            { "161-9", new int[] { 880, 0, 0, 0 }},      //161:最后获得罚球得分 Last Free Throw Made                                                        
            { "171-9", new int[] { 1288, 0, 0, 0 }},     //171:第二节最后获得罚球得分 2nd Quarter - Last Free Throw Made                                                        
            { "181-9", new int[] { 1291, 0, 0, 0 }},     //181:第三节最后获得罚球得分 3rd Quarter - Last Free Throw Made 
            { "261", new int[] { 744, 0, 0, 0 }},        //261:首先获得三分球 First 3 Points Made
            { "271", new int[] { 1295, 0, 0, 0 }},       //271:第二节首先获得三分球 2nd Quarter - First 3 Points Made
            { "281", new int[] { 1298, 0, 0, 0 }},       //281:第三节首先获得三分球 3rd Quarter - First 3 Points Made
            { "291", new int[] { 1301, 0, 0, 0 }},       //291:第四节首先获得三分球 4th Quarter - First 3 Points Made
            { "261-2", new int[] { 1070, 0, 0, 0 }},     //261:第二次获得三分球 Second 3 Points Made
            { "271-2", new int[] { 1296, 0, 0, 0 }},     //271:第二节第二次获得三分球 2nd Quarter - Second 3 Points Made
            { "281-2", new int[] { 1299, 0, 0, 0 }},     //281:第三节第二次获得三分球 3rd Quarter - Second 3 Points Made
            { "291-2", new int[] { 1302, 0, 0, 0 }},     //291:第四节第二次获得三分球 4th Quarter - Second 3 Points Made
            { "261-3", new int[] { 1071, 0, 0, 0 }},     //261:第三次获得三分球 Third 3 Points Made
            { "271-3", new int[] { 1297, 0, 0, 0 }},     //271:第二节第三次获得三分球 2nd Quarter - Third 3 Points Made
            { "281-3", new int[] { 1300, 0, 0, 0 }},     //281:第三节第三次获得三分球 3rd Quarter - Third 3 Points Made
            { "291-3", new int[] { 1303, 0, 0, 0 }},     //291:第四节第三次获得三分球 4th Quarter - Third 3 Points Made
            { "261-4", new int[] { 1146, 0, 0, 0 }},     //261:第四次获得三分球 Fourth 3 Points Made
            { "261-5", new int[] { 1147, 0, 0, 0 }},     //261:第五次获得三分球 Fifth 3 Points Made
			{ "105-5", new int[] { 1189, 0, 0, 0 }},     //105:第二节优先获得5分 2nd Quarter - Race to First 5 Points
			{ "105-10", new int[] { 1190, 0, 0, 0 }},    //105:第二节优先获得10分 2nd Quarter - Race to First 10 Points
			{ "105-15", new int[] { 1191, 0, 0, 0 }},    //105:第二节优先获得15分 2nd Quarter - Race to First 15 Points
			{ "106-5", new int[] { 1192, 0, 0, 0 }},     //106:第三节优先获得5分 3rd Quarter - Race to First 5 Points
			{ "106-10", new int[] { 1193, 0, 0, 0 }},    //106:第三节优先获得10分 3rd Quarter - Race to First 10 Points
			{ "106-15", new int[] { 1194, 0, 0, 0 }},    //106:第三节优先获得15分 3rd Quarter - Race to First 15 Points
			{ "107-5", new int[] { 1195, 0, 0, 0 }},     //107:第四节优先获得5分 4th Quarter - Race to First 5 Point
			{ "107-10", new int[] { 1196, 0, 0, 0 }},    //107:第四节优先获得10分 4th Quarter - Race to First 10 Points
			{ "107-15", new int[] { 1197, 0, 0, 0 }},    //107:第四节优先获得15分 4th Quarter - Race to First 15 Points
			{ "18", new int[] { 0, 1066, 0, 0 }},      	 //19:下半主隊總得分 Second Half Home Team Total Points (OE:1067)
			{ "19", new int[] { 0, 1068, 0, 0 }},      	 //19:下半客隊總得分 Second Half Away Team Total Points (OE:1069)
			{ "35", new int[] { 1134, 1135, 0, 0 }},   	 //35:罰球得分總數 球队罚球得分总数 Total Free Throws Scored (OE:1136)
			{ "34", new int[] { 874, 875, 0, 0 }},     	 //34:蓋帽總數 Total Blocks (OE:876)
			{ "36", new int[] { 1143, 1144, 0, 0 }},   	 //36:總失誤 球队总失误 Total Turnovers (OE:1145)	
            /*
            此區無NBB對應玩法
            new int[] { 1126, 0, 0, 0 },         //1st Quarter Last Basket 
            new int[] { 1129, 0, 0, 0 },         //2nd Quarter First Basket
            new int[] { 1130, 0, 0, 0 },         //1st Half Last Basket
            new int[] { 1131, 0, 0, 0 },         //2nd Half First Basket
            new int[] { 1132, 0, 0, 0 },      	 //3rd Quarter Last Basket
            new int[] { 1133, 0, 0, 0 },      	 //4th Quarter First Basket
            new int[] { 1294, 0, 0, 0 },      	 //第四节最后获得罚球得分 4th Quarter - Last Free Throw Made
            new int[] { 1286, 0, 0, 0 },      	 //第二节第一次犯规 2nd Quarter - First Foul    
            new int[] { 1289, 0, 0, 0 },      	 //第三节第一次犯规 3rd Quarter - First Foul  
            new int[] { 1292, 0, 0, 0 },      	 //第四节第一次犯规 4th Quarter - First Foul   
            new int[] { 745, 0, 0, 0 }},         //最后获得三分球 Last 3 Points Made     
             */
        };

        /// <summary>
        /// 解析目前需要聯盟
        /// </summary>
        /// <param name="html"></param>
        public static void ParserLeague(string html)
        {
            //匹配所有 \x 後面跟著兩位十六進制數字 
            string pattern = @"\\x([0-9A-Fa-f]{2})";

            // \x 這個字元在 JSON 字符串中被視為不正確的轉義序列，將其轉換成 \u 形式
            string result = Regex.Replace(html, pattern, match =>
            {
                string hexValue = match.Groups[1].Value;
                int decimalValue = Convert.ToInt32(hexValue, 16);
                return $@"\u{decimalValue:X4}";
            });

            Match mleague = Regex.Match(result, @"\(\'tournaments\'\,(.*?)\);");

            if (mleague.Success)
            {
                ConcurrentDictionary<decimal, string> tmpDictLeague = new ConcurrentDictionary<decimal, string>();
                JArray jArray = JsonConvert.DeserializeObject<JArray>(Regex.Unescape(mleague.Result("$1").Replace("\\", "\\\\").Replace("\'", "\\'")));

                foreach (var token in jArray)
                {
                    if (token.Count() == 11)
                    {
                        string league = token[1].ToString();

                        //過濾聯盟
                        if ((league.Contains("NBA") && !league.Contains("WNBA")))
                        {
                            //排除指定聯盟
                            if (!Config.FilterLeagueHashSet.Any(filter => league.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                if (decimal.TryParse(token[0].ToString(), out decimal leagueNum))
                                {
                                    tmpDictLeague.TryAdd(leagueNum, league);
                                }
                            }
                        }
                    }
                }

                dictLeague = tmpDictLeague;
            }
        }

        /// <summary>
        /// SBO 第一層解析 新增主賽事
        /// </summary>
        /// <param name="html">來源內容</param>
        /// <param name="threadInfo">執行緒資訊</param>
        /// <param name="sendId">非同步要求流水號</param>
        /// <returns>新增、更新的資料ID串列</returns>
        public static void Parser(string html, long sendId, GpType gpType)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> tmpGameDic = new ConcurrentDictionary<string, GameInfo>();
                //暫存來源gid關聯字典
                ConcurrentDictionary<decimal, string> tmpMainDic = new ConcurrentDictionary<decimal, string>();
                //暫存玩法字典
                ConcurrentDictionary<decimal, HashSet<decimal>> tmpDictPlay = new ConcurrentDictionary<decimal, HashSet<decimal>>();
                //暫存內頁HashSet
                HashSet<decimal> tmpMoreHashSet = new HashSet<decimal>();
                //暫存來源Id對應請求key
                ConcurrentDictionary<decimal, string> tmpRequestKey = new ConcurrentDictionary<decimal, string>();

                //匹配所有 \x 後面跟著兩位十六進制數字 
                string pattern = @"\\x([0-9A-Fa-f]{2})";

                // \x 這個字元在 JSON 字符串中被視為不正確的轉義序列，將其轉換成 \u 形式
                string result = Regex.Replace(html, pattern, match =>
                {
                    string hexValue = match.Groups[1].Value;
                    int decimalValue = Convert.ToInt32(hexValue, 16);
                    return $@"\u{decimalValue:X4}";
                });

                JArray jArrays = JsonConvert.DeserializeObject<JArray>(Regex.Unescape(result.Replace("\\", "\\\\").Replace("\'", "\\'")));

                if (jArrays.Count < 3) { return; }

                JArray jArray = JsonConvert.DeserializeObject<JArray>(jArrays[2].ToString());

                foreach (JToken token in jArray)
                {
                    //來源分層ZD會給1 DS會給2
                    string type = gpType == GpType.Zd ? "1" : "2";

                    if (token.Count() < 2) { continue; }

                    if (type == token[0].ToString())
                    {
                        JArray tks = JsonConvert.DeserializeObject<JArray>(token[1].ToString());

                        foreach (var tk in tks)
                        {
                            JArray datas = JsonConvert.DeserializeObject<JArray>(tk.ToString());

                            if (datas.Count < 3) { continue; }

                            if (decimal.TryParse(datas[1].ToString(), out decimal leagueNum) && dictLeague.TryGetValue(leagueNum, out string sourceLeague))
                            {
                                JArray data = JsonConvert.DeserializeObject<JArray>(datas[2].ToString());

                                if (data.Count != 12) { continue; }

                                if (decimal.TryParse(data[0].ToString(), out decimal sourceMatchId))
                                {
                                    GameInfo gameInfo = new GameInfo(sourceMatchId.ToString(), sendId);
                                    //設定GP類型
                                    gameInfo.GpType = gpType;
                                    //賽事類型
                                    gameInfo.GType = (byte)(gpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                                    //設定聯盟名稱
                                    gameInfo.League.Name = sourceLeague.Trim();
                                    //是否視為NBA賽事
                                    bool isNBA = !Config.CheckNbaExcludeName(gameInfo.League.Name);
                                    //設定隊伍名稱
                                    string teamA = data[1].ToString().Trim();
                                    string teamB = data[2].ToString().Trim();
                                    ConvertTools.ExchangeValue(ref teamA, ref teamB, isNBA);
                                    gameInfo.TeamA.Name = teamA;
                                    gameInfo.TeamB.Name = teamB;
                                    //防止兩隊名稱一樣
                                    if (gameInfo.TeamA.Name.Equals(gameInfo.TeamB.Name)) { continue; }

                                    //設定預設後綴
                                    if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
                                    {
                                        gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Cn;
                                        gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                                        gameInfo.Suffix.Id = suffixId;
                                    }

                                    if (DateTime.TryParse(data[5].ToString(), out DateTime dt))
                                    {
                                        gameInfo.SetGameDate(dt);
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    if (gpType == GpType.Zd)
                                    {
                                        //來源未給比分、狀態
                                        gameInfo.Score.SetScore(false, 0, 0);
                                        gameInfo.Status = 5;
                                    }

                                    gameInfo.Home = isNBA ? 2 : 1;

                                    string key = gameInfo.GetGameDictKey();
                                    //蒐集來源全場gid
                                    tmpMainDic.TryAdd(sourceMatchId, key);
                                    //playNames索引位置
                                    int playIndex = -1;
                                    Match leaguePlayReg = Regex.Match(sourceLeague, @"\s-\s(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                                    if (leaguePlayReg.Success)
                                    {
                                        playIndex = Array.IndexOf(playNames, leaguePlayReg.Result("$1").Trim().Replace(" ", "-").ToLower());
                                    }

                                    //只保留一筆同帳務隊伍比賽
                                    if (tmpGameDic.TryAdd(key, gameInfo))
                                    {
                                        //來源目錄額外抓取玩法處理
                                        if (playIndex > 0)
                                        {
                                            tmpDictPlay.GetOrAdd(playIndex, new HashSet<decimal>()).Add(sourceMatchId);
                                            tmpMoreHashSet.Add(playIndex);
                                            tmpRequestKey.TryAdd(playIndex, sourceLeague.Replace(" ", "-"));
                                        }
                                        //一般處理
                                        else
                                        {
                                            tmpMoreHashSet.Add(sourceMatchId);
                                            tmpRequestKey.TryAdd(sourceMatchId, $"{sourceLeague.Replace(" ", "-")}/{sourceMatchId}/{data[1].ToString().Replace(" ", "-")}-对-{data[2].ToString().Replace(" ", "-")}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //緩存比對處理
                foreach (KeyValuePair<string, GameInfo> kvp in tmpGameDic)
                {
                    //存在才處理
                    if (Config.gameDic.TryGetValue(kvp.Key, out GameInfo getGame))
                    {
                        getGame.Compare(kvp.Value, gpType);

                        //判斷內頁關聯ID是否還存在, 不存在則移除走關閉流程
                        foreach (KeyValuePair<decimal, long[]> tmpkvp in getGame.MoreDataDic)
                        {
                            //來源目錄額外抓取玩法處理
                            if (tmpDictPlay.TryGetValue(tmpkvp.Key, out HashSet<decimal> tmpHashSet))
                            {
                                foreach (decimal sourceId in tmpHashSet)
                                {
                                    if (!tmpMainDic.ContainsKey(sourceId))
                                    {
                                        getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);
                                    }
                                }
                            }
                            //一般處理
                            else if (!tmpMainDic.ContainsKey(tmpkvp.Key))
                            {
                                getGame.MoreDataDic.TryRemove(tmpkvp.Key, out long[] x);
                            }
                        }
                    }
                    //不存在直接添加
                    else
                    {
                        Config.gameDic.TryAdd(kvp.Key, kvp.Value.DeepClone());
                    }
                }

                #region 更新緩存

                //不存在GP類型則添加
                MoreDic.TryAdd(gpType, new HashSet<decimal>());
                DictMain.TryAdd(gpType, new ConcurrentDictionary<decimal, string>());
                RequestKey.TryAdd(gpType, new ConcurrentDictionary<decimal, string>());
                DictPlay.TryAdd(gpType, new ConcurrentDictionary<decimal, HashSet<decimal>>());
                //更新緩存
                DictPlay[gpType] = tmpDictPlay;
                MoreDic[gpType] = tmpMoreHashSet;
                DictMain[gpType] = tmpMainDic;
                RequestKey[gpType] = tmpRequestKey;

                #endregion
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, gpType.ToString() + Environment.NewLine + ex.ToString());
                throw new Exception("SBO-" + gpType.ToString().ToUpper() + "來源內容解析失敗");
            }
        }

        /// <summary>
        /// SBO內頁解析
        /// </summary>
        /// <param name="data">數據</param>
        /// <param name="sendId">發送流水號</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="sourceMatchId">來源賽事Id</param>
        public static void MoreParser(string data, long sendId, GpType gpType, decimal sourceMatchId)
        {
            try
            {
                //暫存賽事字典
                ConcurrentDictionary<string, GameInfo> dictGame = new ConcurrentDictionary<string, GameInfo>();
                GpType tmpGpType = gpType & (GpType.Ds | GpType.Zd | GpType.Zp);

                //匹配所有 \x 後面跟著兩位十六進制數字 
                string pattern = @"\\x([0-9A-Fa-f]{2})";

                // \x 這個字元在 JSON 字符串中被視為不正確的轉義序列，將其轉換成 \u 形式
                string result = Regex.Replace(data, pattern, match =>
                {
                    string hexValue = match.Groups[1].Value;
                    int decimalValue = Convert.ToInt32(hexValue, 16);
                    return $@"\u{decimalValue:X4}";
                });

                JArray jArrays = JsonConvert.DeserializeObject<JArray>(Regex.Unescape(result.Replace("\\", "\\\\").Replace("\'", "\\'")));

                //防護
                if (jArrays.Count <= 2 || !jArrays[2].Any() || jArrays[2][0].Count() <= 1 || !jArrays[2][0][1].Any()) { return; }

                foreach (JToken sourceData in jArrays[2][0][1])
                {
                    //防護
                    if (sourceData.Count() <= 4) { continue; }
                    //取出來源賽事ID
                    if (!decimal.TryParse(sourceData[2][0].ToString(), out decimal sourceId)) { continue; }
                    //取出賽事
                    if (DictMain.TryGetValue(tmpGpType, out ConcurrentDictionary<decimal, string> tmpDictMain)
                        && tmpDictMain.TryGetValue(sourceId, out string gameKey)
                        && Config.gameDic.TryGetValue(gameKey, out GameInfo outGameInfo))
                    {
                        //複製出賽事基本訊息
                        GameInfo gameInfo = outGameInfo.CloneBase();
                        //是否視為NBA賽事
                        bool isNBA = !Config.CheckNbaExcludeName(gameInfo.League.Name);

                        JToken playInfos = sourceData[4];

                        if (!playInfos.Children().Any()) { continue; }

                        foreach (KeyValuePair<string, int[]> playMethod in playMethodNum)
                        {
                            JToken[] sourcePlayInfo = playInfos.Children().Where(x => x.Count() > 2 && x[1].Count() > 5 && int.TryParse(x[1][0].ToString(), out int playInt) && playMethod.Value.Contains(playInt)).ToArray();

                            if (!sourcePlayInfo.Any()) { continue; }

                            for (int sw = 0; sw < playMethod.Value.Length; sw++)
                            {
                                int playNum = playMethod.Value[sw];
                                string[] spilt = playMethod.Key.Split('-');
                                int special = Convert.ToInt16(spilt[0]);

                                special = Config.TeamTotalIsChangeSpecials(special, isNBA);

                                if (playNum == 0) { continue; }

                                JToken[] pkplInfo = sourcePlayInfo.Where(x => x[1][0].ToString() == playNum.ToString()).ToArray();

                                if (!pkplInfo.Any()) { continue; }

                                switch (sw)
                                {
                                    case 0:
                                        //轉換成自定義解析物件
                                        List<PlateInfo> rfInfos = ConverPlateInfo(pkplInfo);

                                        //ZD : 排序最平水,  DS:全場上半 (下拉選單選三盤:抓取鴛鴦盤  下拉選單選一盤:排序最平水)
                                        List<PlateInfo> results = Config.sameCount == 2 && gameInfo.GpType != GpType.Zd && (special == 0 || special == 1) ? SetDsSameMatch(rfInfos, sw, special) : rfInfos.OrderBy(x => Math.Abs(x.Ratio_H - x.Ratio_A)).ToList();

                                        //ZD抓一盤,  DS:全場上半 (下拉選單選三盤:抓取鴛鴦盤  下拉選單選一盤:抓一盤)
                                        int countRF = Config.sameCount == 2 && gameInfo.GpType != GpType.Zd && (special == 0 || special == 1) ? results.Count : 1;

                                        for (int i = 0; i < countRF; i++)
                                        {
                                            //全場RF
                                            if (Config.SetKindType(gameInfo.GpType, special, 1, out int kTypeRF))
                                            {
                                                byte setType = spilt.Length == 2 ? Convert.ToByte(spilt[1]) : gameInfo.GetSetType(kTypeRF);
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType, sourceMatchId);
                                                kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));

                                                PlateInfo rf = results[i];
                                                //來源會給下方隊伍PK
                                                int strong = rf.Plate < 0 ? 1 : 0;
                                                if (!isNBA) { strong = strong == 1 ? 0 : 1; }
                                                decimal pk = Math.Abs(rf.Plate);
                                                decimal pla = rf.Ratio_H - 1;
                                                decimal plb = rf.Ratio_A - 1;
                                                ConvertTools.ExchangeValue(ref pla, ref plb, isNBA);

                                                if (pla > 0 && plb > 0)
                                                {
                                                    //設置數據
                                                    kInfo.SetLine(pk, strong);
                                                    bInfoA.SetOdds(decimal.Floor(pla * 100m) / 100m);
                                                    bInfoB.SetOdds(decimal.Floor(plb * 100m) / 100m);
                                                    kInfo.SetIsOpen();
                                                }

                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = special * 100 + setType;
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                            }
                                        }

                                        break;
                                    case 1:
                                        //轉換成自定義解析物件
                                        List<PlateInfo> ouInfos = ConverPlateInfo(pkplInfo);

                                        //ZD : 排序最平水,  DS:全場上半 (下拉選單選三盤:抓取鴛鴦盤  下拉選單選一盤:排序最平水)
                                        results = Config.sameCount == 2 && gameInfo.GpType != GpType.Zd && (special == 0 || special == 1) ? SetDsSameMatch(ouInfos, sw, special) : ouInfos.OrderBy(x => Math.Abs(x.Ratio_H - x.Ratio_A)).ToList();

                                        //ZD抓一盤,  DS:全場上半 (下拉選單選三盤:抓取鴛鴦盤  下拉選單選一盤:抓一盤)
                                        int countOU = Config.sameCount == 2 && gameInfo.GpType != GpType.Zd && (special == 0 || special == 1) ? results.Count : 1;

                                        for (int i = 0; i < countOU; i++)
                                        {
                                            //全場OU
                                            if (Config.SetKindType(gameInfo.GpType, special, 2, out int kTypeOU))
                                            {
                                                byte setType = spilt.Length == 2 ? Convert.ToByte(spilt[1]) : gameInfo.GetSetType(kTypeOU);
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType, sourceMatchId);
                                                kInfo.IsSourceFollow = gameInfo.GpType == GpType.Zd ? Config.isZdOnlyPK ? 1 : 3 : Config.isDsOnlyPK ? 1 : 3;
                                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeOU, setType, 2));

                                                PlateInfo ou = results[i];
                                                decimal pk = Math.Abs(ou.Plate);
                                                decimal pla = ou.Ratio_H - 1;
                                                decimal plb = ou.Ratio_A - 1;

                                                if (pla > 0 && plb > 0)
                                                {
                                                    //設置數據
                                                    kInfo.SetLine(pk);
                                                    bInfoA.SetOdds(decimal.Floor(pla * 100m) / 100m);
                                                    bInfoB.SetOdds(decimal.Floor(plb * 100m) / 100m);
                                                    kInfo.SetIsOpen();
                                                }

                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = special * 100 + setType;
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                            }
                                        }

                                        break;
                                    case 2:
                                        int type = gameInfo.GpType == GpType.Zd ? 1 : 3;

                                        //全場1*2
                                        if (Config.SetKindType(gameInfo.GpType, special, type, out int kTypeDY))
                                        {
                                            byte setType = 1;

                                            //生成物件
                                            KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDY, setType, sourceMatchId);
                                            BInfo bInfoA = new BInfo(Config.GetBType(kTypeDY, setType, 1));
                                            BInfo bInfoB = new BInfo(Config.GetBType(kTypeDY, setType, 2));

                                            JToken dy = pkplInfo[0];
                                            //生成gpdm並新增對應字典
                                            decimal gpdm = special * 100 + setType;

                                            if (decimal.TryParse(dy[2][0].ToString(), out decimal tmpPla) && decimal.TryParse(dy[2][1].ToString(), out decimal tmpPlb))
                                            {
                                                decimal pla = tmpPla - 1;
                                                decimal plb = tmpPlb - 1;
                                                ConvertTools.ExchangeValue(ref pla, ref plb, isNBA);

                                                if (pla > 0 && plb > 0)
                                                {
                                                    //設置數據
                                                    bInfoA.SetOdds(decimal.Floor(pla * 100m) / 100m);
                                                    bInfoB.SetOdds(decimal.Floor(plb * 100m) / 100m);
                                                    kInfo.SetIsOpen();

                                                    if (gameInfo.GpType == GpType.Zd)
                                                    {
                                                        // ZD參數設置"勾只跟PK"，DY最平水需要抓DY
                                                        //"勾只跟PK"來源【全場】最平水RF PK為0.5，此時抓取獨贏
                                                        if (Config.isZdOnlyPK &&
                                                        gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo) &&
                                                        gpdmInfo.TryGetValue(kTypeDY, out KInfo kInfoRf) && kInfoRf.IsOpen &&
                                                        (kInfoRf.BInfoDic.TryGetValue(bInfoA.BType, out BInfo tmpBIfoA) &&
                                                        kInfoRf.BInfoDic.TryGetValue(bInfoB.BType, out BInfo tmpBIfoB) &&
                                                        Math.Abs(tmpBIfoA.Odds - tmpBIfoB.Odds) > Math.Abs(pla - plb) ||
                                                        (special == 0 && kInfoRf.Line == 500)))
                                                        {
                                                            kInfo.IsSourceFollow = 1;
                                                            //更新kInfo
                                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                            //來源【全場】PK為0.5轉DY，若DY低水低於0.74則關盤
                                                            kInfo.SetIsOpen(!(special == 0 && kInfoRf.Line == 500 && Math.Min(pla, plb) < 0.74m));
                                                            gameInfo.ObjDic[gpdm][kTypeDY] = kInfo;
                                                        }
                                                        //ZD參數設置"未勾只跟PK"，無RF需抓DY
                                                        else if (!Config.isZdOnlyPK &&
                                                                (!gameInfo.ObjDic.Keys.Contains(gpdm) ||
                                                                (gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo _gpdmInfo) &&
                                                                (!_gpdmInfo.Keys.Contains(kTypeDY) || (_gpdmInfo.TryGetValue(kTypeDY, out KInfo _kInfoRf) && !_kInfoRf.IsOpen)))))
                                                        {
                                                            //更新kInfo
                                                            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                            gameInfo.ObjDic[gpdm][kTypeDY] = kInfo;
                                                        }
                                                    }
                                                }
                                            }

                                            if (gameInfo.GpType != GpType.Zd)
                                            {
                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeDY, kInfo);
                                            }
                                        }

                                        break;
                                    case 3:
                                        //全場OE
                                        if (Config.SetKindType(gameInfo.GpType, special, 4, out int kTypeOE))
                                        {
                                            byte setType = spilt.Length == 2 ? Convert.ToByte(spilt[1]) : gameInfo.GetSetType(kTypeOE);

                                            //限定setType只能有一個
                                            if (setType == 1)
                                            {
                                                //生成物件
                                                KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType, sourceMatchId);
                                                BInfo bInfoA = new BInfo(Config.GetBType(kTypeOE, setType, 1));
                                                BInfo bInfoB = new BInfo(Config.GetBType(kTypeOE, setType, 2));
                                                decimal odds_O = 0m;
                                                decimal odds_E = 0m;

                                                //手動設置OE 
                                                if ((gameInfo.GpType != GpType.Zd && Config.isOpenDsZpOe) || (gameInfo.GpType == GpType.Zd && Config.isOpenZdOe))
                                                {
                                                    if (gameInfo.GpType == GpType.Zd)
                                                    {
                                                        odds_O = odds_E = decimal.Floor((Config.zdOERatio) * 100m) / 100m;
                                                    }
                                                    else
                                                    {
                                                        odds_O = odds_E = decimal.Floor((Config.dsOERatio) * 100m) / 100m;
                                                    }

                                                    kInfo.IsManual = true;
                                                }
                                                else
                                                {
                                                    JToken oe = pkplInfo[0];

                                                    if (decimal.TryParse(oe[2][0].ToString(), out decimal tmpPla) && decimal.TryParse(oe[2][1].ToString(), out decimal tmpPlb))
                                                    {
                                                        odds_O = tmpPla - 1;
                                                        odds_E = tmpPlb - 1;
                                                    }
                                                }

                                                //設置數據
                                                bInfoA.SetOdds(odds_O);
                                                bInfoB.SetOdds(odds_E);
                                                kInfo.SetIsOpen(odds_O > 0 && odds_E > 0);

                                                //放進字典
                                                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                                //生成gpdm並新增對應字典
                                                decimal gpdm = special * 100 + setType;
                                                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                                                gameInfo.ObjDic[gpdm].TryAdd(kTypeOE, kInfo);
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }

                        #region 補上玩法缺少的項目

                        foreach (KeyValuePair<decimal, GpdmInfo> tmp in gameInfo.ObjDic.OrderBy(e => e.Key))
                        {
                            decimal gpdm = tmp.Key;
                            GpdmInfo gpdmDic = tmp.Value;

                            if (gpdmDic.Any())
                            {
                                //取得specials值
                                int specials = GetSpecials(gpdm);

                                //RF
                                if (Config.SetKindType(gpType, specials, 1, out int kTypeRF) && !gpdmDic.ContainsKey(kTypeRF))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeRF);
                                    //生成物件
                                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeRF, setType, sourceMatchId);
                                    BInfo bInfoA = new BInfo(Config.GetBType(kTypeRF, setType, 1));
                                    BInfo bInfoB = new BInfo(Config.GetBType(kTypeRF, setType, 2));
                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                    kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeRF, kInfo);
                                }

                                //OU
                                if (Config.SetKindType(gpType, specials, 2, out int kTypeOU) && !gpdmDic.ContainsKey(kTypeOU))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeOU);
                                    //生成物件
                                    KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOU, setType, sourceMatchId);
                                    BInfo bInfoO = new BInfo(Config.GetBType(kTypeOU, setType, 1));
                                    BInfo bInfoU = new BInfo(Config.GetBType(kTypeOU, setType, 2));
                                    //放進字典
                                    kInfo.BInfoDic.TryAdd(bInfoO.BType, bInfoO);
                                    kInfo.BInfoDic.TryAdd(bInfoU.BType, bInfoU);
                                    gameInfo.ObjDic[gpdm].TryAdd(kTypeOU, kInfo);
                                }

                                //DY
                                if (Config.SetKindType(gpType, specials, 3, out int kTypeDY) && !gpdmDic.ContainsKey(kTypeDY))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeDY);

                                    //限定setType只能有一個
                                    if (setType == 1)
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeDY, setType, sourceMatchId);
                                        BInfo bInfoA = new BInfo(Config.GetBType(kTypeDY, setType, 1));
                                        BInfo bInfoB = new BInfo(Config.GetBType(kTypeDY, setType, 2));
                                        //放進字典
                                        kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                                        kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);
                                        gameInfo.ObjDic[gpdm].TryAdd(kTypeDY, kInfo);
                                    }
                                }

                                //OE
                                if (Config.SetKindType(gpType, specials, 4, out int kTypeOE) && !gpdmDic.ContainsKey(kTypeOE))
                                {
                                    byte setType = gameInfo.GetSetType(kTypeOE);

                                    //限定setType只能有一個
                                    if (setType == 1)
                                    {
                                        //生成物件
                                        KInfo kInfo = new KInfo(gpType.ToString(), sendId, kTypeOE, setType, sourceMatchId);
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
                        dictGame.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
                    }
                }

                //緩存比對處理
                if (dictGame.Count > 0)
                {
                    foreach (KeyValuePair<string, GameInfo> kvp in dictGame)
                    {
                        //存在才處理
                        if (Config.gameDic.TryGetValue(kvp.Key, out GameInfo getGame))
                        {
                            getGame.Compare(kvp.Value, gpType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, $"{gpType}, id{sourceMatchId}{Environment.NewLine}{ex.ToString()}");
            }
        }

        /// <summary>
        /// 從已知Gpdm倒推取得Specials值
        /// </summary>
        /// <param name="gpdm">賽事Gpdm</param>
        /// <returns>返回Specials</returns>
        public static int GetSpecials(decimal gpdm)
        {
            int specials = -1;
            int.TryParse(decimal.Truncate(gpdm / 100m).ToString(), out specials);
            return specials;
        }

        /// <summary>
        /// 來源數據轉換為自定義解析物件
        /// </summary>
        /// <param name="pkplInfo">來源PK PL 清單</param>
        /// <returns></returns>
        protected static List<PlateInfo> ConverPlateInfo(JToken[] pkplInfo)
        {
            List<PlateInfo> plateInfos = new List<PlateInfo>();

            for (int i = 0; i < pkplInfo.Count(); i++)
            {
                JToken play = pkplInfo[i];

                if (decimal.TryParse(play[1][5].ToString(), out decimal tmpPk) &&
                    decimal.TryParse(play[2][0].ToString(), out decimal tmpPla) &&
                    decimal.TryParse(play[2][1].ToString(), out decimal tmpPlb))
                {
                    plateInfos.Add(new PlateInfo(tmpPk, tmpPla, tmpPlb));
                }
            }

            return plateInfos;
        }

        /// <summary>
        /// 利用主要PK和差距獲得對應PK
        /// </summary>
        /// <param name="plateInfoList">PK PL 清單</param>
        /// <param name="special">玩法</param>
        /// <param name="play">0:RF 1:OU</param>
        /// <param name="mainPk">主要PK</param>
        /// <param name="difPk">至少差距PK</param>
        /// <param name="isGood">是否找好盤, 反之找壞盤</param>
        /// <returns></returns>
        protected static PlateInfo GetNearPk(List<PlateInfo> plateInfoList, int special, int play, decimal mainPk, decimal difPk, bool isGood)
        {
            //好盤往下減、壞盤往上加
            decimal targetPk = isGood ? Math.Abs(mainPk) - difPk : Math.Abs(mainPk) + difPk;

            for (int i = 0; i < plateInfoList.Count; i++)
            {
                PlateInfo tmpPlateInfo = plateInfoList[i];
                decimal tmpPk = Math.Abs(tmpPlateInfo.Plate);

                //0 PK 跳過 || 全場RF不抓取0.5盤
                if (tmpPk == 0m || (special == 0 && play == 0 && tmpPk == 0.5m))
                {
                    continue;
                }

                //抓取達條件第一筆PlateInfo
                if ((isGood && tmpPk <= targetPk) || (!isGood && tmpPk >= targetPk))
                {
                    return tmpPlateInfo;
                }
            }

            return null;
        }

        /// <summary>
        /// 設定DS鴛鴦盤
        /// </summary>
        /// <param name="plateInfos">PK PL 清單</param>
        /// <param name="play">0:RF 1:OU</param>
        /// <param name="special">玩法</param>
        /// <returns></returns>
        protected static List<PlateInfo> SetDsSameMatch(List<PlateInfo> plateInfos, int play, int special)
        {
            //輸出設定後的結果順序
            List<PlateInfo> results = new List<PlateInfo>();

            if (plateInfos.Count > 0)
            {
                //固定只抓來源前三盤
                List<PlateInfo> plays = plateInfos.Take(3).ToList();

                //A盤位置 (三盤取中間, 不足三盤則取第一盤)
                int selectedIndex = plays.Count == 3 ? plays.Count / 2 : 0;
                PlateInfo plateInfoA = plays[selectedIndex];
                decimal mainPk = plateInfoA.Plate;
                results.Add(plateInfoA);
                plays.RemoveAt(selectedIndex);

                //RF
                if (play == 0)
                {
                    //B盤
                    //當RT為主隊(B隊PK > 0) : 需抓取A盤壞100%的平輸盤,跳過A盤同RT平手
                    //當RT為客隊(B隊PK < 0) : 需抓取A盤好100%的平輸盤,跳過A盤同RT平手
                    PlateInfo plateInfoB = GetNearPk(plays, special, play, mainPk, 0.5m, mainPk < 0 ? true : false);
                    if (plateInfoB != null) { results.Add(plateInfoB); }

                    //C盤
                    //當RT為主隊(B隊PK > 0) : 需抓取A盤好100%的平輸盤,跳過A盤同RT平手
                    //當RT為客隊(B隊PK < 0) : 需抓取A盤壞100%的平輸盤,跳過A盤同RT平手
                    PlateInfo plateInfoC = GetNearPk(plays, special, play, mainPk, 0.5m, mainPk < 0 ? false : true);
                    if (plateInfoC != null) { results.Add(plateInfoC); }
                }
                //OU
                else if (play == 1)
                {
                    //B盤：需抓取A盤壞100%的平輸盤                    
                    PlateInfo plateInfoB = GetNearPk(plays, special, play, mainPk, 0.5m, false);
                    if (plateInfoB != null) { results.Add(plateInfoB); }

                    //C盤：需抓取A盤好100%的平輸盤
                    PlateInfo plateInfoC = GetNearPk(plays, special, play, mainPk, 0.5m, true);
                    if (plateInfoC != null) { results.Add(plateInfoC); }
                }
            }

            return results;
        }
    }
}
