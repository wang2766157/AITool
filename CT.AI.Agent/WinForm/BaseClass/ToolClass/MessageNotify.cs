
namespace FormMessageNotify;

///<summary>   
///   <para>当信息更改是通知其他的窗口重新加载数据   </para>
///   <para>使用方法为：</para>
///   <para>1）通知信息更改(在更改的窗口调用)   
///   MessageNotify.Instance().SendMessage(NotifyInfo.InfoAdd,"提示消息");   
///   其中第一个参数为信息号，第二个参数为信息描述 </para>  
///   <para>2）收取信息(在另一个窗口中)
///   使用方法，在每个在构造函数中加入如下语句   
///    MessageNotify.Instance().OnMsgNotifyEvent += OnNotifyEvent; </para>
///  
/// <para>同时编写如下的方法用于重新加载数据 </para>  
/// <para>protected  void  OnNotifyEvent(object sender,MessageNotify.NotifyEventArgs e)  </para> 
/// <para>{</para>   
/// <para>     if   (e.Code == MessageNotity.NotifyInfo.InfoAdd) </para>  
/// <para>     {</para>
/// <para>          //控件数据重新绑定</para>
/// <para>     }</para>
/// <para>}   </para>
///</summary>   
public class MessageNotify
{
    #region 成员
    /// <summary>
    /// 消息自身实例
    /// </summary>
    private static MessageNotify _mNotify = null;

    /// <summary>
    /// 消息委托事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void MsgNotifyEvent(object sender, NotifyEventArgs e);

    /// <summary>
    /// 消息事件对象
    /// </summary>
    public event MsgNotifyEvent OnMsgNotifyEvent;
    #endregion

    /// <summary>
    /// 获得自身实例,单例实现
    /// </summary>
    /// <returns></returns>
    public static MessageNotify Instance()
    {
        if (_mNotify == null)
        {
            _mNotify = new MessageNotify();
        }
        return _mNotify;
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    public void SendMessage(NotifyInfo code, string message)
    {
        NotifyEventArgs e = new NotifyEventArgs(code, message);
        if (OnMsgNotifyEvent != null)
        {
            OnMsgNotifyEvent(this, e);
        }
    }

    /// <summary>
    /// 更新消息事件
    /// </summary>
    public class NotifyEventArgs : System.EventArgs
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="code">举例更新</param>
        /// <param name="message">消息内容</param>
        public NotifyEventArgs(NotifyInfo code, string message)
        {
            _mNCode = code;
            _mStrMessage = message;
        }

        #region 属性
        private NotifyInfo _mNCode;
        private string _mStrMessage = string.Empty;

        /// <summary>
        /// 消息更新区域类型
        /// </summary>
        public NotifyInfo Code
        {
            get { return _mNCode; }
            set { _mNCode = value; }
        }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message
        {
            get { return _mStrMessage; }
            set
            {
                _mStrMessage = value;
                if (_mStrMessage == null)
                {
                    _mStrMessage = string.Empty;
                }
            }
        }
        #endregion

    }

    #region 各种更新信号枚举
    /// <summary>
    /// 各种更新信号枚举
    /// </summary>
    public enum NotifyInfo
    {
        /// <summary>
        /// 显示窗口
        /// </summary>
        InfoShow,
        /// <summary>
        /// 添加时发生的消息
        /// </summary>
        InfoAdd,
        /// <summary>
        /// 修改时发生的消息
        /// </summary>
        InfoEidt,
        /// <summary>
        /// 删除发生的消息
        /// </summary>
        InfoDelete,
    }
    #endregion
}
