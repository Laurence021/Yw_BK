#region 功能修改描述與時間

/*
    描述：365DS解析(配合YwCore[1.0.1.32])
    日期：2020-02-25

    描述：365物件架構調整
    日期：2020-03-12

    描述：來源數據格式異動，對應調整解析(附加盤玩法尚未觀察調整)
    日期：2020-04-06

    描述：調整隊伍交換邏輯判斷方式
    日期：2020-11-30
    
    描述：調整日光節約時間判斷方式
    日期：2021-03-26

    描述：排除電競賽事
    日期：2022-03-17
*/

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source
{
    public partial class Parser365
    {
        //Pd撈取條件
        private static string[] pdCondition = {
            "E1453",    //0比赛投注
            "E928",     //1上半场
            "E941",     //2第1赛节
            "E1448",    //3第2赛节
            "E1032",    //4第3赛节E1032
            "E1449",    //5第4赛节E1449
            "E181286",  //6附加的比赛总得分E181286
            "E181285",  //7附加让分E181285
            "E1503",    //8首先获得20分E1503
            "E181335",  //9球队总得分E181335
            "E1731",    //10球队总分 - 奇偶数
            "E180013",  //11比赛总分 奇/偶数
            "E180170",  //12第1节 - 总分 單/雙
            "E180171",  //13第2节 - 总分 奇/偶
            "E181366",  //14第3节 - 总分 奇/偶
            "E181368",  //15第4节 - 总分 奇/偶
            "E181204",  //16上半场 - 奇/偶数
            "E181159",  //17上半场球队总得分E181159
            "E181220"   //18第1赛节球队总得分E181220
        };

        //GMT時區
        private static TimeZoneInfo gmtTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

        /// <summary>
        /// 365 Ds 外頁解析
        /// </summary>
        /// <param name="sendId">請求流水號</param>
        /// <param name="html">外頁內容</param>
        /// <returns></returns>
        public static SourceData GetInnerUrlParam(long sendId, string html, FormatData365 formatData)
        {
            //內頁數據資料
            SourceData sourceData = new SourceData(sendId);
            string[] changeTeamAlliance = new string[] { "WNBA", "NBA", "NCAAB" };

            try
            {
                EvModel ev = formatData.DataToModelForDs(html, null);

                foreach (MgModel mg in ev.MgModels.Where(a => !a.Na.Contains("电竞")))//2022/3/16提出電競賽事不跟
                {
                    sourceData.AllianceInfos.TryAdd(mg.It, new AllianceGroup() { Alliance = mg.Na, AllianceNo = mg.It, ChangeTeam = changeTeamAlliance.Any(a => mg.Na.Contains(a)) && !mg.Na.Contains("WNCAAB") });

                    List<PaModel> paModels = mg.maModels.SelectMany(a => a.paModels).Where(a => pdCondition.Any(b => a.Pd.Contains(b))).ToList();

                    if (paModels.Any())
                    {
                        sourceData.AllianceInfos.TryAdd(mg.It, new AllianceGroup());

                        foreach (PaModel paModel in paModels)
                        {
                            //if (paModel.Pd.Contains("#E181285#") || paModel.Pd.Contains("#E181286#"))
                            //{
                            //    foreach (GameInfo365 game in Config.gameDic.Values.Where(a => a.League.Name == mg.Na))
                            //    {
                            //        if (!string.IsNullOrEmpty(game.Half_FI))
                            //        {
                            //            string pd = paModel.Pd + $"G{game.Half_FI}#H0#I0#";
                            //            sourceData.AllianceInfos[mg.It].BatchInfos.Add(new BatchInfo() { Alliance = mg.Na, Pd = pd, AllianceNo = mg.It });
                            //        }
                            //    }
                            //}
                            //else
                            //{
                            sourceData.AllianceInfos[mg.It].BatchInfos.Add(new BatchInfo() { Alliance = mg.Na, Pd = paModel.Pd, AllianceNo = mg.It });
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, GpType.Ds.ToString() + Environment.NewLine + ex.ToString() + Environment.NewLine + "來源資料:" + html);
            }

            return sourceData;
        }

        /// <summary>
        /// 365 DS主要數據解析
        /// </summary>
        /// <param name="request">365DS請求物件</param>
        /// <param name="dataInfo">數據源</param>
        /// <returns>是否需要入庫</returns>
        public static bool DsParser(Request365 request, DataInfo dataInfo)
        {
            AllianceGroup allianceGroup = request.GetBatchData(dataInfo.SendId, dataInfo.Data);
            List<EvModel> evModels = allianceGroup.GetEvModels();
            //暫存賽事字典
            ConcurrentDictionary<string, GameInfo365> tmpGameDic = new ConcurrentDictionary<string, GameInfo365>();
            EvModel tmpEv = new EvModel();

            try
            {
                foreach (EvModel evModel in evModels.OrderBy(a => Array.FindIndex(pdCondition, (b) => { return a.ClModel.It.Contains($"#{b}#"); })))
                {
                    tmpEv = evModel;
                    int pdIndex = Array.FindIndex(pdCondition, (a) => { return evModel.ClModel.It.Contains($"#{a}#"); });
                    //TK值(OD解密值)
                    string tk = evModel.ClModel.Tk;

                    switch (pdIndex)
                    {
                        //比赛投注E1453
                        case 0:
                            ParseGameLines(0, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //上半场E928
                        case 1:
                            ParseGameLines(1, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第1赛节E941
                        case 2:
                            ParseGameLines(3, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第2赛节E1448
                        case 3:
                            ParseGameLines(4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第3赛节E1032
                        case 4:
                            ParseGameLines(5, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第4赛节E1449
                        case 5:
                            ParseGameLines(6, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //首先获得20分E1503
                        case 8:
                            ParseGetScoreFirstOrGameLineOe(101, 1, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //球队总得分E181335
                        case 9:
                            ParseTeamTotalScore(0, allianceGroup, 2, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //球队总分 - 奇偶数E1731、
                        case 10:
                            ParseTeamTotalScore(0, allianceGroup, 4, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //上半球队总得分E181159
                        case 17:
                            ParseTeamTotalScore(1, allianceGroup, 2, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第1节球队总得分E181220
                        case 18:
                            ParseTeamTotalScore(3, allianceGroup, 2, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //比赛总分 奇/偶数E180013
                        case 11:
                            ParseGetScoreFirstOrGameLineOe(0, 4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第1节 - 总分 單/雙E180170
                        case 12:
                            ParseGetScoreFirstOrGameLineOe(3, 4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第2节 - 总分 單/E180171
                        case 13:
                            ParseGetScoreFirstOrGameLineOe(4, 4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第3节 - 总分 單/雙E181366
                        case 14:
                            ParseGetScoreFirstOrGameLineOe(5, 4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //第4节 - 总分 單/雙E181368
                        case 15:
                            ParseGetScoreFirstOrGameLineOe(6, 4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //上半场 - 奇/偶数E181204
                        case 16:
                            ParseGetScoreFirstOrGameLineOe(1, 4, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //TODO:附加的比赛总得分E181286
                        case 6:
                            ParseSameOu(0, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                        //TODO:附加让分E181285
                        case 7:
                            ParseSameRf(0, allianceGroup, evModel, tmpGameDic, dataInfo.SendId);
                            break;
                    }
                }

                foreach (KeyValuePair<string, GameInfo365> game in tmpGameDic)
                {
                    if (game.Value.DataId >= 0)
                    {
                        game.Value.DataId = dataInfo.SendId;
                    }

                    //存在才處理
                    if (Config.gameDic.TryGetValue(game.Key, out GameInfo getGame))
                    {
                        getGame.Compare(game.Value, GpType.Ds);
                    }
                    //不存在直接添加
                    else
                    {
                        Config.gameDic.TryAdd(game.Key, game.Value);
                    }
                }

            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, GpType.Ds.ToString() + Environment.NewLine + ex.ToString() + Environment.NewLine + "來源資料:" + tmpEv.ClModel.It);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析比赛投注、上半场、單節
        /// </summary>
        /// <param name="specials">玩法編號</param>
        /// <param name="allianceGroup">聯盟數據集合</param>
        /// <param name="evModel">EV包</param>
        /// <param name="gameDic">賽事總字典</param>
        /// <param name="sendId">請求流水號</param>
        private static void ParseGameLines(int specials, AllianceGroup allianceGroup, EvModel evModel, ConcurrentDictionary<string, GameInfo365> gameDic, long sendId)
        {
            string tk = evModel.ClModel.Tk;
            MgModel mgModel = evModel.MgModels.SingleOrDefault(a => !string.IsNullOrEmpty(a.Pc));
            MaModel maGame = mgModel?.maModels.FirstOrDefault();

            if (maGame == null)
            {
                return;
            }

            //RF玩法群組
            MaModel maRf = mgModel.maModels.SingleOrDefault(a => a.Na == "让分");
            var rfGroup = maRf?.paModels.Select((value, index) => new { pairNum = index / 2, value }).GroupBy(a => a.pairNum);
            //OU玩法群組
            MaModel maOu = mgModel.maModels.SingleOrDefault(a => a.Na == "总分");
            var ouGroup = maOu?.paModels.Select((value, index) => new { pairNum = index / 2, value }).GroupBy(a => a.pairNum); ;
            //ML玩法群組
            MaModel maMl = mgModel.maModels.SingleOrDefault(a => a.Na == "强弱盘赔率");
            var mlGroup = maMl?.paModels.Select((value, index) => new { pairNum = index / 2, value }).GroupBy(a => a.pairNum);

            //依照對戰組合分組
            List<PaModel> gameGroup = maGame.paModels.Where(a => !string.IsNullOrEmpty(a.Bc)).ToList();

            for (int i = 0; i < gameGroup.Count; i++)
            {
                PaModel paGame = gameGroup[i];
                string[] team = new string[] { paGame.Na, paGame.N2 };
                ConvertTools.ExchangeValue(ref team[0], ref team[1], allianceGroup.ChangeTeam);
                //設定賽事資訊
                GameInfo365 gameInfo = SetGameInfo(evModel.It, allianceGroup.Alliance, team[0], team[1], paGame.Bc);

                //篩選已開賽或賽事資訊不完整
                if (gameInfo == null || paGame.Fs != "0")
                {
                    continue;
                }

                //沿用賽事暫存(數據頁面分散)
                if (gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo365 tmpGameInfo))
                {
                    gameInfo = tmpGameInfo;
                }

                //FI為頁面數據關聯欄位
                gameInfo.FI = paGame.Fi;

                if (specials == 1)
                {
                    //上半FI用來請求附加玩法數據
                    gameInfo.Half_FI = paGame.Fi;
                }

                #region RF區域

                if (rfGroup != null)
                {
                    var rfModels = rfGroup.ElementAtOrDefault(i);

                    if (rfModels != null && rfModels.Count() == 2)
                    {
                        PaModel paModelA = rfModels.ElementAt(0).value;
                        PaModel paModelB = rfModels.ElementAt(1).value;
                        ConvertTools.ExchangeValue(ref paModelA, ref paModelB, allianceGroup.ChangeTeam);

                        if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kType))
                        {
                            byte setType = gameInfo.GetSetType(kType);
                            KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType);
                            //生成gpdm並新增對應字典
                            long gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        }
                    }
                }

                #endregion

                #region OU區域

                if (ouGroup != null)
                {
                    var ouModels = ouGroup.ElementAtOrDefault(i);

                    if (ouModels != null && ouModels.Count() == 2)
                    {
                        PaModel paModelA = ouModels.ElementAt(0).value;
                        PaModel paModelB = ouModels.ElementAt(1).value;

                        if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kType))
                        {
                            byte setType = gameInfo.GetSetType(kType);
                            KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType);
                            //生成gpdm並新增對應字典
                            long gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        }
                    }
                }

                #endregion

                #region ML區域

                if (mlGroup != null)
                {
                    var mlModels = mlGroup.ElementAtOrDefault(i);

                    if (mlModels != null && mlModels.Count() == 2)
                    {
                        PaModel paModelA = mlModels.ElementAt(0).value;
                        PaModel paModelB = mlModels.ElementAt(1).value;
                        ConvertTools.ExchangeValue(ref paModelA, ref paModelB, allianceGroup.ChangeTeam);

                        if (Config.SetKindType(gameInfo.GpType, specials, 3, out int kType))
                        {
                            byte setType = gameInfo.GetSetType(kType);
                            KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType);
                            //生成gpdm並新增對應字典
                            long gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        }
                    }
                }

                #endregion

                SetDefaultKinfo(gameInfo, specials);
                gameDic.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
            }
        }

        /// <summary>
        /// 解析首先获得20分、比赛总分 奇/偶、第1节 - 总分 單/雙
        /// </summary>
        /// <param name="specials">玩法編號</param>
        /// <param name="type">玩法</param>
        /// <param name="allianceGroup">聯盟數據集合</param>
        /// <param name="evModel">EV包</param>
        /// <param name="gameDic">賽事總字典</param>
        /// <param name="sendId">請求流水號</param>
        private static void ParseGetScoreFirstOrGameLineOe(int specials, int type, AllianceGroup allianceGroup, EvModel evModel, ConcurrentDictionary<string, GameInfo365> gameDic, long sendId)
        {
            string tk = evModel.ClModel.Tk;
            MgModel mgModel = evModel.MgModels.SingleOrDefault(a => !string.IsNullOrEmpty(a.Pc));
            MaModel maGame = mgModel?.maModels.FirstOrDefault();

            if (maGame == null)
            {
                return;
            }

            //剔除已開賽賽事
            var gameGroup = maGame.paModels.Where(a => !string.IsNullOrEmpty(a.Bc)).ToList();

            for (int i = 0; i < gameGroup.Count; i++)
            {
                PaModel paGame = gameGroup[i];
                string[] teamName = new string[] { paGame.Na, paGame.N2 };

                ConvertTools.ExchangeValue(ref teamName[0], ref teamName[1], allianceGroup.ChangeTeam);
                //設定賽事資訊
                GameInfo365 gameInfo = SetGameInfo(evModel.It, allianceGroup.Alliance, teamName[0], teamName[1], paGame.Bc);

                if (gameInfo == null || paGame.Fs != "0")
                {
                    continue;
                }

                //沿用賽事暫存(數據頁面分散)
                if (gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo365 tmpGameInfo))
                {
                    gameInfo = tmpGameInfo;
                }

                //FI為頁面數據關聯欄位
                gameInfo.FI = paGame.Fi;

                var maPlay = mgModel.maModels.Where(a => a.Sy?.ToUpper() == "EB");

                if (maPlay.Count() == 2)
                {
                    List<PaModel> paList = maPlay.SelectMany(a => a.paModels).Where(a => a.Fi == gameInfo.FI || a.Nf == gameInfo.NF).ToList();

                    if (paList.Count == 2)
                    {
                        PaModel paModelA = paList[0];
                        PaModel paModelB = paList[1];

                        if (type == 1 || type == 3)
                        {
                            ConvertTools.ExchangeValue(ref paModelA, ref paModelB, allianceGroup.ChangeTeam);
                        }

                        if (Config.SetKindType(gameInfo.GpType, specials, type, out int kType))
                        {
                            //優先取得20則SETTYPE為20
                            byte setType = (byte)(type == 4 ? 1 : 20);
                            KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType);
                            //生成gpdm並新增對應字典
                            long gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        }
                    }
                }

                SetDefaultKinfo(gameInfo, specials);
                gameDic.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
            }
        }

        /// <summary>
        /// 解析球队总得分、球队总分 - 奇偶数
        /// </summary>
        /// <param name="sence">場次</param>
        /// <param name="allianceGroup">聯盟數據集合</param>
        /// <param name="type">玩法</param>
        /// <param name="evModel">EV包</param>
        /// <param name="gameDic">賽事總字典</param>
        /// <param name="sendId">請求流水號</param>
        private static void ParseTeamTotalScore(int sence, AllianceGroup allianceGroup, int type, EvModel evModel, ConcurrentDictionary<string, GameInfo365> gameDic, long sendId)
        {
            string tk = evModel.ClModel.Tk;
            List<MgModel> mgGames = evModel.MgModels.Where(a => !string.IsNullOrEmpty(a.Na) && !string.IsNullOrEmpty(a.Fi)).ToList();

            if (mgGames == null)
            {
                return;
            }

            foreach (MgModel mgModel in mgGames)
            {
                string[] teamName = mgModel.Na.Split(new string[] { " v ", " @ ", " vs ", " V ", " VS " }, StringSplitOptions.RemoveEmptyEntries);

                if (teamName.Length != 2)
                {
                    continue;
                }

                ConvertTools.ExchangeValue(ref teamName[0], ref teamName[1], allianceGroup.ChangeTeam);
                //設定賽事資訊
                GameInfo365 gameInfo = SetGameInfo(evModel.It, allianceGroup.Alliance, teamName[0], teamName[1], mgModel.Bc);

                if (gameInfo == null)
                {
                    continue;
                }

                //沿用賽事暫存(數據頁面分散)
                if (gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo365 tmpGameInfo))
                {
                    gameInfo = tmpGameInfo;
                }

                gameInfo.FI = mgModel.Fi;

                var maOu = mgModel.maModels.Where(a => a.Fi == gameInfo.FI);

                foreach (MaModel ma in maOu)
                {
                    //AB隊判斷
                    int specials = ma.Na == gameInfo.TeamA.Name ? 14 : 15;
                    //判斷節次
                    specials = specials + sence * 2;

                    if (ma.paModels.Count == 2)
                    {
                        PaModel paModelA = ma.paModels[0];
                        PaModel paModelB = ma.paModels[1];

                        if (Config.SetKindType(gameInfo.GpType, specials, type, out int kType))
                        {
                            byte setType = (byte)1;
                            KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType);
                            //生成gpdm並新增對應字典
                            long gpdm = specials * 100 + setType;
                            gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                            gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        }

                        SetDefaultKinfo(gameInfo, specials);
                    }
                }

                gameDic.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
            }
        }

        /// <summary>
        /// 解析附加让分
        /// </summary>
        /// <param name="specials">玩法編號</param>
        /// <param name="allianceGroup">聯盟數據集合</param>
        /// <param name="evModel">EV包</param>
        /// <param name="gameDic">賽事總字典</param>
        /// <param name="sendId">請求流水號</param>
        private static void ParseSameRf(int specials, AllianceGroup allianceGroup, EvModel evModel, ConcurrentDictionary<string, GameInfo365> gameDic, long sendId)
        {
            string tk = evModel.ClModel.Tk;
            List<MgModel> mgGames = evModel.MgModels.Where(a => !string.IsNullOrEmpty(a.Bc)).ToList();

            if (mgGames == null)
            {
                return;
            }

            foreach (MgModel mgModel in mgGames)
            {
                string[] teamName = mgModel.Na.Split(new string[] { " v ", " @ ", " vs ", " V ", " VS " }, StringSplitOptions.RemoveEmptyEntries);

                if (teamName.Length != 2)
                {
                    continue;
                }

                ConvertTools.ExchangeValue(ref teamName[0], ref teamName[1], allianceGroup.ChangeTeam);
                //設定賽事資訊
                GameInfo365 gameInfo = SetGameInfo(evModel.It, allianceGroup.Alliance, teamName[0], teamName[1], mgModel.Bc);

                if (gameInfo == null)
                {
                    continue;
                }

                //沿用賽事暫存(數據頁面分散)
                if (gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo365 tmpGameInfo))
                {
                    gameInfo = tmpGameInfo;
                }

                gameInfo.FI = mgModel.Fi;

                //取得首盤資訊
                KInfo mainRf = gameInfo.ObjDic.SingleOrDefault(a => a.Key == 1).Value?.SingleOrDefault(a => a.Key == 12001).Value;
                //A隊 PL群組
                var modelA = mgModel.maModels.Where(a => a.Na == teamName[0])?.SelectMany(a => a.paModels).Select((value, index) => new { PairNum = index, Value = value });
                //B隊 PL群組
                var modelB = mgModel.maModels.Where(a => a.Na == teamName[1])?.SelectMany(a => a.paModels).Select((value, index) => new { PairNum = index, Value = value });
                //玩法資訊關聯集合<依照PK分組>
                var betGroup = mainRf != null && modelA != null && modelB != null ?
                               modelA.Join(modelB, a => a.PairNum, b => b.PairNum, (inner, result) =>
                                      {
                                          decimal.TryParse(inner.Value.Na, out decimal Pk);
                                          //首盤PK轉換
                                          decimal mainLine = mainRf.LineAt == 0 ? mainRf.Line : -mainRf.Line;
                                          return new { inner.PairNum, Pk, PlA = inner.Value, PlB = result.Value, Difference = Math.Abs(Pk * 1000 - mainLine) };
                                      }).OrderBy(a => a.Difference) : null;

                if (betGroup == null)
                {
                    return;
                }

                //排除首盤PK
                var selectRf = betGroup.Where(a => a.Difference != 0);

                //抓取前後盤
                for (int i = 0; i < selectRf.Count() && i < 2; i++)
                {
                    var rf = selectRf.ElementAt(i);
                    PaModel paModelA = rf.PlA;
                    PaModel paModelB = rf.PlB;

                    if (Config.SetKindType(gameInfo.GpType, specials, 1, out int kType))
                    {
                        byte setType = (byte)(i + 2);
                        KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType, rf.Pk);

                        //生成gpdm並新增對應字典
                        long gpdm = specials * 100 + setType;
                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                        gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        SetDefaultKinfo(gameInfo, specials, setType);
                    }
                }

                gameDic.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
            }
        }

        /// <summary>
        /// 解析附加的比赛总得分
        /// </summary>
        /// <param name="specials">玩法編號</param>
        /// <param name="allianceGroup">聯盟數據集合</param>
        /// <param name="evModel">EV包</param>
        /// <param name="gameDic">賽事總字典</param>
        /// <param name="sendId">請求流水號</param>
        private static void ParseSameOu(int specials, AllianceGroup allianceGroup, EvModel evModel, ConcurrentDictionary<string, GameInfo365> gameDic, long sendId)
        {
            string tk = evModel.ClModel.Tk;
            List<MgModel> mgGames = evModel.MgModels.Where(a => !string.IsNullOrEmpty(a.Bc)).ToList();

            if (mgGames == null)
            {
                return;
            }

            foreach (MgModel mgModel in mgGames)
            {
                string[] teamName = mgModel.Na.Split(new string[] { " v ", " @ ", " vs ", " V ", " VS " }, StringSplitOptions.RemoveEmptyEntries);

                if (teamName.Length != 2)
                {
                    continue;
                }

                ConvertTools.ExchangeValue(ref teamName[0], ref teamName[1], allianceGroup.ChangeTeam);
                //設定賽事資訊
                GameInfo365 gameInfo = SetGameInfo(evModel.It, allianceGroup.Alliance, teamName[0], teamName[1], mgModel.Bc);

                if (gameInfo == null)
                {
                    continue;
                }

                //沿用賽事暫存(數據頁面分散)
                if (gameDic.TryGetValue(gameInfo.GetGameDictKey(), out GameInfo365 tmpGameInfo))
                {
                    gameInfo = tmpGameInfo;
                }

                gameInfo.FI = mgModel.Fi;

                //取得首盤資訊
                KInfo mainOu = gameInfo.ObjDic.SingleOrDefault(a => a.Key == 1).Value?.SingleOrDefault(a => a.Key == 12002).Value;
                //標題群組
                var maTitle = mgModel.maModels.Where(a => a.Na.Trim() == "")?.SelectMany(a => a.paModels).Select((value, index) => new { PairNum = index, Value = value });
                //O PL群組
                var modelO = mgModel.maModels.Where(a => a.Na == "高于")?.SelectMany(a => a.paModels).Select((value, index) => new { PairNum = index, Value = value });
                //U PL群組
                var modelU = mgModel.maModels.Where(a => a.Na == "低于")?.SelectMany(a => a.paModels).Select((value, index) => new { PairNum = index, Value = value });
                //玩法資訊關聯集合<依照PK分組>
                var betGroup = mainOu != null && maTitle != null && modelO != null && modelU != null ?
                               maTitle.Join(modelO, a => a.PairNum, b => b.PairNum, (inner, result) => new { inner.PairNum, inner.Value.Na, PlA = result.Value })
                                      .Join(modelU, a => a.PairNum, b => b.PairNum, (inner, result) =>
                                      {
                                          decimal.TryParse(inner.Na, out decimal Pk);
                                          return new { inner.PairNum, Pk, inner.PlA, PlB = result.Value, Difference = Math.Abs(Pk * 1000 - mainOu.Line) };
                                      }).OrderBy(a => a.Difference) : null;

                if (betGroup == null)
                {
                    return;
                }

                //排除首盤PK
                var selectOu = betGroup.Where(a => a.Difference != 0);

                //抓取前後盤
                for (int i = 0; i < selectOu.Count() && i < 2; i++)
                {
                    var ou = selectOu.ElementAt(i);
                    PaModel paModelA = ou.PlA;
                    PaModel paModelB = ou.PlB;

                    if (Config.SetKindType(gameInfo.GpType, specials, 2, out int kType))
                    {
                        byte setType = (byte)(i + 2);
                        KInfo kInfo = SetKInfo(tk, setType, sendId, paModelA, paModelB, kType, ou.Pk);

                        //生成gpdm並新增對應字典
                        long gpdm = specials * 100 + setType;
                        gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                        gameInfo.ObjDic[gpdm].AddOrUpdate(kType, kInfo, (k, v) => kInfo);
                        SetDefaultKinfo(gameInfo, specials, setType);
                    }
                }

                gameDic.TryAdd(gameInfo.GetGameDictKey(), gameInfo);
            }
        }

        /// <summary>
        /// 設定GameInfo
        /// </summary>
        /// <param name="league">聯盟名</param>
        /// <param name="teamA">隊伍A</param>
        /// <param name="teamB">隊伍B</param>
        /// <param name="bc">來源開賽時間BC</param>
        /// <returns>GameInfo365</returns>
        private static GameInfo365 SetGameInfo(string id, string league, string teamA, string teamB, string bc)
        {
            GameInfo365 gameInfo = new GameInfo365(id);
            gameInfo.GType = (byte)GType.BkDs;
            gameInfo.GpType = GpType.Ds;
            gameInfo.League.Name = league;
            gameInfo.TeamA.Name = teamA;
            gameInfo.TeamB.Name = teamB;
            gameInfo.Home = 1;


            //開賽時間 (統一轉到UTC+0，利用時區資訊判斷是否為日光節約時間，時間取用失敗則不解析)
            if (long.TryParse(bc, out long lBc) && DateTime.TryParse($"{(lBc / 100 * 100).ToString("####/##/## ##:##:##")}Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime dt))
            {
                gameInfo.SetGameDate(gmtTimeZone.IsDaylightSavingTime(dt) ? dt.AddHours(7) : dt.AddHours(8));
            }
            else
            {
                return null;
            }

            Match suffixReg = Regex.Match(league, @"-\s?([1-9].*(分钟|mins))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            if (suffixReg.Success)
            {
                gameInfo.Suffix.Name = suffixReg.Result("$1").Trim();
                //聯盟
                gameInfo.League.Name = league.Replace(suffixReg.Value, "").Trim();
            }

            //設定預設後綴
            if (FrmSuffix.suffixList.TryGetValue(gameInfo.League.Name, out int suffixId) && suffixId != 0)
            {
                gameInfo.Suffix.Name = WebName.suffixDic[suffixId].Cn;
                gameInfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                gameInfo.Suffix.Id = suffixId;
            }

            return gameInfo;
        }

        /// <summary>
        /// 設置Kinfo
        /// </summary>
        /// <param name="tk">內容解密KEY</param>
        /// <param name="setType">SetType</param>
        /// <param name="sendId">請求流水號</param>
        /// <param name="paModelA">A隊PA包</param>
        /// <param name="paModelB">B隊PA包</param>
        /// <param name="kType">KType</param>
        /// <param name="pk">PK</param>
        /// <returns>KInfo物件</returns>
        private static KInfo SetKInfo(string tk, byte setType, long sendId, PaModel paModelA, PaModel paModelB, int kType, decimal? pk = null)
        {
            //生成物件
            KInfo kInfo = new KInfo(GpType.Ds.ToString(), sendId, kType, setType);
            BInfo bInfoA = new BInfo(Config.GetBType(kType, setType, 1));
            BInfo bInfoB = new BInfo(Config.GetBType(kType, setType, 2));



            decimal odds_A = CalculateOD(DecryptOD(paModelA.Od, tk));
            decimal odds_B = CalculateOD(DecryptOD(paModelB.Od, tk));

            //手動設置OE
            if (kInfo.PlayTyp == 8)
            {
                if (Config.isOpenDsZpOe)
                {
                    decimal oeRatio = Config.dsOERatio;
                    odds_A = odds_B = decimal.Floor((oeRatio) * 100m) / 100m;
                    kInfo.IsManual = true;
                }
            }

            if (odds_A <= 0 || odds_B <= 0)
            {
                odds_A = 0;
                odds_B = 0;
            }

            if (pk == null)
            {
                //RT
                int strong = 1;

                if (paModelA.Ha.Contains("-"))
                {
                    strong = 1;
                }
                else if (paModelB.Ha.Contains("-"))
                {
                    strong = 0;
                }

                //設置數據
                if (decimal.TryParse(paModelA.Ha, out decimal plate) && paModelA.Su != "2")
                {
                    kInfo.SetLine(Math.Abs(plate), strong);
                }
            }
            else
            {
                kInfo.SetLine(Math.Abs(pk.Value), kInfo.PlayTyp == 1 && pk.Value > 0 ? 0 : 1);
            }

            bInfoA.SetOdds(odds_A);
            bInfoB.SetOdds(odds_B);
            kInfo.SetIsOpen((paModelA.Su != "2" && paModelA.Su != "1") || kInfo.IsManual);
            //放進字典
            kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
            kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);

            if ((kInfo.PlayTyp & 3) > 0)
            {
                kInfo.IsSourceFollow = Config.isDsOnlyPK ? 1 : 3;
            }

            return kInfo;
        }

        //設定基礎Kinfo
        private static void SetDefaultKinfo(GameInfo365 gameInfo, int special, byte setType = 1)
        {
            int kType = 0;          //kTYPE
            int strong = 0;         //RT[1:A隊讓 0:B隊讓]
            decimal plate = 0;      //PK
            decimal plA = 0;        //A_PL
            decimal plB = 0;        //B_PL
            decimal gpdm = special; //GPDM
            bool isOpen = false;    //玩法開關

            foreach (string play in Config.GetKinfoArray(gameInfo.GpType, special, true))
            {
                //生成物件
                KInfo kInfo = new KInfo("", 0, 0);
                BInfo bInfoA = new BInfo(0);
                BInfo bInfoB = new BInfo(0);

                switch (play)
                {
                    case "RF":

                        //全場RF
                        if (Config.SetKindType(gameInfo.GpType, special, 1, out kType))
                        {
                            gpdm = special * 100 + setType;
                            //生成物件
                            kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
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
                    case "DX":

                        //全場OU
                        if (Config.SetKindType(gameInfo.GpType, special, 2, out kType))
                        {
                            gpdm = special * 100 + setType;
                            //生成物件
                            kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
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
                        if (Config.SetKindType(gameInfo.GpType, special, 3, out kType))
                        {
                            gpdm = special * 100 + setType;

                            //限定setType只能有一個
                            if (setType == 1)
                            {
                                //生成物件
                                kInfo = new KInfo(gameInfo.GpType.ToString("g"), 0, kType, setType);
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
                        }
                        else
                        {
                            return;
                        }

                        break;
                    case "DS":

                        //OE
                        if (Config.SetKindType(gameInfo.GpType, special, 4, out kType))
                        {
                            gpdm = special * 100 + setType;

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
                                if (Config.isOpenDsZpOe)
                                {
                                    decimal oeRatio = gameInfo.GpType == GpType.Zd ? Config.zdOERatio : Config.dsOERatio;
                                    odds_O = odds_E = decimal.Floor((oeRatio) * 100m) / 100m;
                                    kInfo.IsManual = true;
                                }
                                //跟來源OE
                                else
                                {
                                    //PL
                                    odds_O = plA;
                                    odds_E = plB;

                                    kInfo.IsManual = false;
                                }

                                //設置數據
                                bInfoA.SetOdds(odds_O);
                                bInfoB.SetOdds(odds_E);
                                kInfo.SetIsOpen(kInfo.IsManual || isOpen);
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

                //放進字典
                kInfo.BInfoDic.TryAdd(bInfoA.BType, bInfoA);
                kInfo.BInfoDic.TryAdd(bInfoB.BType, bInfoB);

                if ((kInfo.PlayTyp & 3) > 0)
                {
                    kInfo.IsSourceFollow = Config.isDsOnlyPK ? 1 : 3;
                }

                gameInfo.ObjDic.TryAdd(gpdm, new GpdmInfo(gpdm));
                gameInfo.ObjDic[gpdm].TryAdd(kType, kInfo);
            }

            return;
        }

        //OD解密
        private static string DecryptOD(string OD, string TK)
        {
            string realValue = "";

            if (string.IsNullOrEmpty(TK))
            {
                return OD;
            }

            int charCode1 = Convert.ToInt32(TK[0]);
            int charCode2 = Convert.ToInt32(TK[1]);
            char charKey = Convert.ToChar(charCode1 ^ charCode2);
            int charKeyCode = Convert.ToInt32(charKey);
            char realChar;

            for (int s = 0; s < OD.Length; s++)
            {
                realChar = Convert.ToChar(Convert.ToInt32(OD[s]) ^ charKeyCode);
                realValue += realChar;
            }

            return realValue;
        }

        /// 取得365資訊包對應值
        private static string GetValue(string data, string header)
        {
            Regex r = new Regex(string.Format("{0}", header) + "=(.*?)(?:;[A-Z]{2}|;$)");
            Match match = r.Match(data);

            if (match.Success)
            {
                return match.Result("$1");
            }
            else
            {
                return "";
            }
        }
    }
}
