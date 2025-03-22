using System;

namespace DataBaseMarkDown.Models
{
    /// <summary>
    /// Gemini API設定
    /// </summary>
    public class ApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ModelName { get; set; } = "gemini-2.0-flash";
    }
} 