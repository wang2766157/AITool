using FormMessageNotify;
using HistoryBufferNs;
using System.ComponentModel;
using System.Data;
using System.Reflection;

namespace BaseFormNs;
public class BaseForm : Form
{
    //==================纠结的分割线==================//
    //基本
    #region 构造&&成员
    public BaseForm()
    {
        StartPosition = FormStartPosition.CenterScreen;
        Icon = CT.AI.Agent.Properties.Resources.Spell_Holy_MagicalSentry;
        MessageNotify.Instance().OnMsgNotifyEvent += OnNotifyEvent;
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.DoubleBuffer, true);
    }
    #endregion
    #region Load事件
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        //HistoryBufferSetting();
    }
    #endregion
    #region 窗体关闭事件
    protected override void OnClosing(CancelEventArgs e)
    {
        //HistoryBufferSave();
        // 仅将页面隐藏
        //Hide();
        //e.Cancel = true;
    }
    #endregion
    #region 窗体状态改变
    public void FormWinStateChanged(Form f)
    {
        if (f.WindowState==FormWindowState.Maximized)
        {
            f.WindowState = FormWindowState.Normal;
        } 
        else
        {
            f.MaximumSize = new Size(Screen.PrimaryScreen.WorkingArea.Width, Screen.PrimaryScreen.WorkingArea.Height);
            f.WindowState = FormWindowState.Maximized;
        }
    }
    #endregion
    #region 消息通信事件
    protected void OnNotifyEvent(object sender, MessageNotify.NotifyEventArgs e)
    {
        if (e.Code == MessageNotify.NotifyInfo.InfoShow && e.Message == Name)
        {
            Text = e.Message;
            Show();
        }
        else if (e.Code == MessageNotify.NotifyInfo.InfoAdd && e.Message == Name)
        {

        }
        else if (e.Code == MessageNotify.NotifyInfo.InfoEidt && e.Message == Name)
        {

        }
        else if (e.Code == MessageNotify.NotifyInfo.InfoDelete && e.Message == Name)
        {

        }
    }
    #endregion
    #region 控件自动排列
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentCon"></param>
    /// <param name="clist"></param>
    /// <param name="side"></param>
    /// <param name="delta"></param>
    public void AutoLayout(Control parentCon,List<Control> clist , int side, int delta = 3)
    {
        parentCon.Controls.Clear();

        int sum = clist.Count;
        int pcw = parentCon.ClientSize.Width;

        int deltaX = ((pcw + delta) % (side + delta)) / 2;
        int deltaY = 13;

        int x = deltaX;
        for (int i = 0; i < sum; i++)
        {
            clist[i].Left = deltaX;
            clist[i].Top = deltaY;
            deltaX += clist[i].Width + delta;
            if (clist[i].Width > pcw - deltaX)
            {
                deltaX = delta + x;
                deltaY += clist[i].Height + delta;
            }
            clist[i].Parent = parentCon;
        }
    }
    #endregion
    //==================纠结的分割线==================//
    //控件
    #region TextBoxFocus
    public void TextBoxFocus(TextBox tb, string test)
    {
        tb.GotFocus += TextBoxGotFocus;
        tb.LostFocus += TextBoxLostFocus;
        tb.Tag = test;
        //初始化
        tb.Text = test;
        tb.ForeColor = Color.Silver;
    }
    private void TextBoxGotFocus(object sender, EventArgs e)
    {
        TextBox tb = (TextBox)sender;
        if (tb.Tag != null)
        {
            string tar = tb.Tag.ToString();
            if (tb.Text == tar)
            {
                tb.Text = "";
                tb.ForeColor = Color.Black;
            }
        }
    }

    private void TextBoxLostFocus(object sender, EventArgs e)
    {
        TextBox tb = (TextBox)sender;
        if (tb.Tag != null)
        {
            string tar = tb.Tag.ToString();
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = tar;
                tb.ForeColor = Color.Silver;
            }
        }
    }
    #endregion
    #region Button
    /// <summary>
    /// 无焦点按钮（也可控制复选框，单选框）
    /// </summary>
    /// <param name="btn"></param>
    public void ControlStyle(Button btn)
    {
        MethodInfo mhd = btn.GetType().GetMethod("SetStyle",BindingFlags.NonPublic|
            BindingFlags.Instance|BindingFlags.InvokeMethod);
        mhd.Invoke(btn, BindingFlags.NonPublic | 
            BindingFlags.Instance | BindingFlags.InvokeMethod,null,new object[]
        {
            ControlStyles.Selectable,false
        },
        Application.CurrentCulture);
    }

    /// <summary>
    /// 无焦点按钮（也可控制复选框，单选框）
    /// </summary>
    /// <param name="btns"></param>
    public void ControlStyle(Button[] btns)
    {
        for (int i = 0; i < btns.Length; i++)
        {
            ControlStyle(btns[i]);
        }
    }

    public void ControlStyle(Control ctl)
    {
        foreach (Control c in ctl.Controls)
        {   
            if (c.GetType()==typeof(Button))
            {
                ControlStyle((Button)c);
            }
            if (c.Controls.Count > 0)
            {
                ControlStyle(c);
            }
        }
    }

    #endregion
    #region dataGridView
    #region 绑定列 SetGridViewColumn

    #region 绑定列GridViewColumn
    /// <summary>
    /// 绑定datagridview 的值列
    /// </summary>
    /// <param name="dgv"></param>
    /// <param name="disStr"></param>
    /// <param name="valStr"></param>
    /// <param name="width"></param>
    public void SetGridViewColumn(DataGridView dgv, string disStr, string valStr, int width = 100)
    {
        if (!dgv.Columns.Contains(valStr))
        {
            DataGridViewTextBoxColumn dgvc = new DataGridViewTextBoxColumn();
            dgvc.Name = valStr;
            dgvc.Width = width;
            dgvc.HeaderText = disStr;
            dgvc.DataPropertyName = valStr;
            dgvc.SortMode = DataGridViewColumnSortMode.NotSortable;
            dgv.Columns.Add(dgvc);
        }
    }
    #endregion

    #region 绑定CheckBoxColumn
    /// <summary>
    /// 绑定CheckBoxColumn
    /// </summary>
    /// <param name="dgv"></param>
    /// <param name="strHeader"></param>
    /// <param name="strValue"></param>
    /// <param name="width"></param>
    public void SetCheckGridViewColumn(DataGridView dgv, string strHeader, string strValue, int width = 50)
    {
        if (!dgv.Columns.Contains(strValue))
        {
            DataGridViewCheckBoxColumn dgvc = new DataGridViewCheckBoxColumn();
            dgvc.Name = strValue;
            dgvc.Width = width;
            dgvc.HeaderText = strHeader;
            dgvc.DataPropertyName = strValue;
            dgvc.SortMode = DataGridViewColumnSortMode.NotSortable;
            dgv.Columns.Add(dgvc);
        }
    }
    #endregion

    #region  绑定ComboBoxColumn
    /// <summary>
    /// 绑定GridView ComboBox列
    /// </summary>
    /// <param name="dgv"></param>
    /// <param name="disStr"></param>
    /// <param name="header"></param>
    /// <param name="dt">ComboBox数据源</param>
    /// <param name="diss">ComboBox显示列</param>
    /// <param name="vals">ComboBox值列</param>
    /// <param name="width"></param>
    public void SetComboBoxGridViewColumn(DataGridView dgv, string disStr, string header, DataTable dt, string diss, string vals, int width=100)
    {
        if (!dgv.Columns.Contains(disStr))
        {
            DataGridViewComboBoxColumn dgvc = new DataGridViewComboBoxColumn();
            dgvc.Width = width;
            dgvc.Name = disStr;
            dgvc.HeaderText = header;
            dgvc.DataSource = dt;
            dgvc.DisplayMember = diss;
            dgvc.ValueMember = vals;
            dgvc.SortMode = DataGridViewColumnSortMode.NotSortable;
            dgv.Columns.Add(dgvc);
        }
    }
    #endregion

    #region 绑定ButtonColumn
    /// <summary>
    /// ButtonColumn
    /// </summary>
    /// <param name="dgv"></param>
    /// <param name="nameStr"></param>
    /// <param name="headerStr"></param>
    /// <param name="textStr"></param>
    /// <param name="width"></param>
    public void SetButtonGridViewColumn(DataGridView dgv, string nameStr, string headerStr, string textStr, int width = 100)
    {
        if (!dgv.Columns.Contains(nameStr))
        {
            DataGridViewButtonColumn dgvc = new DataGridViewButtonColumn();
            dgvc.Width = width;
            dgvc.Name = nameStr;
            dgvc.UseColumnTextForButtonValue = true;
            dgvc.HeaderText = headerStr;
            dgvc.Text = textStr;
            dgvc.SortMode = DataGridViewColumnSortMode.NotSortable;
            dgv.Columns.Add(dgvc);
        }
    }
    #endregion

    #region 绑定LinkButtonColumn
    /// <summary>
    /// linkbutton列 //[2015-2-5 王志刚] 
    /// </summary>
    /// <param name="dgv"></param>
    /// <param name="nameStr"></param>
    /// <param name="headerStr"></param>
    /// <param name="valStr">当值域为空时默认绑定nameStr的值</param>
    /// <param name="width"></param>
    public void SetLinkButtonGridViewColumn(DataGridView dgv, string nameStr, string headerStr, string valStr = "", int width = 100)
    {
        if (!dgv.Columns.Contains(nameStr))
        {
            DataGridViewLinkColumn dgvc = new DataGridViewLinkColumn();
            dgvc.Width = width;
            dgvc.Name = nameStr;
            dgvc.HeaderText = headerStr;
            if (!string.IsNullOrEmpty(valStr))
            {
                dgvc.DataPropertyName = valStr;
            }
            else
            {
                dgvc.DataPropertyName = nameStr;
            }
            dgvc.SortMode = DataGridViewColumnSortMode.NotSortable;
            dgv.Columns.Add(dgvc);
        }
    }
    #endregion

    #endregion
    #region datagridview样式

    public DataGridViewCellStyle DgvRowStyleNormal;
    public DataGridViewCellStyle DgvRowStyleAlternate;
    public DataGridViewCellStyle DgvHeaderStyle;

    #region 初始化
    /// <summary>
    /// datagirdview样式初始化
    /// </summary>
    /// <param name="dgv"></param>
    public void GridStart(DataGridView dgv)
    {
        dgv.ReadOnly = true;
        GridStartadle(dgv);
    }

    /// <summary>
    /// datagirdview样式初始化 ( 可编辑）
    /// </summary>
    /// <param name="dgv"></param>
    public void GridStartadle(DataGridView dgv)
    {
        SetRowStyle();
        //基本信息
        dgv.AutoGenerateColumns = false;
        dgv.BackgroundColor = Color.White;
        dgv.Font = new Font("微软雅黑", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 134);
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.AllowUserToAddRows = false;
        dgv.EnableHeadersVisualStyles = false;
        dgv.BorderStyle = BorderStyle.None;
        dgv.GridColor = Color.FromArgb(138, 195, 234);
        dgv.VirtualMode = true;
        //列信息
        dgv.ColumnHeadersHeight = 30;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.ColumnHeadersDefaultCellStyle = DgvHeaderStyle;
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        //单元格
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        //行信息
        dgv.RowTemplate.Height = 30;
        dgv.RowHeadersVisible = false;
        //事件
        dgv.RowPostPaint += DgvRowPostPaint;
    }
    #endregion

    #region 设定行样式
    /// <summary>
    /// 设定行样式
    /// </summary>
    public void SetRowStyle()
    {
        DgvRowStyleNormal = new DataGridViewCellStyle();
        DgvRowStyleNormal.BackColor = Color.FromArgb(255, 255, 255);
        DgvRowStyleNormal.SelectionBackColor = Color.FromArgb(219, 243, 255);
        DgvRowStyleNormal.SelectionForeColor = Color.FromArgb(0, 0, 0);

        DgvRowStyleAlternate = new DataGridViewCellStyle();
        DgvRowStyleAlternate.BackColor = Color.FromArgb(240, 240, 240);
        DgvRowStyleAlternate.SelectionBackColor = Color.FromArgb(219, 243, 255);
        DgvRowStyleAlternate.SelectionForeColor = Color.FromArgb(0, 0, 0);

        DgvHeaderStyle = new DataGridViewCellStyle();
        DgvHeaderStyle.BackColor = Color.FromArgb(77, 192, 255);
        DgvHeaderStyle.ForeColor = Color.FromArgb(255, 255, 255);

    }
    #endregion

    #region 行样式绑定
    /// <summary>
    /// 行样式绑定
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void DgvRowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (sender is DataGridView)
        {
            DataGridView dgv = (DataGridView)sender;
            //行首显示行编号
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X,
                  e.RowBounds.Location.Y,
                  dgv.RowHeadersWidth - 4,
                  e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                dgv.RowHeadersDefaultCellStyle.Font,
                rectangle,
                dgv.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

            if ((e.RowIndex + 1) % 2 == 0)//双倍行
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle = DgvRowStyleNormal;
            }
            if ((e.RowIndex + 1) % 2 == 1)//单倍行
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle = DgvRowStyleAlternate;
            }
        }
    }
    #endregion

    #region 日期格式验证
    /// <summary>
    /// 日期格式验证
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <param name="colName"></param>
    public void CellFormatToDate(object sender, DataGridViewCellFormattingEventArgs e, string colName)
    {
        if (e != null && sender != null)
        {
            DataGridView dgv = (DataGridView)sender;
            object obj = e.Value;

            if (dgv.Columns[e.ColumnIndex].DataPropertyName == colName)
            {
                if (obj != null)
                {
                    DateTime dt = new DateTime();
                    bool flag = DateTime.TryParse(obj.ToString(), out dt);
                    if (flag)
                    {
                        e.Value = Convert.ToDateTime(obj).ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        e.Value = "";
                    }
                }
                else
                {
                    e.Value = "";
                }
            }
        }
    }
#endregion

    #endregion
    #endregion
    #region 关闭时保存输入框文本
    //关闭时保存输入框文本
    public List<TextBox> TbList = new List<TextBox>();
    /// <summary>
    /// 保存输入框文本
    /// </summary>
    public void HistoryBufferSave()
    {
        HistoryBuffer hb = new HistoryBuffer();
        List<BufferModel> mlist = (List<BufferModel>)hb.ReadInfo();
        mlist = hb.AddModelList(mlist, TbList.ToArray());
        hb.WriteInfo(mlist);
    }

    /// <summary>
    /// 读取输入框文本
    /// </summary>
    public void HistoryBufferSetting()
    {
        HistoryBuffer hb = new HistoryBuffer();
        List<BufferModel> mlist = (List<BufferModel>)hb.ReadInfo();
        hb.SettingModel(mlist, TbList);
    }

    #endregion
    //==================纠结的分割线==================//
    //事件
    #region 鼠标滚动菜单

    private Panel _tarPanel;
    private void BaseForm_MouseWheel(object sender, MouseEventArgs e)
    {
        if (_tarPanel != null)
        {
            //获取光标位置
            Point mousePoint = new Point(e.X, e.Y);
            //换算成相对本窗体的位置
            mousePoint.Offset(Location.X, Location.Y);
            //判断是否在panel内
            if (_tarPanel.RectangleToScreen(_tarPanel.DisplayRectangle).Contains(mousePoint))
            {
                //滚动
                _tarPanel.AutoScrollPosition = new Point(0, _tarPanel.VerticalScroll.Value - e.Delta);
            }
        }
    }

    private void Panel_MouseClick(object sender, MouseEventArgs e)
    {
        Panel a = (Panel)sender;
        a.Focus();
    }
    /// <summary>
    /// 鼠标滚动菜单
    /// </summary>
    /// <param name="tar"></param>
    public void SetPanelMouseWheel(Panel tar)
    {
        _tarPanel = tar;
        _tarPanel.MouseWheel += BaseForm_MouseWheel;
        _tarPanel.MouseClick += Panel_MouseClick;
    }

    #endregion
    //==================纠结的分割线==================//
    //构造窗口
    #region  反射 创建窗口

    /// <summary>
    /// //[2014-8-8 王志刚]   
    /// 反射 动态创建窗口
    /// </summary>
    /// <param name="formType"></param>
    /// <returns></returns>
    public BaseForm GetWinForm(string formType)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        BaseForm f = assembly.CreateInstance(formType) as BaseForm;
        return f;
    }

    /// <summary>
    /// //[2014-10-20 王志刚]   
    /// 反射 动态创建窗口
    /// </summary>
    /// <param name="formType"></param>
    /// <param name="param">参数队列</param>
    /// <returns></returns>
    public BaseForm GetWinForm(string formType, object[] param)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        BaseForm f = assembly.CreateInstance(formType, false, BindingFlags.Default, null, param, null, null) as BaseForm;
        return f;
    }
    #endregion
    #region ShowForm 反射模式生成form

    /// <summary>
    /// ShowForm反射模式生成form 放到指定的主窗体位置
    /// <para>Form:BaseForm</para>
    /// </summary>
    /// <param name="pnl"></param>
    /// <param name="bf"></param>
    public void ShowForm(Panel pnl, BaseForm bf)
    {
        int t = pnl.Controls.Count;
        for (int i = 0; i < t; i++)
        {
            if (pnl.Controls[i] is BaseForm)
            {
                BaseForm o = (BaseForm) pnl.Controls[i];
                o.Close();
                o.Dispose();
            }
            else
            {
                pnl.Controls[i].Dispose();
            }
        }

        bf.TopLevel = false;
        bf.FormBorderStyle = FormBorderStyle.None;
        bf.Dock = DockStyle.Fill;
        pnl.Controls.Add(bf);
        bf.Show();
    }

    /// <summary>
    /// ShowFormAsDialog反射模式生成form：以对话框的形式
    /// </summary>
    /// <param name="bf"></param>
    public void ShowFormAsDialog(BaseForm bf)
    {
        bf.StartPosition = FormStartPosition.CenterScreen;
        bf.ShowIcon = false;
        bf.ShowInTaskbar = false;
        bf.ShowDialog();
    }

    /// <summary>
    /// ShowFormAsDialog反射模式生成form：以对话框的形式
    /// </summary>
    /// <param name="bf"></param>
    public void ShowFormAsDialogNo(BaseForm bf)
    {
        bf.FormBorderStyle = FormBorderStyle.None;
        bf.MaximizeBox = false;

        ShowFormAsDialog(bf);
    }
    #endregion
    
    //==================纠结的分割线==================//
    //其他
    #region To
    public int ToInt(object o)
    {
        int result = 0;
        if (o != null)
        {
            int.TryParse(o.ToString(), out result);
        }
        return result;
    }
    #endregion
    //==================纠结的分割线==================//
}
