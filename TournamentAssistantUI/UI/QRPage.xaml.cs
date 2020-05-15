using IronBarCode;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using TournamentAssistantShared;
using TournamentAssistantUI.UI.Forms;
using Size = System.Drawing.Size;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for QRPage.xaml
    /// </summary>
    public partial class QRPage : Page
    {
        private ResizableLocationSpecifier _resizableLocationSpecifier;

        private int sourceX = Screen.PrimaryScreen.Bounds.X;
        private int sourceY = Screen.PrimaryScreen.Bounds.Y;
        private Size size = Screen.PrimaryScreen.Bounds.Size;

        public QRPage()
        {
            InitializeComponent();
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (_resizableLocationSpecifier == null ||  _resizableLocationSpecifier.IsDisposed)
            {
                _resizableLocationSpecifier = new ResizableLocationSpecifier();
                _resizableLocationSpecifier.LocationOrSizeChanged += (startX, startY, newSize) =>
                {
                    sourceX = startX;
                    sourceY = startY;
                    size = newSize;
                };
            }
            
            _resizableLocationSpecifier.Show();

            /*Logger.Info("Generating barcode...");
            var barcode = BarcodeWriter.CreateBarcode(GenerateTextBox.Text, BarcodeWriterEncoding.QRCode);
            QRImage.Source = BitmapToImageSource(barcode.ToBitmap());
            Logger.Success("Done!");*/

            //await Task.Delay(2000);

            //await DisplayFromScreen();

            await ContinuouslyScanForQRCodes();

            _resizableLocationSpecifier.Close();
        }

        Bitmap ReadPrimaryScreenBitmap()
        {
            Logger.Info("Capturing screenshot...");
            var bmpScreenshot = new Bitmap(size.Width,
                                           size.Height,
                                           PixelFormat.Format32bppArgb);

            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            gfxScreenshot.CopyFromScreen(sourceX,
                                        sourceY,
                                        0,
                                        0,
                                        size,
                                        CopyPixelOperation.SourceCopy);
            Logger.Success("Done!");
            return bmpScreenshot;
        }

        private async Task ContinuouslyScanForQRCodes(int duration = 30 * 1000)
        {
            Action captureFrame = () =>
            {
                var scanResults = ReadQRsFromScreenIntoUserIds();
                if (scanResults.Length > 0)
                {
                    var successMessage = string.Empty;
                    scanResults.ToList().ForEach(x => successMessage += $"{x}, ");
                    Logger.Success(successMessage);
                }
            };

            var captureStart = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            while ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - captureStart < duration)
            {
                await Task.Run(captureFrame);
            }
        }

        private string[] ReadQRsFromScreenIntoUserIds()
        {
            var bitmap = ReadPrimaryScreenBitmap();

            Logger.Info("Scanning for barcodes");
            BarcodeResult[] results = BarcodeReader.QuiclyReadAllBarcodes(bitmap, BarcodeEncoding.QRCode, true);
            Logger.Info("Done!");
            return results.Select(x => x.Text).ToArray();
        }

        private async Task DisplayFromScreen(int duration = 10 * 1000)
        {
            Action captureFrame = () =>
            {
                var imageSource = BitmapToImageSource(ReadPrimaryScreenBitmap());
                Dispatcher.Invoke(() => QRImage.Source = imageSource);
            };

            var captureStart = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            while ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - captureStart < duration)
            {
                await Task.Run(captureFrame);
            }
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                bitmapimage.Freeze();

                return bitmapimage;
            }
        }
    }
}
