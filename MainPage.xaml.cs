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

                    var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

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

                    var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(imageAsBytes);

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
                        0.3f, 0.3f, 0.3f, 0, 0, // Red
                        0.3f, 0.3f, 0.3f, 0, 0, // Green
                        0.3f, 0.3f, 0.3f, 0, 0, // Blue
                        0, 0, 0, 1, 0  // Alpha
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

            var contrastMatrix = new float[]
            {
                contrast, 0, 0, 0, 0, // Red
                0, contrast, 0, 0, 0, // Green
                0, 0, contrast, 0, 0, // Blue
                0, 0, 0, 1, 0  // Alpha
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
            // Exemplo básico: aplicação de um filtro de desfoque gaussiano para reduzir o ruído
            var width = bitmap.Width;
            var height = bitmap.Height;
            var processedBitmap = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(processedBitmap))
            {
                var paint = new SKPaint
                {
                    ImageFilter = SKImageFilter.CreateBlur(2.0f, 2.0f) // Ajuste o raio do desfoque conforme necessário
                };
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }

            return processedBitmap;
        }
        private string ExtractMeterNumber(string text)
        {    
            var regex = new Regex(@"\d{1,5}"); 
            var match = regex.Match(text);
            return match.Success ? match.Value : null;
        }
    }
}
