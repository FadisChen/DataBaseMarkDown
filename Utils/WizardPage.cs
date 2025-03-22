using System;
using System.Windows.Forms;

namespace DataBaseMarkDown.Utils
{
    /// <summary>
    /// 嚮導頁面基礎類，所有嚮導頁面都繼承自此類
    /// </summary>
    public class WizardPage : UserControl
    {
        // 前一頁事件
        public event EventHandler? PreviousRequested;
        
        // 下一頁事件
        public event EventHandler? NextRequested;
        
        // 完成事件
        public event EventHandler? FinishRequested;

        // 頁面標題
        public string PageTitle { get; set; } = "嚮導頁面";
        
        // 頁面是否有上一步
        public bool HasPrevious { get; set; } = true;
        
        // 頁面是否有下一步
        public bool HasNext { get; set; } = true;
        
        // 頁面是否可以完成
        public bool CanFinish { get; set; } = false;

        // 頁面是否有效（可進行下一步）
        public virtual bool IsValid()
        {
            return true;
        }

        // 頁面激活時調用
        public virtual void OnActivated() { }

        // 頁面請求上一頁
        protected void OnPreviousRequested()
        {
            PreviousRequested?.Invoke(this, EventArgs.Empty);
        }

        // 頁面請求下一頁
        protected void OnNextRequested()
        {
            if (IsValid())
            {
                NextRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // 頁面請求完成
        protected void OnFinishRequested()
        {
            if (IsValid())
            {
                FinishRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
} 