#region 功能修改描述與時間

/*
    描述：阻擋入庫清單
    日期：2025-01-20
*/

#endregion

using System.Collections.Concurrent;

namespace Yw_BK.model
{
    /// <summary>
    /// 賽事組擋清單
    /// </summary>
    public class BlockGameList
    {
        /// <summary>
        /// 不GP聯盟 ｜key：聯盟唯一值game.GetUniqueLeague()｜value：添加時間
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> BlockLeagueDic = new ConcurrentDictionary<string, DateTime>();
        /// <summary>
        /// 不GP賽事 ｜key：賽事唯一值game.GetGameDictKey()｜value：添加時間
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> BlockGameDic = new ConcurrentDictionary<string, DateTime>();
        /// <summary>
        /// 不GP玩法 ｜key：不同層級Level 唯一值(根:GpType，聯盟: game.GetUniqueLeague()，賽事:game.GetGameDictKey()｜value： 玩法阻擋功能清單
        /// </summary>
        private readonly ConcurrentDictionary<string, BlockGpdmList> BlockGpdmDic = new ConcurrentDictionary<string, BlockGpdmList>();

        /// <summary>
        /// 添加阻擋聯盟
        /// </summary>
        /// <param name="league">game.GetUniqueLeague()</param>
        /// <returns></returns>
        public bool AddLeague(string league)
        {
            return BlockLeagueDic.TryAdd(league, DateTime.Now);
        }

        /// <summary>
        /// 移除阻擋聯盟
        /// </summary>
        /// <param name="league">game.GetUniqueLeague()</param>
        /// <returns></returns>
        public bool RemoveLeague(string league)
        {
            return BlockLeagueDic.TryRemove(league, out var _);
        }

        /// <summary>
        /// 添加阻擋賽事
        /// </summary>
        /// <param name="game">game.GetGameDictKey()</param>
        /// <returns></returns>
        public bool AddGame(string game)
        {
            return BlockGameDic.TryAdd(game, DateTime.Now);
        }

        /// <summary>
        /// 移除阻擋賽事
        /// </summary>
        /// <param name="game">game.GetGameDictKey()</param>
        /// <returns></returns>
        public bool RemoveGame(string game)
        {
            return BlockGameDic.TryRemove(game, out var _);
        }

        /// <summary>
        /// 聯盟是否阻擋
        /// </summary>
        /// <param name="league">game.GetUniqueLeague()</param>
        /// <returns></returns>
        public bool BlockLeague(string league)
        {
            return BlockLeagueDic.ContainsKey(league);
        }

        /// <summary>
        /// 賽事是否阻擋
        /// </summary>
        /// <param name="game">game.GetGameDictKey()</param>
        /// <returns></returns>
        public bool BlockGame(string game)
        {
            return BlockGameDic.ContainsKey(game);
        }


        /// <summary>
        /// 取得或添加玩法阻擋功能清單
        /// </summary>
        /// <param name="levelKey">根:GpType.ToString("d")，聯盟: game.GetUniqueLeague()，賽事:game.GetGameDictKey()</param>
        /// <param name="list">玩法阻擋清單</param>
        /// <returns></returns>
        public BlockGpdmList GetOrAddGpdmList(string levelKey, BlockGpdmList list)
        {
            return BlockGpdmDic.GetOrAdd(levelKey, list);
        }

        /// <summary>
        /// 移除玩法阻擋功能清單
        /// </summary>
        /// <param name="levelKey">根:GpType.ToString("d")，聯盟: game.GetUniqueLeague()，賽事:game.GetGameDictKey()</param>
        /// <returns></returns>
        public bool RemoveGpdmList(string levelKey)
        {
            return BlockGpdmDic.TryRemove(levelKey, out var _);
        }

        /// <summary>
        /// 玩法是否阻擋
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="gpdmInfo"></param>
        /// <returns></returns>
        public bool BlockGpdm(GameInfo gameInfo, GpdmInfo gpdmInfo)
        {
            //根層控制
            bool rootControl = false;
            //獨立控制
            bool uniqueControl = false;

            if (BlockGpdmDic.TryGetValue(gameInfo.GetUniqueLeague(), out BlockGpdmList leagueList))
            {
                uniqueControl = leagueList.UniqueControl;
            }

            if (BlockGpdmDic.TryGetValue(gameInfo.GpType.ToString("d"), out BlockGpdmList rootList))
            {
                bool anyBlock = (rootList.BlockGameLine | rootList.Block2Half | rootList.BlockQ2 | rootList.BlockQ4);
                rootControl = (anyBlock ^ uniqueControl) & anyBlock;
            }

            if (rootControl)
            {
                return rootList?.BlockGpdm(gpdmInfo) == true || (BlockGpdmDic.TryGetValue(gameInfo.GetGameDictKey(), out BlockGpdmList? blockGpdmList) && blockGpdmList.BlockGpdm(gpdmInfo));
            }
            else
            {
                return leagueList?.BlockGpdm(gpdmInfo) == true || (BlockGpdmDic.TryGetValue(gameInfo.GetGameDictKey(), out BlockGpdmList? blockGpdmList) && blockGpdmList.BlockGpdm(gpdmInfo));
            }
        }
    }

    /// <summary>
    /// 玩法阻擋清單
    /// </summary>
    public class BlockGpdmList
    {
        /// <summary>
        /// 是否獨立控制
        /// </summary>
        public bool UniqueControl { get; set; }
        /// <summary>
        /// 鴛鴦盤阻擋開關
        /// </summary>
        public bool BlockGameLine { get; set; }
        /// <summary>
        /// Q2阻擋開關
        /// </summary>
        public bool BlockQ2 { get; set; }
        /// <summary>
        /// Q4阻擋開關
        /// </summary>
        public bool BlockQ4 { get; set; }
        /// <summary>
        /// 下半場阻擋開關
        /// </summary>
        public bool Block2Half { get; set; }

        /// <summary>
        /// 玩法阻擋清單
        /// </summary>

        private readonly ConcurrentDictionary<decimal, DateTime> BlockGpdmDic = new ConcurrentDictionary<decimal, DateTime>();

        /// <summary>
        /// 添加阻擋玩法
        /// </summary>
        /// <param name="gpdm"></param>
        /// <returns></returns>
        public bool Add(decimal gpdm)
        {
            return BlockGpdmDic.TryAdd(gpdm, DateTime.Now);
        }

        /// <summary>
        /// 移除阻擋玩法
        /// </summary>
        /// <param name="gpdm"></param>
        /// <returns></returns>
        public bool Remove(decimal gpdm)
        {
            return BlockGpdmDic.TryRemove(gpdm, out var _);
        }

        /// <summary>
        /// 玩法是否阻擋
        /// </summary>
        /// <param name="gpdmInfo"></param>
        /// <returns></returns>
        public bool BlockGpdm(GpdmInfo gpdmInfo)
        {
            KInfo? firstKinfo = gpdmInfo.Values.FirstOrDefault();

            if (firstKinfo != null)
            {
                if (BlockGameLine)
                {
                    if (gpdmInfo.SetType > 1)
                    {
                        switch (firstKinfo.KType)
                        {
                            case 12010:
                            case 12011:
                            case 12012:
                            case 12109:
                            case 12112:
                            case 12212:
                            case 12309:
                            case 12312:
                            case 12410:
                            case 12411:
                            case 12412:
                            case 12509:
                            case 12510:
                            case 12511:
                            case 12512:
                            case 12610:
                            case 12612:
                            case 12801:
                            case 12802:
                                return false;
                            default:
                                return true;
                        }
                    }
                }

                if (BlockQ2)
                {
                    if (firstKinfo.KType / 100 == 124)
                    {
                        return true;
                    }
                }

                if (BlockQ4)
                {
                    if (firstKinfo.KType / 100 == 126)
                    {
                        return true;
                    }
                }

                if (Block2Half)
                {
                    if (firstKinfo.KType / 100 == 122)
                    {
                        return true;
                    }
                }

                return BlockGpdmDic.ContainsKey(gpdmInfo.Key);
            }

            return false;
        }
    }
}
