using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;
using Tesseract;
using System.IO;
using System.Linq;

namespace OcrMaui
{
    public partial class MainPage : ContentPage
    {
        private TesseractEngine _tesseractEngine;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            InitializeTesseract();
        }

        private void InitializeTesseract()
        {
            try
            {
                // Path to the tessdata directory in your project
                var tessDataPath = Path.Combine(FileSystem.AppDataDirectory, "tessdata");

                // Create the TesseractEngine with your custom trained data (replace 'custom' with the language code you used)
                _tesseractEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);

                // Set PSM mode (e.g., Sparse text with OSD)
                _tesseractEngine.SetVariable("tessedit_pageseg_mode", "11");

                // Set additional configurations if needed
                _tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789"); // Example: Whitelist numbers only
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Tesseract: {ex.Message}");
            }
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            try
            {
                var pickResult = await MediaPicker.Default.PickPhotoAsync();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    var imageAsBytes = new byte[imageAsStream.Length];
                    await imageAsStream.ReadAsync(imageAsBytes);

                    var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

                    if (!ocrResult.Success)
                    {
                        await DisplayAlert("No Success", "No OCR possible", "OK");
                        return;
                    }

                    // Extract only the 5-digit sequence
                    var match = Regex.Match(ocrResult.AllText, @"\b\d{5}\b");
                    string sequence;

                    if (match.Success)
                    {
                        sequence = match.Value;
                        await DisplayAlert("OCR Result", $"Sequência encontrada: {sequence}", "OK");
                    }
                    else
                    {
                        await DisplayAlert("No match", "Nenhuma sequência de 5 números encontrada.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }


        private async void PictureBtn_Clicked(object sender, EventArgs e)
        {
            try
            {
                var pickResult = await MediaPicker.Default.CapturePhotoAsync();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    var imageAsBytes = new byte[imageAsStream.Length];
                    await imageAsStream.ReadAsync(imageAsBytes);

                    var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

                    if (!ocrResult.Success)
                    {
                        await DisplayAlert("No Success", "No OCR possible", "OK");
                        return;
                    }

                    // Extract only the 5-digit sequence
                    var match = Regex.Match(ocrResult.AllText, @"\b\d{5}\b");
                    string sequence;

                    if (match.Success)
                    {
                        sequence = match.Value;
                        await DisplayAlert("OCR Result", $"Sequência encontrada: {sequence}", "OK");
                    }
                    else
                    {
                        await DisplayAlert("No match", "Nenhuma sequência de 5 números encontrada.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task ProcessImage(Func<Task<FileResult>> pickImageFunc)
        {
            try
            {
                var pickResult = await pickImageFunc();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await imageAsStream.CopyToAsync(memoryStream);
                    var imageAsBytes = memoryStream.ToArray();

                    // Check if image loaded correctly
                    if (imageAsBytes.Length == 0)
                    {
                        await DisplayAlert("Error", "Image could not be loaded.", "OK");
                        return;
                    }

                    var processedImageBytes = PreprocessImage(imageAsBytes);
                    var ocrResult = await PerformOcrAsync(processedImageBytes);

                    if (!string.IsNullOrEmpty(ocrResult))
                    {
                        await DisplayAlert("OCR Result", $"Sequência encontrada: {ocrResult}", "OK");
                    }
                    else
                    {
                        await DisplayAlert("No match", "Nenhuma sequência de 5 números encontrada.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private byte[] PreprocessImage(byte[] imageBytes)
        {
            try
            {
                using var inputBitmap = SKBitmap.Decode(imageBytes);
                if (inputBitmap == null)
                {
                    throw new Exception("Failed to decode the image.");
                }

                // Enhance image for better OCR accuracy (adjustments might be needed)
                const int resizeFactor = 4;
                using var resizedBitmap = new SKBitmap(inputBitmap.Width * resizeFactor, inputBitmap.Height * resizeFactor);
                using (var canvas = new SKCanvas(resizedBitmap))
                {
                    canvas.DrawBitmap(inputBitmap, SKRect.Create(resizedBitmap.Width, resizedBitmap.Height));
                }

                using var grayBitmap = new SKBitmap(resizedBitmap.Width, resizedBitmap.Height, SKColorType.Gray8, SKAlphaType.Opaque);
                using (var canvas = new SKCanvas(grayBitmap))
                {
                    var paint = new SKPaint
                    {
                        ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                        {
                            0.299f, 0.587f, 0.114f, 0, 0,
                            0.299f, 0.587f, 0.114f, 0, 0,
                            0.299f, 0.587f, 0.114f, 0, 0,
                            0, 0, 0, 1, 0
                        })
                    };
                    canvas.DrawBitmap(resizedBitmap, 0, 0, paint);
                }

                // Aplicar limiar adaptativo
                using var binaryBitmap = grayBitmap.Copy();
                for (int y = 0; y < grayBitmap.Height; y++)
                {
                    for (int x = 0; x < grayBitmap.Width; x++)
                    {
                        var color = grayBitmap.GetPixel(x, y);
                        var threshold = CalculateAdaptiveThreshold(grayBitmap, x, y);
                        var binColor = color.Red > threshold ? (byte)255 : (byte)0;
                        binaryBitmap.SetPixel(x, y, new SKColor(binColor, binColor, binColor));
                    }
                }

                // Converter para bytes
                using var outputStream = new MemoryStream();
                binaryBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar a imagem: {ex.Message}");
                throw;
            }
        }

        private byte CalculateAdaptiveThreshold(SKBitmap bitmap, int x, int y, int windowSize = 15)
        {
            int halfWindowSize = windowSize / 2;
            int startX = Math.Max(0, x - halfWindowSize);
            int endX = Math.Min(bitmap.Width - 1, x + halfWindowSize);
            int startY = Math.Max(0, y - halfWindowSize);
            int endY = Math.Min(bitmap.Height - 1, y + halfWindowSize);

            int sum = 0;
            int count = 0;

            for (int i = startX; i <= endX; i++)
            {
                for (int j = startY; j <= endY; j++)
                {
                    sum += bitmap.GetPixel(i, j).Red;
                    count++;
                }
            }

            return (byte)(sum / count);
        }

        private async Task<string> PerformOcrAsync(byte[] imageBytes)
        {
            try
            {
                using var pixImage = Pix.LoadFromMemory(imageBytes);
                using var page = _tesseractEngine.Process(pixImage);
                var text = page.GetText().Trim();

                // Encontrar todas as sequências de 5 dígitos
                var matches = Regex.Matches(text, @"\b\d{5}\b");

                if (matches.Count > 0)
                {
                    // Retornar todas as correspondências concatenadas em uma string
                    return string.Join(", ", matches.Select(m => m.Value));
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Erro ao realizar o OCR: {ex.Message}");
                throw;
            }
        }
    }
}
