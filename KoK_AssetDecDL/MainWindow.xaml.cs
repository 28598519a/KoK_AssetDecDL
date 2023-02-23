using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KoK_AssetDecDL
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btn_download_list_Click(object sender, RoutedEventArgs e)
        {
            // 強制覆蓋原有的assetlist
            string al = Path.Combine(App.Root, "assetlist");
            List<Task> tasks = new List<Task>
            {
                DownLoadFile("https://ntk-zone-api.kokmm.net/api/system/assets?device_type=android", Path.Combine(al, "assets_android.json"), true),
                DownLoadFile("https://ntk-zone-api.kokmm.net/api/system/assets?device_type=androidodd", Path.Combine(al, "assets_androidodd.json"), true),
                DownLoadFile("https://ntk-zone-api.kokmm.net/api/system/assets?device_type=androidsecret", Path.Combine(al, "assets_androidsecret.json"), true),
                DownLoadFile("https://steam-zone1.kokmm.net/api/system/assets?device_type=pcsecret", Path.Combine(al, "assets_pcsecret.json"), true),
                DownLoadFile("https://ntk-zone-api.kokmm.net/api/system/sound/assets?app_v=11", Path.Combine(al, "assets_sound.json"), true)
            };
            await Task.WhenAll(tasks);
            tasks.Clear();
            if (App.glocount > 0)
            {
                System.Windows.MessageBox.Show($"下載完成，共{App.glocount}個檔案", "Finish");
                App.glocount = 0;
            }
        }

        /// <summary>
        /// 同時下載的線程池上限
        /// </summary>
        int pool = 50;

        private async void btn_download_Click(object sender, RoutedEventArgs e)
        {
            // [1] assets_android.json
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.InitialDirectory = Path.Combine(App.Root, "assetlist");
            openFileDialog.Filter = "assets_android.json|*.json";
            if (!openFileDialog.ShowDialog() == true)
                return;

            JObject ResList = JObject.Parse(File.ReadAllText(openFileDialog.FileName));
            string DownloadUrl = ResList["response"]["download_url"].ToString();
            JArray FileList = JArray.Parse(ResList["response"]["assets"]["asset_patchs"].ToString());

            List<Tuple<string, string>> AssetList = new List<Tuple<string, string>>();
            App.Respath = Path.Combine(App.Root, "Asset");
            foreach (JArray ja in FileList)
            {
                string name = ja[0].ToString();
                string url = ja[1].ToString();

                AssetList.Add(new Tuple<string, string>(name, DownloadUrl + url));
            }

            // [2] assets_androidodd.json
            openFileDialog.InitialDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            openFileDialog.Filter = "assets_androidodd.json|*.json";
            if (!openFileDialog.ShowDialog() == true)
                return;

            ResList = JObject.Parse(File.ReadAllText(openFileDialog.FileName));
            DownloadUrl = ResList["response"]["download_url"].ToString();
            FileList = JArray.Parse(ResList["response"]["assets"]["asset_patchs"].ToString());

            foreach (JArray ja in FileList)
            {
                string name = ja[0].ToString();
                string url = ja[1].ToString();

                AssetList.Add(new Tuple<string, string>(name, DownloadUrl + url));
            }

            // [3] assets_androidsecret.json
            openFileDialog.InitialDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            openFileDialog.Filter = "assets_androidsecret.json | *.json";
            if (!openFileDialog.ShowDialog() == true)
                return;

            ResList = JObject.Parse(File.ReadAllText(openFileDialog.FileName));
            DownloadUrl = ResList["response"]["download_url"].ToString();
            string Servertime = ResList["server_time"].ToString();
            FileList = JArray.Parse(ResList["response"]["assets"]["asset_patchs"].ToString());

            // Try use pcsecret to mapping androidsecret, but some file can't map. (也許有機會有: XP_CN_13.0.0.458_Prd_AG_100.apk)
            openFileDialog.InitialDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            openFileDialog.Filter = "assets_pcsecret.json | *.json";
            if (!openFileDialog.ShowDialog() == true)
                return;

            JObject ResList_PC = JObject.Parse(File.ReadAllText(openFileDialog.FileName));
            string DownloadUrl_PC = ResList_PC["response"]["download_url"].ToString();
            JArray FileList_PC = JArray.Parse(ResList_PC["response"]["assets"]["asset_patchs"].ToString());

            Dictionary<string, string> secret = new Dictionary<string, string>();
            foreach (JArray ja in FileList_PC)
            {
                string name = ja[0].ToString();
                string url = ja[1].ToString();

                secret.Add(name, url);
            }

            foreach (JArray ja in FileList)
            {
                string name = ja[0].ToString();
                string url = ja[1].ToString();

                if (url == String.Empty)
                {
                    if (secret.ContainsKey(name))
                    {
                        url = DownloadUrl_PC + secret[name];
                        App.maplog1.Add(name);
                    }
                    else
                    {
                        App.maplog2.Add(name);
                        continue;
                    }
                }
                else
                {
                    url = DownloadUrl + url;
                }

                AssetList.Add(new Tuple<string, string>(name, url));
            }

            // 很花時間，每搜索一組需要嘗試1000萬次 (先鎖掉這個功能)
            if (cb_Bully.IsChecked == true)
            {
                int end = 0;
                if (int.TryParse(Servertime, out end))
                {
                    int count = 0;
                    int progress = 0;
                    int map3count = 0;
                    List<Task> tasks = new List<Task>();
                    lb_counter.Content = $"正在搜索 : {count} / {App.maplog2.Count}";
                    await Task.Delay(1);

                    foreach (string name in App.maplog2)
                    {
                        for (int i = 1666062389; i<= end; i++)
                        {
                            tasks.Add(DownloadbySearch(DownloadUrl, name, i));

                            // 阻塞線程，等待現有工作完成再給新工作
                            if ((count % pool).Equals(0) || App.TotalCount == count)
                            {
                                // await is better than Task.Wait()
                                await Task.WhenAll(tasks);
                                tasks.Clear();
                                progress = 100 - ((end - i) * 100 / (end - 1666062389));
                                lb_counter.Content = $"正在搜索 : {count} / {App.maplog2.Count}，進度{progress}%";
                            }

                            if (map3count != App.maplog3.Count)
                            {
                                map3count = App.maplog3.Count;
                                break;
                            }                           
                        }
                        count++;
                    }
                }
            }

            // 成功Mapping與失敗的，輸出log
            if (App.maplog1.Count > 0 || App.maplog2.Count > 0)
            {
                using (StreamWriter outputFile = new StreamWriter("SecretFile.log", false))
                {
                    outputFile.WriteLine("## use PC asset ##");
                    foreach (string s in App.maplog1)
                        outputFile.WriteLine(s);

                    outputFile.WriteLine("## Bully android asset ##");
                    foreach (string s in App.maplog3)
                        outputFile.WriteLine(s);

                    outputFile.WriteLine(Environment.NewLine + "## Fail mapping asset, suggest to manual find ##");
                    foreach (string s in App.maplog2)
                        outputFile.WriteLine(s);
                }
            }

            // [4] assets_sound.json (有一些有跟[1]重複，不過直接略過就好)
            openFileDialog.InitialDirectory = Path.GetDirectoryName(openFileDialog.FileName);
            openFileDialog.Filter = "assets_sound.json|*.json";
            if (!openFileDialog.ShowDialog() == true)
                return;

            ResList = JObject.Parse(File.ReadAllText(openFileDialog.FileName));
            DownloadUrl = ResList["response"]["download_url"].ToString();
            FileList = JArray.Parse(ResList["response"]["assets"]["asset_patchs"].ToString());

            foreach (JArray ja in FileList)
            {
                string name = ja[0].ToString();
                string url = ja[1].ToString();

                AssetList.Add(new Tuple<string, string>(name, DownloadUrl + url));
            }

            App.TotalCount = AssetList.Count;

            if (App.TotalCount > 0)
            {
                if (!Directory.Exists(App.Respath))
                    Directory.CreateDirectory(App.Respath);

                int count = 0;
                List<Task> tasks = new List<Task>();
                foreach (Tuple<string, string> asset in AssetList)
                {
                    string path = Path.Combine(App.Respath, asset.Item1);
                    string url = asset.Item2;

                    tasks.Add(DownLoadFile(url, path, cb_isCover.IsChecked == true ? true : false));
                    count++;

                    // 阻塞線程，等待現有工作完成再給新工作
                    if ((count % pool).Equals(0) || App.TotalCount == count)
                    {
                        // await is better than Task.Wait()
                        await Task.WhenAll(tasks);
                        tasks.Clear();
                    }

                    // 用await將線程讓給UI更新
                    lb_counter.Content = $"進度 : {count} / {App.TotalCount}";
                    await Task.Delay(1);
                }

                lb_counter.Content = $"進度 : {count} / {App.TotalCount}，正在解壓";
                await Task.Delay(1);

                string[] fileList = Directory.GetFiles(App.Respath, "*.zip", SearchOption.TopDirectoryOnly);
                foreach (string file in fileList)
                {
                    //ZipFile.ExtractToDirectory(file, App.Unzippath);
                    if (IsEncryptZip(file, "Ky54RycjC7GnUzJK"))
                        UnZipFiles(file, App.Respath, "Ky54RycjC7GnUzJK");
                    else
                        UnZipFiles(file, App.Respath);
                    File.Delete(file);
                }

                if (cb_Debug.IsChecked == true && App.log.Count > 0)
                {
                    using (StreamWriter outputFile = new StreamWriter("404.log", false))
                    {
                        foreach (string s in App.log)
                            outputFile.WriteLine(s);
                    }
                }

                string msg = $"下載完成，共{App.glocount}個檔案";
                if (App.log.Count > 0)
                    msg += $"，{App.log.Count}個檔案失敗";
                if (App.TotalCount - App.glocount > 0)
                    msg += $"，略過{App.TotalCount - App.glocount - App.log.Count}個檔案";

                System.Windows.MessageBox.Show(msg, "Finish");
                lb_counter.Content = String.Empty;
            }
        }

        /// <summary>
        /// 從指定的網址下載檔案
        /// </summary>
        public async Task<Task> DownLoadFile(string downPath, string savePath, bool overWrite)
        {
            if (!Directory.Exists(Path.GetDirectoryName(savePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            if (File.Exists(savePath) && overWrite == false)
                return Task.FromResult(0);

            App.glocount++;

            using (WebClient wc = new WebClient())
            {
                try
                {
                    // Don't use DownloadFileTaskAsync, if 404 it will create an empty file, use DownloadDataTaskAsync instead.
                    byte[] data = await wc.DownloadDataTaskAsync(downPath);
                    File.WriteAllBytes(savePath, data);
                }
                catch (Exception ex)
                {
                    App.glocount--;

                    if (cb_Debug.IsChecked == true)
                        App.log.Add(downPath + Environment.NewLine + savePath + Environment.NewLine);

                    // 沒有的資源直接跳過，避免報錯。
                    //System.Windows.MessageBox.Show(ex.Message.ToString() + Environment.NewLine + downPath + Environment.NewLine + savePath);
                }
            }
            return Task.FromResult(0);
        }

        /// <summary>
        /// 暴力搜索Secret_Url
        /// </summary>
        public async Task<Task> DownloadbySearch(string baseUrl, string name, int index)
        {
            string url = $"{baseUrl}/androidsecret/{Path.GetFileNameWithoutExtension(name)}_{index}{Path.GetExtension(name)}";
            using (WebClient wc = new WebClient())
            {
                try
                {
                    byte[] data = await wc.DownloadDataTaskAsync(url);
                    if (data.Length > 0)
                    {
                        File.WriteAllBytes(Path.Combine(App.Root, "Asset-Bully", name), data);
                        App.maplog3.Add(name + " : " + index);
                        App.maplog2.Remove(name);
                    }
                }
                catch { }
            }
            return Task.FromResult(0);
        }

        /// <summary>
        /// 檢查是否為加密壓縮檔
        /// </summary>
        public static bool IsEncryptZip(string path, string password = "")
        {
            bool isenc = false;
            using (FileStream fileStreamIn = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (ZipInputStream zipInStream = new ZipInputStream(fileStreamIn))
            {
                ZipEntry entry;
                if (password != null && password != string.Empty) zipInStream.Password = password;
                while ((entry = zipInStream.GetNextEntry()) != null)
                {
                    if (entry.IsCrypted) isenc = true;
                }
                return isenc;
            }
        }

        /// <summary>
        /// 解壓縮
        /// </summary>
        private void UnZipFiles(string filepath, string destfolder, string password = "")
        {
            ZipInputStream zipInStream = null;

            try
            {
                if (!Directory.Exists(destfolder))
                    Directory.CreateDirectory(destfolder);
                
                zipInStream = new ZipInputStream(File.OpenRead(filepath));
                if (password != null && password != string.Empty) zipInStream.Password = password;
                ZipEntry entry;

                while ((entry = zipInStream.GetNextEntry()) != null)
                {
                    string filePath = Path.Combine(destfolder, entry.Name);
                    
                    if (entry.Name != "")
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                        // Skip directory entry
                        if (Path.GetFileName(filePath).Length == 0)
                        {
                            continue;
                        }

                        byte[] buffer = new byte[4096];
                        using (FileStream streamWriter = File.Create(filePath))
                        {
                            StreamUtils.Copy(zipInStream, streamWriter, buffer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine(ex.Message);
            }
            finally
            {
                zipInStream.Close();
                zipInStream.Dispose();
            }
        }

        private void btn_decrypt_Click(object sender, RoutedEventArgs e)
        {
            string selectPath = String.Empty;

            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            openFolderDialog.InitialFolder = App.Root;

            if (openFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                selectPath = openFolderDialog.Folder;
                if (!Directory.Exists(selectPath))
                {
                    selectPath = String.Empty;
                    lb_counter.Content = "Error: 選擇的路徑不存在";
                }
            }

            var result = System.Windows.MessageBox.Show("轉換將會覆蓋掉原始檔案，繼續?", "注意", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.Cancel)
                return;

            string signed1 = "euab";
            string signed2 = "eeab";
            int count = 0;
            List<string> fileList = Directory.GetFiles(selectPath, "*.unity3d", SearchOption.AllDirectories).ToList();

            foreach (string file in fileList)
            {
                byte[] data = File.ReadAllBytes(file);
                long data_size = data.Length;

                // File Sign1 check
                if (data_size > signed1.Length)
                {
                    byte[] tmp = new byte[signed1.Length];
                    Array.Copy(data, tmp, signed1.Length);
                    if (Encoding.UTF8.GetString(tmp) == signed1)
                    {
                        //App.euablist.Add(file.Replace(App.Root, String.Empty));
                        int offset = DecryptUnityAsset.GetOffsetFromFilePath(file);
                        byte[] newdata = new byte[data_size - offset];
                        Array.Copy(data, offset, newdata, 0, newdata.Length);
                        File.WriteAllBytes(file, newdata);
                        count++;
                    }
                }

                // File Sign2 check
                if (data_size > signed2.Length)
                {
                    byte[] tmp = new byte[signed2.Length];
                    Array.Copy(data, tmp, signed2.Length);
                    if (Encoding.UTF8.GetString(tmp) == signed2)
                    {
                        //App.eeablist.Add(file.Replace(App.Root, String.Empty));
                        byte[] newdata = DecryptUnityAsset.DecryptMemory(file, data);
                        File.WriteAllBytes(file, newdata);
                        count++;
                    }
                }
            }

            /*
            using (StreamWriter outputFile = new StreamWriter("euablist.log", false))
            {
                foreach (string s in App.euablist)
                    outputFile.WriteLine(s);
            }
            
            using (StreamWriter outputFile = new StreamWriter("eeablist.log", false))
            {
                foreach (string s in App.eeablist)
                    outputFile.WriteLine(s);
            }
            */

            lb_counter.Content = $"已轉換 {count} 個檔案";
        }
    }
}
