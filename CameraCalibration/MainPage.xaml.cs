using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using static System.Net.Mime.MediaTypeNames;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CameraCalibration
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private StorageFolder _captureFolder = null;

        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        private MediaCapture _mediaCapture;
        private bool _isInitialized;
        private bool _isPreviewing;
        private static string CameraName = "osmo";
        //private static string CameraName = "integ";



        public MainPage()
        {
            this.InitializeComponent();
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
            


        }


        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCameraAsync()
        {
            Debug.WriteLine("InitializeCameraAsync");

            var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
         
            _captureFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;


            if (_mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    Debug.WriteLine("No camera device found!");
                    return;
                }

                // Create MediaCapture and its settings
                _mediaCapture = new MediaCapture();

                // Register for a notification when video recording has reached the maximum time and when something goes wrong

                _mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await _mediaCapture.InitializeAsync(settings);
                    _isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }

                // If initialization succeeded, start the preview
                if (_isInitialized)
                {
                     await StartPreviewAsync();


                }
            }
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running
            _displayRequest.RequestActive();

            // Set the preview source in the UI and mirror it if necessary
            PreviewControl.Source = _mediaCapture;
            PreviewControl.FlowDirection = FlowDirection.LeftToRight;

            // Start the preview
            await _mediaCapture.StartPreviewAsync();

             
            _isPreviewing = true;
                        
        }

        /// <summary>
        /// Takes a photo to a StorageFile and adds rotation metadata to it
        /// </summary>
        /// <returns></returns>
        private async Task TakePhotoAsync()
        {
            string fileName = "SamplePhoto.jpg";
            
            var stream = new InMemoryRandomAccessStream();

            Debug.WriteLine("Taking photo...");
            await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

            try
            {
                var file = await _captureFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                await ReencodeAndSavePhotoAsync(stream, file);

                Debug.WriteLine("Photo taken! Saving to " + file.Path);

                            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                Debug.WriteLine("Exception when taking a photo: " + ex.ToString());
            }
             
        }

        private static async Task ReencodeAndSavePhotoAsync(IRandomAccessStream stream, StorageFile file)
        {
            using (var inputStream = stream)
            {
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    await encoder.FlushAsync();
                }
            }
        }

        /// <summary>
        /// Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
        /// </summary>
        /// <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
        /// <returns></returns>
        private async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            int count  = 0;
            while (true)
            {
                // Get available devices for capturing pictures
                var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                // Get the desired camera by panel
                DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

                foreach (var device in allVideoDevices)
                {
                    if (device.Name.ToLower().Contains(CameraName))
                    {
                        txt_Status.Visibility = Visibility.Collapsed;

                        desiredDevice = device;
                        return desiredDevice;
                    }
                }
                count ++;
                txt_Status.Text = "No Camera Found..Checking Again.  as of now " +count.ToString() + "  attempts are made to check for Pocket3 Camera";
                //< TextBlock Text = "Application is Waiting for Pocket3 Camera to be Connected " FontSize = "18" />

                await Task.Delay(5000);

                txt_Status.Text = "Application is Checking for Pocket3 Camera ";


            }

            // If there is no device mounted on the desired panel, return the first device found
            //return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            // Stop the preview
            _isPreviewing = false;
            await _mediaCapture.StopPreviewAsync();

            // Use the dispatcher because this method is sometimes called from non-UI threads
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Cleanup the UI
                PreviewControl.Source = null;

                // Allow the device screen to sleep now that the preview is stopped
                _displayRequest.RequestRelease();
            });
        }


        /// <summary>
        /// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            Debug.WriteLine("CleanupCameraAsync");

            if (_isInitialized)
            {


                if (_isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                _isInitialized = false;
            }

            if (_mediaCapture != null)
            {

                _mediaCapture.Failed -= MediaCapture_Failed;
                _mediaCapture.Dispose();
                _mediaCapture = null;
            }

            
        }


        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

            await CleanupCameraAsync();


        }

        void CloseCamera(object sender, DoubleTappedRoutedEventArgs e)
        {
            Windows.UI.Xaml.Application.Current.Exit();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // await InitializeCameraAsync();
            ImageProcess imageProcess = new ImageProcess("C:\\colorImage.jpg");
            imageProcess.GetHSV();
            
        }
    }
}
