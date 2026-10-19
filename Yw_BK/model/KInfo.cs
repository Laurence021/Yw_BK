#region 功能修改描述與時間

/*
    描述：玩法資訊
    日期：2019-01-07
    
    描述：調整isUpdate設值方式
    日期：2021-08-17

    描述：調整比對計算跳動邏輯, 來源關閉保留原PK
    日期：2021-12-08

    描述：31站需求開發
    日期：2022-01-27

    描述：調整判斷Line異動更新條件
    日期：2022-02-16
*/

#endregion

using System.Collections.Concurrent;

namespace Yw_BK.model
{
    /// <summary>
    /// 玩法資訊
    /// </summary>
    [Serializable]
    public class KInfo
    {
        //檢查新比賽ZD玩法，來源異動超過5秒(含)才能進行更新
        private long checkChangeTick;
        //紀錄資料異動(IsUpdate設置成true)時的時間戳
        private long changeTick;
        //複製changeTick當前的時間戳，用來判斷入庫前後資料一致判斷
        private long updatingTick;
        //是否更新
        private bool _isUpdate;
        //RT[私有屬性]
        private int _lineAt = 1;
        //PK[私有屬性]
        private int _line;
        //模擬測試RT[私有屬性]
        private int _lineAtTest = 1;
        //模擬測試PK[私有屬性]
        private int _lineTest;
        //是否模擬測試[私有屬性]
        private bool _isTest;
        //是否來源開放[私有屬性]
        private bool _isOpen = false;
        //模擬測試來源開放[私有屬性]
        private bool _isOpenTest = false;        
        //鴛鴦盤錯誤開關
        private int _sameErrorSwitch;

        /// <summary>
        /// 是否模擬測試
        /// </summary>
        public bool IsTest
        {
            get => _isTest;
            set
            {
                foreach (BInfo bInfo in BInfoDic.Values)
                {
                    bInfo.IsTest = value;
                }

                _isTest = value;
            }
        }
        /// <summary>
        /// 對應GP類型
        /// </summary>
        private GpType GpType
        {
            get
            {
                GpType gpType = GpType.None;

                if (Enum.TryParse(StrType, out GpType tmpGptype) && Enum.IsDefined(typeof(GpType), tmpGptype))
                {
                    gpType = (tmpGptype.GetHashCode() & 2) == 2 ? GpType.Zd :
                             (tmpGptype.GetHashCode() & 1) == 1 ? GpType.Ds :
                             (tmpGptype.GetHashCode() & 4) == 4 ? GpType.Zp : gpType;

                }

                return gpType;
            }
        }
        /// <summary>
        /// GP類型(所屬頁面)
        /// </summary>
        public string StrType { get; set; }
        /// <summary>
        /// 數據更新索引
        /// </summary>
        public long DataId { get; set; }
        /// <summary>
        /// 關聯gId[HG內頁使用]
        /// </summary>
        public decimal MatchId { get; set; }
        /// <summary>
        /// 玩法
        /// </summary>
        public int KType { get; set; }
        /// <summary>
        /// 所屬集合(鴛鴦盤)
        /// </summary>
        public byte SetType { get; set; }
        /// <summary>
        /// RT[0:B讓A  1:A讓B]
        /// </summary>
        public int LineAt
        {
            get
            {
                return IsTest ? _lineAtTest : _lineAt;
            }
            set
            {
                _lineAt = value;
            }
        }
        /// <summary>
        /// PK
        /// </summary>
        public int Line
        {
            get
            {
                return IsTest ? _lineTest : _line;
            }
            set
            {
                _line = Math.Abs(value);
            }
        }
        /// <summary>
        /// PK上限
        /// </summary>
        public int LineLimit { get; set; }
        /// <summary>
        /// RT換邊
        /// </summary>
        public int LineAtChange { get; set; }
        /// <summary>
        /// PK跳動
        /// </summary>
        public int LineChange { get; set; }
        /// <summary>
        /// PK微調
        /// </summary>
        public int LineModify { get; set; }
        /// <summary>
        /// 是否更新
        /// </summary>
        public bool IsUpdate
        {
            get
            {
                //ZD玩法若在新比賽，異動超過5秒(含)，才能更新，避免頻繁異動導致更新頻繁
                if (_isUpdate && GpType == GpType.Zd && BInfoDic.Values.All(b => b.Status == 0))
                {
                    if (DateTime.Now.AddTicks(-checkChangeTick).Second >= 5)
                    {
                        checkChangeTick = DateTime.Now.Ticks;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return _isUpdate;
                }
            }
            set
            {
                //設定可更新時紀錄當前時間戳
                if (value)
                {
                    changeTick = DateTime.Now.Ticks;
                    if (_isUpdate == false) { checkChangeTick = DateTime.Now.Ticks; }
                    _isUpdate = value;
                }
                else
                {
                    //時間戳不一致代表仍要更新
                    _isUpdate = changeTick != updatingTick;
                }
            }
        }
        /// <summary>
        /// 是否來源開放
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return IsTest ? _isOpenTest : _isOpen;
            }
            set
            {
                _isOpen = value;
            }
        }
        /// <summary>
        /// 是否開放(最終結果 0:否, 1:是, 255:鎖單邊)
        /// </summary>
        public byte IsOpenResult { get; set; } = 0;
        /// <summary>
        /// PL加總(t_kMode.f_oddsTotal)
        /// </summary>
        public decimal OddsTotal { get; set; }
        /// <summary>
        /// 31站PL總數或折數(f_oddsSet31)
        /// </summary>
        public decimal OddsSet31 { get; set; }
        /// <summary>
        /// 玩法集合(t_kMode.f_kTypeGroup)
        /// </summary>
        public int KTypeGroup { get; set; }
        /// <summary>
        /// 玩法模式id(t_kMode.f_id)
        /// </summary>
        public int KModeId { get; set; }
        /// <summary>
        /// 是否開放0分盤
        /// </summary>
        public bool IsZeroLine { get; set; }
        /// <summary>
        /// 是否前次寫入開放提示
        /// </summary>
        public bool IsSourceOpenRecord { get; set; }
        /// <summary>
        /// 玩法類型 1:RF  2:OU  4:DY  8:OE  16:CS  32:TG  64:HF
        /// </summary>
        public int PlayTyp { get; set; }
        /// <summary>
        /// BType字典
        /// </summary>
        public ConcurrentDictionary<int, BInfo> BInfoDic = new ConcurrentDictionary<int, BInfo>();
        /// <summary>
        /// 是否GP true:跟 false:達防護邏輯脫勾
        /// </summary>
        public bool IsFollow { get; set; }
        /// <summary>
        /// 是否可無條件取代(比對緩存)
        /// </summary>
        public bool IsCanReplace { get; set; }
        /// <summary>
        /// 鴛鴦盤錯誤開關 1:PK, 2:PL, 4:讓頭不一致
        /// </summary>
        public int SameErrorSwitch
        {
            get
            {
                return _sameErrorSwitch;
            }
            set
            {
                //設定和原先的不同，需設置該玩法需要更新
                if (_sameErrorSwitch != value) { IsUpdate = true; }

                _sameErrorSwitch = value;
            }
        }
        /// <summary>
        /// 操作分組
        /// </summary>
        public short Group { get; set; }
        /// <summary>
        /// SB來源賽事編號 [SB判斷刪除玩法專用]
        /// </summary>
        public decimal SB_MatchId { get; set; }
        /// <summary>
        /// 是否手動設置
        /// </summary>
        public bool IsManual { get; set; }
        /// <summary>
        /// 是否連動關閉
        /// </summary>
        public bool IsAffectClosed { get; set; }
        /// <summary>
        /// 是否跟來源[都不跟:0, PK:1, PL:2]
        /// </summary>
        public int IsSourceFollow { get; set; } = 3;
        /// <summary>
        /// 後台只跟PK開關[PKPL:false, OnlyPK:true]
        /// </summary>
        public bool IsOddsFollow { get; set; }
        /// <summary>
        /// 玩法標題文字
        /// </summary>
        public string GroupText
        {
            get
            {
                //資源檔的key
                string resourcesKey = string.Format("k{0}", KType);

                //最後玩法
                if (SetType == 9)
                {
                    switch (KType)
                    {
                        case 12109:
                        case 12309:
                        case 12509:
                        case 12010:
                        case 12011:
                        case 12411:
                        case 12511:
                        case 12801:
                        case 12802:
                            resourcesKey += "-09";
                            break;
                    }
                }
                //取得資源檔對應語系內容
                string playName = Properties.Resources.ResourceManager.GetString(resourcesKey, Properties.Resources.Culture);

                //防止NULL
                if (!string.IsNullOrEmpty(playName))
                {
                    //判斷是否替換
                    return playName.Replace("(N)", SetType.ToString());
                }
                else
                {
                    return KType.ToString();
                }

            }
        }
        /// <summary>
        /// 玩法文字
        /// </summary>
        public string PlayText
        {
            get
            {
                switch (PlayTyp)
                {
                    case 1:
                        return Properties.Resources.Hdp;
                    case 2:
                        return Properties.Resources.Ou;
                    case 4:
                        return Properties.Resources.Ml;
                    case 8:
                        return Properties.Resources.Oe;
                    default:
                        return "";
                }
            }
        }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="strType">GP類型</param>
        /// <param name="dataId">數據更新索引</param>
        /// <param name="kType">玩法類型</param>
        /// <param name="setType">所屬集合</param>
        /// <param name="opt">玩法選項</param>
        /// <param name="matchId">來源gid</param>
        public KInfo(string strType, long dataId, int kType, byte setType = 1, decimal matchId = 0m)
        {
            StrType = strType;
            DataId = dataId;
            KType = kType;
            SetType = setType;
            MatchId = matchId;

            //設置玩法分類
            SetAllType();
        }

        /// <summary>
        /// 設定PK
        /// </summary>
        /// <param name="lineAt">RT</param>
        /// <param name="pk">PK</param>
        public void SetLine(decimal pk, int lineAt = 1)
        {
            LineAt = lineAt;
            Line = (int)(pk * 1000m);
        }

        /// <summary>
        /// 設定模擬測試PK
        /// </summary>
        /// <param name="lineAt">RT</param>
        /// <param name="pk">PK</param>
        public void SetLineTest(decimal pk, int lineAt = 1)
        {
            //平手固定RT在上方
            _lineAtTest = pk == 0m ? 1 : lineAt;
            _lineTest = (int)(pk * 1000m);
        }

        /// <summary>
        /// 取得轉換後PK
        /// </summary>
        /// <returns></returns>
        public decimal GetPkValue()
        {
            return Line / 1000m;
        }

        /// <summary>
        /// 獲取最終後台顯示的PK
        /// </summary>
        /// <returns></returns>
        public decimal GetLastLine()
        {
            return (Line + LineModify) / 1000m;
        }

        //設置玩法分類
        private void SetAllType()
        {
            int kType = KType;

            #region 設定玩法類型

            int playTyp = 0;
            switch (kType)
            {
                case 12001:
                case 12101:
                case 12201:
                case 12301:
                case 12401:
                case 12501:
                case 12601:
                case 12701:
                case 12801:
                case 12010:
                case 12011:
                case 12012:
                case 12109:
                case 12112:
                case 12209:
                case 12212:
                case 12309:
                case 12312:
                case 12409:
                case 12410:
                case 12411:
                case 12412:
                case 12509:
                case 12510:
                case 12511:
                case 12512:
                case 12609:
                case 12610:
                case 12611:
                case 12612:
                case 12704:
                case 12707:
                case 12710:
                case 12713:
                case 12716:
                case 12719:
                case 12802:
                case 12803:
                case 12804:
                case 12021:
                case 12022:
                case 12023:
                case 12758:
                case 12761:
                case 12764:
                case 12767:
                case 12776:
                case 12779:
                    playTyp = 1;
                    LineLimit = 400;
                    break;
                case 12002:
                case 12005:
                case 12007:
                case 12102:
                case 12105:
                case 12107:
                case 12202:
                case 12205:
                case 12207:
                case 12302:
                case 12305:
                case 12307:
                case 12402:
                case 12405:
                case 12407:
                case 12502:
                case 12505:
                case 12507:
                case 12602:
                case 12605:
                case 12607:
                case 12702:
                case 12705:
                case 12708:
                case 12711:
                case 12714:
                case 12717:
                case 12720:
                case 12759:
                case 12762:
                case 12765:
                case 12768:
                case 12777:
                case 12780:
                    playTyp = 2;
                    LineLimit = 600;
                    break;
                case 12003:
                case 12103:
                case 12303:
                case 12403:
                case 12503:
                case 12603:
                    playTyp = 4;
                    break;
                case 12004:
                case 12006:
                case 12008:
                case 12104:
                case 12106:
                case 12108:
                case 12204:
                case 12206:
                case 12208:
                case 12304:
                case 12306:
                case 12308:
                case 12404:
                case 12406:
                case 12408:
                case 12504:
                case 12506:
                case 12508:
                case 12604:
                case 12606:
                case 12608:
                case 12703:
                case 12706:
                case 12709:
                case 12712:
                case 12715:
                case 12718:
                case 12721:
                case 12760:
                case 12763:
                case 12766:
                case 12769:
                case 12778:
                case 12781:
                    playTyp = 8;
                    break;
            }

            PlayTyp = playTyp;

            #endregion
        }

        /// <summary>
        /// 自動生成BInfo
        /// </summary>
        public void AutoCreate()
        {
            //設為可被其他群組取代
            IsCanReplace = true;
            //玩法類型
            int kType = KType;
            byte setType = SetType;

            BInfo bInfoO = new BInfo(Config.GetBType(kType, setType, 1));
            BInfo bInfoE = new BInfo(Config.GetBType(kType, setType, 2));
            BInfoDic.TryAdd(bInfoO.BType, bInfoO);
            BInfoDic.TryAdd(bInfoE.BType, bInfoE);
        }

        /// <summary>
        /// 設定是否開放
        /// </summary>
        /// <param name="isOpen">是否開放</param>
        public void SetIsOpen(bool isOpen = true)
        {
            IsOpen = isOpen;
        }

        /// <summary>
        /// 設定是否開放[模擬測試]
        /// </summary>
        /// <param name="isOpen">是否開放</param>
        public void SetIsOpenTest(bool isOpen = true)
        {
            _isOpenTest = isOpen;
        }

        /// <summary>
        /// 清除PL
        /// </summary>
        public void ClearOdds()
        {
            foreach (BInfo bInfo in BInfoDic.Values)
            {
                bInfo.Odds = 0;
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
        /// 新舊緩存比對
        /// </summary>
        /// <param name="newObj">新物件</param>
        public virtual void Compare(KInfo newObj)
        {
            //是否更新
            bool isUpdate = false;

            //有數據才比對
            if (newObj.BInfoDic.Count > 0)
            {
                //無數據則直接複製
                if (BInfoDic.Count == 0)
                {
                    BInfoDic = new ConcurrentDictionary<int, BInfo>(newObj.BInfoDic);
                }
                else
                {
                    foreach (KeyValuePair<int, BInfo> bPair in newObj.BInfoDic)
                    {
                        //bType存在，取出單項玩法資訊
                        if (BInfoDic.TryGetValue(bPair.Key, out BInfo getBInfo))
                        {
                            //單項玩法比對
                            isUpdate |= getBInfo.Compare(bPair.Value, IsOpen && newObj.IsOpen, (IsSourceFollow & newObj.IsSourceFollow) >= 2);
                        }
                        //bType不存在則直接添加
                        else
                        {
                            BInfoDic.TryAdd(bPair.Key, bPair.Value);
                        }
                    }
                }
            }

            //HG內頁玩法取代特別處理
            if (StrType != newObj.StrType && newObj.StrType.Contains("More"))
            {
                MatchId = newObj.MatchId;
            }

            //非模擬測試時比較異動
            if (!IsTest)
            {
                //是否更新
                if (isUpdate
                    || IsOpen != newObj.IsOpen
                    || (newObj.IsOpen && (LineAt != newObj.LineAt || Line != newObj.Line))
                    || IsManual != newObj.IsManual)
                {
                    IsUpdate = true;
                }

                //RT跳動(來源玩法開放時需判斷跳動)
                LineAtChange = newObj.IsOpen ? newObj.LineAt - LineAt : 0;

                //未啟動清零避免一啟動就達條件
                if (!Config.isFollow)
                {
                    LineChange = 0;
                }
                //PK跳動(來源玩法開放時需判斷跳動)
                else if (newObj.IsOpen && (newObj.Line != Line || LineAtChange != 0))
                {
                    //跳動計算基數(A讓為正數)
                    int tmpBase = newObj.LineAt == 0 ? -1 : 1;

                    //需要判斷是否開放0分盤
                    if (IsZeroLine)
                    {
                        LineChange += LineAtChange != 0 ? (newObj.Line + Line) * tmpBase : (newObj.Line - Line) * tmpBase;
                    }
                    else
                    {
                        int tmpNewLine = newObj.Line < 500 ? 500 : newObj.Line;
                        int tmpOldLine = Line < 500 ? 500 : Line;
                        LineChange += LineAtChange != 0 ? (tmpNewLine + tmpOldLine - 1000) * tmpBase : (tmpNewLine - tmpOldLine) * tmpBase;
                    }
                }
            }

            //GP類型
            StrType = newObj.StrType;
            //更新數據索引
            DataId = newObj.DataId;
            //是否開放
            IsOpen = newObj.IsOpen;
            //RT
            LineAt = newObj.IsOpen ? newObj.LineAt : LineAt;
            //PK
            Line = newObj.IsOpen ? newObj.Line : Line;
            //是否手動設置
            IsManual = newObj.IsManual;
            //是否允許無條件取代
            IsCanReplace = newObj.IsCanReplace;
            //是否跟來源數據PKPL
            IsSourceFollow = newObj.IsSourceFollow;
            //SB來源賽事編號
            SB_MatchId = newObj.SB_MatchId;
        }

        /// <summary>
        /// 模擬測試比對變動值
        /// </summary>
        /// <param name="oldObj">舊數據</param>
        public void CompareTest(KInfo oldObj)
        {
            //是否更新
            bool isUpdate = false;

            foreach (KeyValuePair<int, BInfo> bPair in oldObj.BInfoDic)
            {
                //bType存在，取出單項玩法資訊
                if (BInfoDic.TryGetValue(bPair.Key, out BInfo getBInfo))
                {
                    //單項玩法比對
                    isUpdate |= getBInfo.CompareTest(bPair.Value, IsOpen && oldObj.IsOpen);
                }
            }

            //是否更新
            if (isUpdate
                || IsOpen != oldObj.IsOpen
                || (IsOpen && (LineAt != oldObj.LineAt || Line != oldObj.Line)))
            {
                IsUpdate = true;
            }

            //RT跳動(來源玩法開放時需判斷跳動)
            LineAtChange = IsOpen ? LineAt - oldObj.LineAt : 0;

            //PK跳動(來源玩法開放時需判斷跳動)
            if (IsOpen && (oldObj.Line != Line || LineAtChange != 0))
            {
                //跳動計算基數(A讓為正數)
                int tmpBase = LineAt == 0 ? -1 : 1;

                //需要判斷是否開放0分盤
                if (IsZeroLine)
                {
                    LineChange += LineAtChange != 0 ? (Line + oldObj.Line) * tmpBase : (Line - oldObj.Line) * tmpBase;
                }
                else
                {
                    int tmpNewLine = Line < 500 ? 500 : Line;
                    int tmpOldLine = oldObj.Line < 500 ? 500 : oldObj.Line;
                    LineChange += LineAtChange != 0 ? (tmpNewLine + tmpOldLine - 1000) * tmpBase : (tmpNewLine - tmpOldLine) * tmpBase;
                }
            }
            else
            {
                LineChange = 0;
            }
        }
    }
}