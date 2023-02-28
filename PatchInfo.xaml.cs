using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x410

namespace AppxPatch
{
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class PatchInfo : Page
    {
        public XmlDocument doc;

        public StorageFolder AppxFolder;

        public JObject jo;
        public StorageFile fi;

        public PatchInfo()
        {
            InitializeComponent();
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Hide default title bar.
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);

            // Set XAML element as a draggable region.
            Window.Current.SetTitleBar(AppTitleBar);

            // Register a handler for when the size of the overlaid caption control changes.
            // For example, when the app moves to a screen with a different DPI.
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            // Register a handler for when the title bar visibility changes.
            // For example, when the title bar is invoked in full screen mode.
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;

            //Register a handler for when the window changes focus
            Window.Current.Activated += Current_Activated;
        }

        #region Titlebar code
        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Update title bar control size as needed to account for system size changes.
            AppTitleBar.Height = coreTitleBar.Height;

            // Ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                AppTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }
        }

        // Update the TitleBar based on the inactive/active state of the app
        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
        }
        #endregion
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            
            if (e.Parameter is Windows.Storage.StorageFile)
            {
                #region do stuff with files
                var param = e.Parameter as Windows.Storage.StorageFile;
                StorageFolder temp = ApplicationData.Current.TemporaryFolder;
                try
                {
                    StorageFolder fo = await temp.CreateFolderAsync(param.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                StorageFolder folder = await temp.GetFolderAsync(param.Name);
                StorageFile f = await param.CopyAsync(folder, param.Name.ToString().ToLower().Replace("appx", "zip").Replace("msix", "zip"), NameCollisionOption.ReplaceExisting);
                Stream ac = await f.OpenStreamForReadAsync();
                ZipArchive archive = new ZipArchive(ac);
                archive.ExtractToDirectory(folder.Path+"\\opened", true);
                StorageFolder fd = await folder.GetFolderAsync("opened");
#pragma warning disable IDE0059, CS0168
                try
                {
                    StorageFile signature = await fd.GetFileAsync("AppxSignature.p7x");
                    await signature.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                catch (Exception _ex)
                {

                }
#pragma warning restore IDE0059, CS0168
                #endregion
                #region xml parsing
                XmlDocument doc = new XmlDocument();
                StorageFile fi = await fd.GetFileAsync("AppxManifest.xml");
                string text = File.ReadAllText(fi.Path);

                doc.LoadXml(text);
                string JsonText = JsonConvert.SerializeXmlNode(doc);
                var jo = JObject.Parse(JsonText);
                var identity = (JObject)jo["Package"]["Identity"];
                appName.Text = identity["@Name"].ToString();
                appName.IsReadOnly = false;

                appDisplayName.Text = jo["Package"]["Properties"]["DisplayName"].ToString();
                appDisplayName.IsReadOnly = false;

                MinVersion.Text = jo["Package"]["Dependencies"]["TargetDeviceFamily"]["@MinVersion"].ToString();
                MinVersion.IsReadOnly = false;

                MaxVersion.Text = jo["Package"]["Dependencies"]["TargetDeviceFamily"]["@MaxVersionTested"].ToString();
                MaxVersion.IsReadOnly = false;

                publisher.Text = jo["Package"]["Identity"]["@Publisher"].ToString();
                publisher.IsReadOnly = false;

                publisherDisplayName.Text = jo["Package"]["Properties"]["PublisherDisplayName"].ToString();
                publisherDisplayName.IsReadOnly = false;

                version.Text = jo["Package"]["Identity"]["@Version"].ToString();
                version.IsReadOnly = false;

                next.IsEnabled = true;
                #endregion
                this.doc = doc;
                this.AppxFolder = fd;
                this.jo = jo;
                this.fi = fi;
            }
        }

        private async void next_Click(object sender, RoutedEventArgs e)
        {
            var doc = this.doc;
            var jo = this.jo;
            #region reverse assignement
            jo["Package"]["Identity"]["@Name"] = appName.Text;
            jo["Package"]["Properties"]["DisplayName"] = appDisplayName.Text;
            jo["Package"]["Dependencies"]["TargetDeviceFamily"]["@MinVersion"] = MinVersion.Text;
            jo["Package"]["Dependencies"]["TargetDeviceFamily"]["@MaxVersionTested"] = MaxVersion.Text;
            jo["Package"]["Identity"]["@Publisher"] = publisher.Text;
            jo["Package"]["Properties"]["PublisherDisplayName"] = publisherDisplayName.Text;
            jo["Package"]["Identity"]["@Version"] = version.Text;
            #endregion
            #region convert to xml
            var xml = JsonConvert.DeserializeXmlNode(jo.ToString());
            string path = this.fi.Path;
            await this.fi.DeleteAsync();
            File.WriteAllText(path, xml.InnerXml);
            #endregion
            Frame.Navigate(typeof(SaveDialogue), this.AppxFolder);
            
        }
    }
}
