using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Services;

namespace ZKTecoManager
{
    public partial class App : Application
    {
        private static List<Uri> _languageDictionaries = new List<Uri>();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            _languageDictionaries.Add(new Uri("Resources/StringResources.en-US.xaml", UriKind.Relative));
            _languageDictionaries.Add(new Uri("Resources/StringResources.ar-IQ.xaml", UriKind.Relative));

            SwitchLanguage("en-US");

            // Initialize database
            bool dbInitialized = DatabaseInitializer.Initialize();

            if (!dbInitialized)
            {
                MessageBox.Show(
                    "Failed to initialize database. Please ensure PostgreSQL is running.\n\n" +
                    "فشل تهيئة قاعدة البيانات. يرجى التأكد من تشغيل PostgreSQL.",
                    "Database Error / خطأ في قاعدة البيانات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            // Start backup service
            AutomaticBackupService.Start();

            // Start auto-download service
            AutoDownloadService.Start();

            // Preload cache
            CacheManager.Preload();

            // Show login window
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop automatic backup service
            AutomaticBackupService.Stop();

            // Stop auto-download service
            AutoDownloadService.Stop();

            base.OnExit(e);
        }


        public static void SwitchLanguage(string languageCode)
        {
            // Set the culture for the entire application
            Thread.CurrentThread.CurrentCulture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(languageCode);

            var dictionaryUri = _languageDictionaries.FirstOrDefault(d => d.OriginalString.Contains(languageCode));
            if (dictionaryUri == null) return;

            var newDict = new ResourceDictionary() { Source = dictionaryUri };

            var oldDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => _languageDictionaries.Any(lang => d.Source != null && d.Source.OriginalString == lang.OriginalString));

            if (oldDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(oldDict);
            }

            Application.Current.Resources.MergedDictionaries.Insert(0, newDict);

            // Set the FlowDirection for all currently open windows
            var flowDirection = (languageCode == "ar-IQ") ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            foreach (Window window in Application.Current.Windows)
            {
                window.FlowDirection = flowDirection;
            }
        }
    }
}
