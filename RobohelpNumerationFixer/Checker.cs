using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RobohelpNumerationFixer
{
    public class Checker
    {
        /// <summary>Check if any files from the list are missing and if they are, exclude them from the list</summary>
        public static bool CheckMissingFiles(ref List<string> files)
        {
            bool filesMissing = false;
            
            foreach (string filePath in files)
            {
                if (File.Exists(filePath)) { continue; }
                
                Console.WriteLine($"Файл '{filePath}' не найден.");
                files.Remove(filePath);
                filesMissing = true;
            }

            return filesMissing;
        }

        /// <summary>Check HTML pages for miscellaneous issues with pictures labels</summary>
        public static bool CheckPicLabels(List<string> htmlFiles)
        {
            bool miscIssues = false;

            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();  
                htmlDoc.Load(filePath); 

                HtmlNodeCollection picLabelNodes = htmlDoc.DocumentNode.SelectNodes(".//p[@class='Рисунокподпись' or @class='РисунокподписьЗнак']");
                if (picLabelNodes == null) { continue; }

                foreach (HtmlNode labelNode in picLabelNodes)
                {
                    if (!Regex.Match(labelNode.InnerText, @"ис\.\s?\d+").Success)
                    {
                        if (Regex.Match(labelNode.InnerText, @"ис\.\s?(?!\d+)").Success)
                        {
                            Console.WriteLine($"\nВ файле '{filePath}' содержится 'Рис.' без номера. Возможно, номер находится в следующем абзаце. Во время исправления этот случай будет пропущен.");
                            miscIssues = true;
                        }
                        continue;
                    }
                }
            }

            return miscIssues;
        }
        
        /// <summary>Check HTML pages for duplicated picture numbers and if found, return them</summary>
        public static List<int> GetDuplicates(List<string> htmlFiles)
        {
            List<int> existingPictures = new List<int>();
            List<string> picsPaths = new List<string>();   

            List<int> duplicateNumbers = new List<int>(); 

            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();  
                htmlDoc.Load(filePath); 

                HtmlNodeCollection picLabelNodes = htmlDoc.DocumentNode.SelectNodes(".//p[@class='Рисунокподпись' or @class='РисунокподписьЗнак']");
                if (picLabelNodes == null) { continue; }

                foreach (HtmlNode labelNode in picLabelNodes)
                {
                    if (!Regex.Match(labelNode.InnerText, @"ис\.?\s?\d+").Success) { continue; }

                    int picNumber = Convert.ToInt32(Regex.Match(labelNode.InnerText, @"(?<=(ис\.?\s?))\d+").Value);
                    
                    if (!duplicateNumbers.Contains(picNumber) && existingPictures.Contains(picNumber)) 
                    { 
                        duplicateNumbers.Add(picNumber); 
                    }
                    
                    existingPictures.Add(picNumber);
                    picsPaths.Add(filePath);
                }
            }

            duplicateNumbers.Sort();

            foreach (int num in duplicateNumbers)
            {
                Console.WriteLine($"\nКартинки с одинаковым именем 'Рис.{num}' найдены в следующих файлах:");
                for (int i = 0; i < existingPictures.Count; i++)
                {
                    if (existingPictures[i] == num) { Console.WriteLine(picsPaths[i]); }
                }
            }

            return duplicateNumbers;
        }

        /// <summary>Check HTML pages for "Рисуноккартинка" class without an image</summary>
        public static bool CheckEmptyPictures(List<string> htmlFiles)
        {
            bool hasEmptyPictures = false;

            Console.WriteLine();
            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();  
                htmlDoc.Load(filePath); 

                HtmlNodeCollection pictureNodes = htmlDoc.DocumentNode.SelectNodes(".//p[@class='Рисуноккартинка']");
                if (pictureNodes == null) { continue; }

                foreach (HtmlNode picNode in pictureNodes)
                {
                    if (Regex.Match(picNode.InnerHtml, @"\<img").Success) { continue; }

                    Console.WriteLine($"В файле '{filePath}' содержится HTML элемент 'Рисуноккартинка' без изображения.");
                    hasEmptyPictures = true;
                }
            }
            Console.WriteLine();

            return hasEmptyPictures;
        }
    }
}