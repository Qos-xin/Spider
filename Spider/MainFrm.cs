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
            MakepoloCompany.GetCountyPageUrl();
        }

        public CancellationTokenSource CancelTokenCounty = new CancellationTokenSource();
        public CancellationTokenSource CancelTokenCompayDetails = new CancellationTokenSource();

        private void Start_Btn_Click(object sender, EventArgs e)
        {
            if (Start_Btn.Text == "停止爬公司列表")
            {
                CancelTokenCounty.Cancel();
                Start_Btn.Text = "停止中.....";
            }
            else if (Start_Btn.Text == "开始爬公司列表")
            {
                Start_Btn.Text = "停止爬公司列表";
                listBox1.Items.Insert(0, G("爬公司列表已开始"));
                toolStripProgressBar1.Maximum = MakepoloCompany.CountyPageUrlQueue.Count();
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Step = 1;
                var Tasks = new List<Task>();
                for (int j = 0; j < 5; j++)
                {
                    Tasks.Add(Task.Run(async () =>
                    {
                        var str = string.Format("线程{0}已启动.", Thread.CurrentThread.ManagedThreadId);
                        UI(() => { listBox1.Items.Insert(0, G(str)); });
                        while (!CancelTokenCounty.IsCancellationRequested)
                        {
                            try
                            {
                                if (MakepoloCompany.DequeuePageUrl(out KeyValuePair<string, string> SourceUrl))
                                {
                                    var UrlResult = await MakepoloCompany.GetPageContextAsync(SourceUrl.Key, SourceUrl.Value);
                                    UI(() =>
                                    {
                                        CountyLsBox.Items.AddRange(UrlResult.ToArray());
                                        toolStripProgressBar1.PerformStep();
                                        toolStripProgressBar2.Maximum = CountyLsBox.Items.Count;
                                    });
                                }
                                else
                                {
                                    CancelTokenCounty.Cancel();
                                }
                            }
                            catch (Exception ex)
                            {
                                UI(() => { listBox1.Items.Insert(0, G(ex.Message)); });
                            }
                            Thread.Sleep(10);
                        };
                    }, CancelTokenCounty.Token).ContinueWith((t) =>
                     {
                         Tasks.Remove(t);
                         var str = string.Format("线程{0}已退出.", Thread.CurrentThread.ManagedThreadId);
                         UI(() => { listBox1.Items.Insert(0, G(str)); });

                     }));
                }
                Task.WhenAll(Tasks.ToArray()).ContinueWith((t) =>
                {
                    MakepoloCompany.SaveCompanyUrl();
                    UI(() =>
                    {
                        listBox1.Items.Insert(0, G(string.Format("公司地址保存成功,总共拉取{0}个.", CountyLsBox.Items.Count)));
                        Start_Btn.Text = "开始爬公司列表";
                        CancelTokenCounty = new CancellationTokenSource();
                    });
                });



            }

        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (CompanyDetail_Btn.Text == "停止爬公司详情")
            {
                CancelTokenCompayDetails.Cancel();
                CompanyDetail_Btn.Text = "停止中.....";
            }
            else if (CompanyDetail_Btn.Text == "开始爬公司详情")
            {
                CompanyDetail_Btn.Text = "停止爬公司详情";
                UI(() => { listBox2.Items.Insert(0, G("爬公司详情已开始")); });
                toolStripProgressBar2.Minimum = 0;
                toolStripProgressBar2.Step = 1;
                var Tasks = new List<Task>();
                MakepoloCompany.HandlerMsg += (msg) =>
                {
                    UI(() => { listBox2.Items.Insert(0, G(msg)); });
                };
                for (int i = 0; i < 10; i++)
                {
                    Tasks.Add(Task.Run(async () =>
                    {
                        var str = string.Format("线程{0}已起动.", Thread.CurrentThread.ManagedThreadId);
                        UI(() => { listBox2.Items.Insert(0, G(str)); });
                        while (!CancelTokenCompayDetails.IsCancellationRequested)
                        {
                            try
                            {
                                if (MakepoloCompany.DequeueCompanyDetailsUrl(out KeyValuePair<string, string[]> Com))
                                {
                                    foreach (var item in Com.Value)
                                    {
                                        var list = await MakepoloCompany.GetPageDetailsContextAsync(Com.Key, item);
                                        if (list == null)
                                        {
                                            var str1 = string.Format("线程{0}空闲,休息5S.", Thread.CurrentThread.ManagedThreadId);
                                            UI(() =>
                                            {
                                                listBox2.Items.Insert(0, G(str1));

                                            });
                                            Thread.Sleep(5000);
                                        }
                                        else
                                        {
                                            UI(() =>
                                            {
                                                toolStripProgressBar2.PerformStep();
                                                foreach (var Company in list)
                                                {
                                                    CompanyDetail_LsBox.Items.Insert(0, (Company.Name ?? "") + "--" + Company.Address);

                                                }
                                            });
                                        }
                                    }
                                }
                                else {
                                    CancelTokenCompayDetails.Cancel();
                                }

                            }
                            catch (Exception ex)
                            {
                                if (ex.Message == "1")
                                {
                                    CancelTokenCompayDetails.Cancel();
                                }
                                UI(() => { listBox2.Items.Insert(0, G(ex.Message)); });
                            }
                            Thread.Sleep(10);
                        }
                    }, CancelTokenCompayDetails.Token).ContinueWith((t) =>
                    {
                        Tasks.Remove(t);
                        var str = string.Format("线程{0}已退出.", Thread.CurrentThread.ManagedThreadId);
                        UI(() => { listBox2.Items.Insert(0, G(str)); });

                    }));
                }
                Task.WhenAll(Tasks.ToArray()).ContinueWith((t =>
                {
                    MakepoloCompany.SaveCompanyDetails();
                    UI(() =>
                    {
                        listBox2.Items.Insert(0, G("爬公司详情已停止"));
                        listBox2.Items.Insert(0, G(string.Format("公司信息保存成功,总共拉取{0}条记录", CompanyDetail_LsBox.Items.Count)));
                        CompanyDetail_Btn.Text = "开始爬公司详情";
                        CancelTokenCompayDetails = new CancellationTokenSource();
                    });
                }));
            }
        }
        public string G(string str)
        {
            return string.Format("[{0}] {1}", DateTime.Now, str);
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
            if (CompanyDetail_Btn.Text == "停止爬公司详情" || Start_Btn.Text == "停止爬公司列表")
            {
                e.Cancel = true;
                MessageBox.Show("请先停止拉取数据,再关闭!", "警告信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
