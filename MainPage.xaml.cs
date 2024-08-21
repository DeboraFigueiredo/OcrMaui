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

            bitmap = RemoveNoise(bitmap);
            bitmap = AdjustContrast(bitmap, 1.5f);
            bitmap = ConvertToBlackAndWhite(bitmap);

            using var image = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
            var imageAsBytes = image.ToArray();

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

        private SKBitmap ConvertToBlackAndWhite(SKBitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var processedBitmap = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(processedBitmap))
            {
                var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                    {
                        0.3f, 0.3f, 0.3f, 0, 0,
                        0.3f, 0.3f, 0.3f, 0, 0,
                        0.3f, 0.3f, 0.3f, 0, 0,
                        0, 0, 0, 1, 0
                    })
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

        private string ExtractMeterNumber(string text)
        {
            var regex = new Regex(@"\b\d{5}\b"); 
            var match = regex.Match(text);
            return match.Success ? match.Value : null;
        }
    }
}
