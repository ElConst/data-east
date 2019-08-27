using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace RobohelpNumerationFixer
{
    public static class Program
    {
        #region Help text
        private const string HelpText =
@"
Проверяет нумерацию картинок в Robohelp документации и исправляет на сквозную.

Опции и параметры:
path_name           : путь к файлу .hhc, в котором содержатся ссылки на HTML 
                      страницы
--out=path_name
-o=path_name        : выводить информацию в файл path_name вместо консоли
--check -с          : режим проверки (установлен по умолчанию)
--fix -f            : режим изменений
--help -h           : вывести помощь
--wait -w           : ждать ввода с клавиатуры перед выходом из программы
";
        #endregion

        // This class contains all needed information about a picture
        private class Picture
        {
            public string File;
            public int OldNumber;
            public int NewNumber;
            public string Key => $"#{NewNumber}#";

            public Picture(string filePath, int oldNumber, int newNumber)
            {
                File = filePath;
                OldNumber = oldNumber;
                NewNumber = newNumber;
            }
        }

        /// <summary>Opens XML .hhc file and reads paths to HTML pages from it</summary>
        /// <returns>Links to HTML files</returns>
        private static List<string> GetFilesListFromXML(string hhcPath)
        {
            string rootFolder = Path.GetDirectoryName(hhcPath);

            XDocument mainXmlDoc = XDocument.Load(hhcPath);
            
            var items = mainXmlDoc.Descendants("item");
            var filesPaths = from item in items 
                            where item.Attribute("link") != null
                            select $@"{rootFolder}\{item.Attribute("link").Value}";

            return filesPaths.ToList();
        }

        private static bool logToFile = false;
        private static string logFilePath = "";
        
        /// <summary>Prints a message in console or appends this message to an output file if it was specified</summary>
        public static void Log(string message = "")
        {
            if (!logToFile)
            {
                Console.WriteLine(message);
                return;
            }

            if (logFilePath == "") { return; }

            try
            {
                using (StreamWriter streamWriter = new StreamWriter(logFilePath, true))
                {
                    streamWriter.WriteLine($"{message}");
                }
            }
            catch
            {
                Console.WriteLine("Возникла ошибка при попытке записать лог в файл.\n");
                logFilePath = "";
            }
        }

        private static void Main(string[] args)
        {
            bool checkMode = true;
            bool showHelp = false, waitWhenDone = false;

            // Choose program mode and set paths to needed files
            #region Initialization
            string hhcFilePath = "";
            
            if (args.Count() > 0)
            { 
                // Check all given arguments
                foreach (string arg in args)
                {
                    // Find and set path for main XML .hhc file
                    if (File.Exists(arg) && Path.GetExtension(arg) == ".hhc" && hhcFilePath == "") 
                    { 
                        hhcFilePath = arg;
                    }

                    // If the parameter for an output file is specified, place its path into a variable
                    Match outputFileMatch = Regex.Match(arg, @"(?<=((-o=)|(--out=))).*$");
                    if (outputFileMatch.Success && Directory.Exists(Path.GetDirectoryName(outputFileMatch.Value)))
                    {
                        logToFile = true;
                        logFilePath = outputFileMatch.Value;
                        if (File.Exists(logFilePath)) { File.Delete(logFilePath); }
                    }
                    
                    if (arg == "-c" || arg == "--check") { checkMode = true; }
                    else if (arg == "-f" || arg == "--fix") { checkMode = false; }

                    if (arg == "-h" || arg == "--help") { showHelp = true; }

                    if (arg == "-w" || arg == "--wait") { waitWhenDone = true; }
                }

                // Show help and close the program if the help parameter was specified
                if (showHelp)
                {
                    Log(HelpText);
                    if (waitWhenDone) { Console.Read(); }
                    return;
                }
                else if (hhcFilePath == "")
                {
                    // If main XML file wasn't found, tell about it and close the program since there's nothing to work with
                    Log("\nОшибка: .hhc файл не был найден.");
                    if (waitWhenDone) { Console.Read(); }
                    return;
                }
            }
            else
            {
                Console.WriteLine("Отсутствуют параметры запуска. Введите параметр '-h' или '--help', чтобы открыть помощь.");
                return;
            } // If there was no arguments given, close the program

            List<string> htmlFiles = GetFilesListFromXML(hhcFilePath);
            #endregion

            // If check mode was chosen, check all types off issues, tell about them and close the program
            if (checkMode)
            {
                Checker.CheckMissingFiles(ref htmlFiles, true);
                Checker.CheckEmptyPictures(htmlFiles);
                Checker.CheckPicLabels(htmlFiles);
                Checker.GetDuplicates(htmlFiles, true);

                if (waitWhenDone) { Console.Read(); }
                return;
            }

            // If fix mode was chosen, check missing files only, without any warnings
            Checker.CheckMissingFiles(ref htmlFiles);

            Log("Были изменены следующие номера:");
            Log();

            List<Picture> pictures = new List<Picture>();
            List<int> duplicateNumbers = Checker.GetDuplicates(htmlFiles);
            int currentPictureIndex = 0;

            // Get information about pictures and temporarily put their unique keys (which are correct picture numbers 
            // wrapped in hash signs) in labels to distinguish corrected ones
            foreach (string filePath in htmlFiles)
            {
                string fileText = File.ReadAllText(filePath);
                
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.Load(filePath, true); 

                // Get nodes with classes used to mark pictures labels
                string xPath = ".//p[@class='Рисунокподпись' or @class='РисунокподписьЗнак']";
                HtmlNodeCollection labelClassNodes = htmlDoc.DocumentNode.SelectNodes(xPath);
                if (labelClassNodes == null) { continue; }
                // Choose nodes with needed information
                var picLabelNodes = from node in labelClassNodes.AsEnumerable()
                                    where Regex.Match(node.InnerText, @"ис\.?\s?\d+", RegexOptions.Singleline).Success
                                    select node;

                foreach (HtmlNode labelNode in picLabelNodes)
                {
                    currentPictureIndex++;

                    // Get the number after "Рис."
                    int picOldNumber = Convert.ToInt32(Regex.Match(labelNode.InnerText, @"(?<=(ис\.?\s?))\d+", RegexOptions.Singleline).Value);
                    if (picOldNumber == currentPictureIndex) { continue; } // Skip if no numeration issues
                    
                    // Create a new Picture object with all information about current picture
                    Picture pic = new Picture(filePath, picOldNumber, currentPictureIndex);
                    pictures.Add(pic);
                    
                    // Put the key instead of old number
                    fileText = Regex.Replace(fileText, $@"(?<=(ис\.?\s?)){pic.OldNumber}", pic.Key, RegexOptions.Singleline);

                    Log($"'Рис.{pic.OldNumber}' -> 'Рис.{pic.NewNumber}' в файле {filePath}");
                    Log();
                }

                File.WriteAllText(filePath, fileText);
            }

            // Deal with duplicates first to prevent their references from getting messed
            // The idea is to replace references to each duplicate-numbered picture inside file where it is located
            foreach (int duplicate in duplicateNumbers)
            {
                foreach (Picture pic in pictures)
                {
                    if (pic.OldNumber != duplicate) { continue; }
                    ReplacePicReferences(pic, pic.File);
                }
            }

            // Replace the remaining references with unique keys
            foreach (Picture pic in pictures)
            {
                foreach (string filePath in htmlFiles)
                {
                    ReplacePicReferences(pic, filePath);
                }
            }

            // Finally replace keys with correct numbers
            foreach (Picture pic in pictures)
            {
                foreach (string filePath in htmlFiles)
                {
                    string fileText = File.ReadAllText(filePath);
                    fileText = Regex.Replace(fileText, pic.Key, $"{pic.NewNumber}", RegexOptions.Singleline);
                    File.WriteAllText(filePath, fileText);
                }
            }
            if (waitWhenDone) { Console.Read(); }
        }

        /// <summary>Replaces all "рис." references to a picture inside a file</summary>
        private static void ReplacePicReferences(Picture pic, string filePath)
        {
            string fileText = File.ReadAllText(filePath);

            string picRefPattern = $@"(?<=(рис\.\s?&#160;(\<.*\>)?)){pic.OldNumber}";
            string changedFileText = Regex.Replace(fileText, picRefPattern, pic.Key, RegexOptions.Singleline);
                    
            File.WriteAllText(filePath, changedFileText);
        }
    }
}

