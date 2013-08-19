using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace HapScrapper
{
    class Program
    {
        class FNAIMEntry
        {
            public string Titre { get; set; }
            public string Description {get; set;}
            public string Surface {get; set;}
            public string Prix {get; set;}
            public string Charges { get; set; }
            public string Reference { get; set; }
            public string TypeDeBien { get; set; }
            public int NomDePieces { get; set; }
            //TODO: compléter éventuellement avec les champs manquants
        }


        interface IProcessor
        {
            void ProcessElement(HtmlNode node, FNAIMEntry entry);
        }

        class RegexProcessor: IProcessor
        {
            public Regex _rx { get; set; }
            public Action<FNAIMEntry, MatchCollection> _action { get; set; }
            private string nodeContent;

            public RegexProcessor(Regex rx, Action<FNAIMEntry, MatchCollection> action)
            {
                _rx = rx;
                _action = action;
            }

            public void ProcessElement(HtmlNode node, FNAIMEntry entry)
            {
                var collection = _rx.Matches(node.InnerHtml);
                _action(entry, collection);
            }
        }

        class HAPProcessor: IProcessor
        {
            public Action<FNAIMEntry, HtmlNode> _action { get; set; }

            public HAPProcessor(Action<FNAIMEntry, HtmlNode> action)
            {
                _action = action;
            }

            public void ProcessElement(HtmlNode node, FNAIMEntry entry)
            {
                try
                {
                    _action(entry, node);
                }
                catch {
                    Console.WriteLine("Oops");
                }
                
            }
        }

        static List<IProcessor> processingActions = new List<IProcessor>();

        static void Add(string regex, Action<FNAIMEntry, MatchCollection> action)
		{
			var key = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace|RegexOptions.Multiline);
            processingActions.Add(new RegexProcessor(key, action));
		}

        static void Add(Action<FNAIMEntry, HtmlNode> action)
        {
            processingActions.Add(new HAPProcessor(action));
        }

        static void Main(string[] args)
        {
            Add(@"(\d+)\s*m²", (entry, collection) => { // surface
                if (collection.Count == 0)
					return;
				var value = collection[0].Groups[1].Value;
                entry.Surface = value;
            });

            Add(@"([\d\s]+) &euro;", (entry, collection) => // prix
            {
                if (collection.Count == 0)
                    return;
                var value = collection[0].Groups[1].Value.Trim();
                entry.Prix = value;
            });

            Add((entry, node) => // Titre
            {
                entry.Titre = node.SelectSingleNode("./h3/a").InnerText;
            });

            Add((entry, node) =>
            {
                entry.Description = node.SelectSingleNode("./p[@class='resume']/a").InnerText;
            });

            Add((entry, node) =>
                {
                    string hrefContent = node.SelectSingleNode("./h3/a").Attributes["href"].Value;
                    entry.Reference = hrefContent.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries)[1];
                });

            Add((entry, node) =>
            {
                string typeDeBien = node.SelectSingleNode("./h3/a").InnerHtml;
                entry.TypeDeBien = typeDeBien.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries)[0];
            });

            Add(@"(\d+)\s*pièce\(s\)", (entry, collection) =>
            { 
                if (collection.Count == 0)
                    return;
                var value = collection[0].Groups[1].Value;
                entry.NomDePieces = Convert.ToInt32(value);
            });
            //TODO: compléter éventuellement avec des règles de traitement


            // résultats
            var resultats = new List<FNAIMEntry>();



            // TODO: replace id_localite with info from ws: http://www.fnaim.fr/include/ajax/ajax.localite.autocomplete.php?term=<code_postal>
            var identifiantLocaliteFNAIM = "32926";
            string url = "http://www.fnaim.fr/18-louer.htm?ID_LOCALITE={0}&TYPE[]=1&TYPE[]=2&PRIX_MIN=&PRIX_MAX=&ip={1}";
            bool nextPageAvailable = true;
            int currentPage = 1;
            while (nextPageAvailable)
            {
                string pagedUrl = string.Format(url, identifiantLocaliteFNAIM, currentPage);
                HtmlWeb web = new HtmlWeb();
                HtmlAgilityPack.HtmlDocument d = web.Load(pagedUrl);

                int itemCount = 0;

                foreach(HtmlNode item in d.DocumentNode.SelectNodes("//div[@class='itemContent']"))
                {
                    itemCount++;
                    
                    try
                    {
                        var FNAIMItem = new FNAIMEntry();

                        foreach (var action in processingActions)
                        {
                            action.ProcessElement(item, FNAIMItem);
                        }

                        resultats.Add(FNAIMItem);

                        Console.WriteLine("Item: " + FNAIMItem.Titre);
                        Console.WriteLine("\\tSurface " + FNAIMItem.Surface);
                        Console.WriteLine("\\tPrix " + FNAIMItem.Prix);
                        Console.WriteLine("\\tDescription " + FNAIMItem.Description);

                        //TODO: sauvegarder en base de données
                    }
                    catch (Exception e)
                    {
                        //TODO: Sauvegarder une erreur en base de données
                        Console.WriteLine();
                        Console.WriteLine("-------------ERROR-------------");
                        Console.WriteLine(item.InnerHtml);
                        Console.WriteLine(e);
                        throw;
                    }
                }

                nextPageAvailable = d.DocumentNode.SelectNodes("//div[@id='centre']/div[contains(@class, 'blocNavigation')]/div[contains(@class, 'prevnext')]/a[contains(@class, 'next')]") != null;// itemCount > 0;
                currentPage++;
            }

            Console.WriteLine("Résultats " + resultats.Count);
        }
    }
}
