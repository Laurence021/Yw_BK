#region 功能修改描述與時間

/*
    描述：條件式提醒，玩法都沒移出時不需發送MT
    日期：2026-05-08
*/

#endregion

using YwCore;

namespace Yw_BK.model
{
    /// <summary>
    /// 條件式提醒
    /// </summary>
    public class ConditionalRemindInfo : IRemind
    {
        //原提醒物件
        private readonly RemindInfo _info;
        /// <summary>
        /// 提醒類型
        /// </summary>
        public string RemindType { get; private set; }
        /// <summary>
        /// 關聯提醒物件
        /// </summary>
        public IRemind? RelationRemind { get; }

        /// <summary>
        /// 是否已觸發ZD脫勾關小格
        /// </summary>
        public bool IsZdClosed
        {
            get => _info.IsZdClosed;
            set => _info.IsZdClosed = value;
        }

        /// <summary>
        /// 條件發送MT
        /// </summary>
        private Func<bool> _conditionSend;

        /// <summary>
        /// 取得開關
        /// </summary>
        public bool GetEnabled() => _info.GetEnabled();

        /// <summary>
        /// 設置最後更新時間
        /// </summary>
        public void SetLastTime(DateTime time) => _info.SetLastTime(time);

        /// <summary>
        /// 取得最後更新時間
        /// </summary>
        public DateTime GetLastTime() => _info.GetLastTime();

        /// <summary>
        /// 設置紀錄前一次發送的訊息
        /// </summary>
        /// <param name="msg"></param>
        public void SetFlag(string msg) => _info.SetFlag(msg);

        /// <summary>
        /// 設置紀錄前一次發送的訊息
        /// </summary>
        /// <param name="state">1:一般超時提醒 2:超時未獲取進行關閉 4:恢復</param>
        public void SetFlag(int state) => _info.SetFlag(state);

        /// <summary>
        /// 取得對應訊息
        /// </summary>
        /// <param name="remindMode">1:一般超時提醒 4:恢復</param>
        public string GetMessage(RemindMode remindMode) => _info.GetMessage(remindMode);

        /// <summary>
        /// 設置發送MT訊息
        /// </summary>
        /// <param name="remindMode">1:一般超時提醒  2:超時未獲取進行關閉 4:恢復</param>
        public bool SetSendMessage(RemindMode remindMode)
        {
            if (_conditionSend())
            {
                return _info.SetSendMessage(remindMode);
            }

            return false;
        }

        /// <summary>
        /// 取得前一次發送的訊息 (1:一般超時提醒  2:超時未獲取進行關閉 4:恢復)
        /// </summary>
        public int GetSendFlag() => _info.GetSendFlag();

        /// <summary>
        /// 重整Enabled開關
        /// </summary>
        /// <returns></returns>
        public void RefreshEnabled() => _info.RefreshEnabled();

        /// <summary>
        /// 是否超時達標
        /// </summary>
        /// <param name="signalTime">事件觸發時間</param>
        /// <param name="state">狀態值 1:一般超時提醒 </param>
        public bool IsTimeout(DateTime signalTime, out int state) => _info.IsTimeout(signalTime, out state);

        /// <summary>
        /// 建構子
        /// </summary>
        /// <param name="remindType">提醒類型</param>
        /// <param name="alertMsg">警告訊息</param>
        /// <param name="recoverMsg">恢復訊息</param>
        /// <param name="lastTime">最後更新時間</param>
        /// <param name="timeout">超時設定(秒)</param>
        /// <param name="setEnabled">設定開關動作</param>
        /// <param name="sendCondition">發送MT條件</param>
        /// <param name="relationRemind">關聯提醒物件</param>
        public ConditionalRemindInfo(string remindType, string alertMsg, string recoverMsg, DateTime lastTime, int timeout, Func<bool> setEnabled, Func<bool> sendCondition, IRemind? relationRemind = default)
        {
            RemindType = remindType;
            _info = new RemindInfo(remindType, alertMsg, recoverMsg, lastTime, timeout, setEnabled, relationRemind);
            RelationRemind = relationRemind;
            _conditionSend = sendCondition;
            RefreshEnabled();
        }
    }
}
