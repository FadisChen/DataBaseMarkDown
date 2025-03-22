using System;
using System.Configuration;
using DataBaseMarkDown.Models;

namespace DataBaseMarkDown.Utils
{
    public class AppSettings
    {
        public ApiSettings ApiSettings { get; private set; } = new ApiSettings();

        // 單例模式
        private static AppSettings? _instance;
        public static AppSettings Instance
        {
            get
            {
                _instance ??= new AppSettings();
                return _instance;
            }
        }
        
        // 私有建構函式
        private AppSettings()
        {
            LoadFromConfig();
        }
        
        // 從 App.config 加載設定
        private void LoadFromConfig()
        {
            try
            {
                // 讀取 App.config 中的設定
                string apiKey = ConfigurationManager.AppSettings["ApiKey"] ?? "xxx";
                string modelName = ConfigurationManager.AppSettings["ModelName"] ?? "gemini-2.0-flash";
                
                // 更新 API 設定
                ApiSettings.ApiKey = apiKey;
                ApiSettings.ModelName = modelName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加載設置時出錯: {ex.Message}");
            }
        }
    }
} 