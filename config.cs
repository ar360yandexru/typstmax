using System;
using System.Collections.Generic;
using System.IO;
namespace MaximaTypstProcessor
{
    public class ConfigReader
    {
        private Dictionary<string, string> _settings;
        public ConfigReader()
        {
            _settings = new Dictionary<string, string>();
        }
        /// <summary>
        /// Загружает настройки из файла
        /// </summary>
        /// <param name="filePath">Путь к файлу настроек</param>
        public void LoadFromFile(string filePath)
        {
            if (!Path.IsPathRooted(filePath))
            {
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                filePath = Path.Combine(exeDir, filePath);
            }
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл настроек не найден: {filePath}");
            }
            _settings.Clear();
            string[] lines = File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                // Пропускаем пустые строки и комментарии
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//"))
                    continue;
                // Ищем знак равенства
                int equalIndex = trimmedLine.IndexOf('=');
                if (equalIndex > 0)
                {
                    string key = trimmedLine.Substring(0, equalIndex).Trim();
                    string value = trimmedLine.Substring(equalIndex + 1).Trim();
                    _settings[key] = value;
                }
            }
        }
        /// <summary>
        /// Получает строковое значение настройки
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            return _settings.ContainsKey(key) ? _settings[key] : defaultValue;
        }
        /// <summary>
        /// Получает булево значение настройки
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (_settings.ContainsKey(key))
            {
                string value = _settings[key].ToLower();
                return value == "true" || value == "1" || value == "yes" || value == "on";
            }
            return defaultValue;
        }
        /// <summary>
        /// Получает целочисленное значение настройки
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (_settings.ContainsKey(key) && int.TryParse(_settings[key], out int result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Проверяет наличие ключа в настройках
        /// </summary>
        public bool HasKey(string key)
        {
            return _settings.ContainsKey(key);
        }
        /// <summary>
        /// Возвращает все ключи настроек
        /// </summary>
        public IEnumerable<string> GetAllKeys()
        {
            return _settings.Keys;
        }
        /// <summary>
        /// Сохраняет настройки в файл
        /// </summary>
        public void SaveToFile(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var kvp in _settings)
                {
                    writer.WriteLine($"{kvp.Key}={kvp.Value}");
                }
            }
        }
        /// <summary>
        /// Устанавливает значение настройки
        /// </summary>
        public void SetValue(string key, string value)
        {
            _settings[key] = value;
        }
    }
}