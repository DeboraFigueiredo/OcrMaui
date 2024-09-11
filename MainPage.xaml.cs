using Plugin.Maui.OCR;
using SkiaSharp;
using System.Text.RegularExpressions;
using Syncfusion.Maui.ImageEditor;
using Tesseract;

namespace OcrMaui
{
    public partial class MainPage : ContentPage
    {
        private TesseractEngine _tesseractEngine;

        public MainPage()
        {
            InitializeComponent();
            imageEditor.ImageSaving += ImageEditor_ImageSaving; // Adiciona o manipulador do evento de salvamento
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
            await LoadImageFromDevice();
        }

        private async void PictureBtn_Clicked(object sender, EventArgs e)
        {
            await CaptureImageFromCamera();
        }

        private async Task LoadImageFromDevice()
        {
            try
            {
                var pickResult = await MediaPicker.Default.PickPhotoAsync();
                if (pickResult != null)
                {
                    await LoadImageToEditor(pickResult);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task CaptureImageFromCamera()
        {
            try
            {
                var pickResult = await MediaPicker.Default.CapturePhotoAsync();
                if (pickResult != null)
                {
                    await LoadImageToEditor(pickResult);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task LoadImageToEditor(FileResult pickResult)
        {
            try
            {
                using var imageAsStream = await pickResult.OpenReadAsync();
                var imageAsBytes = new byte[imageAsStream.Length];
                await imageAsStream.ReadAsync(imageAsBytes);

                var memoryStream = new MemoryStream(imageAsBytes);
                imageEditor.Source = ImageSource.FromStream(() => memoryStream);
                imageEditor.IsVisible = true; // Exibe o editor para recorte
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void ImageEditor_ImageSaving(object sender, ImageSavingEventArgs e)
        {
            try
            {
                // Salva a imagem recortada e executa o OCR
                using var memoryStream = new MemoryStream();
                e.ImageStream.CopyTo(memoryStream);
                var croppedImageBytes = memoryStream.ToArray();

                var ocrResult = await OcrPlugin.Default.RecognizeTextAsync(croppedImageBytes);

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
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}
