using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BusInfo.Services
{
    public class BusLaneService(IWebHostEnvironment environment) : IBusLaneService
    {
        private readonly IWebHostEnvironment _environment = environment;

        // Define bounds (x, y, width, height) based on Python output
        private static readonly (int X, int Y, int Width, int Height) RedBounds = (391, 492, 2153, 59);
        private static readonly (int X, int Y, int Width, int Height) GreenBounds = (391, 563, 2153, 59);
        private static readonly (int X, int Y, int Width, int Height) BlueBounds = (391, 634, 2153, 59);

        public async Task<byte[]> GenerateBusLaneMapAsync(Dictionary<string, string> bayServiceMap)
        {
            ArgumentNullException.ThrowIfNull(bayServiceMap, nameof(bayServiceMap));

            string mapPath = Path.Combine(_environment.WebRootPath, "images", "buslanemap.png");
            string fontPath = Path.Combine(_environment.WebRootPath, "fonts", "Ubuntu", "Ubuntu-Bold.ttf");

            byte[] imageBytes = await File.ReadAllBytesAsync(mapPath);
            using SKBitmap bitmap = SKBitmap.Decode(imageBytes);
            using SKSurface surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            SKCanvas canvas = surface.Canvas;

            // Draw the background image
            canvas.DrawBitmap(bitmap, 0, 0);

            AddBayNumbers(RedBounds, "A", canvas, bayServiceMap, fontPath);
            AddBayNumbers(GreenBounds, "B", canvas, bayServiceMap, fontPath);
            AddBayNumbers(BlueBounds, "C", canvas, bayServiceMap, fontPath);

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static void AddBayNumbers(
            (int X, int Y, int Width, int Height) bounds,
            string prefix,
            SKCanvas canvas,
            Dictionary<string, string> bayServiceMap,
            string fontPath)
        {
            if (bounds == default) return;

            int sectionWidth = bounds.Width / 16;
            float rectWidth = 70f;
            float rectHeight = 50f;
            int y = bounds.Y + (bounds.Height / 2);

            using SKPaint paint = new()
            {
                IsAntialias = true,
                Color = new SKColor(231, 48, 42),
                Style = SKPaintStyle.Fill
            };

            using SKPaint textPaint = new()
            {
                IsAntialias = true,
                Color = SKColors.White,
                TextAlign = SKTextAlign.Center
            };

            using SKFont textFont = new(SKTypeface.FromFile(fontPath) ?? SKTypeface.Default, 16);

            for (int i = 0; i < 16; i++)
            {
                int x = bounds.X + (i * sectionWidth) + (sectionWidth / 2);
                string bayNumber = $"{prefix}{16 - i}";

                // Create rounded rectangle path with square corners on top-left and bottom-right
                using SKPath path = new();
                float radius = 10f;
                
                SKRect rect = new(x - (rectWidth / 2), y - (rectHeight / 2), 
                                x + (rectWidth / 2), y + (rectHeight / 2));

                path.MoveTo(rect.Left, rect.Top); // Start top-left (square)
                path.LineTo(rect.Right - radius, rect.Top); // Top edge
                path.ArcTo(new SKRect(rect.Right - radius, rect.Top, rect.Right, rect.Top + radius), 
                          270, 90, false);
                path.LineTo(rect.Right, rect.Bottom - radius); // Right edge
                path.ArcTo(new SKRect(rect.Right - radius, rect.Bottom - radius, rect.Right, rect.Bottom),
                          0, 90, false);
                path.LineTo(rect.Left + radius, rect.Bottom); // Bottom edge
                path.ArcTo(new SKRect(rect.Left, rect.Bottom - radius, rect.Left + radius, rect.Bottom),
                          90, 90, false);
                path.LineTo(rect.Left, rect.Top); // Back to start
                path.Close();

                canvas.DrawPath(path, paint);

                // Draw bay number
                float textY = rect.Top + (rect.Height - textFont.Size * 2) / 2 + textFont.Size;
                canvas.DrawText(bayNumber, x, textY, textFont, textPaint);

                // Draw service number if available
                if (bayServiceMap.TryGetValue(bayNumber, out string? serviceName))
                {
                    canvas.DrawText(serviceName, x, textY + textFont.Size, textFont, textPaint);
                }
            }
        }
    }
}
