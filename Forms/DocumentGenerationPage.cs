using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using Markdig;
using DataBaseMarkDown.Models;
using DataBaseMarkDown.Services;
using DataBaseMarkDown.Utils;

namespace DataBaseMarkDown.Forms
{
    public partial class DocumentGenerationPage : WizardPage
    {
        private readonly List<TableInfo> _selectedTables;
        private GeminiService? _geminiService;
        private string _generatedDocument = string.Empty;
        private Label? _lblProgress; // 進度文字標籤

        public string GeneratedDocument => _generatedDocument;

        public DocumentGenerationPage(List<TableInfo> selectedTables)
        {
            InitializeComponent();
            
            PageTitle = "生成與編輯文檔";
            HasPrevious = true;
            HasNext = true;  // 改為 true，以便進入保存頁面
            CanFinish = false;  // 不直接完成
            
            _selectedTables = selectedTables;
            
            // 綁定事件
            btnGenerate.Click += BtnGenerate_Click;
            btnPrevious.Click += BtnPrevious_Click;
            btnNext.Click += BtnNext_Click;
            
            // 創建進度文字標籤
            CreateProgressLabel();
        }
        
        // 創建進度文字標籤
        private void CreateProgressLabel()
        {
            _lblProgress = new Label();
            _lblProgress.AutoSize = true;
            _lblProgress.Location = new System.Drawing.Point(122, 350);
            _lblProgress.Name = "lblProgress";
            _lblProgress.Size = new System.Drawing.Size(300, 15);
            _lblProgress.TabIndex = 8;
            _lblProgress.Text = "";
            _lblProgress.Visible = false;
            _lblProgress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            
            this.Controls.Add(_lblProgress);
        }

        // 頁面激活時調用
        public override void OnActivated()
        {
            // 初始化API服務
            _geminiService = new GeminiService(AppSettings.Instance.ApiSettings);
            
            // 禁用下一步按鈕
            btnNext.Enabled = false;
        }

        // 頁面是否有效
        public override bool IsValid()
        {
            return !string.IsNullOrEmpty(_generatedDocument);
        }
        
        // 更新進度的回調
        private void UpdateProgress(string message, int currentStep, int totalSteps)
        {
            // 由於是從另一個線程調用，需要使用 Invoke
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, int, int>(UpdateProgress), 
                    new object[] { message, currentStep, totalSteps });
                return;
            }
            
            // 更新進度條
            if (totalSteps > 1)
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Maximum = totalSteps;
                progressBar.Value = currentStep;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
            
            // 更新進度文字
            if (_lblProgress != null)
            {
                _lblProgress.Visible = true;
                _lblProgress.Text = message;
            }
        }

        // 生成文檔按鈕點擊
        private async void BtnGenerate_Click(object? sender, EventArgs e)
        {
            if (_geminiService == null)
            {
                MessageBox.Show("API服務未初始化", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnGenerate.Enabled = false;
                btnGenerate.Text = "生成中...";
                progressBar.Visible = true;
                if (_lblProgress != null) _lblProgress.Visible = true;
                
                // 調用API生成文檔，並傳入進度回調
                _generatedDocument = await _geminiService.GenerateDatabaseDocumentationAsync(
                    _selectedTables, 
                    UpdateProgress);
                _generatedDocument = _generatedDocument.Replace("```markdown", "").Replace("```", "");
                // 將 Markdown 轉換為 HTML 並顯示在 WebBrowser 控制項中
                DisplayMarkdownAsHtml(_generatedDocument);
                
                // 如果生成成功，啟用下一步按鈕
                btnNext.Enabled = IsValid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成文檔時發生錯誤: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGenerate.Enabled = true;
                btnGenerate.Text = "重新生成";
                progressBar.Visible = false;
                if (_lblProgress != null) _lblProgress.Visible = false;
            }
        }

        // 將 Markdown 轉換為 HTML 並顯示
        private void DisplayMarkdownAsHtml(string markdown)
        {
            try
            {
                // 使用 Markdig 將 Markdown 轉換為 HTML
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();
                string html = Markdown.ToHtml(markdown, pipeline);

                // 添加基本的 CSS 樣式
                string styledHtml = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ 
                            font-family: Arial, sans-serif; 
                            line-height: 1.6;
                            margin: 20px;
                        }}
                        h1 {{ color: #333; border-bottom: 1px solid #ddd; padding-bottom: 5px; }}
                        h2 {{ color: #444; margin-top: 20px; }}
                        h3 {{ color: #555; }}
                        table {{ border-collapse: collapse; width: 100%; margin: 15px 0; }}
                        th, td {{ border: 1px solid #ddd; padding: 8px; }}
                        th {{ background-color: #f2f2f2; text-align: left; }}
                        code {{ background-color: #f5f5f5; padding: 2px 4px; border-radius: 3px; }}
                        pre {{ background-color: #f5f5f5; padding: 10px; border-radius: 3px; overflow-x: auto; }}
                    </style>
                </head>
                <body>
                    {html}
                </body>
                </html>";

                // 顯示在 WebBrowser 控制項中
                webBrowser.DocumentText = styledHtml;
            }
            catch (Exception ex)
            {
                // 如果 HTML 轉換失敗，則顯示原始 Markdown 文本
                MessageBox.Show($"無法將 Markdown 轉換為 HTML: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMarkdown.Text = markdown;
                txtMarkdown.Visible = true;
                webBrowser.Visible = false;
            }
        }

        // 上一步按鈕點擊
        private void BtnPrevious_Click(object? sender, EventArgs e)
        {
            OnPreviousRequested();
        }

        // 下一步按鈕點擊
        private void BtnNext_Click(object? sender, EventArgs e)
        {
            if (IsValid())
            {
                OnNextRequested();
            }
            else
            {
                MessageBox.Show("請先生成文檔", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // 設計器生成的代碼
        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblInstructions = new System.Windows.Forms.Label();
            this.webBrowser = new System.Windows.Forms.WebBrowser();
            this.txtMarkdown = new System.Windows.Forms.TextBox();
            this.btnGenerate = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTitle.Location = new System.Drawing.Point(16, 16);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(161, 24);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "生成與編輯文檔";
            // 
            // lblInstructions
            // 
            this.lblInstructions.AutoSize = true;
            this.lblInstructions.Location = new System.Drawing.Point(16, 50);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(403, 15);
            this.lblInstructions.TabIndex = 1;
            this.lblInstructions.Text = "點擊「生成文檔」按鈕生成資料庫文檔。您可以點擊「下一步」保存文檔。";
            // 
            // webBrowser
            // 
            this.webBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webBrowser.Location = new System.Drawing.Point(16, 78);
            this.webBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.webBrowser.Name = "webBrowser";
            this.webBrowser.Size = new System.Drawing.Size(600, 265);
            this.webBrowser.TabIndex = 2;
            // 
            // txtMarkdown
            // 
            this.txtMarkdown.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMarkdown.Location = new System.Drawing.Point(16, 78);
            this.txtMarkdown.Multiline = true;
            this.txtMarkdown.Name = "txtMarkdown";
            this.txtMarkdown.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtMarkdown.Size = new System.Drawing.Size(600, 265);
            this.txtMarkdown.TabIndex = 3;
            this.txtMarkdown.Visible = false;
            // 
            // btnGenerate
            // 
            this.btnGenerate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnGenerate.Location = new System.Drawing.Point(16, 369);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.Size = new System.Drawing.Size(100, 30);
            this.btnGenerate.TabIndex = 4;
            this.btnGenerate.Text = "生成文檔";
            this.btnGenerate.UseVisualStyleBackColor = true;
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(122, 369);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(494, 30);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Blocks;
            this.progressBar.TabIndex = 5;
            this.progressBar.Visible = false;
            // 
            // btnPrevious
            // 
            this.btnPrevious.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPrevious.Location = new System.Drawing.Point(410, 400);
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.Size = new System.Drawing.Size(100, 30);
            this.btnPrevious.TabIndex = 6;
            this.btnPrevious.Text = "上一步";
            this.btnPrevious.UseVisualStyleBackColor = true;
            // 
            // btnNext
            // 
            this.btnNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNext.Enabled = false;
            this.btnNext.Location = new System.Drawing.Point(516, 400);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(100, 30);
            this.btnNext.TabIndex = 7;
            this.btnNext.Text = "下一步";
            this.btnNext.UseVisualStyleBackColor = true;
            // 
            // DocumentGenerationPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblInstructions);
            this.Controls.Add(this.webBrowser);
            this.Controls.Add(this.txtMarkdown);
            this.Controls.Add(this.btnGenerate);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnPrevious);
            this.Controls.Add(this.btnNext);
            this.Name = "DocumentGenerationPage";
            this.Size = new System.Drawing.Size(632, 453);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.WebBrowser webBrowser;
        private System.Windows.Forms.TextBox txtMarkdown;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnNext;
    }
} 