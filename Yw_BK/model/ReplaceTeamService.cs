#region 功能修改描述與時間

/*
    描述：聯盟替換功能
    日期：2025-11-04

    描述：監控版
    日期：2026-04-15

    描述：隊伍替換功能
    日期：2026-05-28
*/

#endregion

using Gp.ReplaceNameService;
using YwCore;
using YwCore.main;
using YwCore.model;
using YwCore.model.interfaces;
using ReplaceTeamModel = YwCore.model.ReplaceTeamModel;

namespace Yw_BK.model
{
    /// <summary>
    /// 替換聯盟名稱功能
    /// </summary>
    public class ReplaceTeamService : ReplaceTeamBase
    {
        /// <summary>
        /// 串流狀態標記(0:未連線、1:已連線、2:斷線)
        /// </summary>
        private int _healthFlag;
        /// <summary>
        /// 靜態雙向繫節替換清單
        /// </summary>
        private static ConcurrentBindingDictionary<ReplaceTeamKey, ReplaceTeamModel> _bindingDictionary = new ConcurrentBindingDictionary<ReplaceTeamKey, ReplaceTeamModel>(new ReplaceTeamComparer());
        /// <summary>
        /// 替換清單
        /// </summary>
        protected override ConcurrentBindingDictionary<ReplaceTeamKey, ReplaceTeamModel> ReplaceDic => _bindingDictionary;
        /// <summary>
        /// 存檔唯一值
        /// </summary>
        public override string SaveKey => Config.source.ToString();
        //是否需初始化(私有)
        private bool _needInit;
        /// <summary>
        /// 是否需初始化
        /// </summary>
        public override bool NeedInit => _needInit;
        /// <summary>
        /// 斷線監聽
        /// </summary>
        public override event EventHandler? OnDisconnnected;
        /// <summary>
        /// 重新連線監聽
        /// </summary>
        public override event EventHandler? OnReconnnected;
        /// <summary>
        /// 連線監聽
        /// </summary>
        public override event EventHandler? OnConnnected;

        /// <summary>
        /// 替換聯盟名稱功能
        /// </summary>
        /// <param name="cancellationToken"></param>
        public ReplaceTeamService(CancellationToken cancellationToken)
        {

            _ = Task.Run(async () =>
              {
                  while (!cancellationToken.IsCancellationRequested)
                  {
                      try
                      {
                          await Config.ReplaceNameMonitor.StartListeningAsync((TeamModifyStream stream) =>
                            {
                                if ((_healthFlag & 2) == 2)
                                {
                                    LogFile.SetLog(LogMode.Control, $"隊伍替換名稱 - 串流恢復");

                                    var InitModels = stream.Items.Select(a => new ReplaceTeamModel()
                                    {
                                        IsKeep = a.IsKeep,
                                        ReplaceName = a.ReplaceName ?? "",
                                        SourceId = a.SourceId ?? "",
                                        LeagueName = a.AllyName ?? "",
                                        TeamName = a.TeamName ?? "",
                                        Type = a.Type == ReplaceTeamType.TeamSourceId ? ReplaceType.SourceId : ReplaceType.Name
                                    });

                                    _needInit = !InitModels.ToHashSet(new TeamComparer()).SetEquals(ReplaceDic.ToList());

                                    OnReconnnected?.Invoke(this, EventArgs.Empty);
                                }
                                else if (_healthFlag == 0)
                                {
                                    LogFile.SetLog(LogMode.Control, $"隊伍替換名稱 - 串流開始");
                                    OnConnnected?.Invoke(this, EventArgs.Empty);
                                }

                                _healthFlag = 1;

                                if (_needInit)
                                {
                                    Init();
                                    return;
                                }

                                if (stream.Modify == ModifyType.Init)
                                {
                                    ReplaceDic.Clear();
                                }

                                foreach (var data in stream.Items)
                                {
                                    ReplaceTeamKey key;

                                    if (data.Type == ReplaceTeamType.TeamSourceId)
                                    {
                                        key = new ReplaceTeamKey(ReplaceType.SourceId, data.AllyName, data.TeamName, data.SourceId);
                                    }
                                    else
                                    {
                                        key = new ReplaceTeamKey(ReplaceType.Name, data.AllyName, data.TeamName, $"{data.AllyName}_{data.TeamName}");
                                    }

                                    switch (stream.Modify)
                                    {
                                        case ModifyType.Add:
                                        case ModifyType.Edit:
                                            ReplaceDic.AddOrUpdate(key, new ReplaceTeamModel() { IsKeep = data.IsKeep, ReplaceName = data.ReplaceName, SourceId = data.SourceId ?? "", LeagueName = data.AllyName ?? "", TeamName = data.TeamName ?? "", Type = key.Type });
                                            break;
                                        case ModifyType.Delete:
                                            ReplaceDic.TryRemove(key);
                                            break;
                                        case ModifyType.Init:
                                            ReplaceDic.TryAdd(key, new ReplaceTeamModel() { IsKeep = data.IsKeep, ReplaceName = data.ReplaceName, SourceId = data.SourceId ?? "", LeagueName = data.AllyName ?? "", TeamName = data.TeamName ?? "", Type = key.Type });
                                            break;
                                    }
                                }

                                LogFile.SetLog(LogMode.Control, $"隊伍替換名稱 - 同步設定:{stream.ToString()}");

                                SaveFile(ConfigFilePath.TeamReplacePath);
                            }, Config.source, cancellationToken);
                      }
                      catch (OperationCanceledException) { break; }
                      catch (Exception ex)
                      {
                          if ((_healthFlag & 2) != 2)
                          {
                              OnDisconnnected?.Invoke(this, EventArgs.Empty);
                              LogFile.SetLog(LogMode.Control, $"隊伍替換名稱 - 串流中止");
                          }

                          bool isFirst = _healthFlag == 0;
                          bool continuousErrors = (_healthFlag | 2) == 2;
                          _healthFlag |= 2;

                          //首次連線上即斷線
                          if (isFirst)
                          {
                              LoadFile(ConfigFilePath.TeamReplacePath);
                          }

                          //非連續性重試連線
                          if (!continuousErrors)
                          {
                              LogFile.SetLog(LogMode.Error, ex.ToString());
                          }

                          await Task.Delay(2000, cancellationToken);
                      }
                  }
              });
        }

        /// <summary>
        /// 加入替換條件
        /// </summary>
        /// <param name="key"></param>
        /// <param name="replaceName"></param>
        /// <returns></returns>
        public override (bool? success, string message) TryAdd(ReplaceTeamKey key, string replaceName)
        {
            GameInfo? game = null;
            var addModel = new ReplaceTeamModel() { Type = key.Type, ReplaceName = replaceName };

            if (ReplaceDic.BindingList.Any(a => a.ReplaceName == replaceName))
            {
                return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
            }

            if (key.Type == ReplaceType.Name)
            {
                game = Config.gameDic.Values.FirstOrDefault(a => $"{a.League.Name}_{a.TeamA.SourceName}" == key.Key || $"{a.League.Name}_{a.TeamB.SourceName}" == key.Key);

                if (game != null)
                {
                    addModel.LeagueName = game.League.Name;

                    if (key.TeamName.Equals(game.TeamA.SourceName))
                    {
                        addModel.TeamName = game.TeamA.SourceName;

                        if (game.TeamA.SourceName != game.TeamA.Name)
                        {
                            return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
                        }
                    }
                    else if (key.TeamName.Equals(game.TeamB.SourceName))
                    {
                        addModel.TeamName = game.TeamB.SourceName;

                        if (game.TeamB.SourceName != game.TeamB.Name)
                        {
                            return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
                        }
                    }
                    else
                    {
                        return (null, "");
                    }

                    try
                    {
                        if (ReplaceDic.TryAdd(key, addModel))
                        {
                            base.SaveFile(ConfigFilePath.TeamReplacePath);
                        }
                        else
                        {
                            return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
                        }

                        return Config.ReplaceNameMonitor.ModifyTeam(new List<Gp.ReplaceNameService.ReplaceTeamModel>() { new Gp.ReplaceNameService.ReplaceTeamModel()
                        {
                            IsKeep= addModel.IsKeep,
                            ReplaceName= addModel.ReplaceName,
                            SourceId= addModel.SourceId??"",
                            AllyName= addModel.LeagueName??"",
                            TeamName= addModel.TeamName??"",
                            Type= ReplaceTeamType.TeamName
                        }
                        }, ModifyType.Add, Config.source);
                    }
                    catch (Exception ex)
                    {
                        LogFile.SetLog(LogMode.Error, ex.ToString());
                        return (true, YwCore.Properties.Resources.Sync_Alert_ServiceOffline_Guide);
                    }
                }
                else
                {
                    return (null, "");
                }
            }
            else if (key.Type == ReplaceType.SourceId)
            {
                game = Config.gameDic.Values.FirstOrDefault(a => a.SourceId == key.Key);

                if (game != null)
                {
                    addModel.SourceId = key.Key;
                    addModel.LeagueName = game.League.Name;

                    if (key.TeamName.Equals(game.TeamA.SourceName))
                    {
                        addModel.TeamName = game.TeamA.SourceName;

                        if (game.TeamA.SourceName != game.TeamA.Name)
                        {
                            return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
                        }
                    }
                    else if(key.TeamName.Equals(game.TeamB.SourceName))
                    {
                        addModel.TeamName = game.TeamB.SourceName;

                        if (game.TeamB.SourceName != game.TeamB.Name)
                        {
                            return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
                        }
                    }
                    else
                    {
                        return (null, "");
                    }

                    try
                    {
                        if (ReplaceDic.TryAdd(key, addModel))
                        {
                            base.SaveFile(ConfigFilePath.TeamReplacePath);
                        }
                        else
                        {
                            return (false, YwCore.Properties.Resources.Team_Error_DuplicateName);
                        }

                        return Config.ReplaceNameMonitor.ModifyTeam(new List<Gp.ReplaceNameService.ReplaceTeamModel>() { new Gp.ReplaceNameService.ReplaceTeamModel()
                        {
                            IsKeep= addModel.IsKeep,
                            ReplaceName= addModel.ReplaceName,
                            SourceId= addModel.SourceId??"",
                            AllyName = addModel.LeagueName??"",
                            TeamName= addModel.TeamName??"",
                            Type= ReplaceTeamType.TeamSourceId
                        }
                        }, ModifyType.Add, Config.source);
                    }
                    catch (Exception ex)
                    {
                        LogFile.SetLog(LogMode.Error, ex.ToString());
                        return (true, YwCore.Properties.Resources.Sync_Alert_ServiceOffline_Guide);
                    }
                }
                else
                {
                    return (null, "");
                }
            }

            return (null, "Error");
        }

        /// <summary>
        /// 移除替換條件
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override (bool? success, string message) TryRemove(ReplaceTeamKey key)
        {
            try
            {
                if (ReplaceDic.TryRemove(key))
                {
                    SaveFile(ConfigFilePath.TeamReplacePath);
                }

                return Config.ReplaceNameMonitor.ModifyTeam(new List<Gp.ReplaceNameService.ReplaceTeamModel> { new Gp.ReplaceNameService.ReplaceTeamModel()
                    {
                        ReplaceName="",
                        SourceId= key.Type == ReplaceType.SourceId ? key.Key : "",
                        AllyName= key.LeagueName,
                        TeamName= key.TeamName,
                        Type= key.Type == ReplaceType.SourceId ? ReplaceTeamType.TeamSourceId : ReplaceTeamType.TeamName
                    }
                    }, ModifyType.Delete, Config.source);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
                return (true, YwCore.Properties.Resources.Sync_Alert_ServiceOffline_Guide);
            }
        }

        /// <summary>
        /// 查詢既有替換條件
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public override bool TryGetValue(ReplaceTeamKey key, out ReplaceTeamModel model)
        {
            return ReplaceDic.TryGetValue(key, out model);
        }

        /// <summary>
        /// 更新設定
        /// </summary>
        /// <param name="key"></param>
        /// <param name="updateValueFactory"></param>
        /// <returns></returns>
        public override (bool? success, string message) TryUpdate(ReplaceTeamKey key, Func<ReplaceTeamKey, ReplaceTeamModel, ReplaceTeamModel> updateValueFactory)
        {
            if (TryGetValue(key, out var model))
            {
                updateValueFactory.Invoke(key, model);

                SaveFile(ConfigFilePath.TeamReplacePath);

                try
                {
                    return Config.ReplaceNameMonitor.ModifyTeam(new List<Gp.ReplaceNameService.ReplaceTeamModel>() { new Gp.ReplaceNameService.ReplaceTeamModel()
                    {
                        IsKeep= model.IsKeep,
                        ReplaceName= model.ReplaceName,
                        SourceId= model.SourceId??"",
                        AllyName= model.LeagueName??"",
                        TeamName= model.TeamName??"",
                        Type= model.Type == ReplaceType.SourceId ? ReplaceTeamType.TeamSourceId : ReplaceTeamType.TeamName
                    }
                    }, ModifyType.Edit, Config.source);
                }
                catch (Exception ex)
                {
                    LogFile.SetLog(LogMode.Error, ex.ToString());
                    return (true, YwCore.Properties.Resources.Sync_Alert_ServiceOffline_Guide);
                }
            }
            else
            {
                return (null, "");
            }
        }

        /// <summary>
        /// 清除未保留項目
        /// </summary>
        public override (bool? success, string message) Clear()
        {
            bool result = true;

            foreach (var model in ReplaceDic.ToList())
            {
                if (!model.IsKeep)
                {
                    if (!Config.gameDic.Values.Any(a => model.Type == ReplaceType.Name
                    ? $"{a.League.Name}_{a.TeamA.SourceName}" == model.GetKey().Key || $"{a.League.Name}_{a.TeamB.SourceName}" == model.GetKey().Key
                    : $"{a.SourceId}_{a.TeamA.SourceName}" == $"{model.SourceId}_{model.TeamName}" || $"{a.SourceId}_{a.TeamB.SourceName}" == $"{model.SourceId}_{model.TeamName}"))
                    {
                        var tmp = TryRemove(model.GetKey());
                        result &= tmp.success ?? true;
                    }
                }
            }

            return (result, result ? "" : "Error, please try again.");
        }

        /// <summary>
        /// 初始化服務上的設定
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            try
            {
                var result = Config.ReplaceNameMonitor.ModifyTeam(ReplaceDic.ToList().Select(a => new Gp.ReplaceNameService.ReplaceTeamModel()
                {
                    IsKeep = a.IsKeep,
                    ReplaceName = a.ReplaceName,
                    SourceId = a.SourceId ?? "",
                    AllyName = a.LeagueName ?? "",
                    TeamName = a.TeamName ?? "",
                    Type = a.Type == ReplaceType.SourceId ? ReplaceTeamType.TeamSourceId : ReplaceTeamType.TeamName
                }).ToList(), ModifyType.Init, Config.source);

                if (result.success ?? false)
                {
                    _needInit = false;
                }

                return result.success ?? false;
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// 重新獲得初始化數據
        /// </summary>
        /// <returns></returns>
        public override bool Refresh()
        {
            try
            {
                _needInit = false;
                return Config.ReplaceNameMonitor.RefreshAllyModify(Config.source);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// 替換功能
        /// </summary>
        /// <returns></returns>
        public override ITeamReplacement GetReplacement()
        {
            return new TeamReplaceHandle(() => _bindingDictionary);
        }

        /// <summary>
        /// 排序規則
        /// </summary>
        public class ReplaceTeamComparer : IComparer<ReplaceTeamModel>
        {
            /// <summary>
            /// 優先度比較
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            public int Compare(ReplaceTeamModel x, ReplaceTeamModel y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return 1;
                if (y is null) return -1;

                // Type：大 → 小
                int c = y.Type.CompareTo(x.Type);
                if (c != 0)
                    return c;

                // SourceName：A → Z
                return string.Compare(
                    x.TeamName,
                    y.TeamName,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
