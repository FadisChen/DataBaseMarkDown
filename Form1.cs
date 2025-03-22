using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DataBaseMarkDown.Forms;
using DataBaseMarkDown.Models;
using DataBaseMarkDown.Utils;

namespace DataBaseMarkDown
{
    public partial class Form1 : Form
    {
        // 保存當前頁面索引
        private int _currentPageIndex = 0;
        
        // 保存所有頁面
        private readonly List<WizardPage> _pages = new List<WizardPage>();
        
        // 資料庫信息
        private readonly DatabaseInfo _dbInfo = new DatabaseInfo();
        
        // 選擇的表格
        private List<TableInfo> _selectedTables = new List<TableInfo>();
        
        // 生成的文檔
        private string _generatedDocument = string.Empty;

        public Form1()
        {
            InitializeComponent();
            
            // 設置窗體標題和大小
            Text = "資料庫結構文檔生成器";
            MinimumSize = new System.Drawing.Size(700, 500);
            
            // 初始化頁面
            InitializePages();
            
            // 顯示第一個頁面
            ShowPage(0);
            
            // 綁定按鈕事件
            btnPrevious.Click += BtnPrevious_Click;
            btnNext.Click += BtnNext_Click;
            btnFinish.Click += BtnFinish_Click;
        }

        // 初始化所有嚮導頁面
        private void InitializePages()
        {
            // 創建數據庫連接頁面
            _pages.Add(new DatabaseConnectionPage()
            {
                Dock = DockStyle.Fill,
                HasPrevious = false // 由於第一頁，所以沒有上一頁
            });
            
            // 表格選擇頁面將在進入該頁時創建
            
            // 繫結頁面事件
            foreach (var page in _pages)
            {
                page.Dock = DockStyle.Fill;
                page.PreviousRequested += Page_PreviousRequested;
                page.NextRequested += Page_NextRequested;
                page.FinishRequested += Page_FinishRequested;
            }
        }

        // 顯示頁面
        private void ShowPage(int index)
        {
            //if (index < 0 || index >= _pages.Count)
            //{
            //    return;
            //}

            // 記錄當前頁面索引
            _currentPageIndex = index;
            
            // 如果頁面是表格選擇頁，則需要動態創建
            if (index == 1 && (_pages.Count <= 1 || !(_pages[1] is TableSelectionPage)))
            {
                // 創建表格選擇頁面
                var tablePage = new TableSelectionPage(_dbInfo)
                {
                    Dock = DockStyle.Fill
                };
                
                tablePage.PreviousRequested += Page_PreviousRequested;
                tablePage.NextRequested += Page_NextRequested;
                tablePage.FinishRequested += Page_FinishRequested;
                
                if (_pages.Count > 1)
                {
                    _pages[1] = tablePage;
                }
                else
                {
                    _pages.Add(tablePage);
                }
            }
            
            // 如果頁面是文檔生成頁，則需要動態創建
            if (index == 2 && (_pages.Count <= 2 || !(_pages[2] is DocumentGenerationPage)))
            {
                // 獲取選擇的表格
                var tablePage = _pages[1] as TableSelectionPage;
                if (tablePage != null)
                {
                    _selectedTables = tablePage.SelectedTables;
                }
                
                // 創建文檔生成頁面
                var docPage = new DocumentGenerationPage(_selectedTables)
                {
                    Dock = DockStyle.Fill
                };
                
                docPage.PreviousRequested += Page_PreviousRequested;
                docPage.NextRequested += Page_NextRequested;
                docPage.FinishRequested += Page_FinishRequested;
                
                if (_pages.Count > 2)
                {
                    _pages[2] = docPage;
                }
                else
                {
                    _pages.Add(docPage);
                }
            }
            
            // 如果頁面是保存文檔頁，則需要動態創建
            if (index == 3 && (_pages.Count <= 3 || !(_pages[3] is SaveDocumentPage)))
            {
                // 獲取生成的文檔
                var docPage = _pages[2] as DocumentGenerationPage;
                if (docPage != null)
                {
                    _generatedDocument = docPage.GeneratedDocument;
                }
                
                // 創建保存文檔頁面，並傳遞資料庫名稱
                var savePage = new SaveDocumentPage(
                    _generatedDocument, 
                    _dbInfo.Database) // 傳遞資料庫名稱
                {
                    Dock = DockStyle.Fill
                };
                
                savePage.PreviousRequested += Page_PreviousRequested;
                savePage.NextRequested += Page_NextRequested;
                savePage.FinishRequested += Page_FinishRequested;
                
                if (_pages.Count > 3)
                {
                    _pages[3] = savePage;
                }
                else
                {
                    _pages.Add(savePage);
                }
            }

            // 清除所有頁面
            panelContent.Controls.Clear();
            
            // 添加當前頁面
            var currentPage = _pages[_currentPageIndex];
            panelContent.Controls.Add(currentPage);
            
            // 隱藏嚮導頁面上的按鈕（如果顯示在主視窗）
            HidePageButtons(currentPage);
            
            // 更新按鈕狀態
            UpdateButtonsState(currentPage);
            
            // 更新進度
            UpdateProgress();
            
            // 激活頁面
            currentPage.OnActivated();
        }

        // 隱藏嚮導頁面中的按鈕
        private void HidePageButtons(WizardPage page)
        {
            // 直接查找特定名稱的按鈕並隱藏
            foreach (Control control in page.Controls)
            {
                if (control is Button button)
                {
                    // 根據按鈕名稱或文本內容隱藏導航按鈕
                    string btnName = button.Name?.ToLower() ?? "";
                    string btnText = button.Text?.ToLower() ?? "";
                    
                    if (btnName == "btnnext" || btnName == "btnprevious" || btnName == "btnfinish" ||
                        btnText.Contains("下一步") || btnText.Contains("上一步") || btnText.Contains("完成"))
                    {
                        button.Visible = false;
                    }
                }
            }
        }

        // 更新進度顯示
        private void UpdateProgress()
        {
            // 計算總頁數，包含已知的頁面數量和可能的動態頁面數量
            int totalPages = 4; // 資料庫連接、表格選擇、文檔生成、保存文檔
            
            progressBar.Maximum = totalPages;
            progressBar.Value = _currentPageIndex + 1;
            lblProgress.Text = $"步驟 {_currentPageIndex + 1} / {totalPages}";
        }

        // 更新按鈕狀態
        private void UpdateButtonsState(WizardPage page)
        {
            // 第一頁沒有上一步
            btnPrevious.Visible = _currentPageIndex > 0;
            
            // 根據頁面設置顯示下一步或完成按鈕
            btnNext.Visible = page.HasNext;
            btnFinish.Visible = page.CanFinish;
            
            // 確保按鈕啟用
            btnNext.Enabled = true;
        }

        // 上一步按鈕點擊
        private void BtnPrevious_Click(object sender, EventArgs e)
        {
            // 顯示上一頁
            ShowPage(_currentPageIndex - 1);
        }

        // 下一步按鈕點擊
        private void BtnNext_Click(object sender, EventArgs e)
        {
            var currentPage = _pages[_currentPageIndex];
            
            if (currentPage.IsValid())
            {
                // 如果是資料庫連接頁，更新資料庫信息
                if (currentPage is DatabaseConnectionPage dbPage && _currentPageIndex == 0)
                {
                    dbPage.UpdateDatabaseInfo(_dbInfo);
                }
                
                // 顯示下一頁
                ShowPage(_currentPageIndex + 1);
            }
        }

        // 完成按鈕點擊
        private void BtnFinish_Click(object sender, EventArgs e)
        {
            // 檢查當前頁面是否有效
            if (_pages[_currentPageIndex].IsValid())
            {
                // 跳出提示視窗確認是否已儲存要關閉視窗
                DialogResult result = MessageBox.Show(
                    "即將關閉視窗，請確認是否已儲存檔案？", 
                    "確認", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    Close();
                }
            }
        }

        // 頁面請求上一頁
        private void Page_PreviousRequested(object sender, EventArgs e)
        {
            ShowPage(_currentPageIndex - 1);
        }

        // 頁面請求下一頁
        private void Page_NextRequested(object sender, EventArgs e)
        {
            // 如果是資料庫連接頁，記錄資料庫信息
            if (sender is DatabaseConnectionPage dbPage && _currentPageIndex == 0)
            {
                dbPage.UpdateDatabaseInfo(_dbInfo);
            }
            
            // 顯示下一頁
            ShowPage(_currentPageIndex + 1);
        }

        // 頁面請求完成
        private void Page_FinishRequested(object sender, EventArgs e)
        {
            // 完成應用
            Close();
        }
    }
}
