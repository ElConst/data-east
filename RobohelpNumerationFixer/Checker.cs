using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RobohelpNumerationFixer
{
    public static class Checker
    {
        /// <summary>Checks if any files in a list are missing and if they are, excludes them from the list</summary>
        public static void CheckMissingFiles(ref List<string> files, bool consoleWarnings = false)
        {
            if (consoleWarnings)
            {
                foreach (string filePath in files)
                {
                    if (!File.Exists(filePath)) 
                    { 
                        Program.Log($"Файл '{filePath}' не найден.");
                        Program.Log();
                    }
                }
            }

            files.RemoveAll(f => !File.Exists(f));
        }

        /// <summary>Checks HTML pages for "Рисуноккартинка" classes without images</summary>
        public static void CheckEmptyPictures(List<string> htmlFiles)
        {
            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();  
                htmlDoc.Load(filePath); 

                HtmlNodeCollection pictureNodes = htmlDoc.DocumentNode.SelectNodes(".//p[@class='Рисуноккартинка']");
                if (pictureNodes == null) { continue; }

                foreach (HtmlNode picNode in pictureNodes)
                {
                    if (Regex.Match(picNode.InnerHtml, @"\<img").Success) { continue; }

                    Program.Log($"В файле '{filePath}' содержится HTML элемент 'Рисуноккартинка' без изображения.");
                    Program.Log();
                }
            }
        }

        /// <summary>Checks HTML pages for miscellaneous issues with pictures labels</summary>
        public static void CheckPicLabels(List<string> htmlFiles)
        {
            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();  
                htmlDoc.Load(filePath); 

                string xPath = ".//p[@class='Рисунокподпись' or @class='РисунокподписьЗнак']";
                HtmlNodeCollection picLabelNodes = htmlDoc.DocumentNode.SelectNodes(xPath);
                if (picLabelNodes == null) { continue; }

                foreach (HtmlNode labelNode in picLabelNodes)
                {
                    if (Regex.Match(labelNode.InnerText, @"ис\.\s?\d+").Success) { continue; } // No issues
                    
                    if (Regex.Match(labelNode.InnerText, @"ис\.\s?(?!\d+)").Success)
                    {
                        Program.Log($"В файле '{filePath}' содержится 'Рис.' без номера. Возможно, номер находится в следующем абзаце. Во время исправления этот случай будет пропущен.");
                        Program.Log();
                    }
                }
            }
        }
        
        /// <summary>Checks HTML pages for duplicated picture numbers</summary>
        /// <returns>Duplicated numbers</returns>
        public static List<int> GetDuplicates(List<string> htmlFiles, bool consoleWarnings = false)
        {
            List<int> pictureNumbers = new List<int>();
            List<string> picsPaths = new List<string>();   

            List<int> duplicateNumbers = new List<int>(); 

            foreach (string filePath in htmlFiles)
            {
                HtmlDocument htmlDoc = new HtmlDocument();  
                htmlDoc.Load(filePath); 

                // Select picture labels
                string xPath = ".//p[@class='Рисунокподпись' or @class='РисунокподписьЗнак']";
                HtmlNodeCollection picLabelNodes = htmlDoc.DocumentNode.SelectNodes(xPath);
                var query = picLabelNodes.AsEnumerable()?.Where(node => Regex.Match(node.InnerText, @"ис\.?\s?\d+").Success);

                if (query == null) { continue; }

                // Add repetitive picture numbers to the duplicates list
                foreach (HtmlNode labelNode in query)
                {
                    int picNumber = Convert.ToInt32(Regex.Match(labelNode.InnerText, @"(?<=(ис\.?\s?))\d+").Value);
                    
                    if (!duplicateNumbers.Contains(picNumber) && pictureNumbers.Contains(picNumber)) 
                    { 
                        duplicateNumbers.Add(picNumber); 
                    }

                    pictureNumbers.Add(picNumber);
                    picsPaths.Add(filePath);
                }
            }

            duplicateNumbers.Sort();
            
            if (consoleWarnings)
            {
                foreach (int num in duplicateNumbers)
                {
                    Program.Log($"Картинки с именем 'Рис.{num}' найдены в следующих файлах:");
                    for (int i = 0; i < pictureNumbers.Count; i++)
                    {
                        if (pictureNumbers[i] == num) { Program.Log(picsPaths[i]); }
                    }
                    Program.Log();
                }
            }            

            return duplicateNumbers;
        }
    }
}