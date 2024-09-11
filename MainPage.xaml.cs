using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;
using Tesseract;
using System.Text;
using System.Runtime.Intrinsics.X86;
using Syncfusion.Maui.ImageEditor;

namespace OcrMaui
{
    public partial class MainPage : ContentPage
    {
        private TesseractEngine _tesseractEngine;
        private MemoryStream _editedImageStream;
        private byte[] _editedImageBytes;
        public MainPage()
        {
            InitializeComponent();
            imageEditor.ImageSaving += OnImageSaving;
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

        private async Task<byte[]> ConvertStreamToByteArray(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private async void OnImageSaving(object sender, ImageSavingEventArgs args)
        {
            _editedImageBytes = await ConvertStreamToByteArray(args.ImageStream);
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

                    imageEditor.Source = ImageSource.FromStream(() => new MemoryStream(imageAsBytes));

                    // Wait until the image has been processed
                    bool isImageProcessed = false;

                    void OnImageSaved(object s, ImageSavingEventArgs args)
                    {
                        _editedImageBytes = ConvertStreamToByteArray(args.ImageStream).GetAwaiter().GetResult();
                        isImageProcessed = true;
                    }

                    imageEditor.ImageSaving += OnImageSaved;

                    // Poll for the completion of image processing
                    while (!isImageProcessed)
                    {
                        await Task.Delay(100); // Check every 100 ms
                    }

                    if (_editedImageBytes != null)
                    {
                        var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(_editedImageBytes);

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
                    else
                    {
                        await DisplayAlert("Error", "Edited image bytes are null.", "OK");
                    }

                    imageEditor.ImageSaving -= OnImageSaved; // Clean up the event handler
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

                // Define your designated area here (example values)
                int cropX = 100; // X-coordinate of the designated area
                int cropY = 100; // Y-coordinate of the designated area
                int cropWidth = 300; // Width of the designated area
                int cropHeight = 300; // Height of the designated area

                // Ensure cropping doesn't exceed the image dimensions
                cropX = Math.Max(0, cropX);
                cropY = Math.Max(0, cropY);
                cropWidth = Math.Min(cropWidth, inputBitmap.Width - cropX);
                cropHeight = Math.Min(cropHeight, inputBitmap.Height - cropY);

                using var croppedBitmap = new SKBitmap(cropWidth, cropHeight);
                using (var canvas = new SKCanvas(croppedBitmap))
                {
                    canvas.DrawBitmap(inputBitmap, new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight), new SKRect(0, 0, cropWidth, cropHeight));
                }

                using var outputStream = new MemoryStream();
                croppedBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while cropping image: {ex.Message}");
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