#region 功能修改描述與時間

/*
    描述：全自動移盤篩選區分
    日期：2025-04-01
*/

#endregion
using YwCore.model;
using YwCore.model.interfaces;

namespace Yw_BK.model
{
    /// <summary>
    /// 全自動移盤篩選
    /// </summary>
    internal class FilterAutoCreate : IFilterAutoCreate
    {
        /// <summary>
        /// 篩選
        /// </summary>
        /// <param name="createGameApiModel"></param>
        /// <returns></returns>
        public async Task<IEnumerable<AutoCreateGameApiModel>> Filter(AutoCreateGameApiModel createGameApiModel)
        {
            List<AutoCreateGameApiModel> models = new List<AutoCreateGameApiModel>();

            var result = await YwCore.Common.GameCacheServiceClient.GetCreatedOddsAsync(new Gp.Service.GetCreatedOddsRequest() { GId = createGameApiModel.GId, Sport = (int)Config.sport });

            //同玩法已移盤或待結算不需再移
            var joinResult = result.Result.Join(createGameApiModel.OddsIdList, a => new { a.KType }, b => new { b.KType }, (a, b) => new { b.Id, a.KType, b.BType, b.SetType, a.SourceId });

            foreach (var createdOdds in joinResult)
            {
                createGameApiModel.OddsIdList.Remove(new OddsInfoModel() { Id = createdOdds.Id, KType = createdOdds.KType, BType = createdOdds.BType, SetType = createdOdds.SetType, SourceId = createGameApiModel.SourceId });
            }

            //此批次含主要玩法
            if (createGameApiModel.OddsIdList.Any(a => a.KType <= 12004))
            {
                var otherPlay = createGameApiModel.OddsIdList.Where(a => a.KType > 12004).ToHashSet();

                //若有其他玩法需另分割成下一批處理
                if (otherPlay.Count > 0)
                {
                    foreach (var play in otherPlay)
                    {
                        createGameApiModel.OddsIdList.Remove(play);
                    }

                    models.Add(createGameApiModel);
                    models.Add(new AutoCreateGameApiModel(createGameApiModel.GId, createGameApiModel.SourceId, otherPlay, createGameApiModel.Group, createGameApiModel.GameDate, createGameApiModel.TmaId, createGameApiModel.TmbId, createGameApiModel.SuffixId, createGameApiModel)
                    {
                        AllyName = createGameApiModel.AllyName,
                        TmaName = createGameApiModel.TmaName,
                        TmbName = createGameApiModel.TmbName,
                        SuffixName = createGameApiModel.SuffixName,
                    });
                }
                else
                {
                    models.Add(createGameApiModel);
                }
            }
            else
            {
                //同來源主玩法不在操盤頁，其餘玩法不移盤
                if (result.Result.Any(a => a.KType <= 12004 && a.SourceId == createGameApiModel.SourceId && a.IsCreateGame == 1))
                {
                    models.Add(createGameApiModel);
                }
            }

            return models;
        }
    }

    /// <summary>
    /// 半自動移盤篩選
    /// </summary>
    internal class FilterSemiAutoCreate : IFilterAutoCreate
    {
        /// <summary>
        /// 篩選
        /// </summary>
        /// <param name="createGameApiModel"></param>
        /// <returns></returns>
        public async Task<IEnumerable<AutoCreateGameApiModel>> Filter(AutoCreateGameApiModel createGameApiModel)
        {
            var result = await YwCore.Common.GameCacheServiceClient.GetCreatedOddsAsync(new Gp.Service.GetCreatedOddsRequest() { GId = createGameApiModel.GId, Sport = (int)Config.sport });

            //主玩法已在操盤
            if (result.Result.Any(a => a.KType <= 12004 && a.SourceId == createGameApiModel.SourceId && a.IsCreateGame == 1))
            {
                //同玩法已移盤或待結算不需再移
                var joinResult = result.Result.Join(createGameApiModel.OddsIdList, a => new { a.KType }, b => new { b.KType }, (a, b) => new { b.Id, a.KType, b.BType, b.SetType, a.SourceId });

                foreach (var createdOdds in joinResult)
                {
                    createGameApiModel.OddsIdList.Remove(new OddsInfoModel() { Id = createdOdds.Id, KType = createdOdds.KType, BType = createdOdds.BType, SetType = createdOdds.SetType, SourceId = createGameApiModel.SourceId });
                }
            }
            else
            {
                createGameApiModel.OddsIdList.Clear();
            }


            return new List<AutoCreateGameApiModel>() { createGameApiModel };
        }
    }

}
