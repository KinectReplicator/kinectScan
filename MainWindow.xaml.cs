
namespace kinectScan
{
    using System;
    using System.IO;
    using System.Drawing;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
      
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmapDepth, colorBitmapFilter, colorBitmapNormal;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] colorPixels, colorPixels2, colorPixels3;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
           
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (null != this.sensor)
            {
                //Start the depth stream
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.colorPixels2 = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                //this.colorPixels3 = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                // This is the bitmap we'll display on-screen
                this.colorBitmapDepth = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorBitmapFilter = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
               // this.colorBitmapNormal = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);


                // Set the image we display to point to the bitmap where we'll put the image data
                this.KinectDepthView.Source = this.colorBitmapDepth;
                this.KinectFilterView.Source = this.colorBitmapFilter;
                //this.KinectNormalView.Source = this.colorBitmapFilter;

                // Add an event handler to be called whenever there is new depth frame data available
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
                this.sensor.DepthFrameReady += this.Bilateral_Filter;
                this.sensor.DepthFrameReady += this.Normal_Map;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Content = Properties.Resources.NoKinectReady;
                this.IR_Title.Content = "";
                this.Model_Title.Content = "";
                this.Bilateral_Title.Content = "";
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop the sensor!
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// Take in depth data
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            DepthImagePixel[] tempDepthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(tempDepthPixels);

                    // Get the min and max reliable depth for the current frame
                    double minDepth = Near_Filter_Slider.Value;
                    double maxDepth = Far_Filter_Slider.Value;
                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                                             
                        short depth = depthPixels[i].Depth;
                                          
                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                        // Write out blue byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.colorPixels[colorPixelIndex++] = intensity;
                      
                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    // Write the pixel data into our bitmap
                    this.colorBitmapDepth.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmapDepth.PixelWidth, this.colorBitmapDepth.PixelHeight),
                        this.colorPixels,
                        this.colorBitmapDepth.PixelWidth * sizeof(int), 0);
                
                }
            }
        }

        private void Bilateral_Filter(object sender, DepthImageFrameReadyEventArgs e)
        {
            DepthImagePixel[] tempDepthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {

                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(tempDepthPixels);
                     
                    // Get the min and max reliable depth for the current frame
                    double minDepth = Near_Filter_Slider.Value;
                    double maxDepth = Far_Filter_Slider.Value;
                    // Convert the depth to RGB
                    int colorPixelIndex = 0;

                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        tempDepthPixels[i].Depth = (Int16)(tempDepthPixels[i].Depth >= minDepth && tempDepthPixels[i].Depth <= maxDepth ? tempDepthPixels[i].Depth : 0);
                    }

                    for (int i = 641; i < this.depthPixels.Length - 641; ++i)
                    {

                        short depthaverage2 = (Int16)((tempDepthPixels[i - 1].Depth + (2 * tempDepthPixels[i].Depth) + tempDepthPixels[i + 1].Depth) / 4);

                        short depthaverage = (Int16)((tempDepthPixels[i - 640].Depth + (2 * depthaverage2) + tempDepthPixels[i + 640].Depth) / 4);

                        this.depthPixels[i].Depth = depthaverage;

                        byte intensity = (byte)(depthaverage >= minDepth && depthaverage <= maxDepth ? depthaverage : 0);

                        // Write out blue byte
                        this.colorPixels2[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.colorPixels2[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.colorPixels2[colorPixelIndex++] = intensity;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }


                    // Write the pixel data into our bitmap
                    this.colorBitmapFilter.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmapFilter.PixelWidth, this.colorBitmapFilter.PixelHeight),
                        this.colorPixels2,
                        this.colorBitmapFilter.PixelWidth * sizeof(int), 0);
                }
            }
        }

        private void Normal_Map(object sender, DepthImageFrameReadyEventArgs e)
        {
            DepthImagePixel[] tempDepthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

            Vector3D[] XYDepth = new Vector3D[this.depthPixels.Length];
            Vector3D[] normalMap = new Vector3D[this.depthPixels.Length];

            int colorPixelIndex = 0;
            
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {

                if (depthFrame != null)
                {
                    depthFrame.CopyDepthImagePixelDataTo(tempDepthPixels);

                    for (int x = 0; x < 640; ++x)
                    {

                        for (int y = 0; y < 480; ++y)
                        {
                            if (tempDepthPixels[x + y].Depth != 0)
                            {
                                XYDepth[x + y] = new Vector3D(x, y, tempDepthPixels[x + y].Depth);
                            }

                        }

                    }

                    for (int i = 0; i < this.depthPixels.Length - 641; ++i)
                    {
                        normalMap[i] = Vector3D.CrossProduct(XYDepth[i + 1], XYDepth[i + 640]);

                       // this.colorPixels3[colorPixelIndex++] = (byte)normalMap[i].X;

                        // Write out green byte
                        //this.colorPixels3[colorPixelIndex++] = (byte)normalMap[i].Y;
                        // Write out red byte                        
                        //this.colorPixels3[colorPixelIndex++] = (byte)normalMap[i].Z;
                        //++colorPixelIndex;

                    }


                    // Write the pixel data into our bitmap
                    //this.colorBitmapNormal.WritePixels(
                      //  new Int32Rect(0, 0, this.colorBitmapNormal.PixelWidth, this.colorBitmapNormal.PixelHeight),
                        //this.colorPixels3,
                        //this.colorBitmapNormal.PixelWidth * sizeof(int), 0);
                }
            }


        }

        private void Export_Model_Click(object sender, RoutedEventArgs e)
        {

            Process.Start("C:\\Users\\" + Environment.UserName + "\\Dropbox\\Capstone\\Slic3r\\slic3r-console.exe", "C:\\Users\\" + Environment.UserName + "\\Dropbox\\Capstone\\Slic3r\\objs\\Old_Key.obj $s");
            Process.Start("explorer.exe", string.Format("/select, \"{0}\"", "C:\\Users\\"+ Environment.UserName + "\\Dropbox\\Capstone\\Slic3r\\objs\\Old_Key.obj.gcode"));
        }
    }
}
