#region 功能修改描述與時間

/*
    描述：GP類型列舉
    建立時間：2019-01-07
*/

#endregion

using System.ComponentModel;

namespace Yw_BK.model
{
    /// <summary>
    /// GP類型
    /// </summary>
    public enum GpType
    {
        None = 0,
        [Description("Ds")]
        Ds = 1,
        [Description("Zd")]
        Zd = 2,
        [Description("Zp")]
        Zp = 4,
        Dj = 8,
        DjQ2 = 8 + 1,       //第二節
        Xb = 16,
        Ac = 32,
        AcHT = 128,         //Ac上半
        AcQ1 = 512,         //Ac第一節
        AcQ2 = 1024,        //Ac第二節
        More = 256,         //More更多玩法
        DsMore = 1 + 256,   //Ds更多玩法
        ZdMore = 2 + 256,   //Zd更多玩法
        ZpMore = 4 + 256,   //Zp更多玩法
        MMore = 64,        //MMOR更多玩法
    }
}
