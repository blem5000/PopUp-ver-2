using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

namespace popup_1
{
    public partial class MainWindow : Window
    {
        private readonly string popupId;
        private readonly string flagDir;
        private readonly string flagPath;
        private readonly string rtfPath;

        private const int OkButtonDelaySeconds = 7;

        private DispatcherTimer okButtonTimer;
        private int okButtonSecondsLeft = OkButtonDelaySeconds;

        public MainWindow(string popupId, string rtfPath)
        {
            this.popupId = popupId;
            this.rtfPath = rtfPath;

            this.flagDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"TacticalPopup\flags"
            );

            this.flagPath = Path.Combine(this.flagDir, this.popupId + ".ok");

            InitializeComponent();

            LoadRtfMessage();
            StartOkButtonCountdown();
        }

        public static bool FlagExists(string popupId)
        {
            string flagDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"TacticalPopup\flags"
            );

            string flagPath = Path.Combine(flagDir, popupId + ".ok");

            return File.Exists(flagPath);
        }

        public static bool IsTestPopupId(string popupId)
        {
            return popupId != null &&
                   popupId.Equals("TEST", StringComparison.OrdinalIgnoreCase);
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AdjustWindowToContent();

            this.Topmost = true;
            this.Activate();
        }

        private void LoadRtfMessage()
        {
            if (string.IsNullOrWhiteSpace(rtfPath))
            {
                SetPlainText("Nie podano ścieżki do pliku RTF.");
                return;
            }

            if (!File.Exists(rtfPath))
            {
                SetPlainText("Nie znaleziono pliku RTF:\r\n\r\n" + rtfPath);
                return;
            }

            try
            {
                TextRange textRange = new TextRange(
                    rtb.Document.ContentStart,
                    rtb.Document.ContentEnd
                );

                using (FileStream fileStream = new FileStream(rtfPath, FileMode.Open, FileAccess.Read))
                {
                    textRange.Load(fileStream, DataFormats.Rtf);
                }

                rtb.CaretPosition = rtb.Document.ContentStart;
                rtb.ScrollToHome();
            }
            catch (Exception ex)
            {
                WriteErrorLog(
                    @"C:\ProgramData\TacticalPopup\TacticalPopup_rtf_error.log",
                    ex
                );

                SetPlainText(
                    "Błąd podczas wczytywania pliku RTF:\r\n\r\n" +
                    rtfPath +
                    "\r\n\r\n" +
                    ex.Message
                );
            }
        }

        private void SetPlainText(string text)
        {
            rtb.Document.Blocks.Clear();

            Paragraph paragraph = new Paragraph();
            paragraph.Margin = new Thickness(0);
            paragraph.Inlines.Add(new Run(text));

            rtb.Document.Blocks.Add(paragraph);
        }

        private void AdjustWindowToContent()
        {
            double minWindowWidth = 850;
            double minWindowHeight = 430;

            double rtbTop = 85;
            double rtbLeft = 25;
            double rtbRightMargin = 40;

            double buttonTopMargin = 20;
            double buttonBottomMargin = 25;

            double minRtbHeight = 240;

            double maxWindowHeight = SystemParameters.WorkArea.Height * 0.95;

            rtb.UpdateLayout();

            double contentHeight = EstimateRichTextBoxContentHeight();

            double desiredRtbHeight = Math.Max(minRtbHeight, contentHeight);

            double nonRtbHeight =
                rtbTop +
                buttonTopMargin +
                button.Height +
                buttonBottomMargin;

            double desiredWindowHeight = desiredRtbHeight + nonRtbHeight;

            desiredWindowHeight = Math.Max(minWindowHeight, desiredWindowHeight);
            desiredWindowHeight = Math.Min(maxWindowHeight, desiredWindowHeight);

            double finalRtbHeight =
                desiredWindowHeight -
                rtbTop -
                buttonTopMargin -
                button.Height -
                buttonBottomMargin;

            finalRtbHeight = Math.Max(minRtbHeight, finalRtbHeight);

            this.Width = minWindowWidth;
            this.Height = desiredWindowHeight;

            rtb.Width = minWindowWidth - rtbLeft - rtbRightMargin;
            rtb.Height = finalRtbHeight;

            button.Margin = new Thickness(0, 0, 0, buttonBottomMargin);

            CenterWindowOnScreen();
        }

        private double EstimateRichTextBoxContentHeight()
        {
            try
            {
                FlowDocument document = rtb.Document;

                double width = rtb.ActualWidth;

                if (width <= 0)
                {
                    width = 785;
                }

                document.PageWidth = width;
                document.PagePadding = new Thickness(0);
                document.ColumnWidth = width;

                rtb.UpdateLayout();

                TextPointer start = document.ContentStart;
                TextPointer end = document.ContentEnd;

                Rect startRect = start.GetCharacterRect(LogicalDirection.Forward);
                Rect endRect = end.GetCharacterRect(LogicalDirection.Backward);

                double height = endRect.Bottom - startRect.Top;

                if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                {
                    return 240;
                }

                return height + 30;
            }
            catch
            {
                return 240;
            }
        }

        private void CenterWindowOnScreen()
        {
            this.Left = SystemParameters.WorkArea.Left +
                        (SystemParameters.WorkArea.Width - this.Width) / 2;

            this.Top = SystemParameters.WorkArea.Top +
                       (SystemParameters.WorkArea.Height - this.Height) / 2;
        }

        private void StartOkButtonCountdown()
        {
            okButtonSecondsLeft = OkButtonDelaySeconds;

            button.IsEnabled = false;
            button.Content = $"OK ({okButtonSecondsLeft})";

            okButtonTimer = new DispatcherTimer();
            okButtonTimer.Interval = TimeSpan.FromSeconds(1);

            okButtonTimer.Tick += OkButtonTimer_Tick;
            okButtonTimer.Start();
        }

        private void OkButtonTimer_Tick(object sender, EventArgs e)
        {
            okButtonSecondsLeft--;

            if (okButtonSecondsLeft > 0)
            {
                button.Content = $"OK ({okButtonSecondsLeft})";
            }
            else
            {
                okButtonTimer.Stop();
                okButtonTimer.Tick -= OkButtonTimer_Tick;
                okButtonTimer = null;

                button.Content = "OK";
                button.IsEnabled = true;
                button.Focus();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!button.IsEnabled)
            {
                return;
            }

            // TEST popup does not write flag file
            if (IsTestPopupId(popupId))
            {
                this.Close();
                return;
            }

            try
            {
                Directory.CreateDirectory(flagDir);

                string flagContent =
        $@"User: {Environment.UserDomainName}\{Environment.UserName}
Computer: {Environment.MachineName}
PopupId: {popupId}
Accepted: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
RtfPath: {rtfPath}";

                File.WriteAllText(flagPath, flagContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                WriteErrorLog(
                    @"C:\ProgramData\TacticalPopup\TacticalPopup_flag_error.log",
                    ex
                );
            }

            this.Close();
        }

        private void WriteErrorLog(string logPath, Exception ex)
        {
            try
            {
                string logDir = Path.GetDirectoryName(logPath);

                if (!string.IsNullOrWhiteSpace(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                File.WriteAllText(logPath, ex.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}