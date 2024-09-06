using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;
using Tesseract;
using System.Text;
using System.Runtime.Intrinsics.X86;

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
                _tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789");
                _tesseractEngine.SetVariable("classify_bln_numeric_mode", "1");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inicializar Tesseract: {ex.Message}");
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

                    var allMatches = Regex.Matches(ocrResult.AllText, @"\d+");
                    var sequences = string.Concat(allMatches.Cast<Match>().Select(m => m.Value));


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
        private byte[] EnhanceContrast(byte[] imageBytes)
        {
            using var inputBitmap = SKBitmap.Decode(imageBytes);
            if (inputBitmap == null) throw new Exception("Erro ao carregar imagem.");

            using var contrastBitmap = new SKBitmap(inputBitmap.Width, inputBitmap.Height);
            using (var canvas = new SKCanvas(contrastBitmap))
            {
                var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateHighContrast(true, SKHighContrastConfigInvertStyle.NoInvert, 1.5f)
                };
                canvas.DrawBitmap(inputBitmap, 0, 0, paint);
            }

            using var outputStream = new MemoryStream();
            contrastBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
            return outputStream.ToArray();
        }
        private SKBitmap ApplyDenoising(SKBitmap inputBitmap)
        {
            using var blurredBitmap = new SKBitmap(inputBitmap.Width, inputBitmap.Height);
            using (var canvas = new SKCanvas(blurredBitmap))
            {
                var paint = new SKPaint
                {
                    ImageFilter = SKImageFilter.CreateBlur(2f, 2f) // Ajustar os valores de desfoque conforme necessário
                };
                canvas.DrawBitmap(inputBitmap, 0, 0, paint);
            }

            return blurredBitmap;
        }
        private SKBitmap ApplyThreshold(SKBitmap inputBitmap, byte threshold = 128)
        {
            var binaryBitmap = new SKBitmap(inputBitmap.Width, inputBitmap.Height);

            for (int y = 0; y < inputBitmap.Height; y++)
            {
                for (int x = 0; x < inputBitmap.Width; x++)
                {
                    var color = inputBitmap.GetPixel(x, y);
                    var gray = color.Red; // Como a imagem já está em escala de cinza
                    var binColor = gray > threshold ? (byte)255 : (byte)0;
                    binaryBitmap.SetPixel(x, y, new SKColor(binColor, binColor, binColor));
                }
            }

            return binaryBitmap;
        }

        private SKBitmap RemoveNoise(SKBitmap inputBitmap)
        {
            using var blurredBitmap = new SKBitmap(inputBitmap.Width, inputBitmap.Height);
            using (var canvas = new SKCanvas(blurredBitmap))
            {
                var paint = new SKPaint
                {
                    ImageFilter = SKImageFilter.CreateBlur(1f, 1f) // Aplicando suavização leve
                };
                canvas.DrawBitmap(inputBitmap, 0, 0, paint);
            }
            return blurredBitmap;
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

                    var allMatches = Regex.Matches(ocrResult.AllText, @"\d+");
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

                const float centralFraction = 0.6f; // Ajuste conforme necessário
                int centerX = inputBitmap.Width / 2;
                int centerY = inputBitmap.Height / 2;
                int cropWidth = (int)(inputBitmap.Width * centralFraction);
                int cropHeight = (int)(inputBitmap.Height * centralFraction);
                int cropX = centerX - cropWidth / 2;
                int cropY = centerY - cropHeight / 2;

                cropX = Math.Max(0, cropX);
                cropY = Math.Max(0, cropY);
                cropWidth = Math.Min(cropWidth, inputBitmap.Width - cropX);
                cropHeight = Math.Min(cropHeight, inputBitmap.Height - cropY);

                // Recortar a imagem para a região central
                using var croppedBitmap = new SKBitmap(cropWidth, cropHeight);
                using (var canvas = new SKCanvas(croppedBitmap))
                {
                    canvas.DrawBitmap(inputBitmap, new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight), new SKRect(0, 0, cropWidth, cropHeight));
                }

                // Convertendo a imagem recortada para escala de cinza
                using var grayBitmap = new SKBitmap(croppedBitmap.Width, croppedBitmap.Height, SKColorType.Gray8, SKAlphaType.Opaque);
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
                    canvas.DrawBitmap(croppedBitmap, 0, 0, paint);
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
                using var inputBitmap = SKBitmap.Decode(imageBytes);
                var rectangles = DetectRectangles(inputBitmap);

                var resultBuilder = new StringBuilder();

                foreach (var rect in rectangles)
                {
                    using var croppedBitmap = new SKBitmap((int)rect.Width, (int)rect.Height);
                    using (var canvas = new SKCanvas(croppedBitmap))
                    {
                        canvas.DrawBitmap(inputBitmap, rect, new SKRect(0, 0, rect.Width, rect.Height));
                    }

                    using var memoryStream = new MemoryStream();
                    croppedBitmap.Encode(memoryStream, SKEncodedImageFormat.Png, 100);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    using var pixImage = Pix.LoadFromMemory(memoryStream.ToArray());
                    using var page = _tesseractEngine.Process(pixImage);
                    var text = page.GetText().Trim();

                    resultBuilder.Append(text);
                }

                return resultBuilder.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao realizar o OCR: {ex.Message}");
                throw;
            }
        }

        private List<SKRect> DetectRectangles(SKBitmap bitmap)
        {
            var rectangles = new List<SKRect>();
            rectangles.Add(new SKRect(50, 50, 200, 100));

            return rectangles;
        }

    }
}
