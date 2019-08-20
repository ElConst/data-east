using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace RobohelpNumerationFixer
{
    public class Program
    {
        #region Help text
        private const string helpText = 
@"
Для начала работы с программой необходимо при запуске указать
в качестве первого параметра путь к XML файлу .hhc, в котором
хранится список HTML страниц с пронумерованными картинками.
Чтобы включить режим проверки, запустите программу со вторым
параметром '-c' или '--check', чтобы запустить исправление
нумерации, запустите программу с параметром '-f' или '--fix'.
По умолчанию, если этот параметр отсутствует, запустится 
режим проверки.
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

        /// <summary>Open .hhc file and get links to HTML files</summary>
        private static List<string> GetFilesListFromXML(string hhcPath, string rootFolderPath)
        {
            XDocument mainXmlDoc = XDocument.Load(hhcPath);
            
            var items = mainXmlDoc.Descendants("item").ToArray();
            var filesPaths = from item in items 
                            where item.Attribute("link") != null
                            select $@"{rootFolderPath}\{item.Attribute("link").Value}";

            return filesPaths.ToList();
        }

        private static void Main(string[] args)
        {
            bool checkMode = true;

            #region Initialization
            string rootFolderPath = "", hhcFilePath = "";
            
            if (args.Count() >= 1) 
            { 
                hhcFilePath = args[0];
                if (!File.Exists(hhcFilePath))
                {
                    Console.WriteLine($"\nОшибка: .hhc файл не был найден.");
                    Console.Read();
                    return; 
                }
                rootFolderPath = Path.GetDirectoryName(hhcFilePath);

                if (args.Count() >= 2) 
                { 
                    switch (args[1])
                    {
                        case "-f":
                        case "--fix":
                            checkMode = false;
                            break;
                        
                        case "-c":
                        case "--check":
                            break;

                        default:
                            Console.WriteLine("Неверно введен второй аргумент, запущен режим проверки.\n");
                            break;
                    }
                } 
            }
            else
            {
                Console.WriteLine(helpText);
                Console.Read();
                return;
            }

            List<string> htmlFiles = GetFilesListFromXML(hhcFilePath, rootFolderPath);
            #endregion

            if (checkMode)
            {
                Console.WriteLine("Проверка...");

                Checker.CheckMissingFiles(ref htmlFiles, true);
                Checker.GetDuplicates(htmlFiles, true);
                Checker.CheckEmptyPictures(htmlFiles);
                Checker.CheckPicLabels(htmlFiles);

                Console.Read();
                return;
            }

            Checker.CheckMissingFiles(ref htmlFiles);
            List<int> duplicateNumbers = Checker.GetDuplicates(htmlFiles);

            Console.WriteLine("\nПодождите...");

            List<Picture> pictures = new List<Picture>();
            int currentPictureIndex = 0;

            // Count pictures and put their unique keys (which are correct picture numbers 
            // wrapped in hash signs) in labels to distinguish corrected ones
            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.Load(filePath); 

                // Get elements which contain information about pictures
                HtmlNodeCollection picLabelClassNodes = htmlDoc.DocumentNode.SelectNodes(".//p[@class='Рисунокподпись' or @class='РисунокподписьЗнак']");
                if (picLabelClassNodes == null) { continue; }
                var picLabelNodes = from node in picLabelClassNodes.AsEnumerable()
                                    where Regex.Match(node.InnerText, @"ис\.?\s?\d+", RegexOptions.Singleline).Success
                                    select node;

                foreach (HtmlNode labelNode in picLabelNodes.ToList())
                {
                    currentPictureIndex++;

                    // Get the number right after "Рис."
                    int picOldNumber = Convert.ToInt32(Regex.Match(labelNode.InnerText, @"(?<=(ис\.?\s?))\d+", RegexOptions.Singleline).Value);
                    if (picOldNumber == currentPictureIndex) { continue; } // Skip if no numeration issues
                    
                    // Create a new Picture object with all information about current picture
                    Picture pic = new Picture(filePath, picOldNumber, currentPictureIndex);
                    pictures.Add(pic);
                    
                    // Put the key ("#index#") instead of old number
                    labelNode.InnerHtml = Regex.Replace(labelNode.InnerHtml, $@"(?<=(ис\.?\s?)){pic.OldNumber}", pic.Key, RegexOptions.Singleline);
                    htmlDoc.Save(filePath);
                }
            }

            // Deal with duplicates first to prevent their references from getting messed
            // The idea is to replace references to each duplicate-numbered picture inside file where it is located
            foreach (int duplicate in duplicateNumbers)
            {
                foreach (Picture pic in pictures)
                {
                    if (pic.OldNumber != duplicate) { continue; }
                    
                    string fileText = File.ReadAllText(pic.File);
                    string changedFileText = Regex.Replace(fileText, $@"(?<=(рис\.\s?&#160;(\<.*\>)?)){pic.OldNumber}", pic.Key, RegexOptions.Singleline);
                    File.WriteAllText(pic.File, changedFileText);
                }
            }

            // Replace the remaining references with unique key to know which are corrected already
            foreach (Picture pic in pictures)
            {
                foreach (string filePath in htmlFiles)
                {
                    string fileText = File.ReadAllText(filePath);
                    string changedFileText = Regex.Replace(fileText, $@"(?<=(рис\.\s?&#160;(\<.*\>)?)){pic.OldNumber}", pic.Key, RegexOptions.Singleline);
                    File.WriteAllText(filePath, changedFileText);
                }
            }

            // Finally replace picture keys with correct numbers
            foreach (Picture pic in pictures)
            {
                foreach (string filePath in htmlFiles)
                {
                    string fileText = File.ReadAllText(filePath);
                    fileText = Regex.Replace(fileText, pic.Key, $"{pic.NewNumber}", RegexOptions.Singleline);
                    File.WriteAllText(filePath, fileText);
                }
            }

            Console.WriteLine("\nКартинки пронумерованы.\n");
            Console.Read();
        }
    }
}

