using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TournamentAssistantServer.ASP.Attributes;

namespace TournamentAssistantServer.PacketHandlers
{
    [AllowWebsocketToken]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class FileController : Controller
    {
        public const int ImageMinimumBytes = 512;
        public const int ImageMaximumBytes = 10_000;

        private readonly string _basePath = Path.Combine(Directory.GetCurrentDirectory(), "files/FileServerContent");

        [HttpGet("{filename}")]
        public IActionResult Download(string filename)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_basePath, filename));

            // ⛔ Check that it's still inside the intended directory
            if (!fullPath.StartsWith(_basePath, StringComparison.Ordinal))
                return BadRequest("Invalid filename.");

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var contentType = "application/octet-stream";
            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0 || !IsValidImage(file))
            {
                return BadRequest("No file uploaded, or invalid file. Image must be less than 10MB.");
            }

            // Strip dangerous path parts
            // var sanitizedFileName = Path.GetFileName(file.FileName); // removes any path info
            var sanitizedFileName = Guid.NewGuid().ToString();

            var fullPath = Path.GetFullPath(Path.Combine(_basePath, sanitizedFileName));
            if (!fullPath.StartsWith(_basePath, StringComparison.Ordinal))
            {
                return BadRequest("Invalid filename.");
            }

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return Ok(sanitizedFileName);
        }

        [NonAction]
        public static bool IsValidImage(IFormFile file)
        {
            //-------------------------------------------
            //  Check the image mime types
            //-------------------------------------------
            if (!string.Equals(file.ContentType, "image/jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(file.ContentType, "image/pjpeg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(file.ContentType, "image/gif", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(file.ContentType, "image/x-png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            //-------------------------------------------
            //  Check the image extension
            //-------------------------------------------
            var postedFileExtension = Path.GetExtension(file.FileName);
            if (!string.Equals(postedFileExtension, ".jpg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(postedFileExtension, ".png", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(postedFileExtension, ".gif", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(postedFileExtension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            //-------------------------------------------
            //  Attempt to read the file and check the first bytes
            //-------------------------------------------
            try
            {
                //------------------------------------------
                //   Check whether the image size exceeding the limit or not
                //------------------------------------------ 
                if (file.Length < ImageMinimumBytes)
                {
                    return false;
                }

                if (file.Length > ImageMaximumBytes)
                {
                    return false;
                }

                using var memoryStream = new MemoryStream();
                file.CopyTo(memoryStream);

                byte[] buffer = new byte[ImageMinimumBytes];
                memoryStream.Read(buffer, 0, ImageMinimumBytes);
                string content = System.Text.Encoding.UTF8.GetString(buffer);
                if (Regex.IsMatch(content, @"<script|<html|<head|<title|<body|<pre|<table|<a\s+href|<img|<plaintext|<cross\-domain\-policy",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline))
                {
                    return false;
                }

                //-------------------------------------------
                //  Try to instantiate new Bitmap, if .NET will throw exception
                //  we can assume that it's not a valid image
                //-------------------------------------------

                try
                {
                    using var bitmap = new System.Drawing.Bitmap(memoryStream);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
    }
}
