using System;
using System.Windows.Forms;
using System.IO;
using DataBaseMarkDown.Models;
using DataBaseMarkDown.Services;
using DataBaseMarkDown.Utils;

namespace DataBaseMarkDown.Forms
{
    public partial class DatabaseConnectionPage : WizardPage
    {
        private readonly DatabaseInfo _dbInfo;

        public DatabaseConnectionPage()
        {
            InitializeComponent();
            
            PageTitle = "資料庫連接設置";
            HasPrevious = false;
            HasNext = true;
            CanFinish = false;

            _dbInfo = new DatabaseInfo();
            
            // 初始化數據庫類型下拉框
            cboDbType.Items.Add(DatabaseType.SqlServer);
            cboDbType.Items.Add(DatabaseType.MariaDB);
            cboDbType.Items.Add(DatabaseType.SQLite);
            cboDbType.SelectedIndex = 0;
            
            // 綁定事件
            cboDbType.SelectedIndexChanged += CboDbType_SelectedIndexChanged;
            btnTestConnection.Click += BtnTestConnection_Click;
            btnBrowse.Click += BtnBrowse_Click;
            btnPrevious.Click += BtnPrevious_Click;
            btnNext.Click += BtnNext_Click;
        }

        // 激活頁面時調用
        public override void OnActivated()
        {
            UpdateControlsVisibility();
        }

        // 數據庫類型選擇變更
        private void CboDbType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateControlsVisibility();
        }

        // 測試連接按鈕點擊
        private async void BtnTestConnection_Click(object? sender, EventArgs e)
        {
            if (!ValidateConnection())
            {
                return;
            }

            try
            {
                btnTestConnection.Enabled = false;
                btnTestConnection.Text = "測試中...";

                UpdateDatabaseInfo();

                var dbService = new DatabaseService(_dbInfo);
                bool isConnected = await dbService.TestConnectionAsync();

                if (isConnected)
                {
                    MessageBox.Show("連接成功!", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // 確保成功連接後下一步按鈕可用
                    btnNext.Enabled = true;
                }
                else
                {
                    MessageBox.Show("連接失敗，請檢查連接參數。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"連接測試失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestConnection.Enabled = true;
                btnTestConnection.Text = "測試連接";
            }
        }

        // 瀏覽SQLite文件按鈕點擊
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "SQLite文件 (*.db;*.sqlite;*.sqlite3;*.db3)|*.db;*.sqlite;*.sqlite3;*.db3|所有文件 (*.*)|*.*";
            openFileDialog.Title = "選擇SQLite資料庫文件";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                txtFilePath.Text = openFileDialog.FileName;
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
            if (!ValidateConnection())
            {
                return;
            }

            // 更新資料庫資訊
            UpdateDatabaseInfo();
            
            // 觸發下一頁事件
            OnNextRequested();
        }

        // 頁面是否有效
        public override bool IsValid()
        {
            return ValidateConnection();
        }

        // 驗證連接信息
        private bool ValidateConnection()
        {
            var dbType = (DatabaseType)cboDbType.SelectedItem;
            
            if (dbType == DatabaseType.SQLite)
            {
                if (string.IsNullOrEmpty(txtFilePath.Text))
                {
                    MessageBox.Show("請選擇SQLite資料庫文件", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (!File.Exists(txtFilePath.Text))
                {
                    MessageBox.Show("指定的SQLite文件不存在", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            else // SQL Server 或 MariaDB
            {
                if (string.IsNullOrEmpty(txtServer.Text))
                {
                    MessageBox.Show("請輸入伺服器地址", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                if (string.IsNullOrEmpty(txtDatabase.Text))
                {
                    MessageBox.Show("請輸入資料庫名稱", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            return true;
        }

        // 更新數據庫信息
        private void UpdateDatabaseInfo()
        {
            _dbInfo.Type = (DatabaseType)cboDbType.SelectedItem;
            
            if (_dbInfo.Type == DatabaseType.SQLite)
            {
                _dbInfo.FilePath = txtFilePath.Text;
            }
            else // SQL Server 或 MariaDB
            {
                _dbInfo.Server = txtServer.Text;
                _dbInfo.Database = txtDatabase.Text;
                _dbInfo.Username = txtUsername.Text;
                _dbInfo.Password = txtPassword.Text;
            }

            _dbInfo.ConnectionString = _dbInfo.BuildConnectionString();
        }
        
        // 將資料庫信息更新到外部物件
        public void UpdateDatabaseInfo(DatabaseInfo dbInfo)
        {
            if (dbInfo == null)
            {
                return;
            }
            
            // 更新內部資料庫信息
            UpdateDatabaseInfo();
            
            // 將內部資訊複製到外部
            dbInfo.Type = _dbInfo.Type;
            dbInfo.Server = _dbInfo.Server;
            dbInfo.Database = _dbInfo.Database;
            dbInfo.Username = _dbInfo.Username;
            dbInfo.Password = _dbInfo.Password;
            dbInfo.FilePath = _dbInfo.FilePath;
            dbInfo.ConnectionString = _dbInfo.ConnectionString;
        }

        // 根據選擇的數據庫類型更新控件可見性
        private void UpdateControlsVisibility()
        {
            var dbType = (DatabaseType)cboDbType.SelectedItem;
            
            // SQLite僅需文件路徑
            bool isSqlite = dbType == DatabaseType.SQLite;
            
            // SQL Server和MariaDB需要伺服器信息
            lblServer.Visible = !isSqlite;
            txtServer.Visible = !isSqlite;
            lblDatabase.Visible = !isSqlite;
            txtDatabase.Visible = !isSqlite;
            lblUsername.Visible = !isSqlite;
            txtUsername.Visible = !isSqlite;
            lblPassword.Visible = !isSqlite;
            txtPassword.Visible = !isSqlite;
            
            // SQLite需要文件路徑
            lblFilePath.Visible = isSqlite;
            txtFilePath.Visible = isSqlite;
            btnBrowse.Visible = isSqlite;
        }

        // 設計器生成的代碼
        private void InitializeComponent()
        {
            lblTitle = new Label();
            lblDbType = new Label();
            cboDbType = new ComboBox();
            lblServer = new Label();
            txtServer = new TextBox();
            lblDatabase = new Label();
            txtDatabase = new TextBox();
            lblUsername = new Label();
            txtUsername = new TextBox();
            lblPassword = new Label();
            txtPassword = new TextBox();
            lblFilePath = new Label();
            txtFilePath = new TextBox();
            btnBrowse = new Button();
            btnTestConnection = new Button();
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
            lblTitle.Text = "資料庫連接設置";
            // 
            // lblDbType
            // 
            lblDbType.AutoSize = true;
            lblDbType.Location = new Point(25, 92);
            lblDbType.Margin = new Padding(5, 0, 5, 0);
            lblDbType.Name = "lblDbType";
            lblDbType.Size = new Size(104, 23);
            lblDbType.TabIndex = 1;
            lblDbType.Text = "數據庫類型:";
            // 
            // cboDbType
            // 
            cboDbType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDbType.FormattingEnabled = true;
            cboDbType.Location = new Point(25, 120);
            cboDbType.Margin = new Padding(5);
            cboDbType.Name = "cboDbType";
            cboDbType.Size = new Size(281, 31);
            cboDbType.TabIndex = 2;
            // 
            // lblServer
            // 
            lblServer.AutoSize = true;
            lblServer.Location = new Point(25, 184);
            lblServer.Margin = new Padding(5, 0, 5, 0);
            lblServer.Name = "lblServer";
            lblServer.Size = new Size(104, 23);
            lblServer.TabIndex = 3;
            lblServer.Text = "伺服器地址:";
            // 
            // txtServer
            // 
            txtServer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtServer.Location = new Point(25, 212);
            txtServer.Margin = new Padding(5);
            txtServer.Name = "txtServer";
            txtServer.Size = new Size(941, 30);
            txtServer.TabIndex = 4;
            txtServer.Text = "127.0.0.1";
            // 
            // lblDatabase
            // 
            lblDatabase.AutoSize = true;
            lblDatabase.Location = new Point(25, 261);
            lblDatabase.Margin = new Padding(5, 0, 5, 0);
            lblDatabase.Name = "lblDatabase";
            lblDatabase.Size = new Size(104, 23);
            lblDatabase.TabIndex = 5;
            lblDatabase.Text = "資料庫名稱:";
            // 
            // txtDatabase
            // 
            txtDatabase.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDatabase.Location = new Point(25, 288);
            txtDatabase.Margin = new Padding(5);
            txtDatabase.Name = "txtDatabase";
            txtDatabase.Size = new Size(941, 30);
            txtDatabase.TabIndex = 6;
            // 
            // lblUsername
            // 
            lblUsername.AutoSize = true;
            lblUsername.Location = new Point(25, 337);
            lblUsername.Margin = new Padding(5, 0, 5, 0);
            lblUsername.Name = "lblUsername";
            lblUsername.Size = new Size(68, 23);
            lblUsername.TabIndex = 7;
            lblUsername.Text = "用戶名:";
            // 
            // txtUsername
            // 
            txtUsername.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtUsername.Location = new Point(25, 365);
            txtUsername.Margin = new Padding(5);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(941, 30);
            txtUsername.TabIndex = 8;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Location = new Point(25, 414);
            lblPassword.Margin = new Padding(5, 0, 5, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(50, 23);
            lblPassword.TabIndex = 9;
            lblPassword.Text = "密碼:";
            // 
            // txtPassword
            // 
            txtPassword.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtPassword.Location = new Point(25, 442);
            txtPassword.Margin = new Padding(5);
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.Size = new Size(941, 30);
            txtPassword.TabIndex = 10;
            // 
            // lblFilePath
            // 
            lblFilePath.AutoSize = true;
            lblFilePath.Location = new Point(25, 184);
            lblFilePath.Margin = new Padding(5, 0, 5, 0);
            lblFilePath.Name = "lblFilePath";
            lblFilePath.Size = new Size(104, 23);
            lblFilePath.TabIndex = 11;
            lblFilePath.Text = "資料庫文件:";
            lblFilePath.Visible = false;
            // 
            // txtFilePath
            // 
            txtFilePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtFilePath.Location = new Point(25, 212);
            txtFilePath.Margin = new Padding(5);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(813, 30);
            txtFilePath.TabIndex = 12;
            txtFilePath.Visible = false;
            // 
            // btnBrowse
            // 
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBrowse.Location = new Point(850, 212);
            btnBrowse.Margin = new Padding(5);
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new Size(118, 35);
            btnBrowse.TabIndex = 13;
            btnBrowse.Text = "瀏覽...";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Visible = false;
            // 
            // btnTestConnection
            // 
            btnTestConnection.Location = new Point(25, 506);
            btnTestConnection.Margin = new Padding(5);
            btnTestConnection.Name = "btnTestConnection";
            btnTestConnection.Size = new Size(157, 46);
            btnTestConnection.TabIndex = 14;
            btnTestConnection.Text = "測試連接";
            btnTestConnection.UseVisualStyleBackColor = true;
            // 
            // btnPrevious
            // 
            btnPrevious.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnPrevious.Location = new Point(644, 613);
            btnPrevious.Margin = new Padding(5);
            btnPrevious.Name = "btnPrevious";
            btnPrevious.Size = new Size(157, 46);
            btnPrevious.TabIndex = 15;
            btnPrevious.Text = "上一步";
            btnPrevious.UseVisualStyleBackColor = true;
            // 
            // btnNext
            // 
            btnNext.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnNext.Location = new Point(811, 613);
            btnNext.Margin = new Padding(5);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(157, 46);
            btnNext.TabIndex = 16;
            btnNext.Text = "下一步";
            btnNext.UseVisualStyleBackColor = true;
            // 
            // DatabaseConnectionPage
            // 
            AutoScaleDimensions = new SizeF(11F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(lblTitle);
            Controls.Add(lblDbType);
            Controls.Add(cboDbType);
            Controls.Add(lblServer);
            Controls.Add(txtServer);
            Controls.Add(lblDatabase);
            Controls.Add(txtDatabase);
            Controls.Add(lblUsername);
            Controls.Add(txtUsername);
            Controls.Add(lblPassword);
            Controls.Add(txtPassword);
            Controls.Add(lblFilePath);
            Controls.Add(txtFilePath);
            Controls.Add(btnBrowse);
            Controls.Add(btnTestConnection);
            Controls.Add(btnPrevious);
            Controls.Add(btnNext);
            Margin = new Padding(5);
            Name = "DatabaseConnectionPage";
            Size = new Size(993, 695);
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblDbType;
        private System.Windows.Forms.ComboBox cboDbType;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblDatabase;
        private System.Windows.Forms.TextBox txtDatabase;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label lblFilePath;
        private System.Windows.Forms.TextBox txtFilePath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnTestConnection;
        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnNext;
    }
} 