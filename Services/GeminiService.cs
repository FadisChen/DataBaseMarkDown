using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using DataBaseMarkDown.Models;
using System.Collections.Generic;
using System.Threading;

namespace DataBaseMarkDown.Services
{
    // API 回調委託
    public delegate void ApiProgressCallback(string message, int currentStep, int totalSteps);

    public class GeminiService
    {
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
        
        // 固定參數值
        private const double Temperature = 0.7;
        // 分批處理參數
        private const int MAX_TABLES_PER_BATCH = 5; // 每批處理的最大表格數
        private const int MAX_PROMPT_LENGTH = 8000; // 提示詞的最大長度
        
        // API 限制參數
        private const int API_RATE_LIMIT_PER_MINUTE = 15; // 每分鐘最大請求數
        private const int API_COOLDOWN_MS = 60000; // API 冷卻時間（毫秒）
        private readonly object _apiLock = new object(); // 用於同步 API 請求計數
        private int _apiRequestCount = 0; // API 請求計數
        private DateTime _apiRequestResetTime = DateTime.Now; // API 請求計數重置時間

        public GeminiService(ApiSettings apiSettings)
        {
            _apiSettings = apiSettings;
            _httpClient = new HttpClient();
        }

        // 產生資料庫結構說明
        public async Task<string> GenerateDatabaseDocumentationAsync(
            List<TableInfo> tables, 
            ApiProgressCallback progressCallback = null)
        {
            try
            {
                // 獲取已選擇的表格
                List<TableInfo> selectedTables = tables.FindAll(t => t.IsSelected);
                
                // 通知開始處理
                progressCallback?.Invoke("開始生成資料庫文檔...", 0, 1);
                
                // 如果表格數量較少，直接生成
                if (selectedTables.Count <= MAX_TABLES_PER_BATCH)
                {
                    progressCallback?.Invoke("處理資料庫表格...", 1, 1);
                    return await GenerateDocumentationForTablesAsync(selectedTables);
                }
                
                // 否則，分批處理
                StringBuilder fullDocumentation = new StringBuilder();
                
                // 分批處理表格
                List<List<TableInfo>> batches = SplitTablesIntoBatches(selectedTables);
                
                // 計算總步驟數（概述 + 每個批次）
                int totalSteps = batches.Count + 1;
                int currentStep = 0;
                
                // 首先生成概述部分（使用所有表格但不生成詳細說明）
                progressCallback?.Invoke("生成資料庫概述...", ++currentStep, totalSteps);
                string overview = await GenerateDatabaseOverviewAsync(selectedTables);
                fullDocumentation.AppendLine(overview);
                fullDocumentation.AppendLine();
                
                // 添加表格詳細說明標題
                fullDocumentation.AppendLine("## 表格詳細說明");
                fullDocumentation.AppendLine();
                
                // 為每一批表格生成文檔
                int batchNumber = 1;
                foreach (var batch in batches)
                {
                    // 更新進度
                    progressCallback?.Invoke(
                        $"處理批次 {batchNumber}/{batches.Count} (包含 {batch.Count} 個表格)...", 
                        ++currentStep, 
                        totalSteps);
                    
                    // 生成這一批表格的文檔
                    string batchDocumentation = await GenerateDocumentationForTablesAsync(batch, false);
                    
                    // 添加到完整文檔中
                    fullDocumentation.AppendLine(batchDocumentation);
                    
                    // 在批次之間添加分隔
                    if (batchNumber < batches.Count)
                    {
                        fullDocumentation.AppendLine();
                    }
                    
                    batchNumber++;
                }
                
                // 通知完成
                progressCallback?.Invoke("文檔生成完成！", totalSteps, totalSteps);
                
                return fullDocumentation.ToString();
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"錯誤: {ex.Message}", 0, 1);
                throw new Exception($"生成文檔時發生錯誤: {ex.Message}", ex);
            }
        }
        
        // 將表格分成批次
        private List<List<TableInfo>> SplitTablesIntoBatches(List<TableInfo> tables)
        {
            List<List<TableInfo>> batches = new List<List<TableInfo>>();
            
            // 按表格及其列的數量排序，這樣可以更均衡地分配工作量
            var sortedTables = tables.OrderByDescending(t => t.Columns.Count).ToList();
            
            // 當前批次
            List<TableInfo> currentBatch = new List<TableInfo>();
            int currentBatchSize = 0;
            
            foreach (var table in sortedTables)
            {
                // 估計此表格的「大小」（表格名稱長度 + 列數）
                int tableSize = table.Name.Length + table.Columns.Count * 20; // 粗略估計每列需要20個字符
                
                // 如果當前批次已滿或此表格會使批次過大
                if (currentBatch.Count >= MAX_TABLES_PER_BATCH || 
                    (currentBatchSize + tableSize) > MAX_PROMPT_LENGTH && currentBatch.Count > 0)
                {
                    // 將當前批次添加到批次列表中
                    batches.Add(new List<TableInfo>(currentBatch));
                    
                    // 開始一個新的批次
                    currentBatch.Clear();
                    currentBatchSize = 0;
                }
                
                // 將表格添加到當前批次
                currentBatch.Add(table);
                currentBatchSize += tableSize;
            }
            
            // 添加最後一個批次（如果有）
            if (currentBatch.Count > 0)
            {
                batches.Add(new List<TableInfo>(currentBatch));
            }
            
            return batches;
        }
        
        // 生成數據庫概述
        private async Task<string> GenerateDatabaseOverviewAsync(List<TableInfo> tables)
        {
            try
            {
                // 構建請求URL
                string url = $"{BaseUrl}{_apiSettings.ModelName}:generateContent?key={_apiSettings.ApiKey}";

                // 構建提示詞
                StringBuilder prompt = new StringBuilder();
                prompt.AppendLine("請為以下資料庫結構生成一個簡要的概述，使用Markdown格式。");
                prompt.AppendLine("只需生成資料庫的總體概述和表格之間的關係，不需要詳細描述每個表格和列。");
                prompt.AppendLine("請使用繁體中文回應。");
                prompt.AppendLine();
                prompt.AppendLine("資料庫結構如下：");
                prompt.AppendLine();

                foreach (var table in tables)
                {
                    prompt.AppendLine($"表名: {table.Name}");
                    if (!string.IsNullOrEmpty(table.Schema))
                    {
                        prompt.AppendLine($"結構: {table.Schema}");
                    }
                    
                    // 僅列出主鍵和外鍵，以減少提示詞長度
                    var primaryKeys = table.Columns.FindAll(c => c.IsPrimaryKey && c.IsSelected);
                    var foreignKeys = table.Columns.FindAll(c => c.IsForeignKey && c.IsSelected);
                    
                    if (primaryKeys.Count > 0)
                    {
                        prompt.AppendLine("主鍵:");
                        foreach (var column in primaryKeys)
                        {
                            prompt.AppendLine($"  - {column.Name}");
                        }
                    }
                    
                    if (foreignKeys.Count > 0)
                    {
                        prompt.AppendLine("外鍵:");
                        foreach (var column in foreignKeys)
                        {
                            prompt.AppendLine($"  - {column.Name} -> {column.ForeignKeyTable}.{column.ForeignKeyColumn}");
                        }
                    }
                    
                    prompt.AppendLine();
                }

                prompt.AppendLine("請生成：");
                prompt.AppendLine("1. 資料庫名稱和用途的概述");
                prompt.AppendLine("2. 表格之間的關係摘要");
                prompt.AppendLine("3. 整體資料庫結構的簡要說明");
                
                // 構建請求內容
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt.ToString() }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = Temperature
                    }
                };

                // 發送請求（注意頻率限制）
                return await MakeApiRequestWithRateLimitAsync(url, requestBody);
            }
            catch (Exception ex)
            {
                throw new Exception($"生成資料庫概述時發生錯誤: {ex.Message}", ex);
            }
        }

        // 為一組表格生成文檔
        private async Task<string> GenerateDocumentationForTablesAsync(List<TableInfo> tables, bool includeOverview = true)
        {
            try
            {
                // 構建請求URL
                string url = $"{BaseUrl}{_apiSettings.ModelName}:generateContent?key={_apiSettings.ApiKey}";

                // 構建提示詞
                string prompt = BuildDatabaseDocumentationPrompt(tables, includeOverview);

                // 構建請求內容
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = Temperature
                    }
                };

                // 發送請求（注意頻率限制）
                return await MakeApiRequestWithRateLimitAsync(url, requestBody);
            }
            catch (Exception ex)
            {
                throw new Exception($"生成表格文檔時發生錯誤: {ex.Message}", ex);
            }
        }
        
        // 發送 API 請求，並處理頻率限制
        private async Task<string> MakeApiRequestWithRateLimitAsync(string url, object requestBody)
        {
            bool shouldRetry = true;
            int retryCount = 0;
            const int maxRetries = 5;
            
            while (shouldRetry && retryCount < maxRetries)
            {
                // 檢查並等待 API 限制
                await WaitForApiThrottleAsync();
                
                try
                {
                    // 發送請求
                    string jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, content);
                    
                    // 增加 API 請求計數
                    IncrementApiRequestCount();
                    
                    // 處理響應
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                        
                        // 嘗試獲取生成的文本
                        try
                        {
                            return responseObject.candidates[0].content.parts[0].text;
                        }
                        catch
                        {
                            throw new Exception("無法解析API響應");
                        }
                    }
                    else if ((int)response.StatusCode == 429) // 頻率限制錯誤
                    {
                        retryCount++;
                        string errorResponse = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"API 頻率限制錯誤，等待後重試 ({retryCount}/{maxRetries}): {errorResponse}");
                        
                        // 強制等待一段時間
                        await Task.Delay(API_COOLDOWN_MS);
                    }
                    else
                    {
                        string errorResponse = await response.Content.ReadAsStringAsync();
                        throw new Exception($"API請求失敗: {response.StatusCode}, {errorResponse}");
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("rate limit") || ex.Message.Contains("quota"))
                {
                    // 捕獲頻率限制相關的異常
                    retryCount++;
                    Console.WriteLine($"API 頻率限制異常，等待後重試 ({retryCount}/{maxRetries}): {ex.Message}");
                    
                    // 強制等待一段時間
                    await Task.Delay(API_COOLDOWN_MS);
                }
                catch (Exception ex)
                {
                    // 其他異常不重試
                    throw ex;
                }
            }
            
            throw new Exception($"達到最大重試次數 ({maxRetries})，API 請求失敗");
        }
        
        // 增加 API 請求計數
        private void IncrementApiRequestCount()
        {
            lock (_apiLock)
            {
                // 檢查是否需要重置計數
                if (DateTime.Now > _apiRequestResetTime.AddMinutes(1))
                {
                    _apiRequestCount = 0;
                    _apiRequestResetTime = DateTime.Now;
                }
                
                // 增加計數
                _apiRequestCount++;
            }
        }
        
        // 等待 API 頻率限制
        private async Task WaitForApiThrottleAsync()
        {
            bool needToWait = false;
            int waitTime = 0;
            
            lock (_apiLock)
            {
                // 檢查是否需要重置計數
                if (DateTime.Now > _apiRequestResetTime.AddMinutes(1))
                {
                    _apiRequestCount = 0;
                    _apiRequestResetTime = DateTime.Now;
                    return; // 已重置，不需要等待
                }
                
                // 如果達到限制，計算需要等待的時間
                if (_apiRequestCount >= API_RATE_LIMIT_PER_MINUTE)
                {
                    TimeSpan timeUntilReset = _apiRequestResetTime.AddMinutes(1) - DateTime.Now;
                    waitTime = (int)Math.Ceiling(timeUntilReset.TotalMilliseconds) + 100; // 加 100ms 額外緩衝
                    needToWait = true;
                }
            }
            
            // 如果需要等待，則等待適當的時間
            if (needToWait && waitTime > 0)
            {
                Console.WriteLine($"API 請求已達限制，等待 {waitTime/1000.0:F2} 秒後繼續...");
                await Task.Delay(waitTime);
                
                // 等待後重置計數器
                lock (_apiLock)
                {
                    _apiRequestCount = 0;
                    _apiRequestResetTime = DateTime.Now;
                }
            }
        }

        // 構建資料庫文檔提示詞
        private string BuildDatabaseDocumentationPrompt(List<TableInfo> tables, bool includeOverview = true)
        {
            StringBuilder prompt = new StringBuilder();
            
            if (includeOverview)
            {
                prompt.AppendLine("請為以下資料庫結構生成詳細的Markdown格式文檔。文檔應包含每個表的用途和每個列的精簡描述。");
                prompt.AppendLine("請使用繁體中文回應，並以Markdown格式輸出。");
            }
            else
            {
                prompt.AppendLine("請為以下表格生成詳細的Markdown格式文檔。只需描述這些特定表格，不需要重複資料庫概述。");
                prompt.AppendLine("請使用繁體中文回應，並以Markdown格式輸出。");
            }
            
            prompt.AppendLine();
            prompt.AppendLine("資料庫結構如下：");
            prompt.AppendLine();

            foreach (var table in tables)
            {
                prompt.AppendLine($"表名: {table.Name}");
                if (!string.IsNullOrEmpty(table.Schema))
                {
                    prompt.AppendLine($"結構: {table.Schema}");
                }
                prompt.AppendLine("列:");

                foreach (var column in table.Columns.FindAll(c => c.IsSelected))
                {
                    prompt.AppendLine($"  - {column.Name} ({column.DataType})" +
                        $"{(column.IsNullable ? ", 可為空" : ", 非空")}" +
                        $"{(column.IsPrimaryKey ? ", 主鍵" : "")}" +
                        $"{(column.IsForeignKey ? $", 外鍵參考 {column.ForeignKeyTable}.{column.ForeignKeyColumn}" : "")}");
                }

                prompt.AppendLine();
            }

            if (includeOverview)
            {
                prompt.AppendLine("請為以上資料庫結構生成精簡的文檔，包含以下內容：");
                prompt.AppendLine("1. 資料庫概述");
                prompt.AppendLine("2. 每個表的精簡說明，包括其用途和與其他表的關係");
                prompt.AppendLine("3. 每個列的精簡說明，包括其用途和資料類型的選擇原因");
                prompt.AppendLine("4. 主鍵和外鍵關係的說明");
            }
            else
            {
                prompt.AppendLine("請為以上表格生成精簡的文檔，包含以下內容：");
                prompt.AppendLine("1. 每個表的精簡說明，包括其用途和與其他表的關係");
                prompt.AppendLine("2. 每個列的精簡說明，包括其用途和資料類型的選擇原因");
                prompt.AppendLine("3. 主鍵和外鍵關係的說明");
                prompt.AppendLine("4. 不要重複資料庫概述，直接從表格說明開始");
            }

            return prompt.ToString();
        }
    }
} 