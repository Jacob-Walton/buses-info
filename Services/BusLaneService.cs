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

        // Define bounds (min_row, max_row, min_col, max_col)
        private static readonly (int MinRow, int MaxRow, int MinCol, int MaxCol) RedBounds = (588, 622, 451, 2606);
        private static readonly (int MinRow, int MaxRow, int MinCol, int MaxCol) GreenBounds = (651, 684, 451, 2606);
        private static readonly (int MinRow, int MaxRow, int MinCol, int MaxCol) BlueBounds = (712, 746, 451, 2606);

        public async Task<byte[]> GenerateBusLaneMapAsync(Dictionary<string, string> bayServiceMap)
        {
            ArgumentNullException.ThrowIfNull(bayServiceMap, nameof(bayServiceMap));

            string mapPath = Path.Combine(_environment.WebRootPath, "images", "buslanemap.png");
            string fontPath = Path.Combine(_environment.WebRootPath, "fonts", "Montserrat-Bold.ttf");

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
            (int MinRow, int MaxRow, int MinCol, int MaxCol) bounds,
            string prefix,
            SKCanvas canvas,
            Dictionary<string, string> bayServiceMap,
            string fontPath)
        {
            if (bounds == default) return;

            int width = bounds.MaxCol - bounds.MinCol;
            int sectionWidth = width / 16;
            int y = (bounds.MinRow + bounds.MaxRow) / 2;

            using SKPaint paint = new()
            {
                IsAntialias = true,
                Color = new SKColor(231, 48, 42),
                Style = SKPaintStyle.Fill
            };

            using SKPaint textPaint = new()
            {
                IsAntialias = true,
                Color = SKColors.WhiteSmoke
            };

            using SKFont textFont = new(SKTypeface.FromFile(fontPath) ?? SKTypeface.Default, 16);

            for (int i = 0; i < 16; i++)
            {
                int x = bounds.MinCol + (i * sectionWidth) + (sectionWidth / 2);
                float rectWidth = 70f;
                float rectHeight = 40f;
                string bayNumber = $"{prefix}{16 - i}";

                SKRect rect = new(x - (rectWidth / 2), y - (rectHeight / 2), x + (rectWidth / 2), y + (rectHeight / 2));
                canvas.DrawRect(rect, paint);

                canvas.DrawText(bayNumber, x, y - 5, SKTextAlign.Center, textFont, textPaint);

                if (bayServiceMap.TryGetValue(bayNumber, out string serviceName))
                {
                    canvas.DrawText(serviceName, x, y + 15, SKTextAlign.Center, textFont, textPaint);
                }
            }
        }
    }
}
