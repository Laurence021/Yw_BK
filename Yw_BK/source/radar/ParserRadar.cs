#region 功能修改描述與時間

/*
    描述：解析雷達數據
    日期：2024-05-10
*/

#endregion

using GP_RadarService.Models;
using Yw_BK.model;
using YwCore;

namespace Yw_BK.source.radar
{
    /// <summary>
    /// 解析雷達數據
    /// </summary>
    public class ParserRadar
    {
        /// <summary>
        /// 解析
        /// </summary>
        /// <param name="game"></param>
        public void Parse(RadarGame game)
        {
            try
            {
                int maxNum = 1;
                GameInfoRadar tmpGameinfo = new GameInfoRadar(game.Id.ToString());
                bool isNBA = false;
                int isSourceFollow = 0;
                string ally = game.Ally.Name;
                string teamA = string.Empty;
                string teamB = string.Empty;

                if (game.Ally.Name.StartsWith("NBA") && !Config.CheckNbaExcludeName(game.Ally.Name))
                {
                    isNBA = true;
                    tmpGameinfo.Home = 2;
                    teamB = game.HomeTeam.Name;
                    teamA = game.AwayTeam.Name;
                }
                else
                {
                    tmpGameinfo.Home = 1;
                    teamA = game.HomeTeam.Name;
                    teamB = game.AwayTeam.Name;
                }

                tmpGameinfo.League.Name = string.IsNullOrEmpty(game.Ally.Category) ? ally : $"{game.Ally.Category} {ally}";
                tmpGameinfo.SourceId = game.Id.ToString();
                tmpGameinfo.TeamA.Name = teamA;
                tmpGameinfo.TeamB.Name = teamB;

                //排除聯盟
                if (Config.FilterLeagueHashSet.Any(filter => tmpGameinfo.League.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    foreach (var delGame in Config.gameDic.Values.Where(a => a.League.Name.Equals(tmpGameinfo.League.Name)))
                    {
                        delGame.DataId = -1;
                    }

                    return;
                }

                //設定預設後綴
                if (FrmSuffix.suffixList.TryGetValue(tmpGameinfo.League.Name, out int suffixId) && suffixId != 0)
                {
                    tmpGameinfo.Suffix.Name = WebName.suffixDic[suffixId].En;
                    tmpGameinfo.Suffix.FullName = WebName.suffixDic[suffixId].GetFullName();
                    tmpGameinfo.Suffix.Id = suffixId;
                }

                //Zd或Ds
                if (game.ProducerId == 1)
                {
                    maxNum = 1;
                    isSourceFollow = Config.isZdOnlyPK ? 1 : 3;
                    tmpGameinfo.GpType = GpType.Zd;
                    tmpGameinfo.GType = (byte)GType.BkZd;
                    tmpGameinfo.SetGameDate(game.GameDate);
                }
                else
                {
                    maxNum = Config.sameCount == 0 ? 10 : Config.sameCount == 1 ? 1 : Config.sameCount == 2 ? 3 : 5;
                    isSourceFollow = Config.isDsOnlyPK ? 1 : 3;
                    tmpGameinfo.GpType = GpType.Ds;
                    tmpGameinfo.GType = (byte)GType.BkDs;
                    tmpGameinfo.SetGameDate(game.GameDate);

                    if (tmpGameinfo.GameDate <= DateTime.Now)
                    {
                        tmpGameinfo.DataId = -1;
                    }
                    else if (Config.isTomorrow)
                    {
                        int hour = DateTime.Now.Hour;
                        DateTime zpTDate = hour >= 12 ? DateTime.Now.Date.AddDays(2) : DateTime.Now.Date.AddDays(1);

                        if (tmpGameinfo.PaymentDate > zpTDate)
                        {
                            tmpGameinfo.DataId = -1;
                        }
                    }
                }

                //賽事狀態設定
                if (!SetGameStatus(tmpGameinfo, game))
                {
                    tmpGameinfo.DataId = -1;
                }

                //單節多長
                if (game.ExtraInfo.TryGetValue("period_length", out string strPeriod) && byte.TryParse(strPeriod, out byte periodLength))
                {
                    tmpGameinfo.QuarterTimeFromSource = periodLength;
                }

                //賽事共幾節
                if (game.ExtraInfo.TryGetValue("regular_number_of_periods", out string val) && int.TryParse(val, out int periodNum))
                {
                    tmpGameinfo.RegularNnumberOfPeriods = periodNum;
                }

                //玩法
                if (game.Markets != null)
                {
                    if (tmpGameinfo.GpType == GpType.Zd)
                    {
                        HashSet<GpdmInfoRadar> gpdmList = new HashSet<GpdmInfoRadar>();

                        //全場DY合併至RF
                        foreach (var idGroup in game.Markets.Values.GroupBy(a => a.Id == 219 ? 223 : a.Id))
                        {
                            Dictionary<int, byte> same = new Dictionary<int, byte>();
                            var order1 = idGroup.OrderByDescending(a => a.Odds != null).ThenByDescending(a => a.Status == 1);
                            var order2 = order1.ThenBy(a => { if (a.Specifiers != null && a.Specifiers.TryGetValue("quarternr", out string value)) { return value; } else { return ""; } });
                            var order = order2.ThenBy(a => a.Odds != null ? a.Odds.Select(odds => odds.Odds.GetValueOrDefault(0)).Aggregate((current, next) => Math.Abs(current - next)) : double.MaxValue);

                            //全場0.5盤，用DY取代
                            bool dyToRf = idGroup.Key == 223 ? order.Any(a => a.Id == 223 && a.Status == 1 && a.Specifiers.TryGetValue("hcp", out string value) && Math.Abs(decimal.Parse(value)) == 0.5m) : false;

                            foreach (var market in order)
                            {
                                if (GetSpecialandType(tmpGameinfo.GpType, market, out int specials, out int type, out decimal line, out byte set, out Scene scene, isNBA))
                                {
                                    if (specials == 0)
                                    {
                                        if (type == 1 && Math.Abs(line) == 0.5m && market.Status == 1)
                                        {
                                            continue;
                                        }

                                        //ZD全場DY替換
                                        if (type == 3 && dyToRf)
                                        {
                                            type = 1;
                                        }
                                    }

                                    if (Math.Abs(line % 1) == 0 && line != 0)
                                    {
                                        continue;
                                    }

                                    if (same.TryGetValue(specials, out byte num) && type < 3)
                                    {
                                        if (num >= maxNum)
                                        {
                                            continue;
                                        }

                                        num++;
                                    }
                                    else
                                    {
                                        num = 1;
                                    }

                                    same[specials] = num;

                                    //玩法
                                    if (Config.SetKindType(tmpGameinfo.GpType, specials, type, out int kType))
                                    {
                                        SetGpdm(tmpGameinfo, isSourceFollow, gpdmList, market, specials, line, set, num, kType, out KInfoRadar kInfo, out GpdmInfoRadar gpdm);

                                        if (market.Certainty == 1)
                                        {
                                            continue;
                                        }

                                        gpdm = tmpGameinfo.ObjDic.GetOrAdd(gpdm.Key, gpdm) as GpdmInfoRadar;
                                        gpdm.TryAdd(kType, kInfo);
                                        gpdmList.Add(gpdm);

                                        //手動設置OE
                                        if (Config.isOpenZdOe && type == 4)
                                        {
                                            BInfo bInfoO = new BInfo(Config.GetBType(kType, 1, 1));
                                            BInfo bInfoE = new BInfo(Config.GetBType(kType, 1, 2));
                                            bInfoO.SetOdds(decimal.Floor((Config.zdOERatio) * 100m) / 100m);
                                            bInfoE.SetOdds(decimal.Floor((Config.zdOERatio) * 100m) / 100m);
                                            kInfo.BInfoDic.AddOrUpdate(bInfoO.BType, bInfoO, (k, v) => bInfoO);
                                            kInfo.BInfoDic.AddOrUpdate(bInfoE.BType, bInfoE, (k, v) => bInfoE);
                                            kInfo.SetIsOpen();
                                            kInfo.IsManual = true;
                                        }
                                    }
                                }
                            }
                        }

                        foreach (GpdmInfoRadar gpdm in gpdmList)
                        {
                            gpdm.AutoCreate(tmpGameinfo.GpType);
                        }
                    }
                    else
                    {
                        HashSet<GpdmInfoRadar> gpdmList = new HashSet<GpdmInfoRadar>();

                        //玩法分組
                        var marketGroup = game.Markets.Values.GroupBy(a =>
                        {
                            if (a.Specifiers?.TryGetValue("quarternr", out string value) == true)
                            {
                                return (a.Id, quarternr: value);
                            }

                            return (a.Id, quarternr: "");
                        });

                        foreach (var idGroup in marketGroup)
                        {
                            //最平水排序
                            var order1 = idGroup.OrderByDescending(a => a.Odds != null).ThenByDescending(a => a.Status == 1);
                            var order = order1.ThenBy(a => a.Odds != null ? a.Odds.Select(odds => odds.Odds.GetValueOrDefault(0)).Aggregate((current, next) => Math.Abs(current - next)) : double.MaxValue);
                            //排除無用玩法
                            var tmp = order.Select(a =>
                            {
                                GetSpecialandType(tmpGameinfo.GpType, a, out int specials, out int type, out decimal line, out byte set, out Scene scene, isNBA);
                                return (data: a, specials, type, line, set, scene);
                            }).Where(a => a.specials >= 0);

                            byte num = 1;
                            List<GpdmInfoRadar> tmpGpdm = new List<GpdmInfoRadar>();
                            int kType = 0;

                            //細節玩法分群
                            foreach (var gpdmGroup in tmp.GroupBy(a => (a.type, a.specials, a.scene, a.set)))
                            {
                                //鴛鴦盤處理
                                foreach (var play in gpdmGroup)
                                {
                                    //先依照最平水排序收納
                                    if (Config.SetKindType(tmpGameinfo.GpType, play.specials, play.type, out kType))
                                    {
                                        SetGpdm(tmpGameinfo, isSourceFollow, tmpGpdm, play.data, play.specials, play.line, play.set, num, kType, out KInfoRadar kInfo, out GpdmInfoRadar gpdm);

                                        if (play.data.Certainty == 1)
                                        {
                                            continue;
                                        }

                                        //半場、單節鴛鴦盤開關
                                        if (play.set == 0 && !Config.isSbDjSame &&
                                            (play.scene == Scene.Half1 ||
                                            play.scene == Scene.Half2 ||
                                            play.scene == Scene.Q1 ||
                                            play.scene == Scene.Q2 ||
                                            play.scene == Scene.Q3 ||
                                            play.scene == Scene.Q4) &&
                                            num > 1)
                                        {
                                            continue;
                                        }

                                        num++;

                                        gpdm.TryAdd(kType, kInfo);
                                        tmpGpdm.Add(gpdm);

                                        //手動設置OE
                                        if (Config.isOpenDsZpOe && play.type == 4)
                                        {
                                            BInfo bInfoO = new BInfo(Config.GetBType(kType, 1, 1));
                                            BInfo bInfoE = new BInfo(Config.GetBType(kType, 1, 2));
                                            bInfoO.SetOdds(decimal.Floor((Config.dsOERatio) * 100m) / 100m);
                                            bInfoE.SetOdds(decimal.Floor((Config.dsOERatio) * 100m) / 100m);
                                            kInfo.BInfoDic.AddOrUpdate(bInfoO.BType, bInfoO, (k, v) => bInfoO);
                                            kInfo.BInfoDic.AddOrUpdate(bInfoE.BType, bInfoE, (k, v) => bInfoE);
                                            kInfo.SetIsOpen();
                                            kInfo.IsManual = true;
                                        }
                                    }
                                }

                                //實際排序整合
                                if (tmpGpdm.Count > 0)
                                {
                                    List<KInfo> tmpKInfos = new List<KInfo>();
                                    var firstGpdm = tmpGpdm.FirstOrDefault();

                                    //主要玩法才有複合盤
                                    if (gpdmGroup.Key.set == 0)
                                    {
                                        //RF
                                        if (gpdmGroup.Key.type == 1)
                                        {
                                            //鴛鴦盤全抓
                                            if (maxNum > 5)
                                            {
                                                foreach (var gpdm in tmpGpdm)
                                                {
                                                    tmpKInfos.Add(gpdm[kType]);
                                                }
                                            }
                                            else
                                            {
                                                bool ignore0_5 = gpdmGroup.Key.specials == 0;
                                                KInfo kInfoA = firstGpdm[kType];
                                                //主客讓公式變數
                                                decimal flag = (tmpGameinfo.Home == 2 && kInfoA.LineAt == 0) || (tmpGameinfo.Home == 1 && kInfoA.LineAt == 1) ? 1 : -1;
                                                //換邊基準
                                                decimal abs = ignore0_5 ? 500m : 0;

                                                var kInfos = tmpGpdm.Select(a => a[kType]);

                                                //NBA DS 全場上半額外處理
                                                if (isNBA && (gpdmGroup.Key.specials == 0 || gpdmGroup.Key.specials == 1))
                                                {
                                                    tmpKInfos = NbaDsFtHtHandle(kInfoA, kInfos, maxNum, 1, gpdmGroup.Key.specials);
                                                }
                                                else if (maxNum == 1)
                                                {
                                                    tmpKInfos.Add(kInfoA);
                                                }
                                                else if (maxNum == 3)
                                                {
                                                    /*主讓
                                                         B盤：固定抓取來源比A盤壞100%盤口
                                                         C盤：固定抓取來源比A盤好100%盤口，並跳過獨贏盤

                                                         客讓
                                                         B盤：固定抓取來源比A盤好100%盤口，並跳過獨贏盤
                                                         C盤：固定抓取來源比A盤壞100%盤口

                                                         全場不抓取0輸盤
                                                    */
                                                    tmpKInfos.Add(kInfoA);
                                                    decimal addA = kInfoA.Line + 500m * flag;
                                                    KInfo kInfoB;
                                                    KInfo kInfoC;

                                                    if (addA <= abs)
                                                    {
                                                        kInfoB = kInfos.Where(a => a.LineAt != kInfoA.LineAt).OrderBy(a => a.Line).FirstOrDefault();
                                                    }
                                                    else
                                                    {
                                                        kInfoB = kInfos.FirstOrDefault(a => addA == a.Line && kInfoA.LineAt == a.LineAt);
                                                    }

                                                    addA = kInfoA.Line - 500m * flag;

                                                    if (addA <= abs)
                                                    {
                                                        kInfoC = kInfos.Where(a => a.LineAt != kInfoA.LineAt).OrderBy(a => a.Line).FirstOrDefault();
                                                    }
                                                    else
                                                    {
                                                        kInfoC = kInfos.FirstOrDefault(a => addA == a.Line && kInfoA.LineAt == a.LineAt);
                                                    }

                                                    if (kInfoB != null)
                                                    {
                                                        tmpKInfos.Add(kInfoB);
                                                    }

                                                    if (kInfoC != null)
                                                    {
                                                        tmpKInfos.Add(kInfoC);
                                                    }
                                                }
                                                else if (maxNum == 5)
                                                {
                                                    tmpKInfos.Add(kInfoA);

                                                    //其他籃球排序
                                                    if (!isNBA)
                                                    {
                                                        /*主讓
                                                          B盤：固定抓取來源比A盤壞200%盤口
                                                          C盤：固定抓取來源比B盤好100%盤口
                                                          D盤：固定抓取來源比A盤好100%盤口，並跳過獨贏盤
                                                          E盤：固定抓取來源比D盤好100%盤口，並跳過獨贏盤，有換讓頭需抓取D盤壞於最少100%的平輸盤

                                                          客讓
                                                          B盤：固定抓取來源比A盤好200%盤口，並跳過獨贏盤
                                                          C盤：固定抓取來源比B盤壞100%盤口，並跳過獨贏盤，有換讓頭需抓取B盤好於最少100%的平輸盤
                                                          D盤：固定抓取來源比A盤壞100%盤口
                                                          E盤：固定抓取來源比D盤壞100%盤口

                                                          全場不抓取0輸盤
                      
                                                          沒開0輸的全場
                                                          忽略讓邊　1平>0　的跳動％數，補到另一邊
                                                          有開0輸的玩法
                                                          忽略讓邊　0.5>0　的跳動％數，補到另一邊
                                                        */
                                                        decimal addA = kInfoA.Line + 1000m * flag;
                                                        KInfo kInfoB;

                                                        if (addA <= abs)
                                                        {
                                                            if (ignore0_5)
                                                            {
                                                                if (kInfoA.Line == 1000m)
                                                                {
                                                                    kInfoB = kInfos.FirstOrDefault(a => a.Line == kInfoA.Line + 500m && kInfoA.LineAt != a.LineAt);
                                                                }
                                                                else
                                                                {
                                                                    kInfoB = kInfos.FirstOrDefault(a => a.Line == kInfoA.Line - 500m && kInfoA.LineAt != a.LineAt);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (kInfoA.Line == 1000m)
                                                                {
                                                                    kInfoB = kInfos.FirstOrDefault(a => a.Line == kInfoA.Line - 500m && kInfoA.LineAt != a.LineAt);
                                                                }
                                                                else
                                                                {
                                                                    kInfoB = kInfos.FirstOrDefault(a => a.Line == kInfoA.Line + 500m && kInfoA.LineAt != a.LineAt);
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            kInfoB = kInfos.FirstOrDefault(a => addA == a.Line && kInfoA.LineAt == a.LineAt);
                                                        }

                                                        if (kInfoB != null)
                                                        {
                                                            tmpKInfos.Add(kInfoB);

                                                            KInfo kInfoC;

                                                            if (kInfoB.LineAt != kInfoA.LineAt)
                                                            {
                                                                if (ignore0_5)
                                                                {
                                                                    if (kInfoB.Line == 1000m)
                                                                    {
                                                                        kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line && kInfoB.LineAt != a.LineAt);
                                                                    }
                                                                    else
                                                                    {
                                                                        kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line - 500m && kInfoB.LineAt == a.LineAt);
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    if (kInfoB.Line == 1000m)
                                                                    {
                                                                        kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line - 500m && kInfoB.LineAt == a.LineAt);
                                                                    }
                                                                    else
                                                                    {
                                                                        kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line && kInfoB.LineAt != a.LineAt);
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                decimal addB = kInfoB.Line - 500m * flag;

                                                                if (addB <= abs)
                                                                {
                                                                    if (ignore0_5)
                                                                    {
                                                                        if (kInfoB.Line == 1000m)
                                                                        {
                                                                            kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line && kInfoB.LineAt != a.LineAt);
                                                                        }
                                                                        else
                                                                        {
                                                                            kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line - 500m && kInfoB.LineAt == a.LineAt);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        if (kInfoB.Line == 1000m)
                                                                        {
                                                                            kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line - 500m && kInfoB.LineAt == a.LineAt);
                                                                        }
                                                                        else
                                                                        {
                                                                            kInfoC = kInfos.FirstOrDefault(a => a.Line == kInfoB.Line && kInfoB.LineAt != a.LineAt);
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    kInfoC = kInfos.FirstOrDefault(a => addB == a.Line && kInfoB.LineAt == a.LineAt);
                                                                }
                                                            }

                                                            if (kInfoC != null) tmpKInfos.Add(kInfoC);
                                                        }

                                                        addA = kInfoA.Line - 500m * flag;
                                                        KInfo kInfoD;

                                                        if (addA <= abs)
                                                        {
                                                            kInfoD = kInfos.FirstOrDefault(a => a.Line == kInfoA.Line && kInfoA.LineAt != a.LineAt);
                                                        }
                                                        else
                                                        {
                                                            kInfoD = kInfos.FirstOrDefault(a => addA == a.Line && kInfoA.LineAt == a.LineAt);
                                                        }

                                                        if (kInfoD != null)
                                                        {
                                                            tmpKInfos.Add(kInfoD);

                                                            KInfo kInfoE;
                                                            decimal addD = kInfoD.Line - 500m * flag;

                                                            if (kInfoD.LineAt != kInfoA.LineAt)
                                                            {
                                                                kInfoE = kInfos.FirstOrDefault(a => a.Line == kInfoD.Line + 500m && kInfoD.LineAt == a.LineAt);
                                                            }
                                                            else
                                                            {
                                                                if (addD <= abs)
                                                                {
                                                                    if (ignore0_5)
                                                                    {
                                                                        if (kInfoD.Line == 1000m)
                                                                        {
                                                                            kInfoE = kInfos.FirstOrDefault(a => a.Line == kInfoD.Line && kInfoD.LineAt != a.LineAt);
                                                                        }
                                                                        else
                                                                        {
                                                                            kInfoE = kInfos.FirstOrDefault(a => a.Line == kInfoD.Line - 500m && kInfoD.LineAt == a.LineAt);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        if (kInfoD.Line == 500m)
                                                                        {
                                                                            kInfoE = kInfos.FirstOrDefault(a => a.Line == kInfoD.Line && kInfoD.LineAt != a.LineAt);
                                                                        }
                                                                        else
                                                                        {
                                                                            kInfoE = kInfos.FirstOrDefault(a => a.Line == kInfoD.Line - 500m && kInfoD.LineAt == a.LineAt);
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    kInfoE = kInfos.FirstOrDefault(a => addD == a.Line && kInfoD.LineAt == a.LineAt);
                                                                }
                                                            }

                                                            if (kInfoE != null) tmpKInfos.Add(kInfoE);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        /*主讓
                                                            B盤：固定抓取來源比A盤壞100%盤口
                                                            C盤：固定抓取來源比B盤壞100%盤口
                                                            D盤：固定抓取來源比A盤好100%盤口，並跳過獨贏盤
                                                            E盤：固定抓取來源比D盤好100%盤口，並跳過獨贏盤

                                                            如果D盤有換讓頭E盤需抓取D盤壞於最少100%的平輸盤
                                                          客讓
                                                            B盤：固定抓取來源比A盤好100%盤口，並跳過獨贏盤
                                                            C盤：固定抓取來源比B盤好100%盤口，並跳過獨贏盤
                                                            D盤：固定抓取來源比A盤壞100%盤口
                                                            E盤：固定抓取來源比D盤壞100%盤口
                                                            如果B盤有換讓頭C盤需抓取B盤壞於最少100%的平輸盤
                        
                                                            全場不抓取0輸盤
                                                            沒開0輸的全場
                                                            忽略讓邊　1平>0　的跳動％數，補到另一邊
                                                            有開0輸的玩法
                                                            忽略讓邊　0.5>0　的跳動％數，補到另一邊
                                                        */
                                                        decimal addA = kInfoA.Line + 500m * flag;
                                                        KInfo kInfoB;

                                                        if (addA <= abs)
                                                        {
                                                            kInfoB = kInfos.Where(a => a.LineAt != kInfoA.LineAt).OrderBy(a => a.Line).FirstOrDefault();
                                                        }
                                                        else
                                                        {
                                                            kInfoB = kInfos.FirstOrDefault(a => addA == a.Line && kInfoA.LineAt == a.LineAt);
                                                        }

                                                        if (kInfoB != null)
                                                        {
                                                            tmpKInfos.Add(kInfoB);

                                                            KInfo kInfoC;

                                                            if (kInfoB.LineAt != kInfoA.LineAt)
                                                            {
                                                                kInfoC = kInfos.Where(a => a.LineAt != kInfoA.LineAt && a != kInfoB).OrderBy(a => a.Line).FirstOrDefault();
                                                            }
                                                            else
                                                            {
                                                                decimal addB = kInfoB.Line + 500m * flag;

                                                                if (addB <= abs)
                                                                {
                                                                    kInfoC = kInfos.Where(a => a.LineAt != kInfoA.LineAt).OrderBy(a => a.Line).FirstOrDefault();
                                                                }
                                                                else
                                                                {
                                                                    kInfoC = kInfos.FirstOrDefault(a => addB == a.Line && kInfoB.LineAt == a.LineAt);
                                                                }
                                                            }

                                                            if (kInfoC != null) tmpKInfos.Add(kInfoC);
                                                        }

                                                        addA = kInfoA.Line - 500m * flag;
                                                        KInfo kInfoD;

                                                        if (addA <= abs)
                                                        {
                                                            kInfoD = kInfos.Where(a => a.LineAt != kInfoA.LineAt).OrderBy(a => a.Line).FirstOrDefault();
                                                        }
                                                        else
                                                        {
                                                            kInfoD = kInfos.FirstOrDefault(a => addA == a.Line && kInfoA.LineAt == a.LineAt);
                                                        }

                                                        if (kInfoD != null)
                                                        {
                                                            tmpKInfos.Add(kInfoD);

                                                            KInfo kInfoE;

                                                            if (kInfoD.LineAt != kInfoA.LineAt)
                                                            {
                                                                kInfoE = kInfos.Where(a => a.LineAt != kInfoA.LineAt && a != kInfoD).OrderBy(a => a.Line).FirstOrDefault();
                                                            }
                                                            else
                                                            {
                                                                decimal addD = kInfoD.Line - 500m * flag;

                                                                if (addD <= abs)
                                                                {
                                                                    kInfoE = kInfos.Where(a => a.LineAt != kInfoD.LineAt).OrderBy(a => a.Line).FirstOrDefault();
                                                                }
                                                                else
                                                                {
                                                                    kInfoE = kInfos.FirstOrDefault(a => a.Line == addD && kInfoD.LineAt == a.LineAt);
                                                                }
                                                            }

                                                            if (kInfoE != null) tmpKInfos.Add(kInfoE);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        //OU
                                        else if (gpdmGroup.Key.type == 2)
                                        {
                                            //鴛鴦盤全抓
                                            if (maxNum > 5)
                                            {
                                                foreach (var gpdm in tmpGpdm)
                                                {
                                                    tmpKInfos.Add(gpdm[kType]);
                                                }
                                            }
                                            else
                                            {
                                                KInfo kInfoA = firstGpdm[kType];

                                                var kInfos = tmpGpdm.Select(a => a[kType]);

                                                //NBA DS 全場上半額外處理
                                                if (isNBA && (gpdmGroup.Key.specials == 0 || gpdmGroup.Key.specials == 1))
                                                {
                                                    tmpKInfos = NbaDsFtHtHandle(kInfoA, kInfos, maxNum, 2, gpdmGroup.Key.specials);
                                                }
                                                else if (maxNum == 1)
                                                {
                                                    tmpKInfos.Add(kInfoA);
                                                }
                                                else if (maxNum == 3)
                                                {
                                                    /*B盤：固定抓取來源比A盤壞100%盤口
                                                      C盤：固定抓取來源比A盤好100%盤口
                                                    */
                                                    tmpKInfos.Add(kInfoA);

                                                    decimal range = 500m;
                                                    KInfo kInfoB = kInfos.FirstOrDefault(a => kInfoA.Line + range == a.Line);
                                                    KInfo kInfoC = kInfos.FirstOrDefault(a => kInfoA.Line - range == a.Line);

                                                    if (kInfoB != null) tmpKInfos.Add(kInfoB);
                                                    if (kInfoC != null) tmpKInfos.Add(kInfoC);
                                                }
                                                else if (maxNum == 5)
                                                {
                                                    /*B盤：固定抓取來源比A盤壞200%盤口
                                                      C盤：固定抓取來源比A盤壞100%盤口
                                                      D盤：固定抓取來源比A盤好100%盤口
                                                      E盤：固定抓取來源比A盤好200%盤口
                                                     */
                                                    tmpKInfos.Add(kInfoA);

                                                    KInfo kInfoB = kInfos.FirstOrDefault(a => kInfoA.Line + 1000m == a.Line);
                                                    KInfo kInfoC = kInfos.FirstOrDefault(a => kInfoA.Line + 500m == a.Line);
                                                    KInfo kInfoD = kInfos.FirstOrDefault(a => kInfoA.Line - 500m == a.Line);
                                                    KInfo kInfoE = kInfos.FirstOrDefault(a => kInfoA.Line - 1000m == a.Line);

                                                    if (kInfoB != null) tmpKInfos.Add(kInfoB);
                                                    if (kInfoC != null) tmpKInfos.Add(kInfoC);
                                                    if (kInfoD != null) tmpKInfos.Add(kInfoD);
                                                    if (kInfoE != null) tmpKInfos.Add(kInfoE);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            foreach (var gpdm in tmpGpdm)
                                            {
                                                tmpKInfos.Add(gpdm[kType]);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        foreach (var gpdm in tmpGpdm)
                                        {
                                            tmpKInfos.Add(gpdm[kType]);
                                        }
                                    }

                                    //生成gpdm並新增對應字典
                                    for (byte setType = 1; setType <= tmpKInfos.Count; setType++)
                                    {
                                        var kInfo = tmpKInfos[setType - 1];

                                        if (gpdmGroup.Key.set == 0)
                                        {
                                            kInfo.SetType = setType;
                                        }

                                        GpdmInfoRadar gpdmInfo = new GpdmInfoRadar(gpdmGroup.Key.specials, setType);
                                        gpdmInfo = tmpGameinfo.ObjDic.GetOrAdd(gpdmInfo.Key, gpdmInfo) as GpdmInfoRadar;
                                        gpdmInfo.TryAdd(kType, kInfo);
                                        gpdmList.Add(gpdmInfo);
                                    }
                                }
                            }
                        }

                        //補格子
                        foreach (GpdmInfoRadar gpdm in gpdmList)
                        {
                            gpdm.AutoCreate(tmpGameinfo.GpType);
                        }
                    }
                }
                else
                {

                }

                if (Config.gameDic.TryGetValue(tmpGameinfo.GetGameDictKey(), out GameInfo gameInfo))
                {
                    (gameInfo as GameInfoRadar).Compare(tmpGameinfo, tmpGameinfo.GpType);
                }
                else
                {
                    if (tmpGameinfo.DataId >= 0)
                    {
                        var oldGame = Config.gameDic.Values.FirstOrDefault(a => (a as GameInfoRadar)?.SourceId == tmpGameinfo.SourceId && a.GpType == tmpGameinfo.GpType);

                        if (oldGame != null)
                        {
                            oldGame.DataId = -1;
                        }

                        Config.gameDic.AddOrUpdate(tmpGameinfo.GetGameDictKey(), tmpGameinfo, (k, v) => tmpGameinfo);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.SetLog(LogMode.Error, $"{game.Id}: {ex}");
            }
        }

        /// <summary>
        /// 設定GPDM層
        /// </summary>
        /// <param name="tmpGameinfo"></param>
        /// <param name="isSourceFollow"></param>
        /// <param name="gpdmList"></param>
        /// <param name="market"></param>
        /// <param name="specials"></param>
        /// <param name="line"></param>
        /// <param name="set"></param>
        /// <param name="num"></param>
        /// <param name="kType"></param>
        /// <param name="kInfo"></param>
        /// <param name="gpdm"></param>
        private static void SetGpdm(GameInfoRadar tmpGameinfo, int isSourceFollow, ICollection<GpdmInfoRadar> gpdmList, RadarMarket market, int specials, decimal line, byte set, byte num, int kType, out KInfoRadar kInfo, out GpdmInfoRadar gpdm)
        {
            SetKInfo(tmpGameinfo, isSourceFollow, market, line, set, num, kType, out byte setType, out kInfo);

            //生成gpdm並新增對應字典
            gpdm = new GpdmInfoRadar(specials, setType);
        }

        /// <summary>
        /// 設定Kinfo
        /// </summary>
        /// <param name="tmpGameinfo"></param>
        /// <param name="isSourceFollow"></param>
        /// <param name="market"></param>
        /// <param name="line"></param>
        /// <param name="set"></param>
        /// <param name="num"></param>
        /// <param name="kType"></param>
        /// <param name="setType"></param>
        /// <param name="kInfo"></param>
        private static void SetKInfo(GameInfoRadar tmpGameinfo, int isSourceFollow, RadarMarket market, decimal line, byte set, byte num, int kType, out byte setType, out KInfoRadar kInfo)
        {
            setType = set == 0 ? num : set;
            //生成物件
            kInfo = new KInfoRadar(tmpGameinfo.GpType.ToString(), 0, kType, setType);

            if ((kInfo.PlayTyp & 3) > 0)
            {
                kInfo.IsSourceFollow = isSourceFollow;
            }

            int bNum = 1;

            IEnumerable<OutcomeOdds> tmpOutcomeOdds;

            //交換隊伍RT、PL處理
            if (tmpGameinfo.Home == 2 && (kInfo.PlayTyp == 1 || kInfo.PlayTyp == 4))
            {
                kInfo.SetLine(Math.Abs(line), line < 0 ? 0 : 1);
                tmpOutcomeOdds = market.Odds?.AsEnumerable().Reverse();
            }
            else
            {
                kInfo.SetLine(Math.Abs(line), line <= 0 || kInfo.PlayTyp == 2 ? 1 : 0);
                tmpOutcomeOdds = market.Odds?.OrderBy(a => a.Id);
            }

            if (tmpOutcomeOdds != null)
            {
                foreach (var outcome in tmpOutcomeOdds)
                {
                    BInfo bInfo = new BInfo(Config.GetBType(kType, setType, bNum));

                    if (outcome.Odds.HasValue)
                    {
                        bInfo.SetOdds(Convert.ToDecimal(outcome.Odds.Value) - 1);
                    }

                    kInfo.BInfoDic.TryAdd(bInfo.BType, bInfo);
                    bNum++;
                }

                kInfo.SetIsOpen(market.Status == 1);
            }
            else
            {
                kInfo.SetIsOpen(false);
            }

            kInfo.MarketStatus = market.Status;
        }

        /// <summary>
        /// 設定ZD賽事狀態
        /// </summary>
        /// <param name="match"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        private bool SetGameStatus(GameInfoRadar game, RadarGame eventInfo)
        {
            if (eventInfo.StatusProperties != null)
            {
                eventInfo.StatusProperties.TryGetValue("Status", out Value eventStatus);

                if (game.GpType == GpType.Zd)
                {
                    bool isChangeTeam = game.Home == 2;
                    game.Status = 5;

                    if (eventInfo.StatusProperties.TryGetValue("Clock_RemainingTimeInPeriod", out Value val))
                    {
                        if (TimeSpan.TryParse($"00:{val.StringValue}", out TimeSpan time))
                        {
                            game.LastSec = (int)time.TotalSeconds;
                        }
                    }

                    if (eventInfo.StatusProperties.TryGetValue("HomeScore", out Value homeScore) && eventInfo.StatusProperties.TryGetValue("AwayScore", out Value awayScore))
                    {
                        int teamAScore = Convert.ToInt32(homeScore.DecimalValue);
                        int teamBScore = Convert.ToInt32(awayScore.DecimalValue);
                        ConvertTools.ExchangeValue(ref teamAScore, ref teamBScore, isChangeTeam);
                        game.Score.SetScore(true, teamAScore, teamBScore);
                    }

                    if (eventInfo.StatusProperties.TryGetValue("PeriodScore1_HomeScore", out homeScore) &&
                        eventInfo.StatusProperties.TryGetValue("PeriodScore1_AwayScore", out awayScore)
                        )
                    {
                        int scoreA = Convert.ToInt32(homeScore.DecimalValue);
                        int scoreB = Convert.ToInt32(awayScore.DecimalValue);

                        ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isChangeTeam);

                        if (game.RegularNnumberOfPeriods != 2)
                        {
                            game.Score1.SetScore(true, scoreA, scoreB);
                        }
                        else
                        {
                            game.ScoreHalf1.SetScore(true, scoreA, scoreB);
                        }
                    }

                    if (eventInfo.StatusProperties.TryGetValue("PeriodScore2_HomeScore", out homeScore) &&
                        eventInfo.StatusProperties.TryGetValue("PeriodScore2_AwayScore", out awayScore)
                        )
                    {
                        int scoreA = Convert.ToInt32(homeScore.DecimalValue);
                        int scoreB = Convert.ToInt32(awayScore.DecimalValue);

                        ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isChangeTeam);

                        if (game.RegularNnumberOfPeriods != 2)
                        {
                            game.Score2.SetScore(true, scoreA, scoreB);
                        }
                        else
                        {
                            game.ScoreHalf2.SetScore(true, scoreA, scoreB);
                        }
                    }

                    if (eventInfo.StatusProperties.TryGetValue("PeriodScore3_HomeScore", out homeScore) &&
                      eventInfo.StatusProperties.TryGetValue("PeriodScore3_AwayScore", out awayScore)
                      )
                    {
                        int scoreA = Convert.ToInt32(homeScore.DecimalValue);
                        int scoreB = Convert.ToInt32(awayScore.DecimalValue);
                        ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isChangeTeam);
                        game.Score3.SetScore(true, scoreA, scoreB);
                    }

                    if (eventInfo.StatusProperties.TryGetValue("PeriodScore4_HomeScore", out homeScore) &&
                     eventInfo.StatusProperties.TryGetValue("PeriodScore4_AwayScore", out awayScore)
                     )
                    {
                        int scoreA = Convert.ToInt32(homeScore.DecimalValue);
                        int scoreB = Convert.ToInt32(awayScore.DecimalValue);
                        ConvertTools.ExchangeValue(ref scoreA, ref scoreB, isChangeTeam);
                        game.Score4.SetScore(true, scoreA, scoreB);
                    }

                    if (game.Score1.IsOpen)
                    {
                        game.ScoreHalf1.SetScore(true, game.Score1.ScoreA + game.Score2.ScoreA, game.Score1.ScoreB + game.Score2.ScoreB);
                    }

                    if (game.Score3.IsOpen)
                    {
                        game.ScoreHalf2.SetScore(true, game.Score3.ScoreA + game.Score4.ScoreA, game.Score3.ScoreB + game.Score4.ScoreB);
                    }

                    if (eventInfo.StatusProperties.TryGetValue("MatchStatus", out Value obj))
                    {
                        switch (obj.IntValue)
                        {
                            case 13:    //Q1
                            case 301:   //Q1 break
                                game.Status = 1;
                                break;
                            case 14:    //Q2
                                game.Status = 2;
                                break;
                            case 15:    //Q3
                            case 303:   //Q3 break
                                game.Status = 3;
                                break;
                            case 16:    //Q4
                            case 304:   //Q4 break
                            case 32:    //Awaiting extra time                            
                                game.Status = 4;
                                break;
                            case 31:    //Halftime
                            case 302:   //Q2 break                   
                                game.Status = 6;
                                break;
                            case 1:     //Half1
                            case 6:
                                game.Status = 11;
                                break;
                            case 2:     //Half2
                            case 7:
                                game.Status = 12;
                                break;
                            case 90:    //Abandoned(放棄)
                            case 100:   //END
                            case 110:   //AET(加時結束)
                                return false;
                            case 33:    //加時
                            case 40:
                                game.Status = 13;
                                break;
                            default:
                                game.Status = 5;
                                break;
                        }
                    }

                    switch (eventStatus.IntValue)
                    {
                        case 9: //EventStatus.Abandoned
                        case 8: //EventStatus.Postponed
                        case 5: //EventStatus.Canceled
                        case 4: // EventStatus.Closed:
                        case 3: // EventStatus.Ended:
                            return false;
                    }

                    return eventInfo.Booking == 2;
                }
                else
                {
                    switch (eventStatus.IntValue)
                    {
                        case 9: //EventStatus.Abandoned
                        case 8: //EventStatus.Postponed
                        case 5: //EventStatus.Canceled
                        case 4: // EventStatus.Closed:
                        case 3: // EventStatus.Ended:
                        case 1: // EventStatus.Live
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 雷達玩法ID對應NBBGP玩法ID
        /// </summary>
        /// <param name="gpType"></param>
        /// <param name="market"></param>
        /// <param name="specials"></param>
        /// <param name="type"></param>
        /// <param name="line"></param>
        /// <param name="setType"></param>
        /// <returns></returns>
        private bool GetSpecialandType(GpType gpType, RadarMarket market, out int specials, out int type, out decimal line, out byte setType, out Scene scene, bool isNBA)
        {
            scene = Scene.Main;
            specials = -1;
            type = -1;
            line = 0;
            setType = 0;
            string quarter = "";

            if (market.Specifiers?.TryGetValue("quarternr", out quarter) == true && int.TryParse(quarter, out int num))
            {
                scene = (Scene)num;

                switch (market.Id)
                {
                    case 303:   //第num節RF
                        specials = num + 2;
                        type = 1;
                        break;
                    case 236:   //第num節OU
                        specials = num + 2;
                        type = 2;
                        break;
                    case 302:   //第num節DY
                        specials = num + 2;
                        type = 3;
                        break;
                    case 304:   //第num節OE
                        specials = num + 2;
                        type = 4;
                        break;
                    case 756:   //第num節 A隊總得分
                        specials = isNBA ? 19 + (2 * num) : 19 + (2 * num - 1);
                        type = 2;
                        break;
                    case 757:   //第num節 B隊總得分
                        specials = isNBA ? 19 + (2 * num - 1) : 19 + (2 * num);
                        type = 2;
                        break;
                    case 960:   //Q1 最後得分、上半 最後得分
                        if (isNBA && gpType == GpType.Ds)
                        {
                            if (num == 2)
                            {
                                specials = 121;
                            }
                            else if (num == 1)
                            {
                                specials = 122;
                            }

                            type = 1;
                            setType = 9;
                        }
                        break;
                }
            }
            else
            {
                switch (market.Id)
                {
                    case 223:   //全場RF
                        specials = 0;
                        type = 1;
                        break;
                    case 225:   //全場OU
                        specials = 0;
                        type = 2;
                        break;
                    case 219:   //全場DY
                        specials = 0;
                        type = 3;
                        break;
                    case 229:   //全場OE
                        specials = 0;
                        type = 4;
                        break;
                    case 66:    //上半RF
                        specials = 1;
                        type = 1;
                        scene = Scene.Half1;
                        break;
                    case 68:    //上半OU
                        specials = 1;
                        type = 2;
                        scene = Scene.Half1;
                        break;
                    case 64:    //上半DY
                        specials = 1;
                        type = 3;
                        scene = Scene.Half1;
                        break;
                    case 74:    //上半OE
                        specials = 1;
                        type = 4;
                        scene = Scene.Half1;
                        break;
                    case 231:    //下半RF
                        specials = 2;
                        type = 1;
                        scene = Scene.Half2;
                        break;
                    case 232:    //下半OU
                        specials = 2;
                        type = 2;
                        scene = Scene.Half2;
                        break;
                    case 294:    //下半DY
                        specials = 2;
                        type = 3;
                        scene = Scene.Half2;
                        break;
                    case 295:    //下半OE
                        specials = 2;
                        type = 4;
                        scene = Scene.Half2;
                        break;
                    case 227:   //全場A隊總得分
                        specials = isNBA ? 15 : 14;
                        type = 2;
                        break;
                    case 228:   //全場B隊總得分
                        specials = isNBA ? 14 : 15;
                        type = 2;
                        break;
                    case 69:    //上半A隊總得分
                        if (gpType == GpType.Ds)
                        {
                            specials = isNBA ? 17 : 16;
                            type = 2;
                            scene = Scene.Half1;
                        }
                        break;
                    case 70:    //上半B隊總得分
                        if (gpType == GpType.Ds)
                        {
                            specials = isNBA ? 16 : 17;
                            type = 2;
                            scene = Scene.Half1;
                        }
                        break;
                    //case 234:    //單節最高得分
                    //    specials = 80;
                    //    type = 1;
                    //    break;
                    case 230:   //全場優先得N分
                        if (isNBA)
                        {
                            specials = 101;
                            type = 1;

                            if (market.Specifiers.TryGetValue("pointnr", out string value))
                            {
                                setType = byte.Parse(value);
                            }
                        }
                        break;
                    case 291:   //搶首
                        if (isNBA && gpType == GpType.Ds)
                        {
                            specials = 60;
                            type = 1;
                        }
                        break;
                    case 961:   //首先獲得罰球
                        if (isNBA && gpType == GpType.Ds)
                        {
                            specials = 161;
                            type = 1;
                        }
                        break;
                    case 956:    //首先暫停
                        if (isNBA && gpType == GpType.Ds)
                        {
                            specials = 41;
                            type = 1;
                        }
                        break;
                }
            }

            if (type == 1)
            {
                if (market.Specifiers.TryGetValue("hcp", out string value))
                {
                    line = decimal.Parse(value);
                }

                //全場不抓0輸
                if (specials == 0 && Math.Abs(line) == 0.5m)
                {
                    specials = -1;
                    return false;
                }
            }
            else if (type == 2)
            {
                if (market.Specifiers.TryGetValue("total", out string value))
                {
                    line = decimal.Parse(value);
                }
            }
            return specials >= 0;
        }

        /// <summary>
        /// NBA DS 全場上半抓取邏輯(包含鴛鴦盤處理)
        /// </summary>
        /// <param name="kInfoA">來源主盤</param>
        /// <param name="kInfos">來源pkpl</param>
        /// <param name="maxNum">選擇要跟幾盤鴛鴦盤</param>
        /// <param name="type">玩法type</param>
        /// <param name="specials">玩法specials</param>
        /// <returns></returns>
        private static List<KInfo> NbaDsFtHtHandle(KInfo kInfoA, IEnumerable<KInfo> kInfos, int maxNum, int type, int specials)
        {
            //先加入a盤
            List<KInfo> tmpKInfos = new List<KInfo>() { kInfoA };

            if (kInfos.Count() <= 1) { return tmpKInfos; };

            //轉換成PlateInfo物件
            List<PlateInfo> plateInfoList = new List<PlateInfo>();

            foreach (KInfo kInfo in kInfos)
            {
                plateInfoList.Add(new PlateInfo(kInfo));
            }

            //排序
            plateInfoList = plateInfoList.OrderBy(x => x.Plate).ToList();
            //a盤轉換成PlateInfo
            PlateInfo plate_a = new PlateInfo(kInfoA);
            //a盤位置(判斷全好壞使用
            int index_a = plateInfoList.IndexOf(plate_a);
            //存放抓取鴛鴦盤(存放BCDE盤)
            PlateInfo[] plateInfos = new PlateInfo[4];
            //判斷是否全好全壞
            bool isAllBacdGood = index_a == 0 || index_a == plateInfoList.Count - 1;
            //存放全好全壞固定方向抓法
            bool isMinus = false;
            //上半額外檢查是否有勾選
            int sameCount = (specials == 1 && !Config.isSbDjSame) || maxNum == 1 ? 1 : maxNum;

            for (int i = 1; i < sameCount; i++)
            {
                Config.ArrangedNbaDsFtHtSameCountOdds(plateInfoList, plate_a, type, i, sameCount, isAllBacdGood, ref plateInfos, ref isMinus);
            }

            foreach (PlateInfo tmpPlateInfo in plateInfos)
            {
                if (tmpPlateInfo != null)
                {
                    KInfo tmpKInfo = null;

                    //rf額外尋找rt
                    if (type == 1)
                    {
                        int lineAt = tmpPlateInfo.Plate < 0 ? 0 : 1;
                        tmpKInfo = kInfos.FirstOrDefault(x => x.Line == (Math.Abs(tmpPlateInfo.Plate) * 1000) && x.LineAt == lineAt);
                    }
                    else
                    {
                        tmpKInfo = kInfos.FirstOrDefault(x => x.Line == tmpPlateInfo.Plate * 1000);
                    }

                    if (tmpKInfo != null)
                    {
                        tmpKInfos.Add(tmpKInfo);
                    }
                }
            }

            return tmpKInfos;
        }

        /// <summary>
        /// 場次
        /// </summary>
        enum Scene
        {
            Main = 0,
            Q1 = 1,
            Q2 = 2,
            Q3 = 3,
            Q4 = 4,
            Half1 = 5,
            Half2 = 6
        }
    }
}
