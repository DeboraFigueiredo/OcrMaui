using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;
using Tesseract;

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
            var tessDataPath = Path.Combine(FileSystem.AppDataDirectory, "tessdata");

      
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
                        await DisplayAlert("No success", "No OCR possible", "OK");
                        return;
                    }

                    await DisplayAlert("OCR Result", ocrResult.AllText, "OK");
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
                        await DisplayAlert("No success", "No OCR possible", "OK");
                        return;
                    }

                    await DisplayAlert("OCR Result", ocrResult.AllText, "OK");
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

                    // Converter o Stream para um array de bytes
                    using var memoryStream = new MemoryStream();
                    await imageAsStream.CopyToAsync(memoryStream);
                    var imageAsBytes = memoryStream.ToArray();

                    // Pré-processar a imagem
                    var processedImageBytes = PreprocessImage(imageAsBytes);

                    // Usar OCR para realizar a leitura do texto
                    var ocrResult = await PerformOcrAsync(processedImageBytes);

                    // Filtrar e exibir apenas sequências de 5 números
                    var match = Regex.Match(ocrResult, @"\b\d{5}\b");
                    if (match.Success)
                    {
                        await DisplayAlert("OCR Result", match.Value, "OK");
                    }
                    else
                    {
                        await DisplayAlert("No match", "No sequence of 5 numbers found", "OK");
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
            SKBitmap input;
            using (var memoryStream = new MemoryStream(imageBytes))
            {
                input = SKBitmap.Decode(memoryStream);
            }

            // Converter para escala de cinza
            using var grayBitmap = new SKBitmap(input.Width, input.Height, SKColorType.Gray8, SKAlphaType.Opaque);
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
                canvas.DrawBitmap(input, 0, 0, paint);
            }

            // Aplicar filtro binário (threshold)
            using var binarizedBitmap = new SKBitmap(grayBitmap.Width, grayBitmap.Height, SKColorType.Gray8, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(binarizedBitmap))
            {
                var threshold = 128; // Valor de limiar (threshold) arbitrário
                for (int y = 0; y < grayBitmap.Height; y++)
                {
                    for (int x = 0; x < grayBitmap.Width; x++)
                    {
                        var color = grayBitmap.GetPixel(x, y);
                        var binColor = color.Red < threshold ? SKColors.Black : SKColors.White;
                        binarizedBitmap.SetPixel(x, y, binColor);
                    }
                }
            }

            using var outputStream = new MemoryStream();
            binarizedBitmap.Encode(outputStream, SKEncodedImageFormat.Png, 100);
            return outputStream.ToArray();
        }

        private async Task<string> PerformOcrAsync(byte[] imageBytes)
        {
            using var pixImage = Pix.LoadFromMemory(imageBytes);
            using var page = _tesseractEngine.Process(pixImage);
            var text = page.GetText().Trim();

            // Filtrar apenas as sequências de 5 números
            var matches = Regex.Matches(text, @"\b\d{5}\b");
            var filteredText = string.Join(" ", matches.Select(m => m.Value));
            return filteredText;
        }
    }
}
