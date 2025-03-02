using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BusInfo.Services
{
    /// <summary>
    /// Service responsible for generating the bus lane map image
    /// </summary>
    /// <param name="environment">Hosting environment for accessing web root path</param>
    public class BusLaneService(IWebHostEnvironment environment) : IBusLaneService
    {
        private readonly IWebHostEnvironment _environment = environment;

        /// <summary>
        /// Defines the coordinate boundaries for the Red bus lane section (x, y, width, height)
        /// </summary>
        private static readonly (int X, int Y, int Width, int Height) LaneASymbol = (391, 492, 2153, 59);

        /// <summary>
        /// Defines the coordinate boundaries for the Green bus lane section (x, y, width, height)
        /// </summary>
        private static readonly (int X, int Y, int Width, int Height) LaneBSymbol = (391, 563, 2153, 59);

        /// <summary>
        /// Defines the coordinate boundaries for the Blue bus lane section (x, y, width, height)
        /// </summary>
        private static readonly (int X, int Y, int Width, int Height) LaneCSymbol = (391, 634, 2153, 59);

        /// <summary>
        /// Generates a bus lane map image
        /// </summary>
        /// <param name="bayServiceMap">Dictionary mapping bay number to bus number</param>
        /// <returns>Byte array containing the generated PNG image</returns>
        /// <exception cref="ArgumentNullException">Thrown when bayServiceMap is null</exception>
        public async Task<byte[]> GenerateBusLaneMapAsync(Dictionary<string, string> bayServiceMap)
        {
            ArgumentNullException.ThrowIfNull(bayServiceMap, nameof(bayServiceMap));

            // Load the base map image and font resources
            string mapPath = Path.Combine(_environment.WebRootPath, "images", "buslanemap.png");
            string fontPath = Path.Combine(_environment.WebRootPath, "fonts", "Ubuntu", "Ubuntu-Bold.ttf");

            // Read and decode the base map image
            byte[] imageBytes = await File.ReadAllBytesAsync(mapPath);
            using SKBitmap bitmap = SKBitmap.Decode(imageBytes);
            using SKSurface surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            SKCanvas canvas = surface.Canvas;

            // Draw the background image
            canvas.DrawBitmap(bitmap, 0, 0);

            // Add bay numbers and service information to each lane section
            AddBayNumbers(LaneASymbol, "A", canvas, bayServiceMap, fontPath);
            AddBayNumbers(LaneBSymbol, "B", canvas, bayServiceMap, fontPath);
            AddBayNumbers(LaneCSymbol, "C", canvas, bayServiceMap, fontPath);

            // Encode the final image as PNG and return the byte array
            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <summary>
        /// Adds bay number indicators and service information to a specific lane section
        /// </summary>
        /// <param name="bounds">Coordinate boundaries of the lane section</param>
        /// <param name="prefix">Prefix character for bay numbers (e.g., 'A', 'B', or 'C')</param>
        /// <param name="canvas">Canvas to draw on</param>
        /// <param name="bayServiceMap">Dictionary mapping bay number to bus number</param>
        /// <param name="fontPath">Path to the font file to use for text rendering</param>
        private static void AddBayNumbers(
            (int X, int Y, int Width, int Height) bounds,
            string prefix,
            SKCanvas canvas,
            Dictionary<string, string> bayServiceMap,
            string fontPath)
        {
            if (bounds == default) return;

            // Calculate dimensions for evenly distributing bay indicators across the lane
            int sectionWidth = bounds.Width / 16;
            const float rectWidth = 70f;
            const float rectHeight = 50f;
            int y = bounds.Y + (bounds.Height / 2);

            // Load the font for text rendering
            using SKTypeface typeface = SKTypeface.FromFile(fontPath) ?? SKTypeface.Default;
            using SKFont font = new(typeface, 16);

            // Configure the paint objects for bay indicators and text
            using SKPaint paint = new()
            {
                IsAntialias = true,
                Color = new SKColor(231, 48, 42),  // Red background for bay indicators
                Style = SKPaintStyle.Fill
            };

            using SKPaint textPaint = new()
            {
                IsAntialias = true,
                Color = SKColors.White,
            };

            // Draw 16 bay indicators for the current lane section
            for (int i = 0; i < 16; i++)
            {
                // Calculate the center position for this bay indicator
                int x = bounds.X + (i * sectionWidth) + (sectionWidth / 2);

                string bayNumber = $"{prefix}{16 - i}";

                // Define the rectangle for the bay indicator
                SKRect rect = new(
                    x - (rectWidth / 2),
                    y - (rectHeight / 2),
                    x + (rectWidth / 2),
                    y + (rectHeight / 2)
                );

                // Draw the bay indicator background
                using (SKRoundRect roundRect = new())
                {
                    roundRect.SetRectRadii(
                        rect,
                        [
                            new SKPoint(0, 0),     // Top-left (square)
                            new SKPoint(10, 10),   // Top-right (rounded)
                            new SKPoint(0, 0),     // Bottom-right (square)
                            new SKPoint(10, 10)    // Bottom-left (rounded)
                        ]
                    );

                    canvas.DrawRoundRect(roundRect, paint);
                }

                // Draw the bay number centered in the indicator
                float textY = rect.MidY - (font.Size / 2);
                canvas.DrawText(bayNumber, x, textY, SKTextAlign.Center, font, textPaint);

                // If this bay has a service assigned, draw the service number below the bay number
                if (bayServiceMap.TryGetValue(bayNumber, out string? serviceName))
                {
                    canvas.DrawText(serviceName, x, textY + (font.Size * 1.2f), SKTextAlign.Center, font, textPaint);
                }
            }
        }
    }
}
