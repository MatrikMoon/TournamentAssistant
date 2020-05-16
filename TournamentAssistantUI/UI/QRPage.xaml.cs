using IronBarCode;
using MessagingToolkit.Barcode;
using MessagingToolkit.Barcode.Common;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
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
        private PrimaryDisplayHighlighter _primaryDisplayHighlighter;
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
            /*if (_primaryDisplayHighlighter == null || _primaryDisplayHighlighter.IsDisposed)
            {
                _primaryDisplayHighlighter = new PrimaryDisplayHighlighter();
            }

            _primaryDisplayHighlighter.Show();*/

            if (_resizableLocationSpecifier == null || _resizableLocationSpecifier.IsDisposed)
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

            QRImage.Source = BitmapToImageSource(GenerateMTQR(GenerateTextBox.Text));

            await Task.Delay(1000);

            await ContinuouslyScanForQRCodes_MT();

            //_primaryDisplayHighlighter.Close();
            _resizableLocationSpecifier.Close();
        }

        #region IronBarcode
        public Bitmap GenerateIronQR(string data)
        {
            return BarcodeWriter.CreateBarcode(data, BarcodeWriterEncoding.QRCode, 1920, 1080).ToBitmap();
        }

        private async Task ContinuouslyScanForQRCodes_IRON(int duration = 30 * 1000)
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
        #endregion

        #region MessagingToolkit
        private Result[] ReadQRLocationsFromScreen()
        {
            Logger.Info("Scanning for barcodes");
            var decoder = new BarcodeDecoder();
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(ReadPrimaryScreenBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var writeableBitmap = new WriteableBitmap(bitmapSource);

            var ret = decoder.DecodeMultiple(writeableBitmap);
            Logger.Info("Done!");

            return ret;
        }

        private async Task ContinuouslyScanForQRCodes_MT(int duration = 30 * 1000)
        {
            Action captureFrame = () =>
            {
                var scanResults = ReadQRLocationsFromScreen();
                if (scanResults.Length > 0)
                {
                    var successMessage = string.Empty;
                    scanResults.ToList().ForEach(x => successMessage += $"{x.Text}, ");
                    Logger.Success(successMessage);
                }
            };

            var captureStart = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            while ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - captureStart < duration)
            {
                await Task.Run(captureFrame);
            }
        }

        private Bitmap GenerateMTQR(string data)
        {
            /*using (var encoder = new BarcodeEncoder())
            {
                encoder.Width = 1920;
                encoder.Height = 1080;
                var bitmap = encoder.Encode(BarcodeFormat.QRCode, data);
                return BitmapFromWriteableBitmap(bitmap);
                //return BitMatrixToBitmap(encoder.BitMatrix);
            }*/

            var encoder = new MessagingToolkit.Barcode.QRCode.QRCodeEncoder();
            return BitMatrixToBitmap(encoder.Encode(data, BarcodeFormat.QRCode, 1920, 1080));
        }

        public Bitmap BitMatrixToBitmap(BitMatrix matrix)
        {
            int height = matrix.Height;
            int width = matrix.Width;
            Bitmap bitmap = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bitmap.SetPixel(x, y, matrix.Get(x, y) ? Color.Black : Color.White);
                }
            }
            return bitmap;
        }

        private Bitmap BitmapFromWriteableBitmap(WriteableBitmap writeableBitmap)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                var bitmapEncoder = new BmpBitmapEncoder();
                bitmapEncoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                bitmapEncoder.Save(memoryStream);
                return new Bitmap(memoryStream);
            }
        }
        #endregion

        #region Screen
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
        #endregion
    }
}
