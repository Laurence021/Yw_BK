#region 功能修改描述與時間

/*
    描述：gpdm資訊
    日期：2021-10-28

    描述：添加Display開關
    日期：2022-02-16

    描述：添加IsDisplayChange設值邏輯
    日期：2022-03-30
*/

#endregion

using System;
using System.Collections.Concurrent;
using System.Linq;
using YwCore;

namespace Yw_BK.model
{
    /// <summary>
    /// Gpdm資訊
    /// </summary>
    [Serializable]
    public class GpdmInfo : ConcurrentDictionary<int, KInfo>
    {
        //紀錄資料異動(IsDisplayChange設置成true)時的時間戳
        private long changeTick;
        //複製changeTick當前的時間戳，用來判斷入庫前後資料一致判斷
        private long updatingTick;
        //NBB顯示開關(0:否 1:是)  
        private bool _Display;
        //是否異動過大格
        private bool _IsDisplayChange;
        /// <summary>
        /// 唯一值
        /// </summary>
        public decimal Key { get; private set; }
        /// <summary>
        /// GP玩法代號
        /// </summary>
        public int Specials { get; }
        /// <summary>
        /// SetType
        /// </summary>
        public byte SetType { get; }
        /// <summary>
        /// NBB顯示開關(0:否 1:是)  
        /// , 備註：目前僅JZ使用，其餘來源則預設都是開
        /// </summary>
        public bool Display
        {
            get => _Display;
            set
            {
                if (_Display != value)
                {
                    IsDisplayChange = true;
                }

                _Display = value;
            }
        }
        /// <summary>
        /// 是否異動過大格(經由Display設置時判斷並設定，並由處理完後的流程做清除)
        /// </summary>
        public bool IsDisplayChange
        {
            get => _IsDisplayChange;
            set
            {
                //設定可更新時紀錄當前時間戳
                if (value && IsMain)
                {
                    changeTick = DateTime.Now.Ticks;
                    _IsDisplayChange = value;
                }
                else
                {
                    //時間戳不一致代表仍要更新
                    _IsDisplayChange = changeTick != updatingTick;
                }
            }
        }
        /// <summary>
        /// 是否可以寫入關盤監控佇列(關盤監控用)
        /// </summary>
        public bool CanWrite { get; set; } = true;
        /// <summary>
        /// 是否為GP提前生成的玩法
        /// </summary>
        public bool IsGenerate { get; set; }
        /// <summary>
        /// 是否移到主庫
        /// </summary>
        public bool IsMain { get => this.Any(a => a.Value.BInfoDic.Values.Any(b => b.Status == 1)); }
        /// <summary>
        /// 操控組別
        /// </summary>
        public short? Group { get; set; }
        /// <summary>
        /// JZ目前跟的來源
        /// </summary>
        public Source TsSource { get; set; }
        /// <summary>
        /// 是否更新來源
        /// </summary>
        public bool IsUpdateTsSource { get; set; }
        /// <summary>
        /// TS來源最後寫入時間
        /// </summary>
        public DateTime LastInsertTime { get; set; }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="key">Gpdm</param>
        /// <param name="display">顯示開關</param>
        public GpdmInfo(decimal key, bool isGenerate = false, bool display = true) : base()
        {
            Specials = Convert.ToInt32(decimal.Truncate(key / 100));
            SetType = Convert.ToByte(key % 100);
            Key = key;
            IsGenerate = isGenerate;
            Display = display;
        }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="key">Gpdm</param>
        /// <param name="display">顯示開關</param>
        public GpdmInfo(int specials, byte setType, bool display = true) : base()
        {
            Specials = specials;
            SetType = setType;
            Key = GetGpdm(specials, setType);
            Display = display;
        }

        /// <summary>
        /// 標記更新入庫中
        /// </summary>
        public void SetUpdatingTick()
        {
            updatingTick = changeTick;
        }

        /// <summary>
        /// 自動生成缺少的格子
        /// </summary>
        /// <param name="gpType"></param>
        public void AutoCreate(GpType gpType)
        {
            for (int play = 1; play <= 4; play++)
            {
                if (play > 2 && SetType > 1)
                {
                    break;
                }

                if (Config.SetKindType(gpType, Specials, play, out int newKType))
                {
                    //生成物件
                    KInfo newKInfo = new KInfo(gpType.ToString(), this.FirstOrDefault().Value.DataId, newKType, SetType);

                    var kInfo = GetOrAdd(newKType, newKInfo);

                    if (kInfo.BInfoDic.IsEmpty)
                    {
                        kInfo.AutoCreate();
                    }
                }
            }
        }

        /// <summary>
        /// 計算GPDM
        /// </summary>
        /// <param name="specials"></param>
        /// <param name="setType"></param>
        /// <returns></returns>
        public static decimal GetGpdm(int specials, byte setType)
        {
            return specials * 100 + setType;
        }
    }
}
