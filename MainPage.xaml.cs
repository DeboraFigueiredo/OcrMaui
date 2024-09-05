using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;
using Tesseract;
using System.Text;

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
                var tessDataPath = Path.Combine(FileSystem.AppDataDirectory, "tessdata");

                _tesseractEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                _tesseractEngine.SetVariable("tessedit_pageseg_mode", "6");
                _tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Tesseract: {ex.Message}");
            }
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            await ProcessImageAsync();
        }

        private async void PictureBtn_Clicked(object sender, EventArgs e)
        {
            await ProcessImageAsync(capturePhoto: true);
        }

        private async Task ProcessImageAsync(bool capturePhoto = false)
        {
            try
            {
                var pickResult = capturePhoto ? await MediaPicker.Default.CapturePhotoAsync() : await MediaPicker.Default.PickPhotoAsync();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    var imageAsBytes = new byte[imageAsStream.Length];
                    await imageAsStream.ReadAsync(imageAsBytes);

                    var processedBytes = PreprocessImage(imageAsBytes);
                    var ocrResult = await PerformOcrAsync(processedBytes);

                    if (ocrResult == null)
                    {
                        await DisplayAlert("No Success", "No OCR possible", "OK");
                        return;
                    }

                    var allMatches = Regex.Matches(ocrResult, @"\d+");
                    var sequences = string.Join(", ", allMatches.Cast<Match>().Select(m => m.Value));

                    if (!string.IsNullOrEmpty(sequences))
                    {
                        await DisplayAlert("OCR Result", $"Sequências encontradas: {sequences}", "OK");
                    }
                    else
                    {
                        await DisplayAlert("No match", "Nenhum número encontrado.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
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

                using var contrastAdjustedBitmap = AdjustContrast(inputBitmap);
                using var binaryBitmap = BinarizeImage(contrastAdjustedBitmap);

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

        private SKBitmap AdjustContrast(SKBitmap bitmap)
        {
            // Ajuste de contraste simples
            var newBitmap = new SKBitmap(bitmap.Width, bitmap.Height);
            using (var canvas = new SKCanvas(newBitmap))
            {
                var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                    {
                        1.5f, 0, 0, 0, 0,
                        0, 1.5f, 0, 0, 0,
                        0, 0, 1.5f, 0, 0,
                        0, 0, 0, 1, 0
                    })
                };
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }
            return newBitmap;
        }

        private SKBitmap BinarizeImage(SKBitmap bitmap)
        {
            var otsuThreshold = CalculateOtsuThreshold(bitmap);
            var binaryBitmap = new SKBitmap(bitmap.Width, bitmap.Height);

            using (var canvas = new SKCanvas(binaryBitmap))
            {
                var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                    {
                        1f, 0, 0, 0, 0,
                        0, 1f, 0, 0, 0,
                        0, 0, 1f, 0, 0,
                        0, 0, 0, 1, 0
                    })
                };

                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    var binColor = color.Red > otsuThreshold ? (byte)255 : (byte)0;
                    binaryBitmap.SetPixel(x, y, new SKColor(binColor, binColor, binColor));
                }
            }

            return binaryBitmap;
        }

        private byte CalculateOtsuThreshold(SKBitmap bitmap)
        {
            int[] histogram = new int[256];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    histogram[color.Red]++;
                }
            }

            int totalPixels = bitmap.Width * bitmap.Height;
            float sum = 0;
            for (int i = 0; i < 256; i++)
            {
                sum += i * histogram[i];
            }

            float sumB = 0;
            int weightB = 0;
            int weightF = 0;
            float maxVariance = 0;
            byte threshold = 0;

            for (int t = 0; t < 256; t++)
            {
                weightB += histogram[t];
                if (weightB == 0) continue;

                weightF = totalPixels - weightB;
                if (weightF == 0) break;

                sumB += t * histogram[t];
                float meanB = sumB / weightB;
                float meanF = (sum - sumB) / weightF;

                float betweenVariance = weightB * weightF * (meanB - meanF) * (meanB - meanF);

                if (betweenVariance > maxVariance)
                {
                    maxVariance = betweenVariance;
                    threshold = (byte)t;
                }
            }

            return threshold;
        }

        private async Task<string> PerformOcrAsync(byte[] imageBytes)
        {
            try
            {
                using var pixImage = Pix.LoadFromMemory(imageBytes);
                using var page = _tesseractEngine.Process(pixImage);
                var text = page.GetText().Trim();

                // Pós-processamento para corrigir erros comuns
                return PostProcessText(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao realizar o OCR: {ex.Message}");
                throw;
            }
        }

        private string PostProcessText(string text)
        {
            // Corrige caracteres específicos e erros comuns
            text = Regex.Replace(text, @"\bO\b", "0"); // Corrige "O" isolado para "0"
            text = Regex.Replace(text, @"\bI\b", "1"); // Corrige "I" para "1"
            return text;
        }
    }
}
