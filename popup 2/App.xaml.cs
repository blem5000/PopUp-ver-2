using System;
using System.Windows;

namespace popup_1
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string popupId = null;
            string rtfPath = null;

            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];

                if (arg.Equals("-PopupId", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    popupId = e.Args[i + 1];
                    i++;
                    continue;
                }

                if (arg.Equals("-RtfPath", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    rtfPath = e.Args[i + 1];
                    i++;
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(popupId))
            {
                popupId = "default_popup";
            }

            if (!popup_1.MainWindow.IsTestPopupId(popupId) &&
                popup_1.MainWindow.FlagExists(popupId))
            {
                Shutdown();
                return;
            }

            popup_1.MainWindow popupWindow = new popup_1.MainWindow(popupId, rtfPath);

            this.MainWindow = popupWindow;
            popupWindow.Show();
        }
    }
}