using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spider
{
    public class MakepoloCompany
    {
        public static ConcurrentQueue<KeyValuePair<string, string>> CountyPageUrlQueue = new ConcurrentQueue<KeyValuePair<string, string>>();
        public static ConcurrentQueue<KeyValuePair<string, string[]>> CompanyDetailsQueue = new ConcurrentQueue<KeyValuePair<string, string[]>>();
        public static ConcurrentDictionary<string, List<CompanyDetails>> CompanyDetailsList = new ConcurrentDictionary<string, List<CompanyDetails>>();
        static string[] CountyNames = new string[] {
            //"beijing","shanghai","guangdong","hubei","hebei",
            //"hunan","zhejiang","chongqing","liaoning","tianjin",
            //"sichuan","anhui","shandong","jiangsu" ,
            "henan"
            };
        public static int GetCountyPageUrl()
        {
            var list = new List<string>();
            foreach (var item in CountyNames)
            {
                for (int i = 0; i < 200; i++)
                {
                    CountyPageUrlQueue.Enqueue(new KeyValuePair<string, string>(item, string.Format("http://company.makepolo.com/{0}/{1}", item, i)));
                }

            }
            return CountyPageUrlQueue.Count();
        }
        public static bool DequeuePageUrl(out KeyValuePair<string, string> SourceUrl)
        {
            return CountyPageUrlQueue.TryDequeue(out SourceUrl);
        }

        public static async Task<List<string>> GetPageContextAsync(string CountyName, string SourceUrl)
        {
            var urlList = new List<string>();

            try
            {
                HtmlAgilityPack.HtmlWeb htmlWeb = new HtmlAgilityPack.HtmlWeb();
                if (string.IsNullOrWhiteSpace(SourceUrl))
                    throw new Exception("发现空地址");
                var result = await htmlWeb.LoadFromWebAsync(SourceUrl);
                var root = result.DocumentNode;
                var list = root.SelectNodes("//h2[@class='colist_item_title']");
                if (list != null)
                {
                    foreach (HtmlAgilityPack.HtmlNode item in list)
                    {
                        var val = item.SelectSingleNode("a").Attributes["href"].Value;
                        urlList.Add(val);
                    }
                    CompanyDetailsQueue.Enqueue(new KeyValuePair<string, string[]>(CountyName, urlList.ToArray()));
                }
                else
                {

                    CountyPageUrlQueue.Enqueue(new KeyValuePair<string, string>(CountyName, SourceUrl));
                    Thread.Sleep(5000);
                    throw new Exception("数据下载不完整,已重新入队.");
                }
            }
            catch (Exception ex)
            {
                Thread.Sleep(5000);
                throw new Exception(ex.Message + "(" + SourceUrl + ")");
            }
            return urlList;
        }
        public static event Action<string> HandlerMsg;

        public static bool DequeueCompanyDetailsUrl(out KeyValuePair<string, string[]> Com)
        {
            return CompanyDetailsQueue.TryDequeue(out Com);
        }
        public static async Task<List<CompanyDetails>> GetPageDetailsContextAsync(string CountyName,string CompanyUrl)
        {

            var list = new List<CompanyDetails>();

            try
            {
                var result = await GetDetailContext(CompanyUrl);
                if (CheckCompanyInfo(result))
                    list.Add(result);
            }
            catch (Exception ex)
            {
                HandlerMsg.Invoke(ex.Message);
            }

            return list;
        }
        public static async Task<CompanyDetails> GetDetailContext(string url)
        {
            var CDetails = new CompanyDetails();

            try
            {

                HtmlAgilityPack.HtmlWeb htmlWeb = new HtmlAgilityPack.HtmlWeb();
                var result = await htmlWeb.LoadFromWebAsync(url);
                var DN = result.DocumentNode;
                var id = System.Text.RegularExpressions.Regex.Match(url, @"(?<=/)\d+(?=\.html)").Value; ;
                CDetails.Id = Convert.ToInt64(id);
                CDetails.Name = DN.SelectSingleNode("/html/body/div[3]/div[2]/div[1]/div[3]/div[2]/ul/li[1]/span").InnerText.Replace("公司名称：", "").Trim();
                CDetails.Address = DN.SelectSingleNode("/html/body/div[3]/div[2]/div[1]/div[3]/div[2]/ul/li[3]/span").InnerText.Replace("公司地址：", "").Trim();
                CDetails.Contect = DN.SelectSingleNode("/html/body/div[3]/div[2]/div[1]/div[3]/div[2]/ul/li[2]/span").InnerText.Replace("法人代表：", "").Trim();
                CDetails.Phone = DN.SelectSingleNode("/html/body/div[3]/div[2]/div[1]/div[4]/div[2]/ul/li[7]/span").InnerText.Replace("公司传真：", "").Trim();
                CDetails.Details = DN.SelectSingleNode("/html/body/div[3]/div[2]/div[1]/div[2]/div[2]").InnerText.Replace("&nbsp;", "").Trim();
            }
            catch (Exception ex)
            {
                Thread.Sleep(5000);
                throw new Exception(ex.Message + "(" + url + ")");
            }
            return CDetails;
        }

        public static bool CheckCompanyInfo(CompanyDetails companyDetails)
        {
            if (companyDetails.Name.Contains("公司") || companyDetails.Name.Contains("公司"))
                return true;
            return false;

        }

        public static void SaveCompanyDetails()
        {
            Util.SaveFile("CountyDetails.txt", JsonConvert.SerializeObject(CompanyDetailsList));
        }
        public static void SaveCompanyUrl()
        {
            Util.SaveFile("CompanyUrl.txt", JsonConvert.SerializeObject(CompanyDetailsQueue));
        }



        public class CompanyDetails
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string Address { get; set; }
            public string Contect { get; set; }
            public string Phone { get; set; }
            public string Details { get; set; }

        }
    }
}
