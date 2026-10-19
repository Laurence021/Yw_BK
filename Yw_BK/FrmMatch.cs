#region 功能修改描述與時間

/*
    描述：賽事頁面
    日期：2019-08-24

    描述：移除賽事層勾選不必要的邏輯
    日期：2022-04-13

    描述：添加GPDM玩法層選勾
    日期：2024-12-19

    描述：各層級選勾調整成永久開放
    日期：2025-01-06

    描述：添加排除鴛鴦盤、Q2、Q4功能
    日期：2025-01-20
*/

#endregion

using System.Collections.Concurrent;
using Yw_BK.model;
using YwCore;

namespace Yw_BK
{
    public partial class FrmMatch : Form
    {
        //Refresh計時器
        private System.Timers.Timer timer = new System.Timers.Timer();
        //視窗關閉後執行動作
        private Action afterClosed;

        /// <summary>
        /// 建構子
        /// </summary>
        public FrmMatch(Action afterFormClosed = null)
        {
            afterClosed = afterFormClosed;
            this.TextChanged += FrmMatch_TextChanged;
            InitializeComponent();
        }

        private void MatchForm_Load(object sender, EventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "賽事設定頁 狀態:開啟");
            YwCore.Common.SetTimer(timer, 3000, false, new System.Timers.ElapsedEventHandler((obj, eh) => { try { this.Invoke(new Action(() => SetTreeView(tvMatch, Config.gameDic))); } catch { } finally { if (!this.IsDisposed) ((System.Timers.Timer)obj).Start(); } }));

            SetTreeView(tvMatch, Config.gameDic);
        }

        private void SetTreeView(TreeView treeView, ConcurrentDictionary<string, GameInfo> games)
        {
            //欲刪除節點
            List<TreeNode> delNode = new List<TreeNode>();

            treeView.BeginUpdate();
            treeView.AfterCheck -= TvMatch_AfterCheck;

            #region 移除

            //根節點
            foreach (TreeNode root in treeView.Nodes)
            {
                var gpType = Config.gameDic.Values.Where(a => a.GpType.ToString("d") == root.Name);

                if (!gpType.Any())
                {
                    delNode.Add(root);
                }
                else
                {
                    //聯盟節點
                    foreach (TreeNode leagueNode in root.Nodes)
                    {
                        var league = gpType.Where(a =>
                        {
                            //有後綴才做拼接比對
                            return leagueNode.Name == a.GetUniqueLeague();
                        });

                        if (!league.Any())
                        {
                            delNode.Add(leagueNode);
                        }
                        else
                        {
                            //對戰組合節點
                            foreach (TreeNode gameNode in leagueNode.Nodes)
                            {
                                var game = league.FirstOrDefault(a => a.GetGameDictKey() == gameNode.Name);

                                if (game == null)
                                {
                                    delNode.Add(gameNode);
                                }
                                else
                                {
                                    //GPDM節點
                                    foreach (TreeNode gpdmNode in gameNode.Nodes)
                                    {
                                        var gpdm = game.ObjDic.SingleOrDefault(a => a.Key == Convert.ToDecimal(gpdmNode.Name));

                                        if (gpdm.Value == null)
                                        {
                                            delNode.Add(gpdmNode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //移除節點
            foreach (TreeNode node in delNode)
            {
                tvMatch.Nodes.Remove(node);
            }

            #endregion

            #region 新增更新

            //根節點
            foreach (var root in games.Values.GroupBy(a => a.GpType))
            {
                TreeNode rootNode;
                //root變底色
                bool rootChangeColor = false;
                //GpType文字
                string rootText = root.Key.GetDescriptionText(YwCore.Properties.Resources.ResourceManager);

                if (treeView.Nodes.ContainsKey(root.Key.ToString("d")))
                {
                    rootNode = treeView.Nodes[root.Key.ToString("d")];

                    if (rootNode.Text != rootText)
                    {
                        rootNode.Text = rootText;
                    }
                }
                else
                {
                    rootNode = treeView.Nodes.Add(root.Key.ToString("d"), rootText);
                }

                rootNode.ContextMenuStrip = menuRoot;
                rootNode.HideCheckBox();

                //聯盟節點
                foreach (var league in root.OrderBy(a => a.GetUniqueLeague()).GroupBy(a => a.GetUniqueLeague()))
                {
                    GameInfo firstGame = league.First();
                    TreeNode leagueNode;
                    //league變底色
                    bool leagueChangeColor = false;
                    //聯盟顯示文字
                    string leagueText = string.Join("-", firstGame.League.Name, firstGame.Suffix.Name).Trim('-');
                    //聯盟key
                    string leagueKey = firstGame.GetUniqueLeague();
                    //有玩法勾除
                    bool anyLeaguePlayUnckeck = false;

                    if (rootNode.Nodes.ContainsKey(leagueKey))
                    {
                        leagueNode = rootNode.Nodes[leagueKey];
                    }
                    else
                    {
                        leagueNode = rootNode.Nodes.Add(leagueKey, leagueText);
                        //聯盟粗體字設定
                        leagueNode.NodeFont = new Font(tvMatch.Font, FontStyle.Bold);
                    }

                    if (league.Any(a => a.League.SourceName != a.League.Name))
                    {
                        leagueNode.ForeColor = Color.Red;
                    }
                    else
                    {
                        leagueNode.ForeColor = Color.Empty;
                    }

                    leagueNode.ContextMenuStrip = menuLeague;

                    if (Config.BlockList.BlockLeague(leagueKey))
                    {
                        if (leagueNode.Checked != false)
                        {
                            leagueNode.Checked = false;
                        }
                    }
                    else
                    {
                        if (leagueNode.Checked != true)
                        {
                            leagueNode.Checked = true;
                        }
                    }

                    //對戰組合節點
                    foreach (var game in league.OrderBy(a => a.GameDate))
                    {
                        TreeNode gameNode;
                        //game變底色
                        bool gameChangeColor = false;
                        //隊伍組合key
                        string gameKey = game.GetGameDictKey();
                        //string score = $"{game.Score1.DisplayScore},{game.Score2.DisplayScore},{game.Score3.DisplayScore},{game.Score4.DisplayScore},總:{game.Score.DisplayScore},剩餘:{game.LastSec},狀態:{game.Status}";
                        string teamA = game.TeamA.Name != game.TeamA.SourceName ? $"@{game.TeamA.Name}": game.TeamA.Name;
                        string teamB = game.TeamB.Name != game.TeamB.SourceName ? $"@{game.TeamB.Name}": game.TeamB.Name;

                        string gameText = $"{teamA} vs {teamB}, Gid:{game.GId}, SourceId:{game.SourceId}";

                        if (root.Key != GpType.Zd)
                        {
                            gameText = $"{game.GameDate.ToString("MM/dd HH:mm")} {gameText}";
                        }
                        else
                        {
#if DEBUG
                            string score = $"{game.Score1.DisplayScore},{game.Score2.DisplayScore},{game.Score3.DisplayScore},{game.Score4.DisplayScore},上半:{game.ScoreHalf1.DisplayScore},下半:{game.ScoreHalf2.DisplayScore},總:{game.Score.DisplayScore},剩餘:{game.LastSec},狀態:{game.Status}";
                            gameText += $",比分:{score}";
#endif
                        }

                        if (leagueNode.Nodes.ContainsKey(gameKey))
                        {
                            gameNode = leagueNode.Nodes[gameKey];

                            if (gameNode.Text != gameText)
                            {
                                gameNode.Text = gameText;
                            }
                        }
                        else
                        {
                            gameNode = leagueNode.Nodes.Add(gameKey, gameText);
                        }

                        if (game.League.SourceName != game.League.Name)
                        {
                            gameNode.ForeColor = Color.Red;
                        }
                        else
                        {
                            gameNode.ForeColor = Color.Empty;
                        }

                        //右鍵選單
                        gameNode.ContextMenuStrip = menuGameInfo;

                        if (leagueNode.Checked)
                        {
                            if (game.IsFilter())
                            {
                                if (gameNode.Checked != false)
                                {
                                    gameNode.Checked = false;
                                }
                            }
                            else
                            {
                                if (gameNode.Checked != true)
                                {
                                    gameNode.Checked = true;
                                }
                            }
                        }
                        else
                        {
                            //gameNode.HideCheckBox();
                        }

                        //GPDM節點GPDM
                        foreach (var play in game.ObjDic.Values.OrderBy(a => a.Values.Min(b => b.KType)).Select(a => new { a.Values.First().GroupText, Gpdm = a }))
                        {
                            TreeNode gpdmNode;
                            //gpdm變底色
                            bool gpdmChangeColor = false;
                            //玩法標題文字
                            string playText = play.GroupText + ", Gpdm:" + play.Gpdm.Key;

                            if (gameNode.Nodes.ContainsKey(play.Gpdm.Key.ToString()))
                            {
                                gpdmNode = gameNode.Nodes[play.Gpdm.Key.ToString()];

                                if (gpdmNode.Text != playText)
                                {
                                    gpdmNode.Text = playText;
                                }
                            }
                            else
                            {
                                gpdmNode = gameNode.Nodes.Add(play.Gpdm.Key.ToString(), playText);
                            }

                            //右鍵選單
                            gpdmNode.ContextMenuStrip = menuGpdm;

                            if (leagueNode.Checked && gameNode.Checked)
                            {
                                if (Config.BlockList.BlockGpdm(game, play.Gpdm))
                                {
                                    if (gpdmNode.Checked != false)
                                    {
                                        gpdmNode.Checked = false;
                                    }
                                }
                                else
                                {
                                    if (gpdmNode.Checked != true)
                                    {
                                        gpdmNode.Checked = true;
                                    }
                                }
                            }
                            else
                            {
                                //gpdmNode.HideCheckBox();
                            }

                            //玩法節點
                            foreach (var type in play.Gpdm.OrderBy(a => a.Value.PlayTyp))
                            {
                                TreeNode KinfoNode;
                                //玩法標題文字
                                string kInfoText = string.Format("{0}, KType:{1}, isOpen:{2}", type.Value.PlayText, type.Value.KType, type.Value.IsOpen.GetHashCode());

                                if (gpdmNode.Nodes.ContainsKey(type.Value.KType.ToString()))
                                {
                                    KinfoNode = gpdmNode.Nodes[type.Value.KType.ToString()];

                                    if (KinfoNode.Text != kInfoText)
                                    {
                                        KinfoNode.Text = kInfoText;
                                    }
                                }
                                else
                                {
                                    KinfoNode = gpdmNode.Nodes.Add(type.Value.KType.ToString(), kInfoText);
                                }

                                KinfoNode.ContextMenuStrip = menuPlay;
                                KinfoNode.HideCheckBox();

                                if (type.Value.IsTest)
                                {
                                    gpdmChangeColor = true;
                                    KinfoNode.BackColor = Color.Yellow;
                                }
                                else
                                {
                                    KinfoNode.BackColor = Color.Empty;
                                }

                                ////BTYPE節點
                                //foreach (var bType in type.Value.BInfoDic.Values.OrderBy(a => a.BType))
                                //{
                                //    TreeNode BinfoNode;
                                //    //標題文字
                                //    string bInfoText = string.Format("Btype:{0},RT:{1},Pk:{2} Odds:{3}", bType.BType, type.Value.LineAt, type.Value.Line, bType.Odds);

                                //    if (KinfoNode.Nodes.ContainsKey(bType.BType.ToString()))
                                //    {
                                //        BinfoNode = KinfoNode.Nodes[bType.BType.ToString()];

                                //        if (BinfoNode.Text != bInfoText)
                                //        {
                                //            BinfoNode.Text = bInfoText;
                                //        }
                                //    }
                                //    else
                                //    {
                                //        BinfoNode = KinfoNode.Nodes.Add(bType.BType.ToString(), bInfoText);
                                //    }

                                //    BinfoNode.HideCheckBox();
                                //}
                            }

                            if (gpdmChangeColor)
                            {
                                gameChangeColor = true;
                                gpdmNode.BackColor = Color.Yellow;
                            }
                            else
                            {
                                gpdmNode.BackColor = Color.Empty;
                            }
                        }

                        if (gameChangeColor || gameNode.Checked == false)
                        {
                            leagueChangeColor = true;
                            gameNode.BackColor = Color.Yellow;
                        }
                        else if (gameNode.Nodes.Cast<TreeNode>().Any(a => !a.Checked))
                        {
                            anyLeaguePlayUnckeck = true;
                            gameNode.BackColor = Color.FromArgb(246, 249, 109);
                        }
                        else
                        {
                            gameNode.BackColor = Color.Empty;
                        }
                    }

                    if (leagueChangeColor)
                    {
                        rootChangeColor = true;
                        leagueNode.BackColor = Color.Yellow;
                    }
                    else if (anyLeaguePlayUnckeck || leagueNode.Nodes.Cast<TreeNode>().Any(a => !a.Checked))
                    {
                        leagueNode.BackColor = Color.FromArgb(246, 249, 109);
                    }
                    else
                    {
                        leagueNode.BackColor = Color.Empty;
                    }
                }

                if (rootChangeColor)
                {
                    rootNode.BackColor = Color.Yellow;
                }
                else
                {
                    rootNode.BackColor = Color.Empty;
                }
            }

            #endregion

            treeView.AfterCheck += TvMatch_AfterCheck;
            treeView.EndUpdate();
        }

        ////點擊修改賽事資訊
        //private void MenuItemGameInfo_Click(object sender, EventArgs e)
        //{
        //    //日誌記錄操作
        //    LogFile.SetLog(LogMode.Control, $"點擊修改賽事資訊 : {tvMatch.SelectedNode.FullPath}");

        //    if (Config.gameDic.TryGetValue(tvMatch.SelectedNode.Name, out GameInfo game))
        //    {
        //        FrmGameTest frmGameInfo = new FrmGameTest($"{tvMatch.SelectedNode.Parent.Text}\n{tvMatch.SelectedNode.Text}", game);
        //        frmGameInfo.ShowDialog();
        //    }

        //    SetTreeView(tvMatch, Config.gameDic);
        //}

        //GridView內滑鼠點擊事件(避免選擇的資料與滑鼠點擊不同)
        private void TvMatch_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                //确定右键的位置
                Point clickPoint = new Point(e.X, e.Y);
                //在确定后的位置上面定义一个节点
                TreeNode treeNode = tvMatch.GetNodeAt(clickPoint);

                if (treeNode != null)
                {
                    tvMatch.SelectedNode = treeNode;
                }
            }
        }

        //視窗關閉
        private void FrmMatch_FormClosing(object sender, FormClosingEventArgs e)
        {
            LogFile.SetLog(LogMode.Control, "賽事設定頁 狀態:關閉");
            afterClosed?.Invoke();
            timer.Stop();
            timer.Close();
        }

        //視窗名稱變動事件
        private void FrmMatch_TextChanged(object sender, EventArgs e)
        {
            this.TextChanged -= FrmMatch_TextChanged;
            this.Text += $"({YwCore.Common.GetApplicationName()})";
            this.TextChanged += FrmMatch_TextChanged;
        }

        //UI複製GID事件
        private void MenuCopyGid_Click(object sender, EventArgs e)
        {
            if (Config.gameDic.TryGetValue(tvMatch.SelectedNode.Name, out GameInfo gameInfo))
            {
                Clipboard.SetData(DataFormats.Text, gameInfo.GId.ToString());
            }
        }

        //新增至排除
        private void MenuAddExclude_Click(object sender, EventArgs e)
        {
            if (Config.gameDic.TryGetValue(tvMatch.SelectedNode.Name, out GameInfo gameInfo))
            {
                if (gameInfo.GId > 0)
                {
                    FrmExclude.AddExclude(gameInfo.GId, gameInfo.League.Name, gameInfo.Suffix.Name, gameInfo.TeamA.Name, gameInfo.TeamB.Name);
                    LogFile.SetLog(LogMode.Control, $"新增至排除關閉賽事 : {gameInfo.GId}");
                }
            }
        }

        //修改玩法資訊事件
        private void MenuItemPlay_Click(object sender, EventArgs e)
        {
            //日誌記錄操作
            LogFile.SetLog(LogMode.Control, $"點擊修改玩法資訊 : {tvMatch.SelectedNode.FullPath}");

            GameInfo game = Config.gameDic.SingleOrDefault(a => a.Key == tvMatch.SelectedNode.Parent.Parent.Name).Value;
            KInfo kInfo = game?.ObjDic.SingleOrDefault(a => a.Key.ToString() == tvMatch.SelectedNode.Parent.Name).Value?.SingleOrDefault(a => a.Key.ToString() == tvMatch.SelectedNode.Name).Value;

            if (kInfo != null)
            {
                FrmOddsTest frmOddsTest = new FrmOddsTest($"{tvMatch.SelectedNode.Parent.Parent.Parent.Text}\n{tvMatch.SelectedNode.Parent.Parent.Text}\nGPDM:{tvMatch.SelectedNode.Parent.Name}, {kInfo.GroupText}-{kInfo.PlayText}", kInfo);
                frmOddsTest.ShowDialog();
            }

            SetTreeView(tvMatch, Config.gameDic);
        }

        //清除模擬測試事件
        private void MenuItemClearTest_Click(object sender, EventArgs e)
        {
            //日誌記錄操作
            LogFile.SetLog(LogMode.Control, $"點擊清除模擬測試 : {tvMatch.SelectedNode.FullPath}");

            IEnumerable<KInfo> kInfos = new KInfo[0];
            IEnumerable<GameInfo> gInfos = new GameInfo[0];

            switch (tvMatch.SelectedNode.Level)
            {
                case 0:
                    gInfos = Config.gameDic.Values.Where(a => a.GpType.ToString("d") == tvMatch.SelectedNode.Name);
                    kInfos = gInfos.SelectMany(a => a.ObjDic.Values).SelectMany(a => a.Values);
                    break;
                case 1:
                    var firstGame = Config.gameDic.Values.FirstOrDefault(a => tvMatch.SelectedNode.FirstNode.Name == a.GetGameDictKey());
                    gInfos = Config.gameDic.Values.Where(a => a.GpType.ToString("d") == tvMatch.SelectedNode.Parent.Name && firstGame.League.Name == a.League.Name && firstGame.Suffix.Name == a.Suffix.Name);
                    kInfos = gInfos.SelectMany(a => a.ObjDic.Values).SelectMany(a => a.Values);
                    break;
                case 2:
                    gInfos = Config.gameDic.Values.Where(a => a.GetGameDictKey() == tvMatch.SelectedNode.Name);
                    kInfos = gInfos.SelectMany(a => a.ObjDic.Values).SelectMany(a => a.Values);
                    break;
                case 3:
                    kInfos = Config.gameDic.SingleOrDefault(a => a.Key == tvMatch.SelectedNode.Parent.Name).Value?.ObjDic.SingleOrDefault(a => a.Key.ToString() == tvMatch.SelectedNode.Name).Value?.Values;
                    break;
            }

            //foreach (GameInfo game in gInfos)
            //{
            //    game.IsTest = false;
            //    game.IsUpdate = true;
            //}

            foreach (KInfo kInfo in kInfos)
            {
                kInfo.IsTest = false;
                kInfo.IsUpdate = true;
            }

            SetTreeView(tvMatch, Config.gameDic);
        }

        /// <summary>
        /// 取出字串最後一碼
        /// </summary>
        private string GetLastCode(string _str)
        {
            _str = _str.Substring(_str.Length - 1, 1);
            return _str;
        }

        //勾選事件
        private void TvMatch_AfterCheck(object sender, TreeViewEventArgs e)
        {
            LogFile.SetLog(LogMode.Control, $"{e.Node.FullPath} : {e.Node.Checked}");

            switch (e.Node.Level)
            {
                //聯盟層
                case 1:
                    //勾選為要GP
                    if (e.Node.Checked)
                    {
                        Config.BlockList.RemoveLeague(e.Node.Name);
                    }
                    else
                    {
                        Config.BlockList.AddLeague(e.Node.Name);
                    }

                    //連動內層觸發事件
                    foreach (TreeNode node in e.Node.Nodes)
                    {
                        node.Checked = e.Node.Checked;
                    }

                    break;
                //賽事層
                case 2:
                    //勾選為要GP
                    if (e.Node.Checked)
                    {
                        Config.BlockList.RemoveGame(e.Node.Name);

                        //聯盟層連動
                        if (Config.BlockList.RemoveLeague(e.Node.Parent.Name))
                        {
                            tvMatch.AfterCheck -= TvMatch_AfterCheck;
                            e.Node.Parent.Checked = e.Node.Checked;
                            tvMatch.AfterCheck += TvMatch_AfterCheck;

                            //其它賽事需排除
                            foreach (TreeNode gameNode in e.Node.Parent.Nodes)
                            {
                                if (gameNode != e.Node && !gameNode.Checked)
                                {
                                    Config.BlockList.AddGame(gameNode.Name);
                                }
                            }
                        }
                    }
                    else
                    {
                        //外層有勾上才需排除賽事
                        if (e.Node.Parent.Checked)
                        {
                            Config.BlockList.AddGame(e.Node.Name);
                        }
                        else
                        {
                            Config.BlockList.RemoveGame(e.Node.Name);
                        }
                    }

                    //連動內層觸發事件
                    foreach (TreeNode node in e.Node.Nodes)
                    {
                        node.Checked = e.Node.Checked;
                    }

                    break;
                //GPDM玩法層
                case 3:
                    if (decimal.TryParse(e.Node.Name, out decimal gpdm))
                    {
                        var filter = Config.BlockList.GetOrAddGpdmList(e.Node.Parent.Name, new BlockGpdmList());

                        //勾選為要GP
                        if (e.Node.Checked)
                        {
                            filter.Remove(gpdm);

                            tvMatch.AfterCheck -= TvMatch_AfterCheck;
                            e.Node.Parent.Parent.Checked = e.Node.Parent.Checked = e.Node.Checked;
                            tvMatch.AfterCheck += TvMatch_AfterCheck;

                            //賽事層連動
                            if (Config.BlockList.RemoveGame(e.Node.Parent.Name))
                            {
                                //排除其它玩法
                                foreach (TreeNode playNode in e.Node.Parent.Nodes)
                                {
                                    if (decimal.TryParse(playNode.Name, out decimal filterPlay) && playNode != e.Node && !playNode.Checked)
                                    {
                                        filter.Add(filterPlay);
                                    }
                                }
                            }

                            //聯盟層連動
                            if (Config.BlockList.RemoveLeague(e.Node.Parent.Parent.Name))
                            {
                                foreach (TreeNode gameNode in e.Node.Parent.Parent.Nodes)
                                {
                                    //排除其它賽事
                                    if (gameNode != e.Node.Parent)
                                    {
                                        if (!gameNode.Checked)
                                        {
                                            Config.BlockList.AddGame(gameNode.Name);
                                        }
                                    }
                                    else
                                    {
                                        //排除其它玩法
                                        foreach (TreeNode playNode in gameNode.Nodes)
                                        {
                                            if (decimal.TryParse(playNode.Name, out decimal filterPlay) && playNode != e.Node && !playNode.Checked)
                                            {
                                                filter.Add(filterPlay);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            //外層有勾上才需排除玩法
                            if (e.Node.Parent.Checked)
                            {
                                filter.Add(gpdm);
                            }
                            else
                            {
                                filter.Remove(gpdm);
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 勾除鴛鴦盤
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiBlockSameGameLine_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            BlockGpdmList list = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Name, new BlockGpdmList());
            list.BlockGameLine = menuItem.Checked;
        }

        /// <summary>
        /// 勾除Q2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiBlockQ2_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            BlockGpdmList list = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Name, new BlockGpdmList());
            list.BlockQ2 = menuItem.Checked;
        }

        /// <summary>
        /// 勾除Q4
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiBlockQ4_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            BlockGpdmList list = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Name, new BlockGpdmList());
            list.BlockQ4 = menuItem.Checked;
        }

        /// <summary>
        /// 勾除下半
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiBlock2Half_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            BlockGpdmList list = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Name, new BlockGpdmList());
            list.Block2Half = menuItem.Checked;
        }

        /// <summary>
        /// 選單彈出(勾除功能顯示初始化)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuBlock_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (tvMatch.SelectedNode.Parent != null)
            {
                BlockGpdmList parentList = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Parent.Name, new BlockGpdmList());

                if ((parentList.BlockGameLine || parentList.Block2Half || parentList.BlockQ2 || parentList.BlockQ4))
                {
                    miBlock2HalfLeague.Enabled = false;
                    miBlockGameLineLeague.Enabled = false;
                    miBlockQ4League.Enabled = false;
                    miBlockQ2League.Enabled = false;
                }
                else
                {
                    miBlock2HalfLeague.Enabled = true;
                    miBlockGameLineLeague.Enabled = true;
                    miBlockQ4League.Enabled = true;
                    miBlockQ2League.Enabled = true;
                }
            }

            BlockGpdmList list = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Name, new BlockGpdmList());
            ContextMenuStrip menu = (ContextMenuStrip)sender;

            foreach (var item in menu.Items)
            {
                ToolStripMenuItem funcItem = item as ToolStripMenuItem;

                if (funcItem != null && funcItem.CheckOnClick)
                {
                    if (funcItem.Name.Contains(nameof(list.BlockQ2)))
                    {
                        funcItem.Checked = list.BlockQ2;
                    }
                    else if (funcItem.Name.Contains(nameof(list.BlockQ4)))
                    {
                        funcItem.Checked = list.BlockQ4;
                    }
                    else if (funcItem.Name.Contains(nameof(list.Block2Half)))
                    {
                        funcItem.Checked = list.Block2Half;
                    }
                    else if (funcItem.Name.Contains(nameof(list.BlockGameLine)))
                    {
                        funcItem.Checked = list.BlockGameLine;
                    }
                    else if (funcItem.Name.Contains(nameof(list.UniqueControl)))
                    {
                        funcItem.Checked = list.UniqueControl;

                        if (funcItem.Checked)
                        {
                            miBlock2HalfLeague.Enabled = true;
                            miBlockGameLineLeague.Enabled = true;
                            miBlockQ4League.Enabled = true;
                            miBlockQ2League.Enabled = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 獨立控制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiUniqueControlLeague_CheckedChanged(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            BlockGpdmList list = Config.BlockList.GetOrAddGpdmList(tvMatch.SelectedNode.Name, new BlockGpdmList());
            list.UniqueControl = menuItem.Checked;
        }

        /// <summary>
        /// 複製聯盟名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiCopyLeague_Click(object sender, EventArgs e)
        {
            if (Config.gameDic.TryGetValue(tvMatch.SelectedNode.FirstNode.Name, out GameInfo gameInfo))
            {
                Clipboard.SetData(DataFormats.Text, gameInfo.League.Name);
            }
        }

        /// <summary>
        /// 複製來源ID
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiCopySourceId_Click(object sender, EventArgs e)
        {
            if (Config.gameDic.TryGetValue(tvMatch.SelectedNode.Name, out GameInfo gameInfo))
            {
                Clipboard.SetData(DataFormats.Text, gameInfo.SourceId);
            }
        }

        /// <summary>
        /// 複製隊伍名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MiCopyTeam_Click(object sender, EventArgs e)
        {
            if (Config.gameDic.TryGetValue(tvMatch.SelectedNode.Name, out GameInfo gameInfo))
            {
                Clipboard.SetData(DataFormats.Text, $"{gameInfo.TeamA.Name} vs {gameInfo.TeamB.Name}");
            }
        }
    }
}