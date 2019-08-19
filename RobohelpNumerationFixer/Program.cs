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

        /// <summary>Open "CoGIS 7.0.hhc" and get links to HTML filess</summary>
        private static List<string> GetFilesListFromXML(string rootFolderPath)
        {
            XDocument mainXmlDoc = XDocument.Load(rootFolderPath + @"\CoGIS 7.0.hhc");
            
            var items = mainXmlDoc.Descendants("item").ToArray();
            var filesPaths = from item in items 
                            where item.Attribute("link") != null
                            select $@"{rootFolderPath}\{item.Attribute("link").Value}";

            return filesPaths.ToList();
        }

        private static void Main(string[] args)
        {
            #region Data initialization
            string rootFolderPath = @"D:\CoGIS 7.0";

            Console.WriteLine(@"Введите путь к папке с документацией, например 'C:\Users\username\Documents\CoGIS 7.0'");
            rootFolderPath = Console.ReadLine();

            if (!File.Exists(rootFolderPath + @"\CoGIS 7.0.hhc")) 
            {
                Console.WriteLine($"\nОшибка: файл 'CoGIS 7.0.hhc' не был найден.");
                Console.Read();
                return; 
            }

            List<string> htmlFiles = GetFilesListFromXML(rootFolderPath);
            #endregion

            Console.WriteLine("Проверка...");

            Checker.CheckMissingFiles(ref htmlFiles);
            Checker.CheckEmptyPictures(htmlFiles);
            Checker.CheckPicLabels(htmlFiles);
            List<int> duplicateNumbers = Checker.GetDuplicates(htmlFiles);

            Console.WriteLine("\nВведите цифру, соответствующую вашему выбору:");
            Console.WriteLine("1. Начать исправление нумерации\n2. Выйти из программы");

            ConsoleKeyInfo input;
            do
            {
                input = Console.ReadKey();
                if (input.Key == ConsoleKey.D2) { return; }
            } while (input.Key != ConsoleKey.D1);

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

