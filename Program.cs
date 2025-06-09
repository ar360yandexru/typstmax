using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MaximaTypstProcessor
{
    class Program
    {
        static string maximaPath;
        static string typstPath;
        static string sumatraPdfPath;
        static bool openPdf;
        static void Main(string[] args)
        {
            string inputFile = "test.typmax";
            string outputFile = "";

            if (args.Length > 0)
                inputFile = args[0];
            if (args.Length > 1)
                outputFile = args[1];
            else
                outputFile = inputFile.Replace(".typmax",".typ");

            // Создаем экземпляр класса
            ConfigReader config = new ConfigReader();

            // Загружаем настройки из файла
            config.LoadFromFile("config.conf");

            // Читаем настройки
            maximaPath = config.GetString("maxima_path");
            typstPath = config.GetString("typst_path");
            sumatraPdfPath = config.GetString("pdfviewer_path");
            openPdf = config.GetBool("open_pdf");




            try
            {
                Console.WriteLine($"Обработка файла: {inputFile}");
                ProcessFile(inputFile, outputFile);
                Console.WriteLine($"Результат сохранен в: {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Environment.Exit(1);
            }

            
        }

        static void ProcessFile(string inputFile, string outputFile)
        {
            if (!File.Exists(inputFile))
                throw new FileNotFoundException($"Файл не найден: {inputFile}");

            string[] lines = File.ReadAllLines(inputFile, Encoding.UTF8);
            List<string> outputLines = new List<string>();
            List<string> maximaHistory = new List<string>();
            int tempFileCounter = 1;

            // Базовые настройки Maxima
            string maximaHeader = @"display2d: false$
linel: 1000$      
ratprint: false$  
fpprintprec: 8$
myexplode(x) ::= block (simp:false,return(x), simp:true)$";

            foreach (string line in lines)
            {
                if (line.IndexOf("#m ")>=0)
                {
                    string symbol = "#m ";
                    string maximaCommand = line.Substring(line.IndexOf(symbol) + symbol.Length);
                    Console.WriteLine($"{tempFileCounter:D3}: {maximaCommand}");
                    string result = ExecuteMaxima(maximaHeader, maximaHistory, maximaCommand, tempFileCounter);
                    string processedResult = TypstString(result);
                    processedResult = $"${processedResult}$";
                    if (processedResult == "$$") processedResult = "";

                    string newline = line.Replace("#m "+maximaCommand, $"{processedResult} //{maximaCommand}");
                    outputLines.Add(newline);
                    maximaHistory.Add(maximaCommand.Replace(";","$"));
                    tempFileCounter++;
                }
                else if (line.IndexOf("#m+ ") >= 0)
                {
                    string symbol = "#m+ ";
                    string maximaCommand = line.Substring(line.IndexOf(symbol) + symbol.Length);
                    Console.WriteLine($"{tempFileCounter:D3}: {maximaCommand}");
                    string result = ExecuteMaxima(maximaHeader, maximaHistory, maximaCommand, tempFileCounter);
                    string processedResult = TypstString(result);
                    string maximaCommand2 = maximaCommand;
                    if (maximaCommand.IndexOf(":") >= 0)
                    {
                        maximaCommand2 = maximaCommand.Substring(0, maximaCommand.IndexOf(":"));
                    }
                    string newline = line.Replace(symbol + maximaCommand, $"${TypstString(maximaCommand2)} = {processedResult}$ //{maximaCommand}");
                    outputLines.Add(newline);
                    maximaHistory.Add(maximaCommand.Replace(";", "$"));
                    tempFileCounter++;
                }
                else if (line.IndexOf("#m= ") >= 0)
                {
                    string symbol = "#m= ";
                    string maximaCommand = line.Substring(line.IndexOf(symbol) + symbol.Length);
                    Console.WriteLine($"{tempFileCounter:D3}: {maximaCommand}");
                    string result = ExecuteMaxima(maximaHeader, maximaHistory, maximaCommand, tempFileCounter);
                    string processedResult = TypstString(result);
                    string newline = line.Replace(symbol + maximaCommand, $"${TypstString(maximaCommand)} = {processedResult}$ //{maximaCommand}");
                    outputLines.Add(newline);
                    maximaHistory.Add(maximaCommand.Replace(";", "$"));
                    tempFileCounter++;
                }

                else if (line.IndexOf("#m== ") >= 0)
                {
                    string symbol = "#m== ";
                    string maximaCommand = line.Substring(line.IndexOf(symbol) + symbol.Length);
                    Console.WriteLine($"{tempFileCounter:D3}: {maximaCommand}");
                    string result = ExecuteMaxima(maximaHeader, maximaHistory, maximaCommand, tempFileCounter);
                    string processedResult = TypstString(result);
                    string result2 = ExecuteMaxima(maximaHeader, maximaHistory, "myexplode(" + maximaCommand.Replace(";","").Replace("$", "") + ");", tempFileCounter);
                    string processedResult2 = TypstString(result2);

                    string newline = line.Replace(symbol + maximaCommand, $"${TypstString(maximaCommand)} = {processedResult2} = {processedResult}$ //{maximaCommand}");
                    outputLines.Add(newline);
                    maximaHistory.Add(maximaCommand.Replace(";", "$"));
                    tempFileCounter++;
                }



                else
                {
                    outputLines.Add(line);
                }
            }

            // Сохраняем результат
            File.WriteAllLines(outputFile, outputLines, Encoding.UTF8);
            ExecuteTypst(outputFile);

            // Очищаем временные файлы
            CleanupTempFiles();
        }

        static string ExecuteMaxima(string header, List<string> history, string command, int fileCounter)
        {
            try
            {
                string tempFileName = $"temp_{fileCounter:D3}.mac";

                // Создаем содержимое файла
                StringBuilder content = new StringBuilder();
                content.AppendLine(header);

                // Добавляем историю команд
                foreach (string historyCommand in history)
                {
                    content.AppendLine(historyCommand);
                }

                // Добавляем текущую команду
                content.AppendLine(command);
 
                // Записываем временный файл
                File.WriteAllText(tempFileName, content.ToString(), new UTF8Encoding(false));

                // Выполняем Maxima
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c type {tempFileName} | {maximaPath} --very-quiet",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = new UTF8Encoding(false)
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();

                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                     
                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"Предупреждение Maxima: {error}");
                    }

                    // Извлекаем последнюю значимую строку
                    return ExtractLastResult(output);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения Maxima: {ex.Message}");
                return "";
            }
        }

        static void ExecuteTypst(string filename)
        {
            try
            {
                string newfilename = filename.Replace(".typ",".pdf");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {typstPath}  compile \"{filename}\" \"{newfilename}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = new UTF8Encoding(false)
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();

                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"Предупреждение Typst: {error}");
                    }
                    else {
                        if (openPdf)
                        {
                            ProcessStartInfo startInfo2 = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/c {sumatraPdfPath} \"{newfilename}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true,
                                StandardOutputEncoding = new UTF8Encoding(false)
                            };
                            Process.Start(startInfo2);
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка выполнения Typst: {ex.Message}");
            }
        }
        static string ExtractLastResult(string output)
        {
            if (string.IsNullOrEmpty(output))
                return "";

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Ищем последнюю непустую строку, которая не является служебной
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line)) 
                {
                    return line;
                }
            }

            return lines.LastOrDefault();
        }

        static string TypstString(string result)
        {
            if (string.IsNullOrEmpty(result))
                return result;

            

            return Regex.Replace(result, @"\p{L}[\p{L}\p{N}]*", "\"$&\"").Replace("*", " dot ").Replace(";", "").Replace(":", "=");

 
        }

 

        static void CleanupTempFiles()
        {
            try
            {
                string[] tempFiles = Directory.GetFiles(".", "temp_*.mac");
                foreach (string file in tempFiles)
                {
                   File.Delete(file);
                }
               // Console.WriteLine($"Удалено {tempFiles.Length} временных файлов");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Предупреждение: не удалось очистить временные файлы: {ex.Message}");
            }
        }
    }
}