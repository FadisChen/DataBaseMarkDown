using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using DataBaseMarkDown.Models;
using DataBaseMarkDown.Services;
using DataBaseMarkDown.Utils;

namespace DataBaseMarkDown.Forms
{
    public partial class TableSelectionPage : WizardPage
    {
        private readonly DatabaseInfo _dbInfo;
        private List<TableInfo> _tables = new List<TableInfo>();
        private DatabaseService? _dbService;
        private bool _isFirstLoad = true; // 標記是否為首次加載

        public List<TableInfo> SelectedTables => _tables.FindAll(t => t.IsSelected);

        public TableSelectionPage(DatabaseInfo dbInfo)
        {
            InitializeComponent();
            
            _dbInfo = dbInfo;
            
            PageTitle = "選擇表格";
            HasPrevious = true;
            HasNext = true;
            CanFinish = false;
            
            // 綁定事件
            btnPrevious.Click += BtnPrevious_Click;
            btnNext.Click += BtnNext_Click;
        }

        // 頁面激活時調用
        public override async void OnActivated()
        {
            // 清空樹狀視圖
            treeDatabase.Nodes.Clear();
            
            // 顯示加載提示
            lblLoading.Visible = true;
            progressBar.Visible = true;
            
            // 禁用按鈕
            btnNext.Enabled = false;
            btnPrevious.Enabled = false;
            
            try
            {
                // 創建數據庫服務
                _dbService = new DatabaseService(_dbInfo);
                
                // 只在首次加載時重新獲取表格數據
                if (_isFirstLoad)
                {
                    // 獲取所有表格
                    _tables = await _dbService.GetAllTablesAsync();
                    _isFirstLoad = false;
                }
                
                // 加載樹狀視圖
                LoadTreeView();
                
                // 啟用按鈕
                btnPrevious.Enabled = true;
                
                // 如果有選中的表格，則啟用下一步按鈕
                UpdateNextButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加載表格時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 隱藏加載提示
                lblLoading.Visible = false;
                progressBar.Visible = false;
            }
        }

        // 加載樹狀視圖
        private void LoadTreeView()
        {
            // 移除現有的事件處理
            if (treeDatabase != null)
                treeDatabase.AfterCheck -= TreeDatabase_AfterCheck;
            
            treeDatabase.BeginUpdate();
            treeDatabase.Nodes.Clear();
            
            // 按結構分組
            Dictionary<string, TreeNode> schemaNodes = new Dictionary<string, TreeNode>();
            
            foreach (var table in _tables)
            {
                string schema = string.IsNullOrEmpty(table.Schema) ? "(default)" : table.Schema;
                
                // 如果結構節點不存在，則創建
                if (!schemaNodes.ContainsKey(schema))
                {
                    var schemaNode = new TreeNode(schema)
                    {
                        Tag = null, // 不關聯表格對象
                    };
                    schemaNodes.Add(schema, schemaNode);
                    treeDatabase.Nodes.Add(schemaNode);
                }
                
                // 創建表格節點
                string tableText = $"{table.Name} ({table.Columns.Count} 個欄位)";
                var tableNode = new TreeNode(tableText)
                {
                    Tag = table,
                    Checked = table.IsSelected // 根據現有選擇狀態設置勾選
                };
                
                // 將表格節點添加到相應的結構節點
                schemaNodes[schema].Nodes.Add(tableNode);
            }
            
            // 更新結構節點的勾選狀態
            foreach (TreeNode schemaNode in treeDatabase.Nodes)
            {
                bool allChecked = true;
                
                foreach (TreeNode tableNode in schemaNode.Nodes)
                {
                    if (!tableNode.Checked)
                        allChecked = false;
                }
                
                // 如果該結構下所有表格都被選中，則選中該結構節點
                schemaNode.Checked = allChecked;
            }
            
            treeDatabase.EndUpdate();
            
            // 展開所有節點
            treeDatabase.ExpandAll();
            
            // 添加CheckBox到節點
            treeDatabase.CheckBoxes = true;
            
            // 繫結節點選中事件
            treeDatabase.AfterCheck += TreeDatabase_AfterCheck;
        }

        // 節點選中事件
        private void TreeDatabase_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            // 防止事件遞迴
            treeDatabase.AfterCheck -= TreeDatabase_AfterCheck;
            
            try
            {
                // 如果是表格節點
                if (e.Node.Tag is TableInfo tableInfo)
                {
                    Console.WriteLine($"表格 '{tableInfo.Name}' 勾選狀態變更: {e.Node.Checked}");
                    
                    // 更新表格選擇狀態
                    tableInfo.IsSelected = e.Node.Checked;
                    
                    // 自動選中表格的所有欄位
                    foreach (var column in tableInfo.Columns)
                    {
                        column.IsSelected = e.Node.Checked;
                    }
                }
                // 如果是結構節點（根節點）
                else if (e.Node.Tag == null && e.Node.Nodes.Count > 0)
                {
                    // 更新該結構下所有表格的選中狀態
                    foreach (TreeNode tableNode in e.Node.Nodes)
                    {
                        tableNode.Checked = e.Node.Checked;
                        
                        if (tableNode.Tag is TableInfo table)
                        {
                            Console.WriteLine($"通過結構勾選更新表格 '{table.Name}' 選擇狀態: {e.Node.Checked}");
                            
                            // 更新表格選擇狀態
                            table.IsSelected = tableNode.Checked;
                            
                            // 自動選中表格的所有欄位
                            foreach (var column in table.Columns)
                            {
                                column.IsSelected = tableNode.Checked;
                            }
                        }
                    }
                }
                
                // 檢查是否有選中的表格
                UpdateNextButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"處理勾選事件時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 重新繫結事件
                treeDatabase.AfterCheck += TreeDatabase_AfterCheck;
            }
        }

        // 重置頁面狀態，重新加載所有數據（在需要時調用）
        public void ResetPage()
        {
            _isFirstLoad = true;
        }

        // 更新下一步按鈕狀態
        private void UpdateNextButtonState()
        {
            btnNext.Enabled = _tables.Exists(t => t.IsSelected);
        }

        // 上一步按鈕點擊
        private void BtnPrevious_Click(object? sender, EventArgs e)
        {
            OnPreviousRequested();
        }

        // 下一步按鈕點擊
        private void BtnNext_Click(object? sender, EventArgs e)
        {
            if (SelectedTables.Count == 0)
            {
                MessageBox.Show("請至少選擇一個表格", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // 輸出選中的表格名稱，用於調試
            Console.WriteLine("選中的表格:");
            foreach (var table in SelectedTables)
            {
                Console.WriteLine($"- {table.Name} (勾選狀態: {table.IsSelected})");
            }
            
            OnNextRequested();
        }

        // 設計器生成的代碼
        private void InitializeComponent()
        {
            lblTitle = new Label();
            lblInstructions = new Label();
            treeDatabase = new TreeView();
            lblLoading = new Label();
            progressBar = new ProgressBar();
            btnPrevious = new Button();
            btnNext = new Button();
            SuspendLayout();
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold);
            lblTitle.Location = new Point(25, 25);
            lblTitle.Margin = new Padding(5, 0, 5, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(217, 32);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "選擇表格";
            // 
            // lblInstructions
            // 
            lblInstructions.AutoSize = true;
            lblInstructions.Location = new Point(25, 77);
            lblInstructions.Margin = new Padding(5, 0, 5, 0);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new Size(568, 23);
            lblInstructions.TabIndex = 1;
            lblInstructions.Text = "請選擇需要生成文檔的表格。勾選表格後將自動包含該表格的所有欄位。";
            // 
            // treeDatabase
            // 
            treeDatabase.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            treeDatabase.Location = new Point(25, 120);
            treeDatabase.Margin = new Padding(5, 5, 5, 5);
            treeDatabase.Name = "treeDatabase";
            treeDatabase.Size = new Size(941, 435);
            treeDatabase.TabIndex = 2;
            // 
            // lblLoading
            // 
            lblLoading.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lblLoading.AutoSize = true;
            lblLoading.Location = new Point(25, 561);
            lblLoading.Margin = new Padding(5, 0, 5, 0);
            lblLoading.Name = "lblLoading";
            lblLoading.Size = new Size(130, 23);
            lblLoading.TabIndex = 3;
            lblLoading.Text = "正在加載資料...";
            lblLoading.Visible = false;
            // 
            // progressBar
            // 
            progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Location = new Point(233, 561);
            progressBar.Margin = new Padding(5, 5, 5, 5);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(735, 23);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 4;
            progressBar.Visible = false;
            // 
            // btnPrevious
            // 
            btnPrevious.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnPrevious.Location = new Point(644, 613);
            btnPrevious.Margin = new Padding(5, 5, 5, 5);
            btnPrevious.Name = "btnPrevious";
            btnPrevious.Size = new Size(157, 46);
            btnPrevious.TabIndex = 7;
            btnPrevious.Text = "上一步";
            btnPrevious.UseVisualStyleBackColor = true;
            // 
            // btnNext
            // 
            btnNext.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnNext.Enabled = false;
            btnNext.Location = new Point(811, 613);
            btnNext.Margin = new Padding(5, 5, 5, 5);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(157, 46);
            btnNext.TabIndex = 8;
            btnNext.Text = "下一步";
            btnNext.UseVisualStyleBackColor = true;
            // 
            // TableSelectionPage
            // 
            AutoScaleDimensions = new SizeF(11F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(lblTitle);
            Controls.Add(lblInstructions);
            Controls.Add(treeDatabase);
            Controls.Add(lblLoading);
            Controls.Add(progressBar);
            Controls.Add(btnPrevious);
            Controls.Add(btnNext);
            Margin = new Padding(5, 5, 5, 5);
            Name = "TableSelectionPage";
            Size = new Size(993, 695);
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.TreeView treeDatabase;
        private System.Windows.Forms.Label lblLoading;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnNext;
    }
} 