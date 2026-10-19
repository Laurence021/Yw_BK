#region 功能修改描述與時間

/*
    描述：單項資訊
    建立時間：2019-01-07

    描述：添加RT使用的屬性
    修改時間：2019-02-14

    描述：Line、LineAt、IsUpdate移至KInfo及添加OddsChange
    修改時間：2019-03-04

    描述：添加PL微調
    修改時間：2019-03-19

    描述：添加模擬測試
    修改時間：2020-2-14
*/

#endregion

using System;

namespace Yw_BK.model
{
    /// <summary>
    /// 單項玩法資訊
    /// </summary>
    [Serializable]
    public class BInfo
    {
        //來源PL[私有屬性]
        private decimal _odds;
        //模擬PL[私有屬性]
        private decimal _oddsTest;
        /// <summary>
        /// 是否模擬測試
        /// </summary>
        public bool IsTest { get; set; }
        /// <summary>
        /// 數據庫Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 單項玩法
        /// </summary>
        public int BType { get; set; }
        /// <summary>
        /// 來源PL
        /// </summary>
        public decimal Odds
        {
            get
            {
                return IsTest ? _oddsTest : _odds;
            }
            set
            {
                _odds = value;
            }
        }
        /// <summary>
        /// PL跳動
        /// </summary>
        public decimal OddsChange { get; set; }
        /// <summary>
        /// 經過防護邏輯及加總邏輯計算後PL
        /// </summary>
        public decimal OddsCalc { get; set; }
        /// <summary>
        /// PL微調(差距值)
        /// </summary>
        public decimal OddsModify { get; set; }
        /// <summary>
        /// 玩法狀態[f_isCreateGame] 0:新比賽, 1:主庫, 255:待結算
        /// </summary>
        public byte Status { get; set; }

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="bType"></param>
        public BInfo(int bType)
        {
            BType = bType;
        }
        
        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="bType">單項玩法type</param>
        /// <param name="odds">PL</param>
        public BInfo(int bType, decimal odds)
        {
            BType = bType;
            Odds = odds;
        }

        /// <summary>
        /// 設定PL
        /// </summary>
        /// <param name="odds">PL</param>
        public void SetOdds(decimal odds)
        {
            Odds = odds;
        }

        /// <summary>
        /// 設定模擬測試PL
        /// </summary>
        /// <param name="odds">PL</param>
        public void SetOddsTest(decimal odds)
        {
            _oddsTest = odds;
        }

        /// <summary>
        /// 獲取最終後台顯示的PL
        /// </summary>
        public decimal GetLastOdds()
        {
            return OddsCalc + OddsModify;
        }

        /// <summary>
        /// 新舊緩存比對
        /// </summary>
        /// <param name="newObj">新物件</param>
        /// <param name="isOpen">新舊數據是否皆為開放</param>
        /// <param name="isOddsChange">是否判斷odds跳動</param>
        /// <returns>是否更新</returns>
        public bool Compare(BInfo newObj, bool isOpen, bool isOddsChange = true)
        {
            //判斷數據是否更新
            bool isUpdate = false;

            //非模擬測試時比較異動
            if (!IsTest)
            {
                isUpdate = newObj.Odds != Odds;
                //新舊數據皆有跟來源PL才判斷跳動
                OddsChange += isOpen && newObj.Odds != Odds && isOddsChange ? newObj.Odds - Odds : 0;
            }

            Odds = newObj.Odds;

            return isUpdate;
        }

        /// <summary>
        /// 模擬測試比對變動值
        /// </summary>
        /// <param name="oldObj">舊物件</param>
        /// <param name="isOpen">新舊數據是否皆為開放</param>   
        /// <returns>是否更新</returns>
        public bool CompareTest(BInfo oldObj, bool isOpen)
        {
            //判斷數據是否更新
            bool isUpdate = oldObj.Odds != Odds;

            OddsChange = isOpen && oldObj.Odds != Odds ? Odds - oldObj.Odds : 0;

            return isUpdate;
        }
    }
}