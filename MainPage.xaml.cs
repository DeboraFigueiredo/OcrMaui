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
            try
            {
                var pickResult = await MediaPicker.Default.PickPhotoAsync();

                if (pickResult != null)
                {
                    using var imageAsStream = await pickResult.OpenReadAsync();
                    var imageAsBytes = new byte[imageAsStream.Length];
                    await imageAsStream.ReadAsync(imageAsBytes);

                    var enhancedImage = EnhanceImage(imageAsBytes);

                    var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(enhancedImage);

                    if (ocrResult.Success)
                    {
                        var meterNumber = ExtractMeterNumber(ocrResult.AllText);
                        if (!string.IsNullOrEmpty(meterNumber))
                        {
                            await DisplayAlert("OCR Result", $"Número do Medidor: {meterNumber}", "OK");
                        }
                        else
                        {
                            await DisplayAlert("No Success", "Número do medidor não encontrado", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlert("No Success", "No OCR possible", "OK");
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

                    var enhancedImage = EnhanceImage(imageAsBytes);

                    var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(enhancedImage);

                    if (ocrResult.Success)
                    {
                        var meterNumber = ExtractMeterNumber(ocrResult.AllText);
                        if (!string.IsNullOrEmpty(meterNumber))
                        {
                            await DisplayAlert("OCR Result", $"Número do Medidor: {meterNumber}", "OK");
                        }
                        else
                        {
                            await DisplayAlert("No Success", "Número do medidor não encontrado", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlert("No Success", "No OCR possible", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private SKBitmap AdjustBrightnessContrast(SKBitmap bitmap, float brightness, float contrast)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var processedBitmap = new SKBitmap(width, height);

            float t = (1.0f - contrast) / 2.0f + brightness;

            var contrastMatrix = new float[]
            {
                contrast, 0, 0, 0, t,
                0, contrast, 0, 0, t,
                0, 0, contrast, 0, t,
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

        private SKBitmap SharpenImage(SKBitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var processedBitmap = new SKBitmap(width, height);

            float[] kernel = {
        -1, -1, -1,
        -1,  9, -1,
        -1, -1, -1
    };

            using (var canvas = new SKCanvas(processedBitmap))
            {
                var paint = new SKPaint
                {
                    ImageFilter = SKImageFilter.CreateMatrixConvolution(
                        new SKSizeI(3, 3), 
                        kernel,              
                        1.0f,                  
                        0.0f,                 
                        new SKPointI(1, 1),    
                        SKMatrixConvolutionTileMode.Clamp,  
                        true               
                    )
                };
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }

            return processedBitmap;
        }


        private byte[] EnhanceImage(byte[] imageBytes)
        {
            using var inputStream = new SKMemoryStream(imageBytes);
            using var originalBitmap = SKBitmap.Decode(inputStream);

            var adjustedBitmap = AdjustBrightnessContrast(originalBitmap, 0.1f, 1.5f);
            var sharpenedBitmap = SharpenImage(adjustedBitmap);

            using var imageStream = new SKDynamicMemoryWStream();
            sharpenedBitmap.Encode(imageStream, SKEncodedImageFormat.Jpeg, 100);
            return imageStream.DetachAsData().ToArray();
        }

        private string ExtractMeterNumber(string text)
        {
            var regex = new Regex(@"\b\d{5,}\b"); 
            var match = regex.Match(text);
            return match.Success ? match.Value : null;
        }
    }
}
