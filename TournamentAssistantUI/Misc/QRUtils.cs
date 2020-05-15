using IronBarCode;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using TournamentAssistantShared;

namespace TournamentAssistantUI.Misc
{
    public class QRUtils
    {
        public static Bitmap ReadPrimaryScreenBitmap(int sourceX, int sourceY, Size size)
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

        public static string[] ReadQRsFromScreenIntoUserIds(int sourceX, int sourceY, Size size)
        {
            var bitmap = ReadPrimaryScreenBitmap(sourceX, sourceY, size);

            Logger.Info("Scanning for barcodes");
            BarcodeResult[] results = BarcodeReader.QuiclyReadAllBarcodes(bitmap, BarcodeEncoding.QRCode);
            Logger.Info("Done!");

            return results.Select(x => x.Text).ToArray();
        }

        public static byte[] GenerateQRCodePngBytes(string data)
        {
            return BarcodeWriter.CreateBarcode(data, BarcodeWriterEncoding.QRCode, 1920, 1080).ToPngBinaryData();
        }
    }
}