# 資料庫 Markdown 文檔生成器

## 簡介

本工具是一個 C# Windows Forms 應用程式，旨在幫助開發者快速生成資料庫結構的 Markdown 文檔。它利用 Gemini API 的強大語言模型，將資料庫的結構信息轉換為易於閱讀和分享的 Markdown 格式文檔。

## 功能

* **多種資料庫支援**: 目前支援 SQL Server, MariaDB, 和 SQLite 資料庫。
* **直觀的表格選擇**: 使用樹狀視圖展示資料庫結構，使用者可以輕鬆選擇需要生成文檔的表格。
* **智能文檔生成**: 整合 Gemini API，自動生成表格和欄位的描述文檔。
* **Markdown 格式輸出**: 生成的文檔為標準 Markdown 格式，方便在各種平台展示和編輯。
* **可自訂的檔案名稱**: 預設檔案名稱為資料庫名稱，並可自訂儲存路徑。
* **HTML 樣式預覽**: 使用 WebBrowser 控制項，以 HTML 樣式預覽 Markdown 文檔，提供更佳的視覺效果。

## 使用步驟

1. **連線資料庫**: 在應用程式的第一步，輸入您的資料庫連線資訊，包括資料庫類型、伺服器位址、資料庫名稱、使用者名稱和密碼等。
2. **選擇表格**: 在第二步，您將看到以樹狀結構展示的資料庫表格。勾選您想要包含在文檔中的表格。
3. **生成文檔**: 點擊「生成文檔」按鈕，應用程式將調用 Gemini API 生成 Markdown 文檔。您可以在應用程式中預覽生成的文檔。
4. **儲存文檔**: 在最後一步，您可以選擇儲存路徑和檔案名稱，將生成的 Markdown 文檔儲存到本地磁碟。

## 技術棧

* **開發語言**: C#
* **平台**: .NET Framework, Windows Forms
* **Markdown 轉換**: Markdig
* **AI 服務**: Google Gemini API
* **資料庫存取**: Dapper

## 環境需求

* **作業系統**: Windows
* **.NET SDK**:  需要安裝 .NET SDK 以編譯和執行程式。
* **Gemini API 金鑰**:  需要申請 Google Gemini API 金鑰，並配置到應用程式中才能使用文檔生成功能。

## 如何編譯和執行

1. **下載專案**: 將專案原始碼下載到本地。
2. **設定 Gemini API 金鑰**:  您需要在專案中配置您的 Gemini API 金鑰。具體設定方式請參考專案中的相關文件或程式碼註解。
3. **使用 .NET CLI 編譯**: 開啟命令提示字元或 PowerShell，導航到專案根目錄，執行以下命令編譯專案：
   ```powershell
   dotnet build
   ```
4. **執行程式**: 編譯成功後，執行以下命令來啟動應用程式：
   ```powershell
   dotnet run
   ```
   或者，您也可以直接執行編譯產生的可執行檔 (`.exe`)，通常位於 `bin\Debug\netX.X-windows` 或 `bin\Release\netX.X-windows` 目錄下。