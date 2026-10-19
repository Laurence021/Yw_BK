#region 功能修改描述與時間

/*
    描述：雷達用GpdmInfo
    日期：2023-07-18
*/

#endregion

using System;
using System.Linq;
using Yw_BK.model;

namespace Yw_BK.source.radar
{
    /// <summary>
    /// Gpdm資訊
    /// </summary>
    [Serializable]
    public class GpdmInfoRadar : GpdmInfo
    {
        public GpdmInfoRadar(decimal key, bool isGenerate = false, bool display = true) : base(key, isGenerate, display) { }

        public GpdmInfoRadar(int specials, byte setType, bool display = true) : base(specials, setType, display) { }

        /// <summary>
        /// 自動生成缺少的格子
        /// </summary>
        /// <param name="gpType"></param>
        public new void AutoCreate(GpType gpType)
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
                    KInfoRadar newKInfo = new KInfoRadar(gpType.ToString(), this.FirstOrDefault().Value.DataId, newKType, SetType);

                    var kInfo = GetOrAdd(newKType, newKInfo);

                    if (kInfo.BInfoDic.IsEmpty)
                    {
                        kInfo.AutoCreate();


                        //手動設置OE
                        if ((gpType == GpType.Zd ? Config.isOpenZdOe : Config.isOpenDsZpOe) && play == 4)
                        {
                            BInfo bInfoO = new BInfo(Config.GetBType(newKType, 1, 1));
                            BInfo bInfoE = new BInfo(Config.GetBType(newKType, 1, 2));
                            bInfoO.SetOdds(decimal.Floor((gpType == GpType.Zd ? Config.zdOERatio : Config.dsOERatio) * 100m) / 100m);
                            bInfoE.SetOdds(decimal.Floor((gpType == GpType.Zd ? Config.zdOERatio : Config.dsOERatio) * 100m) / 100m);
                            kInfo.BInfoDic.AddOrUpdate(bInfoO.BType, bInfoO, (k, v) => bInfoO);
                            kInfo.BInfoDic.AddOrUpdate(bInfoE.BType, bInfoE, (k, v) => bInfoE);
                            kInfo.SetIsOpen();
                            kInfo.IsManual = true;
                        }
                    }


                }
            }
        }

    }

}
