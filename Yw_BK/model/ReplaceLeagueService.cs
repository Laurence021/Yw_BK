#region 功能修改描述與時間

/*
    描述：聯盟替換功能
    日期：2025-11-04

    描述：監控版
    日期：2026-04-15
*/

#endregion

using Gp.ReplaceNameService;
using YwCore;
using YwCore.main;
using YwCore.model;
using YwCore.model.interfaces;

namespace Yw_BK.model
{
    /// <summary>
    /// 替換聯盟名稱功能
    /// </summary>
    public class ReplaceLeagueService : ReplaceLeagueBaseV2
    {
        /// <summary>
        /// 串流狀態標記(0:未連線、1:已連線、2:斷線)
        /// </summary>
        private int _healthFlag;
        /// <summary>
        /// 靜態雙向繫節替換清單
        /// </summary>
        private static ConcurrentBindingDictionary<ReplaceLeagueKey, ReplaceLeagueModel> _bindingDictionary = new ConcurrentBindingDictionary<ReplaceLeagueKey, ReplaceLeagueModel>(new ReplaceLeagueComparer());
        /// <summary>
        /// 替換清單
        /// </summary>
        protected override ConcurrentBindingDictionary<ReplaceLeagueKey, ReplaceLeagueModel> ReplaceDic => _bindingDictionary;
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
        public ReplaceLeagueService(CancellationToken cancellationToken)
        {

            _ = Task.Run(async () =>
              {
                  while (!cancellationToken.IsCancellationRequested)
                  {
                      try
                      {
                          await Config.ReplaceNameMonitor.StartListeningAsync((stream) =>
                            {
                                if ((_healthFlag & 2) == 2)
                                {
                                    LogFile.SetLog(LogMode.Control, $"聯盟替換名稱 - 串流恢復");

                                    var InitModels = stream.Items.Select(a => new ReplaceLeagueModel()
                                    {
                                        IsKeep = a.IsKeep,
                                        ReplaceName = a.ReplaceName ?? "",
                                        SourceId = a.SourceId ?? "",
                                        SourceName = a.SourceName ?? "",
                                        TeamA = a.TeamA ?? "",
                                        TeamB = a.TeamB ?? "",
                                        Type = a.Type == ReplaceAllyType.SourceId ? ReplaceType.SourceId : ReplaceType.Name
                                    });

                                    _needInit = !InitModels.ToHashSet(new AllyComparer()).SetEquals(ReplaceDic.ToList());

                                    OnReconnnected?.Invoke(this, EventArgs.Empty);
                                }
                                else if (_healthFlag == 0)
                                {
                                    LogFile.SetLog(LogMode.Control, $"聯盟替換名稱 - 串流開始");
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
                                    ReplaceLeagueKey key;

                                    if (data.Type == ReplaceAllyType.SourceId)
                                    {
                                        key = new ReplaceLeagueKey(ReplaceType.SourceId, data.SourceId);
                                    }
                                    else
                                    {
                                        key = new ReplaceLeagueKey(ReplaceType.Name, data.SourceName);
                                    }

                                    switch (stream.Modify)
                                    {
                                        case ModifyType.Add:
                                        case ModifyType.Edit:
                                            ReplaceDic.AddOrUpdate(key, new ReplaceLeagueModel() { IsKeep = data.IsKeep, ReplaceName = data.ReplaceName, SourceId = data.SourceId ?? "", SourceName = data.SourceName ?? "", TeamA = data.TeamA ?? "", TeamB = data.TeamB ?? "", Type = key.Type });
                                            break;
                                        case ModifyType.Delete:
                                            ReplaceDic.TryRemove(key);
                                            break;
                                        case ModifyType.Init:
                                            ReplaceDic.TryAdd(key, new ReplaceLeagueModel() { IsKeep = data.IsKeep, ReplaceName = data.ReplaceName, SourceId = data.SourceId ?? "", SourceName = data.SourceName ?? "", TeamA = data.TeamA ?? "", TeamB = data.TeamB ?? "", Type = key.Type });
                                            break;
                                    }
                                }

                                LogFile.SetLog(LogMode.Control, $"聯盟替換名稱 - 同步設定:{stream.ToString()}");

                                base.SaveFile(ConfigFilePath.LeagueReplacePath);
                            }, Config.source, cancellationToken);
                      }
                      catch (OperationCanceledException) { break; }
                      catch (Exception ex)
                      {
                          if ((_healthFlag & 2) != 2)
                          {
                              OnDisconnnected?.Invoke(this, EventArgs.Empty);
                              LogFile.SetLog(LogMode.Control, $"聯盟替換名稱 - 串流中止");
                          }

                          bool isFirst = _healthFlag == 0;
                          bool continuousErrors = (_healthFlag | 2) == 2;
                          _healthFlag |= 2;

                          //首次連線上即斷線
                          if (isFirst)
                          {
                              base.LoadFile(ConfigFilePath.LeagueReplacePath);
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
        public override bool? TryAdd(ReplaceLeagueKey key, string replaceName)
        {
            GameInfo? game = null;
            var addModel = new ReplaceLeagueModel() { Type = key.Type, ReplaceName = replaceName };

            if (key.Type == ReplaceType.Name)
            {
                game = Config.gameDic.Values.FirstOrDefault(a => a.League.SourceName == key.Key);

                if (game != null)
                {
                    addModel.SourceName = game.League.SourceName;

                    try
                    {
                        if (ReplaceDic.TryAdd(key, addModel))
                        {
                            base.SaveFile(ConfigFilePath.LeagueReplacePath);
                        }
                        else
                        {
                            return null;
                        }

                        return Config.ReplaceNameMonitor.ModifyAlly(new List<ReplaceAllyModel>() { new ReplaceAllyModel()
                        {
                            IsKeep= addModel.IsKeep,
                            ReplaceName= addModel.ReplaceName,
                            SourceId= addModel.SourceId??"",
                            SourceName= addModel.SourceName??"",
                            TeamA= addModel.TeamA??"",
                            TeamB= addModel.TeamB??"",
                            Type= ReplaceAllyType.LeagueName
                        }
                        }, ModifyType.Add, Config.source);
                    }
                    catch (Exception ex)
                    {
                        LogFile.SetLog(LogMode.Error, ex.ToString());
                        return false;
                    }
                }
            }
            else if (key.Type == YwCore.ReplaceType.SourceId)
            {
                game = Config.gameDic.Values.FirstOrDefault(a => a.SourceId == key.Key);

                if (game != null)
                {
                    addModel.SourceId = key.Key;
                    addModel.SourceName = game.League.SourceName;
                    addModel.TeamA = game.TeamA.Name;
                    addModel.TeamB = game.TeamB.Name;

                    try
                    {
                        if (ReplaceDic.TryAdd(key, addModel))
                        {
                            base.SaveFile(ConfigFilePath.LeagueReplacePath);
                        }
                        else
                        {
                            return null;
                        }

                        return Config.ReplaceNameMonitor.ModifyAlly(new List<ReplaceAllyModel>() { new ReplaceAllyModel()
                        {
                            IsKeep= addModel.IsKeep,
                            ReplaceName= addModel.ReplaceName,
                            SourceId= addModel.SourceId??"",
                            SourceName= addModel.SourceName??"",
                            TeamA= addModel.TeamA??"",
                            TeamB= addModel.TeamB??"",
                            Type= ReplaceAllyType.SourceId
                        }
                        }, ModifyType.Add, Config.source);
                    }
                    catch (Exception ex)
                    {
                        LogFile.SetLog(LogMode.Error, ex.ToString());
                        return false;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 移除替換條件
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override bool? TryRemove(ReplaceLeagueKey key)
        {
            try
            {
                if (ReplaceDic.TryRemove(key))
                {
                    SaveFile(ConfigFilePath.LeagueReplacePath);
                }

                return Config.ReplaceNameMonitor.ModifyAlly(new List<ReplaceAllyModel>() { new ReplaceAllyModel()
                    {
                        ReplaceName="",
                        SourceId= key.Type == ReplaceType.SourceId ? key.Key : "",
                        SourceName= key.Type == ReplaceType.Name ? key.Key : "",
                        TeamA="",
                        TeamB="",
                        Type= key.Type == ReplaceType.SourceId ? ReplaceAllyType.SourceId : ReplaceAllyType.LeagueName
                    }
                    }, ModifyType.Delete, Config.source);
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// 查詢既有替換條件
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public override bool TryGetValue(ReplaceLeagueKey key, out ReplaceLeagueModel model)
        {
            return ReplaceDic.TryGetValue(key, out model);
        }

        /// <summary>
        /// 取得受影響賽事
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override IEnumerable<ReplaceLeagueModel> GetEditGames(ReplaceLeagueKey key)
        {
            if (TryGetValue(key, out ReplaceLeagueModel model))
            {
                return Config.gameDic.Values.Where(a => a.League.SourceName == key.Key).Select(a => new ReplaceLeagueModel() { SourceName = a.League.SourceName, ReplaceName = model.ReplaceName, SourceId = a.SourceId, TeamA = a.TeamA.Name, TeamB = a.TeamB.Name, Type = key.Type });
            }
            else
            {
                return Enumerable.Empty<ReplaceLeagueModel>();
            }
        }

        /// <summary>
        /// 更新設定
        /// </summary>
        /// <param name="key"></param>
        /// <param name="updateValueFactory"></param>
        /// <returns></returns>
        public override bool? TryUpdate(ReplaceLeagueKey key, Func<ReplaceLeagueKey, ReplaceLeagueModel, ReplaceLeagueModel> updateValueFactory)
        {
            if (TryGetValue(key, out var model))
            {
                updateValueFactory.Invoke(key, model);

                SaveFile(ConfigFilePath.LeagueReplacePath);

                try
                {
                    return Config.ReplaceNameMonitor.ModifyAlly(new List<ReplaceAllyModel>() { new ReplaceAllyModel()
                    {
                        IsKeep= model.IsKeep,
                        ReplaceName= model.ReplaceName,
                        SourceId= model.SourceId??"",
                        SourceName= model.SourceName??"",
                        TeamA= model.TeamA??"",
                        TeamB= model.TeamB??"",
                        Type= model.Type == ReplaceType.SourceId ? ReplaceAllyType.SourceId : ReplaceAllyType.LeagueName
                    }
                    }, ModifyType.Edit, Config.source);
                }
                catch (Exception ex)
                {
                    LogFile.SetLog(LogMode.Error, ex.ToString());
                    return false;
                }
            }

            return null;
        }

        /// <summary>
        /// 清除未保留項目
        /// </summary>
        public override bool? Clear()
        {
            bool result = true;

            foreach (var model in ReplaceDic.ToList())
            {
                if (!model.IsKeep)
                {
                    if (!Config.gameDic.Values.Any(a => model.Type == ReplaceType.Name ? a.League.SourceName == model.SourceName : a.SourceId == model.SourceId))
                    {
                        bool? tmp = TryRemove(model.GetKey());
                        result &= tmp ?? true;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 初始化服務上的設定
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            try
            {
                bool result = Config.ReplaceNameMonitor.ModifyAlly(ReplaceDic.ToList().Select(a => new ReplaceAllyModel()
                {
                    IsKeep = a.IsKeep,
                    ReplaceName = a.ReplaceName,
                    SourceId = a.SourceId ?? "",
                    SourceName = a.SourceName ?? "",
                    TeamA = a.TeamA ?? "",
                    TeamB = a.TeamB ?? "",
                    Type = a.Type == ReplaceType.SourceId ? ReplaceAllyType.SourceId : ReplaceAllyType.LeagueName
                }).ToList(), ModifyType.Init, Config.source);

                if (result)
                {
                    _needInit = false;
                }

                return result;
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
        public override IReplacement GetReplacement()
        {
            return new LeagueReplaceHandle(() => _bindingDictionary);
        }

        /// <summary>
        /// 排序規則
        /// </summary>
        public class ReplaceLeagueComparer : IComparer<ReplaceLeagueModel>
        {
            /// <summary>
            /// 優先度比較
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            public int Compare(ReplaceLeagueModel x, ReplaceLeagueModel y)
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
                    x.SourceName,
                    y.SourceName,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
