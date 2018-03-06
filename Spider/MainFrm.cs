using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Concurrent;
namespace Spider
{
    public partial class MainFrm : Form
    {
        public MainFrm()
        {
            InitializeComponent();
        }
        string[] CountyNames = new string[] {
                "beijing","chongqing","shanghai","tianjin","neimenggu","heilongjiang","yunnan",
                "jilin","sichuan","ningxia","anhui","shandong","shanxi","guangdong","guangxi","xinjiang","jiangsu",
                "jiangxi","hebei","henan","zhejiang","hainan","hubei","hunan","gansu","fujian","xizang","guizhou","liaoning",
                "shanxii","qinghai"
            };
        public Queue<string> CountyPageUrlQueue = new Queue<string>();
        public ConcurrentQueue<string[]> CompanyDetailsQueue = new ConcurrentQueue<string[]>();
        public Dictionary<string, List<string>> CompanyUrlDic = new Dictionary<string, List<string>>();
        public static List<CompanyDetails> CompanyDetailsList = new List<CompanyDetails>();
        public CancellationTokenSource CancelTokenCounty = new CancellationTokenSource();
        public CancellationTokenSource CancelTokenCompayDetails = new CancellationTokenSource();

        private void Start_Btn_Click(object sender, EventArgs e)
        {
            if (Start_Btn.Text == "停止爬公司列表")
            {
                CancelTokenCounty.Cancel();
                Start_Btn.Text = "停止中.....";
            }
            else
            {
                Start_Btn.Text = "停止爬公司列表";
                listBox1.Items.Insert(0, G("爬公司列表已开始"));
                foreach (var item in CountyNames)
                {
                    for (int i = 0; i < 500; i++)
                    {
                        CountyPageUrlQueue.Enqueue(string.Format("http://company.makepolo.com/{0}/{1}", item, i));
                    }

                }

                toolStripProgressBar1.Maximum = CountyPageUrlQueue.Count();
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Step = 1;
                var Tasks = new List<Task>();
                for (int j = 0; j < 10; j++)
                {
                    Tasks.Add(Task.Run(async () =>
                    {
                        var str = string.Format("线程{0}已起动.", Thread.CurrentThread.ManagedThreadId);
                        UI(() => { listBox1.Items.Insert(0, G(str)); });
                        while (!CancelTokenCounty.IsCancellationRequested)
                        {
                            var UrlResult = await GetPageContextAsync(CountyPageUrlQueue.Dequeue());
                            CompanyDetailsQueue.Enqueue(UrlResult.ToArray());
                            UI(() =>
                            {
                                CountyLsBox.Items.AddRange(UrlResult.ToArray());
                                toolStripProgressBar1.PerformStep();
                            });
                        };
                    }, CancelTokenCounty.Token).ContinueWith((t) =>
                     {
                         if (t.IsCompleted)
                         {
                             var str = string.Format("线程{0}已退出.", Thread.CurrentThread.ManagedThreadId);
                             UI(() => { listBox1.Items.Insert(0, G(str)); });
                         }
                     }));
                }
                Task.WhenAll(Tasks.ToArray()).ContinueWith((t) =>
                {
                    UI(() =>
                    {
                        listBox1.Items.Insert(0, G(string.Format("总共拉取{0}个.", CountyLsBox.Items.Count)));
                        Start_Btn.Text = "开始爬公司列表";
                    });
                });



            }

        }
        public string G(string str)
        {
            return string.Format("[{0}] {1}", DateTime.Now, str);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (CompanyDetail_Btn.Text == "停止爬公司详情")
            {
                CancelTokenCompayDetails.Cancel();
                CompanyDetail_Btn.Text = "停止中.....";
            }
            else
            {
                CompanyDetail_Btn.Text = "停止爬公司详情";
                UI(() => { listBox2.Items.Insert(0, G("爬公司详情已开始")); });
                toolStripProgressBar2.Maximum = CountyPageUrlQueue.Count();
                toolStripProgressBar2.Minimum = 0;
                toolStripProgressBar2.Step = 1;
                var Tasks = new List<Task>();
                for (int i = 0; i < 20; i++)
                {
                    Tasks.Add(Task.Run(async () =>
                    {
                        var str = string.Format("线程{0}已起动.", Thread.CurrentThread.ManagedThreadId);
                        UI(() => { listBox2.Items.Insert(0, G(str)); });
                        while (!CancelTokenCompayDetails.IsCancellationRequested)
                        {
                            UI(() => { toolStripProgressBar2.Maximum = CountyPageUrlQueue.Count(); });
                            if (CompanyDetailsQueue.TryDequeue(out string[] Com))
                            {

                                foreach (var item in Com)
                                {
                                    var result = await GetDetailContext(item);
                                    CompanyDetailsList.Add(result);
                                    UI(() =>
                                    {
                                        toolStripProgressBar2.PerformStep();
                                        CompanyDetail_LsBox.Items.Insert(0, result.Name ?? "");
                                    });
                                }

                            }

                        }
                    }, CancelTokenCompayDetails.Token).ContinueWith((t) =>
                    {

                        if (t.IsCompleted)
                        {
                            var str = string.Format("线程{0}已退出.", Thread.CurrentThread.ManagedThreadId);
                            UI(() => { listBox2.Items.Insert(0, G(str)); });
                        }
                    }));
                }
                Task.Run(() =>
                {
                    Task.WhenAll(Tasks.ToArray()).ContinueWith((t =>
                    {
                        SaveCompanyDetails(JsonConvert.SerializeObject(CompanyDetailsList));
                        UI(() =>
                        {
                            CompanyDetail_Btn.Text = "开始爬公司详情";
                            listBox2.Items.Insert(0, G("爬公司详情已停止"));
                            listBox2.Items.Insert(0, G(string.Format("总共拉取{0}条记录", CompanyDetailsList.Count)));
                        });
                    }));

                });
            }
        }

        public void SaveCompanyDetails(string Text)
        {
            System.IO.File.WriteAllText("CountyDetails.txt", Text);
            UI(() => { listBox2.Items.Insert(0, G("公司信息保存成功!")); });
        }
        public async Task<List<string>> GetPageContextAsync(string url)
        {
            var urlList = new List<string>();
            try
            {

                HtmlAgilityPack.HtmlWeb htmlWeb = new HtmlAgilityPack.HtmlWeb();
                var result = await htmlWeb.LoadFromWebAsync(url);
                var root = result.DocumentNode;
                var list = root.SelectNodes("//h2[@class='colist_item_title']");
                foreach (HtmlAgilityPack.HtmlNode item in list)
                {
                    var val = item.SelectSingleNode("a").Attributes["href"].Value;
                    urlList.Add(val);
                }
            }
            catch (Exception ex)
            {
                UI(() => { Err_LsBox.Items.Insert(0, G(ex.Message + "(" + url + ")")); });
            }
            return urlList;
        }
        public async Task<CompanyDetails> GetDetailContext(string url)
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
                UI(() => { Err_LsBox.Items.Insert(0, G(ex.Message + "(" + url + ")")); });
            }
            return CDetails;
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
        public void UI(Action action)
        {
            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //if (!CancelTokenCounty.IsCancellationRequested || !CancelTokenCompayDetails.IsCancellationRequested)
            //{
            //    e.Cancel = true;
            //    MessageBox.Show("请先停止拉取数据,再关闭!", "警告信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //}
        }
    }
}
