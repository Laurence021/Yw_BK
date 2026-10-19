#region 功能修改描述與時間

/*
    描述：賽事資訊
    日期：2019-01-07

    描述：整合比分、時間、狀態比對區塊
    日期：2023-06-27

    描述：優化比對剩餘時間邏輯
    日期：2023-07-11

    描述：隊名加工處理(聯盟名有女、UXX時，加工至隊名)
    日期：2024-02-02

    描述: 調整CheckPlAndSet，ZD勾只跟PK，DY轉RF時低水低於0.74關閉
    日期：2024-06-12

    描述：添加山體聯盟全語系名稱
    日期：2024-07-19
*/

#endregion

using Newtonsoft.Json;
using System.Collections.Concurrent;
using YwCore;
using YwCore.main;
using YwCore.model.interfaces;

namespace Yw_BK.model
{
    [Serializable]
    public class GameInfo : IFilter
    {
        //紀錄資料異動(IsUpdate設置成true)時的時間戳
        protected long changeTick;
        //複製updateTick當前的時間戳，用來判斷入庫前後資料一致判斷
        protected long updatingTick;
        //是否更新
        private bool _IsUpdate;
        //是否移到主庫
        private bool _IsMoveMain;
        //賽制時間 game表f_quartertime欄位
        private byte _QuarterTime;
        //剩餘時間
        private int _LastSec;

        /// <summary>
        /// GP類型
        /// </summary>
        public GpType GpType { get; set; }
        /// <summary>
        /// [常用]來源數據更新索引
        /// </summary>
        public long DataId { get; set; }
        /// <summary>
        /// [更多]來源數據相關  key:來源gid   value:陣列索引 1:DataId  2:ErrorCount
        /// </summary>
        public ConcurrentDictionary<decimal, long[]> MoreDataDic { get; set; } = new ConcurrentDictionary<decimal, long[]>();
        /// <summary>
        /// 數據庫Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 賽事型態 [籃球:121單式  122滾球]
        /// </summary>
        public byte GType { get; set; }
        /// <summary>
        /// 賽事Id
        /// </summary>
        public long GId { get; set; }
        /// <summary>
        /// 是否開放
        /// </summary>
        public bool IsOpen { get; set; }
        /// <summary>
        /// 是否更新
        /// </summary>
        public bool IsUpdate
        {
            get => _IsUpdate;
            set
            {
                //設定可更新時紀錄當前時間戳
                if (value)
                {
                    changeTick = DateTime.Now.Ticks;
                    _IsUpdate = value;
                }
                else
                {
                    //時間戳不一致代表仍要更新
                    _IsUpdate = changeTick != updatingTick;
                }
            }
        }
        /// <summary>
        /// 是否更新(1:新比賽庫>>開賽時間異動須更新, 2:主庫>>當無權更新比分狀態時,用於判斷是否還需要更新主庫)
        /// </summary>
        public byte IsUpdateSwitch { get; set; }
        /// <summary>
        /// Game被移到主庫的時間
        /// </summary>
        public DateTime MoveMainTime { get; private set; }
        /// <summary>
        /// 是否移到主庫
        /// </summary>
        public bool IsMoveMain
        {
            get => _IsMoveMain;
            set
            {
                if (_IsMoveMain != value)
                {
                    MoveMainTime = value ? DateTime.Now : DateTime.MinValue;
                    IsUpdate = true;
                }

                _IsMoveMain = value;
            }
        }
        /// <summary>
        /// 是否從庫撈取開賽日期
        /// </summary>
        public bool IsDateFromDb { get; private set; }
        /// <summary>
        /// 數據庫內的賽事狀態
        /// </summary>
        public byte StatusDB { get; set; }
        /// <summary>
        /// 雷達Id  game表 f_matchId varchar(10)
        /// </summary>
        public string MatchId { get; set; }
        /// <summary>
        /// JZ轉播資訊字典  
        /// </summary>
        public ConcurrentDictionary<string, LiveUrlModel> LiveUrlDic { get; set; } = new ConcurrentDictionary<string, LiveUrlModel>();
        /// <summary>
        /// JZ轉播資訊 game表 f_liveUrl
        /// </summary>
        public string LiveUrl
        {
            get
            {
                if (Config.source == Source.JZ)
                {
                    LiveUrlModel live = LiveUrlDic.OrderBy(a => a.Key).FirstOrDefault(a => a.Value != null).Value;

                    if (live != null)
                    {
                        return live.getJson();
                    }
                    else
                    {
                        return "";
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        /// <summary>
        /// 轉播資訊Id
        /// </summary>
        public int ChannelId { get; set; }
        /// <summary>
        /// 純雷達流
        /// </summary>
        public int RadarChannel
        {
            get
            {
                if (!string.IsNullOrEmpty(LiveUrl) && LiveUrl.Contains("lvc\":\"R_"))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
        /// <summary>
        /// 轉播二進制開關(1:因雨延遲, 2:衛星轉播中斷)
        /// </summary>
        public int MultiStatus { get; set; }
        /// <summary>
        /// 是否超出單節賽制時間 (ex:4x10，Q1:10分10秒 => true)
        /// </summary>
        public bool IsOverDjTime { get; set; }
        /// <summary>
        /// 賽制時間 game表f_quartertime欄位
        /// </summary>
        public byte QuarterTime
        {
            get => _QuarterTime;
            set
            {
                //異動則設置要更新
                if (_QuarterTime != value) { IsUpdate = true; }
                _QuarterTime = value;
            }
        }
        /// <summary>
        /// 來源賽制 單節N分鐘，0:未設置，目前僅XB來源使用
        /// </summary>
        public byte QuarterTimeFromSource { get; set; }
        /// <summary>
        /// 來源進行時間，-1代表使用的是剩餘時間
        /// </summary>
        public int RunTimeFromSource { get; set; } = -1;

        /// <summary>
        /// 來源內頁Id
        /// </summary>
        public string SourceId { get; set; }

        //存放GameCache表內的聯盟優先來源提醒
        private int? _prioritySourceId;
        /// <summary>
        /// 聯盟優先來源提醒 (表:[t_gameBkCache](主要)、[t_ally] 欄位:[f_prioritySourceId])
        /// </summary>
        public int PrioritySourceId
        {
            get
            {
                if (_prioritySourceId.HasValue)
                {
                    return _prioritySourceId.Value;
                }
                else
                {
                    return League.PrioritySourceId;
                }
            }
            set
            {
                _prioritySourceId = value;
            }
        }

        /// <summary>
        /// 存放GameCache表內的聯盟模式id (表:[t_gameBkCache]  欄位:[f_allyModeId])
        /// </summary>
        private int? _allyModeId;
        /// <summary>
        /// 聯盟模式id  主要:[t_gameBkCache].[f_allyModeId]  次要:[t_ally].[f_modeId]
        /// </summary>
        public int AllyModeId
        {
            get
            {
                if (_allyModeId.HasValue)
                {
                    return _allyModeId.Value;
                }
                else
                {
                    return League.LeagueMode;
                }
            }
            set
            {
                _allyModeId = value;
            }
        }

        #region 賽事資訊

        /// <summary>
        /// 開賽日期
        /// </summary>
        public DateTime GameDate { get; private set; }
        /// <summary>
        /// 開賽日期(來源提供)
        /// </summary>
        public DateTime GameDateFromSource { get; private set; }
        /// <summary>
        /// 帳務日期
        /// </summary>
        public DateTime PaymentDate { get; private set; }
        /// <summary>
        /// 剩餘時間(取值時判斷來源若為進行時間，則轉換成剩餘時間)
        /// </summary>
        public int LastSec
        {
            get
            {
                if (RunTimeFromSource == -1)
                {
                    return _LastSec;
                }
                //來源為進行時間，需推算成該節剩餘時間
                else
                {
                    int lastSec = 0; //剩餘時間
                    int quarter = 0; //進行到哪一節

                    switch (Status)
                    {
                        case 1:
                        case 11:
                            quarter = 1;
                            break;
                        case 2:
                        case 12:
                            quarter = 2;
                            break;
                        case 3:
                            quarter = 3;
                            break;
                        case 4:
                            quarter = 4;
                            break;
                    }

                    if (quarter > 0)
                    {
                        //先取DB，後取來源
                        int quarterTime = QuarterTime > 0 ? QuarterTime : QuarterTimeFromSource;
                        //計算剩餘時間  總秒數(進行節次*單節多少分鐘*60秒) - 來源進行時間
                        lastSec = quarter * quarterTime * 60 - RunTimeFromSource;
                    }

                    return lastSec > 0 ? lastSec : 0;//防止計算後為負值
                }
            }
            set { _LastSec = value; }
        }
        /// <summary>
        /// 狀態[0:其他, 1:Q1, 2:Q2, 3:Q3, 4:Q4, 5:滾球中, 6:HT, 7:暫停，11:1H, 12:2H, 13:OT]
        /// </summary>
        public int Status { get; set; }
        /// <summary>
        /// 聯盟
        /// </summary>
        public League League { get; set; } = new League();
        /// <summary>
        /// 後綴
        /// </summary>
        public Suffix Suffix { get; set; } = new Suffix();
        /// <summary>
        /// 來源A隊名稱
        /// </summary>
        public Team TeamA { get; set; }
        /// <summary>
        /// 來源B隊名稱
        /// </summary>
        public Team TeamB { get; set; }
        /// <summary>
        /// 主隊標記 [中立:0  A隊:1  B隊:2]
        /// </summary>
        public int Home { get; set; }

        #region 比分

        /// <summary>
        /// 比分陣列(FT)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase Score { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(1H)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase ScoreHalf1 { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(2H)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase ScoreHalf2 { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(Q1)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase Score1 { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(Q2)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase Score2 { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(Q3)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase Score3 { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(Q4)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase Score4 { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };
        /// <summary>
        /// 比分陣列(三分球)  補充：後台限制0-255，因此GP解析限制byte存入，入庫也用byte
        /// </summary>
        public ScoreBase TpNum { get; set; } = new ScoreBase() { MaxValue = byte.MaxValue };

        #endregion

        #endregion

        /// <summary>
        /// key:gpdm, value:玩法字典(key:kType, value:玩法資訊)
        /// </summary>
        public ConcurrentDictionary<decimal, GpdmInfo> ObjDic { get; set; } = new ConcurrentDictionary<decimal, GpdmInfo>();

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="dataId">數據索引</param>
        public GameInfo(long dataId = 0)
        {
            SourceId = "";
            DataId = dataId;
            League = new League();
            TeamA = new Team(League);
            TeamB = new Team(League);
            MatchId = "";
        }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="sourceId">來源單場ID</param>
        /// <param name="dataId">數據索引</param>
        public GameInfo(string sourceId, long dataId = 0)
        {
            SourceId = sourceId;
            DataId = dataId;
            League = new League(sourceId, Config.GetReplaceLeagueService.GetReplacement());
            TeamA = new Team(League, sourceId, Config.GetReplaceTeamService.GetReplacement());
            TeamB = new Team(League, sourceId, Config.GetReplaceTeamService.GetReplacement());
            MatchId = "";
        }

        /// <summary>
        /// 設定開賽、帳務日
        /// </summary>
        /// <param name="dateTime">開賽時間</param>
        /// <param name="isFromDb">是否有庫撈庫</param>
        public void SetGameDate(DateTime dateTime, bool isFromDb = false)
        {
            if (isFromDb)
            {
                GameDateFromSource = dateTime;
                IsDateFromDb = isFromDb;
            }
            else
            {
                GameDate = dateTime;
                PaymentDate = YwCore.Common.GetPaymentDate(dateTime);
            }
        }

        /// <summary>
        /// 取得所屬聯盟唯一值
        /// </summary>
        public string GetUniqueLeague() => string.Join('-', League.Name, Suffix.Name, GpType).Trim('-');

        /// <summary>
        /// 取得GameKey
        /// </summary>
        /// <returns></returns>
        public string GetGameDictKey()
        {
            if (GpType != GpType.Zd)
            {
                return $"{PaymentDate.ToString("yyyyMMdd")}￠{GType}￠{League.Name}￠{Suffix.Name}￠{TeamA.Name}￠{TeamB.Name}";
            }
            else
            {
                return $"{GType}￠{League.Name}￠{Suffix.Name}￠{TeamA.Name}￠{TeamB.Name}";
            }
        }

        /// <summary>
        /// 取得原始GameKey(因應來源名稱替換功能，需要用來判斷修改前的賽事)
        /// </summary>
        /// <returns></returns>
        public string GetSourceGameDictKey()
        {
            if (GpType != GpType.Zd)
            {
                return $"{PaymentDate.ToString("yyyyMMdd")}￠{GType}￠{League.Name}￠{Suffix.Name}￠{TeamA.SourceName}￠{TeamB.SourceName}";
            }
            else
            {
                return $"{GType}￠{League.Name}￠{Suffix.Name}￠{TeamA.SourceName}￠{TeamB.SourceName}";
            }
        }

        /// <summary>
        /// 取得匹配主盤使用的key(AB隊同名稱的玩法使用)
        /// </summary>
        /// <param name="home">主客隊查找標記</param>
        /// <returns></returns>
        public string[] GetPairKey(int home = 0)
        {
            string tmp = $"{GameDate.ToString("yyyyMMdd HH:mm")}￠{GType}￠{League.Name}￠{Suffix.Name}￠{{0}}";
            string homeTeam = "";
            string awayTeam = "";

            //TeamA為主隊
            if (Home == 1)
            {
                homeTeam = string.Format(tmp, TeamA.Name);
                awayTeam = string.Format(tmp, TeamB.Name);
            }
            //TeamB為主隊
            else if (Home == 2)
            {
                homeTeam = string.Format(tmp, TeamB.Name);
                awayTeam = string.Format(tmp, TeamA.Name);
            }
            else
            {
                return new string[] { string.Format(tmp, TeamA.Name), string.Format(tmp, TeamB.Name) };
            }

            //欲查找之隊伍
            switch (home)
            {
                //查主隊
                case 1:
                    return new string[] { homeTeam };
                //查客隊
                case 2:
                    return new string[] { awayTeam };
                default:
                    return new string[] { homeTeam, awayTeam };
            }
        }

        /// <summary>
        /// 取得SetType值
        /// </summary>
        /// <param name="kType">玩法</param>
        /// <returns></returns>
        public byte GetSetType(int kType)
        {
            byte result = 0;
            //鴛鴦盤第N盤
            byte sameNum = 1;

            List<byte> setTypeList = new List<byte>();

            foreach (GpdmInfo tmp in ObjDic.Values)
            {
                if (tmp.TryGetValue(kType, out KInfo kInfo))
                {
                    setTypeList.Add(kInfo.SetType);
                }
            }

            while (true)
            {
                result = sameNum;

                if (!setTypeList.Contains(result))
                {
                    break;
                }

                sameNum++;
            }

            return result;
        }

        /// <summary>
        /// 淺層複製
        /// </summary>
        /// <returns>複製後物件</returns>
        public GameInfo Clone()
        {
            return this.MemberwiseClone() as GameInfo;
        }

        /// <summary>
        /// 複製基本數據
        /// </summary>
        /// <returns></returns>
        public GameInfo CloneBase()
        {
            GameInfo newGameInfo = new GameInfo(SourceId);
            newGameInfo.GType = GType;
            newGameInfo.GpType = GpType;
            newGameInfo.PaymentDate = PaymentDate;
            newGameInfo.GameDate = GameDate;
            newGameInfo.GameDateFromSource = GameDateFromSource;
            newGameInfo.IsDateFromDb = IsDateFromDb;
            newGameInfo.Status = Status;
            newGameInfo.LastSec = LastSec;
            newGameInfo.League.Name = League.Name;
            newGameInfo.TeamA.Name = TeamA.Name;
            newGameInfo.TeamB.Name = TeamB.Name;
            newGameInfo.Suffix.Name = Suffix.Name;
            return newGameInfo;
        }

        /// <summary>
        /// 新舊比對(新物件寫入舊物件)
        /// </summary>
        /// <param name="newGame">新賽事資訊</param>
        /// <param name="gpType">GP類型</param>
        public void Compare(GameInfo newGame, GpType gpType)
        {
            //判斷使用
            Source tmpSource = Config.source;

            //比對賽事資訊
            if (gpType == GpType)
            {
                DataId = newGame.DataId;

                //是否開賽時間相等
                bool isGameDateEqual = newGame.GameDate.Equals(GameDate);

                //是否相等
                bool isEqual = isGameDateEqual &&
                               newGame.GameDate.Equals(GameDate) &&
                               newGame.MatchId == MatchId &&
                               newGame.LiveUrl == LiveUrl &&
                               newGame.ChannelId == ChannelId &&
                               newGame.MultiStatus == MultiStatus &&
                               newGame.QuarterTimeFromSource == QuarterTimeFromSource;

                //更新來源開賽時間
                if (newGame.GameDateFromSource != DateTime.MinValue)
                {
                    GameDateFromSource = newGame.GameDateFromSource;
                }

                //更新NBB開賽時間
                if (newGame.GameDate != DateTime.MinValue)
                {
                    GameDate = newGame.GameDate;
                    PaymentDate = newGame.PaymentDate;
                }

                MatchId = newGame.MatchId;          //JZ雷達
                LiveUrlDic = newGame.LiveUrlDic;    //JZ轉播
                ChannelId = newGame.ChannelId;
                MultiStatus = newGame.MultiStatus;
                IsOverDjTime = newGame.IsOverDjTime;
                QuarterTimeFromSource = newGame.QuarterTimeFromSource;

                //不同則設為更新
                if (!isEqual)
                {
                    //開賽時間異動需標記
                    if (isGameDateEqual == false)
                    {
                        IsUpdateSwitch |= 1;
                    }

                    IsUpdate = true;
                    IsUpdateSwitch |= 2;
                }

                if (Config.source == Source.JZ)
                {
                    //新舊JZ來源主名稱不相同則ID清0重查名稱
                    if (!League.FullNameJZ.SequenceEqual(newGame.League.FullNameJZ))
                    {
                        League.FullNameJZ = newGame.League.FullNameJZ;
                        League.Id = 0;
                    }

                    if (!TeamA.FullNameJZ.SequenceEqual(newGame.TeamA.FullNameJZ))
                    {
                        TeamA.FullNameJZ = newGame.TeamA.FullNameJZ;
                        TeamA.Id = 0;
                    }

                    if (!TeamB.FullNameJZ.SequenceEqual(newGame.TeamB.FullNameJZ))
                    {
                        TeamB.FullNameJZ = newGame.TeamB.FullNameJZ;
                        TeamB.Id = 0;
                    }
                }
            }

            //比分、時間、狀態比對區塊，需為對應頁面才比對
            if (tmpSource == Source.BET365 || tmpSource == Source.JZ || tmpSource == Source.VBET || tmpSource == Source.SB || tmpSource == Source.Radar ||
                (tmpSource == Source.XB && gpType == GpType.Zd) ||
                (tmpSource == Source.MyStake && gpType == GpType.ZdMore))
            {
                #region 比對差異並取代舊值

                //是否進行狀態相等
                bool isStatusEqual = newGame.Status.Equals(Status);
                //是否相等
                bool isEqual = isStatusEqual &&
                               newGame.Score.EqualsObject(Score) &&
                               newGame.ScoreHalf1.EqualsObject(ScoreHalf1) &&
                               newGame.ScoreHalf2.EqualsObject(ScoreHalf2) &&
                               newGame.Score1.EqualsObject(Score1) &&
                               newGame.Score2.EqualsObject(Score2) &&
                               newGame.Score3.EqualsObject(Score3) &&
                               newGame.Score4.EqualsObject(Score4) &&
                               newGame.IsOverDjTime == IsOverDjTime;

                //距離前次更新是否超過3秒
                bool isForcedUpdate = TimeSpan.FromTicks(DateTime.Now.Ticks - updatingTick).TotalSeconds >= 3;

                //依據來源時間格式判斷需檢查哪一種
                //-1表示該場來源時間格式為"該節剩餘時間"
                if (newGame.RunTimeFromSource == -1)
                {
                    bool isLastSecEqual = newGame.LastSec.Equals(LastSec);

                    if (isLastSecEqual == false)
                    {
                        if (isStatusEqual)
                        {
                            //1.狀態一致時，剩餘時間不一致，則檢查誤差範圍，太相近則暫緩更新
                            //2.距離前次超過N秒未更新，則直接更新
                            if (Math.Abs(newGame.LastSec - LastSec) >= 5 || newGame.LastSec == 0 || isForcedUpdate)
                            {
                                isEqual = false;
                                LastSec = newGame.LastSec;
                            }
                        }
                        else
                        {
                            LastSec = newGame.LastSec;
                        }
                    }
                }
                else
                {
                    bool isRunTimeFromSourceEqual = newGame.RunTimeFromSource.Equals(RunTimeFromSource);

                    if (isRunTimeFromSourceEqual == false)
                    {
                        if (isStatusEqual)
                        {
                            //1.狀態一致時，剩餘時間不一致，則檢查誤差範圍，太相近則暫緩更新
                            //2.距離前次超過N秒未更新，則直接更新
                            if (Math.Abs(newGame.RunTimeFromSource - RunTimeFromSource) >= 5 || isForcedUpdate)
                            {
                                isEqual = false;
                                RunTimeFromSource = newGame.RunTimeFromSource;
                            }
                        }
                    }

                    LastSec = 0;
                }

                //設置進行時間
                RunTimeFromSource = newGame.RunTimeFromSource;

                //更新數據
                Status = newGame.Status;
                Score = newGame.Score;
                ScoreHalf1 = newGame.ScoreHalf1;
                ScoreHalf2 = newGame.ScoreHalf2;
                Score1 = newGame.Score1;
                Score2 = newGame.Score2;
                Score3 = newGame.Score3;
                Score4 = newGame.Score4;
                IsOverDjTime = newGame.IsOverDjTime;
                QuarterTimeFromSource = newGame.QuarterTimeFromSource;

                //不同則設為更新
                if (!isEqual)
                {
                    IsUpdate = true;
                }

                #endregion
            }

            OddsCompare(newGame, gpType);

            #region 關盤監控

            //未勾選賽事不寫關盤監控
            if ((gpType & GpType.Zd) == GpType.Zd && !IsFilter())
            {
                foreach (KeyValuePair<decimal, GpdmInfo> newGpdmPair in newGame.ObjDic)
                {
                    decimal gpdm = newGpdmPair.Key;

                    //group存在
                    if (ObjDic.TryGetValue(gpdm, out GpdmInfo tmpGpdm))
                    {
                        //是否移到主庫
                        if (tmpGpdm.IsMain)
                        {
                            //取出RF、OU(關盤監控只判斷這2個)
                            var kInfoPair = tmpGpdm.Where(a => a.Value.PlayTyp == 1 || a.Value.PlayTyp == 2);
                            //是否RF、OU PK都關盤
                            bool isClose = kInfoPair.All(a => !a.Value.IsOpen);

                            if (isClose)
                            {
                                if (tmpGpdm.CanWrite)
                                {
                                    //加入關閉監控字典
                                    ZdCloseNotify closeNotify = new ZdCloseNotify(GId, GType, gpdm, kInfoPair.Select(a => a.Key).ToList(), GameDate);
                                    Config.CloseNotify.AddDic(closeNotify);
                                    tmpGpdm.CanWrite = false;
                                }
                            }
                            else
                            {
                                Config.CloseNotify.RemoveDic($"{GId}￠{gpdm}");
                                tmpGpdm.CanWrite = true;
                            }
                        }
                    }
                }
            }

            #endregion
        }

        /// <summary>
        /// 玩法比對
        /// </summary>
        /// <param name="newGame"></param>
        /// <param name="gpType"></param>
        protected virtual void OddsCompare(GameInfo newGame, GpType gpType)
        {
            //有數據才比對
            if (newGame.ObjDic.Count > 0)
            {
                //無玩法則直接複製
                if (ObjDic.Count == 0)
                {
                    ObjDic = new ConcurrentDictionary<decimal, GpdmInfo>(newGame.ObjDic);
                }
                else
                {
                    foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in newGame.ObjDic)
                    {
                        decimal gpdm = gpdmPair.Key;

                        //group存在
                        if (ObjDic.TryGetValue(gpdm, out GpdmInfo tmpObj))
                        {
                            tmpObj.IsUpdateTsSource |= tmpObj.TsSource != gpdmPair.Value.TsSource;
                            tmpObj.Display = gpdmPair.Value.Display;
                            tmpObj.Group = gpdmPair.Value.Group;
                            tmpObj.TsSource = gpdmPair.Value.TsSource;
                            //來源數據取代GP提前生成
                            tmpObj.IsGenerate = gpdmPair.Value.IsGenerate;

                            //處理各個玩法
                            foreach (KeyValuePair<int, KInfo> kPair in gpdmPair.Value)
                            {
                                //kType存在，取出玩法資訊
                                if (tmpObj.TryGetValue(kPair.Key, out KInfo getKinfo))
                                {
                                    //SetType一致才比對
                                    if (getKinfo.SetType == kPair.Value.SetType)
                                    {
                                        if (getKinfo.StrType == kPair.Value.StrType)
                                        {
                                            getKinfo.Compare(kPair.Value);
                                        }
                                        else if (getKinfo.StrType != kPair.Value.StrType && getKinfo.IsCanReplace)
                                        {
                                            getKinfo.Compare(kPair.Value);
                                        }
                                    }
                                }
                                //kType不存在則直接添加
                                else
                                {
                                    tmpObj.TryAdd(kPair.Key, kPair.Value);
                                }
                            }
                        }
                        //kType不存在則直接添加
                        else
                        {
                            ObjDic.TryAdd(gpdmPair.Key, gpdmPair.Value);
                        }
                    }
                }
            }

        }

        /// <summary>
        /// 賽事不存在處理(清除數據庫Id)
        /// </summary>
        public void GameNotExistHandle()
        {
            Id = 0;
            GId = 0;
            _prioritySourceId = null;
            _allyModeId = null;

            foreach (GpdmInfo pair in ObjDic.Values)
            {
                foreach (KInfo kInfo in pair.Values)
                {
                    foreach (BInfo bInfo in kInfo.BInfoDic.Values)
                    {
                        bInfo.Id = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 標記更新入庫中
        /// </summary>
        public void SetUpdatingTick()
        {
            updatingTick = changeTick;
        }

        /// <summary>
        /// 是否篩選為不GP
        /// </summary>
        /// <returns></returns>
        public bool IsFilter()
        {
            return Config.BlockList.BlockLeague(GetUniqueLeague()) ||
                  Config.BlockList.BlockGame(GetGameDictKey());
        }

        /// <summary>
        /// ZD勾選只跟PK(GP端): 檢查RF,OU,DY ，低水低於0.5不抓
        /// </summary>
        public void CheckPlAndSet(bool isOpenConvertML = false)
        {
            #region 需求描述

            /*
               描述：新需求 ZD GP 防護邏輯優化
               (原需求名稱;  NBA+其他籃球ZD，所有來源，RF抓取邏輯優化)
               日期：2023-09-27

                1.調整ZD參數設置"勾上只跟PK"時
                (1)全場來源RF PK為0.5時關盤抓DY，如沒有DY則關盤。

                2.調整ZD參數設置"勾上只跟PK"、"未勾正盤轉換"時
                (1)抓取來源RF跟DY最平水的一盤。
                (2)來源RF未開，只開DY，不抓取。
                (3)RF、OU、DY低水低於0.50不抓取。

                3.調整ZD參數設置"未勾只跟PK"時
                (1)抓取來源正盤，水位跟高低水。
                (2)來源RF未開，只開DY，抓取DY，水位跟高低水。
             */

            #endregion

            //ZD + GP參數勾只跟PK才處理
            if (GpType == GpType.Zd && Config.isZdOnlyPK)
            {
                foreach (KeyValuePair<decimal, GpdmInfo> gpdmPair in ObjDic)
                {
                    foreach (KInfo kInfo in gpdmPair.Value.Values.Where(a => a.KType == 12001 && a.Line == 500 && a.IsOpen))
                    {
                        kInfo.IsOpen = false;
                    }

                    if (!isOpenConvertML)
                    {
                        decimal odds = Config.source == Source.PIN ? 1.5m : 0.5m;

                        foreach (KInfo kInfo in gpdmPair.Value.Values.Where(a => a.IsOpen && a.PlayTyp != 8))
                        {
                            if (kInfo.BInfoDic.Values.Any(a => a.Odds < odds))
                            {
                                kInfo.IsOpen = false;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 聯盟
    /// </summary>
    [Serializable]
    public class League : FullNameBase
    {
        /// <summary>
        /// 來源單場賽事ID
        /// </summary>
        public string SourceId { get; set; }
        /// <summary>
        /// 替換功能
        /// </summary>        
        public IReplacement? GetReplacement;
        /// <summary>
        /// 聯盟
        /// </summary>
        public League() { SourceId = ""; }
        /// <summary>
        /// 聯盟
        /// </summary>
        /// <param name="sourceId">單場來源ID</param>
        /// <param name="replace">替換功能</param>
        public League(string sourceId, IReplacement replace)
        {
            SourceId = sourceId;
            GetReplacement = replace;
        }
        /// <summary>
        /// 名稱
        /// </summary>
        public override string Name
        {
            get => base.Name;
            set
            {
                base.Name = SourceName = value;

                if (GetReplacement != null)
                {
                    if (GetReplacement.ReplaceWithId(SourceId, out string name))
                    {
                        base.Name = name;
                    }
                    else if (GetReplacement.ReplaceWithName(value, out name))
                    {
                        base.Name = name;
                    }
                }
            }
        }

        /// <summary>
        /// 聯盟模式 (表:[t_ally] 欄位:[f_modeId])
        /// </summary>
        public int LeagueMode { get; set; }
        /// <summary>
        /// 聯盟限額類型 (表:[t_ally] 欄位:[f_limitType])
        /// </summary>
        public byte LimitType { get; set; }
        /// <summary>
        /// 聯盟優先來源提醒 (表:[t_ally] 欄位:[f_prioritySourceId])
        /// </summary>
        public byte PrioritySourceId { get; set; }
        /// <summary>
        /// JZ聯盟名稱(繁,簡,英,越,泰)
        /// </summary>
        public string[] FullNameJZ { get; set; } = new string[] { "", "", "", "", "", "", "" };

    }

    /// <summary>
    /// 後綴
    /// </summary>
    [Serializable]
    public class Suffix : FullNameBase
    {
        /// <summary>
        /// 聯盟模式組別 (表:[t_suffix] 欄位:[f_group])
        /// </summary>
        public int Group { get; set; }
    }

    /// <summary>
    /// 隊伍
    /// </summary>
    [Serializable]
    public class Team : FullNameBase
    {
        /// <summary>
        /// 來源單場賽事ID
        /// </summary>
        public string SourceId { get; set; }
        /// <summary>
        /// 替換功能
        /// </summary>        
        public ITeamReplacement? GetReplacement;
        /// <summary>
        /// 國家Id
        /// </summary>
        public int CountryId { get; set; }
        /// <summary>
        /// 承接聯盟物件位置
        /// </summary>
        private League League { get; set; }
        /// <summary>
        /// 隊伍名稱
        /// </summary>
        public override string Name
        {
            get
            {
                return base.Name;
            }
            set
            {
                SourceName = value;
                base.Name = value;

                //判斷是否有對應的聯盟隊伍名稱需要替換隊名
                if (GetReplacement != null)
                {
                    if (GetReplacement.ReplaceWithId(SourceId, SourceName, out string name))
                    {
                        base.Name = name;
                    }
                    else if (GetReplacement.ReplaceWithName(League.Name, SourceName, $"{League.Name}_{value}", out name))
                    {
                        base.Name = name;
                    }
                }

                string str = base.Name;

                foreach (INameProcess process in new INameProcess[] { new WomanNameProcess(), new AgeNameProcess() })
                {
                    str = process.GetName(str, League.Name);
                }

                base.Name = str;
            }
        }
        /// <summary>
        /// JZ隊伍名稱(繁,簡,英,越,泰,印,31繁)
        /// </summary>
        public string[] FullNameJZ { get; set; } = new string[] { "", "", "", "", "", "", "" };
        /// <summary>
        /// 隊伍是否不開放 (0:開放、1:不開放)
        /// </summary>
        public bool IsNotOpen { get; set; }

        /// <summary>
        /// 初始建構子
        /// </summary>
        public Team()
        {

        }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="league">聯盟資訊</param>
        public Team(League league)
        {
            League = league;
        }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="league">聯盟資訊</param>
        public Team(League league, string sourceId, ITeamReplacement replace)
        {
            League = league;
            SourceId = sourceId;
            GetReplacement = replace;
        }
    }

    /// <summary>
    /// JZ直播資訊
    /// </summary>
    [Serializable]
    public class LiveUrlModel
    {
        //是否過長
        private bool isOverChar = false;
        private string _mbChannel;
        private string _pcChannel;
        /// <summary>
        /// TS數據庫ID
        /// </summary>
        public string gameId { get; set; }
        /// <summary>
        /// 球類
        /// </summary>
        public string ball { get; set; }
        /// <summary>
        /// 頻道
        /// </summary>
        public string lvc { get; set; }
        /// <summary>
        /// 顯示名稱
        /// </summary>
        public string lvt { get; set; }
        /// <summary>
        /// 手機雷達流
        /// </summary>
        public string mbChannel
        {
            get
            {
                return isOverChar ? string.Empty : _mbChannel;
            }
            set
            {
                _mbChannel = value;
            }
        }
        /// <summary>
        /// 電腦雷達流
        /// </summary>
        public string pcChannel
        {
            get
            {
                return isOverChar ? string.Empty : _pcChannel;
            }
            set
            {
                _pcChannel = value;
            }
        }
        /// <summary>
        /// 取得轉換Json後的字串
        /// </summary>
        /// <returns></returns>
        public string getJson()
        {
            string checkChar = YwCore.Common.regexJson.Replace(JsonConvert.SerializeObject(this), "$1");

            //判斷是否大於800字元
            if (checkChar.Length > 800)
            {
                isOverChar = true;
                checkChar = YwCore.Common.regexJson.Replace(JsonConvert.SerializeObject(this), "$1");
            }

            return checkChar;
        }
    }
}