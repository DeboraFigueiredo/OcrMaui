using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;

namespace OcrMaui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            OcrPlugin.Default.InitAsync();
        }

        protected async override void OnAppearing()
        {
            base.OnAppearing();
            await OcrPlugin.Default.InitAsync();
        }

        private async void OnCounterClicked(object sender, EventArgs e)
        {
            await ProcessImageFromFile();
        }

        private async void PictureBtn_Clicked(object sender, EventArgs e)
        {
            await ProcessImageFromCamera();
        }

        private async Task ProcessImageFromFile()
        {
            try
            {
                var pickResult = await MediaPicker.Default.PickPhotoAsync();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    var ocrResult = await ProcessImageAsync(imageAsStream);

                    await DisplayResult(ocrResult);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task ProcessImageFromCamera()
        {
            try
            {
                var pickResult = await MediaPicker.Default.CapturePhotoAsync();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    var ocrResult = await ProcessImageAsync(imageAsStream);

                    await DisplayResult(ocrResult);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task<string> ProcessImageAsync(Stream imageStream)
        {
            var bitmap = SKBitmap.Decode(imageStream);

            bitmap = Resize(bitmap, bitmap.Width / 2, bitmap.Height / 2); // Reduz o tamanho da imagem para melhorar a performance
            bitmap = SetGrayscale(bitmap); // Converte a imagem para escala de cinza
            bitmap = RemoveNoise(bitmap); // Remove o ruído da imagem
            bitmap = AdjustContrast(bitmap, 1.5f); // Ajusta o contraste para melhorar a legibilidade

            using var image = SKImage.FromBitmap(bitmap); // Corrigido
            using var encodedImage = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            var imageAsBytes = encodedImage.ToArray();

            var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

            if (ocrResult.Success)
            {
                return ExtractMeterNumber(ocrResult.AllText);
            }

            return null;
        }

        private async Task DisplayResult(string meterNumber)
        {
            if (!string.IsNullOrEmpty(meterNumber))
            {
                await DisplayAlert("OCR Result", $"Número do Medidor: {meterNumber}", "OK");
            }
            else
            {
                await DisplayAlert("No Success", "Número do medidor não encontrado", "OK");
            }
        }

        private SKBitmap Resize(SKBitmap bitmap, int newWidth, int newHeight)
        {
            var resizedBitmap = new SKBitmap(newWidth, newHeight);

            using (var canvas = new SKCanvas(resizedBitmap))
            {
                var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.High
                };

                canvas.DrawBitmap(bitmap, SKRect.Create(bitmap.Width, bitmap.Height), SKRect.Create(newWidth, newHeight), paint);
            }

            return resizedBitmap;
        }

        private SKBitmap SetGrayscale(SKBitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var grayBitmap = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(grayBitmap))
            {
                var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                    {
                        0.299f, 0.299f, 0.299f, 0, 0,
                        0.587f, 0.587f, 0.587f, 0, 0,
                        0.114f, 0.114f, 0.114f, 0, 0,
                        0, 0, 0, 1, 0
                    })
                };

                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }

            return grayBitmap;
        }

        private SKBitmap RemoveNoise(SKBitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var processedBitmap = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(processedBitmap))
            {
                var paint = new SKPaint
                {
                    ImageFilter = SKImageFilter.CreateBlur(1.0f, 1.0f)
                };
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }

            return processedBitmap;
        }

        private SKBitmap AdjustContrast(SKBitmap bitmap, float contrast)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var processedBitmap = new SKBitmap(width, height);

            float translation = (1 - contrast) * 128;

            var contrastMatrix = new float[]
            {
                contrast, 0, 0, 0, translation,
                0, contrast, 0, 0, translation,
                0, 0, contrast, 0, translation,
                0, 0, 0, 1, 0
            };

            using (var canvas = new SKCanvas(processedBitmap))
            {
                var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateColorMatrix(contrastMatrix)
                };
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }

            return processedBitmap;
        }

        private string ExtractMeterNumber(string text)
        {
            var regex = new Regex(@"\b\d{5,10}\b");
            var match = regex.Match(text);
            return match.Success ? match.Value : null;
        }
    }
}
