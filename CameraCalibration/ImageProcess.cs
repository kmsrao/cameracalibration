using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;

namespace CameraCalibration

{
    public class ImageProcess
    {
        private string filepath;
        public ImageProcess(string _filepath)
        {
            this.filepath = _filepath;
        }

        public async Task GetHSV()
        {
            

            byte[] rgbValues = await GetRgbValuesFromBitmap(filepath);

            List<int> hue;
            List<int> saturation;
            List<int> values;

            ConvertToHSV(rgbValues,out hue,out saturation,out values);



        }

        private void ConvertToHSV(byte[] rgbValues, out List<int> hue, out List<int> saturation, out List<int> values)
        {
            hue = new List<int>();
            saturation = new List<int>();
            values = new List<int>();

            for (int i = 0; i < rgbValues.Length; i += 3) // Assuming 3 bytes per pixel (R, G, B)
            {
                byte r = rgbValues[i];
                byte g = rgbValues[i + 1];
                byte b = rgbValues[i + 2];

                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));

                int _hue = 0, _saturation = 0, _value = 0;
                if (max == min)
                    _hue = 0; // Grayscale
                else if (max == r)
                    _hue = (int)(60 * ((g - b) / (max - min)));
                else if (max == g)
                    _hue = (int)(60 * (2 + (b - r) / (max - min)));
                else
                    _hue = (int)(60 * (4 + (r - g) / (max - min)));

                _saturation = max == 0 ? 0 : (max - min) / max;
                _value = (int)(max / 255.0);


                hue.Add(_hue);
                saturation.Add(_saturation);
                values.Add(_value);

                // Now you have the HSV values for each pixel
                // You can store them or use them for further processing
            }
        }

        private async Task<byte[]> GetRgbValuesFromBitmap(string _filepath)
        {


            byte[] managedArray;

            Windows.Storage.Streams.IRandomAccessStream random = await Windows.Storage.Streams.RandomAccessStreamReference.CreateFromUri(new Uri(_filepath)).OpenReadAsync();
            
            Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(random);
            
            Windows.Graphics.Imaging.PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
            
            byte[] buffer = pixelData.DetachPixelData();

            // Convert IntPtr to Byte[]
            unsafe
            {
                fixed (byte* p = buffer)
                {
                    IntPtr bimapptr = (IntPtr)p;
                    managedArray = new byte[buffer.Length];
                    Marshal.Copy(bimapptr, managedArray, 0, buffer.Length);
                }
            }
                       

            return managedArray;

        }

    }
}