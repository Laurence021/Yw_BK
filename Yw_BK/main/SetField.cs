#region 功能修改描述與時間

/*
    描述：設置區
    日期：2019-01-07

    描述：調整自動移操盤API功能判斷, 避免ID重複添加
    日期：2024-02-19

    描述：添加查詢主庫Game表f_multiStatus欄位, 調整時間狀態比分是否更新判斷
    日期：2023-09-22

    描述：移除ZD OE僅自己的防護邏輯能影響開關
    日期：2025-01-02

    描述：DS&ZD，不跨來源判斷時間
    日期：2024-12-27

    描述：365/MyStake 自動移滾新建Game阻擋條件調整
    日期：2025-05-06

    描述：添加查詢聯盟庫t_team表f_isNotOpen欄位, 調整不開放時不生成
    日期：2026-03-25
*/

#endregion

using Gp.Model;
using Gp.Service;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using Yw_BK.model;
using Yw_BK.model.logInfo;
using YwCore;
using YwCore.main;
using YwCore.model;
using YwCore.tools;

namespace Yw_BK.main
{
    /// <summary>
    /// 設置區
    /// </summary>
    public static class SetField
    {
        //UpdateOdds更新結束後處理委派事件
        private static event Action AfterUpdateHandle;

        #region 聯盟隊伍相關

        /// <summary>
        /// 設定聯盟
        /// </summary>
        /// <param name="gameList">同聯盟的賽事列表</param>
        /// <param name="isReCheck">是否再次檢查</param>
        public static void SetLeague(List<GameInfo> gameList, bool isReCheck = false)
        {
            //取第一筆賽事為基準
            GameInfo gameInfo = gameList[0];
            League league = new League();
            DateTime gameDate = gameInfo.GameDate == DateTime.MinValue ? gameInfo.GameDateFromSource : gameInfo.GameDate;

            //若有緩存則從緩存獲取
            if (!WebName.isRefresh
                && WebName.allyWebNameDic.TryGetValue(Config.lang, out var dic)
                && dic.TryGetValue(gameInfo.League.Name, out int tmpAllyId)
                && WebName.allyModeDic.TryGetValue(tmpAllyId, out AllyInfo allyInfo)
                && WebName.allyDic.TryGetValue(tmpAllyId, out NameInfo ally))
            {
                //取得聯盟模式、限額類型、聯盟名
                league.Id = tmpAllyId;
                league.LeagueMode = allyInfo.ModeId;
                league.LimitType = allyInfo.LimitType;
                league.PrioritySourceId = allyInfo.PrioritySourceId;
                league.FullName = ally.GetFullName();

                //開關有開, 且隊伍ID未發送API失敗過, JZ隊伍全語系名稱與緩存不一致
                if ((Common.apiSwitch & 8) == 8 &&
                    !Process.AllyApi.SendApiFailId.Contains(ally.Id) &&
                    !gameInfo.League.FullNameJZ.SequenceEqual(ally.GetFullName().Take(gameInfo.League.FullNameJZ.Length)))
                {
                    SelectLeague(gameInfo.League, gameInfo.GType, gameDate, out league);
                }
            }
            //複查不存在緩存則直接設為-1
            else if (isReCheck)
            {
                league.Id = -1;
            }
            //入庫查詢
            else
            {
                SelectLeague(gameInfo.League, gameInfo.GType, gameDate, out league);
            }

            //有查到ID
            if (league.Id > 0)
            {
                if ((Common.apiSwitch & 8) == 8)
                {
                    //ID未發送API失敗過, JZ全語系名稱與緩存不一致
                    if (!gameInfo.League.FullNameJZ.SequenceEqual(league.FullName.Take(gameInfo.League.FullNameJZ.Length)))
                    {
                        if (!Process.AllyApi.SendApiFailId.Contains(league.Id))
                        {
                            int nameChangeMark = 0; //(繁:1,簡:2,英:4,越:8,泰:16,印:32,31繁:64)

                            for (int i = 0; i < gameInfo.League.FullNameJZ.Length; i++)
                            {
                                nameChangeMark += !gameInfo.League.FullNameJZ[i].Equals(league.FullName[i]) ? (int)Math.Pow(2, i) : 0;
                            }

                            //轉換物件
                            AllyApiModel apiModel = new AllyApiModel(Config.sport,
                                gameInfo.League.FullNameJZ[0],
                                gameInfo.League.FullNameJZ[1],
                                gameInfo.League.FullNameJZ[2],
                                gameInfo.League.FullNameJZ[3],
                                gameInfo.League.FullNameJZ[4],
                                gameInfo.League.FullNameJZ[5],
                                gameInfo.League.FullNameJZ[6],
                                league.Id,
                                nameChangeMark);
                            Process.AllyApi.AddQueue(apiModel);
                        }

                        league.Id = -1;
                    }
                }
            }

            //設定聯盟資訊
            foreach (GameInfo info in gameList)
            {
                //聯盟Id不同則賽事Id清除
                if (info.League.Id > 0 && info.League.Id != league.Id)
                {
                    if (info.GId != 0)//有gid才需要關盤
                    {
                        //複製需要關閉的賽事出來再加進賽事字典走關閉流程
                        GameInfo closeGame = info.DeepClone();
                        closeGame.DataId = -1;
                        Config.gameDic.TryAdd($"{closeGame.GetGameDictKey()}_d", closeGame);
                    }

                    info.GameNotExistHandle();
                }

                info.League.Id = league.Id;
                info.League.LeagueMode = league.LeagueMode;
                info.League.LimitType = league.LimitType;
                info.League.FullName = league.FullName;
                info.League.PrioritySourceId = league.PrioritySourceId;
            }
        }

        /// <summary>
        /// 查詢DB取得聯盟資料
        /// </summary>
        /// <param name="ally">隊伍資訊</param>
        /// <param name="gType3">賽事gType</param>
        /// <param name="tmpLeague">暫存隊伍資訊</param> 
        public static void SelectLeague(League ally, int gType3, DateTime gameDate, out League tmpLeague)
        {
            tmpLeague = new League();

            //查詢聯盟Id及聯盟模式
            var result = Common.AllyServiceClient.SelectAllyId(new CommonRequest()
            {
                Lang = (int)Config.lang,
                SourceId = (int)Config.source,
                GType = (int)Config.sport,
                GType3 = gType3,
                ApType = 0,
                Name = ally.Name,
                GameDate = TimeStampHelper.ToUtcTimeStamp(gameDate)
            });

            if (result.Result.Count > 0)
            {
                var dr = result.Result.First();

                tmpLeague.FullName = new string[] { dr.NameTw,
                                                    dr.NameCn,
                                                    dr.NameEn,
                                                    dr.NameVi,
                                                    dr.NameTh,
                                                    dr.NameIdn,
                                                    dr.Name31};

                tmpLeague.Id = dr.AllyId;
                tmpLeague.LeagueMode = dr.ModeId;
                tmpLeague.LimitType = Convert.ToByte(dr.LimitType);
                tmpLeague.PrioritySourceId = Convert.ToByte(dr.PrioritySourceId);
            }
            else
            {
                if (string.IsNullOrEmpty(result.ErrorMsg))
                {
                    tmpLeague.Id = -1;
                }
                else
                {
                    tmpLeague = new League();
                    Display.SetDictInfo(Display.Type.Error, YwCore.Properties.Resources.LeagueOrTeamFaild);
                    LogFile.SetLog(LogMode.Error, $"{result.GetType().Name} => {result.ErrorMsg}");
                }
            }
        }


        /// <summary>
        /// 設定後綴
        /// </summary>
        /// <param name="gameList">須查詢後綴的賽事列表</param>
        /// <param name="isReCheck">是否再次檢查</param>
        public static void SetSuffix(List<GameInfo> gameList, bool isReCheck = false)
        {
            int suffixId = -1;
            string[] suffixFullName = new string[] { "", "", "", "", "", "", "" };
            byte suffixGroup = 0;
            //取第一筆賽事為基準
            GameInfo gameInfo = gameList[0];

            //若有緩存則從緩存獲取
            if (!WebName.isRefresh
                && WebName.suffixWebNameDic.TryGetValue(Config.lang, out var dic)
                && dic.TryGetValue(gameInfo.Suffix.Name, out int tmpSuffixId)
                && WebName.suffixDic.TryGetValue(tmpSuffixId, out SuffixInfo suffixInfo))
            {
                //取得後綴名、聯盟模式組別
                suffixId = tmpSuffixId;
                suffixFullName = suffixInfo.GetFullName();
                suffixGroup = suffixInfo.Group;
            }
            //複查不存在緩存則直接設為-1
            else if (isReCheck)
            {
                suffixId = -1;
            }
            //入庫查詢
            else
            {
                //查詢後綴Id
                var result = Common.AllyServiceClient.SelectSuffix(new CommonRequest()
                {
                    Lang = (int)Config.lang,
                    SourceId = (int)Config.source,
                    GType = (int)Config.sport,
                    GType3 = gameInfo.GType,
                    ApType = 0,
                    Name = gameInfo.Suffix.Name,                 
                    IsAddUnknownTeam = (Common.apiSwitch & 1) != 1 ? 1 : 0,
                    GameDate = TimeStampHelper.ToUtcTimeStamp(gameInfo.GameDate == DateTime.MinValue ? gameInfo.GameDateFromSource : gameInfo.GameDate)
                });

                if (result.Result.Count > 0)
                {
                    var dr = result.Result.First();

                    suffixId = dr.SuffixId;
                    suffixFullName = new string[] { dr.NameTw,
                                                    dr.NameCn,
                                                    dr.NameEn,
                                                    dr.NameVi,
                                                    dr.NameTh,
                                                    dr.NameIdn,
                                                    dr.Name31};
                    suffixGroup = Convert.ToByte(dr.Group);
                }
                else
                {
                    if (string.IsNullOrEmpty(result.ErrorMsg))
                    {
                        suffixId = -1;
                    }
                    else
                    {
                        Display.SetDictInfo(Display.Type.Error, YwCore.Properties.Resources.LeagueOrTeamFaild);
                        LogFile.SetLog(LogMode.Error, $"{result.GetType().Name} => {result.ErrorMsg}");
                        return;
                    }
                }
            }

            //設定後綴Id
            foreach (GameInfo info in gameList)
            {
                //後綴Id不同則賽事Id清除
                if (info.Suffix.Id > 0 && info.Suffix.Id != suffixId)
                {
                    if (info.GId != 0)//有gid才需要關盤
                    {
                        //複製需要關閉的賽事出來再加進賽事字典走關閉流程
                        GameInfo closeGame = info.DeepClone();
                        closeGame.DataId = -1;
                        Config.gameDic.TryAdd($"{closeGame.GetGameDictKey()}_d", closeGame);
                    }

                    info.GameNotExistHandle();
                }

                info.Suffix.Id = suffixId;
                info.Suffix.FullName = suffixFullName;
                info.Suffix.Group = suffixGroup;
            }
        }

        /// <summary>
        /// 設定隊伍
        /// </summary>
        /// <param name="gameList">須查詢隊伍的賽事列表</param>
        /// <param name="isReCheck">是否再次檢查</param>
        public static void SetTeam(List<GameInfo> gameList, bool isReCheck = false)
        {
            foreach (GameInfo game in gameList)
            {
                //是否隊伍Id改變
                bool isChange = false;
                DateTime gameDate = game.GameDate == DateTime.MinValue ? game.GameDateFromSource : game.GameDate;
                SetTeamData(game.TeamA, isReCheck, game.GType, game.League.FullName[0], gameDate, ref isChange);
                SetTeamData(game.TeamB, isReCheck, game.GType, game.League.FullName[0], gameDate, ref isChange);

                //隊伍Id不同則賽事Id清除
                if (isChange)
                {
                    if (game.GId != 0)//有gid才需要關盤
                    {
                        //複製需要關閉的賽事出來再加進賽事字典走關閉流程
                        GameInfo closeGame = game.DeepClone();
                        closeGame.DataId = -1;
                        Config.gameDic.TryAdd($"{closeGame.GetGameDictKey()}_d", closeGame);
                    }

                    game.GameNotExistHandle();
                }
            }
        }

        /// <summary>
        /// 從緩存或DB取得隊伍資料
        /// </summary>
        /// <param name="TeamName"></param>
        /// <param name="isReCheck">是否再次檢查</param>
        /// <param name="gType">賽事gType</param>
        /// <param name="allyName">聯盟繁體語系</param>
        /// <param name="isChange">隊伍Id是否變動</param>
        /// <returns></returns>
        public static void SetTeamData(Team team, bool isReCheck, int gType, string allyName, DateTime gameDate, ref bool isChange)
        {
            //空字串跳過
            if (string.IsNullOrEmpty(team.Name)) { return; }
            //暫存隊伍Id
            int tmpId = team.Id;
            Team tmpTeamInfo = new Team();

            //查詢緩存
            if (!WebName.isRefresh
                && WebName.teamWebNameDic.TryGetValue(Config.lang, out var dic)
                && dic.TryGetValue(team.Name, out int tmpTeamId)
                && WebName.teamDic.TryGetValue(tmpTeamId, out TeamInfo nameInfo))
            {
                tmpTeamInfo.Id = nameInfo.Id;
                tmpTeamInfo.FullName = nameInfo.GetFullName();
                tmpTeamInfo.IsNotOpen = nameInfo.IsNotOpen;

                //開關有開, 且隊伍ID未發送API失敗過, JZ隊伍全語系名稱與緩存不一致
                if ((YwCore.Common.apiSwitch & 1) == 1 &&
                    !Process.TeamApi.SendApiFailId.Contains(nameInfo.Id) &&
                    !team.FullNameJZ.SequenceEqual(nameInfo.GetFullName()))
                {
                    //入庫查詢
                    SelectTeam(team, gType, allyName, gameDate, out tmpTeamInfo);
                }
            }
            //複查不存在緩存則直接設為-1
            else if (isReCheck)
            {
                team.Id = -1;
            }
            //入庫查詢
            else
            {
                SelectTeam(team, gType, allyName, gameDate, out tmpTeamInfo);
            }

            //開關有開, 有查到ID
            if ((YwCore.Common.apiSwitch & 1) == 1 && tmpTeamInfo.Id > 0)
            {
                //隊伍ID未發送API失敗過, JZ隊伍全語系名稱與緩存不一致
                if (!team.FullNameJZ.SequenceEqual(tmpTeamInfo.FullName))
                {
                    if (!Process.TeamApi.SendApiFailId.Contains(tmpTeamInfo.Id))
                    {
                        int nameChangeMark = 0; //(繁:1,簡:2,英:4,越:8,泰:16,印:32,31繁:64)

                        for (int i = 0; i < 7; i++)
                        {
                            nameChangeMark += !team.FullNameJZ[i].Equals(tmpTeamInfo.FullName[i]) ? (int)Math.Pow(2, i) : 0;
                        }

                        //轉換物件
                        TeamApiModel teamApiModel = new TeamApiModel(TeamApiModel.ActionMode.Modify, Config.sport, team.FullNameJZ[0], team.FullNameJZ[1], team.FullNameJZ[2], team.FullNameJZ[3], team.FullNameJZ[4],
                                                                 team.FullNameJZ[5], team.FullNameJZ[6], tmpTeamInfo.Id, allyName, gType, tmpTeamInfo.CountryId, nameChangeMark);
                        Process.TeamApi.AddQueue(teamApiModel);
                    }

                    team.Id = -1;
                }
                //全語系名稱與JZ一致直接設定
                else
                {
                    team.Id = tmpTeamInfo.Id;
                    team.FullName = tmpTeamInfo.FullName;
                    team.IsNotOpen = tmpTeamInfo.IsNotOpen;
                }
            }
            else if (tmpTeamInfo.Id > 0)
            {
                team.Id = tmpTeamInfo.Id;
                team.FullName = tmpTeamInfo.FullName;
                team.IsNotOpen = tmpTeamInfo.IsNotOpen;
            }
            else
            {
                team.Id = -1;
            }

            //隊伍Id不同則賽事Id清除
            if (tmpId > 0 && tmpId != team.Id) { isChange = true; }
        }

        /// <summary>
        /// 查詢DB取得隊伍資料
        /// </summary>
        /// <param name="team">隊伍資訊</param>
        /// <param name="gType">賽事gType</param>
        /// <param name="allyName">聯盟繁體語系</param> 
        /// <param name="tmpTeamInfo">暫存隊伍資訊</param> 
        public static void SelectTeam(Team team, int gType, string allyName, DateTime gameDate, out Team tmpTeamInfo)
        {
            tmpTeamInfo = new Team();
            //執行SQL查詢
            var result = Common.AllyServiceClient.SelectTeam(new CommonRequest()
            {
                Lang = (int)Config.lang,
                SourceId = (int)Config.source,
                GType = (int)Config.sport,
                GType3 = gType,
                ApType = 0,
                Name = team.Name,
                AllyName = allyName,
                IsAddUnknownTeam = (Common.apiSwitch & 1) != 1 ? 1 : 0,
                GameDate = TimeStampHelper.ToUtcTimeStamp(gameDate)
            });

            //有數據才設置
            if (result.Result.Count > 0)
            {
                var dr = result.Result.First();

                tmpTeamInfo.Id = dr.Id;
                tmpTeamInfo.IsNotOpen = dr.IsNotOpen;
                tmpTeamInfo.FullName = new string[] { dr.NameTw,
                                                    dr.NameCn,
                                                    dr.NameEn,
                                                    dr.NameVi,
                                                    dr.NameTh,
                                                    dr.NameIdn,
                                                    dr.Name31};
            }
            else
            {
                //查無隊伍資訊, 開關有開, 尚未打API新增過
                if ((YwCore.Common.apiSwitch & 1) == 1 &&
                    !Process.TeamApi.SendApiAddTeamCN.Contains(team.FullNameJZ[1]))
                {
                    //轉換物件
                    TeamApiModel teamApiModel = new TeamApiModel(TeamApiModel.ActionMode.Add, Config.sport, team.FullNameJZ[0], team.FullNameJZ[1], team.FullNameJZ[2], team.FullNameJZ[3], team.FullNameJZ[4],
                                                                 team.FullNameJZ[5], team.FullNameJZ[6], 0, allyName, gType);
                    Process.TeamApi.AddQueue(teamApiModel);
                }
            }
        }

        #endregion

        /// <summary>
        /// 建立賽事(t_game)
        /// </summary>
        /// <param name="gameList">賽事列表</param>
        /// <param name="gpType">GP類型</param>
        public static void CreateGame(List<GameInfo> gameList, GpType gpType)
        {
            //亂數列表(避免同一批次有重複的亂數)
            HashSet<int> randomSet = new HashSet<int>();
            //暫存賽事資料表
            BkGameCacheCreateRequestV10 request = new BkGameCacheCreateRequestV10() { SourceId = (int)Config.source };

            foreach (GameInfo gameInfo in gameList)
            {
                if (gameInfo.Id != 0)
                {
                    continue;
                }
                else
                {
                    //自動移滾功能開啟
                    if (gpType == GpType.Zd && Config.AutoCreateZdGame)
                    {
                        //OE自設以外的玩法全關不需新建
                        if (!gameInfo.ObjDic.Values.Any(a => a.Values.Where(a => !a.IsManual).Any(b => b.IsOpen)))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        //365/MyStake以外，玩法全關不需新建
                        if (!(Config.source == Source.BET365 || Config.source == Source.MyStake))
                        {
                            if (!gameInfo.ObjDic.Values.Any(a => a.Values.Any(b => b.IsOpen)))
                            {
                                continue;
                            }
                        }
                    }
                }

                if (SetCreateGameRow(gameInfo, CommonLogic.GetRandomInt(randomSet), out BkGameCacheCreateDto row))
                {
                    request.Data.Add(row);
                }
            }

            if (request.Data.Count > 0)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                //執行SQL，取回Id
                var response = Config.GameCacheServiceClient.BatchInsertV10(request);
                //加入佇列
                LogProcess.AddQueue(new CreateGameCacheLog(Config.source, gpType.ToString(), request.Data, response.Result, stopwatch.Elapsed, Config.entrance));

                if (string.IsNullOrEmpty(response.ErrorMsg))
                {
                    foreach (var row in response.Result)
                    {
                        //key存在才設置
                        if (Config.gameDic.TryGetValue(row.Key, out GameInfo game))
                        {
                            game.Id = row.Id;
                            game.GId = row.GId;
                            game.StatusDB = Convert.ToByte(row.Status);
                            game.IsMoveMain = row.IsCreateGame;
                            game.PrioritySourceId = row.PrioritySourceId;
                            game.AllyModeId = row.AllyModeId;

                            if (row.Result == 2)
                            {
                                //原本已存在則立即更新
                                game.IsUpdate = true;
                                game.IsUpdateSwitch |= 3;
                            }

                            //添加gid關聯字典
                            Config.gIdDic.TryAdd(row.GId, row.Key);
                        }
                    }
                }
                else
                {
                    Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }
            }
        }

        /// <summary>
        /// 建立玩法(t_odds)
        /// </summary>
        /// <param name="gameList">賽事列表</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="isCreateHT">生成上半場判斷用開關</param>
        public static void CreateOdds(List<GameInfo> gameList, GpType gpType, bool isCreateHT = false)
        {
            List<long> tranGIdDic = new List<long>();

            List<List<BkOddsCacheCreateDto>> dtList = new List<List<BkOddsCacheCreateDto>>();
            List<BkOddsCacheCreateDto> oddsList = new List<BkOddsCacheCreateDto>();

            foreach (GameInfo gameInfo in gameList)
            {
                //有f_id、f_gId且狀態不為過帳才處理
                long gId = gameInfo.GId;

                if (gameInfo.Id > 0 && gId > 0 && gameInfo.StatusDB < 49)
                {
                    foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                    {
                        GpdmInfo tmpDic = gpdmPair.Value;

                        if (Config.BlockList.BlockGpdm(gameInfo, tmpDic))
                        {
                            continue;
                        }

                        //是否有來源玩法開放
                        bool isOpen = tmpDic.IsGenerate || (from kInfo in tmpDic.Values
                                                            where kInfo.IsOpen
                                                            select kInfo).Any();

                        //來源玩法全關不建立 (JZ 和 365ZD 和 MyStake不受此限制) (生成上半場不判斷)
                        if (!isOpen && !isCreateHT)
                        {
                            if (Config.source == Source.BET365)
                            {
                                if (gpType != GpType.Zd)
                                {
                                    continue;
                                }
                            }
                            else if (Config.source != Source.JZ && Config.source != Source.MyStake)
                            {
                                continue;
                            }
                        }

                        foreach (KInfo kInfo in tmpDic.Values)
                        {
                            //判斷KModeDefault是否有勾上不開放玩法
                            if (!KModeDefault.Dic.TryGetValue($"{gameInfo.AllyModeId}￠{gameInfo.Suffix.Group}￠{gameInfo.GType}￠{kInfo.KType}", out bool isNotOpen) || isNotOpen)
                            {
                                continue;
                            }

                            foreach (BInfo bInfo in kInfo.BInfoDic.Values.Where(a => a.Id == 0))
                            {
                                string key = $"{gameInfo.League.LeagueMode}￠{gameInfo.Suffix.Group}￠{gameInfo.GType}￠{kInfo.KType}￠{bInfo.BType}";
                                int lineDefault = 0;
                                decimal oddsDefault = 0m;

                                //勾上只跟PK或是生成上半賽事, 需抓取預設值
                                if (Config.isDsOnlyPK || Config.isZdOnlyPK || isCreateHT)
                                {
                                    if (OddsDefault.Dic.TryGetValue(key, out Tuple<int, decimal> tmp))
                                    {
                                        lineDefault = tmp.Item1;
                                        oddsDefault = tmp.Item2;
                                    }
                                    else
                                    {
                                        if (kInfo.SetType > 1)
                                        {
                                            //N分玩法、首/尾 特殊判斷BTYPE(SetType=>5 ,Btype=>1201051  對應 SetType=>1,Btype=>1201011)
                                            for (int i = kInfo.SetType - 1; i > 0; i--)
                                            {
                                                key = $"{gameInfo.League.LeagueMode}￠{gameInfo.Suffix.Group}￠{gameInfo.GType}￠{kInfo.KType}￠{bInfo.BType - i * 10}";

                                                if (OddsDefault.Dic.TryGetValue(key, out tmp))
                                                {
                                                    lineDefault = tmp.Item1;
                                                    oddsDefault = tmp.Item2;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }

                                oddsList.Add(SetCreateOddsRow(gId, gameInfo.GType, gpdmPair.Key, kInfo, bInfo, lineDefault, oddsDefault));
                            }
                        }
                    }

                    if (oddsList.Count > Common.eachBatchLimit)
                    {
                        dtList.Add(oddsList);
                        oddsList = new List<BkOddsCacheCreateDto>();
                    }
                }
            }

            if (oddsList.Count > 0)
            {
                dtList.Add(oddsList);
            }

            foreach (var tmp in dtList)
            {
                var request = new BkOddsCacheCreateRequest() { IsTransfer = Config.isTransfer };
                var sourceOpenRecordRequest = new SourceOpenRecordRequest();

                request.Data.AddRange(tmp);
                //設定發送時間
                Stopwatch stopwatch = Stopwatch.StartNew();
                var response = Config.OddsCacheServiceClient.BatchInsert(request);
                stopwatch.Stop();

                List<BkOddsCacheCreateResultDto> allResult = response.SelectResult.Concat(response.CreateResult).ToList();

                if (!string.IsNullOrEmpty(response.ErrorMsg))
                {
                    Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }

                //無筆數跳過
                if (allResult.Count == 0) { continue; }

                LogProcess.AddQueue(new CreateOddsCacheLog(Config.source, gpType.ToString(), request.Data, allResult, stopwatch.Elapsed, Config.entrance));

                //暫存待查詢kMode的字典
                ConcurrentDictionary<string, object[]> kModeDic = new ConcurrentDictionary<string, object[]>();
                //暫存查詢odds差距值的字典
                ConcurrentDictionary<string, object[]> oddsDic = new ConcurrentDictionary<string, object[]>();
                //待自動移滾清單(GID+GPDM)
                ConcurrentDictionary<(long Gid, decimal Gpdm), AutoCreateGameApiModel> tmpDicCreateGameModel = new ConcurrentDictionary<(long Gid, decimal Gpdm), AutoCreateGameApiModel>();

                #region 合併結果

                var mergeData = (from output in allResult
                                 join input in tmp
                                 on new { output.GId, output.KType, output.BType, output.SetType } equals
                                 new { input.GId, input.KType, input.BType, input.SetType }
                                 select new
                                 {
                                     input.Gpdm,
                                     output.Id,
                                     output.GId,
                                     output.KType,
                                     output.BType,
                                     output.SetType,
                                     output.IsCreateGame,
                                     output.Result
                                 }).ToArray();

                #endregion


                foreach (var row in mergeData)
                {
                    decimal gpdm = row.Gpdm;
                    int id = row.Id;
                    long gId = row.GId;
                    int kType = row.KType;
                    int bType = row.BType;
                    byte isCreateGame = Convert.ToByte(row.IsCreateGame);
                    int result = row.Result;

                    //取對應物件
                    if (Config.gIdDic.TryGetValue(gId, out string gameKey)
                        && Config.gameDic.TryGetValue(gameKey, out GameInfo gameInfo)
                        && gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo)
                        && gpdmInfo.TryGetValue(kType, out KInfo kInfo)
                        && kInfo.BInfoDic.TryGetValue(bType, out BInfo bInfo))
                    {
                        //設定Id
                        bInfo.Id = id;
                        //設定玩法狀態
                        bInfo.Status = isCreateGame;

                        //當前狀態不為新比賽 且無kMode數據
                        if (isCreateGame > 0 && kInfo.KTypeGroup == 0)
                        {
                            //Api開關有開
                            if ((YwCore.Common.apiSwitch & 4) == 4)
                            {
                                //蒐集玩法與gId
                                string dicKey = $"{gameInfo.GId},{gpdm}";

                                //玩法狀態為待結算
                                if (isCreateGame == 255)
                                {
                                    Process.GameStatusSetApi.tmpDicGameStatusSetApiModel.TryAdd(dicKey, new GameStatusSetApiModel(GameStatusSetApiModel.ActionMode.Trader, gId, 0)
                                    {
                                        AllyName = gameInfo.League.FullName?.ElementAtOrDefault(0),
                                        TmaName = gameInfo.TeamA.FullName?.ElementAtOrDefault(0),
                                        TmbName = gameInfo.TeamB.FullName?.ElementAtOrDefault(0),
                                        SuffixName = gameInfo.Suffix.FullName?.ElementAtOrDefault(0)
                                    });
                                }
                                else
                                {
                                    //有加到待檢查字典則認定為短暫關與開,移除字典不打API
                                    if (!Process.GameStatusSetApi.tmpDicGameStatusSetApiModel.TryRemove(dicKey, out GameStatusSetApiModel value)) { }
                                }
                            }

                            object[] obj = new object[] { gpdm, gameInfo.GId, kInfo.KType, kInfo.SetType, (byte)Config.source };
                            kModeDic.TryAdd(string.Join("_", obj), obj);

                            object[] objOdds = new object[] { gpdm, bInfo.Id, gameInfo.GId, kInfo.KType, bInfo.BType, kInfo.SetType, (byte)Config.source };
                            oddsDic.TryAdd(string.Join("_", objOdds), objOdds);
                        }
                        else if (kInfo.KTypeGroup > 0)
                        {
                            SettlementRecordModel tmpModel = new SettlementRecordModel(gId, gameInfo.GType, kInfo.KTypeGroup);
                            //來源消失未超過60秒,移除佇列
                            Process.SettlementRecordHandle.CheckQueue(tmpModel);

                            //有寫過未切提醒, GP寫入提醒取消
                            if (Process.SettlementRecordHandle.SettlementRecord.TryRemove(tmpModel.DicKey, out DateTime time))
                            {
                                sourceOpenRecordRequest.Data.Add(new SourceOpenRecordDto()
                                {
                                    GId = tmpModel.GId,
                                    GType = tmpModel.GType,
                                    KTypeGroup = tmpModel.KTypeGroup,
                                    SourceId = 0,
                                    IsOpen = 3,
                                });
                            }
                        }

                        //入庫後的結果
                        switch (result)
                        {
                            case 1:
                                //新建立玩法開放設為更新
                                if (kInfo.IsOpen) { kInfo.IsUpdate = true; }
                                break;
                            case 2:
                                //提前生成以外的玩法，查詢到則設為更新
                                if (!gpdmInfo.IsGenerate) { kInfo.IsUpdate = true; }
                                break;
                            case 5:
                                //轉換GP
                                if (kInfo.IsOpen) { kInfo.IsUpdate = true; }

                                if (!tranGIdDic.Contains(gId))
                                {
                                    tranGIdDic.Add(gId);
                                }

                                break;
                        }

                        //有勾上相關功能才處理
                        if (gameInfo.GpType == GpType.Zd && Config.AutoCreateZdGame)
                        {
                            if (!IsAutoMovable(kType)) continue;

                            //後台玩法預設值是否有設定
                            if (!OddsDefault.Dic.TryGetValue($"{gameInfo.League.LeagueMode}￠{gameInfo.Suffix.Group}￠{gameInfo.GType}￠{kType}￠{bType}", out _)) continue;

                            //查看暫存字典是否存在
                            AutoCreateGameApiModel tmpCreateGameModel = tmpDicCreateGameModel.GetOrAdd((gId, gpdm), new AutoCreateGameApiModel(gId, (int)Config.source, new HashSet<OddsInfoModel>(), 90, gameInfo.GameDate, gameInfo.TeamA.Id, gameInfo.TeamB.Id, gameInfo.Suffix.Id, gameInfo.PrioritySourceId == (int)Config.source ? new FilterAutoCreate() : new FilterSemiAutoCreate())
                            {
                                AllyName = gameInfo.League.FullName?.ElementAtOrDefault(0),
                                TmaName = gameInfo.TeamA.FullName?.ElementAtOrDefault(0),
                                TmbName = gameInfo.TeamB.FullName?.ElementAtOrDefault(0),
                                SuffixName = gameInfo.Suffix.FullName?.ElementAtOrDefault(0)
                            });

                            OddsInfoModel oddsInfoModel = new OddsInfoModel() { Id = id, KType = kType, BType = bType, SetType = kInfo.SetType, SourceId = (int)Config.source };

                            //新建立的玩法
                            if (result == 1)
                            {
                                tmpCreateGameModel.OddsIdList.Add(oddsInfoModel);
                            }
                        }
                    }
                }

                //有建立玩法的賽事加入佇列
                foreach (var order in tmpDicCreateGameModel.OrderBy(a => a.Key.Gpdm))
                {
                    AutoCreateGameApiModel tmpModel = order.Value;

                    if (tmpModel.OddsIdList.Count > 0)
                    {
                        Process.CreateGameApi.AddQueue(tmpModel);
                    }
                }

                #region 處理kMode、odds差距

                if (kModeDic.Count > 0)
                {
                    //[主庫]待查詢kMode表
                    var dto = kModeDic.Values.Select(a => new KModeSelectDto()
                    {
                        Gpdm = Convert.ToInt64(a[0]),
                        GId = Convert.ToInt64(a[1]),
                        KType = (int)a[2],
                        SetType = Convert.ToInt32(a[3]),
                        SourceId = Convert.ToInt32(a[4])
                    });

                    KModeSelectRequest kModeSelectRequest = new KModeSelectRequest() { Sport = (int)Config.sport };
                    kModeSelectRequest.Data.AddRange(dto);

                    //放入佇列
                    Task.Delay(2000).ContinueWith(t =>
                    {
                        Config.dtKModeQueue.Enqueue(kModeSelectRequest);
                    });
                }

                if (oddsDic.Count > 0)
                {
                    //[主庫]查詢odds差距
                    var dto = oddsDic.Values.Select(a => new OddsSelectDto()
                    {
                        Gpdm = Convert.ToInt64(a[0]),
                        Id = Convert.ToInt32(a[1]),
                        GId = Convert.ToInt64(a[2]),
                        KType = (int)a[3],
                        BType = (int)a[4],
                        SetType = Convert.ToInt32(a[5]),
                        SourceId = Convert.ToInt32(a[6])
                    });

                    OddsSelectRequest oddsSelectRequest = new OddsSelectRequest() { Sport = (int)Config.sport };
                    oddsSelectRequest.Data.AddRange(dto);

                    //放入佇列
                    Config.dtOddsQueue.Enqueue(oddsSelectRequest);
                }

                #endregion

                //發送轉換GP gid
                if (tranGIdDic.Count > 0 && Config.isTransfer)
                {
                    byte gType = (byte)(gpType == GpType.Zd ? GType.BkZd : GType.BkDs);
                    FrmTransfer.GetGid(tranGIdDic, gType, (byte)Config.source);
                }

                //[新比賽庫]來源開關異動紀錄
                if (sourceOpenRecordRequest.Data.Count > 0)
                {
                    //寫入日誌
                    string dateNow = DateTime.Now.ToString("MMddHH") + ((int.Parse(DateTime.Now.ToString("mm")) / 10) * 10).ToString("00");
                    string content = Environment.NewLine + JsonConvert.SerializeObject(sourceOpenRecordRequest);
                    LogFile.SetLog(LogMode.Remind_SourceOpenRecord, content, "", dateNow);

                    var insertResponse = Common.GameCacheServiceClient.SourceOpenRecord(sourceOpenRecordRequest);

                    if (!string.IsNullOrEmpty(insertResponse.ErrorMsg))
                    {
                        LogFile.SetLog(LogMode.Error, $"{insertResponse.GetType().Name} => {insertResponse.ErrorMsg}");
                    }
                }
            }
        }

        /// <summary>
        /// 查詢賽事的狀態(被刪除或是移到主庫)
        /// </summary>
        /// <param name="gpType">GP類型</param>
        public static void SelectGame(GpType gpType)
        {
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;
            //篩選有Id的賽事
            List<GameInfo> gameList = (from game in gameDic.Values
                                       where game.GpType == gpType && game.Id > 0
                                       select game).ToList();
            //賽事暫存表
            BkGameCacheUpdateRequestV6 request = new BkGameCacheUpdateRequestV6() { SourceId = (int)Config.source };

            foreach (GameInfo gameInfo in gameList)
            {
                if (SetUpdateGameRow(gameInfo, out BkGameCacheUpdateDtoV6 row, out int channelId, out int multiSatatus))
                {
                    request.Data.Add(row);
                }
            }

            if (request.Data.Count > 0)
            {
                //執行SQL
                var response = Config.GameCacheServiceClient.BatchSelectV6(request);

                if (!string.IsNullOrEmpty(response.ErrorMsg))
                {
                    Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }

                foreach (var row in response.SelectResults)
                {
                    int id = row.Id;
                    long gId = row.GId;
                    DateTime gameDate = TimeStampHelper.ToLocalDateTime(row.GameDate);
                    byte status = Convert.ToByte(row.Status);
                    bool isCreateGame = row.IsCreateGame;
                    //是否移除gid關聯
                    bool isRemoveGid = false;

                    //取對應物件
                    if (Config.gIdDic.TryGetValue(gId, out string gameKey)
                        && gameDic.TryGetValue(gameKey, out GameInfo gameInfo))
                    {
                        //有數據庫Id
                        if (id > 0)
                        {
                            gameInfo.StatusDB = status;
                            gameInfo.IsMoveMain = isCreateGame;
                            gameInfo.QuarterTime = (byte)row.QuarterTime;

                            //開賽時間比對是否有異動
                            if (gameInfo.IsDateFromDb && gameInfo.GameDate != gameDate)
                            {
                                //帳務日期重算
                                DateTime paymentDate = Common.GetPaymentDate(gameDate);

                                //若帳務時間需變更，則關閉舊賽事並新建時間異動後的賽事
                                if (paymentDate != gameInfo.PaymentDate)
                                {
                                    //複製需要關閉的賽事出來再加進賽事字典走關閉流程
                                    GameInfo closeGame = gameInfo.DeepClone();
                                    closeGame.DataId = -1;
                                    Config.gameDic.TryAdd($"{closeGame.GetGameDictKey()}_d", closeGame);
                                    //更新賽事的開賽、帳務時間後走新建流程
                                    gameInfo.SetGameDate(gameDate);
                                    gameInfo.GameNotExistHandle();
                                    isRemoveGid = true;
                                }
                                else
                                {
                                    gameInfo.SetGameDate(gameDate);
                                }

                                //更新關盤監控判斷的開賽時間
                                Config.CloseNotify.UpdateGameDate(gId, gameDate);
                            }
                        }
                        else
                        {
                            //複製需要關閉的賽事出來再加進賽事字典走關閉流程
                            GameInfo closeGame = gameInfo.DeepClone();
                            closeGame.DataId = -1;
                            Config.gameDic.TryAdd($"{closeGame.GetGameDictKey()}_d", closeGame);
                            gameInfo.GameNotExistHandle();
                            isRemoveGid = true;
                        }
                    }
                    else
                    {
                        gameDic.Values.Where(a => a.GId == gId).ToList().ForEach(a => a.GameNotExistHandle());
                        isRemoveGid = true;
                    }

                    //移除gid關聯
                    if (isRemoveGid)
                    {
                        Config.gIdDic.TryRemove(gId, out string remove);
                    }
                }
            }
        }

        /// <summary>
        /// 查詢賽事的狀態(被刪除或是移到主庫)
        /// </summary>
        /// <param name="gpType">GP類型</param>
        public static void SelectOdds(GpType gpType)
        {
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;
            //篩選出有Id的賽事
            List<GameInfo> gameList = (from game in gameDic.Values
                                       where game.GpType == gpType && game.Id > 0 && game.StatusDB < 49 && game.ObjDic.Count > 0
                                       select game).ToList();
            //玩法請求參數列表(分批更新)
            List<BkOddsCacheUpdateRequest> requestList = new List<BkOddsCacheUpdateRequest>();
            //玩法請求參數
            BkOddsCacheUpdateRequest request = new BkOddsCacheUpdateRequest();

            //挑選須查詢的玩法
            foreach (GameInfo gameInfo in gameList)
            {
                foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                {
                    GpdmInfo tmpDic = gpdmPair.Value;

                    if (Config.BlockList.BlockGpdm(gameInfo, tmpDic))
                    {
                        continue;
                    }

                    foreach (KInfo kInfo in tmpDic.Values)
                    {
                        foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                        {
                            if (bInfo.Id > 0)
                            {
                                request.Data.Add(SetSelectOddsRow(gameInfo.GId, Convert.ToInt64(gpdmPair.Key), kInfo, bInfo));
                            }
                        }
                    }
                }

                //超過筆數換下一群組(分批查詢)
                if (request.Data.Count > Common.eachBatchLimit)
                {
                    requestList.Add(request);
                    request = new BkOddsCacheUpdateRequest();
                }
            }

            //將最後一個列表加入至群組
            if (request.Data.Count > 0)
            {
                requestList.Add(request);
            }

            foreach (var tmp in requestList)
            {
                //暫存待查詢kMode的字典
                ConcurrentDictionary<string, KModeSelectDto> kModeDic = new ConcurrentDictionary<string, KModeSelectDto>();
                //暫存查詢odds差距值的字典
                ConcurrentDictionary<string, OddsSelectDto> oddsDic = new ConcurrentDictionary<string, OddsSelectDto>();
                //執行SQL
                var response = Config.OddsCacheServiceClient.BatchSelect(tmp);

                if (!string.IsNullOrEmpty(response.ErrorMsg))
                {
                    Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }

                #region 判斷查詢結果

                foreach (var row in response.SelectResult)
                {
                    long gpdm = row.Gpdm;
                    int? id = row.Id;
                    long gId = row.GId;
                    int kType = row.KType;
                    int bType = row.BType;
                    byte isCreateGame = Convert.ToByte(row.IsCreateGame);

                    //取對應物件
                    if (Config.gIdDic.TryGetValue(gId, out string gameKey)
                        && gameDic.TryGetValue(gameKey, out GameInfo gameInfo)
                        && gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo)
                        && gpdmInfo.TryGetValue(kType, out KInfo kInfo)
                        && kInfo.BInfoDic.TryGetValue(bType, out BInfo bInfo))
                    {
                        //玩法存在庫則設定isCreateGame
                        if (id > 0)
                        {
                            //當前狀態不為新比賽 且 狀態切換或無kMode數據
                            if (isCreateGame > 0 && (bInfo.Status != isCreateGame || kInfo.KTypeGroup == 0))
                            {
                                var kmodeSelect = new KModeSelectDto { Gpdm = gpdm, GId = gameInfo.GId, KType = kInfo.KType, SetType = kInfo.SetType, SourceId = (int)Config.source };
                                kModeDic.TryAdd(kmodeSelect.ToString(), kmodeSelect);

                                var oddsSelect = new OddsSelectDto { Gpdm = gpdm, Id = bInfo.Id, GId = gameInfo.GId, KType = kInfo.KType, BType = bInfo.BType, SetType = kInfo.SetType, SourceId = (int)Config.source };
                                oddsDic.TryAdd(oddsSelect.ToString(), oddsSelect);
                            }

                            //待結算切回主庫則清除紀錄
                            if (bInfo.Status == 255 && isCreateGame == 1)
                            {
                                kInfo.IsSourceOpenRecord = false;
                            }

                            //新比賽移到主庫
                            if (bInfo.Status == 0 && isCreateGame == 1)
                            {
                                kInfo.IsUpdate = true;
                            }

                            //設定狀態
                            bInfo.Status = isCreateGame;
                        }
                        //無數據庫Id
                        else
                        {
                            bInfo.Status = 0;
                        }

                        bInfo.Id = id.Value;

                        //bInfo不再主庫則清掉kInfo設置
                        if (kInfo.BInfoDic.Values.All(a => a.Status == 0))
                        {
                            if (kInfo.KModeId != 0 ||
                                kInfo.OddsTotal != 0m ||
                                kInfo.KTypeGroup != 0 ||
                                kInfo.LineModify != 0)
                            {
                                kInfo.KModeId = 0;
                                kInfo.OddsTotal = 0m;
                                kInfo.KTypeGroup = 0;
                                kInfo.LineModify = 0;

                                foreach (BInfo tmpBInfo in kInfo.BInfoDic.Values)
                                {
                                    tmpBInfo.OddsModify = 0m;
                                }

                                kInfo.IsUpdate = true;
                            }
                        }
                    }
                }

                #endregion

                #region 處理kMode、odds差距

                if (kModeDic.Count > 0)
                {
                    //[主庫]待查詢kMode表
                    KModeSelectRequest kModeSelectRequest = new KModeSelectRequest() { Sport = (int)Config.sport };
                    kModeSelectRequest.Data.AddRange(kModeDic.Values);

                    //放入佇列
                    Task.Delay(2000).ContinueWith(t =>
                    {
                        Config.dtKModeQueue.Enqueue(kModeSelectRequest);
                    });
                }

                if (oddsDic.Count > 0)
                {
                    //[主庫]查詢odds差距
                    OddsSelectRequest oddsSelectRequest = new OddsSelectRequest() { Sport = (int)Config.sport };
                    oddsSelectRequest.Data.AddRange(oddsDic.Values);

                    //放入佇列
                    Config.dtOddsQueue.Enqueue(oddsSelectRequest);
                }

                #endregion
            }
        }

        /// <summary>
        /// 更新賽事
        /// </summary>
        /// <param name="games">更新賽事</param>
        /// <param name="gpType">GP類型</param>
        public static void UpdateGame(List<GameInfo> games, GpType gpType)
        {
            //篩選需更新的賽事
            List<GameInfo> gameList = (from game in games
                                       where game.Id > 0 && game.StatusDB < 49 && game.IsUpdate && game.DataId >= 0
                                       select game).ToList();
            //[新比賽庫]更新賽事暫存表
            List<BkGameCacheUpdateDtoV6> gameCacheList = new List<BkGameCacheUpdateDtoV6>();
            //[主庫]更新賽事暫存表
            List<BkGameUpdateDto> mainGameList = new List<BkGameUpdateDto>();
            //[新比賽庫]更新玩法暫存表
            IList<BkOddsCacheUpdateDto> oddsCacheList = new List<BkOddsCacheUpdateDto>();
            //[主庫]更新玩法暫存表
            IList<BkOddsUpdateDto> mainOddsList = new List<BkOddsUpdateDto>();
            //[主庫]查詢Game表二進制開關
            List<GetGameMultiStatusDto> mainMultiStatus = new List<GetGameMultiStatusDto>();
            GetGameMultiStatusResponse response = null;

            //[主庫]查詢Game表二進制開關(ZD才處理)
            if (gpType == GpType.Zd)
            {
                foreach (GameInfo gameInfo in gameList)
                {
                    //[主庫]有移到主庫再查
                    if (gameInfo.IsMoveMain)
                    {
                        mainMultiStatus.Add(new GetGameMultiStatusDto()
                        {
                            GId = gameInfo.GId,
                        });
                    }
                }

                if (mainMultiStatus.Count > 0)
                {
                    var request = new GetGameMultiStatusRequest() { Sport = (int)Config.sport };
                    request.Data.AddRange(mainMultiStatus);
                    //入庫執行
                    response = Common.NbbServiceClient.GetGameMultiStatus(request);
                }
            }

            foreach (GameInfo gameInfo in gameList)
            {
                //標記更新時間戳
                gameInfo.SetUpdatingTick();

                bool isClearUpdate = true;
                bool moveMain = false;

                //[主庫]
                if (gameInfo.IsMoveMain)
                {
                    //Game表二進制開關
                    var tmpResult = response?.Result.FirstOrDefault(a => a.GId == gameInfo.GId);
                    int tmpMultiStatus = tmpResult == null ? 0 : tmpResult.MultiStatus;
                    //(需更新比分狀態)顯示全球比分&顯示來源比分,勾上系統ON跟來源/OFF跟全球 OR 只勾上顯示來源比分
                    bool isUpdateScore = (tmpMultiStatus & (int)Math.Pow(2, 8)) > 0 && (tmpMultiStatus & (int)Math.Pow(2, 9)) > 0 ? Common.isDisplayScoreSet :
                                         (tmpMultiStatus & (int)Math.Pow(2, 8)) > 0 ? true : false;

                    if (gameInfo.MoveMainTime != DateTime.MinValue && DateTime.Now.Subtract(gameInfo.MoveMainTime).TotalSeconds > 2)
                    {
                        //比分狀態以外的主庫Game表欄位有異動或可更新比分才入庫更新
                        if ((gameInfo.IsUpdateSwitch & 2) > 0 || isUpdateScore)
                        {
                            if (SetUpdateGameRow(gameInfo, out BkGameCacheUpdateDtoV6 row, out int channelId, out int multiStatus, true))
                            {
                                mainGameList.Add(new BkGameUpdateDto()
                                {
                                    Id = row.Id,
                                    GameDate = row.GameDate,
                                    PaymentDate = row.PaymentDate,
                                    GId = row.GId,
                                    IsLive = true,
                                    Status = gameInfo.GpType == GpType.Zd ? isUpdateScore ? gameInfo.Status : 255 : 0,
                                    LastSec = gameInfo.GpType == GpType.Zd ? (isUpdateScore ? (gameInfo.Status == 6 ? 0 : gameInfo.LastSec) : -1) : 0,
                                    ScoreA = SetDBValue<int?>(gameInfo.Score.IsOpen && isUpdateScore, gameInfo.Score.ScoreA),
                                    ScoreB = SetDBValue<int?>(gameInfo.Score.IsOpen && isUpdateScore, gameInfo.Score.ScoreB),
                                    ScoreHalf1A = SetDBValue<int?>(gameInfo.ScoreHalf1.IsOpen && isUpdateScore, gameInfo.ScoreHalf1.ScoreA),
                                    ScoreHalf1B = SetDBValue<int?>(gameInfo.ScoreHalf1.IsOpen && isUpdateScore, gameInfo.ScoreHalf1.ScoreB),
                                    ScoreHalf2A = SetDBValue<int?>(gameInfo.ScoreHalf2.IsOpen && isUpdateScore, gameInfo.ScoreHalf2.ScoreA),
                                    ScoreHalf2B = SetDBValue<int?>(gameInfo.ScoreHalf2.IsOpen && isUpdateScore, gameInfo.ScoreHalf2.ScoreB),
                                    Score1A = SetDBValue<int?>(gameInfo.Score1.IsOpen && isUpdateScore, gameInfo.Score1.ScoreA),
                                    Score1B = SetDBValue<int?>(gameInfo.Score1.IsOpen && isUpdateScore, gameInfo.Score1.ScoreB),
                                    Score2A = SetDBValue<int?>(gameInfo.Score2.IsOpen && isUpdateScore, gameInfo.Score2.ScoreA),
                                    Score2B = SetDBValue<int?>(gameInfo.Score2.IsOpen && isUpdateScore, gameInfo.Score2.ScoreB),
                                    Score3A = SetDBValue<int?>(gameInfo.Score3.IsOpen && isUpdateScore, gameInfo.Score3.ScoreA),
                                    Score3B = SetDBValue<int?>(gameInfo.Score3.IsOpen && isUpdateScore, gameInfo.Score3.ScoreB),
                                    Score4A = SetDBValue<int?>(gameInfo.Score4.IsOpen && isUpdateScore, gameInfo.Score4.ScoreA),
                                    Score4B = SetDBValue<int?>(gameInfo.Score4.IsOpen && isUpdateScore, gameInfo.Score4.ScoreB),
                                    LiveUrl = gameInfo.RadarChannel.ToString(),  //純雷達流:1 其餘:0
                                    ChannelId = channelId,
                                    MultiStatus = multiStatus,
                                    QuarterTime = row.QuarterTime
                                });

                                moveMain = true;
                            }
                        }
                    }
                    else
                    {
                        isClearUpdate = false;
                    }
                }

                //[新比賽庫]
                if ((gameInfo.IsUpdateSwitch & 1) > 0 && SetUpdateGameRow(gameInfo, out BkGameCacheUpdateDtoV6 tmpRow, out int tmpChannelId, out int _multiStatus))
                {
                    tmpRow.MoveMain = moveMain;
                    gameCacheList.Add(tmpRow);
                }

                if (isClearUpdate)
                {
                    AfterUpdateHandle += () =>
                    {
                        gameInfo.IsUpdate = false;
                        gameInfo.IsUpdateSwitch = 0;
                    };
                }
            }

            //工作集合，同時處理主庫、新比賽庫的工作
            List<Task> taskList = new List<Task>();

            //[主庫]賽事資訊入庫更新
            if (mainGameList.Count > 0)
            {
                Task mainGameTask = Task.Run(() =>
                {
                    var request = new BkGameUpdateRequest() { Type = "UPDATE" };
                    request.Data.AddRange(mainGameList);
                    //設定發送時間
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    var response = Config.GameServiceClient.BatchUpdate(request);
                    stopwatch.Stop();

                    if (!string.IsNullOrEmpty(response.ErrorMsg))
                    {
                        Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                        LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                        return;
                    }

                    //處理直播連動(ZD才處理)
                    if (gpType == GpType.Zd)
                    {
                        Task task = Task.Run(() =>
                        {
                            LinkChannel.HandleLinkChannel(response.Result.Where(a => a.NewChannelId != a.OldChannelId).Select(a => new EditLinkChannelRequest { ChannelId = a.NewChannelId, GId = a.GId, Sport = (int)Config.sport }));
                        });
                    }
                    else
                    {
                        response.Result.Where(output => output.NewGameDate != output.OldGameDate).ToList().ForEach(output =>
                        {
                            var game = gameList.FirstOrDefault(game => game.GId == output.GId);

                            if (game != null)
                            {
                                Common.SendMessage.Send($"Ds開賽時間已更改{Environment.NewLine}聯盟:{game.League.FullName[0]}{Environment.NewLine}隊伍A:{game.TeamA.FullName[0]}{Environment.NewLine}隊伍B:{game.TeamB.FullName[0]}{Environment.NewLine}變動後開賽時間:{DateTimeOffset.FromUnixTimeMilliseconds(output.NewGameDate).LocalDateTime.ToString("MM-dd HH:mm")}", 0, false);
                            }
                        });
                    }

                    //加入日誌佇列
                    LogProcess.AddQueue(new UpdateMainGameLog(Config.source, gpType.ToString(), request.Data, response.Result, stopwatch.Elapsed, Config.entrance));
                });

                taskList.Add(mainGameTask);
            }

            //[新比賽庫]賽事資訊入庫更新
            if (gameCacheList.Count > 0)
            {
                Task newGameTask = Task.Run(() =>
                {
                    var request = new BkGameCacheUpdateRequestV6() { SourceId = (int)Config.source };
                    request.Data.AddRange(gameCacheList);
                    //設定發送時間
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    var response = Config.GameCacheServiceClient.BatchUpdateV6(request);
                    stopwatch.Stop();

                    if (!string.IsNullOrEmpty(response.ErrorMsg))
                    {
                        Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                        LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                        return;
                    }

                    LogProcess.AddQueue(new UpdateGameCacheLog(Config.source, gpType.ToString(), request.Data, response.UpdateResults, stopwatch.Elapsed, Config.entrance));
                });

                taskList.Add(newGameTask);
            }

            //等待工作都處理完
            Task.WaitAll(taskList.ToArray());

            AfterUpdateHandle?.Invoke();
            AfterUpdateHandle = null;
        }

        /// <summary>
        /// 更新玩法
        /// </summary>
        /// <param name="games">更新賽事</param>
        /// <param name="gpType">GP類型</param>
        /// <param name="updateCount">更新成功數量</param>
        public static void UpdateOdds(List<GameInfo> games, GpType gpType, out int updateCount)
        {
            updateCount = 0;
            //設置關閉訊息物件
            SystemMessage systemMessage = new SystemMessage();
            //篩選出有Id的賽事
            List<GameInfo> gameList = (from game in games
                                       where game.Id > 0 && game.StatusDB < 49 && game.ObjDic.Count > 0 && game.DataId >= 0
                                       select game).ToList();
            //[新比賽庫]暫存表列表(分批更新)
            List<List<BkOddsCacheUpdateDto>> dtOddsList = new List<List<BkOddsCacheUpdateDto>>();
            //[新比賽庫]更新玩法暫存表
            List<BkOddsCacheUpdateDto> dtOdds = new List<BkOddsCacheUpdateDto>();
            //[主庫]暫存表列表(分批更新)
            List<List<BkOddsUpdateDto>> dtMainOddsList = new List<List<BkOddsUpdateDto>>();
            //[主庫]更新玩法暫存表
            List<BkOddsUpdateDto> dtMainOdds = new List<BkOddsUpdateDto>();
            //玩法PL暫存字典
            Dictionary<int, decimal> dicSourceOdds = new Dictionary<int, decimal>();

            //檢查鴛鴦盤防護邏輯
            CheckSameError(gameList);

            //挑選須更新的玩法
            foreach (GameInfo gameInfo in gameList)
            {
                foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                {
                    GpdmInfo tmpDic = gpdmPair.Value;

                    if (Config.BlockList.BlockGpdm(gameInfo, tmpDic))
                    {
                        continue;
                    }

                    //若該群組底下有其中一個還未讀取到kMode則跳過(確保有讀取到加總)
                    bool isNotReady = tmpDic.Values.Any(a => a.BInfoDic.Values.Any(b => b.Status == 1) && a.KModeId == 0);

                    //缺少kMode 或 JZ關大格且大格無變動，則不更新
                    if (isNotReady ||
                        (tmpDic.Display == false && tmpDic.IsDisplayChange == false))
                    {
                        continue;
                    }

                    //是否GP
                    bool isFollow = true;
                    //是否顯示(JZ則需判斷是否有開大格，其餘預設開)
                    bool isDisplay = Config.source == Source.JZ && tmpDic.Display == false ? false : true;
                    //是否小格全部關閉
                    bool isAllClose = false;
                    //是否有其中一個玩法更新
                    bool isUpdate = tmpDic.Values.Any(a => a.IsUpdate) || tmpDic.IsDisplayChange;

                    //其中一個玩法更新才進行計算
                    if (isUpdate)
                    {
                        foreach (KInfo kInfo in tmpDic.Values)
                        {
                            //BInfo沒有Id則跳過不判斷防護
                            if (kInfo.BInfoDic.Values.Any(b => b.Id == 0)) { continue; }
                            //計算邏輯
                            CalcLogic(gameInfo, kInfo, dicSourceOdds);

                            //來源有開放才跑防護邏輯
                            if (kInfo.IsOpen)
                            {
                                //防護邏輯 [陣列索引 0:isDisplay  1:isFollow  2:isAllClose]
                                bool[] sw = ProtectionLogic(gameInfo, kInfo, gpdmPair.Key, systemMessage);

                                isDisplay = isDisplay ? sw[0] : isDisplay;
                                isFollow = isFollow ? sw[1] : isFollow;
                                isAllClose = !isAllClose ? sw[2] : isAllClose;
                            }
                            //來源未開
                            else
                            {
                                kInfo.IsOpenResult = 0;
                            }
                        }

                        //ZD關大格邏輯降為小格全關
                        if (Config.source == Source.JZ)
                        {
                            if (gpType == GpType.Zd && isDisplay == false)
                            {
                                isDisplay = true;
                                isAllClose = true;
                            }
                        }
                    }

                    //連動關閉設置
                    tmpDic.Values.ToList().ForEach(a =>
                    {
                        a.IsAffectClosed = false;

                        //關所有小格
                        if (isAllClose)
                        {
                            a.IsOpenResult = 0;
                        }
                    });

                    //有更新則進行判斷
                    if (isUpdate)
                    {
                        //JZ為不脫勾版本
                        if (Config.source == Source.JZ)
                        {
                            isFollow = true;
                        }

                        //標記更新時間戳
                        if (tmpDic.IsDisplayChange) { tmpDic.SetUpdatingTick(); }

                        foreach (KInfo kInfo in tmpDic.Values)
                        {
                            //BInfo沒有Id則跳過不更新
                            if (kInfo.BInfoDic.Values.Any(b => b.Id == 0)) { continue; }
                            //標記更新時間戳
                            kInfo.SetUpdatingTick();
                            //是否PL清零 (只判斷ZD)
                            bool isClearZero = gpType == GpType.Zd && Config.isOddsClearZero && (!isDisplay || kInfo.IsOpenResult == 0);

                            //轉換完PK是否要開啟玩法
                            bool convertPkIsOpen = true;
                            //參數設置是否有開啟轉換正盤功能
                            bool isOpenConvertML = gameInfo.League.Name.Contains("NBA") && !gameInfo.League.Name.Contains("WNBA") ? Config.isNBAConvertML : Config.isOTConvertML;
                            //轉換正盤數值
                            decimal convertPk = isOpenConvertML && kInfo.IsOddsFollow && ((kInfo.IsSourceFollow & 2) != 2) ? ConvertMainLine(kInfo, out convertPkIsOpen) : 0;

                            foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                            {
                                switch (bInfo.Status)
                                {
                                    case 0://[新比賽庫]
                                        dtOdds.Add(SetUpdateOddsRowNew(gameInfo.GId, tmpDic.Key, kInfo, bInfo, isClearZero));
                                        break;
                                    case 1://[主庫]
                                        dtMainOdds.Add(SetUpdateMainOddsRow(gameInfo, tmpDic.Key, kInfo, bInfo, isDisplay, isFollow, isClearZero, false, false, convertPkIsOpen, convertPk));
                                        break;
                                }
                            }
                        }
                    }
                }

                //[主庫]超過筆數則先添加至佇列
                if (dtMainOdds.Count > Common.eachBatchLimit)
                {
                    dtMainOddsList.Add(dtMainOdds);
                    dtMainOdds = new List<BkOddsUpdateDto>();
                }

                //[新比賽庫]超過筆數換下一群組(分批更新)
                if (dtOdds.Count > Common.eachBatchLimit)
                {
                    dtOddsList.Add(dtOdds);
                    dtOdds = new List<BkOddsCacheUpdateDto>();
                }
            }

            //[主庫]將最後一個dataTable加入集合
            if (dtMainOdds.Count > 0) { dtMainOddsList.Add(dtMainOdds); }
            //[新比賽庫]將最後一個dataTable加入集合
            if (dtOdds.Count > 0) { dtOddsList.Add(dtOdds); }

            //工作集合，同時處理主庫、新比賽庫的工作
            List<Task<int>> taskList = new List<Task<int>>();

            //[主庫]入庫更新
            if (dtMainOddsList.Count > 0)
            {
                Task<int> mainOddsTask = Task.Run(() =>
                {
                    //更新筆數
                    int updCount = 0;

                    foreach (var dt in dtMainOddsList)
                    {
                        var request = new BkOddsUpdateRequest() { IsRemove = false, IsUpdateDisplay = true };
                        request.Data.AddRange(dt);
                        //設定發送時間
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        var response = Config.OddsServiceClient.BatchUpdateV2(request);
                        stopwatch.Stop();

                        if (response.ResultStatus != 1)
                        {
                            Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                            LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                            continue;
                        }

                        //收集回傳表
                        var messageInfo = from a in response.GameServerResult.Result
                                          join b in response.GameServerResult.GKSS on new { a.GId, a.KType, a.SetType } equals new { b.GId, b.KType, b.SetType }
                                          where b.IsLockSuccess
                                          select new UpdateMessageInfo { GId = a.GId, Gpdm = a.Gpdm, UpdateTime = TimeStampHelper.ToLocalDateTime(a.UpdateDate) };

                        systemMessage.GpdmInfo.AddRange(messageInfo);
                        //檢查是否更新kMode
                        Task.Run(() => { UpdataKmode(response.GameServerResult); });

                        //加入日誌佇列
                        LogProcess.AddQueue(new UpdateMainOddsLog(Config.source, gpType.ToString(), UpdateMainOddsLog.LogType.Update, request.Data, response.GameServerResult, stopwatch.Elapsed, Config.entrance));

                        updCount += response.GameServerResult.Result.Count;
                        ClearUpdateStatue(dt, dicSourceOdds, response.GameServerResult.GKSS);
                        CheckLineAtChangeHandle(response.GameServerResult, gpType);
                    }

                    //關閉提醒加入佇列
                    if (systemMessage.GpdmInfo.Count > 0)
                    {
                        CloseInfoHandle.AddQueue(systemMessage);
                    }

                    return updCount;
                });

                taskList.Add(mainOddsTask);
            }


            //[新比賽庫]入庫更新
            if (dtOddsList.Count > 0)
            {
                Task<int> newOddsTask = Task.Run(() =>
                {
                    //更新筆數
                    int updCount = 0;

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
                            Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                            LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                            continue;
                        }

                        //加入日誌佇列
                        LogProcess.AddQueue(new UpdateOddsCacheLog(Config.source, gpType.ToString(), UpdateOddsCacheLog.LogType.Update, request.Data, response.UpdateResult, stopwatch.Elapsed, Config.entrance));

                        updCount += response.UpdateResult.Count;
                        ClearUpdateStatue(dt.Select(a => new BkOddsUpdateDto()
                        {
                            GId = a.GId,
                            Gpdm = a.Gpdm,
                            KType = a.KType,
                            BType = a.BType,
                            SetType = a.SetType,
                            SourceId = a.SourceId,
                            Line = a.Line,
                            LineAt = a.LineAt,
                            Odds = a.Odds,
                            IsOpen = a.IsOpen
                        }), dicSourceOdds);
                    }

                    return updCount;
                });

                taskList.Add(newOddsTask);
            }

            //等待工作都處理完
            Task.WaitAll(taskList.ToArray());

            //主庫、新比賽庫更新筆數加總
            foreach (Task<int> t in taskList)
            {
                updateCount += t.Result;
            }
        }

        /// <summary>
        /// 更新kMode
        /// </summary>
        /// <param name="list"></param>
        private static void UpdataKmode(BkOddsUpdateGameServerResultDto list)
        {
            //RT互換列表
            List<int> changeList = new List<int>();

            foreach (var row in list.Result)
            {
                if (row.OldLineAt != row.NewLineAt)
                {
                    decimal f_gpdm = row.Gpdm;
                    long f_gid = row.GId;
                    int f_kType = row.KType;

                    //取對應物件
                    if (Config.gIdDic.TryGetValue(f_gid, out string gameKey)
                        && Config.gameDic.TryGetValue(gameKey, out GameInfo gameInfo)
                        && gameInfo.ObjDic.TryGetValue(f_gpdm, out var dic)
                        && dic.TryGetValue(f_kType, out KInfo kInfo))
                    {
                        if (kInfo.KModeId > 0 && !changeList.Contains(kInfo.KModeId))
                        {
                            changeList.Add(kInfo.KModeId);
                        }
                    }
                }
            }

            foreach (int id in changeList)
            {
                var response = Common.NbbServiceClient.KModeUpdate(new KModeUpdateRequest() { KModeId = id, Sport = (int)Config.source });

                if (!string.IsNullOrEmpty(response.ErrorMsg))
                {
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }
            }
        }

        /// <summary>
        /// 檢查是否來源開放紀錄(玩法被切待結算)
        /// </summary>
        /// <param name="gpType">GP類型</param>
        public static void CheckSourceOpenRecord(GpType gpType)
        {
            //篩選出有Id的賽事
            List<GameInfo> gameList = (from game in Config.gameDic.Values
                                       where game.GpType == gpType && game.Id > 0 && game.ObjDic.Count > 0
                                       select game).ToList();
            //[新比賽庫]來源開關異動紀錄暫存表
            SourceOpenRecordRequest sourceOpenRequest = new SourceOpenRecordRequest();
            //來源開關異動暫存字典
            ConcurrentDictionary<string, SourceOpenRecordDto> sourceDic = new ConcurrentDictionary<string, SourceOpenRecordDto>();

            //挑選須查詢的玩法
            foreach (GameInfo gameInfo in gameList)
            {
                foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in gameInfo.ObjDic)
                {
                    GpdmInfo tmpDic = gpdmPair.Value;

                    //提前生成玩法不須判斷
                    if (tmpDic.IsGenerate) { continue; }

                    //是否來源開放
                    bool isSourceOpen = false;

                    //挑選來源有開放的玩法且未寫入過開放提示
                    List<KInfo> kInfoList = (from kInfo in tmpDic.Values
                                             where kInfo.IsSourceOpenRecord == false
                                             select kInfo).ToList();

                    foreach (KInfo kInfo in kInfoList)
                    {
                        //是否任一個被移到待結算
                        bool isAny = (from bInfo in kInfo.BInfoDic.Values
                                      where bInfo.Status == 255
                                      select bInfo).Any();
                        if (isAny)
                        {
                            isSourceOpen = true;
                            break;
                        }
                    }

                    if (isSourceOpen)
                    {
                        //取第一筆
                        KInfo kInfo = tmpDic.Values.First();

                        //有kTypeGroup值才做寫入
                        if (kInfo.KTypeGroup > 0)
                        {
                            SourceOpenRecordDto dto = new SourceOpenRecordDto { GId = gameInfo.GId, GType = gameInfo.GType, KTypeGroup = kInfo.KTypeGroup, SourceId = (byte)Common.source, IsOpen = 1 };
                            sourceDic.TryAdd(dto.ToString(), dto);

                            //設置寫入開放提醒
                            foreach (KInfo tmpKInfo in kInfoList)
                            {
                                tmpKInfo.IsSourceOpenRecord = true;
                            }
                        }
                    }
                }
            }

            sourceOpenRequest.Data.AddRange(sourceDic.Values);

            //寫入來源開關異動紀錄
            if (sourceOpenRequest.Data.Count > 0)
            {
                var response = Common.GameCacheServiceClient.SourceOpenRecord(sourceOpenRequest);

                if (!string.IsNullOrEmpty(response.ErrorMsg))
                {
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }
            }
        }

        /// <summary>
        /// 檢查鴛鴦盤防護邏輯
        /// </summary>
        /// <param name="gameList">賽事列表</param>
        private static void CheckSameError(List<GameInfo> gameList)
        {
            foreach (GameInfo gameInfo in gameList)
            {
                //挑選已移出的玩法
                var kInfoList = gameInfo.ObjDic.Values.SelectMany(a => a.Values).Where(a => a.BInfoDic.Values.Any(b => b.Status == 1)).ToList();

                //無數據跳過
                if (kInfoList.Count == 0) { continue; }

                //kInfo分群
                List<List<KInfo>> kGroup = (from kInfo in kInfoList
                                            group kInfo by new { kInfo.KType, bMin = kInfo.BInfoDic.Keys.Min() } into tmpGroup
                                            select tmpGroup.ToList()).ToList();

                foreach (List<KInfo> tmpK in kGroup)
                {
                    if (tmpK.Count == 1)
                    {
                        tmpK.ForEach(kInfo => kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~3));
                    }
                    else
                    {
                        //判斷是否同盤，ZD：所有盤都設置、DS：有達條件的盤才設置
                        {
                            //鴛鴦盤條件 1:PK 2:PL
                            int sameErrorSwitch = 0;
                            //挑選相同Line
                            var sameLineList = tmpK.Where(k => k.IsOpen).GroupBy(kinfo => new { kinfo.Line, kinfo.LineAt }).Where(group => group.Count() > 1).ToList();
                            if (sameLineList.Any()) { sameErrorSwitch = 1; }

                            if (gameInfo.GpType == GpType.Zd)
                            {
                                tmpK.ForEach(kInfo => kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~1) | sameErrorSwitch);
                            }
                            else
                            {
                                //只設置達條件的kInfo
                                sameLineList.SelectMany(a => a).ToList().ForEach(kInfo => kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~1) | sameErrorSwitch);
                                //其餘未達條件則清除
                                tmpK.Except(sameLineList.SelectMany(a => a).ToList()).ToList().ForEach(kInfo => kInfo.SameErrorSwitch &= ~1);
                            }
                        }

                        //判斷是否同水，ZD：所有盤都設置、DS：有達條件的盤才設置
                        {
                            //挑選開放的玩法並依據kType分組
                            var kList = tmpK.Where(t => t.IsOpen).ToList();

                            //鴛鴦盤條件 1:PK 2:PL
                            int sameErrorSwitch = 0;
                            //挑選相同Odds
                            var sameOddsList = kList.SelectMany(kInfo => kInfo.BInfoDic.Values.Select(bInfo => new { KInfo = kInfo, BInfo = bInfo }))
                                .GroupBy(a => new { a.BInfo.BType, a.BInfo.Odds })
                                .Where(group => group.Count() > 1)
                                .SelectMany(a => a.Select(b => b.KInfo)).ToList();

                            if (sameOddsList.Any()) { sameErrorSwitch = 2; }

                            if (gameInfo.GpType == GpType.Zd)
                            {
                                tmpK.ForEach(kInfo => kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~2) | sameErrorSwitch);
                            }
                            else
                            {
                                //只設置達條件的kInfo
                                sameOddsList.ForEach(kInfo => kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~2) | sameErrorSwitch);
                                //其餘未達條件則清除
                                tmpK.Except(sameOddsList).ToList().ForEach(kInfo => kInfo.SameErrorSwitch &= ~2);
                            }
                        }
                    }
                }

                #region RT不一致

                if (gameInfo.GpType != GpType.Zd && Config.source != Source.JZ)
                {
                    //RT不一致
                    int[] arrayKinfo = new[] { 12001, 12101, 12301, 12401, 12501, 12601 };
                    var kInfoRfList = kInfoList.Where(kInfo => arrayKinfo.Contains(kInfo.KType)).ToList();

                    if (!FrmExclude.excludeDic.ContainsKey(gameInfo.GId))
                    {
                        int countLineAt = kInfoRfList.Where(kInfo => kInfo.IsOpen).GroupBy(kInfo => kInfo.LineAt).Count();
                        //鴛鴦盤條件 1:PK, 2:PL, 4:讓頭不一致
                        int sameErrorSwitch = countLineAt > 1 ? 4 : 0;

                        kInfoRfList.ForEach(kInfo =>
                        {
                            kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~4) | sameErrorSwitch;
                        });
                    }
                    else
                    {
                        kInfoRfList.ForEach(kInfo =>
                        {
                            kInfo.SameErrorSwitch = (kInfo.SameErrorSwitch & ~4);
                        });
                    }
                }

                #endregion
            }
        }

        /// <summary>
        /// 清除更新狀態
        /// </summary>
        /// <param name="list">入庫的暫存表</param>
        /// <param name="dicSourceOdds">來源PL暫存字典</param>
        private static void ClearUpdateStatue(IEnumerable<BkOddsUpdateDto> list, Dictionary<int, decimal> dicSourceOdds, IEnumerable<GKSSDto>? gKSSes = null)
        {
            //判斷是否更新成功
            foreach (var row in list)
            {
                decimal gpdm = row.Gpdm;
                int id = row.Id;
                long gId = row.GId;
                int kType = row.KType;
                int bType = row.BType;
                bool lineAt = row.LineAt;
                int line = row.Line;
                decimal odds = (decimal)row.Odds;
                int setType = row.SetType;
                bool isLockSuccess = gKSSes == null || gKSSes.Where(a => a.GId == gId && a.KType == kType && a.SetType == setType).First().IsLockSuccess;

                //取對應物件
                if (Config.gIdDic.TryGetValue(gId, out string gameKey)
                    && Config.gameDic.TryGetValue(gameKey, out GameInfo gameInfo)
                    && gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo)
                    && gpdmInfo.TryGetValue(kType, out KInfo kInfo)
                    && kInfo.BInfoDic.TryGetValue(bType, out BInfo bInfo)
                    && isLockSuccess)
                {
                    gpdmInfo.IsDisplayChange = false;
                    kInfo.IsUpdate = false;
                    //清除異動差距值
                    kInfo.LineAtChange = lineAt == Convert.ToBoolean(kInfo.LineAt) ? 0 : kInfo.LineAtChange;

                    //更新的RTPK與緩存不一致(重新計算跳動)
                    if (kInfo.Line != line || kInfo.LineAtChange != 0)
                    {
                        //跳動計算基數(A讓為正數)
                        int tmpBase = kInfo.LineAt == 0 ? -1 : 1;

                        //需要判斷是否開放0分盤
                        if (kInfo.IsZeroLine)
                        {
                            kInfo.LineChange = kInfo.LineAtChange != 0 ? (kInfo.Line + line) * tmpBase : (kInfo.Line - line) * tmpBase;
                        }
                        else
                        {
                            int tmpNewLine = kInfo.Line < 500 ? 500 : kInfo.Line;
                            int tmpOldLine = line < 500 ? 500 : line;
                            kInfo.LineChange = kInfo.LineAtChange != 0 ? (tmpNewLine + tmpOldLine - 1000) * tmpBase : (tmpNewLine - tmpOldLine) * tmpBase;
                        }
                    }
                    else
                    {
                        kInfo.LineChange = 0;
                    }

                    //字典不存在或更新PL<=0, 清除跳動
                    if (dicSourceOdds.TryGetValue(id, out decimal sourceOdds) && odds > 0m)
                    {
                        //更新的ODDS與緩存不一致(重新計算跳動)
                        bInfo.OddsChange = bInfo.Odds != sourceOdds && (kInfo.IsSourceFollow & 2) == 2 ? bInfo.Odds - sourceOdds : 0;
                    }
                    else
                    {
                        bInfo.OddsChange = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 檢查是否LineAt異動 需進行脫勾
        /// </summary>
        /// <param name="resultDataTable">DB更新完的傳回表</param>
        /// <param name="gpType">GP類型</param>
        private static void CheckLineAtChangeHandle(BkOddsUpdateGameServerResultDto list, GpType gpType)
        {
            //JZ來源不須判斷
            if (Config.source == Source.JZ) { return; }

            //[主庫]更新玩法暫存表
            List<BkOddsUpdateDto> dtMainOdds = new List<BkOddsUpdateDto>();

            //篩選lineAt互換須脫勾的對應玩法
            var groupList = list.Result.Where(row => row.IsChangeLineAt)
                .GroupBy(row => new { gId = row.GId, gpdm = row.Gpdm });

            foreach (var group in groupList)
            {
                long gId = group.Key.gId;
                decimal gpdm = group.Key.gpdm;

                if (Config.gIdDic.TryGetValue(gId, out string gameKey)
                    && Config.gameDic.TryGetValue(gameKey, out GameInfo gameInfo)
                    && gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo))
                {
                    //DS脫勾關大格&關小格    ZD脫勾關小格

                    //是否顯示(ZD不異動大格)
                    bool isDisplay = gpType == GpType.Zd ? true : false;

                    foreach (KInfo kInfo in gpdmInfo.Values)
                    {
                        foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                        {
                            if (bInfo.Status == 1)
                            {
                                dtMainOdds.Add(SetUpdateMainOddsRow(gameInfo, gpdm, kInfo, bInfo, isDisplay, false, false, false, true));
                            }
                        }
                    }
                }
            }

            if (dtMainOdds.Count > 0)
            {
                var request = new BkOddsUpdateRequest() { IsRemove = true, IsUpdateDisplay = true };
                request.Data.AddRange(dtMainOdds);
                //設定發送時間
                Stopwatch stopwatch = Stopwatch.StartNew();
                var response = Config.OddsServiceClient.BatchUpdateV2(request);
                stopwatch.Stop();

                if (response.ResultStatus != 1)
                {
                    Display.SetDictInfo(Display.Type.Error, response.ErrorMsg);
                    LogFile.SetLog(LogMode.Error, $"{response.GetType().Name} => {response.ErrorMsg}");
                }
                else
                {
                    LogProcess.AddQueue(new UpdateMainOddsLog(Config.source, gpType.ToString(), UpdateMainOddsLog.LogType.Update, request.Data, response.GameServerResult, stopwatch.Elapsed, Config.entrance));
                }
            }
        }

        /// <summary>
        /// 計算邏輯
        /// </summary>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="kInfo">玩法資訊</param>
        /// <param name="dicSourceOdds">來源玩法PL資訊</param>
        private static void CalcLogic(GameInfo gameInfo, KInfo kInfo, Dictionary<int, decimal> dicSourceOdds)

        {
            GpType gpType = gameInfo.GpType;
            decimal gap = 0m;
            byte matchType = 0;

            if (Config.source == Source.BET365)
            {
                //上半
                if ((kInfo.KType / 100) == 121)
                {
                    matchType = 1;
                }
                //下半
                else if ((kInfo.KType / 100) == 122)
                {
                    matchType = 2;
                }
                //單節
                else if ((kInfo.KType / 100) >= 123 && (kInfo.KType / 100) <= 126)
                {
                    matchType = 3;
                }
            }

            #region 設定對應的差距及倍率

            switch (kInfo.PlayTyp)
            {
                case 1:
                    if (matchType > 0)
                    {
                        gap = matchType == 1 ? Config.gapHtRf : matchType == 2 ? Config.gapXbRf : Config.gapDjRf;
                    }
                    else
                    {
                        gap = gpType == GpType.Zd ? Config.gapZdRf : Config.gapDsRf;
                    }
                    break;
                case 2:
                    if (matchType > 0)
                    {
                        gap = matchType == 1 ? Config.gapHtOu : matchType == 2 ? Config.gapXbOu : Config.gapDjOu;
                    }
                    else
                    {
                        gap = gpType == GpType.Zd ? Config.gapZdOu : Config.gapDsOu;
                    }
                    break;
                case 4:
                    if (matchType > 0)
                    {
                        gap = matchType == 1 ? Config.gapHtDy : matchType == 2 ? Config.gapXbDy : Config.gapDjDy;
                    }
                    else
                    {
                        gap = gpType == GpType.Zd ? Config.gapZdDy : Config.gapDsDy;
                    }
                    break;
                case 8:
                    if (!kInfo.IsManual && gpType != GpType.Zd)
                    {
                        gap = Config.gapDsOe;
                    }
                    else if (!kInfo.IsManual && gpType == GpType.Zd)
                    {
                        gap = Config.gapZdOe;
                    }
                    break;
            }

            #endregion

            foreach (BInfo bInfo in kInfo.BInfoDic.Values)
            {
                //保留數據避免汙染
                decimal tmpOdds = bInfo.Odds;
                //暫存計算值
                decimal oddsCalc = 0m;

                if (tmpOdds > 0m)
                {
                    //暫存計算值
                    oddsCalc = (tmpOdds + gap);

                    //依類型取小數點後N位
                    switch (kInfo.PlayTyp)
                    {
                        case 1:
                        case 2:
                        case 4:
                        case 8:
                            oddsCalc = decimal.Floor(oddsCalc * 100m) / 100m;
                            break;
                    }
                }

                bInfo.OddsCalc = oddsCalc <= 0m ? 0m : oddsCalc;
                //有Id能更新才加入
                if (bInfo.Id > 0) { dicSourceOdds.Add(bInfo.Id, tmpOdds); }
            }

            //開放才計算
            if (kInfo.IsOpen)
            {
                //排序bType
                List<BInfo> bList = (from bInfo in kInfo.BInfoDic.Values
                                     orderby bInfo.BType
                                     select bInfo).ToList();
                #region 加總Logic

                //RF、OU、DY、OE 才需做加總公式運算
                if ((kInfo.PlayTyp & (1 | 2 | 4 | 8)) > 0 && kInfo.OddsTotal > 0m && bList.Count == 2)
                {
                    OddsCalculate calculate = OddsCalculate.CalcOddsTotalFormula(kInfo.OddsTotal, bList[0].OddsCalc, bList[1].OddsCalc);
                    bList[0].OddsCalc = calculate.OddsA;
                    bList[1].OddsCalc = calculate.OddsB;
                }

                #endregion
            }
        }

        /// <summary>
        /// 防護邏輯
        /// </summary>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="kInfo">玩法資訊</param>
        /// <param name="gpdm">賽事gpdm</param>
        /// <param name="message">關閉訊息物件</param>
        private static bool[] ProtectionLogic(GameInfo gameInfo, KInfo kInfo, decimal gpdm, SystemMessage message)
        {
            GpType gpType = gameInfo.GpType;
            List<BInfo> bList = (from bInfo in kInfo.BInfoDic.Values
                                 orderby bInfo.BType
                                 select bInfo).ToList();

            //預設為來源開放的狀態(有連動關閉則以連動關閉為主)
            byte isOpenResult = kInfo.IsAffectClosed ? kInfo.IsOpenResult : (byte)(kInfo.IsOpen ? 1 : 0);
            //是否GP
            bool isFollow = true;
            //是否顯示
            bool isDisplay = true;
            //是否PL清0
            bool isOddsClearZero = false;
            //是否小格全部關閉
            bool isAllClose = false;
            //關閉訊息type列表
            List<int> typeList = new List<int>();
            //是否外網
            bool isYW = Config.source != Source.JZ;

            //RF
            if (kInfo.PlayTyp == 1)
            {
                //RF為兩項
                if (bList.Count == 2)
                {
                    BInfo bInfoA = bList[0];
                    BInfo bInfoB = bList[1];
                    decimal oddsA = bInfoA.GetLastOdds();
                    decimal oddsB = bInfoB.GetLastOdds();
                    decimal rfPkChange = Math.Abs(kInfo.LineChange);
                    decimal rfPk = kInfo.GetLastLine();

                    //針對只開0平的玩法，來源開出非0平時，脫勾關小格
                    if (IsErrorLine(rfPk, kInfo.KType))
                    {
                        isFollow = false;
                        isAllClose = true;
                    }

                    //PK上限，若超過上限值，脫鉤關小格
                    if (rfPk > kInfo.LineLimit)
                    {
                        isFollow = false;
                        isAllClose = true;
                        typeList.Add(61);
                    }

                    //倒數和小於1
                    if (oddsA > 0 && oddsB > 0 && (1 / (oddsA + 1) + 1 / (oddsB + 1)) <= 1)
                    {
                        isFollow = false;
                        isDisplay = false;
                        isAllClose = isYW;
                        typeList.Add(53);
                    }

                    //ZD關閉
                    if (gpType == GpType.Zd)
                    {
                        //外網防護
                        if (isYW)
                        {
                            //單邊大於2
                            if (oddsA > 2m || oddsB > 2m)
                            {
                                isDisplay = false;
                                isAllClose = true;
                                isFollow = false;
                                typeList.Add(52);
                            }

                            //RF_PK跳動 (三分球總數400%，其餘700%)
                            decimal changeValue;

                            switch (kInfo.KType)
                            {
                                case 12701://三分球總數
                                    changeValue = 2000m;
                                    break;
                                default:
                                    changeValue = 3500m;
                                    break;
                            }

                            //PK跳動超過
                            if (rfPkChange >= changeValue)
                            {
                                //脫勾關所有小格
                                isAllClose = true;
                                isFollow = false;
                                isOpenResult = 0;
                                typeList.Add(56);
                            }
                        }
                    }
                    else
                    {
                        //外網防護
                        if (isYW)
                        {
                            //PK跳動超過700
                            if (rfPkChange >= 3500m)
                            {
                                //脫勾關所有小格
                                isAllClose = true;
                                isFollow = false;
                                isOpenResult = 0;
                                typeList.Add(56);
                            }

                            //低PL跳動超過20
                            if (ConvertTools.LowChange(oddsA, oddsB, bInfoA.OddsChange, bInfoB.OddsChange) >= 0.2m)
                            {
                                //脫勾關所有小格
                                isAllClose = true;
                                isFollow = false;
                                isOpenResult = 0;
                                typeList.Add(57);
                            }
                        }
                    }

                    //有一邊PL小於等於0
                    if (oddsA <= 0m || oddsB <= 0m)
                    {
                        isOddsClearZero = true;
                    }

                    //PL清0,關閉玩法
                    if (isOddsClearZero)
                    {
                        bInfoA.OddsCalc = 0m;
                        bInfoB.OddsCalc = 0m;
                        isOpenResult = 0;
                    }
                }
                //異常數據直接關閉
                else
                {
                    isDisplay = false;
                }
            }
            //OU
            else if (kInfo.PlayTyp == 2)
            {
                //OU為兩項
                if (bList.Count == 2)
                {
                    BInfo bInfoO = bList[0];
                    BInfo bInfoU = bList[1];
                    decimal oddsO = bInfoO.GetLastOdds();
                    decimal oddsU = bInfoU.GetLastOdds();
                    decimal ouPkChange = Math.Abs(kInfo.LineChange);

                    //PK上限，若超過上限值，脫鉤關小格
                    if (kInfo.GetLastLine() > kInfo.LineLimit)
                    {
                        isFollow = false;
                        isAllClose = true;
                        typeList.Add(61);
                    }

                    //倒數和小於1
                    if (oddsO > 0 && oddsU > 0 && (1 / (oddsO + 1) + 1 / (oddsU + 1)) <= 1)
                    {
                        isFollow = false;
                        isDisplay = false;
                        isAllClose = isYW;
                        typeList.Add(53);
                    }

                    //ZD防護
                    if (gpType == GpType.Zd)
                    {
                        //外網防護
                        if (isYW)
                        {
                            //單邊大於2
                            if (oddsO > 2m || oddsU > 2m)
                            {
                                isDisplay = false;
                                isAllClose = true;
                                isFollow = false;
                                typeList.Add(52);
                            }

                            //OU_PK跳動 (三分球總數400%，其餘900%)
                            decimal changeValue;

                            switch (kInfo.KType)
                            {
                                case 12702://三分球總數
                                    changeValue = 2000m;
                                    break;
                                default:
                                    changeValue = 4500m;
                                    break;
                            }

                            //PK跳動超過
                            if (ouPkChange >= changeValue)
                            {
                                //脫勾關所有小格
                                isAllClose = true;
                                isFollow = false;
                                isOpenResult = 0;
                                typeList.Add(56);
                            }
                        }
                    }
                    else
                    {
                        //外網防護
                        if (isYW)
                        {
                            //PK跳動超過900
                            if (ouPkChange >= 4500m)
                            {
                                //脫勾關所有小格
                                isAllClose = true;
                                isFollow = false;
                                isOpenResult = 0;
                                typeList.Add(56);
                            }

                            //低PL跳動超過20
                            if (ConvertTools.LowChange(oddsO, oddsU, bInfoO.OddsChange, bInfoU.OddsChange) >= 0.2m)
                            {
                                //脫勾關所有小格
                                isAllClose = true;
                                isFollow = false;
                                isOpenResult = 0;
                                typeList.Add(57);
                            }
                        }
                    }

                    //有一邊PL小於等於0
                    if (oddsO <= 0m || oddsU <= 0m)
                    {
                        isOddsClearZero = true;
                    }

                    //PL清0,關閉玩法
                    if (isOddsClearZero)
                    {
                        bInfoO.OddsCalc = 0m;
                        bInfoU.OddsCalc = 0m;
                        isOpenResult = 0;
                    }
                }
                //異常數據直接關閉
                else
                {
                    isDisplay = false;
                }
            }
            //DY
            else if (kInfo.PlayTyp == 4)
            {
                //Dy為三項
                if (bList.Count == 2)
                {
                    BInfo bInfoA = bList[0];
                    BInfo bInfoB = bList[1];
                    decimal oddsA = bInfoA.GetLastOdds();
                    decimal oddsB = bInfoB.GetLastOdds();

                    //倒數和小於1
                    if (oddsA > 0 && oddsB > 0 && (1 / (oddsA + 1) + 1 / (oddsB + 1)) <= 1)
                    {
                        isFollow = false;
                        isDisplay = false;
                        isAllClose = isYW;
                        typeList.Add(53);
                    }

                    //DS
                    if (gpType != GpType.Zd)
                    {
                        //取自己Kinfo所在層後，挑選RF的Kinfo
                        if (gameInfo.ObjDic.TryGetValue(gpdm, out GpdmInfo gpdmInfo))
                        {
                            KInfo rfKinfo = gpdmInfo.Values.SingleOrDefault(p => p.PlayTyp == 1);

                            if (rfKinfo != null && rfKinfo.IsOpen)
                            {
                                //RF RT的DY 高於0.89(含)
                                if ((rfKinfo.LineAt == 0 && oddsB >= 0.89m) ||
                                    (rfKinfo.LineAt == 1 && oddsA >= 0.89m))
                                {
                                    isOpenResult = 0;
                                }

                                //RF RT的DY PL高於被RT
                                if ((rfKinfo.LineAt == 0 && oddsA < oddsB) ||
                                    (rfKinfo.LineAt == 1 && oddsA > oddsB))
                                {
                                    //2023/04/14改關閉單玩法
                                    isOpenResult = 0;
                                    typeList.Add(55);
                                }
                            }
                        }

                        //Dy小於0.07
                        if (oddsA <= 0.07m || oddsB <= 0.07m)
                        {
                            isOddsClearZero = true;

                            if (typeList.Count == 0)
                            {
                                typeList.Add(51);
                            }
                        }

                        //外網防護 低PL跳動超過20
                        if (isYW && (ConvertTools.LowChange(oddsA, oddsB, bInfoA.OddsChange, bInfoB.OddsChange) >= 0.2m))
                        {
                            //脫勾關所有小格
                            isAllClose = true;
                            isFollow = false;
                            isOpenResult = 0;
                            typeList.Add(57);
                        }
                    }

                    //有一邊PL小於等於0
                    if (oddsA <= 0m || oddsB <= 0m)
                    {
                        isOddsClearZero = true;
                    }

                    //達到PL清0邏輯,關閉玩法
                    if (isOddsClearZero)
                    {
                        bInfoA.OddsCalc = 0m;
                        bInfoB.OddsCalc = 0m;
                        isOpenResult = 0;
                    }
                }
                //異常數據直接關閉
                else
                {
                    isDisplay = false;
                }
            }
            //OE
            else if (kInfo.PlayTyp == 8)
            {
                //OE為兩項
                if (bList.Count == 2)
                {
                    BInfo bInfoO = bList[0];
                    BInfo bInfoE = bList[1];
                    decimal oddsO = bInfoO.GetLastOdds();
                    decimal oddsE = bInfoE.GetLastOdds();

                    //倒數和小於1
                    if (oddsO > 0 && oddsE > 0 && (1 / (oddsO + 1) + 1 / (oddsE + 1)) <= 1)
                    {
                        isFollow = false;
                        isDisplay = false;
                        isAllClose = isYW;
                        typeList.Add(53);
                    }

                    //DS
                    if (gpType != GpType.Zd)
                    {
                        //外網防護 低PL跳動超過20
                        if (isYW && (ConvertTools.LowChange(oddsO, oddsE, bInfoO.OddsChange, bInfoE.OddsChange) >= 0.2m))
                        {
                            //脫勾關所有小格
                            isAllClose = true;
                            isFollow = false;
                            isOpenResult = 0;
                        }
                    }

                    //外網防護 單邊大於1.5
                    if (isYW && (oddsO > 1.5m || oddsE > 1.5m))
                    {
                        isOpenResult = 0;
                        isDisplay = false;
                        isAllClose = true;
                        isFollow = false;
                        typeList.Add(52);
                    }

                    //有一邊PL小於等於0
                    if (oddsO <= 0m || oddsE <= 0m)
                    {
                        isOddsClearZero = true;
                    }

                    //PL清0,關閉玩法
                    if (isOddsClearZero)
                    {
                        bInfoO.OddsCalc = 0m;
                        bInfoE.OddsCalc = 0m;
                        isOpenResult = 0;
                    }
                }
                //異常數據直接關閉
                else
                {
                    isDisplay = false;
                }
            }

            //鴛鴦盤錯誤關閉玩法[1:PK 2:PL 4:讓頭不一致(JZ不判斷)]
            if (kInfo.SameErrorSwitch > 0)
            {
                if (isYW && (kInfo.SameErrorSwitch & 4) == 4)
                {
                    if (gpType != GpType.Zd)
                    {
                        isFollow = false;
                        isDisplay = false;
                        isAllClose = true;
                        typeList.Add(54);
                    }
                }

                if ((kInfo.SameErrorSwitch & 3) > 0)
                {
                    //DS
                    //1.外網GP：脫鉤 + 關小格，2024.11.14 改成關單個玩法，不脫勾
                    //2.山來源GP：關閉單個玩法，不脫鉤
                    //ZD
                    //1.外網GP：關閉單個玩法，不脫鉤
                    //2.山來源GP：關閉單個玩法，不脫鉤
                    isOpenResult = 0;
                }
            }

            //賽事當前狀態超過來源節次總時長 ex:4x10，Q1:10分10秒 => True
            if (gameInfo.IsOverDjTime)
            {
                //脫勾關所有小格
                isAllClose = true;
                isFollow = false;
                isOpenResult = 0;
                typeList.Add(60);
            }

            #region 設置關閉寫入訊息

            CloseInfo closeInfo;
            ConcurrentDictionary<long, ConcurrentDictionary<decimal, CloseInfo>> cdCloseRemind = message.CloseRemind;

            //字典已存在
            if (cdCloseRemind.ContainsKey(gameInfo.GId) && cdCloseRemind[gameInfo.GId].ContainsKey(gpdm))
            {
                closeInfo = cdCloseRemind[gameInfo.GId][gpdm];
            }
            //不存在則新建
            else
            {
                closeInfo = new CloseInfo(gameInfo.GType, gameInfo.GId, kInfo.KTypeGroup);
            }

            //設置kType集合
            List<int> kTypeList = new List<int>();
            kTypeList.Add(kInfo.KType);

            foreach (int type in typeList)
            {
                //首次設置remark
                if (closeInfo.remark.Count == 0)
                {
                    switch (type)
                    {
                        //Dy 任一端PL <= 0.07
                        case 51:
                            closeInfo.remark.Add("odds", 0.07);
                            break;
                        //單邊PL大於限制
                        case 52:
                            closeInfo.remark.Add("kTypeList", kTypeList);
                            closeInfo.remark.Add("odds", kInfo.PlayTyp == 8 ? 1.5 : 2);
                            break;
                        //倒數和<=1
                        case 53:
                        //PK跳動超過限制
                        case 56:
                        //PL跳動超過限制
                        case 57:
                        //PK跳動
                        case 58:
                            closeInfo.remark.Add("kTypeList", kTypeList);
                            break;
                    }
                }
                //是否串接remark
                else if (closeInfo.Type == type && closeInfo.remark.ContainsKey("kTypeList"))
                {
                    closeInfo.remark["kTypeList"] = (closeInfo.remark["kTypeList"] as List<int>).Union(kTypeList).ToList();
                }

                //首次設置Type
                if (closeInfo.Type == 0)
                {
                    //設置關閉種類Type
                    closeInfo.Type = type;
                }
            }

            //有Type才加入字典
            if (closeInfo.Type > 0)
            {
                cdCloseRemind.TryAdd(gameInfo.GId, new ConcurrentDictionary<decimal, CloseInfo>());
                cdCloseRemind[gameInfo.GId].TryAdd(gpdm, closeInfo);
            }

            #endregion

            //設置開放狀態
            kInfo.IsOpenResult = isOpenResult;
            //初始值連動關閉值
            kInfo.IsAffectClosed = false;
            //若為前台不顯示則設為脫鉤
            isFollow = isDisplay ? isFollow : false;

            return new bool[] { isDisplay, isFollow, isAllClose };
        }

        /// <summary>
        /// 驗證是否為錯誤PK，只允許開0平，但出現0平以外的值
        /// </summary>
        /// <param name="plate">PK</param>
        /// <param name="kType">kType</param>
        /// <returns></returns>
        private static bool IsErrorLine(decimal plate, int kType)
        {
            switch (kType)
            {
                case 12010: //第N顆三分球
                case 12011: //第N顆罰球
                case 12012: //優先獲得N分
                case 12021: //搶首
                case 12022: //搶尾
                case 12023: //單節最高得分
                case 12109: //上半最後得分 
                case 12112: //上半優先獲得N分
                case 12209: //下半首先得分
                case 12212: //下半優先獲得N分
                case 12309: //第一節最後得分
                case 12312: //第一節優先獲得N分
                case 12409: //第二節首先得分 
                case 12410: //第二節獲得第N個三分球
                case 12411: //第二節獲得第N個罰球
                case 12412: //第二節優先獲得N分
                case 12509: //第三節最後得分
                case 12510: //第三節獲得第N個三分球
                case 12511: //第三節獲得第N個罰球
                case 12512: //第三節優先獲得N分
                case 12609: //第四節首先得分
                case 12610: //第四節獲得第N個三分球
                case 12611: //第四節獲得第N個罰球
                case 12612: //第四節優先獲得N分
                case 12801: //首先/最後獲得兩分球
                case 12802: //首先/最後犯規
                case 12803: //首先獲得籃板球
                case 12804: //首先暫停
                    return plate != 0;
            }

            return false;
        }

        /// <summary>
        /// 判斷是否可自動移盤的玩法
        /// </summary>
        /// <param name="kType"></param>
        /// <returns></returns>
        private static bool IsAutoMovable(int kType)
        {
            switch (kType)
            {
                //全場
                case 12001:
                case 12002:
                case 12003:
                case 12004:
                //全場A隊
                case 12005:
                case 12006:
                //全場B隊
                case 12007:
                case 12008:
                //上半
                case 12101:
                case 12102:
                case 12103:
                case 12104:
                //上半A隊
                case 12105:
                case 12106:
                //上半B隊
                case 12107:
                case 12108:
                //下半
                case 12201:
                case 12202:
                case 12204:
                //Q1
                case 12301:
                case 12302:
                case 12303:
                case 12304:
                //Q1 A隊
                case 12305:
                case 12306:
                //Q1 B隊
                case 12307:
                case 12308:
                //Q3
                case 12501:
                case 12502:
                case 12503:
                case 12504:
                //Q3 A隊
                case 12505:
                case 12506:
                //Q3 B隊
                case 12507:
                case 12508:
                    return true;
                default:
                    return false;
            }
        }

        #region 設置DataRow

        /// <summary>
        /// 設定CreateGame暫存表
        /// </summary>
        /// <param name="gameInfo">賽事</param>
        /// <param name="random">亂數</param>
        /// <param name="row">建立數據dto</param>
        private static bool SetCreateGameRow(GameInfo gameInfo, int random, out BkGameCacheCreateDto row)
        {
            //生成gid
            string gid = gameInfo.GameDate.ToString("yyMMddHH") + random.ToString("00000000") + gameInfo.GType.ToString();

            row = new BkGameCacheCreateDto();

            row.Key = gameInfo.GetGameDictKey();
            row.AllyModeId = gameInfo.League.LeagueMode;
            row.LimitType = gameInfo.League.LimitType;
            row.AllyId = gameInfo.League.Id;
            row.SuffixId = gameInfo.Suffix.Id;
            row.GameDate = TimeStampHelper.ToUtcTimeStamp(gameInfo.GameDate);
            row.PaymentDate = TimeStampHelper.ToUtcTimeStamp(gameInfo.PaymentDate);
            row.GType = gameInfo.GType;
            row.GId = long.Parse(gid);
            row.IsLive = true;
            row.Status = gameInfo.GpType == GpType.Zd ? 5 : 0;
            row.Home = gameInfo.Home;
            row.TmaId = gameInfo.TeamA.Id;
            row.TmbId = gameInfo.TeamB.Id;
            row.TmaNameTw = gameInfo.TeamA.FullName[0];
            row.TmaNameCn = gameInfo.TeamA.FullName[1];
            row.TmaNameEn = gameInfo.TeamA.FullName[2];
            row.TmaNameVi = gameInfo.TeamA.FullName[3];
            row.TmaNameTh = gameInfo.TeamA.FullName[4];
            row.TmaNameIdn = gameInfo.TeamA.FullName[5];
            row.TmaName31 = gameInfo.TeamA.FullName[6];
            row.TmbNameTw = gameInfo.TeamB.FullName[0];
            row.TmbNameCn = gameInfo.TeamB.FullName[1];
            row.TmbNameEn = gameInfo.TeamB.FullName[2];
            row.TmbNameVi = gameInfo.TeamB.FullName[3];
            row.TmbNameTh = gameInfo.TeamB.FullName[4];
            row.TmbNameIdn = gameInfo.TeamB.FullName[5];
            row.TmbName31 = gameInfo.TeamB.FullName[6];
            row.AllyNameTw = gameInfo.League.FullName[0];
            row.AllyNameCn = gameInfo.League.FullName[1];
            row.AllyNameEn = gameInfo.League.FullName[2];
            row.AllyNameVi = gameInfo.League.FullName[3];
            row.AllyNameTh = gameInfo.League.FullName[4];
            row.AllyNameIdn = gameInfo.League.FullName[5];
            row.AllyName31 = gameInfo.League.FullName[6];
            row.SuffixNameTw = gameInfo.Suffix.FullName[0];
            row.SuffixNameCn = gameInfo.Suffix.FullName[1];
            row.SuffixNameEn = gameInfo.Suffix.FullName[2];
            row.SuffixNameVi = gameInfo.Suffix.FullName[3];
            row.SuffixNameTh = gameInfo.Suffix.FullName[4];
            row.SuffixNameIdn = gameInfo.Suffix.FullName[5];
            row.SuffixName31 = gameInfo.Suffix.FullName[6];
            row.QuarterTime = gameInfo.QuarterTimeFromSource;
            row.PrioritySource = gameInfo.GpType == GpType.Zd ? gameInfo.League.PrioritySourceId : 0;

            if (row.AllyId <= 0 ||
               (!string.IsNullOrEmpty(gameInfo.Suffix.Name) && row.SuffixId <= 0) ||
               row.TmaId <= 0 ||
               row.TmbId <= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 設定CreateOdds暫存表
        /// </summary>
        /// <param name="gId">賽事GID</param>
        /// <param name="gType">GType(3碼)</param>
        /// <param name="gpdm">GPDM</param>
        /// <param name="kInfo">玩法資訊</param>
        /// <param name="bInfo">單項資訊</param>
        /// <param name="lineDefault">預設值line</param>
        /// <param name="oddsDefault">預設值odds</param>
        private static BkOddsCacheCreateDto SetCreateOddsRow(long gId, int gType, decimal gpdm, KInfo kInfo, BInfo bInfo, int lineDefault, decimal oddsDefault)
        {
            //來源超過PK上限, 建立時以上限為主
            int tmpLine = kInfo.GetLastLine() > kInfo.LineLimit ? kInfo.LineLimit * 1000 : kInfo.Line;

            return new BkOddsCacheCreateDto()
            {
                Gpdm = Convert.ToInt64(gpdm),
                GId = gId,
                GType = gType,
                KType = kInfo.KType,
                BType = bInfo.BType,
                SetType = kInfo.SetType,
                SourceId = (int)Config.source,
                IsOpen = 0,
                LineAt = Convert.ToBoolean((kInfo.IsSourceFollow & 1) == 1 ? kInfo.LineAt : 1),
                Line = (kInfo.IsSourceFollow & 1) == 1 ? tmpLine : lineDefault,
                Odds = (float)((kInfo.IsSourceFollow & 2) == 2 ? 0 : oddsDefault)
            };
        }

        /// <summary>
        /// 設定UpdateGame暫存表
        /// </summary>
        /// <param name="dt">暫存表</param>
        /// <param name="gameInfo">賽事資訊</param>
        /// <param name="isMain">是否移到主庫</param>
        public static bool SetUpdateGameRow(GameInfo gameInfo, out BkGameCacheUpdateDtoV6 row, out int channelId, out int multiStatus, bool isMain = false)
        {
            row = new BkGameCacheUpdateDtoV6();
            channelId = 0;
            multiStatus = 0;

            //移到主庫則判斷是否有odds也被移到主庫，有才更新狀態，否則不更新
            if (isMain && !gameInfo.ObjDic.Values.Any(gpdm => gpdm.Values.Any(kInfo => kInfo.BInfoDic.Values.Any(b => b.Status == 1))))
            {
                return false;
            }

            bool? mainInMain = gameInfo.ObjDic.Values.FirstOrDefault(gpdm => gpdm.Key == 1)?.IsMain;

            row.Id = gameInfo.Id;
            row.GameDate = SetDBValue<long?>(gameInfo.GpType != GpType.Zd && mainInMain == true || Config.source == Source.JZ, TimeStampHelper.ToUtcTimeStamp(gameInfo.GameDate));
            row.PaymentDate = TimeStampHelper.ToUtcTimeStamp(gameInfo.PaymentDate);
            row.GId = gameInfo.GId;
            row.QuarterTime = gameInfo.QuarterTimeFromSource;

            //f_channelId 轉播資訊ID(SP判斷-1則保留原本值)
            //f_multiStatus 轉播二進制開關,(SP判斷255則保留原本值)
            //JZ以外來源、DS賽事但尚未開主玩法一律不更新
            if (Config.source == Source.JZ)
            {
                if (gameInfo.GpType == GpType.Zd || gameInfo.ObjDic.Values.SelectMany(a => a.Values).Any(a => a.KType == 12001))
                {
                    channelId = gameInfo.ChannelId;
                    multiStatus = gameInfo.MultiStatus;
                }
                else
                {
                    channelId = -1;
                    multiStatus = 255;
                }
            }
            else
            {
                channelId = -1;
                multiStatus = 255;
            }

            return true;
        }

        //開關決定設定值
        private static T SetDBValue<T>(bool isOpen, T value)
        {
            if (isOpen)
            {
                return value;
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// 設定SelectOdds暫存表
        /// </summary>
        /// <param name="gId">賽事gId</param>
        /// <param name="gpdm"></param>
        /// <param name="kInfo">玩法資訊</param>
        /// <param name="bInfo">單項資訊</param>
        public static BkOddsCacheUpdateDto SetSelectOddsRow(long gId, long gpdm, KInfo kInfo, BInfo bInfo)
        {
            return new BkOddsCacheUpdateDto()
            {
                Gpdm = gpdm,
                Id = bInfo.Id,
                GId = gId,
                KType = kInfo.KType,
                BType = bInfo.BType,
                SetType = Convert.ToByte(kInfo.SetType),
                SourceId = (int)Config.source,
                IsOpen = 0,
                LineAt = false,
                Line = 0,
                Odds = 0
            };
        }

        /// <summary>
        /// [新比賽庫]設定UpdateOdds暫存表
        /// </summary>
        /// <param name="gId">賽事gId</param>
        /// <param name="gpdm">分組代碼</param>
        /// <param name="kInfo">玩法資訊</param>
        /// <param name="bInfo">單項資訊</param>
        /// <param name="isClearZero">是否為PL清零</param>
        public static BkOddsCacheUpdateDto SetUpdateOddsRowNew(long gId, decimal gpdm, KInfo kInfo, BInfo bInfo, bool isClearZero = false, bool isRemove = false)
        {
            BkOddsCacheUpdateDto row = new BkOddsCacheUpdateDto();
            decimal odds = isClearZero ? 0m : bInfo.OddsCalc;

            //依類型取小數點後N位
            switch (kInfo.PlayTyp)
            {
                case 1:
                case 2:
                case 4:
                case 8:
                    odds = decimal.Floor(odds * 100m) / 100m;
                    break;
            }

            //來源消失則關閉
            byte isOpen = (byte)(isRemove || isClearZero ? 0 : kInfo.IsOpenResult);
            //來源超過PK上限, 建立時以上限為主
            int tmpLine = kInfo.GetLastLine() > kInfo.LineLimit ? kInfo.LineLimit * 1000 : kInfo.Line;
            //是否更新PL(odds為-998則保留庫裡數據)
            odds = ((kInfo.IsSourceFollow & 2) != 2) ? -998 : odds;

            row.Gpdm = Convert.ToInt64(gpdm);
            row.Id = bInfo.Id;
            row.GId = gId;
            row.KType = kInfo.KType;
            row.BType = bInfo.BType;
            row.SetType = kInfo.SetType;
            row.SourceId = (int)Config.source;
            row.IsOpen = isOpen;
            row.LineAt = Convert.ToBoolean(kInfo.LineAt);
            row.Line = tmpLine;
            row.Odds = (float)odds;

            return row;
        }

        /// <summary>
        /// [主庫]設定UpdateOdds暫存表
        /// </summary>
        /// <param name="gameInfo">賽事</param>
        /// <param name="gpdm">分組代碼</param>
        /// <param name="kInfo">玩法資訊</param>
        /// <param name="bInfo">單項資訊</param>
        /// <param name="isFollow">是否跟</param>
        /// <param name="isClearZero">是否為PL清零</param>
        /// <param name="isRemove">是否為來源消失</param>
        /// <param name="isClosedRatio">是否為小格全關</param>
        public static BkOddsUpdateDto SetUpdateMainOddsRow(GameInfo gameInfo, decimal gpdm, KInfo kInfo, BInfo bInfo, bool isDisplay, bool isFollow, bool isClearZero = false, bool isRemove = false, bool isClosedRatio = false, bool convertPkIsOpen = true, decimal convertPk = 0)
        {
            BkOddsUpdateDto row = new BkOddsUpdateDto();
            //來源消失則清0，否則為計算後PL
            decimal odds = isClearZero ? -999m : bInfo.OddsCalc;
            //判斷是否超過PK上限, 超過則關閉
            bool tmpSourceOpen = kInfo.IsOpen;

            //依類型取小數點後N位, RF,OU 額外判斷是否超過PK上限
            switch (kInfo.PlayTyp)
            {
                case 1:
                case 2:

                    if (kInfo.GetLastLine() + convertPk > kInfo.LineLimit) { tmpSourceOpen = false; }
                    odds = decimal.Floor(odds * 100m) / 100m;
                    break;

                case 4:
                case 8:
                    odds = decimal.Floor(odds * 100m) / 100m;
                    break;
            }

            //是否開放
            byte isOpen = kInfo.IsOpenResult;
            //是否更新PL(odds為-998則保留庫裡數據)
            odds = kInfo.IsOddsFollow && ((kInfo.IsSourceFollow & 2) != 2) ? -998 : odds;

            //若為來源消失或小格全關則設為關閉
            if (isRemove || isClosedRatio || !convertPkIsOpen)
            {
                isOpen = 0;
            }

            row.Gpdm = Convert.ToInt64(gpdm);
            row.Id = bInfo.Id;
            row.GId = gameInfo.GId;
            row.KType = kInfo.KType;
            row.BType = bInfo.BType;
            row.SetType = kInfo.SetType;
            row.SourceId = (int)Config.source;
            row.IsFollow = isFollow;
            row.IsOpen = isOpen;
            row.Display = isDisplay;
            row.LineAt = Convert.ToBoolean(kInfo.LineAt);
            row.Line = kInfo.Line + (int)(convertPk * 1000);
            row.Odds = (float)odds;
            row.Rate31 = (float)(1 + kInfo.OddsSet31 / 2);
            row.SourceOpen = tmpSourceOpen;
            row.IsZeroLine = kInfo.PlayTyp == 1 ? (kInfo.IsZeroLine ? 1 : 0) : (kInfo.PlayTyp == 2 ? 2 : 3);

            return row;
        }

        /// <summary>
        /// 轉換正盤
        /// </summary>
        private static decimal ConvertMainLine(KInfo kInfo, out bool isOpen)
        {
            isOpen = true;
            decimal tmpCovnertPK = 0;

            foreach (BInfo bInfo in kInfo.BInfoDic.Values)
            {
                //依類型取小數點後N位, RF,OU 額外判斷是否超過PK上限
                switch (kInfo.PlayTyp)
                {
                    case 1:
                        //RF 讓 PL小於0.74 PK + 200% 被讓 PL小於0.74 PK - 200%
                        if (bInfo.Odds < 0.74m && kInfo.Line != 0)
                        {
                            // A隊
                            if (bInfo.BType % 2 != 0)
                            {
                                if (kInfo.LineAt == 1) { tmpCovnertPK = 1m; }
                                //全場
                                else if (bInfo.BType == 120011)
                                {
                                    //當PK小於等於1.5且被讓PL為低水時，不轉換
                                    if (kInfo.Line > 1500) { tmpCovnertPK = -1m; }
                                }
                                //半場/單節
                                else
                                {
                                    //當PK小於等於0.5且被讓PL為低水時，不轉換
                                    if (kInfo.Line > 500) { tmpCovnertPK = -1m; }
                                }
                            }
                            //B隊
                            else
                            {
                                if (kInfo.LineAt == 0) { tmpCovnertPK = 1m; }
                                //全場
                                else if (bInfo.BType == 120012)
                                {
                                    //當PK小於等於1.5且被讓PL為低水時，不轉換
                                    if (kInfo.Line > 1500) { tmpCovnertPK = -1m; }
                                }
                                //半場/單節
                                else
                                {
                                    //當PK小於等於0.5且被讓PL為低水時，不轉換
                                    if (kInfo.Line > 500) { tmpCovnertPK = -1m; }
                                }
                            }
                        }

                        break;
                    case 2:
                        //OU 大 PL小於0.74 PK + 200% 小 PL小於0.74 PK - 200%
                        if (bInfo.Odds < 0.74m)
                        {
                            // A隊
                            if (bInfo.BType % 2 != 0) { tmpCovnertPK = 1m; }
                            //B隊
                            else
                            {
                                //轉換後出現負數/平手，則關單個玩法
                                if (kInfo.Line > 1000) { tmpCovnertPK = -1m; }
                                else { isOpen = false; }
                            }
                        }

                        break;
                }
            }

            return tmpCovnertPK;
        }

        #endregion

        #region 副名稱緩存

        /// <summary>
        /// 設定聯盟隊伍緩存
        /// </summary>
        public static void RefreshWebNameBuf(System.Windows.Forms.Control control)
        {
            try
            {
                //防止短時間內重複點擊
                YwCore.Common.SetControlEnabled(control, false);
                //刷新KMode預設值
                KModeDefault.RefreshHandler();
                //刷新Odds預設值
                OddsDefault.RefreshHandler();
                //重新載入
                WebName.Handle();
                //檢查賽事資訊
                CheckGameInfo();

                //清除聯盟隊伍Id
                foreach (GameInfo gameInfo in Config.gameDic.Values)
                {
                    if (gameInfo.League.Id == -1) { gameInfo.League.Id = 0; }
                    if (gameInfo.Suffix.Id == -1) { gameInfo.Suffix.Id = 0; }
                    if (gameInfo.TeamA.Id == -1) { gameInfo.TeamA.Id = 0; }
                    if (gameInfo.TeamB.Id == -1) { gameInfo.TeamB.Id = 0; }
                }

                //刷新緩存後清除清單
                Process.TeamApi.SendApiFailId.Clear();
                Process.TeamApi.SendApiAddTeamCN.Clear();

                if (Config.isFollow && control != null)
                {
                    Common.SendMessage.Send($"{Config.gpName}籃球刷新副名稱緩存成功");
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
            }
            finally
            {
                Task.Delay(5000).Wait();
                YwCore.Common.SetControlEnabled(control, true);
            }
        }

        /// <summary>
        /// 檢查賽事資訊(聯盟、後綴、隊伍)
        /// </summary>
        private static void CheckGameInfo()
        {
            //未啟動則跳過
            if (!Config.isFollow) { return; }
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;

            #region 查詢聯盟Id

            //挑選有聯盟Id的賽事
            List<GameInfo> gameList = (from game in gameDic.Values
                                       where game.League.Id > 0
                                       select game).ToList();
            if (gameList.Count > 0)
            {
                //聯盟分群
                List<List<GameInfo>> gameGroup = (from game in gameList
                                                  group game by new { game.League.Name } into tmpGroup
                                                  select tmpGroup.ToList()).ToList();
                for (int i = 0; i < gameGroup.Count; i++)
                {
                    SetLeague(gameGroup[i], true);
                }
            }

            #endregion

            #region 查詢後綴Id

            //挑選有後綴Id的賽事
            List<GameInfo> suffixGameList = (from game in gameDic.Values
                                             where !string.IsNullOrEmpty(game.Suffix.Name) && game.Suffix.Id > 0
                                             select game).ToList();
            if (suffixGameList.Count > 0)
            {
                //後綴分群
                List<List<GameInfo>> gameGroup = (from game in suffixGameList
                                                  group game by new { game.Suffix.Name } into tmpGroup
                                                  select tmpGroup.ToList()).ToList();
                for (int i = 0; i < gameGroup.Count; i++)
                {
                    SetSuffix(gameGroup[i], true);
                }
            }

            #endregion

            #region 查詢隊伍Id

            //挑選有隊伍Id的賽事
            List<List<GameInfo>> teamList = (from game in gameDic.Values
                                             where (game.TeamA.Id > 0 || game.TeamB.Id > 0)
                                             group game by new { game.League.Name } into tmpGroup
                                             select tmpGroup.ToList()).ToList();
            foreach (List<GameInfo> list in teamList)
            {
                SetTeam(list, true);
            }

            #endregion
        }

        /// <summary>
        /// 設定聯盟資訊
        /// </summary>
        /// <param name="allyId">API調整後的聯盟ID</param>
        /// <param name="allyApiModel">聯盟資訊</param>
        public static void SetAllyInfo(int allyId, AllyApiModel allyApiModel)
        {
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;

            #region 設定聯盟資訊

            foreach (GameInfo game in gameDic.Values)
            {
                if (game.League.Id <= 0 && game.League.FullNameJZ.SequenceEqual(allyApiModel.FullNameJZ))
                {
                    game.League.Id = allyId;

                    for (int i = 0; i < allyApiModel.FullNameJZ.Length; i++)
                    {
                        game.League.FullName[i] = allyApiModel.FullNameJZ[i];
                    }
                }
            }

            #endregion
        }

        /// <summary>
        /// 設定隊伍資訊
        /// </summary>
        /// <param name="teamId">API新建後的隊伍ID</param>
        /// <param name="teamApiModel">隊伍資訊</param>
        public static void SetTeamInfo(int teamId, TeamApiModel teamApiModel)
        {
            //賽事字典
            ConcurrentDictionary<string, GameInfo> gameDic = Config.gameDic;

            #region 設定隊伍資訊

            foreach (GameInfo game in gameDic.Values)
            {
                if (game.TeamA.Id <= 0 && game.TeamA.FullNameJZ.SequenceEqual(teamApiModel.FullNameJZ))
                {
                    game.TeamA.Id = teamId;
                    game.TeamA.FullName = teamApiModel.FullNameJZ;
                }

                if (game.TeamB.Id <= 0 && game.TeamB.FullNameJZ.SequenceEqual(teamApiModel.FullNameJZ))
                {
                    game.TeamB.Id = teamId;
                    game.TeamB.FullName = teamApiModel.FullNameJZ;
                }
            }

            #endregion
        }

        #endregion
    }
}