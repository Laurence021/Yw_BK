#region 功能修改描述與時間

/*
    描述：存放PK PL資訊
    日期：2025-02-20  
*/

#endregion

namespace Yw_BK.model
{
    /// <summary>
    /// PKPL資訊
    /// </summary>
    public class PlateInfo
    {
        /// <summary>
        /// PK
        /// </summary>
        public decimal Plate { get; set; }
        /// <summary>
        /// 主隊PL 或 大PL
        /// </summary>
        public decimal Ratio_H { get; set; }
        /// <summary>
        /// 客隊PL 或 小PL
        /// </summary>
        public decimal Ratio_A { get; set; }

        /// <summary>
        /// 帶參數建構子
        /// </summary>
        /// <param name="plate">PK</param>
        /// <param name="ratio_H">主隊PL</param>
        /// <param name="ratio_A">客隊PL</param>
        public PlateInfo(decimal plate, decimal ratio_H, decimal ratio_A)
        {
            Plate = plate;
            Ratio_H = ratio_H;
            Ratio_A = ratio_A;
        }

        /// <summary>
        /// kInfo轉換(僅pk)
        /// </summary>
        /// <param name="kInfo"></param>
        public PlateInfo(KInfo kInfo)
        {
            Plate = kInfo.LineAt == 0 ? (decimal)(-1 * (double)kInfo.Line / 1000) : (decimal)((double)kInfo.Line / 1000);
        }
    }
}
